using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 挂在 MainCamera 上。
/// 统一收集场景内所有 MenuButtonFX 的 GL 绘制请求，
/// 在 OnPostRender 里一次性画完。
/// </summary>
[RequireComponent(typeof(Camera))]
public class MenuButtonGLRenderer : MonoBehaviour
{
    public static MenuButtonGLRenderer Instance { get; private set; }

    private Material _glMat;
    private readonly List<MenuButtonFX> _buttons = new List<MenuButtonFX>();

    void Awake()
    {
        Instance = this;

        Shader s = Shader.Find("Hidden/Internal-Colored");
        if(s == null) s = Shader.Find("UI/Default");
        _glMat = new Material(s);
        _glMat.hideFlags = HideFlags.HideAndDontSave;
        _glMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        _glMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        _glMat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
        _glMat.SetInt("_ZWrite",   0);
        _glMat.SetInt("_ZTest",    (int)UnityEngine.Rendering.CompareFunction.Always);
    }

    void OnDestroy()
    {
        if(Instance == this) Instance = null;
        if(_glMat != null) Destroy(_glMat);
    }

    public void Register(MenuButtonFX fx)
    {
        if(!_buttons.Contains(fx)) _buttons.Add(fx);
    }

    public void Unregister(MenuButtonFX fx)
    {
        _buttons.Remove(fx);
    }

    void OnPostRender()
    {
        if(_glMat == null) return;
        if(_buttons.Count == 0) return;

        _glMat.SetPass(0);
        GL.PushMatrix();
        GL.LoadPixelMatrix(0, Screen.width, Screen.height, 0);

        foreach(var fx in _buttons)
        {
            if(fx == null || !fx.isActiveAndEnabled) continue;
            fx.GLDraw();
        }

        GL.PopMatrix();
    }
}