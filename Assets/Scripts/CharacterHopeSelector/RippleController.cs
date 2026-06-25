using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 复刻 HTML canvas 涟漪：描边不规则椭圆圈 + 内圈细线 + 心跳节奏 + 线性淡出。
/// 用 RawImage + Texture2D，每帧清空重绘（与 HTML canvas 一致）。
/// </summary>
public class RippleController : MonoBehaviour
{
    [Header("颜色")]
    public Color rippleColor = new Color(0.12f, 0.56f, 1f, 1f);

    [Header("主圈")]
    public float mainLineWidth  = 5f;
    [Range(0f,1f)] public float mainAlpha = 0.35f;

    [Header("内圈")]
    public float innerLineWidth  = 2f;
    [Range(0f,1f)] public float innerAlpha = 0.14f;
    public float innerOffset     = 12f;

    [Header("形状")]
    [Range(0.3f, 1.5f)] public float ellipseRatio = 0.65f;
    [Range(24, 128)]     public int   polyPts      = 48;

    [Header("心跳间隔（秒）")]
    public float beatInterval = 2.2f;

    [Header("纹理分辨率")]
    public int texW = 1024;
    public int texH = 576;

    RawImage        _raw;
    Texture2D       _tex;
    Color[]         _buf;
    List<Ripple>    _ripples = new List<Ripple>();
    float           _lastBeat;

    struct Ripple
    {
        public Vector2 c;   // center
        public float   r;
        public float   maxR;
        public float   speed; // px/s
    }

    void Awake()
    {
        _raw = GetComponent<RawImage>();
        if (_raw == null) { _raw = gameObject.AddComponent<RawImage>(); _raw.raycastTarget = false; }

        _tex = new Texture2D(texW, texH, TextureFormat.RGBA32, false);
        _tex.filterMode = FilterMode.Bilinear;
        _tex.wrapMode   = TextureWrapMode.Clamp;
        _raw.texture    = _tex;

        _buf = new Color[texW * texH];
    }

    public void StartBeating()
    {
        StopAllCoroutines();
        _lastBeat = Time.time;
        SpawnRipple();
        StartCoroutine(BeatLoop());
    }

    public void StopBeating() => StopAllCoroutines();

    public void SpawnBurst()
    {
        SpawnRipple();
        Invoke(nameof(SpawnRipple), 0.08f);
        Invoke(nameof(SpawnRipple), 0.18f);
    }

    IEnumerator BeatLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(beatInterval);
            SpawnRipple();
        }
    }

    void SpawnRipple()
    {
        float cx = texW * 0.5f + Random.Range(-60f, 60f);
        float cy = texH * 0.5f + Random.Range(-40f, 40f);
        float maxR = texW * 0.55f;

        // HTML speed: 2.2~3.4 px/frame → 换算成 px/s（按60fps）
        float speed = Random.Range(132f, 204f);

        _ripples.Add(new Ripple { c = new Vector2(cx, cy), r = 0f, maxR = maxR, speed = speed });
    }

    void Update()
    {
        float dt = Time.deltaTime;
        for (int i = _ripples.Count - 1; i >= 0; i--)
        {
            var rp = _ripples[i];
            rp.r += rp.speed * dt;
            if (rp.r >= rp.maxR) _ripples.RemoveAt(i);
            else _ripples[i] = rp;
        }
        Draw();
    }

    void Draw()
    {
        for (int i = 0; i < _buf.Length; i++) _buf[i] = Color.clear;

        for (int ri = 0; ri < _ripples.Count; ri++)
        {
            var rp = _ripples[ri];
            float life = 1f - Mathf.Clamp01(rp.r / rp.maxR);
            if (life <= 0f) continue;

            // ── 主圈描边（不规则多边形 + 椭圆畸变）──
            float ma = mainAlpha * life;
            Color mainCol = new Color(rippleColor.r, rippleColor.g, rippleColor.b, ma);
            DrawRippleRing(rp.c, rp.r, rp.maxR, mainCol, mainLineWidth, true);

            // ── 内圈细线（r - 12px 处）──
            if (rp.r > 18f)
            {
                float ia = innerAlpha * life;
                Color innerCol = new Color(rippleColor.r, rippleColor.g, rippleColor.b, ia);
                DrawRippleRing(rp.c, rp.r - innerOffset, rp.maxR, innerCol, innerLineWidth, false);
            }
        }

        _tex.SetPixels(_buf);
        _tex.Apply();
    }

    void DrawRippleRing(Vector2 c, float r, float maxR, Color col, float lineWidth, bool useNoise)
    {
        if (r <= 0f) return;

        int pts = polyPts;
        Vector2[] poly = new Vector2[pts];
        float step = Mathf.PI * 2f / pts;

        for (int k = 0; k < pts; k++)
        {
            float angle = k * step;
            float rNoise = r;
            if (useNoise)
            {
                rNoise *= 1f + 0.018f * Mathf.Sin(angle * 7f + r * 0.04f)
                              + 0.012f * Mathf.Cos(angle * 13f + r * 0.03f);
            }
            poly[k] = new Vector2(
                c.x + rNoise * Mathf.Cos(angle),
                c.y + rNoise * ellipseRatio * Mathf.Sin(angle));
        }

        // 对每条边画粗线（模拟 lineWidth）
        for (int k = 0; k < pts; k++)
        {
            int n = (k + 1) % pts;
            DrawThickLine(poly[k], poly[n], lineWidth, col);
        }
    }

    void DrawThickLine(Vector2 a, Vector2 b, float w, Color col)
    {
        Vector2 dir = (b - a).normalized;
        Vector2 nrm = new Vector2(-dir.y, dir.x) * w * 0.5f;

        // 四个角形成粗线段
        Vector2 p0 = a - nrm, p1 = a + nrm, p2 = b - nrm, p3 = b + nrm;
        Vector2[] quad = { p0, p2, p3, p1 };
        FillQuad(quad, col);
    }

    void FillQuad(Vector2[] q, Color col)
    {
        float minX = Mathf.Min(q[0].x, q[1].x, q[2].x, q[3].x);
        float maxX = Mathf.Max(q[0].x, q[1].x, q[2].x, q[3].x);
        float minY = Mathf.Min(q[0].y, q[1].y, q[2].y, q[3].y);
        float maxY = Mathf.Max(q[0].y, q[1].y, q[2].y, q[3].y);

        int x0 = Mathf.Clamp(Mathf.FloorToInt(minX), 0, texW - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(maxX),  0, texW - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(minY), 0, texH - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(maxY),  0, texH - 1);

        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (PointInQuad(new Vector2(x + 0.5f, y + 0.5f), q))
                {
                    int idx = y * texW + x;
                    Color bg = _buf[idx];
                    // 预乘 alpha 混合
                    float a = col.a;
                    float invA = 1f - a;
                    _buf[idx] = new Color(
                        bg.r * invA + col.r * a,
                        bg.g * invA + col.g * a,
                        bg.b * invA + col.b * a,
                        bg.a + a * (1f - bg.a));
                }
            }
        }
    }

    bool PointInQuad(Vector2 p, Vector2[] q)
    {
        bool inside = false;
        for (int i = 0, j = 3; i < 4; j = i++)
        {
            if ((q[i].y > p.y) != (q[j].y > p.y) &&
                p.x < (q[j].x - q[i].x) * (p.y - q[i].y) / (q[j].y - q[i].y) + q[i].x)
                inside = !inside;
        }
        return inside;
    }

    void OnDestroy()
    {
        StopBeating();
        if (_tex != null) Destroy(_tex);
    }
}
