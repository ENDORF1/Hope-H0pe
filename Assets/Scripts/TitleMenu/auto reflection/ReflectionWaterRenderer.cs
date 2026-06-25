using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(RawImage))]
public class ReflectionWaterRenderer : MonoBehaviour
{
    static readonly int _propTime      = Shader.PropertyToID("_Time2");
    static readonly int _propStrength  = Shader.PropertyToID("_WaveStrength");
    static readonly int _propSpeed     = Shader.PropertyToID("_WaveSpeed");
    static readonly int _propFreqX     = Shader.PropertyToID("_WaveFreqX");
    static readonly int _propFreqY     = Shader.PropertyToID("_WaveFreqY");
    static readonly int _propFalloff   = Shader.PropertyToID("_DistortionFalloff");
    static readonly int _propAlpha     = Shader.PropertyToID("_Alpha");
    static readonly int _propTint      = Shader.PropertyToID("_TintColor");
    static readonly int _propFadeStart = Shader.PropertyToID("_FadeStart");
    static readonly int _propFadeEnd   = Shader.PropertyToID("_FadeEnd");
    static readonly int _propChromatic = Shader.PropertyToID("_ChromaticAberration");
    static readonly int _propScanline  = Shader.PropertyToID("_ScanlineStrength");
    static readonly int _propMaskAlpha = Shader.PropertyToID("_MaskAlpha");
    static readonly int _propFlipY     = Shader.PropertyToID("_FlipY");

    [Header("来源")]
    public ReflectionCamera reflectionCamera;
    public Material         waterMaterial;

    [Header("波形")]
    [Range(0f, 0.05f)] public float waveStrength = 0.008f;
    [Range(0f, 5f)]    public float waveSpeed    = 1.2f;
    [Range(0f, 20f)]   public float waveFreqX    = 6f;
    [Range(0f, 20f)]   public float waveFreqY    = 4f;
    [Range(0f, 2f)]    public float falloff      = 1.0f;

    [Header("视觉")]
    [Range(0f, 1f)]    public float reflAlpha  = 0.55f;
    [Range(0f, 1f)]    public float fadeStart  = 0.3f;
    [Range(0f, 1f)]    public float fadeEnd    = 1.0f;
    [Range(0f, 0.01f)] public float chromatic  = 0.002f;
    [Range(0f, 1f)]    public float scanline   = 0.15f;

    [Header("遮罩")]
    [Tooltip("渐隐遮罩强度（0=关闭遮罩，1=完全渐隐）")]
    [Range(0f, 1f)]    public float maskAlpha  = 1.0f;

    [Header("阵营色调")]
    public Color hopeTint    = new Color(0.29f, 0.62f, 1.00f, 0.12f);
    public Color voidTint    = new Color(0.86f, 0.20f, 0.20f, 0.12f);
    public float tintDuration = 0.8f;

    private RawImage _rawImage;
    private Material _matInst;
    private float    _time;
    private Color    _tint;
    void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        _tint     = hopeTint;

        if (waterMaterial != null)
        {
            _matInst           = Instantiate(waterMaterial);
            _rawImage.material = _matInst;
        }
    }

    void OnEnable()  => TitleScreenManager.OnFactionChanged += OnFactionChanged;
    void OnDisable() => TitleScreenManager.OnFactionChanged -= OnFactionChanged;

    void Update()
    {
        _time += Time.deltaTime;

        if (_rawImage.texture == null)
            TryBindRT();

        _rawImage.uvRect = new Rect(0f, 1f, 1f, -1f);
        PushShaderParams();
    }

    void TryBindRT()
    {
        if (reflectionCamera == null) return;
        if (reflectionCamera.RT == null) return;

        // RawImage 设置了自定义 material 后，
        // texture 属性失效，必须直接设置 material 的 _MainTex
        _rawImage.texture = reflectionCamera.RT;
        if (_matInst != null)
            _matInst.mainTexture = reflectionCamera.RT;
        _rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
        Debug.Log("[ReflectionWaterRenderer] RT 已绑定到 RawImage 和 Material");
    }

    void PushShaderParams()
    {
        if (_matInst == null) return;
        _matInst.SetFloat(_propTime,      _time);
        _matInst.SetFloat(_propStrength,  waveStrength);
        _matInst.SetFloat(_propSpeed,     waveSpeed);
        _matInst.SetFloat(_propFreqX,     waveFreqX);
        _matInst.SetFloat(_propFreqY,     waveFreqY);
        _matInst.SetFloat(_propFalloff,   falloff);
        _matInst.SetFloat(_propAlpha,     reflAlpha);
        _matInst.SetFloat(_propFadeStart, fadeStart);
        _matInst.SetFloat(_propFadeEnd,   fadeEnd);
        _matInst.SetFloat(_propChromatic, chromatic);
        _matInst.SetFloat(_propScanline,  scanline);
        _matInst.SetColor(_propTint,      _tint);
        _matInst.SetFloat(_propMaskAlpha, maskAlpha);
        _matInst.SetFloat(_propFlipY,     1f);
    }

    void OnFactionChanged(TitleScreenManager.Faction f)
    {
        Color target = f == TitleScreenManager.Faction.Hope ? hopeTint : voidTint;
        DOTween.To(() => _tint, x => _tint = x, target, tintDuration);
    }

    void OnDestroy()
    {
        if (_matInst != null) Destroy(_matInst);
    }
}