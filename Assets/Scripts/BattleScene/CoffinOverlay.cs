using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 棺椁特效 Canvas 层。
/// 挂在任意常驻对象上，自动创建所有所需 UI 元素。
/// 粗重棺椁轮廓 + 密集二进制字符流 + 瞬间断电黑屏。
/// </summary>
public class CoffinOverlay : MonoBehaviour
{
    public static CoffinOverlay Instance { get; private set; }

    [Header("二进制流参数")]
    [SerializeField] private int    binaryColumnCount = 20;
    [SerializeField] private int    binaryRowCount    = 12;
    [SerializeField] private float  fontSize          = 13f;
    [SerializeField] private Color  binaryColor       = new Color(0.85f, 0.08f, 0.05f, 0.75f);

    [Header("棺椁轮廓参数")]
    [SerializeField] private float  coffinDuration    = 1.0f;
    [SerializeField] private float  coffinThickness   = 22f;
    [SerializeField] private Color  coffinColor       = new Color(0.5f, 0.0f, 0.02f, 1f);
    [SerializeField] private Color  coffinEdgeColor   = new Color(0.15f, 0.0f, 0.0f, 1f);
    [SerializeField] private float  blackoutHold      = 0.6f;

    // 运行时创建的对象
    private Canvas           _canvas;
    private RectTransform    _canvasRT;
    private Image            _blackScreen;
    private RectTransform    _coffinTop, _coffinBottom, _coffinLeft, _coffinRight;
    // 棺椁内边框（双层厚重感）
    private RectTransform    _coffinTopInner, _coffinBottomInner, _coffinLeftInner, _coffinRightInner;
    // 角落装饰
    private RectTransform[]  _corners = new RectTransform[4];
    // 二进制文字
    private List<TextMeshProUGUI> _binaryTexts = new List<TextMeshProUGUI>();
    // 二进制列数据
    private List<BinaryColumn> _binaryColumns = new List<BinaryColumn>();

    private struct BinaryColumn
    {
        public List<TextMeshProUGUI> chars;
        public float speed;
        public float freezeTime; // 何时冻结
        public bool  frozen;
        public int   edge; // 0上 1下 2左 3右
    }

    // ─────────────────────────────────────────────────
    // 从 Canvas RectTransform 取逻辑尺寸，兼容 WorldSpace
    // ─────────────────────────────────────────────────
    private Vector2 CanvasSize => _canvasRT.rect.size;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        BuildUI();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────────
    // 自动构建所有 UI
    // ─────────────────────────────────────────────────
    private void BuildUI()
    {
        // Canvas
        GameObject canvasGO = new GameObject("CoffinCanvas");
        DontDestroyOnLoad(canvasGO);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();

        // 缓存 RectTransform，确保 pivot 在中心（WorldSpace 兼容）
        _canvasRT = canvasGO.GetComponent<RectTransform>();
        _canvasRT.pivot = new Vector2(0.5f, 0.5f);

        // 黑屏
        _blackScreen = CreateImage("BlackScreen", canvasGO.transform, Color.black);
        StretchFull(_blackScreen.rectTransform);
        _blackScreen.gameObject.SetActive(false);

        // 棺椁外层边框（粗重）
        _coffinTop       = CreateImage("CoffinTop",       canvasGO.transform, coffinColor).rectTransform;
        _coffinBottom    = CreateImage("CoffinBottom",    canvasGO.transform, coffinColor).rectTransform;
        _coffinLeft      = CreateImage("CoffinLeft",      canvasGO.transform, coffinColor).rectTransform;
        _coffinRight     = CreateImage("CoffinRight",     canvasGO.transform, coffinColor).rectTransform;

        // 棺椁内层边框（更暗，制造厚度感）
        _coffinTopInner    = CreateImage("CoffinTopIn",    canvasGO.transform, coffinEdgeColor).rectTransform;
        _coffinBottomInner = CreateImage("CoffinBottomIn", canvasGO.transform, coffinEdgeColor).rectTransform;
        _coffinLeftInner   = CreateImage("CoffinLeftIn",   canvasGO.transform, coffinEdgeColor).rectTransform;
        _coffinRightInner  = CreateImage("CoffinRightIn",  canvasGO.transform, coffinEdgeColor).rectTransform;

        // 四角装饰块（方正棺钉）
        for (int i = 0; i < 4; i++)
        {
            var corner = CreateImage("Corner_" + i, canvasGO.transform, coffinEdgeColor);
            _corners[i] = corner.rectTransform;
        }

        SetCoffinActive(false);
    }

    private Image CreateImage(string name, Transform parent, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        Image img = go.GetComponent<Image>();
        img.color         = color;
        img.raycastTarget = false;
        return img;
    }

    private void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    // ─────────────────────────────────────────────────
    // 二进制代码流（密集列式，从四边向中心飘动后冻结熄灭）
    // ─────────────────────────────────────────────────
    public IEnumerator PlayBinaryFlow(float duration)
    {
        SpawnBinaryColumns();

        float elapsed   = 0f;
        float fadeStart = duration * 0.55f;

        // 取一次 Canvas 逻辑中心（anchorMin=anchorMax=zero 坐标系下的中心）
        Vector2 canvasSize   = CanvasSize;
        Vector2 canvasCenter = canvasSize * 0.5f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            for (int c = 0; c < _binaryColumns.Count; c++)
            {
                var col = _binaryColumns[c];

                // 冻结判定
                if (!col.frozen && elapsed >= col.freezeTime)
                {
                    col.frozen = true;
                    _binaryColumns[c] = col;
                }

                foreach (var tmp in col.chars)
                {
                    if (tmp == null) continue;

                    if (!col.frozen)
                    {
                        // 活跃：随机刷新字符
                        if (Random.value < 0.15f)
                            tmp.text = Random.value > 0.5f ? "1" : "0";

                        // 向 Canvas 逻辑中心微移（不再依赖 Screen.width/height）
                        var rt = tmp.rectTransform;
                        Vector2 pos = rt.anchoredPosition;
                        Vector2 dir = (canvasCenter - pos).normalized;
                        rt.anchoredPosition = pos + dir * col.speed * Time.deltaTime;
                    }

                    // 淡出
                    if (elapsed > fadeStart)
                    {
                        float fadeT = (elapsed - fadeStart) / (duration - fadeStart);
                        Color clr = tmp.color;
                        clr.a = Mathf.Lerp(binaryColor.a, 0f, fadeT * fadeT);
                        tmp.color = clr;
                    }
                }
            }
            yield return null;
        }

        ClearBinaryTexts();
    }

    private void SpawnBinaryColumns()
    {
        ClearBinaryTexts();
        _binaryColumns.Clear();

        // 使用 Canvas 逻辑尺寸，兼容 WorldSpace Canvas
        Vector2 canvasSize = CanvasSize;
        float sw = canvasSize.x;
        float sh = canvasSize.y;

        int colsPerEdge = binaryColumnCount / 4;

        for (int edge = 0; edge < 4; edge++)
        {
            for (int c = 0; c < colsPerEdge; c++)
            {
                var column = new BinaryColumn
                {
                    chars      = new List<TextMeshProUGUI>(),
                    speed      = Random.Range(15f, 45f),
                    freezeTime = Random.Range(0.8f, 2.0f),
                    frozen     = false,
                    edge       = edge
                };

                for (int r = 0; r < binaryRowCount; r++)
                {
                    GameObject go = new GameObject("Bin", typeof(RectTransform), typeof(TextMeshProUGUI));
                    go.transform.SetParent(_canvas.transform, false);

                    TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
                    tmp.fontSize      = fontSize * (0.7f + Random.value * 0.6f);
                    tmp.color         = new Color(
                        binaryColor.r * (0.8f + Random.value * 0.4f),
                        binaryColor.g * (0.5f + Random.value),
                        binaryColor.b,
                        binaryColor.a * (0.4f + Random.value * 0.6f)
                    );
                    tmp.text          = Random.value > 0.5f ? "1" : "0";
                    tmp.alignment     = TextAlignmentOptions.Center;
                    tmp.raycastTarget = false;
                    tmp.fontStyle     = FontStyles.Bold;

                    RectTransform rt = go.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = Vector2.zero;
                    rt.pivot     = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(24f, 24f);

                    // 按边分布（坐标基于 Canvas 逻辑尺寸）
                    Vector2 pos = Vector2.zero;
                    float along = (float)(c * binaryRowCount + r) / (colsPerEdge * binaryRowCount);
                    float scatter = Random.Range(-15f, 15f);

                    switch (edge)
                    {
                        case 0: // 上
                            pos = new Vector2(along * sw, sh - Random.Range(0f, sh * 0.2f) + scatter);
                            break;
                        case 1: // 下
                            pos = new Vector2(along * sw, Random.Range(0f, sh * 0.2f) + scatter);
                            break;
                        case 2: // 左
                            pos = new Vector2(Random.Range(0f, sw * 0.2f) + scatter, along * sh);
                            break;
                        case 3: // 右
                            pos = new Vector2(sw - Random.Range(0f, sw * 0.2f) + scatter, along * sh);
                            break;
                    }
                    rt.anchoredPosition = pos;

                    column.chars.Add(tmp);
                    _binaryTexts.Add(tmp);
                }

                _binaryColumns.Add(column);
            }
        }
    }

    private void ClearBinaryTexts()
    {
        foreach (var tmp in _binaryTexts)
            if (tmp != null) Destroy(tmp.gameObject);
        _binaryTexts.Clear();
        _binaryColumns.Clear();
    }

    // ─────────────────────────────────────────────────
    // 棺椁收拢（粗重双层边框 + 角落棺钉）
    // ─────────────────────────────────────────────────
    public IEnumerator PlayCoffinConverge()
    {
        // 使用 Canvas 逻辑尺寸，兼容 WorldSpace Canvas
        Vector2 canvasSize = CanvasSize;
        float sw = canvasSize.x;
        float sh = canvasSize.y;
        float cx = sw * 0.5f;
        float cy = sh * 0.5f;

        float targetW = sw * 0.55f;
        float targetH = sh * 0.55f;
        float thick      = coffinThickness;
        float innerThick = thick * 0.4f;
        float cornerSize = thick * 1.8f;

        // ── 初始位置：四边外侧 ──
        // 外层
        SetRectPixel(_coffinTop,    cx,       sh + thick,  sw * 1.2f, thick);
        SetRectPixel(_coffinBottom, cx,       -thick,      sw * 1.2f, thick);
        SetRectPixel(_coffinLeft,   -thick,   cy,          thick,     sh * 1.2f);
        SetRectPixel(_coffinRight,  sw+thick, cy,          thick,     sh * 1.2f);
        // 内层
        SetRectPixel(_coffinTopInner,    cx,       sh + thick + innerThick, sw * 1.2f, innerThick);
        SetRectPixel(_coffinBottomInner, cx,       -thick - innerThick,     sw * 1.2f, innerThick);
        SetRectPixel(_coffinLeftInner,   -thick - innerThick, cy,           innerThick, sh * 1.2f);
        SetRectPixel(_coffinRightInner,  sw+thick+innerThick, cy,           innerThick, sh * 1.2f);
        // 角落隐藏
        for (int i = 0; i < 4; i++)
        {
            _corners[i].sizeDelta = new Vector2(cornerSize, cornerSize);
            _corners[i].gameObject.SetActive(false);
        }

        SetCoffinAlpha(1f);
        SetCoffinActive(true);

        // ── 目标位置 ──
        float topY    = cy + targetH * 0.5f;
        float bottomY = cy - targetH * 0.5f;
        float leftX   = cx - targetW * 0.5f;
        float rightX  = cx + targetW * 0.5f;

        // 外层动画
        DOTween.To(() => GetY(_coffinTop),    y => SetRectPixel(_coffinTop,    cx, y, targetW + thick * 2f, thick), topY + thick * 0.5f, coffinDuration).SetEase(Ease.InOutQuart);
        DOTween.To(() => GetY(_coffinBottom), y => SetRectPixel(_coffinBottom, cx, y, targetW + thick * 2f, thick), bottomY - thick * 0.5f, coffinDuration).SetEase(Ease.InOutQuart);
        DOTween.To(() => GetX(_coffinLeft),   x => SetRectPixel(_coffinLeft,   x, cy, thick, targetH + thick * 2f), leftX - thick * 0.5f, coffinDuration).SetEase(Ease.InOutQuart);
        DOTween.To(() => GetX(_coffinRight),  x => SetRectPixel(_coffinRight,  x, cy, thick, targetH + thick * 2f), rightX + thick * 0.5f, coffinDuration).SetEase(Ease.InOutQuart);

        // 内层动画（稍延迟）
        float innerDelay = 0.08f;
        DOTween.To(() => GetY(_coffinTopInner),    y => SetRectPixel(_coffinTopInner,    cx, y, targetW, innerThick), topY - innerThick * 0.5f, coffinDuration - innerDelay).SetEase(Ease.InOutQuart).SetDelay(innerDelay);
        DOTween.To(() => GetY(_coffinBottomInner), y => SetRectPixel(_coffinBottomInner, cx, y, targetW, innerThick), bottomY + innerThick * 0.5f, coffinDuration - innerDelay).SetEase(Ease.InOutQuart).SetDelay(innerDelay);
        DOTween.To(() => GetX(_coffinLeftInner),   x => SetRectPixel(_coffinLeftInner,   x, cy, innerThick, targetH), leftX + innerThick * 0.5f, coffinDuration - innerDelay).SetEase(Ease.InOutQuart).SetDelay(innerDelay);
        DOTween.To(() => GetX(_coffinRightInner),  x => SetRectPixel(_coffinRightInner,  x, cy, innerThick, targetH), rightX - innerThick * 0.5f, coffinDuration - innerDelay).SetEase(Ease.InOutQuart).SetDelay(innerDelay);

        yield return new WaitForSeconds(coffinDuration * 0.85f);

        // 角落棺钉出现
        Vector2[] cornerPositions = new Vector2[]
        {
            new Vector2(leftX,  topY),     // 左上
            new Vector2(rightX, topY),     // 右上
            new Vector2(leftX,  bottomY),  // 左下
            new Vector2(rightX, bottomY),  // 右下
        };
        for (int i = 0; i < 4; i++)
        {
            _corners[i].gameObject.SetActive(true);
            SetRectPixel(_corners[i], cornerPositions[i].x, cornerPositions[i].y, cornerSize, cornerSize);
            // 缩放弹入
            _corners[i].localScale = Vector3.zero;
            _corners[i].DOScale(Vector3.one, 0.2f).SetEase(Ease.OutBack);
        }

        yield return new WaitForSeconds(coffinDuration * 0.15f + 0.2f);
    }

    // ─────────────────────────────────────────────────
    // 黑屏（瞬间断电，非渐变）
    // ─────────────────────────────────────────────────
    public IEnumerator PlayBlackout(float holdDuration)
    {
        // 瞬间切黑 —— 不用渐变，模拟断电
        _blackScreen.gameObject.SetActive(true);
        var c = _blackScreen.color;
        c.a = 1f;
        _blackScreen.color = c;

        yield return new WaitForSeconds(holdDuration);

        // 恢复：缓慢淡出（从黑暗中苏醒）
        yield return DOTween.To(
            () => _blackScreen.color.a,
            a  => { var col = _blackScreen.color; col.a = a; _blackScreen.color = col; },
            0f, 0.4f).SetEase(Ease.OutQuad).WaitForCompletion();

        _blackScreen.gameObject.SetActive(false);
        SetCoffinActive(false);
    }

    // ─────────────────────────────────────────────────
    // 辅助方法
    // ─────────────────────────────────────────────────
    private void SetRectPixel(RectTransform rt, float px, float py, float w, float h)
    {
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(px, py);
    }

    private float GetX(RectTransform rt) => rt.anchoredPosition.x;
    private float GetY(RectTransform rt) => rt.anchoredPosition.y;

    private void SetCoffinActive(bool v)
    {
        if (_coffinTop)          _coffinTop.gameObject.SetActive(v);
        if (_coffinBottom)       _coffinBottom.gameObject.SetActive(v);
        if (_coffinLeft)         _coffinLeft.gameObject.SetActive(v);
        if (_coffinRight)        _coffinRight.gameObject.SetActive(v);
        if (_coffinTopInner)     _coffinTopInner.gameObject.SetActive(v);
        if (_coffinBottomInner)  _coffinBottomInner.gameObject.SetActive(v);
        if (_coffinLeftInner)    _coffinLeftInner.gameObject.SetActive(v);
        if (_coffinRightInner)   _coffinRightInner.gameObject.SetActive(v);
        for (int i = 0; i < 4; i++)
            if (_corners[i]) _corners[i].gameObject.SetActive(v);
    }

    private void SetCoffinAlpha(float a)
    {
        void Apply(RectTransform rt)
        {
            if (rt == null) return;
            var img = rt.GetComponent<Image>();
            if (img == null) return;
            var clr = img.color; clr.a = a; img.color = clr;
        }
        Apply(_coffinTop);         Apply(_coffinBottom);
        Apply(_coffinLeft);        Apply(_coffinRight);
        Apply(_coffinTopInner);    Apply(_coffinBottomInner);
        Apply(_coffinLeftInner);   Apply(_coffinRightInner);
        for (int i = 0; i < 4; i++) Apply(_corners[i]);
    }
}