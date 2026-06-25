using UnityEngine;

/// <summary>
/// 挂在 Main Camera 上。
/// OnRenderImage 在 Main Camera 渲染完整帧后触发，
/// 此时画面包含所有内容（背景 Shader + Canvas 文字）。
/// 把完整画面 Blit 进 ReflectionRT，供 ReflectionDisplay 读取。
/// 
/// 注意：Canvas B 里的 ButtonContainer 和 ReflectionDisplay
/// 因为在独立的 Canvas B 里，不会被截进去。
/// </summary>
[RequireComponent(typeof(Camera))]
public class ScreenCaptureToRT : MonoBehaviour
{
    [Header("渲染目标")]
    [Tooltip("拖入 ReflectionRT")]
    [SerializeField] private RenderTexture reflectionRT;

    private void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (reflectionRT != null)
            Graphics.Blit(src, reflectionRT);

        Graphics.Blit(src, dest);
    }
}
