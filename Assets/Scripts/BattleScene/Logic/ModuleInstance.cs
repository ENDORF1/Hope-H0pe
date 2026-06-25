using UnityEngine;
using System;

/// <summary>
/// 场上模块的运行时数据。
/// CardAsset 是静态模板（ScriptableObject），ModuleInstance 是场上活数据。
/// 挂在模块卡牌 GameObject 上，由 DeploySlot.PlaceModule 初始化。
/// </summary>
public class ModuleInstance : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // 静态数据引用
    // ─────────────────────────────────────────────────
    public CardAsset Asset       { get; private set; }
    public PlayerState Owner     { get; private set; }
    public int SlotIndex         { get; private set; }

    // ─────────────────────────────────────────────────
    // 运行时属性
    // ─────────────────────────────────────────────────
    public int MaxHealth         { get; private set; }
    public int CurrentHealth     { get; private set; }
    public int Attack            { get; private set; }

    public bool IsAlive          => CurrentHealth > 0;
    public bool IsFaceDown       { get; private set; } = true;

    // 冷兵器：剩余冷却回合数（0 = 可以攻击）
    private int _cooldownRemaining = 0;
    public int CooldownRemaining => _cooldownRemaining;

    // ─────────────────────────────────────────────────
    // 事件
    // ─────────────────────────────────────────────────

    /// <summary>血量变化（currentHealth, delta）</summary>
    public event Action<int, int> OnHealthChanged;

    /// <summary>模块被摧毁</summary>
    public event Action<ModuleInstance> OnDestroyed;

    /// <summary>翻开状态变化（isFaceDown）</summary>
    public event Action<bool> OnFaceStateChanged;

    // ─────────────────────────────────────────────────
    // 初始化
    // ─────────────────────────────────────────────────

    public void Initialize(CardAsset asset, PlayerState owner, int slotIndex)
    {
        Asset              = asset;
        Owner              = owner;
        SlotIndex          = slotIndex;
        MaxHealth          = asset.Health;
        CurrentHealth      = asset.Health;
        Attack             = asset.Attack;
        IsFaceDown         = true;
        _cooldownRemaining = 0;

        Debug.Log($"[ModuleInstance] 初始化: {asset.GetDisplayName()} | 血量:{CurrentHealth}/{MaxHealth} | 攻击:{Attack} | 槽位:{slotIndex}");
    }

    // ─────────────────────────────────────────────────
    // 伤害与治疗
    // ─────────────────────────────────────────────────

    public int TakeDamage(int amount)
    {
        if (!IsAlive || amount <= 0) return 0;

        int remaining = amount;
        for (int i = _drones.Count - 1; i >= 0 && remaining > 0; i--)
        {
            DroneInstance drone = _drones[i];
            if (drone == null || !drone.IsAlive) continue;

            int absorbed = drone.TakeDamage(remaining);
            remaining -= absorbed;

            if (!drone.IsAlive)
            {
                _drones.RemoveAt(i);
                DestroyDroneWithSlot(drone);
            }
        }

        if (remaining <= 0) return 0;

        int actual = Mathf.Min(remaining, CurrentHealth);
        CurrentHealth -= actual;

        Debug.Log($"[ModuleInstance] {Asset.GetDisplayName()} 受到 {actual} 点伤害，剩余: {CurrentHealth}/{MaxHealth}");

        OnHealthChanged?.Invoke(CurrentHealth, -actual);
        Owner?.OnModuleDamaged(actual);

        if (CurrentHealth <= 0)
            Destroy_Internal();

        return actual;
    }

    public int Heal(int amount)
    {
        if (!IsAlive || amount <= 0) return 0;

        int actual = Mathf.Min(amount, MaxHealth - CurrentHealth);
        if (actual <= 0) return 0;

        CurrentHealth += actual;

        Debug.Log($"[ModuleInstance] {Asset.GetDisplayName()} 治疗 {actual} 点，当前: {CurrentHealth}/{MaxHealth}");

        OnHealthChanged?.Invoke(CurrentHealth, actual);
        Owner?.OnModuleHealed(actual);

        return actual;
    }

    // ─────────────────────────────────────────────────
    // 翻面状态
    // ─────────────────────────────────────────────────

    /// <summary>翻开模块（盖伏→翻开）。翻开时不进入冷却，攻击后再由 CombatEngine 调用 StartCooldown()。</summary>
    public void FlipFaceUp()
    {
        if (!IsFaceDown) return;
        IsFaceDown = false;

        OnFaceStateChanged?.Invoke(false);

        SceneRefs.Instance?.GameManager?.DialogueManager?.PlayCardDialogue(Asset);

        OneCardManager ocm = GetComponent<OneCardManager>() ?? GetComponentInChildren<OneCardManager>(true);
        if (ocm != null)
        {
            ocm.ReadCardFromAsset();
            if (ocm.PreviewManager != null)
            {
                ocm.PreviewManager.cardAsset = ocm.cardAsset;
                ocm.PreviewManager.ReadCardFromAsset();
            }
        }

        BetterCardRotation rot = GetComponentInChildren<BetterCardRotation>(true);
        if (rot != null) rot.FlipWithAnimation(true);
        else Debug.LogWarning($"[ModuleInstance] {Asset?.GetDisplayName()} 找不到 BetterCardRotation！");
    }

    /// <summary>过热盖伏（翻开→盖伏）</summary>
    public void FlipFaceDown()
    {
        if (IsFaceDown) return;
        IsFaceDown         = true;
        _cooldownRemaining = 0;

        OnFaceStateChanged?.Invoke(true);

        BetterCardRotation rot = GetComponentInChildren<BetterCardRotation>(true);
        if (rot != null) rot.FlipWithAnimation(false);
        else Debug.LogWarning($"[ModuleInstance] {Asset?.GetDisplayName()} 找不到 BetterCardRotation！");
    }

    public bool IsOverheated { get; private set; } = false;

    public void OverheatFaceDown()
    {
        FlipFaceDown();
        IsOverheated = true;
        GetComponent<OverheatIndicator>()?.Show();
    }

    public void ClearOverheat()
    {
        IsOverheated = false;
        GetComponent<OverheatIndicator>()?.Hide();
    }

    // ─────────────────────────────────────────────────
    // 冷却管理
    // ─────────────────────────────────────────────────

    /// <summary>攻击结算后由 CombatEngine 调用，开始冷却。
    /// CooldownTurns &lt; 0 → 永久冻结（攻击一次后再也无法攻击）。
    /// CooldownTurns = N → 跳过 N 个完整回合后才能再次攻击。
    /// 存 N+1 是因为下一回合开始时 OnTurnStart() 会立即 -1。</summary>
    public void StartCooldown()
    {
        if (Asset == null || Asset.ModuleType != ModuleType.Melee) return;

        if (Asset.CooldownTurns < 0)
            _cooldownRemaining = int.MaxValue; // 永久冻结
        else
            _cooldownRemaining = Asset.CooldownTurns + 1;
    }

    // ─────────────────────────────────────────────────
    // 回合管理
    // ─────────────────────────────────────────────────

    /// <summary>每回合开始时调用：冷却计数器递减</summary>
    public void OnTurnStart()
    {
        if (_cooldownRemaining > 0)
            _cooldownRemaining--;
    }

    /// <summary>冷兵器：本回合是否可以攻击（冷却归零才能攻击）</summary>
    public bool CanMeleeAttack => _cooldownRemaining <= 0;

    // ─────────────────────────────────────────────────
    // 无人机管理
    // ─────────────────────────────────────────────────

    private readonly System.Collections.Generic.List<DroneInstance> _drones
        = new System.Collections.Generic.List<DroneInstance>();

    public System.Collections.Generic.IReadOnlyList<DroneInstance> Drones => _drones.AsReadOnly();

    public void AttachDrone(DroneInstance drone)
    {
        if (drone == null) return;
        _drones.Add(drone);
        Debug.Log($"[ModuleInstance] {Asset.GetDisplayName()} 附着无人机 {drone.Type}，当前共 {_drones.Count} 架");
    }

    public void RemoveDrone(DroneInstance drone)
    {
        _drones.Remove(drone);
    }

    public void PurgeDeadDrones()
    {
        _drones.RemoveAll(d => d == null || !d.IsAlive);
    }

    // ─────────────────────────────────────────────────
    // 运行时属性修改
    // ─────────────────────────────────────────────────

    public void SetAttack(int value)
    {
        Attack = Mathf.Max(0, value);
    }

    public void SetMaxHealth(int value, bool adjustCurrent = true)
    {
        int old = MaxHealth;
        MaxHealth = Mathf.Max(1, value);
        int delta = MaxHealth - old;
        if (delta > 0)
            Owner?.AddMaxHealth(delta);
        if (adjustCurrent)
            CurrentHealth = Mathf.Clamp(CurrentHealth, 0, MaxHealth);
    }

    public void AddCurrentHealth(int amount)
    {
        if (amount <= 0) return;
        CurrentHealth += amount;
        Owner?.OnModuleHealed(amount);
        OnHealthChanged?.Invoke(CurrentHealth, amount);
        Debug.Log($"[ModuleInstance] {Asset.GetDisplayName()} 血量溢出增加 {amount}，当前: {CurrentHealth}/{MaxHealth}");
    }

    private void DestroyDroneWithSlot(DroneInstance drone)
    {
        if (drone == null || drone.gameObject == null) return;

        Transform slot = drone.transform.parent;
        if (slot != null)
        {
            DroneDeployZone droneZone = slot.GetComponentInParent<DroneDeployZone>();
            if (droneZone != null)
                droneZone.RemoveDroneSlot(-1, slot.gameObject);
            else
            {
                slot.gameObject.SetActive(false);
                Destroy(slot.gameObject);
            }
        }
        else
        {
            Destroy(drone.gameObject);
        }
    }

    // ─────────────────────────────────────────────────
    // 销毁
    // ─────────────────────────────────────────────────

    private bool _destroyFired = false;

    private void Destroy_Internal()
    {
        if (_destroyFired) return;
        _destroyFired = true;

        for (int i = _drones.Count - 1; i >= 0; i--)
            DestroyDroneWithSlot(_drones[i]);
        _drones.Clear();

        Debug.Log($"[ModuleInstance] {Asset.GetDisplayName()} 被摧毁！");
        OnDestroyed?.Invoke(this);
    }

    public void ForceDestroy()
    {
        CurrentHealth = 0;
        Destroy_Internal();
    }

    public void SilentDestroy()
    {
        if (_destroyFired) return;
        CurrentHealth = 0;
        _destroyFired = true;
        Debug.Log($"[ModuleInstance] {Asset.GetDisplayName()} 被静默摧毁（不扣本体血量）");
        OnDestroyed?.Invoke(this);
    }
}