using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 用 GL 即时模式绘制所有希望入侵的水系特效：
///   - 波纹圆环（对应 HTML drawRipples）
///   - 水滴椭圆 + 溅射弧线（对应 HTML Drop）
///
/// 挂在与 ScreenGlitchUI 相同的 GameObject 上。
/// ScreenGlitchUI 通过公开方法向本组件提交数据，本组件负责渲染。
///
/// 坐标系：内部全部使用像素坐标（左上角原点，向下为正 Y），
/// 与 HTML Canvas 一致，GL 绘制时自动换算到裁剪空间。
/// </summary>
public class HopeGLRenderer : MonoBehaviour
{
    // ── 材质（只需一个支持透明的纯色材质）──────────────
    private Material _mat;

    // ── 分辨率缓存 ────────────────────────────────────
    private float _sw, _sh; // Screen.width / height，每帧刷新

    // ══════════════════════════════════════════════════
    // 数据结构（与 ScreenGlitchUI 共享，通过引用操作）
    // ══════════════════════════════════════════════════

    // 波纹
    public class RippleWave
    {
        public float delay;
        public float radius;
        public bool  born;
        public float speed;   // 像素/秒
        public float maxR;    // 最大半径，像素
        public float thick;   // 线宽，像素
        public float peak;
        public float alpha;
        public float elapsed;
    }
    public class RippleSet
    {
        public float x, y;   // 像素坐标，左上原点
        public List<RippleWave> waves = new List<RippleWave>();
        public float elapsed;
    }

    // 水滴
    public class FallingDrop
    {
        public float x, y;
        public float vy;
        public float gravity;
        public float groundY;
        public float size;
        public bool  splashed;
    }

    // 溅射弧线
    public class SplashArc
    {
        public float cx, cy;
        public float vx, vy;
        public float grav;
        public float ax, ay;
        public float size;
        public float alpha;
        public float fadeSpeed;
    }


    // ── 公开列表，由 ScreenGlitchUI 操作 ─────────────
    public List<RippleSet>   RippleSets   = new List<RippleSet>();
    public List<FallingDrop> FallingDrops = new List<FallingDrop>();
    public List<SplashArc>   SplashArcs   = new List<SplashArc>();

    // ── 颜色（由 ScreenGlitchUI 设置）────────────────
    public Color HopeColor = new Color(0.24f, 0.91f, 0.78f, 1f);

    // ── 圆形分段数 ────────────────────────────────────
    private const int CIRCLE_SEGS = 64;

    // ── 预分配顶点缓存 ────────────────────────────────
    private static readonly Vector2[] _circleDir = new Vector2[CIRCLE_SEGS + 1];
    private static bool _circleDirReady = false;

    // ══════════════════════════════════════════════════
    // 初始化
    // ══════════════════════════════════════════════════

    void Awake()
    {
        // 预计算圆方向向量
        if (!_circleDirReady)
        {
            for (int i = 0; i <= CIRCLE_SEGS; i++)
            {
                float a = i / (float)CIRCLE_SEGS * Mathf.PI * 2f;
                _circleDir[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            _circleDirReady = true;
        }

        // 创建透明叠加材质
        _mat = new Material(Shader.Find("Hidden/Internal-Colored"));
        _mat.hideFlags = HideFlags.HideAndDontSave;
        _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _mat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        _mat.SetInt("_ZWrite",   0);
        _mat.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    void OnDestroy()
    {
        if (_mat != null) Destroy(_mat);
    }

    // ══════════════════════════════════════════════════
    // 每帧物理更新（在 ScreenGlitchUI.Update 里调用）
    // ══════════════════════════════════════════════════

    public void Tick(float dt)
    {
        _sw = Screen.width;
        _sh = Screen.height;

        UpdateRipples(dt);
        UpdateDrops(dt);
    }

    // ── 波纹更新（严格对应 HTML drawRipples）──────────

    void UpdateRipples(float dt)
    {
        for (int si = RippleSets.Count - 1; si >= 0; si--)
        {
            var set = RippleSets[si];
            set.elapsed += dt;
            bool anyAlive = false;

            foreach (var w in set.waves)
            {
                if (set.elapsed < w.delay) { anyAlive = true; continue; }
                if (!w.born) { w.born = true; w.radius = 0f; w.elapsed = 0f; }

                w.elapsed += dt;
                w.radius  += w.speed * dt;

                // HTML: alpha = p<0.08 ? peak*(p/0.08) : peak*pow(1-p,1.5)
                float p = w.radius / w.maxR;
                w.alpha = p < 0.08f
                    ? w.peak * (p / 0.08f)
                    : w.peak * Mathf.Pow(1f - p, 1.5f);

                if (w.radius < w.maxR && w.alpha > 0.005f)
                    anyAlive = true;
            }

            if (!anyAlive) RippleSets.RemoveAt(si);
        }
    }

    // ── 水滴更新（严格对应 HTML Drop fall/splash phase）

    void UpdateDrops(float dt)
    {
        for (int i = FallingDrops.Count - 1; i >= 0; i--)
        {
            var d = FallingDrops[i];
            if (d.splashed) { FallingDrops.RemoveAt(i); continue; }

            d.vy += d.gravity * dt;
            d.y  += d.vy * dt;

            if (d.y >= d.groundY)
            {
                d.splashed = true;

                // 触发波纹（HTML: spawnRipple(x,groundY,4,2.2,160,0.75)）
                SpawnRippleSet(d.x, d.groundY, 4, 2.2f, 160f, 0.75f);

                // 溅射弧线（HTML: 5+random*4 条）
                int arcCount = 5 + Random.Range(0, 4);
                float sY = _sh / 360f;
                float sX = _sw / 680f;
                for (int ai = 0; ai < arcCount; ai++)
                {
                    float angle = -Mathf.PI + Random.Range(0f, Mathf.PI);
                    float spd   = 1.5f + Random.Range(0f, 3f);
                    SplashArcs.Add(new SplashArc
                    {
                        cx = d.x, cy = d.groundY,
                        vx = Mathf.Cos(angle) * spd * sX * 60f,
                        vy = (Mathf.Sin(angle) * spd - 2f) * sY * 60f,
                        grav      = 0.18f * sY * 60f,
                        ax = 0f, ay = 0f,
                        size      = (1f + Random.Range(0f, 2f)) * sY,
                        alpha     = 1f,
                        fadeSpeed = 60f / 32f,
                    });
                }
                FallingDrops.RemoveAt(i);
            }
        }

        // 溅射弧线运动
        for (int i = SplashArcs.Count - 1; i >= 0; i--)
        {
            var arc = SplashArcs[i];
            arc.ax    += arc.vx * dt;
            arc.ay    += arc.vy * dt;
            arc.vy    += arc.grav * dt;
            arc.alpha -= arc.fadeSpeed * dt;
            if (arc.alpha <= 0f) SplashArcs.RemoveAt(i);
        }
    }

    // ══════════════════════════════════════════════════
    // 公开：生成波纹组（供外部调用）
    // 参数对应 HTML spawnRipple(x,y,rings,speed,maxR,peakAlpha)
    // x,y：像素坐标；speed：HTML px/帧；maxR：HTML px
    // ══════════════════════════════════════════════════

    public void SpawnRippleSet(float x, float y, int rings,
        float htmlSpeed, float htmlMaxR, float peakAlpha,
        float extraDelay = 0f)
    {
        float sY = _sh > 0 ? _sh / 360f : 1f;

        var set = new RippleSet { x = x, y = y, elapsed = 0f };
        for (int i = 0; i < rings; i++)
        {
            set.waves.Add(new RippleWave
            {
                // HTML: delay = i*14帧
                delay   = extraDelay + i * (14f / 60f),
                radius  = 0f,
                born    = false,
                // HTML: speed px/帧 → px/秒，含逐圈减速
                speed   = htmlSpeed * sY * 60f * (1f - i * 0.05f),
                maxR    = htmlMaxR * sY + i * 6f * sY,
                thick   = Mathf.Max(0.3f, 1.5f - i * 0.18f),
                peak    = peakAlpha - i * 0.08f,
                alpha   = 0f,
                elapsed = 0f,
            });
        }
        RippleSets.Add(set);
    }

    // ══════════════════════════════════════════════════
    // GL 渲染（OnRenderObject 在摄像机渲染完场景后调用）
    // ══════════════════════════════════════════════════

    void OnRenderObject()
    {
        if (_mat == null) return;
        _sw = Screen.width;
        _sh = Screen.height;

        _mat.SetPass(0);

        GL.PushMatrix();
        // 设置像素坐标矩阵：左上(0,0)，右下(sw,sh)
        GL.LoadPixelMatrix(0, _sw, _sh, 0);

        DrawRipples();
        DrawDrops();

        GL.PopMatrix();
    }

    // ── 绘制波纹（对应 HTML drawRipples）──────────────

    void DrawRipples()
    {
        foreach (var set in RippleSets)
        {
            foreach (var w in set.waves)
            {
                if (!w.born || w.alpha < 0.005f) continue;

                Color c = new Color(HopeColor.r, HopeColor.g, HopeColor.b, w.alpha);

                // 主环
                DrawCircleRing(set.x, set.y, w.radius, w.thick, c);

                // HTML 高光弧：dot(dn, (-0.6,0.8)) > 0.7，约对应
                // 圆弧顶部左侧约 30° 范围，alpha*0.5
                Color hc = new Color(1f, 1f, 1f, w.alpha * 0.5f);
                DrawArc(set.x, set.y, w.radius, w.thick * 0.4f,
                    Mathf.PI * 0.6f, Mathf.PI * 0.75f, hc);
            }
        }
    }

    // ── 绘制水滴和溅射（对应 HTML Drop）──────────────

    void DrawDrops()
    {
        float sY = _sh / 360f;

        // 下落中的水滴（拉伸椭圆）
        foreach (var d in FallingDrops)
        {
            if (d.splashed) continue;
            // HTML: stretch = min(2.5, 1 + vy * 0.08)
            // vy 此处是实际像素/秒，还原为 HTML px/帧单位做 stretch
            float vyFrame = d.vy / (sY * 60f);
            float stretch = Mathf.Min(2.5f, 1f + vyFrame * 0.08f);
            float rw = d.size / stretch;
            float rh = d.size * stretch;
            Color c = new Color(HopeColor.r, HopeColor.g, HopeColor.b, 0.9f);
            DrawEllipseFilled(d.x, d.y, rw, rh, c);
        }

        // 溅射小圆
        foreach (var arc in SplashArcs)
        {
            if (arc.alpha <= 0f) continue;
            Color c = new Color(HopeColor.r, HopeColor.g, HopeColor.b, arc.alpha * 0.9f);
            DrawCircleFilled(arc.cx + arc.ax, arc.cy + arc.ay, arc.size, c);
        }
    }

    // ══════════════════════════════════════════════════
    // GL 基元绘制工具
    // ══════════════════════════════════════════════════

    // 圆环（线段模拟，对应 HTML arc + strokeStyle）
    void DrawCircleRing(float cx, float cy, float r, float thick, Color col)
    {
        if (r <= 0f || thick <= 0f) return;
        float halfT = thick * 0.5f;
        float rIn   = Mathf.Max(0f, r - halfT);
        float rOut  = r + halfT;

        GL.Begin(GL.TRIANGLES);
        GL.Color(col);
        for (int i = 0; i < CIRCLE_SEGS; i++)
        {
            Vector2 d0 = _circleDir[i];
            Vector2 d1 = _circleDir[i + 1];

            float x0i = cx + d0.x * rIn,  y0i = cy + d0.y * rIn;
            float x0o = cx + d0.x * rOut, y0o = cy + d0.y * rOut;
            float x1i = cx + d1.x * rIn,  y1i = cy + d1.y * rIn;
            float x1o = cx + d1.x * rOut, y1o = cy + d1.y * rOut;

            GL.Vertex3(x0i, y0i, 0);
            GL.Vertex3(x0o, y0o, 0);
            GL.Vertex3(x1o, y1o, 0);

            GL.Vertex3(x0i, y0i, 0);
            GL.Vertex3(x1o, y1o, 0);
            GL.Vertex3(x1i, y1i, 0);
        }
        GL.End();
    }

    // 弧段（对应 HTML arc 局部段）
    void DrawArc(float cx, float cy, float r, float thick,
        float startAngle, float endAngle, Color col)
    {
        if (r <= 0f) return;
        float halfT = thick * 0.5f;
        float rIn   = Mathf.Max(0f, r - halfT);
        float rOut  = r + halfT;
        int segs    = Mathf.Max(4, Mathf.RoundToInt(CIRCLE_SEGS * (endAngle - startAngle) / (Mathf.PI * 2f)));

        GL.Begin(GL.TRIANGLES);
        GL.Color(col);
        for (int i = 0; i < segs; i++)
        {
            float a0 = Mathf.Lerp(startAngle, endAngle, i       / (float)segs);
            float a1 = Mathf.Lerp(startAngle, endAngle, (i + 1) / (float)segs);

            float x0i = cx + Mathf.Cos(a0) * rIn,  y0i = cy + Mathf.Sin(a0) * rIn;
            float x0o = cx + Mathf.Cos(a0) * rOut, y0o = cy + Mathf.Sin(a0) * rOut;
            float x1i = cx + Mathf.Cos(a1) * rIn,  y1i = cy + Mathf.Sin(a1) * rIn;
            float x1o = cx + Mathf.Cos(a1) * rOut, y1o = cy + Mathf.Sin(a1) * rOut;

            GL.Vertex3(x0i, y0i, 0); GL.Vertex3(x0o, y0o, 0); GL.Vertex3(x1o, y1o, 0);
            GL.Vertex3(x0i, y0i, 0); GL.Vertex3(x1o, y1o, 0); GL.Vertex3(x1i, y1i, 0);
        }
        GL.End();
    }

    // 实心圆（对应 HTML arc + fill）
    void DrawCircleFilled(float cx, float cy, float r, Color col)
    {
        if (r <= 0f) return;
        GL.Begin(GL.TRIANGLES);
        GL.Color(col);
        for (int i = 0; i < CIRCLE_SEGS; i++)
        {
            GL.Vertex3(cx, cy, 0);
            GL.Vertex3(cx + _circleDir[i].x * r,     cy + _circleDir[i].y * r,     0);
            GL.Vertex3(cx + _circleDir[i + 1].x * r, cy + _circleDir[i + 1].y * r, 0);
        }
        GL.End();
    }

    // 实心椭圆（对应 HTML ellipse + fill，用于拉伸水滴）
    void DrawEllipseFilled(float cx, float cy, float rw, float rh, Color col)
    {
        if (rw <= 0f || rh <= 0f) return;
        GL.Begin(GL.TRIANGLES);
        GL.Color(col);
        for (int i = 0; i < CIRCLE_SEGS; i++)
        {
            GL.Vertex3(cx, cy, 0);
            GL.Vertex3(cx + _circleDir[i].x * rw,     cy + _circleDir[i].y * rh,     0);
            GL.Vertex3(cx + _circleDir[i + 1].x * rw, cy + _circleDir[i + 1].y * rh, 0);
        }
        GL.End();
    }

    // ══════════════════════════════════════════════════
    // 外部调用：清空所有数据
    // ══════════════════════════════════════════════════

    public void ClearAll()
    {
        RippleSets.Clear();
        FallingDrops.Clear();
        SplashArcs.Clear();
    }
}