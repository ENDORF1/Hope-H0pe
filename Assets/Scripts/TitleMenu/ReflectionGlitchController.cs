using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 希望阵营倒影故障效果。
/// glitch 期间禁用 ReflectionWaterRenderer，用独立 RawImage 显示处理后的 RT。
/// </summary>
public class ReflectionGlitchController : MonoBehaviour
{
    [Header("依赖")]
    [SerializeField] private ReflectionCamera        reflCamera;
    [SerializeField] private ReflectionWaterRenderer waterRenderer;
    [SerializeField] private Material                glitchMaterial;

    [Header("文字引用（生成熄忘倒影用）")]
    [SerializeField] private TextMeshProUGUI titleMain;
    [SerializeField] private TextMeshProUGUI titleSub;
    [SerializeField] private TextMeshProUGUI factionLabel;
    [SerializeField] private Color           voidColor     = new Color(0.86f, 0.20f, 0.20f);
    [SerializeField] private string          voidTitleText = "熄忘";
    [SerializeField] private string          voidSubText   = "CARVE SILENCE AND THE UNIVERSE OBEYS";
    [SerializeField] private string          voidLabelText = "— 柩世方 · VOID —";

    [Header("触发间隔")]
    [SerializeField] private float intervalMin = 10f;
    [SerializeField] private float intervalMax = 25f;

    [Header("时序（秒）")]
    [SerializeField] private float buildUpDuration = 3f;
    [SerializeField] private float holdDuration    = 2f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    [Header("Shader 参数")]
    [SerializeField] private float seedSpeed = 15f;

    private enum Phase { Idle, BuildUp, Hold, FadeOut }
    private Phase _phase     = Phase.Idle;
    private float _phaseTime = 0f;
    private float _time      = 0f;
    private float _nextTrigger;
    private bool  _active    = false;

    private RenderTexture _voidRT;
    private RenderTexture _outputRT;

    // 独立显示用的 RawImage（glitch 期间启用）
    private RawImage _glitchImage;

    private string _origTitle, _origSub, _origLabel;
    private Color  _origTitleColor, _origSubColor, _origLabelColor;

    void OnEnable()  => TitleScreenManager.OnFactionChanged += OnFactionChanged;
    void OnDisable() { TitleScreenManager.OnFactionChanged -= OnFactionChanged; EndGlitch(true); }

    void Start()
    {
        CreateGlitchImage();
        _active = true;
        ScheduleNext();
    }

    void LateUpdate()
    {
        _time += Time.deltaTime;
        if (!_active) return;

        if (_phase == Phase.Idle)
        {
            if (_time >= _nextTrigger) StartGlitch();
            return;
        }

        _phaseTime += Time.deltaTime;

        switch (_phase)
        {
            case Phase.BuildUp:
                float bi = _phaseTime / buildUpDuration;
                ApplyGlitch(bi * bi);
                if (_phaseTime >= buildUpDuration) TransitionTo(Phase.Hold);
                break;

            case Phase.Hold:
                ApplyGlitch(1f);
                if (_phaseTime >= holdDuration) TransitionTo(Phase.FadeOut);
                break;

            case Phase.FadeOut:
                ApplyGlitch(Mathf.Max(0f, 1f - _phaseTime / fadeOutDuration));
                if (_phaseTime >= fadeOutDuration) EndGlitch(false);
                break;
        }
    }

    /// <summary>手动触发（供测试脚本调用）</summary>
    public void TriggerGlitch()
    {
        if (_phase != Phase.Idle) return;
        StartGlitch();
    }

    void StartGlitch()
    {
        if (reflCamera?.RT == null) return;
        EnsureRTs();

        // 渲染熄忘内容到 voidRT
        SaveOriginalText();
        ApplyVoidText();
        reflCamera.RenderOnce(_voidRT);
        RestoreOriginalText();

        // 启用独立 RawImage，禁用原来的 waterRenderer
        waterRenderer.enabled = false;
        _glitchImage.enabled  = true;

        TransitionTo(Phase.BuildUp);
    }

    void ApplyGlitch(float intensity)
    {
        if (glitchMaterial == null || reflCamera?.RT == null || _voidRT == null) return;

        glitchMaterial.SetTexture("_MainTex", reflCamera.RT);
        glitchMaterial.SetTexture("_VoidTex", _voidRT);
        glitchMaterial.SetFloat("_Intensity", intensity);
        glitchMaterial.SetFloat("_Seed",      _time * seedSpeed);

        Graphics.Blit(reflCamera.RT, _outputRT, glitchMaterial);
        _glitchImage.texture = _outputRT;
    }

    void EndGlitch(bool immediate)
    {
        _phase = Phase.Idle;

        // 恢复原来的 waterRenderer，隐藏独立 RawImage
        if (waterRenderer != null) waterRenderer.enabled = true;
        if (_glitchImage  != null) _glitchImage.enabled  = false;

        if (glitchMaterial != null) glitchMaterial.SetFloat("_Intensity", 0f);
        if (!immediate) ScheduleNext();
    }

    void TransitionTo(Phase phase) { _phase = phase; _phaseTime = 0f; }
    void ScheduleNext() => _nextTrigger = _time + Random.Range(intervalMin, intervalMax);

    // ── 独立 RawImage ─────────────────────────────────

    void CreateGlitchImage()
    {
        // 在 waterRenderer 同级创建一个 RawImage，覆盖同样区域
        var go = new GameObject("GlitchOverlay", typeof(RectTransform), typeof(RawImage));
        go.transform.SetParent(waterRenderer.transform.parent, false);

        var rt = go.GetComponent<RectTransform>();
        var wr = waterRenderer.GetComponent<RectTransform>();
        rt.anchorMin        = wr.anchorMin;
        rt.anchorMax        = wr.anchorMax;
        rt.offsetMin        = wr.offsetMin;
        rt.offsetMax        = wr.offsetMax;
        rt.anchoredPosition = wr.anchoredPosition;
        rt.sizeDelta        = wr.sizeDelta;

        _glitchImage              = go.GetComponent<RawImage>();
        _glitchImage.raycastTarget = false;
        _glitchImage.uvRect        = new Rect(0f, 1f, 1f, -1f); // 保持倒影翻转
        _glitchImage.enabled       = false; // 默认隐藏
    }

    // ── RT 管理 ───────────────────────────────────────

    void EnsureRTs()
    {
        var src  = reflCamera.RT;
        _voidRT  = EnsureRT(_voidRT,  src);
        _outputRT= EnsureRT(_outputRT, src);
    }

    RenderTexture EnsureRT(RenderTexture rt, RenderTexture src)
    {
        if (rt != null && rt.width == src.width && rt.height == src.height) return rt;
        if (rt != null) { rt.Release(); Destroy(rt); }
        var n = new RenderTexture(src.width, src.height, 0, src.format)
            { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        n.Create();
        return n;
    }

    // ── 文字替换 ──────────────────────────────────────

    void SaveOriginalText()
    {
        if (titleMain    != null) { _origTitle = titleMain.text;    _origTitleColor  = titleMain.color; }
        if (titleSub     != null) { _origSub   = titleSub.text;     _origSubColor    = titleSub.color; }
        if (factionLabel != null) { _origLabel = factionLabel.text; _origLabelColor  = factionLabel.color; }
    }

    void ApplyVoidText()
    {
        if (titleMain    != null) { titleMain.text    = voidTitleText; titleMain.color    = voidColor; }
        if (titleSub     != null) { titleSub.text     = voidSubText;   titleSub.color     = new Color(voidColor.r, voidColor.g, voidColor.b, 0.4f); }
        if (factionLabel != null) { factionLabel.text = voidLabelText; factionLabel.color = new Color(voidColor.r, voidColor.g, voidColor.b, 0.5f); }
    }

    void RestoreOriginalText()
    {
        if (titleMain    != null) { titleMain.text    = _origTitle; titleMain.color    = _origTitleColor; }
        if (titleSub     != null) { titleSub.text     = _origSub;   titleSub.color     = _origSubColor; }
        if (factionLabel != null) { factionLabel.text = _origLabel; factionLabel.color = _origLabelColor; }
    }

    void OnFactionChanged(TitleScreenManager.Faction f)
    {
        if (f == TitleScreenManager.Faction.Void)
        {
            _active = false;
            if (_phase != Phase.Idle) EndGlitch(true);
            _nextTrigger = float.MaxValue;
        }
        else
        {
            _active = true;
            ScheduleNext();
        }
    }

    void OnDestroy()
    {
        foreach (var rt in new[] { _voidRT, _outputRT })
            if (rt != null) { rt.Release(); Destroy(rt); }
        if (_glitchImage != null) Destroy(_glitchImage.gameObject);
    }
}