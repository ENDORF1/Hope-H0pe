using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 挂在选角场景的 Camera 上。
/// 在 OnPostRender 中统一绘制所有飞行卡牌的拖尾，黑色背景上仅拖尾可见。
/// </summary>
[RequireComponent(typeof(Camera))]
public class CardTrailRenderer : MonoBehaviour
{
    public static CardTrailRenderer Instance { get; private set; }

    public class Trail
    {
        public List<Vector2> points = new List<Vector2>();
        public Color color;
        public float size;
        public int maxPoints;
        public bool active = true;
    }

    private List<Trail> _trails = new List<Trail>();
    private Material _glMat;

    void Awake()
    {
        Instance = this;
        Shader s = Shader.Find("Hidden/Internal-Colored");
        if (s == null) s = Shader.Find("UI/Default");
        _glMat = new Material(s);
        _glMat.hideFlags = HideFlags.HideAndDontSave;
        _glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glMat.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        _glMat.SetInt("_ZWrite", 0);
        _glMat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (_glMat != null) Destroy(_glMat);
    }

    public Trail CreateTrail(Color color, float size, int maxPoints)
    {
        var t = new Trail { color = color, size = size, maxPoints = maxPoints };
        _trails.Add(t);
        return t;
    }

    public void RemoveTrail(Trail t)
    {
        t.active = false;
        _trails.Remove(t);
    }

    void OnPostRender()
    {
        if (_glMat == null || _trails.Count == 0) return;
        _glMat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

        foreach (var trail in _trails)
        {
            var pts = trail.points;
            if (!trail.active || pts.Count < 2) continue;

            GL.Begin(GL.QUADS);
            for (int i = 0; i < pts.Count; i++)
            {
                float ratio = (float)(i + 1) / pts.Count;
                float a = ratio * trail.color.a;
                float hs = trail.size * ratio * 0.5f;

                GL.Color(new Color(trail.color.r, trail.color.g, trail.color.b, a));
                Vector2 p = pts[i];
                GL.Vertex3(p.x - hs, p.y - hs, 0);
                GL.Vertex3(p.x + hs, p.y - hs, 0);
                GL.Vertex3(p.x + hs, p.y + hs, 0);
                GL.Vertex3(p.x - hs, p.y + hs, 0);
            }
            GL.End();
        }

        GL.PopMatrix();
    }
}
