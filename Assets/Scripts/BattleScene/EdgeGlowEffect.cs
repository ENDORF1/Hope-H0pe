using UnityEngine;
using DG.Tweening;
using System.Collections;

/// <summary>
/// 天允终偿屏幕边框特效。
/// 挂在 Main Camera 上。
/// Built-in 管线通过 OnRenderImage 把特效叠加到最终画面，
/// 永远和屏幕完全对齐，不受 Canvas 尺寸影响。
/// </summary>
[RequireComponent(typeof(Camera))]
public class EdgeGlowEffect : MonoBehaviour
{
    public static EdgeGlowEffect Instance { get; private set; }

    [Header("材质")]
    [Tooltip("拖入用 Custom/EdgeGlow Shader 创建的 Material（EdgeGlowMaterial）")]
    [SerializeField] private Material glowMaterial;

    [Header("呼吸参数")]
    [Tooltip("呼吸最高透明度（0~1）")]
    [SerializeField] private float breathMax   = 0.85f;
    [Tooltip("呼吸最低透明度（0~1）")]
    [SerializeField] private float breathMin   = 0.2f;
    [Tooltip("单次呼吸时长（秒）")]
    [SerializeField] private float breathSpeed = 1.0f;

    // 当前透明度（由 DOTween 控制）
    private float _alpha     = 0f;
    private bool  _breathing = false;
    private Tweener _breathTweener;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ── Built-in 管线全屏后处理钩子 ──────────────────
    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        if (glowMaterial == null || _alpha <= 0.001f)
        {
            Graphics.Blit(src, dst);
            return;
        }

        glowMaterial.SetFloat("_VignetteAlpha", _alpha);
        Graphics.Blit(src, dst, glowMaterial);
    }

    // ─────────────────────────────────────────────────
    // 公开接口（供 TianYunZhongChang 调用）
    // ─────────────────────────────────────────────────

    /// <summary>淡入并开始呼吸循环</summary>
    public IEnumerator StartBreathing()
    {
        if (_breathing) yield break;
        _breathing = true;

        // 淡入到最亮后保持常亮
        yield return DOTween.To(() => _alpha, a => _alpha = a, breathMax, 0.3f)
            .SetEase(Ease.OutQuad).WaitForCompletion();
    }

    /// <summary>停止呼吸并淡出</summary>
    public IEnumerator StopBreathing()
    {
        if (!_breathing) yield break;
        _breathing = false;

        if (_breathTweener != null && _breathTweener.IsActive())
            _breathTweener.Kill();

        yield return DOTween.To(() => _alpha, a => _alpha = a, 0f, 0.5f)
            .SetEase(Ease.InQuad).WaitForCompletion();
    }

    /// <summary>金色脉冲两次（额外战斗阶段开始前）</summary>
    public IEnumerator PlayGoldPulse()
    {
        if (glowMaterial == null) yield break;

        // 保存三个颜色的原始值
        Color origColor      = glowMaterial.GetColor("_VignetteColor");
        Color origColorInner = glowMaterial.GetColor("_VignetteColorInner");
        Color origColorDeep  = glowMaterial.GetColor("_VignetteColorDeep");

        // 三层全部切换到金色系
        // 边缘：亮金
        // 内侧：深金/琥珀
        // 深处：暗棕金，避免和原蓝紫混色
        glowMaterial.SetColor("_VignetteColor",      new Color(1.00f, 0.82f, 0.10f));
        glowMaterial.SetColor("_VignetteColorInner", new Color(0.70f, 0.45f, 0.02f));
        glowMaterial.SetColor("_VignetteColorDeep",  new Color(0.25f, 0.12f, 0.00f));

        for (int i = 0; i < 2; i++)
        {
            yield return DOTween.To(() => _alpha, a => _alpha = a, 0.9f, 0.2f)
                .SetEase(Ease.OutQuad).WaitForCompletion();
            yield return DOTween.To(() => _alpha, a => _alpha = a, 0f, 0.35f)
                .SetEase(Ease.InQuad).WaitForCompletion();
            if (i < 1) yield return new WaitForSeconds(0.1f);
        }

        // 还原三个颜色
        glowMaterial.SetColor("_VignetteColor",      origColor);
        glowMaterial.SetColor("_VignetteColorInner", origColorInner);
        glowMaterial.SetColor("_VignetteColorDeep",  origColorDeep);
    }

    /// <summary>设置发光颜色（白色/金色切换）</summary>
    public void SetColor(Color color)
    {
        glowMaterial?.SetColor("_VignetteColor", color);
    }

    // ─────────────────────────────────────────────────
    private void BreathLoop()
    {
        if (!_breathing) return;

        _breathTweener = DOTween.To(() => _alpha, a => _alpha = a, breathMin, breathSpeed * 0.5f)
            .SetEase(Ease.InOutSine)
            .OnComplete(() =>
            {
                if (!_breathing) return;
                _breathTweener = DOTween.To(() => _alpha, a => _alpha = a, breathMax, breathSpeed * 0.5f)
                    .SetEase(Ease.InOutSine)
                    .OnComplete(BreathLoop);
            });
    }
}