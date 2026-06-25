using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;

public class HoverPreview : MonoBehaviour
{
    // PUBLIC FIELDS
    public GameObject TurnThisOffWhenPreviewing;
    public Vector3 TargetPosition;
    public float TargetScale = 2f;
    public GameObject previewGameObject;
    public bool ActivateInAwake = false;

    [Header("瞄准高亮（可选）")]
    [Tooltip("拖拽瞄准时随 Preview 显示的 CardGlow Image。ForceShow 时淡入，ForceHide 时淡出。")]
    public Image previewCardGlow;

    [Header("手牌可用高亮")]
    [Tooltip("小卡（原卡）上的 CardFaceGlowImage，速攻窗口期间绿色常亮。")]
    public Image smallCardGlow;
    [Tooltip("大卡（previewGameObject）上的 CardFaceGlowImage，预览弹出时绿色常亮，收起时熄灭。")]
    public Image previewCardFaceGlow;

    [SerializeField] private float glowFadeIn  = 0.15f;
    [SerializeField] private float glowPulseMin   = 0.5f;
    [SerializeField] private float glowPulseSpeed = 1.0f;

    [Header("动画平滑设置")]
    [SerializeField] private float animationDuration = 0.35f;
    [SerializeField] private Ease moveEase  = Ease.OutQuint;
    [SerializeField] private float overshoot = 1.2f;

    /// <summary>
    /// 设为 true 后，PreviewThisObject 不会关闭 smallCardGlow。
    /// 用于肖像被拖拽瞄准时，避免 ForceShow 把 bodyCardGlow 关掉。
    /// </summary>
    public bool SkipSmallCardGlowOnPreview = false;

    [Header("额外Canvas提升")]
    [Tooltip("预览激活时将此 Canvas 一并提升到 Above Above Everything 层")]
    public Canvas ExtraCanvasToLift;

    [Header("预览卡背覆盖（无 BetterCardRotation 时使用）")]
    [Tooltip("填写 previewGameObject 内的卡背 GameObject（如 LowerPortraitCardBack），有值时替代名字查找")]
    public GameObject previewCardBackOverride;

    private string _originalDroneCanvasSortingLayer;
    private int    _originalDroneCanvasSortingOrder;
    private bool   _droneCanvasLifted = false;

    // PRIVATE FIELDS
    private static HoverPreview currentlyViewing = null;
    private Tweener  moveTween;
    private Tweener  scaleTween;
    private Sequence previewSequence;
    private Sequence returnSequence;

    private Canvas _cardCanvas;
    private int    _originalSortingOrder;
    private string _originalSortingLayer;

    private int _originalSiblingIndex;

    private bool _forcedOpen = false;
    public bool IsForcedOpen => _forcedOpen;
    private System.Action _pendingHideCallback = null;

    private bool _locked = false;
    public void Lock()   => _locked = true;
    public void Unlock() => _locked = false;

    /// <summary>任意预览开始时触发，传出当前 HoverPreview 实例，供 KeywordTooltipPanel 等订阅</summary>
    public static event System.Action<HoverPreview> OnAnyPreviewStarted;

    /// <summary>预览收起后触发，供 GameManager 刷新手牌高亮状态</summary>
    public static event System.Action OnAnyPreviewStopped;

    public float AnimationDuration => animationDuration;
    public Ease  MoveEase          => moveEase;

    // PROPERTIES
    private static bool _PreviewsAllowed = true;
    public static bool PreviewsAllowed
    {
        get => _PreviewsAllowed;
        set
        {
            _PreviewsAllowed = value;
            if (!_PreviewsAllowed) StopAllPreviews();
        }
    }

    private bool _thisPreviewEnabled = false;
    public bool ThisPreviewEnabled
    {
        get => _thisPreviewEnabled;
        set
        {
            _thisPreviewEnabled = value;
            if (!_thisPreviewEnabled) StopThisPreview();
        }
    }

    public bool OverCollider { get; set; }

    // ─────────────────────────────────────────────────
    void Awake()
    {
        ThisPreviewEnabled = ActivateInAwake;
        if (previewGameObject == null)
            previewGameObject = gameObject;
        DOTween.defaultEaseType = Ease.OutQuint;
    }

    void OnEnable()  { OverCollider = false; }
    void OnDisable() { StopThisPreview(); }
    void OnDestroy() { KillAllTweens(); }

    void OnMouseEnter()
    {
        OverCollider = true;
        if (PreviewsAllowed && ThisPreviewEnabled)
            PreviewThisObject();
    }

    void OnMouseExit()
    {
        OverCollider = false;
        if (_forcedOpen) return;
        if (currentlyViewing == this)
        {
            StopThisPreview();
            currentlyViewing = null;
        }
    }

    // ─────────────────────────────────────────────────

    void PreviewThisObject()
    {
        if (currentlyViewing != null && currentlyViewing != this)
            currentlyViewing.StopThisPreview();

        currentlyViewing = this;

        if (previewGameObject == null) return;

        BetterCardRotation rot = GetComponent<BetterCardRotation>();
        if (rot != null && rot.IsFlipping && !_forcedOpen) return;

        if (TurnThisOffWhenPreviewing != null)
            TurnThisOffWhenPreviewing.SetActive(false);

        _cardCanvas = GetComponentInParent<Canvas>();
        if (_cardCanvas != null)
        {
            Canvas root = _cardCanvas.rootCanvas;
            _originalSortingLayer = root.sortingLayerName;
            _originalSortingOrder = root.sortingOrder;
            root.sortingLayerName = "Above Above Everything";
            root.sortingOrder     = 0;
        }

        _originalSiblingIndex = transform.GetSiblingIndex();
        transform.SetAsLastSibling();

        DroneDeployZone droneZone = GetComponentInParent<DroneDeployZone>();
        if (droneZone != null)
            droneZone.BringDroneToTop(gameObject);

        // 预览激活时提升额外 Canvas
        _droneCanvasLifted = false;
        if (ExtraCanvasToLift != null)
        {
            _originalDroneCanvasSortingLayer = ExtraCanvasToLift.sortingLayerName;
            _originalDroneCanvasSortingOrder = ExtraCanvasToLift.sortingOrder;
            ExtraCanvasToLift.sortingLayerName = "Above Above Everything";
            ExtraCanvasToLift.sortingOrder     = 1;
            _droneCanvasLifted = true;
        }

        previewGameObject.SetActive(true);
        previewGameObject.transform.localScale    = Vector3.one;
        previewGameObject.transform.localPosition = Vector3.zero;
        previewGameObject.transform.localRotation = Quaternion.identity;

        // SkipSmallCardGlowOnPreview が true の場合は smallCardGlow を触らない
        if (!SkipSmallCardGlowOnPreview && smallCardGlow != null && smallCardGlow.enabled)
        {
            smallCardGlow.enabled = false;
            if (previewCardFaceGlow != null)
            {
                previewCardFaceGlow.color   = smallCardGlow.color;
                previewCardFaceGlow.enabled = true;
            }
        }

        bool showFront = rot != null && rot.IsShowingFront;

        BetterCardRotation previewRot = previewGameObject.GetComponentInChildren<BetterCardRotation>(true);
        if (previewRot != null)
        {
            if (showFront) previewRot.ShowFront();
            else           previewRot.ShowBack();
        }
        else
        {
            Transform  cardface   = FindDeepChild(previewGameObject.transform, "cardface");
            GameObject cardBackGo = previewCardBackOverride != null
                ? previewCardBackOverride
                : FindDeepChild(previewGameObject.transform, "cardback")?.gameObject;
            if (cardface   != null) cardface.gameObject.SetActive(showFront);
            if (cardBackGo != null) cardBackGo.SetActive(!showFront);
        }

        KillAllTweens();

        previewSequence = DOTween.Sequence();
        previewSequence.Append(
            previewGameObject.transform.DOLocalMove(TargetPosition, animationDuration)
                .SetEase(moveEase, overshoot));
        previewSequence.Join(
            previewGameObject.transform.DOScale(TargetScale, animationDuration)
                .SetEase(moveEase, overshoot));

        // 通知订阅者（如 KeywordTooltipPanel）预览已开始
        OnAnyPreviewStarted?.Invoke(this);
    }

    // ─────────────────────────────────────────────────

    void StopThisPreview()
    {
        KillAllTweens();

        if (previewGameObject == null) return;

        bool previewGlowWasActive = previewCardFaceGlow != null && previewCardFaceGlow.enabled;
        if (previewCardFaceGlow != null)
        {
            if (!SkipSmallCardGlowOnPreview && smallCardGlow != null && previewGlowWasActive)
            {
                smallCardGlow.color   = previewCardFaceGlow.color;
                smallCardGlow.enabled = true;
            }
            previewCardFaceGlow.enabled = false;
        }

        returnSequence = DOTween.Sequence();
        returnSequence.Append(
            previewGameObject.transform.DOLocalMove(Vector3.zero, animationDuration * 0.7f)
                .SetEase(Ease.InQuint));
        returnSequence.Join(
            previewGameObject.transform.DOScale(Vector3.one, animationDuration * 0.7f)
                .SetEase(Ease.InQuint));
        returnSequence.OnComplete(() =>
        {
            previewGameObject.SetActive(false);

            if (TurnThisOffWhenPreviewing != null)
                TurnThisOffWhenPreviewing.SetActive(true);

            if (_cardCanvas != null)
            {
                Canvas root = _cardCanvas.rootCanvas;
                root.sortingLayerName = _originalSortingLayer;
                root.sortingOrder     = _originalSortingOrder;
            }

            if (_droneCanvasLifted && ExtraCanvasToLift != null)
            {
                ExtraCanvasToLift.sortingLayerName = _originalDroneCanvasSortingLayer;
                ExtraCanvasToLift.sortingOrder     = _originalDroneCanvasSortingOrder;
                _droneCanvasLifted = false;
            }

            transform.SetSiblingIndex(_originalSiblingIndex);

            _pendingHideCallback?.Invoke();
            _pendingHideCallback = null;

            // 通知 GameManager 刷新手牌高亮，修复预览期间 glow 状态可能残留的问题
            OnAnyPreviewStopped?.Invoke();
        });
    }

    // ─────────────────────────────────────────────────

    private void KillAllTweens()
    {
        if (moveTween      != null && moveTween.IsActive())      moveTween.Kill();
        if (scaleTween     != null && scaleTween.IsActive())     scaleTween.Kill();
        if (previewSequence!= null && previewSequence.IsActive())previewSequence.Kill();
        if (returnSequence != null && returnSequence.IsActive())
        {
            returnSequence.Kill();
            if (TurnThisOffWhenPreviewing != null)
                TurnThisOffWhenPreviewing.SetActive(true);
        }
        moveTween = null; scaleTween = null; previewSequence = null; returnSequence = null;
    }

    public void ForceStopImmediate()
    {
        KillAllTweens();

        if (previewGameObject != null)
        {
            DOTween.Kill(previewGameObject.transform);
            previewGameObject.SetActive(false);
            previewGameObject.transform.localScale    = Vector3.one;
            previewGameObject.transform.localPosition = Vector3.zero;
        }

        if (TurnThisOffWhenPreviewing != null)
            TurnThisOffWhenPreviewing.SetActive(true);

        bool previewGlowWasActive = previewCardFaceGlow != null && previewCardFaceGlow.enabled;
        if (previewCardFaceGlow != null)
        {
            if (!SkipSmallCardGlowOnPreview && smallCardGlow != null && previewGlowWasActive)
            {
                smallCardGlow.color   = previewCardFaceGlow.color;
                smallCardGlow.enabled = true;
            }
            previewCardFaceGlow.enabled = false;
        }

        if (_cardCanvas != null)
        {
            Canvas root = _cardCanvas.rootCanvas;
            root.sortingLayerName = _originalSortingLayer;
            root.sortingOrder     = _originalSortingOrder;
        }

        if (_droneCanvasLifted && ExtraCanvasToLift != null)
        {
            ExtraCanvasToLift.sortingLayerName = _originalDroneCanvasSortingLayer;
            ExtraCanvasToLift.sortingOrder     = _originalDroneCanvasSortingOrder;
            _droneCanvasLifted = false;
        }

        transform.SetSiblingIndex(_originalSiblingIndex);

        _locked     = false;
        _forcedOpen = false;

        if (currentlyViewing == this)
            currentlyViewing = null;

        OnAnyPreviewStopped?.Invoke();
    }

    private static void StopAllPreviews()
    {
        if (currentlyViewing != null)
        {
            currentlyViewing.StopThisPreview();
            currentlyViewing = null;
        }
    }

    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name.ToLower() == name.ToLower()) return child;
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ─────────────────────────────────────────────────
    // 强制显示 / 隐藏
    // ─────────────────────────────────────────────────

    public void ForceShow(bool ignoreLock = false)
    {
        if (_locked && !ignoreLock) return;
        _forcedOpen         = true;
        _thisPreviewEnabled = true;
        PreviewThisObject();
        StartPreviewGlow();

        if (previewCardFaceGlow != null)
        {
            Color cyan = new Color(0f, 0.922f, 1f, 1f);
            previewCardFaceGlow.color   = cyan;
            previewCardFaceGlow.enabled = true;
        }
    }

    /// <summary>强制弹出预览，但不添加任何光效</summary>
    public void ForceShowNoGlow()
    {
        _forcedOpen         = true;
        _thisPreviewEnabled = true;
        PreviewThisObject();

        // 关闭所有光效
        StopPreviewGlow();
        if (previewCardFaceGlow != null)
            previewCardFaceGlow.enabled = false;
        if (smallCardGlow != null)
        {
            smallCardGlow.DOKill();
            smallCardGlow.enabled = false;
        }
    }

    public void ForceHide(System.Action onComplete = null)
    {
        _forcedOpen          = false;
        _thisPreviewEnabled  = ActivateInAwake;
        _pendingHideCallback = onComplete;

        if (previewCardFaceGlow != null)
            previewCardFaceGlow.enabled = false;

        StopThisPreview();
        if (currentlyViewing == this)
            currentlyViewing = null;
    }

    public void SetPreviewCardFaceGlowColor(Color color)
    {
        if (previewCardFaceGlow == null) return;
        previewCardFaceGlow.color   = color;
        previewCardFaceGlow.enabled = true;
    }

    // ── previewCardGlow 控制 ──────────────────────────

    private void StartPreviewGlow()
    {
        if (previewCardGlow == null) return;
        Debug.Log($"[HoverPreview] StartPreviewGlow 被调用 on {gameObject.name}\n{System.Environment.StackTrace}");
        previewCardGlow.DOKill();
        Color c = previewCardGlow.color; c.a = 0f; previewCardGlow.color = c;
        previewCardGlow.DOFade(1f, glowFadeIn).OnComplete(() =>
        {
            if (previewCardGlow != null)
                previewCardGlow.DOFade(glowPulseMin, glowPulseSpeed)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        });
    }

    public void SetPreviewGlowColor(Color color)
    {
        if (previewCardGlow == null) return;
        previewCardGlow.DOKill();
        previewCardGlow.gameObject.SetActive(true);
        previewCardGlow.color = new Color(color.r, color.g, color.b, 0f);
        previewCardGlow.DOFade(1f, glowFadeIn).OnComplete(() =>
        {
            if (previewCardGlow != null)
            {
                previewCardGlow.color = new Color(color.r, color.g, color.b, 1f);
                previewCardGlow.DOFade(glowPulseMin, glowPulseSpeed)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
            }
        });
    }

    public void StopPreviewGlow()
    {
        if (previewCardGlow == null) return;
        previewCardGlow.DOKill();
        previewCardGlow.DOFade(0f, animationDuration * 0.7f).SetEase(Ease.InOutQuad);
    }

    public void PreviewGlowFull()
    {
        if (previewCardGlow == null) return;
        previewCardGlow.DOKill();
        previewCardGlow.DOFade(1f, 0.1f);
    }

    public void PreviewGlowDim()
    {
        if (previewCardGlow == null) return;
        previewCardGlow.DOKill();
        previewCardGlow.DOFade(0.15f, 0.15f);
    }

    public void PreviewGlowBreath()
    {
        if (previewCardGlow == null) return;
        previewCardGlow.DOKill();
        Color c = previewCardGlow.color; c.a = 0f; previewCardGlow.color = c;
        previewCardGlow.DOFade(1f, glowFadeIn).OnComplete(() =>
        {
            if (previewCardGlow != null)
                previewCardGlow.DOFade(glowPulseMin, glowPulseSpeed)
                    .SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine);
        });
    }
}