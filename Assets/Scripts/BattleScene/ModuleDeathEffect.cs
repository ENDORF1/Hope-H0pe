using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 模块卡死亡动画：从上往下逐渐透明消失。
/// 挂在模块卡 Prefab 根对象上。
/// </summary>
public class ModuleDeathEffect : MonoBehaviour
{
    [Header("动画参数")]
    [SerializeField] private float duration  = 0.8f;   // 总动画时长
    [SerializeField] private float stagger   = 0.15f;  // 每个 Image 之间的延迟差

    /// <summary>
    /// 播放死亡动画，完成后销毁 GameObject。
    /// 由 CombatEngine 调用。
    /// </summary>
    private bool _playing = false;

    public IEnumerator PlayAndDestroy()
    {
        if (_playing) yield break;
        _playing = true;
        Debug.Log($"[ModuleDeathEffect] 开始播放死亡动画：{gameObject.name}");

        var images = new List<(Image img, float normalizedY)>();

        foreach (Image img in GetComponentsInChildren<Image>(true))
        {
            if (img.gameObject.name == "CardGlow") continue;

            float y = img.rectTransform.position.y;
            images.Add((img, y));
            Debug.Log($"[ModuleDeathEffect] 找到 Image：{img.gameObject.name}，Y={y}，Alpha={img.color.a}");
        }

        if (images.Count == 0)
        {
            Debug.LogWarning($"[ModuleDeathEffect] 没有找到任何 Image，直接销毁");
            Destroy(gameObject);
            yield break;
        }

        images.Sort((a, b) => b.normalizedY.CompareTo(a.normalizedY));

        float yMax   = images[0].normalizedY;
        float yMin   = images[images.Count - 1].normalizedY;
        float yRange = Mathf.Max(yMax - yMin, 0.01f);

        foreach (var (img, y) in images)
        {
            float delay   = (1f - (y - yMin) / yRange) * stagger;
            float fadeDur = duration * 0.7f;
            Debug.Log($"[ModuleDeathEffect] DOFade {img.gameObject.name} delay={delay:F2} dur={fadeDur:F2}");
            img.DOFade(0f, fadeDur).SetDelay(delay).SetEase(Ease.InQuart);
        }

        yield return new WaitForSeconds(duration);
        Destroy(gameObject);
    }
}