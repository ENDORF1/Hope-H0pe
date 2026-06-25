using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Inspector 可显式指定的 Glow 引用，避免硬编码 transform.Find("Canvas/CardGlow") 失效。
/// 通常挂在“模块根对象”或“部署格子对象”上，把对应的 CardGlow(Image) 拖进来即可。
///
/// - glowImage：要被点亮/熄灭的 Image（例如 CardGlow）
/// - 若未赋值，会在 Awake 时尝试自动查找：
///     1) 先找本物体直系路径 "Canvas/CardGlow"
///     2) 再在子层级里找第一个名为 "CardGlow" 的 Image（包含 inactive）
/// </summary>
[DisallowMultipleComponent]
public class GlowReference : MonoBehaviour
{
    [SerializeField] private Image glowImage;

    public Image GlowImage => glowImage;

    private void Awake()
    {
        if (glowImage != null) return;

        // 优先：本体 Canvas/CardGlow（避免误拿到 Preview 的 CardGlow）
        Transform t = transform.Find("Canvas/CardGlow");
        if (t != null)
        {
            glowImage = t.GetComponent<Image>();
            if (glowImage != null) return;
        }

        // 兜底：找任意子物体名为 CardGlow 的 Image（含 inactive）
        foreach (var img in GetComponentsInChildren<Image>(true))
        {
            if (img != null && img.name == "CardGlow")
            {
                glowImage = img;
                return;
            }
        }
    }

#if UNITY_EDITOR
    // 在 Editor 里点击 Reset 时自动尝试填充，减少手动工作量
    private void Reset()
    {
        glowImage = null;
        Awake();
    }
#endif
}

