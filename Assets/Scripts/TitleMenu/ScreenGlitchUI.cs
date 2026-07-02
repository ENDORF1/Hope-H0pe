using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 挂在 Screen Space Overlay Canvas 上。
/// 管理全屏故障效果的所有 UI 动作。
/// 通过 ScreenGlitchFX 驱动块撕裂和 RGB 分离。
///
/// 使用方式：
///   手动触发单个动作：TriggerAction("blockTear")
///   触发全部：       TriggerAll()
///   随机自动触发：   autoTrigger = true（Inspector 里勾选）
/// </summary>
public class ScreenGlitchUI : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private ScreenGlitchFX glitchFX;
    [Tooltip("拖入 World Space Canvas 的 RectTransform")]
    [SerializeField] private RectTransform canvasRect;
    [Tooltip("主标题 TMP（希望/熄忘），供 voidGhost 动画控制显隐")]
    [SerializeField] private TextMeshProUGUI titleMain;
    [Tooltip("背景脚本，voidGhost 时驱动 Shader 压暗整个背景")]
    [SerializeField] private TitleBackground titleBackground;
    [Tooltip("幽灵文字出现的锚点 RectTransform，拖入主标题所在的 RectTransform")]
    [SerializeField] private RectTransform ghostAnchor;
    [Tooltip("阵营标签（上方），voidGhost 时一并淡出")]
    [SerializeField] private TextMeshProUGUI factionLabel;
    [Tooltip("分隔符 Image，voidGhost 时一并淡出")]
    [SerializeField] private UnityEngine.UI.Image separatorImage;
    [Tooltip("TitleScreenManager 引用，用于触发菜单按钮 GlitchText")]
    [SerializeField] private TitleScreenManager titleScreenManager;
    [Tooltip("切换阵营按钮，voidGhost 时一并淡出+触发文字变色")]
    [SerializeField] private UnityEngine.UI.Button switchButton;
    [Tooltip("副标题，voidGhost 时一并淡出")]
    [SerializeField] private TextMeshProUGUI titleSub;
    [Tooltip("阵营标签（下方），voidGhost 时一并淡出")]
    [SerializeField] private TextMeshProUGUI factionLabel2;

    [Header("字体")]
    [SerializeField] private Font errorFont;

    [Header("熄忘颜色")]
    [SerializeField] private Color voidColor  = new Color(0.86f, 0.20f, 0.20f, 1f);
    [SerializeField] private Color hopeColor  = new Color(0.24f, 0.91f, 0.78f, 1f);

    [Header("自动触发")]
    public bool  autoTrigger    = true;
    public float autoRateMin    = 8f;
    public float autoRateMax    = 20f;

    // ── 各动作参数 ─────────────────────────────────────

    [System.Serializable]
    public class ActionParams
    {
        public bool  enabled   = true;
        public float duration  = 0.1f;
        [Range(0f,1f)]
        public float weight    = 1f; // 自动触发时的权重
    }

    [Header("── 块撕裂 (blockTear)")]
    public ActionParams blockTear   = new ActionParams { duration = 0.12f };
    public float blockSize    = 0.04f;
    public float tearMaxShift = 0.14f;

    [Header("── RGB 分离 (rgbSplit)")]
    public ActionParams rgbSplit    = new ActionParams { duration = 0.18f };
    public float rgbHOffset = 0.016f;
    public float rgbVOffset = 0.022f;

    [Header("── 错误弹窗 (errorBars)")]
    public ActionParams errorBars   = new ActionParams { duration = 1.2f  };

    [Header("错误弹窗 - 参数")]
    public float popupWidthMin     = 0.20f;
    public float popupWidthMax     = 0.38f;
    public float popupAspect       = 0.58f;
    public int   popupCopyMin      = 2;
    public int   popupCopyMax      = 4;
    public float popupCopyOffset   = 0.08f;
    public float popupCopyAlphaMin = 0.12f;
    public float popupCopyAlphaMax = 0.28f;
    public float popupJitterAmp    = 4f;
    public float popupJitterFreq   = 20f;
    public float popupSpawnInterval= 0.12f;
    public float bodyOffsetY       = 0f;

    [Header("错误弹窗 - 内容")]
    public List<ErrorMessage> errorMessages = new List<ErrorMessage>
    {
        new ErrorMessage { title="FATAL ERROR",      body="memory corruption detected\nat void_sector_0x4E58",    button="IGNORE"     },
        new ErrorMessage { title="熄忘入侵",           body="希望系统正在崩溃\n无法阻止",                              button="你无法阻止"  },
        new ErrorMessage { title="CRITICAL FAILURE",  body="reflection.integrity==NULL\ncontainment lost",        button="DISMISS"    },
        new ErrorMessage { title="你还在吗",           body="hope.anchor returned void\n你的选择已被覆写",             button="继续"       },
        new ErrorMessage { title="这不是希望",          body="true_self exposed\n你一直知道的",                       button="我知道"     },
        new ErrorMessage { title="VOID BREACH",       body="熄忘侵入 — 阻断失败\n阻断失败 — 阻断失败",                 button="确认"       },
        new ErrorMessage { title="NULL_REF",          body="hope.anchor = void\ndepth exceeded",                  button="ACCEPT"     },
        new ErrorMessage { title="你看见了吗",          body="水面下面是什么\n你真的想知道吗",                            button="想"         },
    };

    [Header("── 白色噪点 (noise)")]
    public ActionParams noise       = new ActionParams { duration = 0.10f };
    [Range(0f,1f)] public float noiseDensity = 0.85f;

    [Header("── 闪红噪点 (flashRed)")]
    public ActionParams flashRed    = new ActionParams { duration = 0.06f };

    [Header("── 闪蓝噪点 (flashBlue)")]
    public ActionParams flashBlue   = new ActionParams { duration = 0.06f, weight = 1f };

    [Header("── 熄忘裂缝 (voidCrack)")]
    public ActionParams voidCrack = new ActionParams { duration = 3.0f, weight = 0.7f };
    [Tooltip("VoidCrack 材质，Shader 选 XiWang_XiWang/VoidCrack")]
    [SerializeField] private Material voidCrackMaterial;
    public int   crackWaveCount = 12;
    public float crackRInner    = 0.12f;
    public float crackAmpBase   = 0.03f;
    public float crackAmpStep   = 0.005f;
    public float crackJagAmp    = 0.06f;
    public ActionParams edgeBleed      = new ActionParams { duration = 1.0f };
    [Tooltip("EdgeBleed 材质，Shader 选 XiWang_XiWang/EdgeBleed")]
    [SerializeField] private Material edgeBleedMaterial;
    public float edgeDashCount   = 60f;
    public float edgeLenMin      = 20f;
    public float edgeLenMax      = 120f;
    public float edgeDashWMin    = 0.002f;
    public float edgeDashWMax    = 0.012f;
    public float edgeBeatInterval = 6f;
    public float edgeBeatDuration = 0.8f;
    public float edgeGlowStrength = 0.15f;

    [Header("── 画面失步 (screenRoll)")]
    public ActionParams screenRoll  = new ActionParams { duration = 0.8f  };
    public float rollMinJump   = 0.08f;
    public float rollMaxJump   = 0.3f;
    public int   rollJumpCount = 3;

    [Header("── 熄忘幽灵 (voidGhost)")]
    public ActionParams voidGhost   = new ActionParams { duration = 0.7f  };
    public string ghostTitleText = "熄忘";
    public string ghostSubText   = "CARVE SILENCE AND THE UNIVERSE OBEYS";
    [Range(0f,1f)] public float ghostAlpha = 0.88f;
    public float ghostJitter = 0.012f;

    // ══════════════════════════════════════════════════════
    // 希望入侵特效（熄忘阵营下触发）
    // ══════════════════════════════════════════════════════

    [Header("═══ 希望入侵 - 通用 ═══")]
    [Tooltip("希望阵营颜色（用于所有 hope 特效）")]
    [SerializeField] private Color hopeGlitchColor = new Color(0.24f, 0.91f, 0.78f, 1f);

    // ── 希望入侵特效预留槽 ────────────────────────────────
    // hopeRipple   : 波纹扩散（GL圆环，HopeGLRenderer）
    // hopeDrops    : 水滴落下（保留，Keypad1）
    // hopeSin    : 波浪线入侵（Shader）← 已实现
    // hopeParticles: 小点爆发（CPU贴图，可扩展）
    // hopeNegative : 负片闪现（Shader，可扩展）
    // hopeGhost    : 希望幽灵（TMP叠层，可扩展）
    // hopeBubble   : 气泡（折射Shader+CommandBuffer，可扩展）
    // hopePopup    : 希望弹窗（动态UI）← 已实现


    [Header("── LED 点阵 (hopeLED)")]
    public ActionParams hopeLED = new ActionParams { duration = 4.0f, weight = 0.5f };
    public float ledSize        = 6f;
    public float ledGapRatio    = 0.25f;
    public float ledBrightness  = 1.2f;
    public float ledColorShift  = 0.35f;
    public float ledFadeInDur   = 0.08f;  // 闪白+切入时长
    public float ledFadeOutDur  = 0.08f;  // 闪白+还原时长
    public float ledHoldDur     = 2.5f;   // 保持 LED 效果时长

    [Header("── 希望幽灵 (hopeGhost)")]
    public ActionParams hopeGhost     = new ActionParams { duration = 0.7f };
    public string hopeGhostTitleText  = "希望";
    public string hopeGhostSubText    = "LET THE LIGHT BREAK THROUGH";
    public float  hopeGhostAlpha      = 0.85f;
    public float  hopeGhostFloatAmp   = 18f;   // 上浮幅度（像素）
    public float  hopeGhostBrightness = 0.15f; // 背景提亮幅度

    [Header("── 希望扫掠 (hopeSweep)")]
    public ActionParams hopeSweep = new ActionParams { duration = 3.0f, weight = 0.6f };
    public float sweepDotCountMin  = 20f;
    public float sweepDotCountMax  = 40f;
    public float sweepDotSizeMin   = 2f;
    public float sweepDotSizeMax   = 5f;
    public float sweepFrontDur     = 1.5f;  // 前锋横扫持续秒数
    public float sweepDotLifeMin   = 1.0f;  // 光点存活最短秒数
    public float sweepDotLifeMax   = 2.0f;  // 光点存活最长秒数
    public float sweepJitterAmp    = 3f;    // 光点颤动幅度（像素）
    public float sweepRippleMaxR   = 60f;   // 光点波纹最大半径（像素）

    [Header("── 希望弹窗 (hopePopup)")]
    public ActionParams hopePopup = new ActionParams { duration = 4.0f, weight = 0.6f };
    [Tooltip("每次触发同时出现的弹窗组数（最小值）")]
    public int   hopePopupCountMin    = 2;
    [Tooltip("每次触发同时出现的弹窗组数（最大值）")]
    public int   hopePopupCountMax    = 3;
    [Tooltip("弹窗宽度占 canvas 宽度的最小比例")]
    public float hopePopupWidthMin    = 0.22f;
    [Tooltip("弹窗宽度占 canvas 宽度的最大比例")]
    public float hopePopupWidthMax    = 0.36f;
    [Tooltip("弹窗高宽比（height = width * aspect）")]
    public float hopePopupAspect      = 0.58f;
    [Tooltip("副本相对主体的偏移比例（0~1，相对弹窗尺寸）")]
    public float hopePopupCopyOffset  = 0.08f;
    [Tooltip("副本透明度最小值")]
    public float hopePopupCopyAlphaMin= 0.12f;
    [Tooltip("副本透明度最大值")]
    public float hopePopupCopyAlphaMax= 0.28f;
    [Tooltip("文字抖动幅度（像素）")]
    public float hopePopupJitterAmp   = 3f;
    [Tooltip("文字抖动频率（每秒切换次数）")]
    public float hopePopupJitterFreq  = 18f;
    [Tooltip("每组弹窗之间的生成间隔（秒）")]
    public float hopePopupSpawnInterval = 0.10f;
    [Tooltip("弹窗上浮的最大像素幅度")]
    public float hopePopupFloatAmp    = 8f;
    [Tooltip("正文区域顶部偏移（占弹窗高度的比例，值越大正文越靠下）")]
    public float hopePopupBodyOffsetY = 0.06f;

    [Header("希望弹窗 - 内容")]
    public List<HopeMessage> hopeMessages = new List<HopeMessage>
    {
        new HopeMessage { title="hope.signal",   body="波纹共鸣已建立\n正在同步中",           button="感受"   },
        new HopeMessage { title="light.detected", body="希望的频率\n正在渗入",                button="接受"   },
        new HopeMessage { title="wave.sync",      body="记忆碎片浮现\n你还记得吗",             button="记得"   },
        new HopeMessage { title="希望入侵",        body="water.anchor = hope\n共鸣深度超限",   button="继续"   },
        new HopeMessage { title="ripple.echo",    body="你曾许下的愿望\n仍在水面震荡",          button="我知道" },
        new HopeMessage { title="光在这里",        body="hope.core detected\n熄灭尚未完成",    button="是的"   },
    };

    [System.Serializable]
    public class HopeMessage
    {
        public string title;
        public string body;
        public string button;
    }
    public ActionParams hopeSin = new ActionParams { duration = 3.0f, weight = 0.7f };
    [Tooltip("HopeSin 材质，Shader 选 XiWang_XiWang/HopeSin")]
    [SerializeField] private Material hopeSinMaterial;
    [Tooltip("波浪线数量")]
    public int sinWaveCount = 8;
    [Tooltip("波浪线最深入屏幕中心的半径（UV空间，0=中心，0.5=边缘）")]
    [Range(0f, 0.5f)] public float sinRInner = 0.15f;
    [Tooltip("最内圈振幅基础值")]
    public float sinAmpBase = 0.018f;
    [Tooltip("每圈振幅递增量")]
    public float sinAmpStep = 0.007f;

 

    // ── 内部状态 ───────────────────────────────────────
    private class ActionState
    {
        public bool  active;
        public float p;       // 0~1 进度
        public float dur;
        public object data;   // 动作专用数据
    }

    private Dictionary<string, ActionState> _states;
    private float _time;
    private float _nextAuto;
    private string _lastPick1 = "";
    private string _lastPick2 = "";
    private Coroutine _firstImpactCoroutine;
    private RectTransform _canvasRect;

    // UI 元素池
    private GameObject _overlayRoot;
    private RawImage _voidCrackImage;
    private float    _voidCrackTime;
    private float    _voidCrackProgress;
    private Coroutine _voidCrackCoroutine;
    private RawImage   _flashRedImage;
    private RawImage   _flashBlueImage;
    private Texture2D  _redNoiseTex;
    private Texture2D  _blueNoiseTex;
    private RawImage         _edgeBleedImage;  // 边缘渗出全屏 RawImage
    private float            _edgeBleedTime;   // 独立计时，触发时归零确保从心跳起点开始
    private RawImage   _noiseImage;
    private TextMeshProUGUI _ghostTitle, _ghostSub;
    // voidGhost 渐黑由 TitleBackground.SetDarkness() 驱动，无需 UI 遮罩
    private Coroutine  _voidGhostCoroutine;
    private Coroutine  _popupExitCoroutine;
    private List<GameObject> _errorBarObjects = new List<GameObject>(); // 保留兼容
    private List<PopupData>  _popupObjects    = new List<PopupData>();

    [System.Serializable]
    public class ErrorMessage
    {
        public string title;
        public string body;
        public string button;
    }

    private class PopupSpawnEntry
    {
        public float spawnTime;
        public int   msgIndex;
        public bool  isMain;
        public float x, y, w, h;
        public bool  red;
        public float targetAlpha;
        public float copyOffX, copyOffY; // 副本相对主体偏移（已算好）
    }

    private class PopupData
    {
        public GameObject     root;
        public CanvasGroup    cg;
        public List<RectTransform> jitterRTs  = new List<RectTransform>();
        public List<Vector2>       jitterBase = new List<Vector2>();
        public float born;
    }
    private Texture2D _noiseTex;

    // 失步状态
    private float     _screenRollY;
    private Coroutine _screenRollCoroutine;

    // ── Hope 特效内部状态 ──────────────────────────────
    // 波纹/水滴/破裂渲染全部由 HopeGLRenderer 负责
    private HopeGLRenderer       _glRenderer;
    private RawImage _hopeSinImage;
    private float    _hopeSinTime;
    private float    _hopeSinProgress;


    private Coroutine _hopeDropsCoroutine;
    private Coroutine _hopeSweepCoroutine;
    private Coroutine _hopeLEDCoroutine;
    private Coroutine _hopeGhostCoroutine;
    private TextMeshProUGUI _hopeGhostTitle, _hopeGhostSub;
    private UnityEngine.UI.Image _flashWhiteImage;
    private UnityEngine.UI.Image[] _hopeCornerGlows; // 四角光晕
    private Coroutine _hopePopupExitCoroutine;
    private List<HopePopupData> _hopePopupObjects = new List<HopePopupData>();

    private class HopePopupData
    {
        public GameObject          root;
        public CanvasGroup         cg;
        public RectTransform       rootRT;
        public float               born;
        public float               baseY;   // 初始 anchoredPosition.y，用于上浮
        public List<RectTransform> jitterRTs  = new List<RectTransform>();
        public List<Vector2>       jitterBase = new List<Vector2>();
    }

    private class HopePopupSpawnEntry
    {
        public float spawnTime;
        public int   msgIndex;
        public bool  isMain;
        public float x, y, w, h;
        public float targetAlpha;
    }


    void Awake()
    {
        _canvasRect = canvasRect != null ? canvasRect : GetComponent<RectTransform>();
        _states = new Dictionary<string, ActionState>();
        foreach (var name in new[]{
            "blockTear","rgbSplit","errorBars","noise","voidGhost","voidCrack","flashRed","flashBlue","edgeBleed","screenRoll",
            "hopeDrops","hopeSin","hopePopup","hopeSweep","hopeLED","hopeGhost",
        })
            _states[name] = new ActionState();

        _glRenderer = gameObject.GetComponent<HopeGLRenderer>();
        if (_glRenderer == null) _glRenderer = gameObject.AddComponent<HopeGLRenderer>();


        BuildUIElements();
        BuildHopeSinUI();
        ScheduleNext();
        _firstImpactCoroutine = StartCoroutine(FirstImpactRoutine());
    }

    void Start()
    {
    }

    void OnEnable()  => TitleScreenManager.OnFactionChanged += OnFactionChanged;
    void OnDisable() => TitleScreenManager.OnFactionChanged -= OnFactionChanged;

    void OnFactionChanged(TitleScreenManager.Faction f)
    {
        if (_glRenderer != null) _glRenderer.HopeColor = hopeGlitchColor;

        // 切换阵营时重置首次冲击计时
        if (_firstImpactCoroutine != null) StopCoroutine(_firstImpactCoroutine);
        _firstImpactCoroutine = StartCoroutine(FirstImpactRoutine());

        if (f == TitleScreenManager.Faction.Void)
        {
            // 停止所有 hope 特效，启动 void 自动触发
            StopAllHopeEffects();
            HideHopeAll();
            ScheduleNext();
        }
        else
        {
            // 停止所有 void 特效，启动 hope 自动触发
            foreach (var name in new[]{"blockTear","rgbSplit","errorBars","noise","voidGhost","flashRed","edgeBleed","screenRoll"})
            { var s = _states[name]; s.active = false; s.p = 0f; }
            HideVoidAll();
            ScheduleNext();
        }
    }

    void Update()
    {
        _time += Time.deltaTime;

        // 自动触发
        if (autoTrigger && _time >= _nextAuto)
        {
            TriggerRandom();
            ScheduleNext();
        }

        _glRenderer?.Tick(Time.deltaTime);
        UpdateStates(Time.deltaTime);
        ApplyStates();
        UpdateHopeSin();
    }

    // ── 触发接口 ──────────────────────────────────────

    public void TriggerAction(string name)
    {
        Debug.Log($"[ScreenGlitchUI] TriggerAction: {name}");
        if (!_states.ContainsKey(name))
        {
            Debug.LogWarning($"[ScreenGlitchUI] 找不到 action: {name}");
            return;
        }

        // Ghost 运行期间屏蔽所有其他特效（Ghost 内部自己触发的除外）
        bool ghostRunning = _voidGhostCoroutine != null || _hopeGhostCoroutine != null;
        if (ghostRunning && name != "blockTear" && name != "rgbSplit" && name != "hopeSin")
            return;

        // voidCrack 运行期间屏蔽所有其他特效（内部触发的 blockTear 除外）
        bool crackRunning = _voidCrackCoroutine != null;
        if (crackRunning && name != "voidCrack" && name != "blockTear")
            return;
        // edgeBleed 触发时重置独立计时
        if (name == "edgeBleed") _edgeBleedTime = 0f;

        // screenRoll 走独立协程
        if (name == "screenRoll")
        {
            if (_screenRollCoroutine != null) StopCoroutine(_screenRollCoroutine);
            _screenRollCoroutine = StartCoroutine(ScreenRollRoutine());
            return;
        }

        // voidGhost 走独立协程序列
        if (name == "voidGhost")
        {
            if (_voidGhostCoroutine != null) StopCoroutine(_voidGhostCoroutine);
            _voidGhostCoroutine = StartCoroutine(VoidGhostRoutine());
            return;
        }

        // Hope 特效路由
        if (name == "voidCrack")  { if (_voidCrackCoroutine != null) StopCoroutine(_voidCrackCoroutine); _voidCrackCoroutine = StartCoroutine(VoidCrackRoutine()); return; }
        if (name == "hopeSin")    { _hopeSinTime = 0f; _hopeSinProgress = 0f; var hs = _states["hopeSin"]; hs.active=true; hs.p=0f; hs.dur=hopeSin.duration; return; }
        if (name == "hopeDrops")  { if (_hopeDropsCoroutine  != null) StopCoroutine(_hopeDropsCoroutine);  _hopeDropsCoroutine  = StartCoroutine(HopeDropsRoutine());  return; }
        if (name == "hopeGhost")  { if (_hopeGhostCoroutine  != null) StopCoroutine(_hopeGhostCoroutine);  _hopeGhostCoroutine  = StartCoroutine(HopeGhostRoutine());  return; }
        if (name == "hopePopup")  { InitHopePopupData(_states["hopePopup"]); return; }
        if (name == "hopeSweep")  { if (_hopeSweepCoroutine != null) StopCoroutine(_hopeSweepCoroutine); _hopeSweepCoroutine = StartCoroutine(HopeSweepRoutine()); return; }
        if (name == "hopeLED")    { if (_hopeLEDCoroutine   != null) StopCoroutine(_hopeLEDCoroutine);   _hopeLEDCoroutine   = StartCoroutine(HopeLEDRoutine());   return; }

        var s = _states[name];
        s.active = true;
        s.p      = 0f;
        s.dur    = GetParams(name).duration;
        InitActionData(name, s);
        Debug.Log($"[ScreenGlitchUI] {name} 已激活，dur={s.dur}, data={s.data?.GetType()}");
    }

    public void TriggerAll()
    {
        foreach (var name in _states.Keys) TriggerAction(name);
    }

    void TriggerRandom()
    {
        bool isVoid = (TitleScreenManager.Instance == null) ||
                      (TitleScreenManager.Instance.CurrentFaction == TitleScreenManager.Faction.Void);

        // ── 三个池子（侵入逻辑：触发对方阵营的特效）──────
        string[] voidPool   = { "hopeDrops","hopeSin","hopePopup","hopeSweep","flashBlue","hopeGhost" };
        string[] hopePool   = { "errorBars","flashRed","edgeBleed","voidGhost","voidCrack" };
        string[] sharedPool = { "blockTear","rgbSplit","noise","screenRoll","hopeLED" };

        // 当前阵营专属池 + 公用池合并
        var factionPool = isVoid ? voidPool : hopePool;
        var combined    = new string[factionPool.Length + sharedPool.Length];
        factionPool.CopyTo(combined, 0);
        sharedPool.CopyTo(combined, factionPool.Length);

        int count = Random.Range(1, 3);
        for (int i = 0; i < count; i++)
        {
            // 防止同一特效连续触发三次，最多重试 5 次
            string pick = "";
            for (int retry = 0; retry < 5; retry++)
            {
                pick = combined[Random.Range(0, combined.Length)];
                if (!(pick == _lastPick1 && pick == _lastPick2)) break;
            }
            var p = GetParams(pick);
            if (p != null && p.enabled && Random.value < p.weight)
            {
                TriggerAction(pick);
                _lastPick2 = _lastPick1;
                _lastPick1 = pick;
            }
        }
    }

    void ScheduleNext()
    {
        _nextAuto = _time + Random.Range(autoRateMin, autoRateMax);
    }

    IEnumerator FirstImpactRoutine()
    {
        yield return new WaitForSeconds(3f);

        bool isVoid = (TitleScreenManager.Instance == null) ||
                      (TitleScreenManager.Instance.CurrentFaction == TitleScreenManager.Faction.Void);

        string[] impactPool = isVoid
            ? new[] { "hopeSin", "hopeGhost", "hopeLED", "hopePopup" }
            : new[] { "voidCrack", "voidGhost", "edgeBleed", "errorBars" };

        string pick = impactPool[Random.Range(0, impactPool.Length)];
        TriggerAction(pick);

        // 首次触发后重置计时器，避免和自动触发太近
        ScheduleNext();
    }

    // ── 动作数据初始化 ────────────────────────────────

    void InitActionData(string name, ActionState s)
    {
        if (name == "errorBars")
        {
            ClearPopups();
            float cW = _canvasRect.rect.width;
            float cH = _canvasRect.rect.height;

            int groupCount = Random.Range(3, 7);
            var entries    = new List<PopupSpawnEntry>();
            float cumTime  = 0f;

            var msgPool = new List<int>();
            for (int i = 0; i < errorMessages.Count; i++) msgPool.Add(i);
            for (int i = msgPool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                int tmp = msgPool[i]; msgPool[i] = msgPool[j]; msgPool[j] = tmp;
            }

            for (int gi = 0; gi < groupCount; gi++)
            {
                cumTime += Random.Range(0.06f, 0.20f);

                float w   = Random.Range(popupWidthMin, popupWidthMax) * cW;
                float h   = w * popupAspect;
                float x   = Random.Range(0f, cW - w) - cW * 0.5f;
                float y   = Random.Range(0f, cH - h) - cH * 0.5f;
                bool  red = Random.value > 0.35f;
                int  mIdx = msgPool[gi % msgPool.Count];

                int copies = Random.Range(popupCopyMin, popupCopyMax + 1);
                for (int ci = 0; ci < copies; ci++)
                {
                    float ox = Random.Range(-popupCopyOffset, popupCopyOffset) * w;
                    float oy = Random.Range(-popupCopyOffset, popupCopyOffset) * h;
                    float ca = Random.Range(popupCopyAlphaMin, popupCopyAlphaMax);
                    entries.Add(new PopupSpawnEntry
                    {
                        spawnTime   = cumTime,
                        msgIndex    = mIdx,
                        isMain      = false,
                        x = x + ox, y = y + oy, w = w, h = h,
                        red         = red,
                        targetAlpha = ca,
                    });
                    cumTime += popupSpawnInterval;
                }

                entries.Add(new PopupSpawnEntry
                {
                    spawnTime   = cumTime,
                    msgIndex    = mIdx,
                    isMain      = true,
                    x = x, y = y, w = w, h = h,
                    red         = red,
                    targetAlpha = 1f,
                });
                cumTime += popupSpawnInterval;
            }

            s.dur  = cumTime + GetParams("errorBars").duration;
            s.data = entries;
        }
    }

    // ── 状态更新 ──────────────────────────────────────

    void UpdateStates(float dt)
    {
        foreach (var kv in _states)
        {
            var s = kv.Value;
            if (!s.active) continue;
            s.p += dt / Mathf.Max(0.001f, s.dur);

            if (s.p >= 1f)
            {
                s.active = false;
                s.p      = 0f;
                if (kv.Key == "errorBars")
                {
                    if (_popupExitCoroutine != null) StopCoroutine(_popupExitCoroutine);
                    _popupExitCoroutine = StartCoroutine(PopupExitRoutine());
                }
                if (kv.Key == "hopePopup")
                {
                    if (_hopePopupExitCoroutine != null) StopCoroutine(_hopePopupExitCoroutine);
                    _hopePopupExitCoroutine = StartCoroutine(HopePopupExitRoutine());
                }
            }
        }
    }

    // ── 应用状态到 UI ─────────────────────────────────

    void ApplyStates()
    {
        // 后处理：块撕裂
        if (glitchFX != null)
        {
            glitchFX.TearIntensity = _states["blockTear"].active ? 1f : 0f;
            glitchFX.RGBIntensity  = _states["rgbSplit"].active  ? 1f : 0f;
            glitchFX.blockSize     = blockSize;
            glitchFX.tearMaxShift  = tearMaxShift;
            glitchFX.rgbHOffset    = rgbHOffset;
            glitchFX.rgbVOffset    = rgbVOffset;
        }

        // 噪点
        if (_states["noise"].active)
        {
            UpdateNoiseTex();
            _noiseImage.enabled = true;
        }
        else _noiseImage.enabled = false;

        // 错误弹窗：按 spawnTime 逐张生成
        var ebState = _states["errorBars"];
        if (ebState.active && ebState.data is List<PopupSpawnEntry> spawnEntries)
        {
            float elapsed = ebState.p * ebState.dur;
            for (int i = _popupObjects.Count; i < spawnEntries.Count; i++)
            {
                var e = spawnEntries[i];
                if (elapsed < e.spawnTime) break;
                var msg   = errorMessages[Mathf.Clamp(e.msgIndex, 0, errorMessages.Count - 1)];
                Color col = e.red ? voidColor : hopeColor;
                float titleH = e.h * 0.22f;
                float fs     = e.h * 0.095f;
                SpawnPopupInstance(msg, e.red, e.x, e.y, e.w, e.h, col, titleH, fs, e.targetAlpha, e.isMain);
            }
            UpdatePopupJitter();
            foreach (var pd in _popupObjects)
            {
                if (pd.cg == null) continue;
                float age = _time - pd.born;
                pd.cg.alpha = Mathf.Clamp01(age / 0.08f);
            }
        }

        // 熄忘裂缝
        if (_states["voidCrack"].active && voidCrackMaterial != null)
        {
            float cW = _canvasRect != null ? _canvasRect.rect.width  : Screen.width;
            float cH = _canvasRect != null ? _canvasRect.rect.height : Screen.height;
            voidCrackMaterial.SetColor("_Color",      voidColor);
            voidCrackMaterial.SetFloat("_Time2",      _voidCrackTime);
            voidCrackMaterial.SetFloat("_Progress",   _voidCrackProgress);
            voidCrackMaterial.SetFloat("_Aspect",     cW / cH);
            voidCrackMaterial.SetFloat("_ScreenH",    cH);
            voidCrackMaterial.SetFloat("_CrackCount", crackWaveCount);
            voidCrackMaterial.SetFloat("_RInner",     crackRInner);
            voidCrackMaterial.SetFloat("_AmpBase",    crackAmpBase);
            voidCrackMaterial.SetFloat("_AmpStep",    crackAmpStep);
            voidCrackMaterial.SetFloat("_JagAmp",     crackJagAmp);
            voidCrackMaterial.SetFloat("_Alpha",      1f);
            _voidCrackImage.enabled = true;
            _voidCrackImage.SetMaterialDirty();
        }
        else if (_voidCrackImage != null) _voidCrackImage.enabled = false;

        // 熄忘幽灵由 VoidGhostRoutine 协程独立驱动，此处不处理

        // 闪红：红色随机噪点（和白色噪点逻辑相同，用红色）
        if (_states["flashRed"].active)
        {
            UpdateRedNoiseTex();
            _flashRedImage.enabled = true;
        }
        else _flashRedImage.enabled = false;

        // 闪蓝：青色随机噪点（希望阵营）
        if (_states["flashBlue"].active)
        {
            UpdateBlueNoiseTex();
            _flashBlueImage.enabled = true;
        }
        else if (_flashBlueImage != null) _flashBlueImage.enabled = false;

        // 边缘渗出
        if (_states["edgeBleed"].active)
        {
            _edgeBleedTime += Time.deltaTime;
            if (edgeBleedMaterial != null)
            {
                edgeBleedMaterial.SetFloat("_Time2",         _edgeBleedTime);
                float ebAlpha = Mathf.Sin(_states["edgeBleed"].p * Mathf.PI);
                edgeBleedMaterial.SetFloat("_Intensity",     ebAlpha);
                edgeBleedMaterial.SetFloat("_Aspect",        Screen.width / (float)Screen.height);
                edgeBleedMaterial.SetColor("_Color",         voidColor);
                edgeBleedMaterial.SetFloat("_DashCount",     edgeDashCount);
                edgeBleedMaterial.SetFloat("_DashLenMin",    edgeLenMin);
                edgeBleedMaterial.SetFloat("_DashLenMax",    edgeLenMax);
                edgeBleedMaterial.SetFloat("_DashWMin",      edgeDashWMin);
                edgeBleedMaterial.SetFloat("_DashWMax",      edgeDashWMax);
                edgeBleedMaterial.SetFloat("_BeatInterval",  edgeBeatInterval);
                edgeBleedMaterial.SetFloat("_BeatDuration",  edgeBeatDuration);
                edgeBleedMaterial.SetFloat("_GlowStrength",  edgeGlowStrength);
            }
            _edgeBleedImage.enabled = true;
            if (_edgeBleedImage != null) _edgeBleedImage.SetMaterialDirty();
        }
        else
        {
            if (_edgeBleedImage != null) _edgeBleedImage.enabled = false;
        }

        // 失步由 ScreenRollRoutine 协程独立驱动，此处不处理

        // ── Hope 特效每帧更新 ──────────────────────────
        UpdateHopePopups();
    }

    // ── 弹窗撕裂退出协程 ────────────────────────────

    IEnumerator PopupExitRoutine()
    {
        // 触发撕裂和 RGB 分离
        var tearState = _states["blockTear"];
        tearState.active = true; tearState.p = 0f; tearState.dur = blockTear.duration;
        var rgbState = _states["rgbSplit"];
        rgbState.active = true; rgbState.p = 0f; rgbState.dur = rgbSplit.duration;

        // 等撕裂播完
        yield return new WaitForSeconds(Mathf.Max(blockTear.duration, rgbSplit.duration));

        ClearPopups();
        _popupExitCoroutine = null;
    }

    // ── screenRoll 失步协程 ──────────────────────────

    IEnumerator ScreenRollRoutine()
    {
        float totalDur = rollJumpCount * 0.25f + 0.4f;
        float elapsed  = 0f;
        float rollY    = 0f;

        while (elapsed < totalDur)
        {
            elapsed += Time.deltaTime;

            // 每隔一段时间随机跳位
            float jumpPhase = Mathf.Repeat(elapsed, 0.22f) / 0.22f;
            if (jumpPhase < Time.deltaTime / 0.22f)
            {
                float jump = Random.value > 0.4f ? 1f : -1f;
                jump  *= Random.Range(rollMinJump, rollMaxJump);
                rollY  = Mathf.Repeat(rollY + jump, 1f);
            }

            // 叠加连续慢速漂移
            rollY = Mathf.Repeat(rollY + Time.deltaTime * 0.04f, 1f);

            // 只驱动 Shader UV 偏移，不动 UI
            titleBackground?.SetRollOffset(rollY);

            yield return null;
        }

        titleBackground?.SetRollOffset(0f);
        _screenRollCoroutine = null;
    }

    // ── voidCrack 完整序列协程 ───────────────────────

    IEnumerator VoidCrackRoutine()
    {
        var s = _states["voidCrack"];
        s.active = true; s.p = 0f; s.dur = float.MaxValue;

        _voidCrackTime     = 0f;
        _voidCrackProgress = 0f;

        // shader 总时长分配：段1(40%) + 段2(30%) + 段3(30%)
        // 用 voidCrack.duration 控制总时长
        float totalDur  = voidCrack.duration;
        float phase1End = totalDur * 0.40f;
        float phase2End = totalDur * 0.70f;

        // ── 阶段1+2：shader 驱动裂缝推进+脉冲 ──────────
        float elapsed = 0f;
        while (elapsed < phase2End)
        {
            elapsed            += Time.deltaTime;
            _voidCrackTime     += Time.deltaTime;
            _voidCrackProgress  = Mathf.Clamp01(elapsed / totalDur);
            s.p                 = _voidCrackProgress;
            yield return null;
        }

        // ── 阶段3：shader 消退 ───────────────────────────
        while (elapsed < totalDur)
        {
            elapsed            += Time.deltaTime;
            _voidCrackTime     += Time.deltaTime;
            _voidCrackProgress  = Mathf.Clamp01(elapsed / totalDur);
            s.p                 = _voidCrackProgress;
            yield return null;
        }

        s.active           = false;
        s.p                = 0f;
        _voidCrackProgress = 0f;

        // ── 阶段4：裂缝消失后触发 blockTear ─────────────
        var tearState = _states["blockTear"];
        tearState.active = true; tearState.p = 0f; tearState.dur = 0.7f;
        yield return new WaitForSeconds(0.7f);

        _voidCrackCoroutine = null;
    }

    // ── voidGhost 完整序列协程 ───────────────────────

    IEnumerator VoidGhostRoutine()
    {
        // ── 准备 ──────────────────────────────────────
        // 字体大小与主标题一致
        if (titleMain != null) _ghostTitle.fontSize = titleMain.fontSize;

        // 把幽灵文字定位到 ghostAnchor 指定位置（拖入主标题 RectTransform）
        var ghostRT = _ghostTitle.GetComponent<RectTransform>();
        if (ghostAnchor != null)
        {
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, ghostAnchor.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _overlayRoot.GetComponent<RectTransform>(), screenPos, null, out Vector2 localPos);
            ghostRT.anchoredPosition = localPos;
        }
        else
        {
            ghostRT.anchoredPosition = new Vector2(0f, 60f);
        }
        Vector2 ghostBasePos = ghostRT.anchoredPosition;
        _ghostTitle.text    = ghostTitleText;
        _ghostTitle.color   = new Color(voidColor.r, voidColor.g, voidColor.b, 0f);
        _ghostTitle.enabled = true;

        _ghostSub.enabled   = false;

        // 收集所有标题原始颜色
        Color titleOrigColor    = titleMain     != null ? titleMain.color     : Color.white;
        Color labelOrigColor    = factionLabel  != null ? factionLabel.color  : Color.white;
        Color subOrigColor      = titleSub      != null ? titleSub.color      : Color.white;
        Color label2OrigColor   = factionLabel2 != null ? factionLabel2.color : Color.white;
        Color sepOrigColor      = separatorImage != null ? separatorImage.color : Color.white;
        var switchTmp           = switchButton?.GetComponentInChildren<TextMeshProUGUI>();
        Color switchOrigColor   = switchTmp != null ? switchTmp.color : Color.white;
        Color switchImgOrig     = switchButton?.GetComponent<UnityEngine.UI.Image>() != null
                                ? switchButton.GetComponent<UnityEngine.UI.Image>().color : Color.white;
        // 只有菜单按钮变色+乱码，标题类只乱码不变色
        titleMain?.GetComponent<GlitchText>()?.GlitchHold();
        titleSub?.GetComponent<GlitchText>()?.GlitchHold();
        factionLabel2?.GetComponent<GlitchText>()?.GlitchHold();
        titleScreenManager?.GlitchHoldAllButtons(voidColor);

        // ── 阶段1：渐黑 0~0.8s ───────────────────────
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / 0.8f);
            titleBackground?.SetDarkness(a);
            yield return null;
        }

        // ── 阶段2：标题淡出 0.8~1.4s ─────────────────
        t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / 0.6f);
            // titleMain/titleSub/factionLabel2 由 GlitchHold 控制内容，只控制 alpha
            if (titleMain     != null) titleMain.color     = new Color(titleMain.color.r,     titleMain.color.g,     titleMain.color.b,     titleOrigColor.a  * a);
            if (factionLabel  != null) factionLabel.color  = new Color(labelOrigColor.r,      labelOrigColor.g,      labelOrigColor.b,      labelOrigColor.a  * a);
            if (titleSub      != null) titleSub.color      = new Color(titleSub.color.r,      titleSub.color.g,      titleSub.color.b,      subOrigColor.a    * a);
            if (factionLabel2 != null) factionLabel2.color = new Color(factionLabel2.color.r, factionLabel2.color.g, factionLabel2.color.b, label2OrigColor.a * a);
            if (separatorImage != null) separatorImage.color = new Color(sepOrigColor.r, sepOrigColor.g, sepOrigColor.b, sepOrigColor.a * a);
            if (switchTmp      != null) switchTmp.color      = new Color(switchOrigColor.r, switchOrigColor.g, switchOrigColor.b, switchOrigColor.a * a);
            var swImg = switchButton?.GetComponent<UnityEngine.UI.Image>();
            if (swImg != null) swImg.color = new Color(switchImgOrig.r, switchImgOrig.g, switchImgOrig.b, switchImgOrig.a * a);
            switchButton?.GetComponent<SwitchButtonFX>()?.SetBorderAlpha(a);
            switchButton?.GetComponent<SwitchButtonFX>()?.SetWaveAlpha(a);
            yield return null;
        }
        if (titleMain     != null) titleMain.color     = new Color(titleMain.color.r,     titleMain.color.g,     titleMain.color.b,     0f);
        if (factionLabel  != null) factionLabel.color  = new Color(labelOrigColor.r,      labelOrigColor.g,      labelOrigColor.b,      0f);
        if (titleSub      != null) titleSub.color      = new Color(titleSub.color.r,      titleSub.color.g,      titleSub.color.b,      0f);
        if (factionLabel2 != null) factionLabel2.color = new Color(factionLabel2.color.r, factionLabel2.color.g, factionLabel2.color.b, 0f);
        if (separatorImage != null) separatorImage.color = new Color(sepOrigColor.r, sepOrigColor.g, sepOrigColor.b, 0f);
        if (switchTmp      != null) switchTmp.color      = new Color(switchOrigColor.r, switchOrigColor.g, switchOrigColor.b, 0f);
        var swImg2 = switchButton?.GetComponent<UnityEngine.UI.Image>();
        if (swImg2 != null) swImg2.color = new Color(switchImgOrig.r, switchImgOrig.g, switchImgOrig.b, 0f);
        switchButton?.GetComponent<SwitchButtonFX>()?.SetBorderAlpha(0f);
        switchButton?.GetComponent<SwitchButtonFX>()?.SetWaveAlpha(0f);

        // ── 阶段3：幽灵淡入 1.4~2.2s ─────────────────
        t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / 0.8f) * ghostAlpha;
            _ghostTitle.color = new Color(voidColor.r, voidColor.g, voidColor.b, a);
            // 轻微颤抖
            float jx = (Mathf.Sin(Time.time * 23f) * 0.4f + Mathf.Sin(Time.time * 37f) * 0.3f) * 6f;
            float jy = (Mathf.Sin(Time.time * 19f) * 0.3f) * 3f;
            ghostRT.anchoredPosition = ghostBasePos + new Vector2(jx, jy);
            yield return null;
        }

        // ── 阶段4：持续颤抖 2.2~3.5s ─────────────────
        t = 0f;
        while (t < 1.3f)
        {
            t += Time.deltaTime;
            float jx = (Mathf.Sin(Time.time * 23f) * 0.5f + Mathf.Sin(Time.time * 41f) * 0.4f) * 8f;
            float jy = (Mathf.Sin(Time.time * 17f) * 0.4f) * 4f;
            ghostRT.anchoredPosition = ghostBasePos + new Vector2(jx, jy);
            _ghostTitle.color = new Color(voidColor.r, voidColor.g, voidColor.b, ghostAlpha);
            yield return null;
        }

        // ── 阶段5：撕裂 3.5~4.2s ──────────────────────
        var tearState = _states["blockTear"];
        tearState.active = true; tearState.p = 0f; tearState.dur = 0.7f;
        var rgbState = _states["rgbSplit"];
        rgbState.active = true; rgbState.p = 0f; rgbState.dur = 0.7f;
        yield return new WaitForSeconds(0.7f);

        // ── 阶段6：闪回（跳变）─────────────────────────
        _ghostTitle.enabled = false;
        titleBackground?.SetDarkness(0f);
        // 标题 alpha 还原（颜色未变，只还原 alpha）
        titleMain?.GetComponent<GlitchText>()?.Release();
        titleSub?.GetComponent<GlitchText>()?.Release();
        factionLabel2?.GetComponent<GlitchText>()?.Release();
        if (titleMain      != null) titleMain.color      = titleOrigColor;
        if (factionLabel   != null) factionLabel.color   = labelOrigColor;
        if (titleSub       != null) titleSub.color       = subOrigColor;
        if (factionLabel2  != null) factionLabel2.color  = label2OrigColor;
        if (separatorImage != null) separatorImage.color = sepOrigColor;
        if (switchTmp      != null) switchTmp.color      = switchOrigColor;
        var swImg3 = switchButton?.GetComponent<UnityEngine.UI.Image>();
        if (swImg3 != null) swImg3.color = switchImgOrig;
        switchButton?.GetComponent<SwitchButtonFX>()?.SetBorderAlpha(1f);
        switchButton?.GetComponent<SwitchButtonFX>()?.SetWaveAlpha(1f);
        titleScreenManager?.ReleaseAllButtons();
        _voidGhostCoroutine = null;
    }

    // ── hopeGhost 完整序列协程 ───────────────────────

    IEnumerator HopeGhostRoutine()
    {
        // ── 准备 ──────────────────────────────────────
        if (titleMain != null) _hopeGhostTitle.fontSize = titleMain.fontSize;

        var ghostRT = _hopeGhostTitle.GetComponent<RectTransform>();
        if (ghostAnchor != null)
        {
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(null, ghostAnchor.position);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _overlayRoot.GetComponent<RectTransform>(), screenPos, null, out Vector2 localPos);
            ghostRT.anchoredPosition = localPos;
        }
        else ghostRT.anchoredPosition = new Vector2(0f, 60f);

        Vector2 ghostBasePos = ghostRT.anchoredPosition;

        _hopeGhostTitle.text    = hopeGhostTitleText;
        _hopeGhostTitle.color   = new Color(hopeColor.r, hopeColor.g, hopeColor.b, 0f);
        _hopeGhostTitle.enabled = true;
        _hopeGhostSub.enabled   = false;

        // 收集所有标题原始颜色
        Color titleOrigColor  = titleMain     != null ? titleMain.color     : Color.white;
        Color labelOrigColor  = factionLabel  != null ? factionLabel.color  : Color.white;
        Color subOrigColor    = titleSub      != null ? titleSub.color      : Color.white;
        Color label2OrigColor = factionLabel2 != null ? factionLabel2.color : Color.white;
        Color sepOrigColor    = separatorImage != null ? separatorImage.color : Color.white;
        var   switchTmp       = switchButton?.GetComponentInChildren<TextMeshProUGUI>();
        Color switchOrigColor = switchTmp != null ? switchTmp.color : Color.white;
        Color switchImgOrig   = switchButton?.GetComponent<UnityEngine.UI.Image>() != null
                              ? switchButton.GetComponent<UnityEngine.UI.Image>().color : Color.white;

        // 只有菜单按钮变色+乱码，标题类只乱码不变色
        titleMain?.GetComponent<GlitchText>()?.GlitchHold();
        titleSub?.GetComponent<GlitchText>()?.GlitchHold();
        factionLabel2?.GetComponent<GlitchText>()?.GlitchHold();
        titleScreenManager?.GlitchHoldAllButtons(hopeColor);

        // ── 阶段1：渐黑 0~0.8s ───────────────────────
        float t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float a = Mathf.Clamp01(t / 0.8f);
            titleBackground?.SetDarkness(a);
            yield return null;
        }

        // ── 阶段2：标题淡出 0.8~1.4s ─────────────────
        t = 0f;
        while (t < 0.6f)
        {
            t += Time.deltaTime;
            float a = 1f - Mathf.Clamp01(t / 0.6f);
            // titleMain/titleSub/factionLabel2 由 GlitchHold 控制内容，只控制 alpha
            if (titleMain      != null) titleMain.color      = new Color(titleMain.color.r,     titleMain.color.g,     titleMain.color.b,     titleOrigColor.a  * a);
            if (factionLabel   != null) factionLabel.color   = new Color(labelOrigColor.r,      labelOrigColor.g,      labelOrigColor.b,      labelOrigColor.a  * a);
            if (titleSub       != null) titleSub.color       = new Color(titleSub.color.r,      titleSub.color.g,      titleSub.color.b,      subOrigColor.a    * a);
            if (factionLabel2  != null) factionLabel2.color  = new Color(factionLabel2.color.r, factionLabel2.color.g, factionLabel2.color.b, label2OrigColor.a * a);
            if (separatorImage != null) separatorImage.color = new Color(sepOrigColor.r,    sepOrigColor.g,    sepOrigColor.b,    sepOrigColor.a    * a);
            if (switchTmp      != null) switchTmp.color      = new Color(switchOrigColor.r, switchOrigColor.g, switchOrigColor.b, switchOrigColor.a * a);
            var swImg = switchButton?.GetComponent<UnityEngine.UI.Image>();
            if (swImg != null) swImg.color = new Color(switchImgOrig.r, switchImgOrig.g, switchImgOrig.b, switchImgOrig.a * a);
            switchButton?.GetComponent<SwitchButtonFX>()?.SetBorderAlpha(a);
            switchButton?.GetComponent<SwitchButtonFX>()?.SetWaveAlpha(a);
            yield return null;
        }
        if (titleMain      != null) titleMain.color      = new Color(titleMain.color.r,     titleMain.color.g,     titleMain.color.b,     0f);
        if (factionLabel   != null) factionLabel.color   = new Color(labelOrigColor.r,      labelOrigColor.g,      labelOrigColor.b,      0f);
        if (titleSub       != null) titleSub.color       = new Color(titleSub.color.r,      titleSub.color.g,      titleSub.color.b,      0f);
        if (factionLabel2  != null) factionLabel2.color  = new Color(factionLabel2.color.r, factionLabel2.color.g, factionLabel2.color.b, 0f);
        if (separatorImage != null) separatorImage.color = new Color(sepOrigColor.r,    sepOrigColor.g,    sepOrigColor.b,    0f);
        if (switchTmp      != null) switchTmp.color      = new Color(switchOrigColor.r, switchOrigColor.g, switchOrigColor.b, 0f);
        var swImg2 = switchButton?.GetComponent<UnityEngine.UI.Image>();
        if (swImg2 != null) swImg2.color = new Color(switchImgOrig.r, switchImgOrig.g, switchImgOrig.b, 0f);
        switchButton?.GetComponent<SwitchButtonFX>()?.SetBorderAlpha(0f);
        switchButton?.GetComponent<SwitchButtonFX>()?.SetWaveAlpha(0f);

        // ── 阶段3：幽灵淡入 1.4~2.2s ─────────────────
        t = 0f;
        while (t < 0.8f)
        {
            t += Time.deltaTime;
            float a     = Mathf.Clamp01(t / 0.8f) * hopeGhostAlpha;
            float floatY= Mathf.Clamp01(t / 0.8f) * hopeGhostFloatAmp * 0.5f;
            _hopeGhostTitle.color        = new Color(hopeColor.r, hopeColor.g, hopeColor.b, a);
            ghostRT.anchoredPosition     = ghostBasePos + new Vector2(0f, floatY);
            yield return null;
        }

        // ── 阶段4：持续上浮 2.2~3.5s ─────────────────
        t = 0f;
        while (t < 1.3f)
        {
            t += Time.deltaTime;
            // 缓慢上浮 + 轻微左右飘动
            float floatY = hopeGhostFloatAmp * 0.5f + Mathf.Sin(t * 1.2f) * hopeGhostFloatAmp * 0.3f;
            float floatX = Mathf.Sin(t * 0.9f) * 4f;
            ghostRT.anchoredPosition     = ghostBasePos + new Vector2(floatX, floatY);
            _hopeGhostTitle.color        = new Color(hopeColor.r, hopeColor.g, hopeColor.b, hopeGhostAlpha);
            yield return null;
        }

        // ── 阶段5：hopeSin 爆发 3.5~4.2s ─────────────
        TriggerAction("hopeSin");
        yield return new WaitForSeconds(0.7f);

        // ── 阶段6：等待 hopeSin 波纹消散后才还原 ────────
        // 同时幽灵标题缓慢淡出
        float waitMax   = hopeSin.duration;
        float waited    = 0f;
        float fadeStart = _hopeGhostTitle.color.a;
        while (waited < waitMax && _states["hopeSin"].active)
        {
            waited += Time.deltaTime;
            // 随 hopeSin 的 progress 同步淡出
            float sinP   = Mathf.Clamp01(_states["hopeSin"].p);
            float fadeA  = Mathf.Lerp(fadeStart, 0f, sinP);
            _hopeGhostTitle.color = new Color(hopeColor.r, hopeColor.g, hopeColor.b, fadeA);
            yield return null;
        }
        _hopeGhostTitle.enabled = false;

        // 还原背景黑暗，触发一次心跳
        titleBackground?.SetDarkness(0f);
        titleBackground?.TriggerBeat();

        if (separatorImage != null) separatorImage.color = sepOrigColor;
        titleMain?.GetComponent<GlitchText>()?.Release();
        titleSub?.GetComponent<GlitchText>()?.Release();
        factionLabel2?.GetComponent<GlitchText>()?.Release();
        if (titleMain      != null) titleMain.color      = titleOrigColor;
        if (factionLabel   != null) factionLabel.color   = labelOrigColor;
        if (titleSub       != null) titleSub.color       = subOrigColor;
        if (factionLabel2  != null) factionLabel2.color  = label2OrigColor;
        var swImg3 = switchButton?.GetComponent<UnityEngine.UI.Image>();
        if (swImg3 != null) swImg3.color = switchImgOrig;
        switchButton?.GetComponent<SwitchButtonFX>()?.SetBorderAlpha(1f);
        switchButton?.GetComponent<SwitchButtonFX>()?.SetWaveAlpha(1f);
        if (switchTmp != null)
            switchTmp.DOColor(new Color(switchOrigColor.r, switchOrigColor.g, switchOrigColor.b, 1f), 0.4f);
        titleScreenManager?.ReleaseAllButtons();
        _hopeGhostCoroutine = null;
    }

    void HideVoidAll()
    {
        if (_voidGhostCoroutine  != null) { StopCoroutine(_voidGhostCoroutine);  _voidGhostCoroutine  = null; }
        if (_voidCrackCoroutine  != null) { StopCoroutine(_voidCrackCoroutine);  _voidCrackCoroutine  = null; }
        if (_screenRollCoroutine != null) { StopCoroutine(_screenRollCoroutine); _screenRollCoroutine = null; }
        if (_popupExitCoroutine  != null) { StopCoroutine(_popupExitCoroutine);  _popupExitCoroutine  = null; }
        titleBackground?.SetRollOffset(0f);
        titleBackground?.SetDarkness(0f);
        _overlayRoot.transform.localPosition = Vector3.zero;
        if (glitchFX != null) { glitchFX.TearIntensity = 0f; glitchFX.RGBIntensity = 0f; }
        _noiseImage.enabled = false;
        if (_flashRedImage  != null) _flashRedImage.enabled  = false;
        if (_edgeBleedImage != null) _edgeBleedImage.enabled = false;
        _ghostTitle.enabled = false;
        _ghostSub.enabled   = false;
        void RestoreAlpha(TextMeshProUGUI t2) { if (t2 != null) t2.color = new Color(t2.color.r, t2.color.g, t2.color.b, 1f); }
        RestoreAlpha(titleMain); RestoreAlpha(factionLabel); RestoreAlpha(titleSub); RestoreAlpha(factionLabel2);
        if (separatorImage != null) separatorImage.color = new Color(separatorImage.color.r, separatorImage.color.g, separatorImage.color.b, 1f);
        var swTmp2 = switchButton?.GetComponentInChildren<TextMeshProUGUI>();
        if (swTmp2 != null) swTmp2.color = new Color(swTmp2.color.r, swTmp2.color.g, swTmp2.color.b, 1f);
        var swImg4 = switchButton?.GetComponent<UnityEngine.UI.Image>();
        if (swImg4 != null) swImg4.color = new Color(swImg4.color.r, swImg4.color.g, swImg4.color.b, 1f);
        switchButton?.GetComponent<SwitchButtonFX>()?.SetBorderAlpha(1f);
        switchButton?.GetComponent<SwitchButtonFX>()?.SetWaveAlpha(1f);
        foreach (var go in _errorBarObjects) go.SetActive(false);
        ClearPopups();
    }

    void HideHopeAll()
    {
        _glRenderer?.ClearAll();
        if (_hopePopupExitCoroutine != null) { StopCoroutine(_hopePopupExitCoroutine); _hopePopupExitCoroutine = null; }
        if (_hopeSweepCoroutine     != null) { StopCoroutine(_hopeSweepCoroutine);     _hopeSweepCoroutine     = null; }
        if (_hopeLEDCoroutine       != null) { StopCoroutine(_hopeLEDCoroutine);       _hopeLEDCoroutine       = null; }
        if (_hopeGhostCoroutine     != null) { StopCoroutine(_hopeGhostCoroutine);     _hopeGhostCoroutine     = null; }
        if (_hopeGhostTitle != null) _hopeGhostTitle.enabled = false;
        if (_hopeGhostSub   != null) _hopeGhostSub.enabled   = false;
        titleBackground?.SetDarkness(0f);
        if (glitchFX != null) glitchFX.LEDIntensity = 0f;
        if (_flashWhiteImage != null) _flashWhiteImage.color = new Color(1f,1f,1f,0f);
        ClearHopePopups();
        foreach (var name in new[]{"hopeDrops","hopeSin","hopePopup","hopeSweep","hopeLED","flashBlue"})
        { var s = _states[name]; s.active = false; s.p = 0f; }
    }
    void StopAllHopeEffects()
    {
        if (_hopeDropsCoroutine != null) { StopCoroutine(_hopeDropsCoroutine); _hopeDropsCoroutine = null; }
    }
    void HideAll() => HideVoidAll();

    // ── UI 元素构建 ───────────────────────────────────

    void BuildUIElements()
    {
        // overlayRoot 必须挂在 Canvas 里才能渲染
        Transform uiParent = _canvasRect != null ? _canvasRect : transform;
        _overlayRoot = new GameObject("GlitchOverlay");
        _overlayRoot.transform.SetParent(uiParent, false);
        var rootRT = _overlayRoot.AddComponent<RectTransform>();
        rootRT.anchorMin = Vector2.zero;
        rootRT.anchorMax = Vector2.one;
        rootRT.offsetMin = Vector2.zero;
        rootRT.offsetMax = Vector2.zero;

        // 闪红噪点（RawImage，复用噪点贴图机制）
        {
            var go = new GameObject("FlashRed");
            go.transform.SetParent(_overlayRoot.transform, false);
            _flashRedImage = go.AddComponent<RawImage>();
            _flashRedImage.raycastTarget = false;
            _flashRedImage.enabled = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            _redNoiseTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            _redNoiseTex.filterMode = FilterMode.Point;
            _redNoiseTex.wrapMode   = TextureWrapMode.Repeat;
            _flashRedImage.texture  = _redNoiseTex;
        }

        // 闪蓝噪点
        {
            var go = new GameObject("FlashBlue");
            go.transform.SetParent(_overlayRoot.transform, false);
            _flashBlueImage = go.AddComponent<RawImage>();
            _flashBlueImage.raycastTarget = false;
            _flashBlueImage.enabled = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // 熄忘裂缝
        {
            var go = new GameObject("VoidCrack");
            go.transform.SetParent(_overlayRoot.transform, false);
            _voidCrackImage = go.AddComponent<RawImage>();
            _voidCrackImage.raycastTarget = false;
            _voidCrackImage.enabled = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            if (voidCrackMaterial != null)
                _voidCrackImage.material = voidCrackMaterial;
        }

        // 噪点：高分辨率贴图 + Repeat 平铺，点小而密集
        _noiseTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
        _noiseTex.filterMode = FilterMode.Point;
        _noiseTex.wrapMode   = TextureWrapMode.Repeat;
        var noiseGO = new GameObject("Noise");
        noiseGO.transform.SetParent(_overlayRoot.transform, false);
        _noiseImage = noiseGO.AddComponent<RawImage>();
        _noiseImage.raycastTarget = false;
        _noiseImage.texture = _noiseTex;
        var noiseRT = noiseGO.GetComponent<RectTransform>();
        noiseRT.anchorMin = Vector2.zero; noiseRT.anchorMax = Vector2.one;
        noiseRT.offsetMin = Vector2.zero; noiseRT.offsetMax = Vector2.zero;
        _noiseImage.enabled = false;

        // 错误弹窗对象在运行时动态创建，无需预建

        // 边缘渗出
        // 边缘渗出：全屏 RawImage + EdgeBleed Shader
        {
            var go = new GameObject("EdgeBleed");
            go.transform.SetParent(_overlayRoot.transform, false);
            _edgeBleedImage = go.AddComponent<RawImage>();
            _edgeBleedImage.raycastTarget = false;
            _edgeBleedImage.enabled = false;
            if (edgeBleedMaterial != null)
                _edgeBleedImage.material = edgeBleedMaterial;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // 熄忘幽灵（voidGhost 序列用）
        _ghostTitle = CreateGhostText("GhostTitle", ghostTitleText, 60f, new Vector2(0f, 60f));
        _ghostSub   = CreateGhostText("GhostSub",   ghostSubText,   16f, new Vector2(0f, -20f));

        // 希望幽灵（hopeGhost 序列用）
        _hopeGhostTitle = CreateGhostText("HopeGhostTitle", hopeGhostTitleText, 60f, new Vector2(0f, 60f));
        _hopeGhostSub   = CreateGhostText("HopeGhostSub",   hopeGhostSubText,   16f, new Vector2(0f, -20f));

        // 闪白（hopeLED 过渡用）
        {
            var go = new GameObject("FlashWhite");
            go.transform.SetParent(_overlayRoot.transform, false);
            _flashWhiteImage = go.AddComponent<UnityEngine.UI.Image>();
            _flashWhiteImage.color = new Color(1f, 1f, 1f, 0f);
            _flashWhiteImage.raycastTarget = false;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        // 四角光晕（hopeGhost 用）
        _hopeCornerGlows = new UnityEngine.UI.Image[4];
        float cW2 = _canvasRect != null ? _canvasRect.rect.width  : Screen.width;
        float cH2 = _canvasRect != null ? _canvasRect.rect.height : Screen.height;
        float glowSize = Mathf.Min(cW2, cH2) * 0.45f; // 短边的 45%
        Vector2[] cornerAnchors = { Vector2.zero, new Vector2(1,0), new Vector2(0,1), Vector2.one };
        Vector2[] cornerPivots  = { Vector2.zero, new Vector2(1,0), new Vector2(0,1), Vector2.one };
        for (int ci = 0; ci < 4; ci++)
        {
            var go   = new GameObject($"HopeCornerGlow{ci}");
            go.transform.SetParent(_overlayRoot.transform, false);
            var img  = go.AddComponent<UnityEngine.UI.Image>();
            img.color          = new Color(hopeColor.r, hopeColor.g, hopeColor.b, 0f);
            img.raycastTarget  = false;
            var rt   = go.GetComponent<RectTransform>();
            rt.anchorMin       = cornerAnchors[ci];
            rt.anchorMax       = cornerAnchors[ci];
            rt.pivot           = cornerPivots[ci];
            rt.anchoredPosition= Vector2.zero;
            rt.sizeDelta       = new Vector2(glowSize, glowSize);
            _hopeCornerGlows[ci] = img;
        }

    }

    Image CreateFullscreenImage(string name, Color col)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_overlayRoot.transform, false);
        var img = go.AddComponent<Image>();
        img.color = col;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        return img;
    }



    TextMeshProUGUI CreateGhostText(string name, string text, float size, Vector2 pos)
    {
        var go = new GameObject(name);
        go.transform.SetParent(_overlayRoot.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text      = text;
        tmp.fontSize  = size;
        tmp.color     = new Color(voidColor.r, voidColor.g, voidColor.b, ghostAlpha);
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.raycastTarget = false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(800f, 100f);
        tmp.enabled  = false;
        return tmp;
    }




    void UpdateNoiseTex()
    {
        var pixels = _noiseTex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (Random.value < noiseDensity)
            {
                byte v = (byte)Random.Range(30, 150); // 亮度随机，压低上限改为灰调
                float r = Random.value;
                if (r < 0.08f)
                {
                    // 约8%红色（熄忘感）
                    pixels[i] = new Color32(v, 0, 0, 200);
                }
                else if (r < 0.13f)
                {
                    // 约5%青色（希望感）
                    pixels[i] = new Color32(0, v, v, 200);
                }
                else
                {
                    // 其余黑白灰
                    pixels[i] = new Color32(v, v, v, 200);
                }
            }
            else pixels[i] = new Color32(0, 0, 0, 0);
        }
        _noiseTex.SetPixels32(pixels);
        _noiseTex.Apply();
        float canvasW = _canvasRect != null ? _canvasRect.rect.width  : Screen.width;
        float canvasH = _canvasRect != null ? _canvasRect.rect.height : Screen.height;
        _noiseImage.uvRect = new Rect(0f, 0f, canvasW / _noiseTex.width, canvasH / _noiseTex.height);

    }

    void UpdateRedNoiseTex()
    {
        var pixels = _redNoiseTex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (Random.value < noiseDensity)
            {
                byte v = (byte)Random.Range(80, 255);
                pixels[i] = new Color32(v, 0, 0, 200);
            }
            else pixels[i] = new Color32(0, 0, 0, 0);
        }
        _redNoiseTex.SetPixels32(pixels);
        _redNoiseTex.Apply();
        float canvasW = _canvasRect != null ? _canvasRect.rect.width  : Screen.width;
        float canvasH = _canvasRect != null ? _canvasRect.rect.height : Screen.height;
        _flashRedImage.uvRect = new Rect(0f, 0f, canvasW / _redNoiseTex.width, canvasH / _redNoiseTex.height);
    }

    void UpdateBlueNoiseTex()
    {
        if (_blueNoiseTex == null)
        {
            _blueNoiseTex = new Texture2D(512, 512, TextureFormat.RGBA32, false);
            _blueNoiseTex.filterMode = FilterMode.Point;
            _blueNoiseTex.wrapMode   = TextureWrapMode.Repeat;
            _flashBlueImage.texture  = _blueNoiseTex;
        }
        var pixels = _blueNoiseTex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++)
        {
            if (Random.value < noiseDensity)
            {
                byte v = (byte)Random.Range(80, 255);
                // 青色：R=0, G=v, B=v（对应希望阵营颜色）
                pixels[i] = new Color32(0, v, v, 200);
            }
            else pixels[i] = new Color32(0, 0, 0, 0);
        }
        _blueNoiseTex.SetPixels32(pixels);
        _blueNoiseTex.Apply();
        float canvasW = _canvasRect != null ? _canvasRect.rect.width  : Screen.width;
        float canvasH = _canvasRect != null ? _canvasRect.rect.height : Screen.height;
        _flashBlueImage.uvRect = new Rect(0f, 0f, canvasW / _blueNoiseTex.width, canvasH / _blueNoiseTex.height);
    }


    float HashF(int n)
    {
        n = (int)(((long)n * 0x45d9f3b) & 0x7fffffff);
        n = (int)(((long)(n >> 16) ^ n) & 0x7fffffff);
        return (float)n / 2147483647f;
    }

    void SpawnPopup(int index)
    {
        if (errorMessages == null || errorMessages.Count == 0) return;
        if (_canvasRect == null) return;

        float cW = _canvasRect.rect.width;
        float cH = _canvasRect.rect.height;
        if (cW <= 0f || cH <= 0f) return;

        var msg  = errorMessages[Random.Range(0, errorMessages.Count)];
        float w  = Random.Range(popupWidthMin, popupWidthMax) * cW;
        float h  = w * popupAspect;
        float x  = Random.Range(0f, cW - w) - cW * 0.5f;
        float y  = Random.Range(0f, cH - h) - cH * 0.5f;
        bool  red= Random.value > 0.35f;
        Color col= red ? voidColor : hopeColor;
        float titleH = h * 0.22f;
        float fs     = h * 0.095f;

        int copies = Random.Range(popupCopyMin, popupCopyMax + 1);
        for (int ci = copies - 1; ci >= 0; ci--)
        {
            float ox = Random.Range(-popupCopyOffset, popupCopyOffset) * w;
            float oy = Random.Range(-popupCopyOffset, popupCopyOffset) * h;
            float ca = Random.Range(popupCopyAlphaMin, popupCopyAlphaMax);
            SpawnPopupInstance(msg, red, x + ox, y + oy, w, h, col, titleH, fs, ca, false);
        }
        SpawnPopupInstance(msg, red, x, y, w, h, col, titleH, fs, 1f, true);
    }

    void SpawnPopupInstance(ErrorMessage msg, bool red, float x, float y, float w, float h,
        Color col, float titleH, float fs, float targetAlpha, bool addJitter)
    {
        var root = new GameObject("Popup");
        root.transform.SetParent(_overlayRoot.transform, false);
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0.5f);
        rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot     = new Vector2(0f, 0f);
        rootRT.anchoredPosition = new Vector2(x, y);
        rootRT.sizeDelta = new Vector2(w, h);

        var cg = root.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        MakeImg(root, "Bg", new Color(0.03f,0.03f,0.055f,1f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        MakeImg(root, "TitleBg", red?new Color(0.16f,0.02f,0.02f,1f):new Color(0.02f,0.05f,0.08f,1f),
            new Vector2(0,1f-titleH/h), Vector2.one, Vector2.zero, Vector2.zero);
        float lw=1f;
        MakeImg(root,"BT",new Color(col.r,col.g,col.b,0.85f),new Vector2(0,1),Vector2.one,new Vector2(0,-lw),Vector2.zero);
        MakeImg(root,"BB",new Color(col.r,col.g,col.b,0.85f),Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,lw));
        MakeImg(root,"BL",new Color(col.r,col.g,col.b,0.85f),Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(lw,0));
        MakeImg(root,"BR",new Color(col.r,col.g,col.b,0.85f),new Vector2(1,0),Vector2.one,new Vector2(-lw,0),Vector2.zero);
        MakeImg(root,"Div",new Color(col.r,col.g,col.b,0.25f),
            new Vector2(0,1f-titleH/h),new Vector2(1,1f-titleH/h),new Vector2(0,-0.5f),new Vector2(0,0.5f));
        float bs=titleH*.62f,bx=w-bs-titleH*.2f,bby=h-titleH+titleH*.19f;
        MakeImgAbs(root,"XBorder",new Color(col.r,col.g,col.b,0.7f),bx,bby,bs,bs,w,h,0.8f);

        float padX    = w * 0.06f;
        float padYBot = h * 0.05f;
        float bodyJX  = Random.Range(-padX * 0.5f, padX * 0.5f);
        float bodyJY  = Random.Range(-h * 0.08f, h * 0.08f);
        float bodyBaseShift = bodyOffsetY * h;
        float bodyMinOffY = padYBot + bodyBaseShift;
        float bodyMaxOffY = -h * 0.15f + bodyBaseShift;

        var titleTmp = MakeTMP(root,"Title",msg.title,fs*1.1f,col,TextAlignmentOptions.Left,
            new Vector2(0,1f-titleH/h),Vector2.one,
            new Vector2(titleH*.3f,0),new Vector2(-titleH*.3f,0));
        titleTmp.fontStyle = FontStyles.Bold;

        var bodyTmp = MakeTMP(root,"Body",msg.body,fs,new Color(col.r,col.g,col.b,.88f),TextAlignmentOptions.Left,
            Vector2.zero,new Vector2(1,1f-titleH/h),
            new Vector2(padX + bodyJX, Mathf.Clamp(bodyMinOffY + bodyJY, padYBot * 0.3f, h * 0.3f)),
            new Vector2(-padX + bodyJX, Mathf.Clamp(bodyMaxOffY + bodyJY, -h * 0.4f, -padYBot)));

        float okW=w*.28f,okH=h*.14f,okX2=w*.36f,okY2=h*.10f;
        MakeImgAbs(root,"BtnBorder",new Color(col.r,col.g,col.b,.55f),okX2,okY2,okW,okH,w,h,0.8f);
        var btnTmp = MakeTMP(root,"Btn",msg.button,fs*.72f,new Color(col.r,col.g,col.b,.8f),TextAlignmentOptions.Center,
            new Vector2(okX2/w,okY2/h),new Vector2((okX2+okW)/w,(okY2+okH)/h),Vector2.zero,Vector2.zero);

        var btnHitGO = new GameObject("BtnHit");
        btnHitGO.transform.SetParent(root.transform, false);
        var btnHitRT = btnHitGO.AddComponent<RectTransform>();
        btnHitRT.anchorMin = new Vector2(okX2/w, okY2/h);
        btnHitRT.anchorMax = new Vector2((okX2+okW)/w, (okY2+okH)/h);
        btnHitRT.offsetMin = btnHitRT.offsetMax = Vector2.zero;
        var btnHitImg = btnHitGO.AddComponent<Image>();
        btnHitImg.color = Color.clear;
        var btnComp = btnHitGO.AddComponent<UnityEngine.UI.Button>();
        btnComp.transition = UnityEngine.UI.Selectable.Transition.None;
        btnComp.onClick.AddListener(() =>
        {
            ClearPopups();
            _states["errorBars"].active = false;
            _states["errorBars"].p = 0f;
            TitleScreenManager.Instance?.SwitchFaction();
        });

        var pd = new PopupData { root=root, cg=cg, born=_time };
        if (addJitter)
        {
            void RegJ(RectTransform rt2){ pd.jitterRTs.Add(rt2); pd.jitterBase.Add(rt2.anchoredPosition); }
            RegJ(titleTmp.GetComponent<RectTransform>());
            RegJ(bodyTmp.GetComponent<RectTransform>());
            RegJ(btnTmp.GetComponent<RectTransform>());
        }
        _popupObjects.Add(pd);
    }

    void UpdatePopupJitter()
    {
        if (popupJitterAmp <= 0f) return;
        int seed = Mathf.FloorToInt(_time * popupJitterFreq);
        int gi = 0;
        foreach (var pd in _popupObjects)
        {
            for (int i = 0; i < pd.jitterRTs.Count; i++)
            {
                var rt = pd.jitterRTs[i];
                if (rt == null) continue;
                float jx = (HashF(seed * 7 + gi * 13 + i * 31) * 2f - 1f) * popupJitterAmp;
                float jy = (HashF(seed * 11 + gi * 7 + i * 17) * 2f - 1f) * popupJitterAmp * 0.5f;
                rt.anchoredPosition = pd.jitterBase[i] + new Vector2(jx, jy);
            }
            gi++;
        }
    }


    void ClearPopups()
    {
        foreach (var pd in _popupObjects)
            if (pd.root != null) Destroy(pd.root);
        _popupObjects.Clear();
    }

    Image MakeImg(GameObject parent, string name, Color col,
        Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
        var img = go.AddComponent<Image>(); img.color=col; img.raycastTarget=false;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin=aMin;rt.anchorMax=aMax;rt.offsetMin=oMin;rt.offsetMax=oMax;
        return img;
    }

    void MakeImgAbs(GameObject parent, string name, Color col,
        float rx, float ry, float rw, float rh, float pw, float ph, float lw)
    {
        float xMin=rx/pw,xMax=(rx+rw)/pw,yMin=ry/ph,yMax=(ry+rh)/ph;
        MakeImg(parent,name+"T",col,new Vector2(xMin,yMax),new Vector2(xMax,yMax),new Vector2(0,-lw),Vector2.zero);
        MakeImg(parent,name+"B",col,new Vector2(xMin,yMin),new Vector2(xMax,yMin),Vector2.zero,new Vector2(0,lw));
        MakeImg(parent,name+"L",col,new Vector2(xMin,yMin),new Vector2(xMin,yMax),Vector2.zero,new Vector2(lw,0));
        MakeImg(parent,name+"R",col,new Vector2(xMax,yMin),new Vector2(xMax,yMax),new Vector2(-lw,0),Vector2.zero);
    }

    TextMeshProUGUI MakeTMP(GameObject parent, string name, string text,
        float fontSize, Color col, TextAlignmentOptions align,
        Vector2 aMin, Vector2 aMax, Vector2 oMin, Vector2 oMax)
    {
        var go = new GameObject(name); go.transform.SetParent(parent.transform, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text=text; tmp.fontSize=fontSize; tmp.color=col;
        tmp.alignment=align; tmp.raycastTarget=false;
        tmp.enableAutoSizing=false; tmp.overflowMode=TextOverflowModes.Overflow;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin=aMin;rt.anchorMax=aMax;rt.offsetMin=oMin;rt.offsetMax=oMax;
        return tmp;
    }


    // ══════════════════════════════════════════════════════
    // Hope 特效：UI 元素构建
    // ══════════════════════════════════════════════════════

    // ══════════════════════════════════════════════════════
    // Hope 特效：希望弹窗（严格对应 HTML HopePopup 逻辑）
    // ══════════════════════════════════════════════════════

    void BuildHopeSinUI()
    {
        if (hopeSinMaterial == null) return;
        var go = new GameObject("HopeSin");
        go.transform.SetParent(_overlayRoot.transform, false);
        _hopeSinImage = go.AddComponent<UnityEngine.UI.RawImage>();
        _hopeSinImage.raycastTarget = false;
        _hopeSinImage.enabled       = false;
        _hopeSinImage.material      = hopeSinMaterial;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    void UpdateHopeSin()
    {
        var s = _states["hopeSin"];
        if (!s.active)
        {
            if (_hopeSinImage != null) _hopeSinImage.enabled = false;
            return;
        }

        _hopeSinTime     += Time.deltaTime;
        _hopeSinProgress  = s.p; // 0~1，由 UpdateStates 驱动

        if (s.p >= 1f)
        {
            s.active = false; s.p = 0f;
            if (_hopeSinImage != null) _hopeSinImage.enabled = false;
            return;
        }

        if (hopeSinMaterial == null) return;

        float cW = _canvasRect.rect.width;
        float cH = _canvasRect.rect.height;

        hopeSinMaterial.SetColor("_Color",      hopeGlitchColor);
        hopeSinMaterial.SetFloat("_Time2",      _hopeSinTime);
        hopeSinMaterial.SetFloat("_Progress",   _hopeSinProgress);
        hopeSinMaterial.SetFloat("_Aspect",     cW / cH);
        hopeSinMaterial.SetFloat("_ScreenH",    cH);
        hopeSinMaterial.SetFloat("_Alpha",      1f);
        hopeSinMaterial.SetFloat("_WaveCount",  sinWaveCount);
        hopeSinMaterial.SetFloat("_RInner",     sinRInner);
        hopeSinMaterial.SetFloat("_AmpBase",    sinAmpBase);
        hopeSinMaterial.SetFloat("_AmpStep",    sinAmpStep);

        if (_hopeSinImage != null)
        {
            _hopeSinImage.enabled = true;
            _hopeSinImage.SetMaterialDirty();
        }
    }

        // ══════════════════════════════════════════════════════
    // Hope 特效：水滴落下（严格按 HTML Drop + spawnRipple 逻辑）
    // ══════════════════════════════════════════════════════

    IEnumerator HopeDropsRoutine()
    {
        if (_glRenderer == null) yield break;
        _glRenderer.HopeColor = hopeGlitchColor;

        float cW = _canvasRect.rect.width;
        float cH = _canvasRect.rect.height;
        float sY = cH / 360f;

        int count = 3 + Random.Range(0, 2);
        for (int k = 0; k < count; k++)
        {
            _glRenderer.FallingDrops.Add(new HopeGLRenderer.FallingDrop
            {
                x        = cW * 0.1f + Random.Range(0f, cW * 0.8f),
                y        = -12f * sY,
                vy       = 0f,
                gravity  = 0.55f * sY * 60f * 60f,
                groundY  = cH * 0.5f + Random.Range(0f, cH * 0.3f),
                size     = (3f + Random.Range(0f, 4f)) * sY * 0.6f,
                splashed = false,
            });
            float delay = k * 0.18f + Random.Range(0f, 0.1f);
            yield return new WaitForSeconds(delay);
        }
        _hopeDropsCoroutine = null;
    }

    // ══════════════════════════════════════════════════════
    // Hope 特效：希望扫掠（水波前锋横扫 + 光点 + 波纹）
    // ══════════════════════════════════════════════════════

    IEnumerator HopeSweepRoutine()
    {
        if (_glRenderer == null) yield break;
        _glRenderer.HopeColor = hopeGlitchColor;

        float cW  = _canvasRect.rect.width;
        float cH  = _canvasRect.rect.height;
        float sY  = cH / 360f;
        float sX  = cW / 680f;

        int   dotCount = Mathf.RoundToInt(Random.Range(sweepDotCountMin, sweepDotCountMax));
        float frontDur = sweepFrontDur;

        // 预先随机好每个光点的 x 比例和 y 位置
        var dotXRatio = new float[dotCount]; // 0~1，光点出现时前锋所在的 x 比例
        var dotY      = new float[dotCount];
        var dotSize   = new float[dotCount];
        var dotLife   = new float[dotCount];
        var dotSpawned= new bool[dotCount];

        for (int i = 0; i < dotCount; i++)
        {
            dotXRatio[i] = Random.value;
            dotY[i]      = Random.Range(cH * 0.08f, cH * 0.92f);
            dotSize[i]   = Random.Range(sweepDotSizeMin, sweepDotSizeMax) * sY;
            dotLife[i]   = Random.Range(sweepDotLifeMin, sweepDotLifeMax);
            dotSpawned[i]= false;
        }

        float elapsed = 0f;

        // 阶段1：前锋横扫，逐步生成光点
        while (elapsed < frontDur)
        {
            elapsed   += Time.deltaTime;
            float frontX = (elapsed / frontDur) * cW; // 前锋当前 x 像素位置

            for (int i = 0; i < dotCount; i++)
            {
                if (dotSpawned[i]) continue;
                float spawnX = dotXRatio[i] * cW;
                if (frontX >= spawnX)
                {
                    dotSpawned[i] = true;

                    // 生成光点（用 SplashArc 表示静止光点，vx/vy=0，fadeSpeed 由 life 决定）
                    _glRenderer.SplashArcs.Add(new HopeGLRenderer.SplashArc
                    {
                        cx        = spawnX,
                        cy        = dotY[i],
                        vx        = 0f,
                        vy        = 0f,
                        grav      = 0f,
                        ax        = Random.Range(-sweepJitterAmp, sweepJitterAmp) * sX,
                        ay        = Random.Range(-sweepJitterAmp, sweepJitterAmp) * sY,
                        size      = dotSize[i],
                        alpha     = 0.85f,
                        fadeSpeed = 1f / dotLife[i], // 每秒衰减量
                    });

                    // 生成小波纹
                    _glRenderer.SpawnRippleSet(
                        spawnX, dotY[i],
                        rings:      2,
                        htmlSpeed:  1.2f,
                        htmlMaxR:   sweepRippleMaxR,
                        peakAlpha:  0.5f
                    );
                }
            }

            yield return null;
        }

        // 阶段2：等待光点自然淡出（最长存活时间）
        yield return new WaitForSeconds(sweepDotLifeMax + 0.3f);

        _hopeSweepCoroutine = null;
    }

    // ══════════════════════════════════════════════════════
    // Hope 特效：LED 点阵（闪白→LED→闪白→还原）
    // ══════════════════════════════════════════════════════

    IEnumerator HopeLEDRoutine()
    {
        if (glitchFX == null || _flashWhiteImage == null) yield break;

        glitchFX.LEDSize       = ledSize;
        glitchFX.LEDGapRatio   = ledGapRatio;
        glitchFX.LEDBrightness = ledBrightness;
        glitchFX.LEDColorShift = ledColorShift;

        // ── 阶段1：闪白 + LED 切入 ───────────────────────
        float t = 0f;
        while (t < ledFadeInDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / ledFadeInDur);
            if (p < 0.5f)
            {
                _flashWhiteImage.color = new Color(0.5f, 0.5f, 0.5f, p * 0.5f);   // 闪灰
                glitchFX.LEDIntensity  = 0f;
            }
            else
            {
                _flashWhiteImage.color = new Color(0.5f, 0.5f, 0.5f, (1f - p) * 0.5f); // 闪灰
                glitchFX.LEDIntensity  = (p - 0.5f) * 2f;
            }
            yield return null;
        }
        _flashWhiteImage.color = new Color(1f, 1f, 1f, 0f);
        glitchFX.LEDIntensity  = 1f;

        // ── 阶段2：保持 LED ───────────────────────────────
        yield return new WaitForSeconds(ledHoldDur);

        // ── 阶段3：闪白 + 还原 ───────────────────────────
        t = 0f;
        while (t < ledFadeOutDur)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / ledFadeOutDur);
            if (p < 0.5f)
            {
                _flashWhiteImage.color = new Color(0.5f, 0.5f, 0.5f, p * 0.5f);   // 闪灰
                glitchFX.LEDIntensity  = 1f - p;
            }
            else
            {
                _flashWhiteImage.color = new Color(0.5f, 0.5f, 0.5f, (1f - p) * 0.5f); // 闪灰
                glitchFX.LEDIntensity  = 0f;
            }
            yield return null;
        }
        _flashWhiteImage.color = new Color(1f, 1f, 1f, 0f);
        glitchFX.LEDIntensity  = 0f;
        _hopeLEDCoroutine = null;
    }

    ActionParams GetParams(string name) => name switch
    {
        "blockTear"     => blockTear,
        "rgbSplit"      => rgbSplit,
        "errorBars"     => errorBars,
        "noise"         => noise,
        "voidGhost"     => voidGhost,
        "voidCrack"     => voidCrack,
        "flashRed"      => flashRed,
        "flashBlue"     => flashBlue,
        "edgeBleed"     => edgeBleed,
        "screenRoll"    => screenRoll,
        "hopeSin"       => hopeSin,
        "hopePopup"     => hopePopup,
        "hopeSweep"     => hopeSweep,
        "hopeLED"       => hopeLED,
        "hopeGhost"     => hopeGhost,
        _ => new ActionParams()
    };

    // ══════════════════════════════════════════════════════
    // Hope 弹窗：初始化数据
    // ══════════════════════════════════════════════════════

    void InitHopePopupData(ActionState s)
    {
        ClearHopePopups();
        float cW = _canvasRect.rect.width;
        float cH = _canvasRect.rect.height;

        int groupCount = Random.Range(hopePopupCountMin, hopePopupCountMax + 1);
        var entries    = new List<HopePopupSpawnEntry>();
        float cumTime  = 0f;

        var msgPool = new List<int>();
        for (int i = 0; i < hopeMessages.Count; i++) msgPool.Add(i);
        for (int i = msgPool.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            int tmp = msgPool[i]; msgPool[i] = msgPool[j]; msgPool[j] = tmp;
        }

        for (int gi = 0; gi < groupCount; gi++)
        {
            cumTime += Random.Range(0.06f, 0.18f);
            float w   = Random.Range(hopePopupWidthMin, hopePopupWidthMax) * cW;
            float h   = w * hopePopupAspect;
            float x   = Random.Range(0f, cW - w) - cW * 0.5f;
            float y   = Random.Range(0f, cH - h) - cH * 0.5f;
            int   mIdx= msgPool[gi % msgPool.Count];

            // 副本（半透明偏移，对应 HTML 里先生成的偏移副本）
            float ox = Random.Range(8f, 20f);
            float oy = Random.Range(6f, 16f);
            entries.Add(new HopePopupSpawnEntry
            {
                spawnTime   = cumTime,
                msgIndex    = mIdx,
                isMain      = false,
                x = x + ox, y = y - oy, w = w, h = h,
                targetAlpha = Random.Range(hopePopupCopyAlphaMin, hopePopupCopyAlphaMax),
            });
            cumTime += hopePopupSpawnInterval;

            // 主体
            entries.Add(new HopePopupSpawnEntry
            {
                spawnTime   = cumTime,
                msgIndex    = mIdx,
                isMain      = true,
                x = x, y = y, w = w, h = h,
                targetAlpha = 1f,
            });
            cumTime += hopePopupSpawnInterval;
        }

        s.active = true;
        s.p      = 0f;
        s.dur    = cumTime + hopePopup.duration;
        s.data   = entries;
    }

    // ══════════════════════════════════════════════════════
    // Hope 弹窗：每帧更新（生成 + 上浮 + jitter）
    // ══════════════════════════════════════════════════════

    void UpdateHopePopups()
    {
        var s = _states["hopePopup"];

        // 按 spawnTime 逐张生成
        if (s.active && s.data is List<HopePopupSpawnEntry> entries)
        {
            float elapsed = s.p * s.dur;
            for (int i = _hopePopupObjects.Count; i < entries.Count; i++)
            {
                var e = entries[i];
                if (elapsed < e.spawnTime) break;
                var msg = hopeMessages[Mathf.Clamp(e.msgIndex, 0, hopeMessages.Count - 1)];
                SpawnHopePopupInstance(msg, e.x, e.y, e.w, e.h, e.targetAlpha, e.isMain);
            }
        }

        // 上浮 + alpha 淡入 + jitter
        UpdateHopePopupFloat();
        if (hopePopupJitterAmp > 0f) UpdateHopePopupJitter();
    }

    void UpdateHopePopupFloat()
    {
        foreach (var pd in _hopePopupObjects)
        {
            if (pd.rootRT == null || pd.cg == null) continue;
            float age    = _time - pd.born;
            // 淡入：0.12秒内从0到1，对应 HTML fadeDur=25帧
            pd.cg.alpha  = Mathf.Clamp01(age / 0.12f);
            // 上浮：随 age 缓慢上浮，最大 floatAmp 像素，对应 HTML ry=y-alpha*8
            float floatY = Mathf.Clamp01(age / 0.5f) * hopePopupFloatAmp;
            pd.rootRT.anchoredPosition = new Vector2(
                pd.rootRT.anchoredPosition.x,
                pd.baseY + floatY
            );
        }
    }

    void UpdateHopePopupJitter()
    {
        int seed = Mathf.FloorToInt(_time * hopePopupJitterFreq);
        int gi   = 0;
        foreach (var pd in _hopePopupObjects)
        {
            for (int i = 0; i < pd.jitterRTs.Count; i++)
            {
                var rt = pd.jitterRTs[i];
                if (rt == null) continue;
                float jx = (HashF(seed * 7 + gi * 13 + i * 31) * 2f - 1f) * hopePopupJitterAmp;
                float jy = (HashF(seed * 11 + gi * 7 + i * 17) * 2f - 1f) * hopePopupJitterAmp * 0.5f;
                rt.anchoredPosition = pd.jitterBase[i] + new Vector2(jx, jy);
            }
            gi++;
        }
    }

    IEnumerator HopePopupExitRoutine()
    {
        // 淡出所有希望弹窗
        float dur = 0.3f;
        float t   = 0f;
        var   cgs = new List<(CanvasGroup cg, float startAlpha)>();
        foreach (var pd in _hopePopupObjects)
            if (pd.cg != null) cgs.Add((pd.cg, pd.cg.alpha));

        while (t < dur)
        {
            t += Time.deltaTime;
            float p = 1f - Mathf.Clamp01(t / dur);
            foreach (var (cg, sa) in cgs)
                if (cg != null) cg.alpha = sa * p;
            yield return null;
        }
        ClearHopePopups();
        _hopePopupExitCoroutine = null;
    }

    void ClearHopePopups()
    {
        foreach (var pd in _hopePopupObjects)
            if (pd.root != null) Destroy(pd.root);
        _hopePopupObjects.Clear();
    }

    // ══════════════════════════════════════════════════════
    // Hope 弹窗：单个实例构建（严格对应 HTML HopePopup）
    // ══════════════════════════════════════════════════════

    void SpawnHopePopupInstance(HopeMessage msg, float x, float y, float w, float h,
        float targetAlpha, bool addJitter)
    {
        var root = new GameObject("HopePopup");
        root.transform.SetParent(_overlayRoot.transform, false);
        var rootRT = root.AddComponent<RectTransform>();
        rootRT.anchorMin = new Vector2(0.5f, 0.5f);
        rootRT.anchorMax = new Vector2(0.5f, 0.5f);
        rootRT.pivot     = new Vector2(0f, 0f);
        rootRT.anchoredPosition = new Vector2(x, y);
        rootRT.sizeDelta = new Vector2(w, h);

        var cg    = root.AddComponent<CanvasGroup>();
        cg.alpha  = 0f;

        // 参考像素（对应 HTML canvas H=360）
        float sY  = h / (360f * hopePopupAspect);

        // ── 背景：极深色半透明，青色微染（对应 HTML rgba(4,14,18,0.82)）
        MakeImg(root, "Bg", new Color(0.016f, 0.055f, 0.071f, 0.82f),
            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

        // ── 细边框：青色 0.35（对应 HTML strokeStyle rgba(TR,TG,TB,0.35) lineWidth 0.8）
        float lw = 0.8f;
        MakeImg(root,"BT",new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.35f),new Vector2(0,1),Vector2.one,new Vector2(0,-lw),Vector2.zero);
        MakeImg(root,"BB",new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.35f),Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,lw));
        MakeImg(root,"BL",new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.35f),Vector2.zero,new Vector2(0,1),Vector2.zero,new Vector2(lw,0));
        MakeImg(root,"BR",new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.35f),new Vector2(1,0),Vector2.one,new Vector2(-lw,0),Vector2.zero);

        // ── 顶部装饰细线：青色 0.6（对应 HTML moveTo(rx+r,ry) lineTo(rx+w-r,ry)）
        MakeImg(root,"TopLine",new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.6f),
            new Vector2(0,1), Vector2.one, new Vector2(w*0.03f,-1f), new Vector2(-w*0.03f,0f));

        float fs      = h * 0.12f;   // 字体大小
        float titleH  = h * 0.28f;   // 标题区高度

        // ── 分隔线：青色 0.12（对应 HTML strokeStyle rgba(TR,TG,TB,0.12)）
        MakeImg(root,"Div",new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.12f),
            new Vector2(0, 1f - titleH/h), new Vector2(1, 1f - titleH/h),
            new Vector2(w*0.05f, -0.5f), new Vector2(-w*0.05f, 0.5f));

        // ── 标题：青色 0.9（对应 HTML fillStyle rgba(TR,TG,TB,0.9) font 500 11px monospace）
        var titleTmp = MakeTMP(root,"Title",msg.title, fs*1.1f,
            new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.9f),
            TextAlignmentOptions.Left,
            new Vector2(0, 1f - titleH/h), Vector2.one,
            new Vector2(w*0.07f, 0f), new Vector2(-w*0.07f, 0f));
        titleTmp.fontStyle = FontStyles.Bold;

        // ── 正文：青色 0.55（对应 HTML fillStyle rgba(TR,TG,TB,0.55) font 10px monospace）
        var bodyTmp = MakeTMP(root,"Body",msg.body, fs,
            new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.55f),
            TextAlignmentOptions.Left,
            Vector2.zero, new Vector2(1, 1f - titleH/h),
            new Vector2(w*0.07f, hopePopupBodyOffsetY * h), new Vector2(-w*0.07f, -h*0.06f));

        // ── X 图标（右上角，对应 errorBars 的 XBorder）
        float bs  = titleH * 0.62f;
        float bxX = w - bs - titleH * 0.2f;
        float byX = h - titleH + titleH * 0.19f;
        MakeImgAbs(root, "XBorder", new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.7f),
            bxX, byX, bs, bs, w, h, 0.8f);

        // ── 按钮：细框 + 极淡青色背景（对应 HTML roundRect button）
        float bw = w * 0.28f, bh = h * 0.14f;
        float bxBtn = w * 0.36f, byBtn = h * 0.10f;
        MakeImgAbs(root,"BtnBorder",new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.30f),
            bxBtn, byBtn, bw, bh, w, h, 0.6f);
        // 按钮极淡背景（对应 HTML rgba(TR,TG,TB,0.06)）
        MakeImg(root,"BtnBg", new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.06f),
            new Vector2(bxBtn/w, byBtn/h), new Vector2((bxBtn+bw)/w,(byBtn+bh)/h),
            Vector2.zero, Vector2.zero);
        var btnTmp = MakeTMP(root,"Btn",msg.button, fs*0.72f,
            new Color(hopeColor.r,hopeColor.g,hopeColor.b,0.65f),
            TextAlignmentOptions.Center,
            new Vector2(bxBtn/w,byBtn/h), new Vector2((bxBtn+bw)/w,(byBtn+bh)/h),
            Vector2.zero, Vector2.zero);

        // ── 点击区域：点击触发阵营切换
        var btnHitGO = new GameObject("BtnHit");
        btnHitGO.transform.SetParent(root.transform, false);
        var btnHitRT  = btnHitGO.AddComponent<RectTransform>();
        btnHitRT.anchorMin = new Vector2(bxBtn/w, byBtn/h);
        btnHitRT.anchorMax = new Vector2((bxBtn+bw)/w,(byBtn+bh)/h);
        btnHitRT.offsetMin = btnHitRT.offsetMax = Vector2.zero;
        var btnHitImg = btnHitGO.AddComponent<Image>();
        btnHitImg.color = Color.clear;
        var btnComp   = btnHitGO.AddComponent<UnityEngine.UI.Button>();
        btnComp.transition = UnityEngine.UI.Selectable.Transition.None;
        btnComp.onClick.AddListener(() =>
        {
            ClearHopePopups();
            _states["hopePopup"].active = false;
            _states["hopePopup"].p      = 0f;
            TitleScreenManager.Instance?.SwitchFaction();
        });

        var pd = new HopePopupData
        {
            root   = root,
            cg     = cg,
            rootRT = rootRT,
            born   = _time,
            baseY  = y,
        };
        // cg.alpha 用 targetAlpha 作为上限（副本比主体暗）
        // 通过 born time 驱动淡入，最终停在 targetAlpha
        // 重新实现：born 存 targetAlpha 用于淡入上限
        pd.born = _time - (1f - targetAlpha) * 0.12f; // 偏移 born 使副本淡入上限更低

        if (addJitter)
        {
            void RegJ(RectTransform rt2){ pd.jitterRTs.Add(rt2); pd.jitterBase.Add(rt2.anchoredPosition); }
            RegJ(titleTmp.GetComponent<RectTransform>());
            RegJ(bodyTmp.GetComponent<RectTransform>());
            RegJ(btnTmp.GetComponent<RectTransform>());
        }
        _hopePopupObjects.Add(pd);
    }
}