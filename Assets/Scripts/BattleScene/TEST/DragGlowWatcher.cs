using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 挂在无目标速攻牌 Prefab 上，监听拖拽过程中所有 Glow 的状态变化。
/// 调试完成后删除。
/// </summary>
public class DragGlowWatcher : MonoBehaviour
{
    private HoverPreview _hp;
    private Image _smallCardGlow;
    private Image _previewCardFaceGlow;
    private Image _previewCardGlow;
    private Image _cardGlow;

    private bool  _lastSmallEnabled;
    private float _lastSmallAlpha;
    private bool  _lastPreviewFaceEnabled;
    private float _lastPreviewFaceAlpha;
    private bool  _lastPreviewGlowEnabled;
    private float _lastPreviewGlowAlpha;
    private bool  _lastCardGlowEnabled;
    private float _lastCardGlowAlpha;

    void Awake()
    {
        _hp = GetComponent<HoverPreview>();
        if (_hp != null)
        {
            _smallCardGlow      = _hp.smallCardGlow;
            _previewCardFaceGlow = _hp.previewCardFaceGlow;
            _previewCardGlow    = _hp.previewCardGlow;
        }
        // 找 _cardGlow（QuickPlayDraggable 的拖拽光）
        var drag = GetComponent<QuickPlayDraggable>();
        // 通过名字找
        foreach (var img in GetComponentsInChildren<Image>(true))
            if (img.gameObject.name == "CardGlow" && img.transform.parent?.name == "CardPanel")
                _cardGlow = img;

        Snapshot();
    }

    void Snapshot()
    {
        if (_smallCardGlow      != null) { _lastSmallEnabled      = _smallCardGlow.enabled;      _lastSmallAlpha      = _smallCardGlow.color.a; }
        if (_previewCardFaceGlow!= null) { _lastPreviewFaceEnabled = _previewCardFaceGlow.enabled; _lastPreviewFaceAlpha = _previewCardFaceGlow.color.a; }
        if (_previewCardGlow    != null) { _lastPreviewGlowEnabled = _previewCardGlow.enabled;    _lastPreviewGlowAlpha = _previewCardGlow.color.a; }
        if (_cardGlow           != null) { _lastCardGlowEnabled    = _cardGlow.enabled;           _lastCardGlowAlpha    = _cardGlow.color.a; }
    }

    void Update()
    {
        Check("smallCardGlow",       _smallCardGlow,       ref _lastSmallEnabled,       ref _lastSmallAlpha);
        Check("previewCardFaceGlow", _previewCardFaceGlow, ref _lastPreviewFaceEnabled, ref _lastPreviewFaceAlpha);
        Check("previewCardGlow",     _previewCardGlow,     ref _lastPreviewGlowEnabled, ref _lastPreviewGlowAlpha);
        Check("cardGlow",            _cardGlow,            ref _lastCardGlowEnabled,    ref _lastCardGlowAlpha);
    }

    void Check(string name, Image img, ref bool lastEnabled, ref float lastAlpha)
    {
        if (img == null) return;
        bool  curEnabled = img.enabled;
        float curAlpha   = img.color.a;

        if (curEnabled != lastEnabled || Mathf.Abs(curAlpha - lastAlpha) > 0.02f)
        {
            Debug.Log($"[DragGlowWatcher] {gameObject.name} / {name}: enabled {lastEnabled}→{curEnabled} | alpha {lastAlpha:F2}→{curAlpha:F2}\n{System.Environment.StackTrace}");
            lastEnabled = curEnabled;
            lastAlpha   = curAlpha;
        }
    }
}
