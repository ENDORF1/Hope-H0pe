using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

[RequireComponent(typeof(RawImage))]
public class TitleBackground : MonoBehaviour
{
    [Header("Material")]
    [SerializeField] private Material backgroundMaterial;

    [Header("Colors")]
    [SerializeField] private Color hopeColor = new Color(0.29f, 0.62f, 1f);
    [SerializeField] private Color voidColor = new Color(0.86f, 0.20f, 0.20f);

    [Header("Transition Duration")]
    [SerializeField] private float transitionDuration = 0.8f;

    [Header("切换滚动衔接")]
    [Tooltip("画面滚动一圈的时间（秒），越短越快。")]
    [SerializeField] private float rollDuration = 0.4f;

    private static readonly int _propBlend     = Shader.PropertyToID("_Blend");
    private static readonly int _propTime      = Shader.PropertyToID("_Time2");
    private static readonly int _propHopeColor = Shader.PropertyToID("_HopeColor");
    private static readonly int _propVoidColor = Shader.PropertyToID("_VoidColor");
    private static readonly int _propAspect    = Shader.PropertyToID("_Aspect");
    private static readonly int _propDarkness  = Shader.PropertyToID("_Darkness");
    private static readonly int _propBeatInterval = Shader.PropertyToID("_BeatInterval");
    private static readonly int _propRollOffset  = Shader.PropertyToID("_RollOffset");
    private static readonly int _propScrollX     = Shader.PropertyToID("_ScrollOffsetX");

    private RawImage _rawImage;
    private float    _time  = 0f;
    private float    _blend    = 0f;
    private float    _darkness    = 0f;
    private float    _rollOffset  = 0f;
    private float    _scrollOffsetX = 0f;
    private Tweener  _blendTween;
    private Coroutine _rollCoroutine;

    void Awake()
    {
        _rawImage = GetComponent<RawImage>();
        if (backgroundMaterial != null)
            _rawImage.material = backgroundMaterial;
        else
            Debug.LogError("[TitleBackground] backgroundMaterial not assigned");
    }

    void OnEnable()
    {
        TitleScreenManager.OnFactionChanged += OnFactionChanged;
    }

    void OnDisable()
    {
        TitleScreenManager.OnFactionChanged -= OnFactionChanged;
        _blendTween?.Kill();
    }

    void Start()
    {
        if (backgroundMaterial == null) return;
        backgroundMaterial.SetColor(_propHopeColor, hopeColor);
        backgroundMaterial.SetColor(_propVoidColor, voidColor);
        backgroundMaterial.SetFloat(_propBlend, 0f);
    }

    void Update()
    {
        if (backgroundMaterial == null) return;
        _time += Time.deltaTime;
        backgroundMaterial.SetFloat(_propTime,     _time);
        backgroundMaterial.SetFloat(_propBlend,   _blend);
        backgroundMaterial.SetFloat(_propAspect,  Screen.width / (float)Screen.height);
        backgroundMaterial.SetFloat(_propDarkness,  _darkness);
        backgroundMaterial.SetFloat(_propRollOffset, _rollOffset);
        backgroundMaterial.SetFloat(_propScrollX,    _scrollOffsetX);
        _rawImage.SetMaterialDirty();
    }

    /// <summary>由 voidGhost 协程调用，直接设置背景压暗程度（0=正常，1=全黑）</summary>
    public void SetDarkness(float value)
    {
        _darkness = Mathf.Clamp01(value);
    }

    /// <summary>由 hopeGhost 协程调用，设置背景 UV 垂直偏移模拟失步（0=正常）</summary>
    public void SetRollOffset(float value)
    {
        _rollOffset = value;
    }

    /// <summary>由 HopeTransition 调用，设置背景 UV 横向偏移模拟镜头左移（0=正常）</summary>
    public void SetScrollX(float value)
    {
        _scrollOffsetX = value;
    }

    /// <summary>强制对齐心跳相位，让下一帧从心跳起点开始播放</summary>
    public void TriggerBeat()
    {
        if (backgroundMaterial == null) return;
        float beatInterval = backgroundMaterial.GetFloat(_propBeatInterval);
        if (beatInterval <= 0f) beatInterval = 6f;
        // 把 _time 对齐到 beatInterval 整数倍，下一帧 fmod(t, beatInterval) = 0
        _time = Mathf.Floor(_time / beatInterval) * beatInterval;
    }

    private void OnFactionChanged(TitleScreenManager.Faction faction)
    {
        float target = faction == TitleScreenManager.Faction.Hope ? 0f : 1f;
        _blendTween?.Kill();
        _blendTween = DOTween.To(
            () => _blend,
            x  => _blend = x,
            target,
            transitionDuration
        ).SetEase(Ease.InOutCubic);

        if (_rollCoroutine != null) StopCoroutine(_rollCoroutine);
        _rollCoroutine = StartCoroutine(RollTransitionRoutine());
    }

    private System.Collections.IEnumerator RollTransitionRoutine()
    {
        float t = 0f;
        while (t < rollDuration)
        {
            // _rollOffset 从 0 匀速推到 1，画面向上连续滚动一圈
            _rollOffset = t / rollDuration;
            t          += Time.deltaTime;
            yield return null;
        }
        // 滚完归零，画面回到原位（frac(uv.y + 1) = frac(uv.y) 所以视觉上无跳变）
        _rollOffset    = 0f;
        _rollCoroutine = null;
    }

    void OnDestroy()
    {
        if (backgroundMaterial != null)
            backgroundMaterial.SetFloat(_propBlend, 0f);
    }
}