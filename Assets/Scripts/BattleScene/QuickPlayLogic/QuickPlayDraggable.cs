using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[RequireComponent(typeof(QuickPlayTargetSelector))]
public class QuickPlayDraggable : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler
{
    [Header("拖拽设置")]
    [SerializeField] private float dragZ          = -1f;
    [SerializeField] private float returnDuration = 0.25f;

    [Header("放大设置")]
    [SerializeField] private float dragScale     = 1.3f;
    [SerializeField] private float scaleDuration = 0.15f;

    [Header("CardGlow Settings")]
    [SerializeField] private Image  _cardGlow;
    [SerializeField] private float  glowPulseMin   = 0.4f;
    [SerializeField] private float  glowPulseSpeed = 0.8f;

    private QuickPlayTargetSelector _selector;
    private HoverPreview            _hoverPreview;
    private Vector3  _originPosition;
    private Vector3  _originScale;
    private bool     _dragging       = false;
    private bool     _aboveThreshold = false;
    private Vector3  _mouseOrigin;
    private float    _zDisplace;

    public bool CanDrag { get; set; } = false;
    public bool IsBeingDragged => _dragging;

    void Awake()
    {
        _selector     = GetComponent<QuickPlayTargetSelector>();
        _hoverPreview = GetComponent<HoverPreview>();
        if (_cardGlow == null) Debug.LogWarning($"[Draggable] {gameObject.name} CardGlow not set in Inspector.");
    }

    // ─────────────────────────────────────────────────
    // UI 指针事件
    // ─────────────────────────────────────────────────

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!CanDrag) return;

        _originPosition = transform.position;
        _originScale    = transform.localScale;
        _dragging       = true;
        _aboveThreshold = false;
        _zDisplace      = Camera.main.WorldToScreenPoint(_originPosition).z + dragZ;
        _mouseOrigin    = MouseWorldPos();

        if (_selector != null && _selector.NeedsTarget())
        {
            if (_selector.gameManager != null)
            {
                _selector.gameManager.IsPlayerDragging = true;
                _selector.gameManager.RefreshHandHighlight();
            }
            if (_hoverPreview != null && _hoverPreview.smallCardGlow != null)
            {
                _hoverPreview.smallCardGlow.DOKill();
                _hoverPreview.smallCardGlow.enabled = false;
            }
            _hoverPreview?.ForceShow(ignoreLock: true);
            if (_hoverPreview != null && _hoverPreview.previewCardFaceGlow != null)
                _hoverPreview.previewCardFaceGlow.enabled = false;
        }
        else
        {
            // 无目标牌：时机合法时自身降为浅色，不干预全局高亮，不提升 sorting layer
            if (_selector != null && _selector.IsTimingValid()
                && _hoverPreview != null && _hoverPreview.smallCardGlow != null)
            {
                var g = _hoverPreview.smallCardGlow;
                g.DOKill();
                g.enabled = true;
                g.color   = new Color(0.2f, 1f, 0.3f, 0f);
                string dimId = "handglow_" + g.GetInstanceID();
                DOTween.Kill(dimId);
                DOTween.To(() => g.color.a,
                    a => { var c = g.color; c.a = a; g.color = c; },
                    0.15f, 0.2f)
                    .SetId(dimId)
                    .OnComplete(() =>
                    {
                        if (g != null && g.enabled)
                            DOTween.To(() => g.color.a,
                                a => { var c = g.color; c.a = a; g.color = c; },
                                0.05f, 0.6f)
                                .SetLoops(-1, LoopType.Yoyo)
                                .SetEase(Ease.InOutSine)
                                .SetId(dimId)
                                .SetLink(gameObject);
                    });
            }
            HoverPreview.PreviewsAllowed = false;
            transform.SetAsLastSibling();
        }

        _selector.OnDragStart();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // Update / GetMouseButton 轮询兜底
    }

    // ─────────────────────────────────────────────────

    void Update()
    {
        if (!_dragging) return;

        if (!Input.GetMouseButton(0))
        {
            EndDrag();
            return;
        }

        Vector3 mouse = MouseWorldPos();

        if (_selector != null && _selector.NeedsTarget())
        {
            _selector.OnDragging(mouse);
            return;
        }

        transform.position = new Vector3(mouse.x, mouse.y, _originPosition.z);
        _selector?.OnDragging(mouse);

        bool over     = (mouse.y - _mouseOrigin.y) >= (_selector?.NoTargetThreshold ?? 1.5f);
        bool canPlay  = _selector != null && _selector.IsTimingValid();
        bool wantGlow = over && canPlay;

        if (wantGlow != _aboveThreshold)
        {
            _aboveThreshold = wantGlow;
            if (wantGlow)
            {
                // 超过阈值且可用：让 ForceShow 自己处理 sorting layer 和 smallCardGlow→previewCardFaceGlow 的切换
                StartCardGlow();
                if (_hoverPreview != null)
                {
                    _hoverPreview.ForceShow(ignoreLock: true);
                    // ForceShow 内部已经把 smallCardGlow 关掉、previewCardFaceGlow 打开
                    // 这里只把颜色改成绿色
                    if (_hoverPreview.previewCardFaceGlow != null)
                    {
                        var pfg = _hoverPreview.previewCardFaceGlow;
                        string pfgId = "previewglow_" + pfg.GetInstanceID();
                        pfg.DOKill();
                        DOTween.Kill(pfgId);
                        pfg.enabled = true;
                        pfg.color   = new Color(0.2f, 1f, 0.3f, 0f);
                        DOTween.To(() => pfg.color.a,
                            a => { var c = pfg.color; c.a = a; pfg.color = c; },
                            1f, 0.15f)
                            .SetId(pfgId)
                            .OnComplete(() =>
                            {
                                if (pfg != null && pfg.enabled)
                                    DOTween.To(() => pfg.color.a,
                                        a => { var c = pfg.color; c.a = a; pfg.color = c; },
                                        0.35f, 0.9f)
                                        .SetLoops(-1, LoopType.Yoyo)
                                        .SetEase(Ease.InOutSine)
                                        .SetId(pfgId)
                                        .SetLink(gameObject);
                            });
                    }
                }
            }
            else
            {
                // 退回阈值以下：收起预览（ForceHide 会复原 sorting layer），恢复浅色
                StopCardGlow();
                if (_hoverPreview != null)
                {
                    _hoverPreview.ForceHide();
                    if (canPlay && _hoverPreview.smallCardGlow != null)
                    {
                        var g = _hoverPreview.smallCardGlow;
                        g.DOKill();
                        g.enabled = true;
                        g.color   = new Color(0.2f, 1f, 0.3f, 0f);
                        string dimId = "handglow_" + g.GetInstanceID();
                        DOTween.Kill(dimId);
                        DOTween.To(() => g.color.a,
                            a => { var c = g.color; c.a = a; g.color = c; },
                            0.15f, 0.2f)
                            .SetId(dimId)
                            .OnComplete(() =>
                            {
                                if (g != null && g.enabled)
                                    DOTween.To(() => g.color.a,
                                        a => { var c = g.color; c.a = a; g.color = c; },
                                        0.05f, 0.6f)
                                        .SetLoops(-1, LoopType.Yoyo)
                                        .SetEase(Ease.InOutSine)
                                        .SetId(dimId)
                                        .SetLink(gameObject);
                            });
                    }
                }
            }
        }
    }

    private void EndDrag()
    {
        _dragging = false;

        // 有目标牌才需要重置 IsPlayerDragging 并刷新全局高亮
        if (_selector != null && _selector.NeedsTarget() && _selector.gameManager != null)
        {
            _selector.gameManager.IsPlayerDragging = false;
            _selector.gameManager.RefreshHandHighlight();
        }

        if (_selector != null && _selector.NeedsTarget())
        {
            _hoverPreview?.ForceHide();
            ScaleBack();
        }
        else
        {
            if (_aboveThreshold)
                _hoverPreview?.ForceHide();
            HoverPreview.PreviewsAllowed = true;
            StopCardGlow();
        }

        bool played = _selector != null && _selector.OnDragEnd(MouseWorldPos());
        if (!played)
        {
            ReturnToOrigin();
            if (_selector != null && _selector.IsTimingValid())
                RestoreWindowGlow();
        }
        else
        {
            transform.localScale = _originScale;
        }
    }

    public void SetValidTargetHover(bool onTarget)
    {
        if (!_selector.NeedsTarget()) return;

        UnityEngine.UI.Image glow = _hoverPreview != null ? _hoverPreview.smallCardGlow : null;
        if (glow == null && _hoverPreview != null) glow = _hoverPreview.previewCardFaceGlow;
        if (glow == null) return;

        glow.DOKill();
        if (onTarget)
        {
            glow.enabled = true;
            glow.color   = Color.green;
        }
        else
        {
            glow.enabled = false;
        }
    }

    private void RestoreWindowGlow()
    {
        if (_hoverPreview == null || _hoverPreview.smallCardGlow == null) return;
        var glow = _hoverPreview.smallCardGlow;
        DOTween.Kill(glow, complete: false);
        glow.gameObject.SetActive(true);
        glow.enabled = true;
        glow.color   = new Color(0.2f, 1f, 0.3f, 1f);
        string glowId = "handglow_" + glow.GetInstanceID();
        DOTween.Kill(glowId);
        DOTween.To(() => glow.color.a,
            a => { var c = glow.color; c.a = a; glow.color = c; },
            0.35f, 0.9f)
            .SetLoops(-1, LoopType.Yoyo)
            .SetEase(Ease.InOutSine)
            .SetId(glowId)
            .SetLink(gameObject);
    }

    public void ReturnToOrigin()
    {
        _selector?.CancelTargeting();
        transform.DOKill();
        transform.DOMove(_originPosition, returnDuration).SetEase(Ease.OutBack);
        transform.DOScale(_originScale,   returnDuration).SetEase(Ease.OutBack);
    }

    private void ScaleTo(float multiplier)
    {
        transform.DOKill();
        transform.DOScale(_originScale * multiplier, scaleDuration).SetEase(Ease.OutBack);
    }

    private void ScaleBack()
    {
        transform.DOKill();
        transform.DOScale(_originScale, scaleDuration).SetEase(Ease.OutQuad);
    }

    private void StartCardGlow(Color? color = null)
    {
        if (_cardGlow == null) return;
        Color glowColor = color ?? new Color(0f, 0.922f, 1f, 1f);

        _cardGlow.gameObject.SetActive(true);
        _cardGlow.DOKill();
        _cardGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 0f);
        _cardGlow.DOFade(1f, glowPulseSpeed * 0.3f).OnComplete(() =>
        {
            if (_cardGlow != null)
            {
                _cardGlow.color = new Color(glowColor.r, glowColor.g, glowColor.b, 1f);
                _cardGlow.DOFade(glowPulseMin, glowPulseSpeed)
                         .SetLoops(-1, LoopType.Yoyo)
                         .SetEase(Ease.InOutSine);
            }
        });
    }

    private void StopCardGlow()
    {
        if (_cardGlow == null) return;
        _cardGlow.DOKill();
        _cardGlow.DOFade(0f, glowPulseSpeed * 0.2f).OnComplete(() =>
        {
            if (_cardGlow != null) _cardGlow.gameObject.SetActive(false);
        });
    }

    private Vector3 MouseWorldPos()
    {
        Vector3 p = Input.mousePosition;
        p.z = _zDisplace;
        return Camera.main.ScreenToWorldPoint(p);
    }
}