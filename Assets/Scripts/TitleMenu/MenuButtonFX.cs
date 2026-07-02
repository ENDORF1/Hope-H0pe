using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// 菜单按钮动画效果（完全重写版）。
///
/// 希望阵营（Hope）：
///   Hover  → 每个字符独立随机抽搐（不规律位移 + 随机闪烁），营造失控/危险感
///   Click  → 字符炸散成噪点方块，粗暴消失
///
/// 熄忘阵营（Void）：
///   Hover  → 文字先隐藏，由光标逐字"重新打印"，营造精密/仪式感
///   Click  → 光标从左到右精准擦除每个字符，干净无残留
///
/// Hope click 噪点方块由 MenuButtonGLRenderer 统一调用 GLDraw() 绘制，
/// 不创建任何子 GameObject。其余效果基于 TMP mesh 顶点操作（字符级）。
/// Void 光标用单个 Image 子对象实现，生命周期与 hover/click 严格绑定。
/// </summary>
[RequireComponent(typeof(Button))]
public class MenuButtonFX : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    // ═══════════════════════════════════════════════════
    // Inspector 参数 —— 希望阵营
    // ═══════════════════════════════════════════════════

    [Header("── 希望阵营 / Hover 抽搐")]

    [Tooltip("每次抽搐的最大水平位移（像素）。越大越剧烈。")]
    [SerializeField] private float hopeJitterRangeX = 10f;

    [Tooltip("每次抽搐的最大垂直位移（像素）。通常设为 X 的 40%~60%。")]
    [SerializeField] private float hopeJitterRangeY = 5f;

    [Tooltip("每次抽搐持续的最短时间（秒）。越小越频繁。")]
    [SerializeField] private float hopeJitterIntervalMin = 0.03f;

    [Tooltip("每次抽搐持续的最长时间（秒）。")]
    [SerializeField] private float hopeJitterIntervalMax = 0.10f;

    [Tooltip("字符随机闪烁的概率（每帧每字符）。0=不闪，0.05=偶尔闪。")]
    [Range(0f, 0.3f)]
    [SerializeField] private float hopeFlickerChance = 0.05f;

    [Tooltip("闪烁时字符 alpha 的最低值。0=完全消失，0.2=只是变暗。")]
    [Range(0f, 1f)]
    [SerializeField] private float hopeFlickerAlphaMin = 0.15f;

    [Header("── 希望阵营 / Click 噪点爆炸")]

    [Tooltip("爆炸持续时间（秒）。")]
    [SerializeField] private float hopeClickDuration = 0.4f;

    [Tooltip("爆炸产生的噪点方块数量。")]
    [SerializeField] private int hopeNoiseCount = 60;

    [Tooltip("噪点方块的最小尺寸（像素）。")]
    [SerializeField] private float hopeNoiseSizeMin = 4f;

    [Tooltip("噪点方块的最大尺寸（像素）。")]
    [SerializeField] private float hopeNoiseSizeMax = 14f;

    [Tooltip("噪点方块的水平扩散范围（像素，相对按钮屏幕中心）。")]
    [SerializeField] private float hopeNoiseSpreadX = 120f;

    [Tooltip("噪点方块的垂直扩散范围（像素，相对按钮屏幕中心）。")]
    [SerializeField] private float hopeNoiseSpreadY = 30f;

    [Tooltip("爆开阶段尾迹视觉像素长度。")]
    [SerializeField] private float hopeTrailLengthExplode = 120f;

    [Tooltip("转场推进阶段尾迹视觉像素长度。")]
    [SerializeField] private float hopeTrailLengthSlide = 200f;

    [Tooltip("相邻尾迹点间距（像素）。越小越连贯。推荐2~4。")]
    [SerializeField] private float hopeTrailSpacing = 2f;

    // ═══════════════════════════════════════════════════
    // Inspector 参数 —— 熄忘阵营
    // ═══════════════════════════════════════════════════

    [Header("── 熄忘阵营 / Hover 逐字打印")]

    [Tooltip("每个字符被打印出来的时间间隔（秒）。越小越快。")]
    [SerializeField] private float voidPrintInterval = 0.07f;

    [Tooltip("未打印字符的占位 alpha（0=完全不可见，0.15=若隐若现）。")]
    [Range(0f, 0.5f)]
    [SerializeField] private float voidUnprintedAlpha = 0.12f;

    [Tooltip("光标闪烁速度（Hz，每秒闪几次）。")]
    [SerializeField] private float voidCursorBlinkHz = 4f;

    [Tooltip("光标竖线的宽度（像素）。")]
    [SerializeField] private float voidCursorWidth = 2f;

    [Tooltip("光标竖线的高度相对字符高度的比例。1=与字符等高。")]
    [Range(0.5f, 1.5f)]
    [SerializeField] private float voidCursorHeightScale = 1.0f;

    [Header("── 熄忘阵营 / Click 光标擦除")]

    [Tooltip("光标从头擦到尾的总时间（秒）。越短越干脆。")]
    [SerializeField] private float voidWipeDuration = 0.28f;

    [Header("── 阵营颜色")]
    [Tooltip("希望阵营的颜色，应与 TMP 文字颜色保持一致。")]
    [SerializeField] private Color hopeColor = new Color(0.29f, 0.62f, 1f);

    [Tooltip("熄忘阵营的颜色，应与 TMP 文字颜色保持一致。")]
    [SerializeField] private Color voidColor = new Color(0.86f, 0.20f, 0.20f);

    // ═══════════════════════════════════════════════════
    // 内部状态
    // ═══════════════════════════════════════════════════

    private TitleScreenManager.Faction _faction = TitleScreenManager.Faction.Hope;
    private Button          _button;
    private RectTransform   _rect;
    private TextMeshProUGUI _tmp;
    private Camera          _canvasCamera; // World Space Canvas 的 worldCamera
    private bool            _isHovered  = false;
    private bool            _isClicking = false;
    private float           _hoverAlpha = 0f;
    private float           _idleTime   = 0f;
    private System.Action   _onClickCallback;

    // ── Hope：每字符抽搐状态
    private struct CharJitter
    {
        public float offsetX, offsetY;
        public float timer;
    }
    private CharJitter[] _jitters;

    // ── Hope：click 爆炸用
    // Hope click 粒子（TMP本地空间坐标）
    private struct HopeParticle
    {
        public float x, y;         // 当前位置（TMP本地空间）
        public float prevX, prevY; // 上一帧位置，用于插值
        public float vx, vy;       // 速度（本地空间单位/秒）
        public float w, h;         // 方块尺寸
        public float alpha;        // 当前透明度
        public float[] trailX;     // 尾迹历史X（屏幕像素坐标）
        public float[] trailY;     // 尾迹历史Y（屏幕像素坐标）
        public int    trailLen;    // 当前有效点数
    }
    private HopeParticle[]      _hopeParticles;
    private int                 _hopeParticleCount;
    private List<RectTransform> _noiseRects = new List<RectTransform>();
    private List<Image>         _noiseImgs  = new List<Image>();

    // ── Void：打印状态
    private float _printTimer   = 0f;
    private int   _printedCount = 0;
    private float _cursorBlink  = 0f;

    // ── Void：光标擦除位置（单位：字符索引，浮点）
    private float _wipePos = 0f;

    // ── 粒子追踪鼠标
    private Vector2 _mouseLocalPos; // 点击位置（TMP 本地坐标）

    // ── 通用 click 计时
    private float _clickTimer = 0f;

    // ── 转场模式标记（仅用于在 OnPointerClick 时通知 HopeTransition）
    private bool _transitionMode = false;

    /// <summary>标记此按钮为转场按钮，点击时通知 HopeTransition 启动协程。</summary>
    public void SetTransitionMode(bool enabled, float biasX = 0f)
    {
        _transitionMode = enabled;
    }

    // ── 外部速度叠加（由 HopeTransition 每帧调用，让粒子跟随镜头左移）
    private Vector2 _externalVelocity = Vector2.zero;

    /// <summary>
    /// 每帧由 HopeTransition 调用，叠加一个额外速度到所有粒子上。
    /// velocityDelta：本帧的位移增量（本地空间单位）。
    /// </summary>
    public void AddParticleOffset(Vector2 delta)
    {
        if (_hopeParticles == null) return;
        for (int i = 0; i < _hopeParticleCount; i++)
        {
            _hopeParticles[i].x += delta.x;
            _hopeParticles[i].y += delta.y;
        }
    }

    // ── 强制淡出（转场结束后由 HopeTransition 调用）
    private bool  _forceFadeOut     = false;
    private float _forceFadeOutTime = 0.3f;
    private float _forceFadeTimer   = 0f;

    /// <summary>
    /// 强制粒子在 duration 秒内淡出消失。
    /// 由 HopeTransition 在新场景加载完毕后调用。
    /// </summary>
    public void StartForceFadeOut(float duration)
    {
        _forceFadeOut     = true;
        _forceFadeOutTime = Mathf.Max(0.05f, duration);
        _forceFadeTimer   = 0f;
    }
    private Color _factionColor;

    // ── Void 光标 Image（单个子对象，懒创建，复用）
    private GameObject    _cursorObj;
    private RectTransform _cursorRT;
    private Image         _cursorImg;

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════

    void Awake()
    {
        _button            = GetComponent<Button>();
        _rect              = GetComponent<RectTransform>();
        _button.transition = Selectable.Transition.None;
    }

    void Start()
    {
        _tmp          = GetComponentInChildren<TextMeshProUGUI>();
        _factionColor = GetFactionColor();
        InitJitters();
        SetupHitbox();

        // 拿到 World Space Canvas 的渲染摄像机，用于正确的屏幕坐标转换
        var canvas = GetComponentInParent<Canvas>();
        _canvasCamera = (canvas != null) ? canvas.worldCamera : Camera.main;
    }

    /// <summary>
    /// 在 TMP 子节点下创建一个透明 Image 子对象作为精确碰撞箱。
    /// 大小和位置完全由 TMP.textBounds 决定（字符实际渲染区域），
    /// 不依赖 RectTransform 尺寸，不涉及屏幕坐标换算。
    /// Button 本体和 TMP 的 raycastTarget 全部关闭，只靠这个子对象响应输入。
    /// </summary>
    private void SetupHitbox()
    {
        // 关掉 Button 自身（prefab 的 400×80 透明区域）
        var img = GetComponent<Image>();
        if (img != null) img.raycastTarget = false;

        // 关掉 TMP 自身（避免 RectTransform 大框干扰）
        if (_tmp != null) _tmp.raycastTarget = false;

        if (_tmp == null) return;
        _tmp.ForceMeshUpdate();
        Bounds b = _tmp.textBounds;
        if (b.size == Vector3.zero) return;

        // 创建碰撞子对象，挂在 TMP 的 transform 下，坐标系与 TMP 本地空间一致
        var hitGO = new GameObject("TextHitbox", typeof(RectTransform), typeof(Image));
        hitGO.transform.SetParent(_tmp.transform, false);

        var hitRT = hitGO.GetComponent<RectTransform>();
        // anchor/pivot 都居中，anchoredPosition 对齐 textBounds 中心
        hitRT.anchorMin        = new Vector2(0.5f, 0.5f);
        hitRT.anchorMax        = new Vector2(0.5f, 0.5f);
        hitRT.pivot            = new Vector2(0.5f, 0.5f);
        // textBounds.center 是 TMP 本地空间的包围盒中心
        hitRT.anchoredPosition = new Vector2(b.center.x, b.center.y);
        hitRT.sizeDelta        = new Vector2(b.size.x, b.size.y);

        // 透明 Image，只用来接收射线
        var hitImg = hitGO.GetComponent<Image>();
        hitImg.color         = Color.clear;
        hitImg.raycastTarget = true;
    }

    void OnEnable()
    {
        TitleScreenManager.OnFactionChanged += SetFaction;
        if (MenuButtonGLRenderer.Instance != null)
            MenuButtonGLRenderer.Instance.Register(this);
    }

    void OnDisable()
    {
        TitleScreenManager.OnFactionChanged -= SetFaction;
        if (MenuButtonGLRenderer.Instance != null)
            MenuButtonGLRenderer.Instance.Unregister(this);
        RestoreAllChars();
        ClearNoise();
        DestroyVoidCursor();
    }

    // ═══════════════════════════════════════════════════
    // 公共接口（保持与旧版完全兼容）
    // ═══════════════════════════════════════════════════

    public void SetFaction(TitleScreenManager.Faction faction)
    {
        _faction      = faction;
        _factionColor = GetFactionColor();
        _isHovered    = false;
        _hoverAlpha   = 0f;
        _isClicking   = false;
        _clickTimer   = 0f;
        _printTimer   = 0f;
        _printedCount = 0;
        _wipePos      = 0f;
        RestoreAllChars();
        ClearNoise();
        DestroyVoidCursor();
        InitJitters();
    }

    public void SetClickCallback(System.Action callback)
    {
        _onClickCallback = callback;
    }

    // ═══════════════════════════════════════════════════
    // 主循环
    // ═══════════════════════════════════════════════════

    void Update()
    {
        if (_tmp == null) return;

        float targetAlpha = _isHovered ? 1f : 0f;
        float alphaSpeed  = _isHovered ? 8f : 10f;
        _hoverAlpha = Mathf.MoveTowards(_hoverAlpha, targetAlpha, alphaSpeed * Time.deltaTime);

        _tmp.ForceMeshUpdate();

        if (_faction == TitleScreenManager.Faction.Hope)
            UpdateHope();
        else
            UpdateVoid();
    }

    // ═══════════════════════════════════════════════════
    // 希望阵营 —— Update
    // ═══════════════════════════════════════════════════

    private void UpdateHope()
    {
        if (_isClicking)
        {
            UpdateHopeClick();
            return;
        }

        float a = _hoverAlpha;
        if (a <= 0.001f)
        {
            // 待机逐字呼吸：alpha 在 0.4~1.0 间波浪循环
            _idleTime += Time.deltaTime;
            _tmp.ForceMeshUpdate();
            var ti = _tmp.textInfo;
            for (int ci = 0; ci < ti.characterCount; ci++)
            {
                var ci2 = ti.characterInfo[ci];
                if (!ci2.isVisible) continue;
                int mi = ci2.materialReferenceIndex;
                int vi = ci2.vertexIndex;
                var mesh = ti.meshInfo[mi];
                float wave = Mathf.Sin(_idleTime * 1.8f - ci * 0.5f) * 0.5f + 0.5f;
                float alpha = Mathf.Lerp(0.4f, 1f, wave);
                byte a2 = (byte)(alpha * 255f);
                for (int v = 0; v < 4; v++)
                {
                    var col = mesh.colors32[vi + v];
                    mesh.colors32[vi + v] = new Color32(col.r, col.g, col.b, a2);
                }
            }
            _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
            return;
        }
        _idleTime = 0f;
        DestroyVoidCursor();

        EnsureJitters();

        var textInfo = _tmp.textInfo;

        for (int ci = 0; ci < textInfo.characterCount; ci++)
        {
            var charInfo = textInfo.characterInfo[ci];
            if (!charInfo.isVisible) continue;

            ref CharJitter jit = ref _jitters[ci];
            jit.timer -= Time.deltaTime;
            if (jit.timer <= 0f)
            {
                // 瞬间跳到新随机位置（无缓动 = 抽搐感）
                jit.offsetX = Random.Range(-hopeJitterRangeX, hopeJitterRangeX) * a;
                jit.offsetY = Random.Range(-hopeJitterRangeY, hopeJitterRangeY) * a;
                jit.timer   = Random.Range(hopeJitterIntervalMin, hopeJitterIntervalMax);
            }

            int     meshIdx  = charInfo.materialReferenceIndex;
            int     vertIdx  = charInfo.vertexIndex;
            var     meshInfo = textInfo.meshInfo[meshIdx];
            Vector3 offset   = new Vector3(jit.offsetX, jit.offsetY, 0f);

            meshInfo.vertices[vertIdx + 0] += offset;
            meshInfo.vertices[vertIdx + 1] += offset;
            meshInfo.vertices[vertIdx + 2] += offset;
            meshInfo.vertices[vertIdx + 3] += offset;

            // 随机闪烁
            if (Random.value < hopeFlickerChance * a)
            {
                float   fa = Random.Range(hopeFlickerAlphaMin, 1f);
                Color32 fc = new Color32(
                    meshInfo.colors32[vertIdx].r,
                    meshInfo.colors32[vertIdx].g,
                    meshInfo.colors32[vertIdx].b,
                    (byte)(fa * 255f));
                meshInfo.colors32[vertIdx + 0] = fc;
                meshInfo.colors32[vertIdx + 1] = fc;
                meshInfo.colors32[vertIdx + 2] = fc;
                meshInfo.colors32[vertIdx + 3] = fc;
            }
        }

        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private void UpdateHopeClick()
    {
        float dt = Time.deltaTime;
        _clickTimer += dt;
        float p = Mathf.Clamp01(_clickTimer / hopeClickDuration);

        // 强制淡出计时（转场结束后触发）
        if (_forceFadeOut)
        {
            _forceFadeTimer += dt;
            float fadeP = Mathf.Clamp01(_forceFadeTimer / _forceFadeOutTime);
            if (fadeP >= 1f)
            {
                _isClicking = false;
                _clickTimer = 0f;
                _forceFadeOut = false;
                ClearNoise();
                RestoreAllChars();
                return;
            }
        }

        // 1. 文字本体立刻隐藏（不淡出）
        SetAllCharsAlpha(0f);

        // 2. 每帧推进粒子位置+衰减alpha，同步到Image子对象
        if (_hopeParticles != null)
        {
            for (int i = 0; i < _hopeParticleCount; i++)
            {
                _hopeParticles[i].prevX = _hopeParticles[i].x;
                _hopeParticles[i].prevY = _hopeParticles[i].y;
                _hopeParticles[i].x    += _hopeParticles[i].vx * dt;
                _hopeParticles[i].y    += _hopeParticles[i].vy * dt;

                if (_forceFadeOut)
                {
                    // 强制淡出：按淡出进度衰减
                    _hopeParticles[i].alpha = Mathf.Max(0f, 1f - Mathf.Clamp01(_forceFadeTimer / _forceFadeOutTime));
                }
                else if (_transitionMode)
                {
                    // 转场模式：速度按阻力衰减，alpha保持不变，等待StartForceFadeOut
                    _hopeParticles[i].vx *= (1f - dt * 2f);
                    _hopeParticles[i].vy *= (1f - dt * 2f);
                }
                else
                {
                    // 吸引到鼠标点击位置，到达后才消失
                    float dx = _mouseLocalPos.x - _hopeParticles[i].x;
                    float dy = _mouseLocalPos.y - _hopeParticles[i].y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist > 3f)
                    {
                        float force = 400f / (dist + 20f);
                        _hopeParticles[i].vx += (dx / dist) * force * dt;
                        _hopeParticles[i].vy += (dy / dist) * force * dt;
                        // 飞行途中慢速衰减
                        _hopeParticles[i].alpha -= dt * 0.15f;
                    }
                    else
                    {
                        // 到达鼠标位置，快速消失
                        _hopeParticles[i].alpha -= dt * 4f;
                    }
                }

                if (i < _noiseRects.Count && _noiseRects[i] != null)
                {
                    _noiseRects[i].anchoredPosition = new Vector2(_hopeParticles[i].x, _hopeParticles[i].y);
                    _noiseImgs[i].color = new Color(
                        _factionColor.r, _factionColor.g, _factionColor.b,
                        _hopeParticles[i].alpha);
                }

                // 尾迹：存 TMP 本地坐标（和粒子同坐标系，镜头滑动不影响方向）
                if (_hopeParticles[i].trailX != null)
                {
                    float trailLength = _transitionMode ? hopeTrailLengthSlide : hopeTrailLengthExplode;
                    float spacing     = Mathf.Max(0.5f, hopeTrailSpacing);
                    int   maxPts      = _hopeParticles[i].trailX.Length;

                    float dx  = _hopeParticles[i].x - _hopeParticles[i].prevX;
                    float dy  = _hopeParticles[i].y - _hopeParticles[i].prevY;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    int steps = dist < spacing ? 0 : Mathf.FloorToInt(dist / spacing);
                    for (int s = 0; s <= steps; s++)
                    {
                        float t2  = steps > 0 ? (float)s / steps : 1f;
                        float ipx = _hopeParticles[i].prevX + dx * t2;
                        float ipy = _hopeParticles[i].prevY + dy * t2;

                        if (_hopeParticles[i].trailLen < maxPts)
                        {
                            _hopeParticles[i].trailX[_hopeParticles[i].trailLen] = ipx;
                            _hopeParticles[i].trailY[_hopeParticles[i].trailLen] = ipy;
                            _hopeParticles[i].trailLen++;
                        }
                        else
                        {
                            for (int t = 0; t < maxPts - 1; t++)
                            {
                                _hopeParticles[i].trailX[t] = _hopeParticles[i].trailX[t + 1];
                                _hopeParticles[i].trailY[t] = _hopeParticles[i].trailY[t + 1];
                            }
                            _hopeParticles[i].trailX[maxPts - 1] = ipx;
                            _hopeParticles[i].trailY[maxPts - 1] = ipy;
                        }
                    }

                    // 截断：从最新点往回数，超出trailPixels的旧点丢掉（前移起点）
                    float accum = 0f;
                    int   len2  = _hopeParticles[i].trailLen;
                    int   start = 0;
                    float trailLen = _transitionMode ? hopeTrailLengthSlide : hopeTrailLengthExplode;
                    for (int t = len2 - 1; t > 0; t--)
                    {
                        float ddx = _hopeParticles[i].trailX[t] - _hopeParticles[i].trailX[t - 1];
                        float ddy = _hopeParticles[i].trailY[t] - _hopeParticles[i].trailY[t - 1];
                        accum += Mathf.Sqrt(ddx * ddx + ddy * ddy);
                        if (accum > trailLen) { start = t; break; }
                    }
                    if (start > 0)
                    {
                        int newLen = len2 - start;
                        for (int t = 0; t < newLen; t++)
                        {
                            _hopeParticles[i].trailX[t] = _hopeParticles[i].trailX[t + start];
                            _hopeParticles[i].trailY[t] = _hopeParticles[i].trailY[t + start];
                        }
                        _hopeParticles[i].trailLen = newLen;
                    }
                }
            }
        }

        // 正常结束：非转场模式下hopeClickDuration到期才清除；转场模式只有StartForceFadeOut能结束
        if (!_forceFadeOut && !_transitionMode && p >= 1f)
        {
            _isClicking = false;
            _clickTimer = 0f;
            ClearNoise();
            RestoreAllChars();
            _onClickCallback?.Invoke();
        }
    }

    private void StartHopeClick()
    {
        _tmp.ForceMeshUpdate();
        var    textInfo = _tmp.textInfo;
        Bounds b        = _tmp.textBounds;

        // 文字包围盒本地坐标
        float bLeft = b.min.x;
        float bBot  = b.min.y;
        float bW    = b.size.x;
        float bH    = b.size.y;

        // 预生成粒子：在文字包围盒内随机分布，向外随机飞散
        ClearNoise();
        _hopeParticles      = new HopeParticle[hopeNoiseCount];
        _hopeParticleCount  = hopeNoiseCount;

        float cx = bLeft + bW * 0.5f;
        float cy = bBot  + bH * 0.5f;

        for (int i = 0; i < hopeNoiseCount; i++)
        {
            float px = bLeft + Random.value * bW;
            float py = bBot  + Random.value * bH;
            float w  = Random.Range(hopeNoiseSizeMin, hopeNoiseSizeMax);
            float h  = w * Random.Range(0.2f, 1f);

            float dx    = px - cx;
            float dy    = py - cy;
            float dist  = Mathf.Max(1f, Mathf.Sqrt(dx * dx + dy * dy));
            float speed = Random.Range(80f, 350f);
            float alpha = 0.3f + Random.value * 0.7f;

            float spacing = Mathf.Max(0.5f, hopeTrailSpacing);
            int   maxPts  = Mathf.CeilToInt(Mathf.Max(hopeTrailLengthExplode, hopeTrailLengthSlide) / spacing) + 2;
            _hopeParticles[i] = new HopeParticle
            {
                x      = px,   prevX = px,
                y      = py,   prevY = py,
                vx     = (dx / dist) * speed + Random.Range(-30f, 30f),
                vy     = (dy / dist) * speed + Random.Range(-30f, 30f),
                w      = w,
                h      = h,
                alpha  = alpha,
                trailX = new float[maxPts],
                trailY = new float[maxPts],
                trailLen = 0,
            };

            // 创建对应的 Image 子对象，挂在 TMP 下，坐标与粒子同步
            var go  = new GameObject("P" + i, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_tmp.transform, false);
            var rt  = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(0.5f, 0.5f);
            rt.anchorMax        = new Vector2(0.5f, 0.5f);
            rt.pivot            = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(px, py);
            rt.sizeDelta        = new Vector2(w, h);
            var img = go.GetComponent<Image>();
            img.color         = new Color(_factionColor.r, _factionColor.g, _factionColor.b, alpha);
            img.raycastTarget = false;
            _noiseRects.Add(rt);
            _noiseImgs.Add(img);
        }
    }

    // ═══════════════════════════════════════════════════
    // 希望阵营 —— GL 绘制（由 MenuButtonGLRenderer 调用）
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 粒子用 TMP 的 Image 子对象绘制，坐标在 TMP 本地空间。
    /// GL 不再使用，保留空实现维持接口兼容。
    /// </summary>
    public void GLDraw()
    {
        if (_hopeParticles == null || _hopeParticleCount == 0) return;

        Camera cam = _canvasCamera != null ? _canvasCamera : Camera.main;
        GL.Begin(GL.QUADS);
        for (int i = 0; i < _hopeParticleCount; i++)
        {
            if (_hopeParticles[i].trailX == null) continue;
            int len = _hopeParticles[i].trailLen;
            if (len == 0) continue;

            float baseAlpha = _hopeParticles[i].alpha;
            float pw = _hopeParticles[i].w;
            float ph = _hopeParticles[i].h;

            for (int t = 0; t < len; t++)
            {
                float ratio = (float)(t + 1) / len;       // t=0最旧最暗，t=len-1最新最亮（O==》）
                float a     = baseAlpha * ratio * 0.75f;
                if (a <= 0f) continue;
                float s  = Mathf.Max(0.5f, ratio * 0.8f);
                float hw = pw * s * 0.5f;
                float hh = ph * s * 0.5f;

                // 从 TMP 本地坐标转换为屏幕坐标（GL 用左上原点）
                Vector3 world = _tmp.transform.TransformPoint(
                    new Vector3(_hopeParticles[i].trailX[t], _hopeParticles[i].trailY[t], 0));
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
                screen.y = Screen.height - screen.y;
                float px = screen.x;
                float py = screen.y;

                GL.Color(new Color(_factionColor.r, _factionColor.g, _factionColor.b, a));
                GL.Vertex3(px - hw, py - hh, 0);
                GL.Vertex3(px + hw, py - hh, 0);
                GL.Vertex3(px + hw, py + hh, 0);
                GL.Vertex3(px - hw, py + hh, 0);
            }
        }
        GL.End();
    }

    // ═══════════════════════════════════════════════════
    // 熄忘阵营 —— Update
    // ═══════════════════════════════════════════════════

    private void UpdateVoid()
    {
        if (_isClicking)
        {
            UpdateVoidClick();
            return;
        }

        var textInfo = _tmp.textInfo;
        int total    = textInfo.characterCount;

        if (_isHovered || _printedCount < total)
        {
            if (_isHovered && _printedCount < total)
            {
                _printTimer   += Time.deltaTime;
                _printedCount  = Mathf.Min(
                    Mathf.FloorToInt(_printTimer / voidPrintInterval), total);
            }

            _cursorBlink += Time.deltaTime;

            for (int ci = 0; ci < total; ci++)
            {
                var charInfo = textInfo.characterInfo[ci];
                if (!charInfo.isVisible) continue;

                int meshIdx = charInfo.materialReferenceIndex;
                int vertIdx = charInfo.vertexIndex;
                var mesh    = textInfo.meshInfo[meshIdx];

                float alpha = (ci < _printedCount) ? 1f
                            : (_isHovered          ? voidUnprintedAlpha : 1f);

                SetCharAlpha(ref mesh, vertIdx, alpha);
            }

            UpdateVoidCursor(total, -1f);
            _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
        }
        else if (!_isHovered)
        {
            _printTimer   = 0f;
            _printedCount = 0;
            _cursorBlink  = 0f;
            RestoreAllChars();
            DestroyVoidCursor();
        }
    }

    private void UpdateVoidClick()
    {
        _clickTimer += Time.deltaTime;

        var textInfo = _tmp.textInfo;
        int total    = textInfo.characterCount;

        // 终点设为 total + 1，确保最后一帧 _wipePos 必然超过 total，所有字符都被擦掉
        _wipePos = (_clickTimer / voidWipeDuration) * (total + 1);

        for (int ci = 0; ci < total; ci++)
        {
            var charInfo = textInfo.characterInfo[ci];
            if (!charInfo.isVisible) continue;

            int meshIdx = charInfo.materialReferenceIndex;
            int vertIdx = charInfo.vertexIndex;
            var mesh    = textInfo.meshInfo[meshIdx];

            // 用 ci + 1 <= _wipePos 而不是 ci < Floor(_wipePos)，
            // 确保 _wipePos 到达每个字符右边缘时该字符立即消失，
            // 避免最后一个字符因浮点精度问题残留
            float alpha = (ci + 1f) <= _wipePos ? 0f : 1f;
            SetCharAlpha(ref mesh, vertIdx, alpha);
        }

        UpdateVoidCursor(total, _wipePos);
        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);

        if (_clickTimer >= voidWipeDuration)
        {
            // 强制把所有字符擦干净，不依赖 _wipePos 的浮点精度
            SetAllCharsAlpha(0f);
            DestroyVoidCursor();

            _isClicking   = false;
            _clickTimer   = 0f;
            _wipePos      = 0f;
            _printedCount = total;
            _printTimer   = total * voidPrintInterval;
            RestoreAllChars();
            _onClickCallback?.Invoke();
        }
    }

    // ═══════════════════════════════════════════════════
    // Void 光标
    // ═══════════════════════════════════════════════════

    /// <param name="cursorOverride">
    /// -1 = hover 打印模式，用 _printedCount 定位，光标闪烁；
    /// >= 0 = click 擦除模式，用该浮点值定位，光标常亮。
    /// </param>
    private void UpdateVoidCursor(int total, float cursorOverride)
    {
        int curIdx = cursorOverride >= 0f
            ? Mathf.FloorToInt(cursorOverride)
            : _printedCount;

        if (curIdx >= total)
        {
            DestroyVoidCursor();
            return;
        }

        // 懒创建
        if (_cursorObj == null)
        {
            _cursorObj = new GameObject("VoidCursor", typeof(RectTransform), typeof(Image));
            _cursorObj.transform.SetParent(_tmp.transform, false);
            _cursorRT              = _cursorObj.GetComponent<RectTransform>();
            _cursorImg             = _cursorObj.GetComponent<Image>();
            // anchor/pivot 都设为 (0.5, 0.5)，与 TMP pivot 一致，
            // 这样 charInfo.origin（相对 TMP pivot 的 x 坐标）可以直接用
            _cursorRT.anchorMin    = new Vector2(0.5f, 0.5f);
            _cursorRT.anchorMax    = new Vector2(0.5f, 0.5f);
            _cursorRT.pivot        = new Vector2(0.5f, 0.5f);
            _cursorImg.raycastTarget = false;
        }

        var   charInfo = _tmp.textInfo.characterInfo[curIdx];
        float charH    = charInfo.ascender - charInfo.descender;

        // charInfo.origin = 字符左边缘 x（相对 TMP pivot）
        // 光标 pivot 在自身中心，所以 x 加半个光标宽度让左边缘贴齐字符起点
        float cursorX = charInfo.origin + voidCursorWidth * 0.5f;
        _cursorRT.anchoredPosition = new Vector2(cursorX, 0f);
        _cursorRT.sizeDelta        = new Vector2(voidCursorWidth, charH * voidCursorHeightScale);

        float alpha = cursorOverride >= 0f
            ? 1f
            : (Mathf.Sin(_cursorBlink * voidCursorBlinkHz * Mathf.PI * 2f) > 0f ? 1f : 0f);

        _cursorImg.color = new Color(_factionColor.r, _factionColor.g, _factionColor.b, alpha);
    }

    private void DestroyVoidCursor()
    {
        if (_cursorObj != null)
        {
            Destroy(_cursorObj);
            _cursorObj = null;
            _cursorRT  = null;
            _cursorImg = null;
        }
    }

    // ═══════════════════════════════════════════════════
    // 指针事件
    // ═══════════════════════════════════════════════════

    public void OnPointerEnter(PointerEventData e)
    {
        if (_isClicking) return;
        _isHovered = true;
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (_isClicking) return;
        _isHovered = false;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (_isClicking || !_button.interactable) return;

        _isClicking = true;
        _isHovered  = false;
        _clickTimer = 0f;

        // 鼠标点击位置（TMP 本地坐标），用于粒子追踪
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _rect, e.position, _canvasCamera, out _mouseLocalPos);

        if (_faction == TitleScreenManager.Faction.Hope)
        {
            if (_transitionMode && HopeTransition.Instance != null)
                HopeTransition.Instance.BeginTransition();

            StartHopeClick();
        }
        // Void 无需预生成数据，UpdateVoidClick 直接推进
    }

    // ═══════════════════════════════════════════════════
    // 工具方法
    // ═══════════════════════════════════════════════════

    private void InitJitters()
    {
        if (_tmp == null) return;
        _tmp.ForceMeshUpdate();
        int n    = Mathf.Max(_tmp.textInfo.characterCount, 16);
        _jitters = new CharJitter[n];
        for (int i = 0; i < n; i++)
            _jitters[i].timer = Random.Range(hopeJitterIntervalMin, hopeJitterIntervalMax);
    }

    private void EnsureJitters()
    {
        int n = _tmp.textInfo.characterCount;
        if (_jitters == null || _jitters.Length < n)
        {
            var old  = _jitters;
            _jitters = new CharJitter[n + 4];
            if (old != null)
                System.Array.Copy(old, _jitters, Mathf.Min(old.Length, n));
            int start = old?.Length ?? 0;
            for (int i = start; i < _jitters.Length; i++)
                _jitters[i].timer = Random.Range(hopeJitterIntervalMin, hopeJitterIntervalMax);
        }
    }

    private void RestoreAllChars()
    {
        if (_tmp == null) return;
        // ForceMeshUpdate 将顶点重置回 TMP 计算的原始位置
        _tmp.ForceMeshUpdate();
        var textInfo = _tmp.textInfo;
        for (int ci = 0; ci < textInfo.characterCount; ci++)
        {
            var charInfo = textInfo.characterInfo[ci];
            if (!charInfo.isVisible) continue;
            int meshIdx = charInfo.materialReferenceIndex;
            int vertIdx = charInfo.vertexIndex;
            var mesh    = textInfo.meshInfo[meshIdx];
            SetCharAlpha(ref mesh, vertIdx, 1f);
        }
        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private static void SetCharAlpha(ref TMP_MeshInfo mesh, int vertIdx, float alpha)
    {
        byte a = (byte)(alpha * 255f);
        for (int v = 0; v < 4; v++)
        {
            Color32 c = mesh.colors32[vertIdx + v];
            c.a = a;
            mesh.colors32[vertIdx + v] = c;
        }
    }

    private void SetAllCharsAlpha(float alpha)
    {
        var textInfo = _tmp.textInfo;
        for (int ci = 0; ci < textInfo.characterCount; ci++)
        {
            var charInfo = textInfo.characterInfo[ci];
            if (!charInfo.isVisible) continue;
            int meshIdx = charInfo.materialReferenceIndex;
            int vertIdx = charInfo.vertexIndex;
            var mesh    = textInfo.meshInfo[meshIdx];
            SetCharAlpha(ref mesh, vertIdx, alpha);
        }
        _tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private void ClearNoise()
    {
        foreach (var rt in _noiseRects)
            if (rt != null) Destroy(rt.gameObject);
        _noiseRects.Clear();
        _noiseImgs.Clear();
        _hopeParticles     = null;
        _hopeParticleCount = 0;
    }

    /// <summary>
    /// 把按钮中心转换成屏幕像素坐标（左上原点，与 GL.LoadPixelMatrix 一致）。
    /// </summary>
    private Vector2 GetButtonScreenCenter()
    {
        // 必须传入 World Space Canvas 的 worldCamera，
        // 传 null 在 World Space 模式下坐标完全错位
        Camera cam = _canvasCamera != null ? _canvasCamera : Camera.main;

        if (_tmp == null)
        {
            Vector2 pt0 = RectTransformUtility.WorldToScreenPoint(cam, _rect.position);
            return new Vector2(pt0.x, Screen.height - pt0.y);
        }

        // 用 textBounds 世界空间中心，确保噪点从文字实际位置炸开
        Bounds   b      = _tmp.textBounds;
        Vector3  worldC = _tmp.transform.TransformPoint(b.center);
        Vector2  sc     = RectTransformUtility.WorldToScreenPoint(cam, worldC);
        // 翻转 Y：Unity 屏幕左下原点 → GL.LoadPixelMatrix 左上原点
        return new Vector2(sc.x, Screen.height - sc.y);
    }

    private Color GetFactionColor()
    {
        return _faction == TitleScreenManager.Faction.Hope ? hopeColor : voidColor;
    }
}