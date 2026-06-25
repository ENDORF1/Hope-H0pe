using UnityEngine;
using TMPro;
using DG.Tweening;

/// <summary>
/// 挂在 Player Portrait 上。
/// 同时维护两个 Health Number：原肖像上的 + 预览对象上的，保持同步。
/// </summary>
public class PlayerHealthUI : MonoBehaviour
{
    [Header("直接拖入对应的 TMP 对象")]
    [SerializeField] private TextMeshProUGUI portraitHealthText;  // 原肖像上的 Health Number
    [SerializeField] private TextMeshProUGUI previewHealthText;   // 预览对象上的 Health Number

    [Header("PlayerState（留空则自动向上查找）")]
    [SerializeField] private PlayerState playerState;

    [Header("闪烁颜色")]
    [SerializeField] private Color damageFlashColor = Color.red;
    [SerializeField] private Color healFlashColor   = new Color(0.2f, 1f, 0.3f);
    [SerializeField] private float flashDuration    = 0.2f;

    private Color   _originalPortraitColor;
    private Color   _originalPreviewColor;
    private Tweener _flashTweenPortrait;
    private Tweener _flashTweenPreview;
    private bool    _bound = false;

    // ─────────────────────────────────────────────────
    void Awake()
    {
        if (playerState == null)
            playerState = GetComponentInParent<PlayerState>(true);

        if (portraitHealthText != null) _originalPortraitColor = portraitHealthText.color;
        if (previewHealthText  != null) _originalPreviewColor  = previewHealthText.color;

        TryBind();
    }

    void Start()
    {
        TryBind();
        RefreshImmediate();
    }

    void OnDestroy() => Unbind();

    // ─────────────────────────────────────────────────
    void TryBind()
    {
        if (_bound || playerState == null) return;
        playerState.OnHealthChanged += HandleHealthChanged;
        _bound = true;
        Debug.Log($"[PlayerHealthUI] 绑定成功 → {playerState.PlayerName}");
    }

    void Unbind()
    {
        if (!_bound || playerState == null) return;
        playerState.OnHealthChanged -= HandleHealthChanged;
        _bound = false;
    }

    // ─────────────────────────────────────────────────
    private void HandleHealthChanged(int newHealth, int delta)
    {
        // 即时更新两个文字
        if (portraitHealthText != null) portraitHealthText.text = newHealth.ToString();
        if (previewHealthText  != null) previewHealthText.text  = newHealth.ToString();

        // 闪烁
        Flash(portraitHealthText, ref _flashTweenPortrait, _originalPortraitColor, delta);
        Flash(previewHealthText,  ref _flashTweenPreview,  _originalPreviewColor,  delta);

        // 浮字（导弹阶段由弹体落点统一显示，此处跳过）
        if (playerState == null || !playerState.SuppressDamageEffect)
            DamageEffect.Create(transform.position, -delta, playerState.MaxHealth);
    }

    private void Flash(TextMeshProUGUI text, ref Tweener tween, Color original, int delta)
    {
        if (text == null) return;
        Color flash = delta < 0 ? damageFlashColor : healFlashColor;
        tween?.Kill();
        text.color = flash;
        tween = DOTween.To(
            () => text.color,
            c  => text.color = c,
            original, flashDuration
        ).SetEase(Ease.OutQuad);
    }

    public void RefreshImmediate()
    {
        if (playerState == null) return;
        string hp = playerState.TotalHealth.ToString();
        if (portraitHealthText != null) portraitHealthText.text = hp;
        if (previewHealthText  != null) previewHealthText.text  = hp;
    }
}