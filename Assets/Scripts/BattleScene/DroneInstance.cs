using UnityEngine;

/// <summary>
/// 无人机运行时数据。
/// 挂在无人机 GameObject 上，由 ModuleInstance.AttachDrone 初始化。
/// 不占用部署格，附着在宿主模块上。
/// </summary>
public class DroneInstance : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // 运行时属性
    // ─────────────────────────────────────────────────
    public DroneType Type       { get; private set; }
    public int       Attack     { get; private set; }  // 伤害型专用
    public int       HealAmount { get; private set; }  // 治疗型专用
    public int       MaxHealth  { get; private set; }
    public int       CurrentHealth { get; private set; }
    public bool      IsAlive    => CurrentHealth > 0;

    /// <summary>true = 主动消耗（如建造型触发后），false = 被摧毁。供计数类卡牌区分用。</summary>
    public bool      WasConsumed { get; private set; } = false;

    /// <summary>标记为主动消耗（建造型无人机触发后调用）</summary>
    public void MarkConsumed() => WasConsumed = true;

    /// <summary>附着的宿主模块</summary>
    public ModuleInstance Host  { get; private set; }

    // ─────────────────────────────────────────────────
    // 初始化
    // ─────────────────────────────────────────────────

    /// <summary>由 ModuleInstance.AttachDrone 调用</summary>
    public void Initialize(DroneType type, int attack, int healAmount,
                           int health, ModuleInstance host)
    {
        Type          = type;
        Attack        = attack;
        HealAmount    = healAmount;
        MaxHealth     = health;
        CurrentHealth = health;
        Host          = host;

        Debug.Log($"[DroneInstance] 初始化: {type} | 攻:{attack} 治:{healAmount} 血:{health} | 宿主:{host?.Asset?.GetDisplayName()}");
    }

    // ─────────────────────────────────────────────────
    // 伤害
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 无人机受到伤害。返回实际吸收量（剩余伤害由调用方继续传导）。
    /// 注意：无人机血量不计入玩家总血量，死亡不触发 PlayerState 扣血。
    /// </summary>
    public int TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return 0;

        int absorbed = Mathf.Min(amount, CurrentHealth);
        CurrentHealth -= absorbed;

        Debug.Log($"[DroneInstance] {Type} 无人机受到 {absorbed} 点伤害，剩余: {CurrentHealth}/{MaxHealth}");

        if (CurrentHealth <= 0)
            Debug.Log($"[DroneInstance] {Type} 无人机被摧毁！");

        return absorbed;
    }
}