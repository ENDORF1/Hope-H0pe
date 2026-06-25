using UnityEngine;

/// <summary>
/// 连接 ModuleInstance（数据）和 UI 层（OneCardManager + ModuleHealthUI）。
/// 挂在模块卡牌根对象上，与 ModuleInstance 同层。
/// </summary>
[RequireComponent(typeof(ModuleInstance))]
public class ModuleRuntimeBridge : MonoBehaviour
{
    private ModuleInstance _module;
    private OneCardManager _ui;
    private ModuleHealthUI _healthUI;
    private bool _bound = false;

    void Awake()
    {
        _module   = GetComponent<ModuleInstance>();
        _ui       = GetComponent<OneCardManager>() ?? GetComponentInChildren<OneCardManager>(true);
        _healthUI = GetComponent<ModuleHealthUI>();
    }

    void Start()
    {
        TryBind();
    }

    void OnDestroy() => Unbind();

    void TryBind()
    {
        if (_bound || _module == null) return;
        _module.OnHealthChanged    += HandleHealthChanged;
        _module.OnDestroyed        += HandleDestroyed;
        _module.OnFaceStateChanged += HandleFaceStateChanged;
        _bound = true;
    }

    void Unbind()
    {
        if (!_bound || _module == null) return;
        _module.OnHealthChanged    -= HandleHealthChanged;
        _module.OnDestroyed        -= HandleDestroyed;
        _module.OnFaceStateChanged -= HandleFaceStateChanged;
        _bound = false;
    }

    /// <summary>DeploySlot.PlaceModule 初始化 ModuleInstance 后调用，刷新初始 UI</summary>
    public void OnInitialized()
    {
        TryBind(); // 确保已绑定（Initialize 在 Start 之前可能被调用）
        _healthUI?.RefreshDisplay();
        // 通知 MissileModuleVFX 判断是否启用（非导弹模块自动忽略）
        GetComponent<MissileModuleVFX>()?.OnModuleInitialized();
    }

    // ─────────────────────────────────────────────────
    private void HandleHealthChanged(int currentHealth, int delta)
    {
        if (_ui != null)
        {
            if (delta < 0) _ui.TakeDamage(-delta, currentHealth);
            else           _ui.ChangeStats(_module.Attack, currentHealth);

            // 同步更新预览窗口血量
            if (_ui.PreviewManager != null)
            {
                if (delta < 0) _ui.PreviewManager.TakeDamage(-delta, currentHealth);
                else           _ui.PreviewManager.ChangeStats(_module.Attack, currentHealth);
            }
        }
        // 浮字由 CombatEngine 统一调用，此处不重复
    }

    private void HandleDestroyed(ModuleInstance module)
    {
        Debug.Log($"[ModuleRuntimeBridge] {module.Asset.GetDisplayName()} 被摧毁");
        // 销毁前先清理所有子对象上的 DOTween，防止访问已销毁 Image
        foreach (var img in gameObject.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            DG.Tweening.DOTween.Kill(img, complete: false);
        foreach (var t in gameObject.GetComponentsInChildren<UnityEngine.Transform>(true))
            DG.Tweening.DOTween.Kill(t, complete: false);
        ModuleDeathEffect effect = GetComponent<ModuleDeathEffect>();
        if (effect != null)
            StartCoroutine(effect.PlayAndDestroy());
        else
            Destroy(gameObject, 0.5f);
    }

    private void HandleFaceStateChanged(bool isFaceDown)
    {
        // 预留：翻面音效等
    }
}