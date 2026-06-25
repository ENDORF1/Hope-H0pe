using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 天允终偿的视觉特效。
/// 挂在场景常驻对象上（推荐和 SceneRefs 同一对象）。
/// </summary>
public class TianYunEffects : MonoBehaviour
{
    public static TianYunEffects Instance { get; private set; }

    [Header("特效层")]
    [Tooltip("拖入 EffectsCanvas 对象，并将其 Render Mode 设为 Screen Space - Overlay。边框特效会自动铺满整个屏幕。")]
    [SerializeField] private Canvas targetCanvas;

    [Tooltip("拖入玩家牌库对象下的 CardGlow Image（例如 DeckCardBack/Canvas/Cardglow）。打出天允终偿时闪烁玩家牌库。")]
    [SerializeField] private Image playerDeckGlowImage;

    [Tooltip("拖入 AI 牌库对象下的 CardGlow Image。AI 打出天允终偿时闪烁 AI 牌库。")]
    [SerializeField] private Image aiDeckGlowImage;

    [Header("边框内发光参数")]
    [Tooltip("边框发光的厚度（屏幕比例，0.08 = 屏幕宽/高的 8%）")]
    [SerializeField] private float glowThickness = 0.08f;

    // 运行时创建的四条边发光
    private Image _glowTop;
    private Image _glowBottom;
    private Image _glowLeft;
    private Image _glowRight;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        if (targetCanvas == null)
        {
            Debug.LogWarning("[TianYunEffects] 请在 Inspector 中填入 Target Canvas");
            return;
        }
        BuildEdgeGlows();


    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────────────────────────────────────────
    // 构建四条边内发光（基于摄像机视野世界坐标定位）
    // ─────────────────────────────────────────────────
    private void BuildEdgeGlows()
    {
        _glowTop    = CreateEdgeImage("TianYun_GlowTop",    GradientDirection.TopToBottom);
        _glowBottom = CreateEdgeImage("TianYun_GlowBottom", GradientDirection.BottomToTop);
        _glowLeft   = CreateEdgeImage("TianYun_GlowLeft",   GradientDirection.LeftToRight);
        _glowRight  = CreateEdgeImage("TianYun_GlowRight",  GradientDirection.RightToLeft);

        FitGlowsToCanvas();

        _glowTop.gameObject.SetActive(false);
        _glowBottom.gameObject.SetActive(false);
        _glowLeft.gameObject.SetActive(false);
        _glowRight.gameObject.SetActive(false);
    }

    /// <summary>
    /// Screen Space Overlay 下用 Screen.width/height 直接设置像素尺寸和位置。
    /// 不依赖 Canvas 尺寸，永远和屏幕对齐。
    /// </summary>
    private void FitGlowsToCanvas()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        float cx = sw * 0.5f;
        float cy = sh * 0.5f;
        float thickW = sw * glowThickness;
        float thickH = sh * glowThickness;

        // Screen Space Overlay 下坐标原点在屏幕左下，RectTransform pivot=0.5
        // 上边
        SetPixelRect(_glowTop,    cx, sh - thickH * 0.5f,  sw, thickH);
        // 下边
        SetPixelRect(_glowBottom, cx, thickH * 0.5f,        sw, thickH);
        // 左边
        SetPixelRect(_glowLeft,   thickW * 0.5f, cy,        thickW, sh - thickH * 2f);
        // 右边
        SetPixelRect(_glowRight,  sw - thickW * 0.5f, cy,  thickW, sh - thickH * 2f);
    }

    private void SetPixelRect(Image img, float px, float py, float w, float h)
    {
        RectTransform rt = img.rectTransform;
        rt.anchorMin     = Vector2.zero;
        rt.anchorMax     = Vector2.zero;
        rt.pivot         = new Vector2(0.5f, 0.5f);
        rt.sizeDelta     = new Vector2(w, h);
        rt.anchoredPosition = new Vector2(px, py);
    }



    private enum GradientDirection { TopToBottom, BottomToTop, LeftToRight, RightToLeft }

    private Image CreateEdgeImage(string name, GradientDirection dir)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(targetCanvas.transform, false);
        go.transform.SetAsLastSibling();

        Image img = go.GetComponent<Image>();
        img.sprite        = CreateGradientSprite(dir);
        img.color         = Color.white;
        img.raycastTarget = false;
        img.type          = Image.Type.Simple;

        var c = img.color; c.a = 0f; img.color = c;
        return img;
    }

    /// <summary>生成单方向渐变（白色→透明）纹理</summary>
    private Sprite CreateGradientSprite(GradientDirection dir)
    {
        int w = dir == GradientDirection.LeftToRight || dir == GradientDirection.RightToLeft ? 64 : 4;
        int h = dir == GradientDirection.TopToBottom || dir == GradientDirection.BottomToTop ? 64 : 4;

        Texture2D tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                float t = 0f;
                switch (dir)
                {
                    case GradientDirection.TopToBottom:    t = 1f - (float)y / (h - 1); break;
                    case GradientDirection.BottomToTop:    t = (float)y / (h - 1);      break;
                    case GradientDirection.LeftToRight:    t = 1f - (float)x / (w - 1); break;
                    case GradientDirection.RightToLeft:    t = (float)x / (w - 1);      break;
                }
                // 平方让中间衰减更柔和
                t = t * t;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, t));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
    }

    [Header("呼吸参数")]
    [Tooltip("呼吸动画最高亮度（0~1）")]
    [SerializeField] private float breathMax   = 0.85f;
    [Tooltip("呼吸动画最低亮度（0~1）")]
    [SerializeField] private float breathMin   = 0.25f;
    [Tooltip("单次呼吸时长（秒），一呼一吸各为此时长的一半")]
    [SerializeField] private float breathSpeed = 1.0f;

    // 呼吸循环控制
    private bool      _breathing      = false;
    private Tweener   _breathTweener  = null;

    // ─────────────────────────────────────────────────
    // 效果1：边框内发光 + 持续呼吸
    // 调用 StartBreathing() 开启，调用 StopBreathing() 停止并淡出
    // ─────────────────────────────────────────────────
    public IEnumerator PlayCastFlash()
    {
        yield return StartBreathing();
    }

    /// <summary>启动边框呼吸发光（淡入 + 开始循环），立即返回不阻塞</summary>
    public IEnumerator StartBreathing()
    {
        Debug.Log($"[TianYunEffects] StartBreathing: _glowTop={_glowTop}, targetCanvas={targetCanvas}");
        if (_glowTop == null) { Debug.LogError("[TianYunEffects] _glowTop 为空！请确认 Target Canvas 和 Main Camera 已在 Inspector 填好，且 TianYunEffects 的 Start() 已运行"); yield break; }
        if (_breathing) yield break;

        _breathing = true;
        SetEdgeGlowsColor(Color.white);
        SetEdgeGlowsActive(true);
        SetEdgeGlowsAlpha(0f);

        // 淡入到最亮
        yield return TweenEdgeGlows(breathMax, 0.3f, Ease.OutQuad);

        // 开始呼吸循环（不阻塞协程）
        BreathLoop();
    }

    private void BreathLoop()
    {
        if (!_breathing) return;

        _breathTweener = DOTween.To(
            () => _glowTop != null ? _glowTop.color.a : 0f,
            a  => SetEdgeGlowsAlpha(a),
            breathMin, breathSpeed * 0.5f)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                if (!_breathing) return;
                _breathTweener = DOTween.To(
                    () => _glowTop != null ? _glowTop.color.a : 0f,
                    a  => SetEdgeGlowsAlpha(a),
                    breathMax, breathSpeed * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .OnComplete(BreathLoop);
            });
    }

    /// <summary>停止呼吸并淡出，由 TianYunZhongChang 在抽牌完成后调用</summary>
    public IEnumerator StopBreathing()
    {
        if (!_breathing) yield break;
        _breathing = false;

        if (_breathTweener != null && _breathTweener.IsActive())
            _breathTweener.Kill();

        yield return TweenEdgeGlows(0f, 0.5f, Ease.InQuad);
        SetEdgeGlowsActive(false);
    }

    private void SetEdgeGlowsActive(bool active)
    {
        _glowTop.gameObject.SetActive(active);
        _glowBottom.gameObject.SetActive(active);
        _glowLeft.gameObject.SetActive(active);
        _glowRight.gameObject.SetActive(active);
    }

    private void SetEdgeGlowsAlpha(float a)
    {
        SetAlpha(_glowTop,    a);
        SetAlpha(_glowBottom, a);
        SetAlpha(_glowLeft,   a);
        SetAlpha(_glowRight,  a);
    }

    private IEnumerator TweenEdgeGlows(float targetAlpha, float duration, Ease ease)
    {
        yield return DOTween.To(
            () => _glowTop.color.a,
            a  => { SetAlpha(_glowTop, a); SetAlpha(_glowBottom, a);
                    SetAlpha(_glowLeft, a); SetAlpha(_glowRight, a); },
            targetAlpha, duration)
            .SetEase(ease)
            .WaitForCompletion();
    }

    private static void SetAlpha(Image img, float a)
    {
        if (img == null) return;
        var c = img.color; c.a = a; img.color = c;
    }

    // ─────────────────────────────────────────────────
    // 效果2：牌库彩虹闪烁
    // ─────────────────────────────────────────────────
    // 彩虹循环控制
    private bool _rainbowLooping = false;

    /// <summary>开始持续彩虹循环（抽牌期间调用）</summary>
    public IEnumerator StartDeckRainbow(bool isPlayer)
    {
        Image deckGlowImage = isPlayer ? playerDeckGlowImage : aiDeckGlowImage;
        if (deckGlowImage == null) yield break;

        _rainbowLooping = true;
        Color originalColor = deckGlowImage.color;
        bool  wasActive     = deckGlowImage.gameObject.activeSelf;

        Color[] rainbow = new Color[]
        {
            new Color(1f,   0.1f, 0.1f, 0.9f),
            new Color(1f,   0.5f, 0f,   0.9f),
            new Color(1f,   1f,   0f,   0.9f),
            new Color(0.1f, 1f,   0.1f, 0.9f),
            new Color(0f,   1f,   0.9f, 0.9f),
            new Color(0.1f, 0.3f, 1f,   0.9f),
            new Color(0.8f, 0f,   1f,   0.9f),
        };

        deckGlowImage.color = new Color(rainbow[0].r, rainbow[0].g, rainbow[0].b, 0f);
        deckGlowImage.gameObject.SetActive(true);

        // 淡入
        yield return DOTween.To(
            () => deckGlowImage.color.a,
            a => { var c = deckGlowImage.color; c.a = a; deckGlowImage.color = c; },
            rainbow[0].a, 0.05f).WaitForCompletion();

        // 持续循环直到 StopDeckRainbow 被调用
        int idx = 1;
        while (_rainbowLooping)
        {
            yield return deckGlowImage.DOColor(rainbow[idx], 0.07f)
                .SetEase(Ease.Linear).WaitForCompletion();
            idx = (idx + 1) % rainbow.Length;
        }

        // 淡出
        yield return DOTween.To(
            () => deckGlowImage.color.a,
            a => { var c = deckGlowImage.color; c.a = a; deckGlowImage.color = c; },
            0f, 0.12f).WaitForCompletion();

        deckGlowImage.color = originalColor;
        deckGlowImage.gameObject.SetActive(wasActive);
    }

    /// <summary>停止彩虹循环</summary>
    public void StopDeckRainbow()
    {
        _rainbowLooping = false;
    }

    // ─────────────────────────────────────────────────
    // 效果3：抽牌完成后停止呼吸并淡出
    // ─────────────────────────────────────────────────
    public IEnumerator PlayDrawCompleteFlash()
    {
        yield return StopBreathing();
    }

    // ─────────────────────────────────────────────────
    // 效果4：屏幕边缘金色脉冲（复用边框，改为金色）
    // ─────────────────────────────────────────────────
    public IEnumerator PlayEdgeGoldPulse()
    {
        if (_glowTop == null) yield break;

        // 临时改为金色
        Color gold = new Color(1f, 0.82f, 0.1f, 1f);
        SetEdgeGlowsColor(gold);
        SetEdgeGlowsActive(true);
        SetEdgeGlowsAlpha(0f);

        for (int i = 0; i < 2; i++)
        {
            yield return TweenEdgeGlows(0.85f, 0.2f, Ease.OutQuad);
            yield return TweenEdgeGlows(0f,    0.35f, Ease.InQuad);
            if (i < 1) yield return new WaitForSeconds(0.1f);
        }

        SetEdgeGlowsActive(false);
        // 还原为白色（供下次白光效果使用）
        SetEdgeGlowsColor(Color.white);
    }

    private void SetEdgeGlowsColor(Color color)
    {
        void Apply(Image img) {
            if (img == null) return;
            var c = color; c.a = img.color.a; img.color = c;
        }
        Apply(_glowTop); Apply(_glowBottom);
        Apply(_glowLeft); Apply(_glowRight);
    }
}