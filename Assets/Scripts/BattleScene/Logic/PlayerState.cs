using UnityEngine;
using System;

/// <summary>
/// 玩家状态：持有总血量，处理受伤/治疗，广播变化事件
/// 挂在玩家根对象上。场景中应有两个实例：一个 Player，一个 AI。
/// </summary>
public class PlayerState : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // Inspector
    // ─────────────────────────────────────────────────
    [Header("基础设置")]
    [SerializeField] private string playerName = "Player";
    [SerializeField] private int maxHealth = 100;

    [Header("肖像位置（供激光特效定位）")]
    public Transform PortraitTransform;

    [Header("调试")]
    [SerializeField] private bool logDamageEvents = true;

    // ─────────────────────────────────────────────────
    // 运行时数据
    // ─────────────────────────────────────────────────
    public int MaxHealth   { get; private set; }
    public int TotalHealth { get; private set; }
    public bool IsDead     => TotalHealth <= 0;
    public string PlayerName => playerName;

    // ─────────────────────────────────────────────────
    // 事件
    // ─────────────────────────────────────────────────

    /// <summary>血量变化时触发（newHealth, delta）delta 为负=受伤，正=治疗</summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>死亡时触发（只触发一次）</summary>
    public event Action OnDeath;

    /// <summary>为true时，PlayerHealthUI不显示伤害数字（由弹体落点统一显示）</summary>
    public bool SuppressDamageEffect { get; set; } = false;

    // ─────────────────────────────────────────────────
    void Awake()
    {
        MaxHealth   = maxHealth;
        TotalHealth = maxHealth;
    }

    // ─────────────────────────────────────────────────
    // 公开接口
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 对玩家本体直接造成伤害（跳过模块，用于导弹直攻/速攻牌等）
    /// </summary>
    public void HealDirectly(int amount)
    {
        if (amount <= 0) return;
        int actual = Mathf.Min(amount, MaxHealth - TotalHealth);
        if (actual <= 0) return;
        TotalHealth += actual;
        OnHealthChanged?.Invoke(TotalHealth, actual);
        Debug.Log($"[{playerName}] 本体直接治疗 {actual}，总血量: {TotalHealth}/{MaxHealth}");
    }

    public void TakeDamageDirectly(int amount)
    {
        if (IsDead || amount <= 0) return;

        int actual = Mathf.Min(amount, TotalHealth);
        TotalHealth -= actual;

        if (logDamageEvents)
            Debug.Log($"[{playerName}] 本体受到 {actual} 点伤害，剩余血量: {TotalHealth}/{MaxHealth}");

        OnHealthChanged?.Invoke(TotalHealth, -actual);

        if (TotalHealth <= 0)
            TriggerDeath();
    }

    /// <summary>
    /// 模块受到伤害时调用。
    /// 规则：扣除量 = min(伤害值, 模块当前血量)
    /// 由 ModuleInstance.TakeDamage 在结算后调用，传入实际扣除量。
    /// </summary>
    public void OnModuleDamaged(int actualDeducted)
    {
        if (IsDead || actualDeducted <= 0) return;

        TotalHealth -= actualDeducted;
        TotalHealth  = Mathf.Max(TotalHealth, 0);

        if (logDamageEvents)
            Debug.Log($"[{playerName}] 模块受伤同步扣除 {actualDeducted}，总血量: {TotalHealth}/{MaxHealth}");

        OnHealthChanged?.Invoke(TotalHealth, -actualDeducted);

        if (TotalHealth <= 0)
            TriggerDeath();
    }

    /// <summary>
    /// 模块被治疗时调用。
    /// 规则：治疗量 = min(治疗值, 模块上限 - 模块当前血量)
    /// 由 ModuleInstance.Heal 在结算后调用，传入实际治疗量。
    /// </summary>
    public void OnModuleHealed(int actualHealed)
    {
        if (actualHealed <= 0) return;

        TotalHealth += actualHealed;
        TotalHealth  = Mathf.Min(TotalHealth, MaxHealth);

        if (logDamageEvents)
            Debug.Log($"[{playerName}] 模块被治疗同步恢复 {actualHealed}，总血量: {TotalHealth}/{MaxHealth}");

        OnHealthChanged?.Invoke(TotalHealth, actualHealed);
    }

    /// <summary>
    /// 增加血量上限（万物归墟等特殊效果）。
    /// 同步增加 TotalHealth，保持当前百分比不变。
    /// </summary>
    public void AddMaxHealth(int amount)
    {
        if (amount <= 0) return;
        MaxHealth   += amount;
        TotalHealth += amount;
        OnHealthChanged?.Invoke(TotalHealth, amount);
        Debug.Log($"[{playerName}] 血量上限 +{amount}，当前: {TotalHealth}/{MaxHealth}");
    }

    /// <summary>直接设置总血量（存档读取/测试用）</summary>
    public void SetHealth(int value)
    {
        int prev   = TotalHealth;
        TotalHealth = Mathf.Clamp(value, 0, MaxHealth);
        int delta  = TotalHealth - prev;

        if (delta != 0)
            OnHealthChanged?.Invoke(TotalHealth, delta);

        if (TotalHealth <= 0 && !IsDead)
            TriggerDeath();
    }

    // ─────────────────────────────────────────────────
    // 内部
    // ─────────────────────────────────────────────────
    private bool _deathFired = false;

    private void TriggerDeath()
    {
        if (_deathFired) return;
        _deathFired = true;

        Debug.Log($"[{playerName}] 总血量归零，判负！");
        OnDeath?.Invoke();
    }
}