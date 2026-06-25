using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 挂在模块卡牌根对象上（与 ModuleInstance 同层）。
/// 监听 ModuleInstance 血量变化，刷新卡牌上的 Health Number 文字。
/// </summary>
public class ModuleHealthUI : MonoBehaviour
{
    [Header("引用（留空则自动查找）")]
    [SerializeField] private ModuleInstance moduleInstance;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("受伤/治疗闪烁")]
    [SerializeField] private Color damageColor = Color.red;
    [SerializeField] private Color healColor   = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private float flashDuration = 0.25f;

    private Color _originalColor;
    private Tweener _flashTween;

    void Awake()
    {
        if (moduleInstance == null)
            moduleInstance = GetComponent<ModuleInstance>();

        if (healthText == null)
            healthText = FindDeepChild(transform, "Health Number")
                         ?.GetComponent<TextMeshProUGUI>();

        if (healthText != null)
            _originalColor = healthText.color;
    }

    void OnEnable()
    {
        if (moduleInstance != null)
            moduleInstance.OnHealthChanged += HandleHealthChanged;
    }

    void OnDisable()
    {
        if (moduleInstance != null)
            moduleInstance.OnHealthChanged -= HandleHealthChanged;
    }

    // ModuleInstance 初始化后由外部调用一次，刷新初始数值
    public void RefreshDisplay()
    {
        if (moduleInstance == null || healthText == null) return;
        healthText.text = moduleInstance.CurrentHealth.ToString();
    }

    // ─────────────────────────────────────────────────
    private void HandleHealthChanged(int currentHealth, int delta)
    {
        if (healthText == null) return;

        healthText.text = currentHealth.ToString();

        Color flashColor = delta < 0 ? damageColor : healColor;
        _flashTween?.Kill();
        healthText.color = flashColor;
        _flashTween = DOTween.To(
            () => healthText.color,
            c  => healthText.color = c,
            _originalColor,
            flashDuration
        ).SetEase(Ease.OutQuad);
    }

    // ─────────────────────────────────────────────────
    private static Transform FindDeepChild(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name) return child;
            Transform found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
