using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 挂在水面分割线 GameObject（Image）上。
///
/// 效果：
///   · 一条横贯全屏的细发光线，位于倒影区顶部
///   · 颜色跟随阵营切换（希望→蓝，熄忘→红）
///   · 带呼吸光晕效果（宽度和透明度轻微脉动）
///   · 阵营切换时触发一次「扫描线」闪光动画
///
/// 场景搭建：
///   1. 在 Canvas 里新建 Image，命名 WaterDivider
///   2. RectTransform：anchorMin=(0, reflectionRatio), anchorMax=(1, reflectionRatio)
///      offsetMin=(0, -lineHalfHeight), offsetMax=(0, lineHalfHeight)
///      （也可以全部交给此脚本自动设置，见 autoPosition 参数）
///   3. 把此脚本挂上去，Image 的 Color 交由脚本管理
///   4. Image Source Image 留空（纯色即可）
/// </summary>
[RequireComponent(typeof(Image))]
public class WaterDivider : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────

    [Header("位置（自动对齐水面线）")]
    [Tooltip("开启后脚本自动根据 reflectionHeightRatio 设置 RectTransform")]
    [SerializeField] private bool  autoPosition        = true;
    [Tooltip("与 ReflectionCamera 里的 reflectionHeightRatio 保持一致")]
    [Range(0.1f, 0.6f)]
    [SerializeField] private float reflectionHeightRatio = 0.4f;

    [Header("线条外观")]
    [Tooltip("主线条高度（像素）")]
    [SerializeField] private float lineHeight    = 2f;
    [Tooltip("光晕扩散高度（像素）。叠加在主线上方/下方，透明度较低")]
    [SerializeField] private float glowHeight    = 18f;
    [Tooltip("线条基础亮度（Alpha）")]
    [Range(0f, 1f)]
    [SerializeField] private float lineAlpha     = 0.9f;
    [Tooltip("光晕基础亮度（Alpha）")]
    [Range(0f, 1f)]
    [SerializeField] private float glowAlpha     = 0.25f;

    [Header("呼吸动画")]
    [Tooltip("呼吸周期（秒）")]
    [SerializeField] private float breathPeriod  = 2.5f;
    [Tooltip("呼吸时 Alpha 变化幅度")]
    [Range(0f, 0.5f)]
    [SerializeField] private float breathAmount  = 0.15f;
    [Tooltip("呼吸时线条高度变化幅度（像素）")]
    [Range(0f, 8f)]
    [SerializeField] private float breathHeightAmount = 2f;

    [Header("扫描线闪光（阵营切换时）")]
    [Tooltip("扫描闪光持续时长（秒）")]
    [SerializeField] private float flashDuration = 0.35f;
    [Tooltip("扫描闪光时线条最大 Alpha")]
    [Range(0f, 1f)]
    [SerializeField] private float flashAlpha    = 1f;
    [Tooltip("扫描闪光时线条最大高度（像素）")]
    [SerializeField] private float flashHeight   = 8f;

    [Header("阵营配色")]
    [SerializeField] private Color hopeColor = new Color(0.29f, 0.62f, 1f);
    [SerializeField] private Color voidColor = new Color(0.86f, 0.20f, 0.20f);

    [Header("切换动画时长")]
    [SerializeField] private float switchDuration = 0.8f;

    // ─────────────────────────────────────────────────
    // 子对象（光晕层）
    // ─────────────────────────────────────────────────

    private Image         _lineImg;
    private Image         _glowImg;
    private RectTransform _lineRect;
    private RectTransform _glowRect;
    private RectTransform _selfRect;

    private Color  _currentColor = new Color(0.29f, 0.62f, 1f);
    private float  _time = 0f;
    private bool   _flashing = false;
    private Tweener _colorTween;

    // ─────────────────────────────────────────────────
    // 生命周期
    // ─────────────────────────────────────────────────

    void Awake()
    {
        _lineImg  = GetComponent<Image>();
        _selfRect = GetComponent<RectTransform>();
        _lineRect = _selfRect;

        EnsureGlowChild();
    }

    void Start()
    {
        if (autoPosition)
            ApplyPosition();

        ApplyColor(hopeColor, instant: true);
        TitleScreenManager.OnFactionChanged += OnFactionChanged;
    }

    void OnDestroy()
    {
        TitleScreenManager.OnFactionChanged -= OnFactionChanged;
        _colorTween?.Kill();
    }

    void Update()
    {
        _time += Time.deltaTime;

        if (!_flashing)
            UpdateBreath();
    }

    // ─────────────────────────────────────────────────
    // 位置自动设置
    // ─────────────────────────────────────────────────

    private void ApplyPosition()
    {
        if (_selfRect == null) return;

        float r = reflectionHeightRatio;
        _selfRect.anchorMin = new Vector2(0f, r);
        _selfRect.anchorMax = new Vector2(1f, r);
        _selfRect.pivot     = new Vector2(0.5f, 0.5f);
        _selfRect.offsetMin = new Vector2(0f, -lineHeight * 0.5f);
        _selfRect.offsetMax = new Vector2(0f,  lineHeight * 0.5f);
    }

    // ─────────────────────────────────────────────────
    // 呼吸动画
    // ─────────────────────────────────────────────────

    private void UpdateBreath()
    {
        float breath = Mathf.Sin(_time * (Mathf.PI * 2f / breathPeriod)) * 0.5f + 0.5f;

        // 主线
        float currentLineH     = lineHeight + breath * breathHeightAmount;
        float currentLineAlpha = lineAlpha  + breath * breathAmount - breathAmount * 0.5f;
        currentLineAlpha = Mathf.Clamp01(currentLineAlpha);

        _lineRect.offsetMin = new Vector2(0f, -currentLineH * 0.5f);
        _lineRect.offsetMax = new Vector2(0f,  currentLineH * 0.5f);
        _lineImg.color = new Color(_currentColor.r, _currentColor.g, _currentColor.b, currentLineAlpha);

        // 光晕
        if (_glowImg != null && _glowRect != null)
        {
            float currentGlowH     = glowHeight + breath * breathHeightAmount * 3f;
            float currentGlowAlpha = glowAlpha  + breath * breathAmount * 0.5f;
            currentGlowAlpha = Mathf.Clamp01(currentGlowAlpha);

            _glowRect.offsetMin = new Vector2(0f, -currentGlowH * 0.5f);
            _glowRect.offsetMax = new Vector2(0f,  currentGlowH * 0.5f);
            _glowImg.color = new Color(_currentColor.r, _currentColor.g, _currentColor.b, currentGlowAlpha);
        }
    }

    // ─────────────────────────────────────────────────
    // 扫描线闪光
    // ─────────────────────────────────────────────────

    private System.Collections.IEnumerator FlashRoutine()
    {
        _flashing = true;
        float elapsed = 0f;

        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float p    = elapsed / flashDuration;
            float ease = p < 0.3f ? p / 0.3f : 1f - (p - 0.3f) / 0.7f;

            float h = Mathf.Lerp(lineHeight, flashHeight, ease);
            float a = Mathf.Lerp(lineAlpha,  flashAlpha,  ease);

            _lineRect.offsetMin = new Vector2(0f, -h * 0.5f);
            _lineRect.offsetMax = new Vector2(0f,  h * 0.5f);
            _lineImg.color = new Color(_currentColor.r, _currentColor.g, _currentColor.b, a);

            if (_glowImg != null && _glowRect != null)
            {
                float gh = Mathf.Lerp(glowHeight, flashHeight * 4f, ease);
                float ga = Mathf.Lerp(glowAlpha,  flashAlpha * 0.6f, ease);
                _glowRect.offsetMin = new Vector2(0f, -gh * 0.5f);
                _glowRect.offsetMax = new Vector2(0f,  gh * 0.5f);
                _glowImg.color = new Color(_currentColor.r, _currentColor.g, _currentColor.b, ga);
            }

            yield return null;
        }

        _flashing = false;
    }

    // ─────────────────────────────────────────────────
    // 阵营切换
    // ─────────────────────────────────────────────────

    private void OnFactionChanged(TitleScreenManager.Faction faction)
    {
        Color target = faction == TitleScreenManager.Faction.Hope ? hopeColor : voidColor;
        ApplyColor(target, instant: false);
        StartCoroutine(FlashRoutine());
    }

    private void ApplyColor(Color c, bool instant)
    {
        _colorTween?.Kill();
        _currentColor = c;

        if (instant)
        {
            _lineImg.color = new Color(c.r, c.g, c.b, lineAlpha);
            if (_glowImg != null)
                _glowImg.color = new Color(c.r, c.g, c.b, glowAlpha);
        }
        else
        {
            _colorTween = DOTween.To(
                () => _currentColor,
                v  => _currentColor = v,
                c,
                switchDuration
            ).SetEase(Ease.InOutCubic);
        }
    }

    // ─────────────────────────────────────────────────
    // 光晕子对象
    // ─────────────────────────────────────────────────

    private void EnsureGlowChild()
    {
        // 复用已有的 Glow 子对象，或自动创建
        Transform existing = transform.Find("Glow");
        if (existing != null)
        {
            _glowImg  = existing.GetComponent<Image>();
            _glowRect = existing.GetComponent<RectTransform>();
            return;
        }

        GameObject go = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        _glowRect = go.GetComponent<RectTransform>();
        _glowRect.anchorMin = new Vector2(0f, 0.5f);
        _glowRect.anchorMax = new Vector2(1f, 0.5f);
        _glowRect.pivot     = new Vector2(0.5f, 0.5f);
        _glowRect.offsetMin = new Vector2(0f, -glowHeight * 0.5f);
        _glowRect.offsetMax = new Vector2(0f,  glowHeight * 0.5f);

        _glowImg = go.GetComponent<Image>();
        _glowImg.color         = new Color(hopeColor.r, hopeColor.g, hopeColor.b, glowAlpha);
        _glowImg.raycastTarget = false;

        // 光晕用软边渐变——如果项目里有 Soft UI Sprite 可以替换
        // 没有的话纯色也有不错的发光感（配合 Canvas 的 Additive 材质更好）
        go.transform.SetAsFirstSibling(); // 光晕在线条下方渲染
    }

    // ─────────────────────────────────────────────────
    // 公开接口
    // ─────────────────────────────────────────────────

    /// <summary>运行时更新倒影高度比例（与 ReflectionCamera 联动时调用）</summary>
    public void SetReflectionHeightRatio(float ratio)
    {
        reflectionHeightRatio = Mathf.Clamp(ratio, 0.1f, 0.6f);
        if (autoPosition) ApplyPosition();
    }
}
