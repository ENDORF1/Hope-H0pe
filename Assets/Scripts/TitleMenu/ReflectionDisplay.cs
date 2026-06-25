using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 Canvas B 里的 ReflectionDisplay（RawImage）上。
/// 每帧把参数传给 ReflectionDistortion Shader，驱动倒影后处理效果。
/// </summary>
[RequireComponent(typeof(RawImage))]
public class ReflectionDisplay : MonoBehaviour
{
    [Header("材质")]
    [Tooltip("拖入使用 XiWang/ReflectionDistortion Shader 的 Material")]
    [SerializeField] private Material distortionMaterial;

    [Header("波纹扰动")]
    [SerializeField] [Range(0f, 0.05f)] private float waveAmplitude  = 0.006f;
    [SerializeField] [Range(1f, 40f)]   private float waveFrequency  = 12f;
    [SerializeField] [Range(0f, 5f)]    private float waveSpeed      = 1.0f;
    [SerializeField] [Range(0f, 5f)]    private float waveDepthScale = 1.8f;

    [Header("模糊")]
    [SerializeField] [Range(0f, 0.02f)] private float blurRadius     = 0.003f;
    [SerializeField] [Range(0f, 6f)]    private float blurDepthScale = 2.5f;

    [Header("压暗与色调")]
    [SerializeField] [Range(0f, 1f)]    private float brightness     = 0.45f;
    [SerializeField]                    private Color tintColor       = new Color(0.85f, 0.92f, 1.0f, 1f);

    [Header("渐变遮罩")]
    [SerializeField] [Range(0f, 1f)]    private float fadeStart      = 0.15f;
    [SerializeField] [Range(0f, 1f)]    private float fadeEnd        = 0.85f;
    [SerializeField] [Range(0.5f, 5f)]  private float fadePower      = 2.0f;

    private static readonly int _propTime           = Shader.PropertyToID("_Time2");
    private static readonly int _propWaveAmplitude  = Shader.PropertyToID("_WaveAmplitude");
    private static readonly int _propWaveFrequency  = Shader.PropertyToID("_WaveFrequency");
    private static readonly int _propWaveSpeed      = Shader.PropertyToID("_WaveSpeed");
    private static readonly int _propWaveDepthScale = Shader.PropertyToID("_WaveDepthScale");
    private static readonly int _propBlurRadius     = Shader.PropertyToID("_BlurRadius");
    private static readonly int _propBlurDepthScale = Shader.PropertyToID("_BlurDepthScale");
    private static readonly int _propBrightness     = Shader.PropertyToID("_Brightness");
    private static readonly int _propTintColor      = Shader.PropertyToID("_TintColor");
    private static readonly int _propFadeStart      = Shader.PropertyToID("_FadeStart");
    private static readonly int _propFadeEnd        = Shader.PropertyToID("_FadeEnd");
    private static readonly int _propFadePower      = Shader.PropertyToID("_FadePower");

    private RawImage _rawImage;
    private float    _time = 0f;

    void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        if (distortionMaterial != null)
            _rawImage.material = distortionMaterial;
    }

    void Update()
    {
        if (distortionMaterial == null) return;
        _time += Time.deltaTime;

        distortionMaterial.SetFloat(_propTime,           _time);
        distortionMaterial.SetFloat(_propWaveAmplitude,  waveAmplitude);
        distortionMaterial.SetFloat(_propWaveFrequency,  waveFrequency);
        distortionMaterial.SetFloat(_propWaveSpeed,      waveSpeed);
        distortionMaterial.SetFloat(_propWaveDepthScale, waveDepthScale);
        distortionMaterial.SetFloat(_propBlurRadius,     blurRadius);
        distortionMaterial.SetFloat(_propBlurDepthScale, blurDepthScale);
        distortionMaterial.SetFloat(_propBrightness,     brightness);
        distortionMaterial.SetColor(_propTintColor,      tintColor);
        distortionMaterial.SetFloat(_propFadeStart,      fadeStart);
        distortionMaterial.SetFloat(_propFadeEnd,        fadeEnd);
        distortionMaterial.SetFloat(_propFadePower,      fadePower);

        _rawImage.SetMaterialDirty();
    }

    public void SetTintColor(Color c) => tintColor = c;
}