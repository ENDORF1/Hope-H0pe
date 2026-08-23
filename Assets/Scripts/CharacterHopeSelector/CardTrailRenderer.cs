using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 挂在选角场景的 Camera 上，在黑屏之上绘制卡牌飞出留下的「尾波」。
///
/// 这是救世（希望）阵营的语言：路径被扰动的浪、留在水面的涟漪、上浮的碎光、
/// 偶发的分叉。熄忘阵营需要一套收敛 / 抹平 / 沉默的语言，不要直接复用本效果。
/// 详见 Docs/世界观.md。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CardTrailRenderer : MonoBehaviour
{
    public static CardTrailRenderer Instance { get; private set; }

    // ─────────────────────────────────────────────────
    // 数据
    // ─────────────────────────────────────────────────

    public class Trail
    {
        public RectTransform follow;
        public Color color;
        public float scale = 1f;

        public bool emitting = true;
        public float alpha = 1f;
        public float fadeSpeed;

        public float elapsed;
        public float travelled;
        public float seed;

        public bool hasRaw;
        public Vector2 lastRaw;
        public float sinceRipple;
        public float sinceMote;
        public float sinceBranch;

        public readonly List<Vector2> path = new List<Vector2>(256);
        public readonly List<Ripple> ripples = new List<Ripple>(24);
        public readonly List<Mote> motes = new List<Mote>(160);
        public readonly List<Branch> branches = new List<Branch>(8);
    }

    public class Ripple
    {
        public Vector2 center;
        public float radius, maxRadius, rotation, wobble, age, life;
    }

    public class Mote
    {
        public Vector2 pos, velocity;
        public float size, age, life, spin;
    }

    public class Branch
    {
        public readonly List<Vector2> points = new List<Vector2>(16);
        public float age, life;
    }

    // ─────────────────────────────────────────────────
    // 参数
    // ─────────────────────────────────────────────────

    [Header("路径扰动（最小输入 → 可见偏差）")]
    [Tooltip("相邻路径点的像素间距，越小越平滑")]
    [SerializeField] private float pathSampleDistance = 4f;

    [Tooltip("路径点上限；尾端本身已淡到透明，丢弃看不出来")]
    [SerializeField] private int maxPathPoints = 260;

    [Tooltip("横向摆动的最大幅度（像素）")]
    [SerializeField] private float wanderAmplitude = 42f;

    [Tooltip("摆动幅度从 0 长到最大所需的飞行距离（像素）")]
    [SerializeField] private float wanderRampDistance = 420f;

    [Header("浪（多股细线，不要合成一条实带）")]
    [SerializeField] private int strandCount = 3;
    [Tooltip("各股之间的横向张开幅度（像素）")]
    [SerializeField] private float strandSpread = 14f;
    [Tooltip("单股线宽（像素）")]
    [SerializeField] private float strandWidth = 2.6f;
    [Range(0f, 1f)][SerializeField] private float strandAlpha = 0.55f;

    [Header("涟漪（沿途留在水面上）")]
    [Tooltip("每飞过多少像素留下一圈")]
    [SerializeField] private float rippleSpacing = 72f;
    [SerializeField] private float rippleMaxRadius = 104f;
    [SerializeField] private float rippleLife = 0.62f;
    [SerializeField] private float rippleWidth = 2.6f;
    [Range(0f, 1f)][SerializeField] private float rippleAlpha = 0.5f;

    [Header("碎光（像气泡上浮）")]
    [Tooltip("每飞过多少像素撒一粒")]
    [SerializeField] private float moteSpacing = 10f;
    [SerializeField] private Vector2 moteSizeRange = new Vector2(2f, 7f);
    [SerializeField] private Vector2 moteLifeRange = new Vector2(0.35f, 0.95f);
    [Tooltip("碎光相对路径的横向散布（像素）")]
    [SerializeField] private float moteScatter = 30f;
    [Tooltip("碎光上浮速度（像素/秒）")]
    [SerializeField] private float moteDrift = 52f;
    [Range(0f, 1f)][SerializeField] private float moteAlpha = 0.9f;

    [Header("分叉（同一祈愿裂出的别的可能）")]
    [Tooltip("每飞过多少像素尝试分叉一次；0 = 不分叉")]
    [SerializeField] private float branchSpacing = 260f;
    [SerializeField] private float branchLife = 0.34f;

    [Header("淡出")]
    [SerializeField] private float defaultFadeDuration = 0.5f;

    // ─────────────────────────────────────────────────

    private readonly List<Trail> _trails = new List<Trail>();
    private readonly List<Vector2> _strandBuf = new List<Vector2>(256);
    private Camera _cam;
    private Material _glMat;

    void Awake()
    {
        Instance = this;
        _cam = GetComponent<Camera>();

        Shader s = Shader.Find("Hidden/Internal-Colored");
        if (s == null) s = Shader.Find("Sprites/Default");
        _glMat = new Material(s);
        _glMat.hideFlags = HideFlags.HideAndDontSave;
        _glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
        _glMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _glMat.SetInt("_ZWrite", 0);
        _glMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_glMat != null) Destroy(_glMat);
    }

    // ─────────────────────────────────────────────────
    // 对外接口
    // ─────────────────────────────────────────────────

    public Trail CreateTrail(RectTransform follow, Color color, float scale = 1f)
    {
        var t = new Trail
        {
            follow = follow,
            color = color,
            scale = scale <= 0f ? 1f : scale,
            seed = Random.Range(0f, 100f)
        };
        _trails.Add(t);
        return t;
    }

    /// <summary>停止发射，让已有的浪、涟漪和碎光一起淡出。</summary>
    public void FadeOut(Trail t, float duration = -1f)
    {
        if (t == null) return;
        t.emitting = false;
        float d = duration > 0f ? duration : defaultFadeDuration;
        t.fadeSpeed = d > 0.0001f ? 1f / d : 1000f;
    }

    public void RemoveTrail(Trail t)
    {
        if (t == null) return;
        t.emitting = false;
        _trails.Remove(t);
    }

    // ─────────────────────────────────────────────────
    // 更新
    // ─────────────────────────────────────────────────

    void LateUpdate()
    {
        float dt = Time.deltaTime;

        for (int i = _trails.Count - 1; i >= 0; i--)
        {
            Trail t = _trails[i];
            t.elapsed += dt;

            if (t.emitting && t.follow != null)
                Emit(t, dt);

            AgeRipples(t, dt);
            AgeMotes(t, dt);
            AgeBranches(t, dt);

            if (t.fadeSpeed > 0f)
            {
                t.alpha -= t.fadeSpeed * dt;
                if (t.alpha <= 0f)
                    _trails.RemoveAt(i);
            }
        }
    }

    void Emit(Trail t, float dt)
    {
        Vector2 raw = WorldToPixel(t.follow.TransformPoint(t.follow.rect.center));

        if (!t.hasRaw)
        {
            t.hasRaw = true;
            t.lastRaw = raw;
            t.path.Add(raw);
            return;
        }

        Vector2 delta = raw - t.lastRaw;
        float dist = delta.magnitude;
        if (dist < 0.01f) return;

        Vector2 dir = delta / dist;
        Vector2 perp = new Vector2(-dir.y, dir.x);
        float startTravelled = t.travelled;

        int steps = Mathf.Clamp(Mathf.CeilToInt(dist / pathSampleDistance), 1, 24);
        for (int s = 1; s <= steps; s++)
        {
            float f = (float)s / steps;
            float travelled = startTravelled + dist * f;
            Vector2 basePos = Vector2.Lerp(t.lastRaw, raw, f);
            Vector2 point = basePos + perp * Wander(t, travelled);

            t.path.Add(point);

            float segment = dist / steps;
            t.sinceRipple += segment;
            t.sinceMote += segment;
            t.sinceBranch += segment;

            if (rippleSpacing > 0f && t.sinceRipple >= rippleSpacing)
            {
                t.sinceRipple = 0f;
                SpawnRipple(t, point, dir);
            }

            if (moteSpacing > 0f && t.sinceMote >= moteSpacing)
            {
                t.sinceMote = 0f;
                SpawnMote(t, point, perp);
            }

            if (branchSpacing > 0f && t.sinceBranch >= branchSpacing)
            {
                t.sinceBranch = 0f;
                SpawnBranch(t, point, dir);
            }
        }

        t.travelled = startTravelled + dist;
        t.lastRaw = raw;

        int overflow = t.path.Count - maxPathPoints;
        if (overflow > 0)
            t.path.RemoveRange(0, overflow);
    }

    /// <summary>三层错相位正弦叠加：飞得越远，扰动越明显。</summary>
    float Wander(Trail t, float travelled)
    {
        float ramp = Mathf.Clamp01(travelled / Mathf.Max(1f, wanderRampDistance));
        float time = t.elapsed;
        float seed = t.seed;

        float w = Mathf.Sin(travelled * 0.013f + time * 2.9f + seed)
                + Mathf.Sin(travelled * 0.032f - time * 1.7f + seed * 2.3f) * 0.55f
                + Mathf.Sin(travelled * 0.006f + time * 0.8f + seed * 4.1f) * 0.85f;

        return w / 2.4f * wanderAmplitude * t.scale * ramp;
    }

    void SpawnRipple(Trail t, Vector2 pos, Vector2 dir)
    {
        t.ripples.Add(new Ripple
        {
            center = pos,
            radius = rippleMaxRadius * t.scale * 0.12f,
            maxRadius = rippleMaxRadius * t.scale * Random.Range(0.7f, 1.25f),
            rotation = Mathf.Atan2(dir.y, dir.x),
            wobble = Random.Range(0f, Mathf.PI * 2f),
            life = rippleLife * Random.Range(0.8f, 1.2f)
        });
    }

    void SpawnMote(Trail t, Vector2 pos, Vector2 perp)
    {
        float lateral = Random.Range(-1f, 1f);
        Vector2 spawn = pos + perp * lateral * moteScatter * t.scale;

        Vector2 vel = Vector2.up * moteDrift * Random.Range(0.35f, 1f)
                    + perp * lateral * moteDrift * Random.Range(0.1f, 0.5f);

        t.motes.Add(new Mote
        {
            pos = spawn,
            velocity = vel,
            size = Random.Range(moteSizeRange.x, moteSizeRange.y) * t.scale,
            life = Random.Range(moteLifeRange.x, moteLifeRange.y),
            spin = Random.Range(0f, Mathf.PI * 2f)
        });
    }

    void SpawnBranch(Trail t, Vector2 pos, Vector2 dir)
    {
        var b = new Branch { life = branchLife * Random.Range(0.8f, 1.3f) };

        float side = Random.value < 0.5f ? -1f : 1f;
        float angle = Random.Range(22f, 52f) * Mathf.Deg2Rad * side;
        float curve = Random.Range(3f, 9f) * Mathf.Deg2Rad * side;
        float step = Random.Range(9f, 16f) * t.scale;

        Vector2 p = pos;
        Vector2 d = Rotate(dir, angle);
        int count = Random.Range(5, 10);

        b.points.Add(p);
        for (int i = 0; i < count; i++)
        {
            d = Rotate(d, curve);
            p += d * step;
            b.points.Add(p);
        }

        t.branches.Add(b);
    }

    void AgeRipples(Trail t, float dt)
    {
        for (int i = t.ripples.Count - 1; i >= 0; i--)
        {
            Ripple r = t.ripples[i];
            r.age += dt;
            if (r.age >= r.life) { t.ripples.RemoveAt(i); continue; }

            float p = r.age / r.life;
            r.radius = Mathf.Lerp(r.maxRadius * 0.12f, r.maxRadius, p);
            r.wobble += dt * 2.2f;
        }
    }

    void AgeMotes(Trail t, float dt)
    {
        for (int i = t.motes.Count - 1; i >= 0; i--)
        {
            Mote m = t.motes[i];
            m.age += dt;
            if (m.age >= m.life) { t.motes.RemoveAt(i); continue; }

            m.pos += m.velocity * dt;
            m.velocity *= 1f - 1.1f * dt;
            m.spin += dt * 3f;
        }
    }

    void AgeBranches(Trail t, float dt)
    {
        for (int i = t.branches.Count - 1; i >= 0; i--)
        {
            Branch b = t.branches[i];
            b.age += dt;
            if (b.age >= b.life) t.branches.RemoveAt(i);
        }
    }

    Vector2 WorldToPixel(Vector3 world)
    {
        Vector3 sp = _cam.WorldToScreenPoint(world);
        Rect r = _cam.pixelRect;
        return new Vector2(sp.x - r.x, sp.y - r.y);
    }

    static Vector2 Rotate(Vector2 v, float rad)
    {
        float c = Mathf.Cos(rad), s = Mathf.Sin(rad);
        return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
    }

    // ─────────────────────────────────────────────────
    // 绘制
    // ─────────────────────────────────────────────────

    void OnPostRender()
    {
        if (_glMat == null || _trails.Count == 0) return;

        _glMat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0f, _cam.pixelWidth, 0f, _cam.pixelHeight);

        for (int i = 0; i < _trails.Count; i++)
        {
            Trail t = _trails[i];
            if (t.alpha <= 0f) continue;

            float a = Mathf.Clamp01(t.alpha) * t.color.a;
            Color c = t.color;

            DrawRipples(t, c, a);
            DrawStrands(t, c, a);
            DrawBranches(t, c, a);
            DrawMotes(t, c, a);
        }

        GL.PopMatrix();
    }

    void DrawStrands(Trail t, Color c, float alpha)
    {
        int n = t.path.Count;
        if (n < 4) return;

        int strands = Mathf.Max(1, strandCount);
        for (int k = 0; k < strands; k++)
        {
            float lerp = strands == 1 ? 0f : (float)k / (strands - 1);
            float amp = strandSpread * t.scale * Mathf.Lerp(0.25f, 1f, lerp) * (k % 2 == 0 ? 1f : -1f);
            float phase = t.seed * 3.7f + k * 2.399f;
            float width = strandWidth * t.scale * Mathf.Lerp(1f, 0.55f, lerp);
            float a = alpha * strandAlpha * Mathf.Lerp(1f, 0.45f, lerp);

            _strandBuf.Clear();
            for (int i = 0; i < n; i++)
            {
                Vector2 tangent = Tangent(t.path, i);
                Vector2 perp = new Vector2(-tangent.y, tangent.x);
                float ride = Mathf.Sin(i * 0.17f + phase + t.elapsed * 2.4f);
                _strandBuf.Add(t.path[i] + perp * ride * amp);
            }

            DrawTaperedBand(_strandBuf, width, c, a);
        }
    }

    void DrawTaperedBand(List<Vector2> pts, float width, Color color, float alpha)
    {
        int n = pts.Count;
        if (n < 2) return;

        GL.Begin(GL.TRIANGLE_STRIP);
        for (int i = 0; i < n; i++)
        {
            float along = (float)i / (n - 1);
            float half = width * Mathf.Lerp(0.25f, 1f, along) * 0.5f;
            float a = alpha * Mathf.Pow(along, 0.85f);

            Vector2 tangent = Tangent(pts, i);
            Vector2 nrm = new Vector2(-tangent.y, tangent.x);
            Vector2 p = pts[i];

            GL.Color(new Color(color.r, color.g, color.b, a));
            GL.Vertex3(p.x + nrm.x * half, p.y + nrm.y * half, 0f);
            GL.Vertex3(p.x - nrm.x * half, p.y - nrm.y * half, 0f);
        }
        GL.End();
    }

    void DrawBranches(Trail t, Color c, float alpha)
    {
        for (int i = 0; i < t.branches.Count; i++)
        {
            Branch b = t.branches[i];
            float p = Mathf.Clamp01(b.age / b.life);
            float a = alpha * strandAlpha * 0.55f * (1f - p);
            if (a <= 0.001f) continue;

            DrawTaperedBand(b.points, strandWidth * t.scale * 0.7f, c, a);
        }
    }

    void DrawRipples(Trail t, Color c, float alpha)
    {
        for (int i = 0; i < t.ripples.Count; i++)
        {
            Ripple r = t.ripples[i];
            float p = Mathf.Clamp01(r.age / r.life);
            float a = alpha * rippleAlpha * Mathf.Sin(Mathf.PI * p);
            if (a <= 0.001f) continue;

            DrawIrregularRing(r, rippleWidth * t.scale, c, a);
        }
    }

    /// <summary>不规则椭圆描边圈，长轴垂直于飞行方向，像被推开的水面。</summary>
    void DrawIrregularRing(Ripple r, float width, Color color, float alpha)
    {
        const int segments = 30;
        float rx = r.radius * 0.42f;
        float ry = r.radius;
        float half = width * 0.5f;

        GL.Begin(GL.TRIANGLE_STRIP);
        for (int i = 0; i <= segments; i++)
        {
            float ang = (i / (float)segments) * Mathf.PI * 2f;
            float jitter = 1f
                + 0.11f * Mathf.Sin(ang * 3f + r.wobble)
                + 0.06f * Mathf.Sin(ang * 7f - r.wobble * 1.6f);

            Vector2 outward = new Vector2(Mathf.Cos(ang) * rx, Mathf.Sin(ang) * ry) * jitter;
            Vector2 radial = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector2.up;

            Vector2 inner = Rotate(outward - radial * half, r.rotation) + r.center;
            Vector2 outer = Rotate(outward + radial * half, r.rotation) + r.center;

            GL.Color(new Color(color.r, color.g, color.b, alpha));
            GL.Vertex3(inner.x, inner.y, 0f);
            GL.Vertex3(outer.x, outer.y, 0f);
        }
        GL.End();
    }

    void DrawMotes(Trail t, Color c, float alpha)
    {
        if (t.motes.Count == 0) return;

        GL.Begin(GL.QUADS);
        for (int i = 0; i < t.motes.Count; i++)
        {
            Mote m = t.motes[i];
            float p = Mathf.Clamp01(m.age / m.life);
            float a = alpha * moteAlpha * (1f - p) * (1f - p);
            if (a <= 0.001f) continue;

            float half = m.size * Mathf.Lerp(1f, 0.4f, p) * 0.5f;
            float flicker = 0.75f + 0.25f * Mathf.Sin(m.spin * 5f);

            GL.Color(new Color(c.r, c.g, c.b, a * flicker));
            GL.Vertex3(m.pos.x - half, m.pos.y - half, 0f);
            GL.Vertex3(m.pos.x + half, m.pos.y - half, 0f);
            GL.Vertex3(m.pos.x + half, m.pos.y + half, 0f);
            GL.Vertex3(m.pos.x - half, m.pos.y + half, 0f);
        }
        GL.End();
    }

    static Vector2 Tangent(List<Vector2> pts, int i)
    {
        Vector2 d;
        if (i == 0) d = pts[1] - pts[0];
        else if (i == pts.Count - 1) d = pts[i] - pts[i - 1];
        else d = pts[i + 1] - pts[i - 1];

        float mag = d.magnitude;
        return mag > 0.001f ? d / mag : Vector2.up;
    }
}
