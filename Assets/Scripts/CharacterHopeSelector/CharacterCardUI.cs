using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// 单张角色卡的 UI 组件。
/// 职责：
///   1. 从 CharacterAsset 读取数据并刷新显示
///   2. 处理 Hover（3D 倾斜）和 Click（通知 Manager）
///   3. 提供飞入、翻面、消散、坠落等动画接口
/// 不负责选择逻辑，全部交给 CharacterSelectManager。
/// </summary>
public class CharacterCardUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // ─────────────────────────────────────────────────
    // Inspector 引用（由 Editor 脚本自动填，或手动拖入）
    // ─────────────────────────────────────────────────

    [Header("卡面 UI")]
    [Tooltip("角色名 TMP")]
    public TextMeshProUGUI nameText;

    [Tooltip("职位/角色描述 TMP")]
    public TextMeshProUGUI roleText;

    [Tooltip("HP 数值 TMP（左上角角标）")]
    public TextMeshProUGUI hpText;

    [Tooltip("立绘 Image（替换 sprite 即可）")]
    public Image portraitImage;

    [Tooltip("部署格容器 RectTransform（运行时填充平行四边形图标）")]
    public RectTransform deploySlotContainer;

    [Tooltip("牌库上限 TMP")]
    public TextMeshProUGUI deckCapacityText;

    [Tooltip("卡面水印（角色首字母）TMP")]
    public TextMeshProUGUI artWatermark;

    [Header("卡背")]
    [Tooltip("卡背根节点，翻面时隐藏")]
    public GameObject cardBackRoot;

    [Tooltip("卡背主文字 TMP")]
    public TextMeshProUGUI backTextMain;

    [Tooltip("卡面根节点，翻面时切换")]
    public GameObject cardFaceRoot;

    [Header("发光边框")]
    [Tooltip("光晕容器 CanvasGroup（四角 + 四边）")]
    public CanvasGroup glowGroup;

    [Header("倾斜容器")]
    [Tooltip("3D 倾斜效果作用的子容器（TiltRoot）")]
    public RectTransform tiltRoot;

    // ─────────────────────────────────────────────────
    // 动画参数
    // ─────────────────────────────────────────────────

    [Header("Hover 倾斜")]
    [SerializeField] private float hoverTiltY    = -12f;
    [SerializeField] private float hoverTiltX    =   6f;
    [SerializeField] private float hoverLiftY    =  14f;
    [SerializeField] private float hoverDuration =  0.18f;

    [Header("飞入动画")]
    [SerializeField] private float flyInDuration    = 0.55f;
    [SerializeField] private float overshootAmount  = 22f;
    [SerializeField] private float bounceDuration   = 0.28f;
    [SerializeField] private float flipDelay        = 0.15f;
    [SerializeField] private float flipDuration     = 0.28f;

    [Header("浮动呼吸")]
    [SerializeField] private float floatAmpMin  = 4f;
    [SerializeField] private float floatAmpMax  = 8f;
    [SerializeField] private float floatSpeedMin = 0.6f;
    [SerializeField] private float floatSpeedMax = 1.1f;

    [Header("随机微颤")]
    [SerializeField] private float jitterAmplitude = 3f;
    [SerializeField] private float jitterIntervalMin = 1f;
    [SerializeField] private float jitterIntervalMax = 3.5f;
    [SerializeField] private float jitterDecayTime   = 0.25f;

    [Header("消散动画")]
    [SerializeField] private float dismissDuration = 0.28f;

    [Header("退场动画（选中后）")]
    [SerializeField] private float exitDipDuration  = 0.22f;
    [SerializeField] private float exitDipAmount    = 28f;
    [SerializeField] private float exitFlyDuration  = 0.75f;
    [SerializeField] private float exitFlyDistance  = 320f;

    // ─────────────────────────────────────────────────
    // 运行时状态
    // ─────────────────────────────────────────────────

    public CharacterAsset Data { get; set; }

    /// <summary>在扇形中的最终落点（由 Manager 设置）</summary>
    public Vector2 FanPosition  { get; set; }
    /// <summary>在扇形中的最终旋转角（由 Manager 设置）</summary>
    public float   FanRotation  { get; set; }

    private CharacterSelectManager _manager;
    private RectTransform          _rect;
    private CanvasGroup            _cg;
    private bool                   _interactable = false;
    private bool                   _isFloating = false;
    private Tween                  _breathe;

    // 浮动呼吸参数（每张卡随机）
    private float _floatPhase;
    private float _floatSpeed;
    private float _floatAmp;

    // 随机微颤
    private float _jitterTimer;
    private float _jitterX, _jitterY, _jitterRot;
    private float _jitterElapsed;

    // ─────────────────────────────────────────────────
    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _cg   = GetComponent<CanvasGroup>();
        if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();

        if (glowGroup != null) glowGroup.alpha = 0f;
    }

    // ─────────────────────────────────────────────────
    // 初始化
    // ─────────────────────────────────────────────────

    public void Init(CharacterAsset asset, CharacterSelectManager manager)
    {
        Data     = asset;
        _manager = manager;
        RefreshDisplay();

        // 初始位置：屏幕右侧外
        _rect.anchoredPosition = new Vector2(Screen.width * 1.2f, 0);
        _rect.localRotation    = Quaternion.Euler(0, 0, 20f);
        _cg.alpha              = 0f;
        _interactable          = false;
    }

    // ─────────────────────────────────────────────────
    // 数据刷新
    // ─────────────────────────────────────────────────

    public void RefreshDisplay()
    {
        if (Data == null) return;

        if (nameText    != null) nameText.text = Data.CharacterName;
        if (roleText    != null) roleText.text = Data.CharacterNameEn;
        if (hpText      != null) hpText.text   = Data.MaxHealth.ToString();

        if (portraitImage != null && Data.Portrait != null)
        {
            portraitImage.sprite = Data.Portrait;
            portraitImage.color  = Color.white;
        }

        if (deploySlotContainer != null)
            BuildDeploySlots(Data.DeploySlots);

        if (deckCapacityText != null)
            deckCapacityText.text = Data.DeckCapacity.ToString();

        if (backTextMain != null && !string.IsNullOrEmpty(Data.BackTextMain))
            backTextMain.text = Data.BackTextMain;

        if (artWatermark != null && !string.IsNullOrEmpty(Data.CharacterNameEn))
            artWatermark.text = Data.CharacterNameEn.Length >= 2
                ? Data.CharacterNameEn.Substring(0, 2).ToUpper()
                : Data.CharacterNameEn.ToUpper();
    }

    // ─────────────────────────────────────────────────
    // 动画：飞入
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 从屏幕右侧飞入，冲过头弹回，然后翻面。
    /// delay：此卡开始飞入前的延迟（秒）
    /// onComplete：翻面完成后回调
    /// </summary>
    public void PlayFlyIn(float delay, System.Action onComplete = null)
    {
        DOVirtual.DelayedCall(delay, () =>
        {
            // 1. 淡入 + 飞到过冲位置
            _cg.DOFade(1f, 0.25f);
            _rect.DOAnchorPos(
                new Vector2(FanPosition.x - overshootAmount, FanPosition.y),
                flyInDuration
            ).SetEase(Ease.OutQuart).OnComplete(() =>
            {
                // 2. 弹回最终位置
                _rect.DOAnchorPos(FanPosition, bounceDuration)
                     .SetEase(Ease.OutBack)
                     .OnComplete(() =>
                {
                    _rect.DOLocalRotate(new Vector3(0, 0, FanRotation), bounceDuration * 0.5f);

                    // 3. 翻面
                    DOVirtual.DelayedCall(flipDelay, () =>
                    {
                        FlipToFace(() =>
                        {
                            _interactable = true;
                            StartFloat();
                            onComplete?.Invoke();
                        });
                    });
                });
            });

            _rect.DOLocalRotate(new Vector3(0, 0, FanRotation * 0.5f), flyInDuration)
                 .SetEase(Ease.OutQuart);
        });
    }

    // ─────────────────────────────────────────────────
    // 动画：翻面（卡背 → 卡面）
    // ─────────────────────────────────────────────────

    private void FlipToFace(System.Action onComplete = null)
    {
        if (tiltRoot == null) { onComplete?.Invoke(); return; }

        var seq = DOTween.Sequence();
        seq.Append(tiltRoot.DOScaleX(0f, flipDuration * 0.5f).SetEase(Ease.InQuart));
        seq.AppendCallback(() =>
        {
            if (cardBackRoot != null) cardBackRoot.SetActive(false);
            if (cardFaceRoot  != null) cardFaceRoot.SetActive(true);
        });
        seq.Append(tiltRoot.DOScaleX(1f, flipDuration * 0.5f).SetEase(Ease.OutBack));
        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>翻面：卡面 → 卡背（点击选角后，飞升前）</summary>
    public void FlipToBack(System.Action onComplete = null)
    {
        if (tiltRoot == null) { onComplete?.Invoke(); return; }

        var seq = DOTween.Sequence();
        seq.Append(tiltRoot.DOScaleX(0f, flipDuration * 0.5f).SetEase(Ease.InQuart));
        seq.AppendCallback(() =>
        {
            if (cardBackRoot != null) cardBackRoot.SetActive(true);
            if (cardFaceRoot  != null) cardFaceRoot.SetActive(false);
        });
        seq.Append(tiltRoot.DOScaleX(1f, flipDuration * 0.5f).SetEase(Ease.OutBack));
        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>立即翻到卡背（无动画，供入场初始化用）</summary>
    public void FlipToBackImmediate()
    {
        if (cardBackRoot != null) cardBackRoot.SetActive(true);
        if (cardFaceRoot  != null) cardFaceRoot.SetActive(false);
        if (tiltRoot != null) tiltRoot.localScale = Vector3.one;
    }

    // ─────────────────────────────────────────────────
    // 动画：浮动呼吸 + 随机微颤（Update 驱动，复刻 HTML）
    // ─────────────────────────────────────────────────

    public void StartFloat()
    {
        DOTween.Kill(_rect);
        _isFloating  = true;
        _floatPhase  = Random.Range(0f, Mathf.PI * 2f);
        _floatSpeed  = Random.Range(floatSpeedMin, floatSpeedMax);
        _floatAmp    = Random.Range(floatAmpMin, floatAmpMax);
        _jitterTimer = Random.Range(jitterIntervalMin, jitterIntervalMax);
        _jitterElapsed = jitterDecayTime;
    }

    public void StopFloat()
    {
        _isFloating = false;
        DOTween.Kill(_rect);
        _rect.anchoredPosition = FanPosition;
        _rect.localRotation = Quaternion.Euler(0f, 0f, FanRotation);
    }

    void Update()
    {
        if (!_isFloating) return;

        float t = Time.time;
        float fy = Mathf.Sin(t * _floatSpeed + _floatPhase) * _floatAmp;
        float fr = Mathf.Sin(t * _floatSpeed * 0.7f + _floatPhase + 1f) * 1.2f;

        // 随机微颤
        _jitterTimer -= Time.deltaTime;
        if (_jitterTimer <= 0f && _jitterElapsed >= jitterDecayTime)
        {
            _jitterX = (Random.value - 0.5f) * jitterAmplitude;
            _jitterY = (Random.value - 0.5f) * jitterAmplitude;
            _jitterRot = (Random.value - 0.5f) * 1.5f;
            _jitterElapsed = 0f;
            _jitterTimer = Random.Range(jitterIntervalMin, jitterIntervalMax);
        }

        // 微颤衰减
        _jitterElapsed += Time.deltaTime;
        float decay = Mathf.Clamp01(1f - _jitterElapsed / jitterDecayTime);
        float jx = _jitterX * decay;
        float jy = _jitterY * decay;
        float jr = _jitterRot * decay;

        _rect.anchoredPosition = new Vector2(FanPosition.x + jx, FanPosition.y + fy + jy);
        _rect.localRotation = Quaternion.Euler(0f, 0f, FanRotation + fr + jr);
    }

    // ─────────────────────────────────────────────────
    // 动画：消散（未选中的卡）
    // ─────────────────────────────────────────────────

    public void PlayDismiss(float delay = 0f)
    {
        StopFloat();
        _interactable = false;

        DOVirtual.DelayedCall(delay, () =>
        {
            float dir = FanPosition.x < 0 ? -1f : 1f;
            _rect.DOAnchorPos(
                new Vector2(FanPosition.x + dir * 60f, FanPosition.y + 30f),
                dismissDuration
            ).SetEase(Ease.InQuart);
            _rect.DOLocalRotate(
                new Vector3(0, 0, FanRotation + dir * 8f),
                dismissDuration
            );
            _cg.DOFade(0f, dismissDuration * 0.85f);
        });
    }

    // ─────────────────────────────────────────────────
    // 动画：退场（根据 ExitAnimationAsset 决定方向与参数）
    // ─────────────────────────────────────────────────

    public void PlayExit(System.Action onComplete = null)
    {
        StopFloat();
        _interactable = false;

        // 读取参数：角色资产 > 默认值
        var anim = Data != null ? Data.ExitAnimation : null;
        bool ascending  = anim == null || anim.direction == ExitAnimationAsset.Direction.Ascend;
        float dipDur    = anim != null ? anim.dipDuration       : exitDipDuration;
        float dipAmt    = anim != null ? anim.dipAmount         : exitDipAmount;
        float dipScale  = anim != null ? anim.dipScale          : 0.94f;
        float flyDur    = anim != null ? anim.flyDuration       : exitFlyDuration;
        float flyDst    = anim != null ? anim.flyDistance       : exitFlyDistance;
        Ease  dipEase   = anim != null ? anim.dipEase           : (ascending ? Ease.InBack : Ease.OutBack);
        Ease  flyEase   = anim != null ? anim.flyEase           : (ascending ? Ease.OutQuart : Ease.InQuart);
        float fadeDelay = anim != null ? anim.fadeDelayRatio    : 0.35f;
        float fadeDur   = anim != null ? anim.fadeDurationRatio : 0.6f;

        // 如果是飞升且未指定距离：自动计算飞出画面上方
        if (ascending && flyDst <= 0f)
        {
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null)
            {
                float canvasH = ((RectTransform)c.transform).rect.height;
                float stageTopY = canvasH;
                if (transform.parent != null)
                {
                    var st = transform.parent as RectTransform;
                    if (st != null) stageTopY = canvasH * st.anchorMax.y + st.anchoredPosition.y;
                }
                flyDst = Mathf.Max(exitFlyDistance,
                    stageTopY - _rect.anchoredPosition.y + _rect.rect.height + 120f);
            }
        }

        float dipDir = ascending ? -1f : 1f;   // 飞升先沉，坠落先扬
        float flyDir = ascending ? 1f : -1f;

        var seq = DOTween.Sequence();
        seq.Append(_rect.DOAnchorPosY(FanPosition.y + dipAmt * dipDir, dipDur)
            .SetEase(dipEase));
        seq.Join(_rect.DOScale(dipScale, dipDur));

        seq.Append(_rect.DOAnchorPosY(FanPosition.y + flyDst * flyDir, flyDur)
            .SetEase(flyEase));
        seq.Join(_cg.DOFade(0f, flyDur * fadeDur).SetDelay(flyDur * fadeDelay));

        seq.OnComplete(() => onComplete?.Invoke());
    }

    /// <summary>飞升退场（向上飞出，Hope 用）</summary>
    public void PlayAscend(System.Action onComplete = null) => PlayExit(onComplete);

    /// <summary>下沉退场（向下坠落，Void 用）</summary>
    public void PlaySink(System.Action onComplete = null) => PlayExit(onComplete);

    // ─────────────────────────────────────────────────
    // 发光边框
    // ─────────────────────────────────────────────────

    public void SetGlow(bool on)
    {
        _breathe?.Kill();
        if (glowGroup == null) return;

        if (on)
        {
            glowGroup.DOFade(1f, 0.2f);
            _breathe = glowGroup.DOFade(0.55f, 0.8f).SetDelay(0.3f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else
        {
            glowGroup.DOFade(0f, 0.15f);
        }
    }

    private void BuildDeploySlots(int count)
    {
        // 清理旧图标
        foreach (Transform child in deploySlotContainer)
            Destroy(child.gameObject);

        for (int i = 0; i < count; i++)
        {
            var slot = new GameObject($"Slot_{i}", typeof(RectTransform), typeof(Image));
            slot.transform.SetParent(deploySlotContainer, false);
            var rt  = slot.GetComponent<RectTransform>();
            var img = slot.GetComponent<Image>();

            rt.sizeDelta  = new Vector2(12f, 22f);
            rt.localRotation = Quaternion.Euler(0f, 0f, 12f);
            img.color     = Data.Faction == TitleScreenManager.Faction.Hope
                ? new Color(0.12f, 0.56f, 1f, 0.7f)
                : new Color(0.86f, 0.20f, 0.20f, 0.7f);
            img.raycastTarget = false;
        }
    }

    // ─────────────────────────────────────────────────
    // Pointer 事件
    // ─────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData _)
    {
        if (!_interactable || tiltRoot == null) return;
        StopFloat();
        // 透视感：X 轴微缩
        tiltRoot.DOScaleX(0.94f, hoverDuration).SetEase(Ease.OutQuart);
        tiltRoot.DOScaleY(0.97f, hoverDuration).SetEase(Ease.OutQuart);
        tiltRoot.DOLocalRotate(new Vector3(hoverTiltX, hoverTiltY, 0f), hoverDuration);
        _rect.DOAnchorPosY(FanPosition.y + hoverLiftY, hoverDuration).SetEase(Ease.OutQuart);
        SetGlow(true);
        _manager?.OnCardHovered(this);
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (!_interactable || tiltRoot == null) return;
        tiltRoot.DOScaleX(1f, hoverDuration);
        tiltRoot.DOScaleY(1f, hoverDuration);
        tiltRoot.DOLocalRotate(Vector3.zero, hoverDuration);
        _rect.DOAnchorPosY(FanPosition.y, hoverDuration).SetEase(Ease.OutQuart)
             .OnComplete(StartFloat);
        SetGlow(false);
        _manager?.OnCardUnhovered();
    }

    public void OnPointerClick(PointerEventData _)
    {
        if (!_interactable) return;
        _manager?.OnCardClicked(this);
    }

    // ─────────────────────────────────────────────────
    void OnDestroy()
    {
        _breathe?.Kill();
        _isFloating = false;
        DOTween.Kill(_rect);
        DOTween.Kill(_cg);
        if (tiltRoot != null) DOTween.Kill(tiltRoot);
    }
}
