using UnityEngine;
using UnityEngine.Rendering;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 水面气泡特效渲染器。
/// 挂在与 ScreenGlitchUI / HopeGLRenderer 相同的 GameObject 上。
///
/// 职责：
///   1. 用 CommandBuffer 在 MainCamera 渲染后抓屏到 RenderTexture
///   2. 管理气泡生命周期（生长 → 破裂）
///   3. 破裂阶段：白闪 + 碎片用 GL 绘制，波纹交由 HopeGLRenderer
///
/// 使用方式：
///   WaterBubbleRenderer.Instance.SpawnBubble();
///   或由 ScreenGlitchUI.TriggerAction("hopeBubble") 调用
/// </summary>
[RequireComponent(typeof(Camera))]  // 需要摄像机才能挂 CommandBuffer，此组件挂在独立GO上
public class WaterBubbleRenderer : MonoBehaviour
{
    public static WaterBubbleRenderer Instance { get; private set; }

    // ── Inspector 引用 ────────────────────────────────
    [Header("引用")]
    [Tooltip("主摄像机，用于挂 CommandBuffer 抓屏")]
    [SerializeField] private Camera mainCamera;
    [Tooltip("WaterBubble 材质，Shader 选 XiWang_XiWang/WaterBubble")]
    [SerializeField] private Material bubbleMaterial;
    [Tooltip("HopeGLRenderer，破裂时触发波纹")]
    [SerializeField] private HopeGLRenderer glRenderer;

    [Header("气泡参数")]
    [Tooltip("气泡最大半径（屏幕高度的比例）")]
    public float bubbleMaxRadiusRatio = 0.145f;  // HTML: 52/360 ≈ 0.144
    [Tooltip("生长时长（秒）")]
    public float growDuration  = 80f / 60f;      // HTML: 80帧
    [Tooltip("破裂时长（秒）")]
    public float burstDuration = 14f / 60f;      // HTML: 14帧

    [Header("Hope 颜色")]
    public Color hopeColor = new Color(0.24f, 0.91f, 0.78f, 1f);

    // ── 内部状态 ──────────────────────────────────────
    private RenderTexture _backgroundRT;
    private CommandBuffer _cmdBuffer;
    private bool          _cmdAttached = false;

    // 全屏四边形 Mesh（用于绘制气泡 Shader）
    private Mesh     _quadMesh;
    private Material _mat;

    // GL 材质（破裂碎片）
    private Material _glMat;

    // 圆形方向缓存（破裂碎片用）
    private const int CIRCLE_SEGS = 64;
    private static readonly Vector2[] _circleDir = new Vector2[CIRCLE_SEGS + 1];
    private static bool _circleDirReady = false;

    // ── 气泡数据 ──────────────────────────────────────
    private class Bubble
    {
        public float cx, cy;         // 屏幕像素坐标
        public float elapsed;
        public float growDur;
        public float burstDur;
        public bool  burstTriggered;

        // 破裂碎片
        public List<BurstShard> shards = new List<BurstShard>();
        public float burstElapsed;

        // 状态
        public enum Phase { Grow, Burst, Done }
        public Phase phase = Phase.Grow;
    }

    private class BurstShard
    {
        public float vx, vy, ax, ay, size, alpha;
    }

    private readonly List<Bubble> _bubbles = new List<Bubble>();

    // ══════════════════════════════════════════════════
    // 初始化
    // ══════════════════════════════════════════════════

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        // 预计算圆方向
        if (!_circleDirReady)
        {
            for (int i = 0; i <= CIRCLE_SEGS; i++)
            {
                float a = i / (float)CIRCLE_SEGS * Mathf.PI * 2f;
                _circleDir[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            _circleDirReady = true;
        }

        // GL 材质
        _glMat = new Material(Shader.Find("Hidden/Internal-Colored"));
        _glMat.hideFlags = HideFlags.HideAndDontSave;
        _glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glMat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        _glMat.SetInt("_ZWrite",   0);
        _glMat.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);

        // 全屏四边形 Mesh
        _quadMesh = CreateFullscreenQuad();

        // 自动查找引用
        if (mainCamera == null)
            mainCamera = Camera.main;
        if (glRenderer == null)
            glRenderer = GetComponent<HopeGLRenderer>();
        if (glRenderer == null)
            glRenderer = FindObjectOfType<HopeGLRenderer>();

        BuildRT();
        AttachCommandBuffer();
    }

    void OnDestroy()
    {
        DetachCommandBuffer();
        if (_backgroundRT != null) _backgroundRT.Release();
        if (_glMat != null) Destroy(_glMat);
        if (Instance == this) Instance = null;
    }

    void OnEnable()
    {
        if (!_cmdAttached) AttachCommandBuffer();
    }

    void OnDisable()
    {
        DetachCommandBuffer();
    }

    // ══════════════════════════════════════════════════
    // RenderTexture + CommandBuffer
    // ══════════════════════════════════════════════════

    void BuildRT()
    {
        if (_backgroundRT != null) _backgroundRT.Release();
        int w = Mathf.Max(1, Screen.width);
        int h = Mathf.Max(1, Screen.height);
        _backgroundRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        _backgroundRT.filterMode = FilterMode.Bilinear;
        _backgroundRT.Create();
    }

    void AttachCommandBuffer()
    {
        if (mainCamera == null || _cmdAttached) return;

        _cmdBuffer = new CommandBuffer { name = "WaterBubble_GrabBackground" };
        // AfterForwardOpaque：在不透明物体渲染完后抓屏，此时背景已经画好
        _cmdBuffer.Blit(BuiltinRenderTextureType.CurrentActive, _backgroundRT);
        mainCamera.AddCommandBuffer(CameraEvent.AfterForwardOpaque, _cmdBuffer);
        _cmdAttached = true;
    }

    void DetachCommandBuffer()
    {
        if (mainCamera != null && _cmdBuffer != null && _cmdAttached)
        {
            mainCamera.RemoveCommandBuffer(CameraEvent.AfterForwardOpaque, _cmdBuffer);
        }
        _cmdBuffer?.Dispose();
        _cmdBuffer   = null;
        _cmdAttached = false;
    }

    // 分辨率变化时重建 RT
    void LateUpdate()
    {
        if (_backgroundRT == null ||
            _backgroundRT.width  != Screen.width ||
            _backgroundRT.height != Screen.height)
        {
            DetachCommandBuffer();
            BuildRT();
            AttachCommandBuffer();
        }
    }

    // ══════════════════════════════════════════════════
    // 公开接口
    // ══════════════════════════════════════════════════

    /// <summary>在随机位置生成一个气泡。</summary>
    public void SpawnBubble()
    {
        float sw = Screen.width;
        float sh = Screen.height;
        float cx = sw * (0.25f + Random.Range(0f, 0.5f));
        float cy = sh * (0.25f + Random.Range(0f, 0.5f));
        SpawnBubbleAt(cx, cy);
    }

    /// <summary>在指定屏幕像素坐标生成气泡。</summary>
    public void SpawnBubbleAt(float cx, float cy)
    {
        float sY  = Screen.height / 360f;
        _bubbles.Add(new Bubble
        {
            cx       = cx,
            cy       = cy,
            elapsed  = 0f,
            growDur  = growDuration,
            burstDur = burstDuration,
            burstTriggered = false,
            phase    = Bubble.Phase.Grow,
        });
    }

    public void ClearAll()
    {
        _bubbles.Clear();
    }

    // ══════════════════════════════════════════════════
    // Update：物理 + 渲染
    // ══════════════════════════════════════════════════

    void Update()
    {
        float dt = Time.deltaTime;
        float sw = Screen.width;
        float sh = Screen.height;
        float sY = sh / 360f;

        for (int i = _bubbles.Count - 1; i >= 0; i--)
        {
            var b = _bubbles[i];

            if (b.phase == Bubble.Phase.Grow)
            {
                b.elapsed += dt;
                if (b.elapsed >= b.growDur)
                {
                    b.phase   = Bubble.Phase.Burst;
                    b.elapsed = 0f;
                    TriggerBurst(b, sY);
                }
            }
            else if (b.phase == Bubble.Phase.Burst)
            {
                b.elapsed += dt;
                float bp = b.elapsed / b.burstDur;

                // 更新碎片
                float acc = 1f + bp * 3f;
                for (int si = b.shards.Count - 1; si >= 0; si--)
                {
                    var sh2 = b.shards[si];
                    sh2.ax    += sh2.vx * acc * dt;
                    sh2.ay    += sh2.vy * acc * dt;
                    sh2.alpha  = Mathf.Pow(1f - bp, 1.5f);
                    if (sh2.alpha < 0.01f) b.shards.RemoveAt(si);
                }

                if (b.elapsed >= b.burstDur)
                {
                    b.phase = Bubble.Phase.Done;
                    _bubbles.RemoveAt(i);
                }
            }
        }
    }

    void TriggerBurst(Bubble b, float sY)
    {
        // 28个碎片（对应 HTML shards）
        for (int si = 0; si < 28; si++)
        {
            float angle = si / 28f * Mathf.PI * 2f + (Random.value - 0.5f) * 0.2f;
            float spd   = (2.5f + Random.Range(0f, 6f)) * sY * 60f;
            b.shards.Add(new BurstShard
            {
                vx    = Mathf.Cos(angle) * spd,
                vy    = Mathf.Sin(angle) * spd,
                ax    = 0f, ay = 0f,
                size  = (1.5f + Random.Range(0f, 3f)) * sY,
                alpha = 1f,
            });
        }

        // 4组波纹（对应 HTML setTimeout i*60ms）
        if (glRenderer != null)
        {
            for (int ri = 0; ri < 4; ri++)
            {
                int   idx   = ri;
                float delay = ri * 0.06f;
                glRenderer.SpawnRippleSet(b.cx, b.cy,
                    6,
                    2.2f + idx * 0.3f,
                    280f  + idx * 20f,
                    0.85f - idx * 0.1f,
                    extraDelay: delay);
            }
        }
    }

    // ══════════════════════════════════════════════════
    // 渲染
    // ══════════════════════════════════════════════════

    void OnRenderObject()
    {
        if (_bubbles.Count == 0) return;
        if (bubbleMaterial == null || _backgroundRT == null) return;

        float sw = Screen.width;
        float sh = Screen.height;

        // ── 1. 气泡本体（Shader 渲染）──────────────────
        foreach (var b in _bubbles)
        {
            if (b.phase != Bubble.Phase.Grow) continue;

            float p = b.elapsed / b.growDur;

            // HTML ease
            float ease;
            if (p < 0.7f)
                ease = p / 0.7f * 0.9f;
            else
            {
                float sub = (p - 0.7f) / 0.3f;
                ease = 0.9f + Mathf.Sin(sub * Mathf.PI * 4f) * 0.06f * (1f - sub);
            }

            float tension = Mathf.Max(0f, (p - 0.6f) / 0.4f);
            float rPx     = ease * bubbleMaxRadiusRatio * sh; // 像素半径
            float rUV     = rPx / sh;                         // UV半径（以高度归一化）
            float aspect  = sw / sh;

            // 传参给 Shader
            bubbleMaterial.SetTexture("_BackgroundTex", _backgroundRT);
            bubbleMaterial.SetColor("_Color",           hopeColor);
            bubbleMaterial.SetVector("_CenterUV",       new Vector4(b.cx / sw, 1f - b.cy / sh, 0, 0));
            bubbleMaterial.SetFloat("_Radius",          rUV);
            bubbleMaterial.SetFloat("_GrowPhase",       ease);
            bubbleMaterial.SetFloat("_Tension",         tension);
            bubbleMaterial.SetFloat("_RefractStrength", 0.06f);
            bubbleMaterial.SetFloat("_FresnelPow",      2.5f);
            bubbleMaterial.SetFloat("_Aspect",          aspect);

            // 用全屏四边形绘制（Shader 内部 clip 圆外）
            bubbleMaterial.SetPass(0);
            Graphics.DrawMeshNow(_quadMesh, Matrix4x4.identity);
        }

        // ── 2. 破裂阶段：白闪 + 碎片（GL）─────────────
        bool hasBurst = false;
        foreach (var b in _bubbles)
            if (b.phase == Bubble.Phase.Burst) { hasBurst = true; break; }

        if (!hasBurst) return;

        _glMat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, sw, sh, 0);

        foreach (var b in _bubbles)
        {
            if (b.phase != Bubble.Phase.Burst) continue;

            float bp = b.elapsed / b.burstDur;

            // 白色闪光全屏（HTML: fillRect white (1-bp)*0.85）
            float flashA = (1f - bp) * 0.85f;
            if (flashA > 0.01f)
            {
                GL.Begin(GL.QUADS);
                GL.Color(new Color(1f, 1f, 1f, flashA));
                GL.Vertex3(0,  0,  0);
                GL.Vertex3(sw, 0,  0);
                GL.Vertex3(sw, sh, 0);
                GL.Vertex3(0,  sh, 0);
                GL.End();
            }

            // 青色叠加（HTML: fillRect teal (1-bp)*0.28）
            float tealA = (1f - bp) * 0.28f;
            if (tealA > 0.01f)
            {
                GL.Begin(GL.QUADS);
                GL.Color(new Color(hopeColor.r, hopeColor.g, hopeColor.b, tealA));
                GL.Vertex3(0,  0,  0);
                GL.Vertex3(sw, 0,  0);
                GL.Vertex3(sw, sh, 0);
                GL.Vertex3(0,  sh, 0);
                GL.End();
            }

            // 碎片（小水珠，用实心圆近似）
            GL.Begin(GL.TRIANGLES);
            foreach (var sh2 in b.shards)
            {
                if (sh2.alpha < 0.01f) continue;
                float px = b.cx + sh2.ax;
                float py = b.cy + sh2.ay;
                float r2 = sh2.size * (1f - bp * 0.4f);
                Color c  = new Color(hopeColor.r, hopeColor.g, hopeColor.b, sh2.alpha * 0.9f);
                GL.Color(c);
                for (int si = 0; si < CIRCLE_SEGS; si++)
                {
                    GL.Vertex3(px, py, 0);
                    GL.Vertex3(px + _circleDir[si].x * r2,     py + _circleDir[si].y * r2,     0);
                    GL.Vertex3(px + _circleDir[si + 1].x * r2, py + _circleDir[si + 1].y * r2, 0);
                }
            }
            GL.End();
        }

        GL.PopMatrix();
    }

    // ══════════════════════════════════════════════════
    // 工具
    // ══════════════════════════════════════════════════

    static Mesh CreateFullscreenQuad()
    {
        var mesh = new Mesh { name = "FullscreenQuad" };
        mesh.vertices = new Vector3[]
        {
            new Vector3(-1, -1, 0),
            new Vector3(-1,  1, 0),
            new Vector3( 1,  1, 0),
            new Vector3( 1, -1, 0),
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, 1),
            new Vector2(1, 1),
            new Vector2(1, 0),
        };
        mesh.triangles = new int[] { 0, 1, 2, 0, 2, 3 };
        mesh.UploadMeshData(true);
        return mesh;
    }
}
