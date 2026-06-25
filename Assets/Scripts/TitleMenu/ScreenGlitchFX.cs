using UnityEngine;

/// <summary>
/// 挂在 MainCamera 上。
/// 负责块撕裂、RGB 分离、LED 点阵的后处理效果（OnRenderImage）。
/// 由 ScreenGlitchUI 驱动强度值。
/// </summary>
[RequireComponent(typeof(Camera))]
public class ScreenGlitchFX : MonoBehaviour
{
    [Header("Shader（拖入 XiWang/ScreenGlitch）")]
    [SerializeField] private Shader glitchShader;

    [Header("LED 点阵（拖入 XiWang_XiWang/HopeLED）")]
    [SerializeField] private Shader ledShader;

    [Header("块撕裂")]
    public float blockSize     = 0.04f;
    public float tearMaxShift  = 0.14f;
    public float tearBigChance = 0.9f;
    public float tearBigShift  = 0.4f;

    [Header("RGB 分离")]
    public float rgbHOffset = 0.016f;
    public float rgbVOffset = 0.022f;

    private Material _mat;
    private Material _ledMat;
    private float    _tearIntensity;
    private float    _rgbIntensity;
    private float    _time;

    // LED 参数（由 ScreenGlitchUI 驱动）
    public float LEDIntensity  { get; set; } = 0f;
    public float LEDSize       { get; set; } = 6f;
    public float LEDGapRatio   { get; set; } = 0.25f;
    public float LEDBrightness { get; set; } = 1.2f;
    public float LEDColorShift { get; set; } = 0.35f;

    public float TearIntensity { set => _tearIntensity = value; }
    public float RGBIntensity  { set => _rgbIntensity  = value; }

    void Awake()
    {
        if (glitchShader == null)
            glitchShader = Shader.Find("XiWang/ScreenGlitch");
        if (glitchShader != null)
            _mat = new Material(glitchShader) { hideFlags = HideFlags.HideAndDontSave };

        if (ledShader == null)
            ledShader = Shader.Find("XiWang_XiWang/HopeLED");
        if (ledShader != null)
            _ledMat = new Material(ledShader) { hideFlags = HideFlags.HideAndDontSave };
    }

    void Update() => _time += Time.deltaTime;

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        bool hasGlitch = _tearIntensity > 0.001f || _rgbIntensity > 0.001f;
        bool hasLED    = LEDIntensity   > 0.001f && _ledMat != null;

        if (!hasGlitch && !hasLED)
        {
            Graphics.Blit(src, dst);
            return;
        }

        // 两个 pass 都需要时，用临时 RT 串联
        RenderTexture tmp = (hasGlitch && hasLED)
            ? RenderTexture.GetTemporary(src.descriptor)
            : null;

        // ── Pass 1：块撕裂 + RGB 分离 ──────────────────
        if (hasGlitch && _mat != null)
        {
            _mat.SetFloat("_TearIntensity", _tearIntensity);
            _mat.SetFloat("_TearSeed",      _time * 60f);
            _mat.SetFloat("_BlockSize",     blockSize);
            _mat.SetFloat("_TearMaxShift",  tearMaxShift);
            _mat.SetFloat("_TearBigChance", tearBigChance);
            _mat.SetFloat("_TearBigShift",  tearBigShift);
            _mat.SetFloat("_RGBIntensity",  _rgbIntensity);
            _mat.SetFloat("_RGBHOffset",    rgbHOffset);
            _mat.SetFloat("_RGBVOffset",    rgbVOffset);
            Graphics.Blit(src, hasLED ? tmp : dst, _mat);
        }
        else if (hasLED)
        {
            // 无 glitch，把 src 原样复制到 tmp 供 LED pass 读取
            Graphics.Blit(src, tmp);
        }

        // ── Pass 2：LED 点阵 ───────────────────────────
        if (hasLED)
        {
            RenderTexture ledSrc = hasGlitch ? tmp : src;
            // 用 RT 实际尺寸传给 shader，保证 LED 物理像素大小正确
            _ledMat.SetFloat("_LedSize",    LEDSize);
            _ledMat.SetFloat("_GapRatio",   LEDGapRatio);
            _ledMat.SetFloat("_Brightness", LEDBrightness);
            _ledMat.SetFloat("_ColorShift", LEDColorShift);
            _ledMat.SetFloat("_Intensity",  LEDIntensity);
            _ledMat.SetFloat("_ScreenW",    src.width);
            _ledMat.SetFloat("_ScreenH",    src.height);
            Graphics.Blit(ledSrc, dst, _ledMat);
        }

        if (tmp != null) RenderTexture.ReleaseTemporary(tmp);
    }

    void OnDestroy()
    {
        if (_mat    != null) DestroyImmediate(_mat);
        if (_ledMat != null) DestroyImmediate(_ledMat);
    }
}