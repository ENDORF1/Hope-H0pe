using UnityEngine;

/// <summary>
/// 血量系统测试脚本
/// 运行后在 Inspector 拖入引用。
/// testModule 被摧毁后重新拖入新模块即可继续测试。
///
///   Q - 模块受伤（damageAmount）
///   W - 模块受伤（damageAmountLarge）
///   E - 治疗 15（满血时先扣10）
///   R - 玩家本体直接受伤（damageAmount）
///   F - 翻开/盖伏模块
///   T - 强制摧毁模块
///   Y - 打印状态
///   U - 敌方本体直接受伤（damageAmount）
///   I - 目标格子内模块受伤（damageAmount）
///   O - 目标无人机格子内第一个无人机受伤（damageAmount）
/// </summary>
public class HealthSystemTest : MonoBehaviour
{
    [Header("运行后拖入（模块摧毁后需重新拖）")]
    public ModuleInstance testModule;
    public PlayerState    testPlayer;
    public PlayerState    enemyPlayer;

    [Header("部署区引用")]
    public DeployZone playerDeployZone;
    public DeployZone aiDeployZone;

    [Header("无人机部署区引用")]
    public DroneDeployZone playerDroneZone;
    public DroneDeployZone aiDroneZone;

    [Header("伤害数值")]
    public int damageAmount      = 10;
    public int damageAmountLarge = 25;
    public int healAmount        = 15;

    [Header("目标格子设置")]
    public int  targetSlotIndex  = 0;
    public bool targetIsPlayer   = false; // false = AI部署区

    [Header("目标无人机格子设置")]
    public int  targetDroneColumn  = 0;
    public bool droneTargetIsPlayer = false; // false = AI无人机区

    [Header("按键绑定")]
    [SerializeField] private KeyCode damageSmall      = KeyCode.Q;
    [SerializeField] private KeyCode damageLarge      = KeyCode.W;
    [SerializeField] private KeyCode heal             = KeyCode.E;
    [SerializeField] private KeyCode directDamage     = KeyCode.R;
    [SerializeField] private KeyCode flipModule       = KeyCode.F;
    [SerializeField] private KeyCode forceDestroy     = KeyCode.T;
    [SerializeField] private KeyCode printStatus      = KeyCode.Y;
    [SerializeField] private KeyCode enemyDirect      = KeyCode.U;
    [SerializeField] private KeyCode slotDamage       = KeyCode.I;
    [SerializeField] private KeyCode droneDamage      = KeyCode.O;

    void Start()
    {
        Debug.Log("[HealthSystemTest] 就绪");
        Debug.Log($"  {damageSmall}/-{damageAmount}  {damageLarge}/-{damageAmountLarge}  {heal}/治疗  {directDamage}/本体-{damageAmount}");
        Debug.Log($"  {flipModule}/翻面  {forceDestroy}/摧毁  {printStatus}/状态");
        Debug.Log($"  {enemyDirect}/敌方本体-{damageAmount}  {slotDamage}/目标格子模块-{damageAmount}  {droneDamage}/目标无人机-{damageAmount}");
        Debug.Log("  模块被摧毁后，在 Inspector 里把新模块拖到 testModule 字段继续测试");
    }

    void Update()
    {
        if (Input.GetKeyDown(damageSmall))  DoDamage(damageAmount);
        if (Input.GetKeyDown(damageLarge))  DoDamage(damageAmountLarge);
        if (Input.GetKeyDown(heal))         DoHeal(healAmount);
        if (Input.GetKeyDown(directDamage)) DoDirectDamage(damageAmount);
        if (Input.GetKeyDown(flipModule))   DoFlip();
        if (Input.GetKeyDown(forceDestroy)) DoForceDestroy();
        if (Input.GetKeyDown(printStatus))  PrintStatus();
        if (Input.GetKeyDown(enemyDirect))  DoEnemyDirectDamage(damageAmount);
        if (Input.GetKeyDown(slotDamage))   DoSlotModuleDamage(damageAmount);
        if (Input.GetKeyDown(droneDamage))  DoDroneDamage(damageAmount);
    }

    // ─────────────────────────────────────────────────
    // 原有功能
    // ─────────────────────────────────────────────────

    void DoDamage(int amount)
    {
        if (!CheckModule()) return;
        int actual = testModule.TakeDamage(amount);
        Debug.Log($"[Test] 模块 -{actual} → {testModule.CurrentHealth}/{testModule.MaxHealth} | 玩家: {testPlayer?.TotalHealth}");
    }

    void DoHeal(int amount)
    {
        if (!CheckModule()) return;
        if (testModule.CurrentHealth == testModule.MaxHealth)
        {
            Debug.Log("[Test] 满血，先扣 10 再治疗");
            testModule.TakeDamage(10);
        }
        int actual = testModule.Heal(amount);
        Debug.Log($"[Test] 模块 +{actual} → {testModule.CurrentHealth}/{testModule.MaxHealth} | 玩家: {testPlayer?.TotalHealth}");
    }

    void DoDirectDamage(int amount)
    {
        if (testPlayer == null) { Debug.LogWarning("[Test] testPlayer 未赋值"); return; }
        testPlayer.TakeDamageDirectly(amount);
        Debug.Log($"[Test] 玩家本体 -{amount} → {testPlayer.TotalHealth}/{testPlayer.MaxHealth}");
    }

    void DoFlip()
    {
        if (!CheckModule()) return;
        if (testModule.IsFaceDown) { testModule.FlipFaceUp();   Debug.Log("[Test] 翻开"); }
        else                       { testModule.FlipFaceDown(); Debug.Log("[Test] 盖伏"); }
    }

    void DoForceDestroy()
    {
        if (!CheckModule()) return;
        Debug.Log("[Test] 强制摧毁");
        testModule.ForceDestroy();
        testModule = null;
    }

    void PrintStatus()
    {
        if (testModule != null && testModule.IsAlive)
            Debug.Log($"[Status] {testModule.Asset?.GetDisplayName() ?? "未初始化"} | " +
                      $"{testModule.CurrentHealth}/{testModule.MaxHealth}HP | " +
                      $"攻:{testModule.Attack} | 盖伏:{testModule.IsFaceDown}");
        else
            Debug.Log("[Status] 模块未赋值或已摧毁，请在 Inspector 拖入新模块");

        if (testPlayer != null)
            Debug.Log($"[Status] {testPlayer.PlayerName} | " +
                      $"{testPlayer.TotalHealth}/{testPlayer.MaxHealth}HP | 死亡:{testPlayer.IsDead}");

        if (enemyPlayer != null)
            Debug.Log($"[Status] {enemyPlayer.PlayerName} | " +
                      $"{enemyPlayer.TotalHealth}/{enemyPlayer.MaxHealth}HP | 死亡:{enemyPlayer.IsDead}");
    }

    // ─────────────────────────────────────────────────
    // 新增功能
    // ─────────────────────────────────────────────────

    /// <summary>直接伤害敌方本体</summary>
    void DoEnemyDirectDamage(int amount)
    {
        if (enemyPlayer == null) { Debug.LogWarning("[Test] enemyPlayer 未赋值"); return; }
        enemyPlayer.TakeDamageDirectly(amount);
        Debug.Log($"[Test] 敌方本体 -{amount} → {enemyPlayer.TotalHealth}/{enemyPlayer.MaxHealth}");
    }

    /// <summary>对目标格子内的模块造成伤害</summary>
    void DoSlotModuleDamage(int amount)
    {
        DeployZone zone = targetIsPlayer ? playerDeployZone : aiDeployZone;
        if (zone == null) { Debug.LogWarning("[Test] 目标部署区未赋值"); return; }

        GameObject slotObj = zone.GetSlot(targetSlotIndex);
        if (slotObj == null) { Debug.LogWarning($"[Test] 格子 {targetSlotIndex} 不存在"); return; }

        DeploySlot slot = slotObj.GetComponent<DeploySlot>();
        ModuleInstance m = slot?.OccupyingModuleInstance;
        if (m == null || !m.IsAlive) { Debug.LogWarning($"[Test] 格子 {targetSlotIndex} 没有存活模块"); return; }

        int actual = m.TakeDamage(amount);
        Debug.Log($"[Test] 格子{targetSlotIndex} {m.Asset.GetDisplayName()} -{actual} → {m.CurrentHealth}/{m.MaxHealth}");
    }

    /// <summary>对目标无人机列的第一个无人机造成伤害</summary>
    void DoDroneDamage(int amount)
    {
        DroneDeployZone droneZone = droneTargetIsPlayer ? playerDroneZone : aiDroneZone;
        if (droneZone == null) { Debug.LogWarning("[Test] 目标无人机部署区未赋值"); return; }

        var drones = droneZone.GetDronesInColumn(targetDroneColumn);
        if (drones == null || drones.Count == 0) { Debug.LogWarning($"[Test] 列 {targetDroneColumn} 没有无人机"); return; }

        // 取第一个槽里的 DroneInstance
        GameObject slotGO = drones[0];
        if (slotGO == null) { Debug.LogWarning("[Test] 无人机槽对象为空"); return; }

        DroneInstance drone = slotGO.GetComponentInChildren<DroneInstance>();
        if (drone == null || !drone.IsAlive) { Debug.LogWarning("[Test] 找不到存活的 DroneInstance"); return; }

        int actual = drone.TakeDamage(amount);
        Debug.Log($"[Test] 列{targetDroneColumn} 无人机 -{actual} → {drone.CurrentHealth}/{drone.MaxHealth}");
    }

    // ─────────────────────────────────────────────────
    bool CheckModule()
    {
        if (testModule == null)
        {
            Debug.LogWarning("[Test] testModule 未赋值，请在 Inspector 拖入场上的模块");
            return false;
        }
        if (!testModule.IsAlive)
        {
            Debug.Log("[Test] 模块已摧毁，请在 Inspector 重新拖入新模块");
            testModule = null;
            return false;
        }
        return true;
    }
}