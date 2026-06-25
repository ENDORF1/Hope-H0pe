using UnityEngine;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 万物归墟翻面特效：红色抓痕从四边渗入后褪去。
/// 挂在 Main Camera 上，和 EdgeGlowEffect 并列。
/// </summary>
[RequireComponent(typeof(Camera))]
public class ClawGlowEffect : MonoBehaviour
{
    public static ClawGlowEffect Instance { get; private set; }

    [Header("材质")]
    [Tooltip("拖入用 Custom/ClawGlow Shader 创建的 Material（ClawGlowMaterial）")]
    [SerializeField] private Material clawMaterial;

    [Header("动画参数")]
    [Tooltip("渗入时长（秒）")]
    [SerializeField] private float fadeInDuration  = 0.4f;
    [Tooltip("褪去时长（秒）")]
    [SerializeField] private float fadeOutDuration = 0.6f;
    [Tooltip("渗入峰值透明度")]
    [SerializeField] private float peakAlpha       = 1.0f;

    private float _alpha = 0f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (clawMaterial == null || _alpha <= 0.001f)
        {
            Graphics.Blit(src, dst);
            return;
        }

        clawMaterial.SetFloat("_ClawAlpha", _alpha);
        Graphics.Blit(src, dst, clawMaterial);
    }

    // ─────────────────────────────────────────────────
    // 公开接口
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 抓痕渗入：瞬间爆发到峰值后保持，直到调用 FadeOut。
    /// </summary>
    public IEnumerator FadeIn()
    {
        if (clawMaterial == null) yield break;

        clawMaterial.SetFloat("_ClawSeed", Random.Range(0f, 100f));

        // 极短时间内爆发到峰值，制造突然侵入感
        yield return DOTween.To(() => _alpha, a => _alpha = a, peakAlpha, fadeInDuration)
            .SetEase(Ease.OutExpo).WaitForCompletion();
    }

    /// <summary>
    /// 抓痕褪去。供 WanWuGuiXu 在 MessageManager 播完后调用。
    /// </summary>
    public IEnumerator FadeOut()
    {
        if (clawMaterial == null) yield break;

        yield return DOTween.To(() => _alpha, a => _alpha = a, 0f, fadeOutDuration)
            .SetEase(Ease.InQuart).WaitForCompletion();
    }

    /// <summary>
    /// 完整播放：渗入 → 保持（由外部控制时长）→ 褪去。
    /// holdCoroutine 期间保持显示，结束后自动褪去。
    /// </summary>
    public IEnumerator PlayWithHold(IEnumerator holdCoroutine)
    {
        yield return FadeIn();
        if (holdCoroutine != null)
            yield return holdCoroutine;
        yield return FadeOut();
    }
}