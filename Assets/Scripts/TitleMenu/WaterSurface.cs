using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 挂在 WaterSurface 对象上（UI Image）。
/// 用程序化方式绘制动态水面线，模拟横板2D游戏中的水面分隔效果。
///
/// 实现原理：
///   - 使用多个非常细的 UI Image 条，排列成微起伏的水面线
///   - 每帧用正弦函数驱动位置，产生流动感
///   - 水面线有高光和阴影两层，增加立体感
///
/// 注意：此脚本需要挂在一个有 RectTransform 的对象上，
/// 父级应是 ReflectionContainer 或 Canvas 的直接子对象。
/// </summary>
public class WaterSurface : MonoBehaviour
{
    [Header("水面线参数")]
    [Tooltip("分段数量，越多越平滑（建议 40~80）")]
    [SerializeField] private int   segmentCount    = 60;

    [Tooltip("水面线高度（像素）")]
    [SerializeField] private float lineThickness   = 2f;

    [Tooltip("高光线宽度")]
    [SerializeField] private float highlightThickness = 4f;

    [Tooltip("波形振幅（像素）")]
    [SerializeField] private float waveAmplitude   = 6f;

    [Tooltip("波形速度")]
    [SerializeField] private float waveSpeed       = 1.0f;

    [Tooltip("波形频率")]
    [SerializeField] private float waveFrequency   = 2.5f;

    [Tooltip("次级波频率（叠加产生更自然效果）")]
    [SerializeField] private float wave2Frequency  = 4.1f;

    [Tooltip("次级波振幅比例")]
    [SerializeField] private float wave2AmpScale   = 0.35f;

    [Header("颜色")]
    [SerializeField] private Color hopeLineColor      = new Color(0.29f, 0.62f, 1.00f, 0.70f);
    [SerializeField] private Color voidLineColor      = new Color(0.86f, 0.20f, 0.20f, 0.70f);
    [SerializeField] private Color highlightColor     = new Color(1.0f,  1.0f,  1.0f, 0.25f);
    [SerializeField] private float colorTransDuration = 0.8f;

    [Header("泡沫粒子（可选）")]
    [Tooltip("水面泡沫粒子数量，0 = 关闭")]
    [SerializeField] private int   foamCount = 12;
    [SerializeField] private float foamSize  = 3f;
    [SerializeField] private Color foamColor = new Color(1f, 1f, 1f, 0.3f);

    // ── 运行时 ──────────────────────────────────────────
    private RectTransform _rect;
    private List<RectTransform> _lineSegs     = new List<RectTransform>();
    private List<Image>         _lineImgs     = new List<Image>();
    private List<RectTransform> _hlSegs       = new List<RectTransform>();
    private List<Image>         _hlImgs       = new List<Image>();
    private List<RectTransform> _foamSegs     = new List<RectTransform>();
    private List<Image>         _foamImgs     = new List<Image>();

    private float _time = 0f;
    private Color _currentLineColor;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _currentLineColor = hopeLineColor;
    }

    void OnEnable()
    {
        TitleScreenManager.OnFactionChanged += OnFactionChanged;
        EnsureSegments();
    }

    void OnDisable()
    {
        TitleScreenManager.OnFactionChanged -= OnFactionChanged;
    }

    void Update()
    {
        _time += Time.deltaTime;
        UpdateWaterLine();
    }

    // ────────────────────────────────────────────────────
    private void EnsureSegments()
    {
        float totalWidth = _rect.rect.width > 0 ? _rect.rect.width : Screen.width;

        // 主水面线
        while (_lineSegs.Count < segmentCount)
        {
            var go = new GameObject("WS_Seg_" + _lineSegs.Count,
                                   typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rect, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            _lineSegs.Add(rt);
            _lineImgs.Add(img);
        }

        // 高光线
        while (_hlSegs.Count < segmentCount)
        {
            var go = new GameObject("WS_HL_" + _hlSegs.Count,
                                   typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rect, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            _hlSegs.Add(rt);
            _hlImgs.Add(img);
        }

        // 泡沫粒子
        while (_foamSegs.Count < foamCount)
        {
            var go = new GameObject("WS_Foam_" + _foamSegs.Count,
                                   typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_rect, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(foamSize, foamSize);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            _foamSegs.Add(rt);
            _foamImgs.Add(img);
        }
    }

    private void UpdateWaterLine()
    {
        if (_lineSegs.Count == 0) EnsureSegments();

        float totalWidth = _rect.rect.width > 10 ? _rect.rect.width : Screen.width;
        float segWidth   = totalWidth / segmentCount + 1f; // +1 防止接缝
        float halfWidth  = totalWidth * 0.5f;

        for (int i = 0; i < segmentCount && i < _lineSegs.Count; i++)
        {
            float t    = (float)i / segmentCount;
            float posX = -halfWidth + t * totalWidth + segWidth * 0.5f;

            // 双层正弦叠加
            float y = Mathf.Sin(t * Mathf.PI * 2f * waveFrequency + _time * waveSpeed) * waveAmplitude
                    + Mathf.Sin(t * Mathf.PI * 2f * wave2Frequency + _time * waveSpeed * 0.7f + 1.5f)
                      * waveAmplitude * wave2AmpScale;

            // 计算相邻段的高度差，用于倾斜（让线段更自然地跟随波形）
            float tNext = (float)(i + 1) / segmentCount;
            float yNext = Mathf.Sin(tNext * Mathf.PI * 2f * waveFrequency + _time * waveSpeed) * waveAmplitude
                        + Mathf.Sin(tNext * Mathf.PI * 2f * wave2Frequency + _time * waveSpeed * 0.7f + 1.5f)
                          * waveAmplitude * wave2AmpScale;
            float dy    = yNext - y;
            float angle = Mathf.Atan2(dy, segWidth) * Mathf.Rad2Deg;

            // 主线
            _lineSegs[i].anchoredPosition = new Vector2(posX, y);
            _lineSegs[i].sizeDelta        = new Vector2(segWidth + 0.5f, lineThickness);
            _lineSegs[i].localEulerAngles = new Vector3(0, 0, angle);
            _lineImgs[i].color            = _currentLineColor;

            // 高光（略高于主线，细一点，更亮）
            _hlSegs[i].anchoredPosition = new Vector2(posX, y + lineThickness);
            _hlSegs[i].sizeDelta        = new Vector2(segWidth + 0.5f, highlightThickness * 0.5f);
            _hlSegs[i].localEulerAngles = new Vector3(0, 0, angle);
            _hlImgs[i].color            = highlightColor;
        }

        // 泡沫粒子
        for (int i = 0; i < foamCount && i < _foamSegs.Count; i++)
        {
            float fi   = (float)i / foamCount;
            float posX = -halfWidth + fi * totalWidth;
            float y    = Mathf.Sin(fi * Mathf.PI * 2f * waveFrequency + _time * waveSpeed) * waveAmplitude
                       + Mathf.Sin(fi * Mathf.PI * 2f * wave2Frequency + _time * waveSpeed * 0.7f + 1.5f)
                         * waveAmplitude * wave2AmpScale;

            // 泡沫上下漂浮
            float floatY = Mathf.Sin(_time * 1.2f + i * 0.8f) * 3f;
            // 泡沫透明度脉动
            float alpha  = (Mathf.Sin(_time * 0.8f + i * 1.3f) * 0.3f + 0.7f) * foamColor.a;

            _foamSegs[i].anchoredPosition = new Vector2(posX, y + lineThickness + floatY);
            _foamSegs[i].sizeDelta        = new Vector2(foamSize, foamSize);
            _foamImgs[i].color            = new Color(foamColor.r, foamColor.g, foamColor.b, alpha);
        }
    }

    private void OnFactionChanged(TitleScreenManager.Faction faction)
    {
        Color target = faction == TitleScreenManager.Faction.Hope ? hopeLineColor : voidLineColor;
        DG.Tweening.DOTween.To(
            () => _currentLineColor,
            x  => _currentLineColor = x,
            target,
            colorTransDuration
        );
    }

    void OnDestroy()
    {
        foreach (var rt in _lineSegs) if (rt) Destroy(rt.gameObject);
        foreach (var rt in _hlSegs)   if (rt) Destroy(rt.gameObject);
        foreach (var rt in _foamSegs) if (rt) Destroy(rt.gameObject);
    }
}
