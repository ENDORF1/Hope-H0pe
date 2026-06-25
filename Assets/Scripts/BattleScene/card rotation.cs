using UnityEngine;
using System.Collections;
using DG.Tweening;

/// <summary>
/// 控制卡牌卡面/卡背的显示
/// 通过从摄像机向卡面目标点射线检测来自动判断当前朝向
/// 也可通过 ShowFront() / ShowBack() 手动强制设置
/// </summary>
[ExecuteInEditMode]
public class BetterCardRotation : MonoBehaviour
{
    public RectTransform CardFront;
    public RectTransform CardBack;
    public RectTransform CardPanel;  // 挂有背景 Image 的 CardPanel
    public Transform targetFacePoint;
    public Collider col;

    private bool showingBack = false;
    private bool forceOverride = false;

    // FIX Bug1: 标记 Awake 执行前是否已被外部强制设置，防止 Awake/Start 覆盖
    private bool _initOverride = false;

    /// <summary>当前是否显示卡面（供 HoverPreview 读取）</summary>
    public bool IsShowingFront => !showingBack;

    /// <summary>卡牌已完成入场翻转，可以正常预览</summary>
    public bool IsFlipComplete { get; private set; } = false;

    /// <summary>正在播放翻转动画期间为 true，此时禁止激活预览</summary>
    public bool IsFlipping { get; private set; } = false;

    [Header("初始状态")]
    [SerializeField] private bool startShowingBack = false;  // 勾选后初始显示卡背

    [Header("翻转动画（部署区翻面用）")]
    [SerializeField] private float flipDuration = 0.35f;
    [SerializeField] private float flipDelay    = 0.15f;

    /// <summary>翻转动画总时长（供外部等待）</summary>
    public float FlipTotalDuration => flipDelay + flipDuration;

    void Awake()
    {
        // FIX Bug1: 若 Awake 前已被 ShowFront/ShowBack 调用，跳过初始化，不覆盖已设状态
        if (!_initOverride)
        {
            showingBack = startShowingBack;
            // 若初始显示卡背，锁定 forceOverride 防止 Update 自动检测覆盖
            if (startShowingBack) forceOverride = true;
            ApplyState();
        }
    }

    void Start()
    {
        // FIX Bug1: 删除原本和 Awake 重复的初始化逻辑，避免二次覆盖
        // （不再重复执行 showingBack = startShowingBack）
    }

    void Update()
    {
        // 手动强制时不做自动检测
        if (forceOverride) return;

        if (Camera.main == null || targetFacePoint == null || col == null
            || CardFront == null || CardBack == null) return;

        Vector3 dir = (targetFacePoint.position - Camera.main.transform.position).normalized;
        float dist  = Vector3.Distance(Camera.main.transform.position, targetFacePoint.position);

        RaycastHit[] hits = Physics.RaycastAll(Camera.main.transform.position, dir, dist);

        bool passedThrough = false;
        foreach (RaycastHit h in hits)
            if (h.collider == col) { passedThrough = true; break; }

        if (passedThrough != showingBack)
        {
            showingBack = passedThrough;
            ApplyState();
        }
    }

    // ─────────────────────────────────────────────────
    // 公开接口
    // ─────────────────────────────────────────────────

    /// <summary>强制显示卡面（速攻牌入手时调用）</summary>
    public void ShowFront()
    {
        // FIX Bug1: 设置 _initOverride，防止后续 Awake/Start 覆盖此状态
        _initOverride = true;
        forceOverride = true;
        showingBack   = false;
        ApplyState();
        Debug.Log($"[BetterCardRotation] ShowFront on {gameObject.name} (ID:{gameObject.GetInstanceID()}) | CardFront ID:{(CardFront != null ? CardFront.gameObject.GetInstanceID().ToString() : "null")} | CardFront parent: {(CardFront != null ? CardFront.transform.parent?.name : "null")}");
    }

    /// <summary>强制显示卡背（模块牌入部署区时调用）</summary>
    public void ShowBack()
    {
        // FIX Bug1: 同上，防止 Awake/Start 覆盖
        _initOverride = true;
        forceOverride = true;
        showingBack   = true;
        ApplyState();
    }

    /// <summary>翻牌阶段调用，恢复自动检测并翻到卡面</summary>
    public void FlipToFrontAndResume()
    {
        forceOverride  = false;
        showingBack    = false;
        IsFlipComplete = true;
        IsFlipping     = false;
        ApplyState();
    }

    /// <summary>
    /// 翻转动画开始前调用：隐藏卡背，准备翻面
    /// 动画转到90度时调用 ShowFront()，动画结束时调用 FlipToFrontAndResume()
    /// </summary>
    public void PrepareFlip()
    {
        forceOverride = true;
        IsFlipping    = true;

        HoverPreview hp = GetComponent<HoverPreview>();
        if (hp != null)
        {
            hp.ForceStopImmediate();

            if (hp.previewGameObject != null)
            {
                DOTween.Kill(hp.previewGameObject.transform);
                hp.previewGameObject.SetActive(false);
                hp.previewGameObject.transform.SetParent(null);
                hp.previewGameObject.transform.localScale    = Vector3.one;
                hp.previewGameObject.transform.localPosition = Vector3.zero;
                _detachedPreview           = hp.previewGameObject;
                _detachedPreviewOrigParent = transform;
            }
        }
        // ScaleX 翻转不需要隐藏任何内容，卡背自然压扁消失
    }

    // 翻转完成后调用，把预览对象归还给卡牌
    public void RestorePreviewParent()
    {
        if (_detachedPreview != null && _detachedPreviewOrigParent != null)
        {
            _detachedPreview.transform.SetParent(_detachedPreviewOrigParent, false);
            _detachedPreview.transform.localScale    = Vector3.one;
            _detachedPreview.transform.localPosition = Vector3.zero;
            _detachedPreview.transform.localRotation = Quaternion.identity;
            _detachedPreview = null;
        }
    }

    private GameObject _detachedPreview;
    private Transform  _detachedPreviewOrigParent;

    // ─────────────────────────────────────────────────
    // 带动画的翻面（部署区模块翻开时调用）
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 播放 ScaleX 压扁→换面→展开动画，和抽牌翻面完全一致。
    /// 由 DeploySlot.FlipFaceUp() 调用。
    /// </summary>
    public void FlipWithAnimation(bool toFront)
    {
        PrepareFlip();
        StartCoroutine(FlipRoutine(toFront));
    }

    private IEnumerator FlipRoutine(bool toFront)
    {
        if (flipDelay > 0f)
            yield return new WaitForSeconds(flipDelay);

        float half = flipDuration * 0.5f;
        Transform panel = CardPanel != null ? CardPanel : transform;
        Vector3 originalScale = panel.localScale;

        // 压扁
        yield return panel.DOScaleX(0f, half)
            .SetEase(Ease.InQuart).WaitForCompletion();

        // 切换卡面（ScaleX=0 时切换，无闪烁）
        if (toFront) ShowFront();
        else         ShowBack();

        // 展开
        yield return panel.DOScaleX(originalScale.x, half)
            .SetEase(Ease.OutBack).WaitForCompletion();

        panel.localScale = originalScale;

        if (toFront) FlipToFrontAndResume();
        RestorePreviewParent();
    }

    // ─────────────────────────────────────────────────
    private void ApplyState()
    {
        if (CardFront != null) CardFront.gameObject.SetActive(!showingBack);
        if (CardBack  != null) CardBack.gameObject.SetActive(showingBack);
    }
}