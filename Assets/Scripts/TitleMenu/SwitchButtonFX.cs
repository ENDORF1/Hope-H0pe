using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// 切换阵营按钮的视觉效果。
///
/// 蓝色阵营（Hope）：
///   Hover  → 波浪从底部涌入填满按钮，文字变深色
///   Click  → 从点击位置向外扩散圆形波纹环
///
/// 红色阵营（Void）：
///   暂未实现，保持按钮原样。
/// </summary>
[RequireComponent(typeof(Button))]
public class SwitchButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IPointerClickHandler, IPointerDownHandler
{
    // ═══════════════════════════════════════════════════
    // Inspector 参数 —— 蓝色阵营 Hover 波浪
    // ═══════════════════════════════════════════════════

    [Header("── 按钮外观")]

    [Tooltip("Hope 阵营时的边框+文字颜色（按钮显示字符雨效果时）。")]
    [SerializeField] private Color hopeSchemeColor = new Color(0.86f, 0.20f, 0.20f, 1f);

    [Tooltip("Void 阵营时的边框+文字颜色（按钮显示波浪效果时）。")]
    [SerializeField] private Color voidSchemeColor = new Color(0.13f, 0.67f, 1f, 1f);

    [Tooltip("边框厚度（像素）。")]
    [SerializeField] private float borderThickness = 1.5f;


    [Header("── 蓝色阵营 / Hover 波浪")]

    [Tooltip("波浪填充颜色（仅 Void 阵营显示波浪时使用）。")]
    [SerializeField] private Color voidWaveColor = new Color(0.0f, 0.67f, 0.78f, 1f);

    [Tooltip("勾选后波浪颜色跟随阵营颜色，取消勾选则使用上方单独设置的颜色。")]
    [SerializeField] private bool waveColorFollowScheme = false;

    [Tooltip("波浪频率（越大波峰越密）。")]
    [SerializeField] private float waveFrequency = 0.06f;

    [Tooltip("波浪振幅（像素）。")]
    [SerializeField] private float waveAmplitude = 8f;

    [Tooltip("波浪流动速度。")]
    [SerializeField] private float waveSpeed = 2f;

    [Tooltip("均衡器格子大小（像素）。")]
    [SerializeField] private int ledCellSize = 6;

    [Tooltip("均衡器格子间隙（像素）。")]
    [SerializeField] private int ledGapSize = 1;

    [Tooltip("Hover 进入/退出的过渡速度。")]
    [SerializeField] private float hoverTransitionSpeed = 6f;

    [Header("── 蓝色阵营 / Click 波纹")]

    [Tooltip("波纹扩散最大半径（像素）。")]
    [SerializeField] private float rippleMaxRadius = 200f;

    [Tooltip("波纹扩散持续时间（秒）。")]
    [SerializeField] private float rippleDuration = 0.35f;

    [Tooltip("波纹颜色。")]
    [SerializeField] private Color rippleColor = new Color(0.4f, 0.86f, 1f, 0.9f);

    [Tooltip("同时存在的最大波纹数量。")]
    [SerializeField] private int maxRipples = 4;

    // ═══════════════════════════════════════════════════
    // 内部状态
    // ═══════════════════════════════════════════════════

    private Button          _button;
    private RectTransform   _rect;
    private Image           _bgImage;
    private TextMeshProUGUI _tmp;
    private Canvas          _canvas;
    private Camera          _canvasCamera;

    private bool  _isHovered = false;
    private float _hoverT    = 0f;
    private float _waveT     = 0f;
    private Color _originalBgColor;
    private Color _originalTextColor;

    private TitleScreenManager.Faction _faction = TitleScreenManager.Faction.Hope;
    private Color _currentColor = new Color(0.13f, 0.67f, 1f, 1f); // 当前阵营对应的颜色，由ApplySchemeColor更新
    private Color _waveColor;    // 波浪实际使用颜色，由ApplySchemeColor更新

    // 波浪 RawImage
    private RawImage  _waveImage;
    private Texture2D _waveTex;
    private int       _waveTexW, _waveTexH;

    // 边框
    private RectTransform _borderTop, _borderBottom, _borderLeft, _borderRight;


    // 波纹环
    private struct Ripple { public float progress; public Vector2 localPos; }
    private List<Ripple>        _ripples    = new List<Ripple>();
    private List<GameObject>    _rippleGOs  = new List<GameObject>();
    private List<Image>         _rippleImgs = new List<Image>();
    private List<RectTransform> _rippleRTs  = new List<RectTransform>();

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════

    void Awake()
    {
        _button  = GetComponent<Button>();
        _rect    = GetComponent<RectTransform>();
        _bgImage = GetComponent<Image>();
        _tmp     = GetComponentInChildren<TextMeshProUGUI>();

        // 禁用 Button 的颜色过渡，防止它把 targetGraphic 的颜色覆盖回去
        if (_button != null)
            _button.transition = Selectable.Transition.None;

        // 在第一帧渲染前就设透明，避免出现一帧原始背景色的闪烁
        if (_bgImage != null)
        {
            _bgImage.color   = Color.clear;
            _originalBgColor = Color.clear;
        }
    }

    void Start()
    {
        _canvas       = GetComponentInParent<Canvas>();
        _canvasCamera = _canvas != null ? _canvas.worldCamera : Camera.main;

        // _originalBgColor 已在 Awake 里设好，这里只处理文字颜色
        if (_tmp != null)
        {
            _originalTextColor = _tmp.color;
            _tmp.raycastTarget = false;
        }
        SetupWaveLayer();
        SetupAppearance();
        ApplySchemeColor(_faction);
        SetupVoidRain();

        TitleScreenManager.OnFactionChanged += OnFactionChanged;
    }

    void OnDestroy()
    {
        TitleScreenManager.OnFactionChanged -= OnFactionChanged;
        if (_waveTex != null) Destroy(_waveTex);
    }

    void OnRectTransformDimensionsChange()
    {
        // 按钮尺寸变化时重建波浪纹理，避免尺寸不匹配
        if (_waveTex == null) return;
        int newW = Mathf.Max(4, Mathf.RoundToInt(_rect.rect.width));
        int newH = Mathf.Max(4, Mathf.RoundToInt(_rect.rect.height));
        if (newW == _waveTexW && newH == _waveTexH) return;
        _waveTexW = newW;
        _waveTexH = newH;
        Destroy(_waveTex);
        _waveTex          = new Texture2D(_waveTexW, _waveTexH, TextureFormat.RGBA32, false);
        _waveTex.wrapMode = TextureWrapMode.Clamp;
        // 初始化为全透明，防止首帧显示未初始化的脏数据
        var clearPixels = new Color32[_waveTexW * _waveTexH];
        _waveTex.SetPixels32(clearPixels);
        _waveTex.Apply();
        _waveImage.texture = _waveTex;
    }

    void Update()
    {
        _waveT += Time.deltaTime;
        _hoverT = Mathf.MoveTowards(
            _hoverT, _isHovered ? 1f : 0f,
            hoverTransitionSpeed * Time.deltaTime);

        // Hope阵营用字符雨+边框收缩，Void阵营用波浪+波纹——对调产生切换感
        if (_faction == TitleScreenManager.Faction.Hope)
        {
            UpdateVoidHover();
            UpdateVoidClick();
        }
        else
        {
            UpdateHopeHover();
            UpdateRipples();
        }
    }

    // ═══════════════════════════════════════════════════
    // 阵营切换
    // ═══════════════════════════════════════════════════

    private void OnFactionChanged(TitleScreenManager.Faction faction)
    {
        _faction   = faction;
        _isHovered = false;
        _hoverT    = 0f;
        ApplySchemeColor(faction);
        ResetVisuals();
        ClearRipples();
    }

    private void ApplySchemeColor(TitleScreenManager.Faction faction)
    {
        // Hope阵营→字符雨+红色，Void阵营→波浪+蓝色
        _currentColor = (faction == TitleScreenManager.Faction.Hope)
            ? hopeSchemeColor
            : voidSchemeColor;

        // 边框颜色
        foreach (var border in new[] { _borderTop, _borderBottom, _borderLeft, _borderRight })
        {
            if (border == null) continue;
            var img = border.GetComponent<Image>();
            if (img != null) img.color = _currentColor;
        }

        // 文字颜色
        if (_tmp != null) _tmp.color = _currentColor;
        _originalTextColor = _currentColor;

        // 波浪颜色：可选跟随阵营颜色或使用独立设置
        _waveColor = waveColorFollowScheme ? _currentColor : voidWaveColor;

        // 字符雨颜色（Hope阵营用字符雨时用hopeSchemeColor）
        voidCharColor = _currentColor;
    }

    private void ResetVisuals()
    {
        if (_bgImage   != null) _bgImage.color = _originalBgColor;
        if (_tmp       != null) _tmp.color     = _originalTextColor;
        if (_waveImage != null) _waveImage.gameObject.SetActive(false);
        // 重置Void状态
        _voidShrinking = false;
        _voidShrinkT   = -1f;
        _voidRainActive = false;
        if (_voidCharTexts != null)
            foreach (var t in _voidCharTexts) if (t != null) t.gameObject.SetActive(false);
        UpdateBorderLayout();
    }

    private void SetupAppearance()
    {
        // 背景改为透明
        if (_bgImage != null)
        {
            _bgImage.color   = Color.clear;
            _originalBgColor = Color.clear;
            _bgImage.raycastTarget = true;
        }

        // RectMask2D 裁剪波纹
        if (gameObject.GetComponent<RectMask2D>() == null)
            gameObject.AddComponent<RectMask2D>();

        // 4条边框
        _borderTop    = CreateBorderEdge("BorderTop");
        _borderBottom = CreateBorderEdge("BorderBottom");
        _borderLeft   = CreateBorderEdge("BorderLeft");
        _borderRight  = CreateBorderEdge("BorderRight");

        UpdateBorderLayout();
    }

    private RectTransform CreateBorderEdge(string name)
    {
        var go  = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_rect, false);
        go.transform.SetSiblingIndex(0);

        var img           = go.GetComponent<Image>();
        img.color         = _currentColor;
        img.raycastTarget = false;

        return go.GetComponent<RectTransform>();
    }

    private void UpdateBorderLayout()
    {
        if (_borderTop == null) return;
        float t = borderThickness;

        _borderTop.anchorMin    = new Vector2(0f, 1f);
        _borderTop.anchorMax    = new Vector2(1f, 1f);
        _borderTop.offsetMin    = new Vector2(0f, -t);
        _borderTop.offsetMax    = Vector2.zero;

        _borderBottom.anchorMin = new Vector2(0f, 0f);
        _borderBottom.anchorMax = new Vector2(1f, 0f);
        _borderBottom.offsetMin = Vector2.zero;
        _borderBottom.offsetMax = new Vector2(0f, t);

        _borderLeft.anchorMin   = new Vector2(0f, 0f);
        _borderLeft.anchorMax   = new Vector2(0f, 1f);
        _borderLeft.offsetMin   = Vector2.zero;
        _borderLeft.offsetMax   = new Vector2(t, 0f);

        _borderRight.anchorMin  = new Vector2(1f, 0f);
        _borderRight.anchorMax  = new Vector2(1f, 1f);
        _borderRight.offsetMin  = new Vector2(-t, 0f);
        _borderRight.offsetMax  = Vector2.zero;
    }

    private void SetupWaveLayer()
    {
        var go = new GameObject("WaveFill", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(_rect, false);

        var rt       = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _waveImage              = go.GetComponent<RawImage>();
        _waveImage.raycastTarget = false;
        _waveImage.gameObject.SetActive(false);

        // 放在文字下方
        go.transform.SetSiblingIndex(0);

        // 纹理尺寸：用 rect 或默认值
        _waveTexW = Mathf.Max(4, Mathf.RoundToInt(_rect.rect.width));
        _waveTexH = Mathf.Max(4, Mathf.RoundToInt(_rect.rect.height));
        if (_waveTexW < 4) _waveTexW = 400;
        if (_waveTexH < 4) _waveTexH = 80;

        _waveTex           = new Texture2D(_waveTexW, _waveTexH, TextureFormat.RGBA32, false);
        _waveTex.wrapMode  = TextureWrapMode.Clamp;
        _waveImage.texture = _waveTex;
    }

    // ═══════════════════════════════════════════════════
    // 蓝色 Hover 波浪
    // ═══════════════════════════════════════════════════

    private void UpdateHopeHover()
    {
        if (_hoverT <= 0.001f)
        {
            if (_waveImage.gameObject.activeSelf)
                _waveImage.gameObject.SetActive(false);
            return;
        }

        _waveImage.gameObject.SetActive(true);

        int W = _waveTexW, H = _waveTexH;
        var pixels = new Color32[W * H];
        Color32 wc = _waveColor;

        // 像素均衡器：每列小方块从底部往上堆叠，高度由波形决定
        int cell = Mathf.Max(1, ledCellSize);
        int gap  = Mathf.Max(0, ledGapSize);
        int step = cell + gap;
        byte ba  = (byte)(255f * _hoverT);

        for (int cx = 0; cx < W; cx += step)
        {
            float sampleX = cx + cell * 0.5f;
            float raw  = Mathf.Sin(sampleX * waveFrequency + _waveT * waveSpeed)
                       + Mathf.Sin(sampleX * waveFrequency * 1.7f + _waveT * waveSpeed * 0.6f) * 0.5f;
            // raw 约 -1.5~1.5，归一化到 0~1
            float norm = Mathf.Clamp01((raw + 1.5f) / 3f);
            // 最低保留 20% 高度，避免波谷时格子全灭导致闪烁
            float barH = Mathf.Max(H * 0.2f, norm * H);

            for (int cy = 0; cy < H; cy += step)
            {
                if (cy + cell > barH) continue; // 超过柱高不亮
                for (int px = cx; px < Mathf.Min(cx + cell, W); px++)
                for (int py = cy; py < Mathf.Min(cy + cell, H); py++)
                    pixels[py * W + px] = new Color32(wc.r, wc.g, wc.b, ba);
            }
        }

        _waveTex.SetPixels32(pixels);
        _waveTex.Apply();

        // 文字颜色保持不变
    }

    // ═══════════════════════════════════════════════════
    // 蓝色 Click 波纹
    // ═══════════════════════════════════════════════════

    private void SpawnRipple(Vector2 localPos)
    {
        if (_ripples.Count >= maxRipples) return;

        _ripples.Add(new Ripple { progress = 0f, localPos = localPos });

        var go  = new GameObject("Ripple", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(_rect, false);
        go.transform.SetAsLastSibling();

        var rt              = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = localPos;
        rt.sizeDelta        = Vector2.zero;

        var img           = go.GetComponent<Image>();
        img.color         = rippleColor;
        img.raycastTarget = false;
        // 用 Unity 内置圆形 sprite，避免波纹显示为方块
        // 程序生成高精度圆环纹理（256×256，带软边抗锯齿）
        int res = 256;
        float half = res * 0.5f;
        float outerR = half - 1f;
        float innerR = outerR - 3f;   // 环宽3px
        float feather = 1.5f;         // 软边宽度

        var tex  = new Texture2D(res, res, TextureFormat.RGBA32, false);
        var cols = new Color32[res * res];
        for (int py = 0; py < res; py++)
        for (int px = 0; px < res; px++)
        {
            float dist = Mathf.Sqrt((px - half) * (px - half) + (py - half) * (py - half));
            // 外边软边
            float outerA = Mathf.Clamp01((outerR - dist) / feather);
            // 内边软边
            float innerA = Mathf.Clamp01((dist - innerR) / feather);
            float a = outerA * innerA;
            cols[py * res + px] = new Color32(255, 255, 255, (byte)(a * 255f));
        }
        tex.SetPixels32(cols);
        tex.Apply();
        img.sprite         = Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
        img.type           = Image.Type.Simple;
        img.preserveAspect = true;

        _rippleGOs.Add(go);
        _rippleImgs.Add(img);
        _rippleRTs.Add(rt);
    }

    private void UpdateRipples()
    {
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var r = _ripples[i];
            r.progress    += Time.deltaTime / rippleDuration;
            _ripples[i]    = r;

            float radius = r.progress * rippleMaxRadius;
            float alpha  = rippleColor.a * (1f - r.progress);

            if (i < _rippleRTs.Count && _rippleRTs[i] != null)
            {
                _rippleRTs[i].sizeDelta = new Vector2(radius * 2f, radius * 2f);
                _rippleImgs[i].color    = new Color(
                    rippleColor.r, rippleColor.g, rippleColor.b, alpha);
            }

            if (r.progress >= 1f)
            {
                if (i < _rippleGOs.Count && _rippleGOs[i] != null)
                    Destroy(_rippleGOs[i]);
                _ripples.RemoveAt(i);
                if (i < _rippleGOs.Count)  _rippleGOs.RemoveAt(i);
                if (i < _rippleImgs.Count) _rippleImgs.RemoveAt(i);
                if (i < _rippleRTs.Count)  _rippleRTs.RemoveAt(i);
            }
        }
    }

    private void ClearRipples()
    {
        foreach (var go in _rippleGOs)
            if (go != null) Destroy(go);
        _ripples.Clear();
        _rippleGOs.Clear();
        _rippleImgs.Clear();
        _rippleRTs.Clear();
    }

    // ═══════════════════════════════════════════════════
    // 指针事件
    // ═══════════════════════════════════════════════════

    public void OnPointerEnter(PointerEventData e)
    {
        _isHovered = true;
        Debug.Log($"[SwitchFX] PointerEnter, hoverT={_hoverT:F3}");
    }

    public void OnPointerExit(PointerEventData e)
    {
        // 用鼠标坐标直接判断是否还在按钮范围内，忽略子对象抢焦点导致的假退出
        if (RectTransformUtility.RectangleContainsScreenPoint(_rect, e.position, _canvasCamera))
        {
            Debug.Log($"[SwitchFX] PointerExit IGNORED (still inside rect), hoverT={_hoverT:F3}");
            return;
        }
        _isHovered = false;
        Debug.Log($"[SwitchFX] PointerExit, hoverT={_hoverT:F3}");
    }

    public void OnPointerDown(PointerEventData e)
    {
        if (_faction == TitleScreenManager.Faction.Hope)
        {
            // Hope阵营用字符雨套餐，click用边框收缩
            StartVoidClick();
        }
        else
        {
            // Void阵营用波浪套餐，click用波纹
            Vector2 localPos;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rect, e.position, _canvasCamera, out localPos);
            SpawnRipple(localPos);
        }
    }

    public void OnPointerClick(PointerEventData e) { }

    // ═══════════════════════════════════════════════════
    // 红色阵营 —— 字符雨 + 边框收缩
    // ═══════════════════════════════════════════════════

    [Header("── 红色阵营 / 字符雨")]
    [SerializeField] private Color voidCharColor     = new Color(0.7f, 0.1f, 0.1f, 0.6f);
    [SerializeField] private int   voidCharColumns   = 12;
    [Tooltip("字符大小（像素）。")]
    [SerializeField] private float voidCharSize      = 14f;
    [Tooltip("每列字符下落速度。")]
    [SerializeField] private float voidCharSpeed     = 3f;
    [Tooltip("字符雨整体透明度（0~1）")]
    [Range(0f,1f)]
    [SerializeField] private float voidCharAlpha     = 0.55f;

    [Header("── 红色阵营 / Click 边框收缩")]
    [Tooltip("边框向内收缩的最大距离（像素）")]
    [SerializeField] private float voidShrinkAmount  = 14f;
    [Tooltip("收缩动画总时长（秒）")]
    [SerializeField] private float voidShrinkDuration = 0.35f;

    // 字符雨状态
    private struct CharColumn
    {
        public float y;        // 当前头部 y 位置（0=顶，1=底）
        public float x;        // 列的本地空间 x 坐标
        public float speed;    // 每列独立下落速度
        public char[] chars;   // 每列显示的字符
        public int    len;     // 列长度（字符数）
    }
    private CharColumn[]         _voidColumns;
    private TextMeshProUGUI[]    _voidCharTexts;
    private bool                 _voidRainActive = false;

    // 边框收缩状态
    private float _voidShrinkT   = -1f; // -1=未激活
    private bool  _voidShrinking = false;

    private static readonly char[] ASCII_CHARS =
        "!@#$%^&*()_+-=[]{}|;:,.<>?/0123456789ABCDEF".ToCharArray();

    private void SetupVoidRain()
    {
        if (_voidCharTexts != null) return;
        if (_borderTop == null) return;

        // 直接用 RectTransform 的 rect 算范围，不依赖 anchoredPosition（layout可能未刷新）
        // 按钮以中心为 anchor，所以本地坐标：左=-w/2，右=w/2，上=h/2，下=-h/2
        float w     = _rect.rect.width;
        float h     = _rect.rect.height;
        float left  = -w * 0.5f + borderThickness;
        float right =  w * 0.5f - borderThickness;
        float top   =  h * 0.5f - borderThickness;
        float bot   = -h * 0.5f + borderThickness;
        float rainW = right - left;
        float rainH = top   - bot;

        _voidRainLeft = left;
        _voidRainTop  = top;
        _voidRainH    = rainH;

        _voidColumns   = new CharColumn[voidCharColumns];
        _voidCharTexts = new TextMeshProUGUI[voidCharColumns];

        for (int i = 0; i < voidCharColumns; i++)
        {
            var go  = new GameObject("VoidCol_" + i, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(_rect, false);
            go.transform.SetSiblingIndex(1);

            var rt        = go.GetComponent<RectTransform>();
            rt.anchorMin  = new Vector2(0.5f, 0.5f);
            rt.anchorMax  = new Vector2(0.5f, 0.5f);
            rt.pivot      = new Vector2(0.5f, 1f);
            rt.sizeDelta  = new Vector2(rainW / voidCharColumns, rainH * 2f);

            var tmp               = go.GetComponent<TextMeshProUGUI>();
            tmp.fontSize          = voidCharSize;
            tmp.color             = new Color(voidCharColor.r, voidCharColor.g, voidCharColor.b, 0f);
            tmp.alignment         = TextAlignmentOptions.Center;
            tmp.raycastTarget     = false;
            tmp.overflowMode      = TextOverflowModes.Overflow;
            tmp.enableWordWrapping = false;

            _voidCharTexts[i] = tmp;

            int len = Random.Range(4, 10);
            var col = new CharColumn
            {
                x     = left + (i + 0.5f) / voidCharColumns * rainW,
                // 随机初始 y（-1~1），确保任何时刻都有字符在不同位置
                y     = Random.Range(-0.5f, 1.2f),
                speed = Random.Range(0.6f, 1.4f), // 各列速度不同
                len   = len,
                chars = new char[len]
            };
            for (int c = 0; c < len; c++)
                col.chars[c] = ASCII_CHARS[Random.Range(0, ASCII_CHARS.Length)];
            _voidColumns[i] = col;

            go.SetActive(false);
        }
    }

    private float _voidRainLeft, _voidRainTop, _voidRainH;

    private void UpdateVoidHover()
    {
        if (_hoverT <= 0.001f)
        {
            // 隐藏所有列
            if (_voidCharTexts != null)
                foreach (var t in _voidCharTexts) if (t != null) t.gameObject.SetActive(false);
            _voidRainActive = false;
            return;
        }

        if (!_voidRainActive)
        {
            _voidRainActive = true;
            if (_voidCharTexts != null)
                foreach (var t in _voidCharTexts) if (t != null) t.gameObject.SetActive(true);
        }

        // 同步字体大小（支持运行时在Inspector调整）
        if (_voidCharTexts != null)
            foreach (var t in _voidCharTexts)
                if (t != null && t.fontSize != voidCharSize) t.fontSize = voidCharSize;

        float w = _rect.rect.width;
        float h = _rect.rect.height;

        for (int i = 0; i < voidCharColumns; i++)
        {
            ref CharColumn col = ref _voidColumns[i];

            // 下落：各列速度独立，重置时随机相位确保连绵不断
            col.y += voidCharSpeed * col.speed * Time.deltaTime / _voidRainH * 60f;
            if (col.y > 1.3f)
            {
                // 重置到顶部上方随机位置，不同列不会同时出现
                col.y = Random.Range(-0.8f, -0.1f);
                col.speed = Random.Range(0.6f, 1.4f);
                for (int c = 0; c < col.len; c++)
                    col.chars[c] = ASCII_CHARS[Random.Range(0, ASCII_CHARS.Length)];
            }

            // 随机刷新部分字符（闪烁感）
            if (Random.value < 0.1f)
                col.chars[Random.Range(0, col.len)] = ASCII_CHARS[Random.Range(0, ASCII_CHARS.Length)];

            // 更新 TMP 位置和文字
            var rt = _voidCharTexts[i].GetComponent<RectTransform>();
            // col.x 已经是本地空间的 x 坐标（边框内均匀分布）
            // col.y 是下落进度（0=顶，1=底），映射到 top→bot
            rt.anchoredPosition = new Vector2(
                col.x,
                _voidRainTop - col.y * _voidRainH
            );

            // 拼字符串
            var sb = new System.Text.StringBuilder();
            for (int c = 0; c < col.len; c++)
            {
                sb.Append(col.chars[c]);
                if (c < col.len - 1) sb.Append('\n');
            }
            // 用 RichText 给每个字符独立亮度：头部最亮，尾部渐暗
            var richSb = new System.Text.StringBuilder();
            for (int c = 0; c < col.len; c++)
            {
                // c=0 是头部（最亮），c=len-1 是尾部（最暗）
                float bright = Mathf.Lerp(1f, 0.15f, (float)c / (col.len - 1));
                float a      = voidCharAlpha * _hoverT * bright;
                byte  ab     = (byte)(Mathf.Clamp01(a) * 255f);
                byte rb = (byte)(Mathf.Clamp01(voidCharColor.r * bright) * 255f);
                byte gb = (byte)(Mathf.Clamp01(voidCharColor.g * bright) * 255f);
                byte bb = (byte)(Mathf.Clamp01(voidCharColor.b * bright) * 255f);
                richSb.Append($"<color=#{rb:X2}{gb:X2}{bb:X2}{ab:X2}>{col.chars[c]}</color>");
                if (c < col.len - 1) richSb.Append("\n");
            }
            _voidCharTexts[i].text        = richSb.ToString();
            _voidCharTexts[i].richText    = true;
            _voidCharTexts[i].color       = Color.white; // 颜色由richtext控制
        }
    }

    private void StartVoidClick()
    {
        _voidShrinkT   = 0f;
        _voidShrinking = true;
    }

    private void UpdateVoidClick()
    {
        if (!_voidShrinking) return;

        _voidShrinkT += Time.deltaTime / voidShrinkDuration;

        // 收缩曲线：先快速收缩到最小，再弹回
        float shrink;
        if (_voidShrinkT < 0.4f)
        {
            // 收缩阶段
            shrink = Mathf.Lerp(0f, voidShrinkAmount, _voidShrinkT / 0.4f);
        }
        else if (_voidShrinkT < 0.7f)
        {
            // 保持最小
            shrink = voidShrinkAmount;
        }
        else
        {
            // 弹回
            shrink = Mathf.Lerp(voidShrinkAmount, 0f, (_voidShrinkT - 0.7f) / 0.3f);
        }

        // 收缩：四条边框整体向内平移，不缩短长度
        // Top向下移、Bottom向上移、Left向右移、Right向左移
        if (_borderTop != null)
        {
            float t = borderThickness;
            // Top：整体向下平移 shrink
            _borderTop.offsetMin    = new Vector2(0f,  -t - shrink);
            _borderTop.offsetMax    = new Vector2(0f,  -shrink);
            // Bottom：整体向上平移 shrink
            _borderBottom.offsetMin = new Vector2(0f,  shrink);
            _borderBottom.offsetMax = new Vector2(0f,  t + shrink);
            // Left：整体向右平移 shrink
            _borderLeft.offsetMin   = new Vector2(shrink,      0f);
            _borderLeft.offsetMax   = new Vector2(t + shrink,  0f);
            // Right：整体向左平移 shrink
            _borderRight.offsetMin  = new Vector2(-t - shrink, 0f);
            _borderRight.offsetMax  = new Vector2(-shrink,     0f);
        }

        if (_voidShrinkT >= 1f)
        {
            _voidShrinking = false;
            _voidShrinkT   = -1f;
            UpdateBorderLayout(); // 恢复原始边框位置
        }
    }
}