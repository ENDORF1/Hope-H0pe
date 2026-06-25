using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

/// <summary>
/// 受击/治疗浮字效果。
/// 普通伤害/治疗：文字带 <wave> 标签
/// 大伤害（≥总血量33%）：文字带 <shake> 标签
/// </summary>
public class DamageEffect : MonoBehaviour
{
    [Header("血溅图片")]
    public Sprite[] Splashes;
    public Image    DamageImage;

    [Header("图片颜色")]
    [SerializeField] private Color damageImageColor = Color.white;
    [SerializeField] private Color healImageColor   = new Color(0.2f, 1f, 0.3f, 0.5f);

    [Header("伤害数字 TMP")]
    public TextMeshProUGUI AmountTextTMP;

    [Header("文字颜色")]
    [SerializeField] private Color damageTextColor    = Color.red;
    [SerializeField] private Color healTextColor      = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private Color bigDamageTextColor = Color.yellow;

    [Header("Canvas Group")]
    public CanvasGroup cg;

    [Header("动画参数")]
    [SerializeField] private float floatUpDistance = 80f;
    [SerializeField] private float floatDuration   = 0.4f;
    [SerializeField] private float holdDuration    = 0.5f;
    [SerializeField] private float fadeDuration    = 0.3f;
    [SerializeField] private float punchScale      = 1.3f;
    [SerializeField] private float bigDamagePunch  = 1.7f;  // 大伤害时更强的冲击

    // ─────────────────────────────────────────────────
    void Awake()
    {
        if (AmountTextTMP == null)
            AmountTextTMP = GetComponentInChildren<TextMeshProUGUI>(true);
        if (DamageImage == null)
            DamageImage = GetComponentInChildren<Image>(true);
        if (cg == null)
            cg = GetComponentInChildren<CanvasGroup>(true);

        if (Splashes != null && Splashes.Length > 0 && DamageImage != null)
            DamageImage.sprite = Splashes[Random.Range(0, Splashes.Length)];
    }

    // ─────────────────────────────────────────────────
    // amount > 0 = 伤害，amount < 0 = 治疗
    // maxHealth 用于判断是否大伤害（传0则不判断大伤害）
    // ─────────────────────────────────────────────────
    public static void Create(Vector3 worldPosition, int amount, int maxHealth = 0)
    {
        if (amount == 0) return;
        Debug.Log($"[DamageEffect] Create 调用，amount={amount}\n{System.Environment.StackTrace}");

        GameObject prefab = DamageEffectSpawner.Prefab;
        if (prefab == null)
        {
            Debug.LogWarning("[DamageEffect] DamageEffectSpawner.Prefab 未设置！");
            return;
        }

        GameObject go = Instantiate(prefab, worldPosition, Quaternion.identity);
        foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            t.gameObject.SetActive(true);

        Canvas canvas = go.GetComponentInChildren<Canvas>(true);
        if (canvas != null)
        {
            canvas.overrideSorting  = true;
            canvas.sortingLayerName = "Above Above Everything";
            canvas.sortingOrder     = 999;
        }

        DamageEffect de = go.GetComponent<DamageEffect>();
        if (de == null) return;

        bool isBigDamage = amount > 0 && maxHealth > 0 && amount >= maxHealth / 3;
        de.Setup(amount, isBigDamage);
        de.StartCoroutine(de.PlayAnimation(isBigDamage));
    }

    public static void CreateDamageEffect(Vector3 position, int amount) => Create(position, amount);

    // ─────────────────────────────────────────────────
    private void Setup(int amount, bool isBigDamage)
    {
        bool isHeal = amount < 0;

        string rawNumber = isHeal ? $"+{-amount}" : $"-{amount}";

        // TMPEffects 标签（需要 TMPAnimator 组件 + Use default database 勾选）
        string tagged;
        if (isHeal)
            tagged = $"<wave>{rawNumber}</wave>";
        else if (isBigDamage)
            tagged = $"<shake>{rawNumber}</shake>";
        else
            tagged = $"<wave>{rawNumber}</wave>";

        if (AmountTextTMP != null)
        {
            AmountTextTMP.text  = tagged;
            AmountTextTMP.color = isBigDamage ? bigDamageTextColor
                                : isHeal       ? healTextColor
                                               : damageTextColor;
        }

        if (DamageImage != null)
            DamageImage.color = isHeal ? healImageColor : damageImageColor;

        if (cg != null) cg.alpha = 1f;
        transform.localScale = Vector3.one;
    }

    // ─────────────────────────────────────────────────
    private IEnumerator PlayAnimation(bool isBigDamage)
    {
        float punch = isBigDamage ? bigDamagePunch : punchScale;

        transform.localScale = Vector3.one * 0.3f;
        yield return transform.DOScale(punch, floatDuration * 0.4f)
            .SetEase(Ease.OutBack).WaitForCompletion();
        yield return transform.DOScale(1f, floatDuration * 0.2f)
            .SetEase(Ease.InQuad).WaitForCompletion();

        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            float startY = rt.anchoredPosition.y;
            yield return rt.DOAnchorPosY(startY + floatUpDistance, floatDuration + holdDuration)
                .SetEase(Ease.OutQuart).WaitForCompletion();
        }
        else
        {
            yield return transform.DOMoveY(transform.position.y + 0.8f, floatDuration + holdDuration)
                .SetEase(Ease.OutQuart).WaitForCompletion();
        }

        if (cg != null)
            yield return cg.DOFade(0f, fadeDuration).SetEase(Ease.InQuad).WaitForCompletion();

        Destroy(gameObject);
    }
}