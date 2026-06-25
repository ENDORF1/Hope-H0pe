using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 战斗引擎。
/// 由 GameManager 在战斗阶段调用 RunCombatPhase()。
/// 
/// 每格流程（规则文档V4）：
///   1. 翻开 & 效果触发（激光、无人机母体）
///   2. 无人机攻击/治疗
///   3. 互相攻击（实弹/冷兵器）
///   4. 下一格
/// 导弹阶段（所有格结算后）：
///   所有已翻开的导弹同时齐射
/// </summary>
public class CombatEngine : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private DeployZone playerDeployZone;
    [SerializeField] private DeployZone aiDeployZone;
    [SerializeField] private PlayerState playerState;
    [SerializeField] private PlayerState aiState;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private DroneDeployZone playerDroneZone;
    [SerializeField] private DroneDeployZone aiDroneZone;

    [Header("无人机 Prefab")]
    [SerializeField] private GameObject dronePrefab;

    [Header("动画间隔")]
    [SerializeField] private float stepDelay      = 0.5f;
    [SerializeField] private float slotDelay      = 0.8f;
    [SerializeField] private float missileDelay   = 0.3f;

    [Header("近战动画")]
    [SerializeField] private float meleeWindbackDist = 0.25f;
    [SerializeField] private float meleeWindbackTime = 0.12f;
    [SerializeField] private float meleePunchDist    = 0.6f;
    [SerializeField] private float meleePunchOut     = 0.10f;
    [SerializeField] private float meleePunchReturn  = 0.12f;

    [Header("导弹飞行VFX")]
    [Tooltip("拖入 MissileProjectile Prefab")]
    [SerializeField] private GameObject missileProjectilePrefab;
    [Tooltip("玩家肖像位置（导弹命中本体时的目标点），拖入玩家肖像的Transform")]
    [SerializeField] private Transform playerPortraitTarget;
    [Tooltip("AI肖像位置（导弹命中本体时的目标点），拖入AI肖像的Transform")]
    [SerializeField] private Transform aiPortraitTarget;

    [Header("无人机生产动画")]
    [SerializeField] private float droneSpawnRiseDist    = 0.3f;   // 从母体飞起的距离（世界单位）
    [SerializeField] private float droneSpawnDropDist    = 0.2f;   // 向下移动的距离（世界单位）
    [SerializeField] private float droneSpawnRiseDur     = 0.25f;  // 飞起时长
    [SerializeField] private float droneSpawnShakeDur    = 0.15f;  // 抖动时长
    [SerializeField] private float droneSpawnTravelDur   = 0.3f;   // 平移到目标列时长
    [SerializeField] private float droneSpawnLandDur     = 0.2f;   // 降落时长
    [SerializeField] private float droneSpawnFadeInDur   = 0.25f;  // 槽位淡入时长

    // ─────────────────────────────────────────────────
    // 主入口（由 GameManager 调用）
    // ─────────────────────────────────────────────────

    public IEnumerator RunCombatPhase()
    {
        int slotCount = Mathf.Max(
            playerDeployZone != null ? playerDeployZone.CurrentSlotCount : 0,
            aiDeployZone     != null ? aiDeployZone.CurrentSlotCount     : 0
        );

        for (int i = 0; i < slotCount; i++)
        {
            if (IsGameOver()) yield break;

            // GameManager 时点④ BeforeSlot 已在 GameManager 处理
            yield return ResolveSlot(i);
            // GameManager 时点⑤ AfterSlot 已在 GameManager 处理

            yield return new WaitForSeconds(slotDelay);
        }
    }

    /// <summary>
    /// 供 QuickPlayExecutor 等外部调用：在指定模块上生成并附着一架无人机。
    /// </summary>
    public DroneInstance SpawnDroneOnModule(ModuleInstance target, DroneType type)
    {
        if (target == null || !target.IsAlive || target.IsFaceDown) return null;

        bool isPlayer     = (target.Owner == playerState);
        DroneDeployZone droneZone = isPlayer ? playerDroneZone : aiDroneZone;

        // 外部调用时启动协程但不等待动画（速攻牌等场景）
        StartCoroutine(SpawnAndAttachDrone(target, type, target.Asset, droneZone, target.transform.position));
        return null;
    }
    public void SetAllMissilesHighlight(bool on)
    {
        SetZoneMissilesHighlight(playerDeployZone, on);
        SetZoneMissilesHighlight(aiDeployZone,     on);
    }

    private void SetZoneMissilesHighlight(DeployZone zone, bool on)
    {
        if (zone == null) return;
        Color orange = new Color(1f, 0.55f, 0f, 1f);
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            ModuleInstance m = GetAliveModule(zone, i);
            if (m == null || m.Asset.ModuleType != ModuleType.Missile || m.IsFaceDown) continue;
            SetModuleGlow(m, on, orange);
        }
    }

    private void SetModuleGlow(ModuleInstance module, bool on, Color? color = null)
    {
        if (module == null) return;
        Transform glowT = module.transform.Find("Canvas/CardGlow");
        if (glowT == null) return;
        UnityEngine.UI.Image glow = glowT.GetComponent<UnityEngine.UI.Image>();
        if (glow == null) return;

        if (on)
        {
            Color c = color ?? new Color(0f, 1f, 0.9f, 1f);
            c.a = 0f;
            glow.color = c;
            glow.enabled = true;
            glow.DOKill();
            DOTween.To(() => glow.color.a, a => { var col = glow.color; col.a = a; glow.color = col; }, 1f, 0.2f);
        }
        else
        {
            glow.DOKill();
            DOTween.To(() => glow.color.a, a => { var col = glow.color; col.a = a; glow.color = col; }, 0f, 0.3f)
                .OnComplete(() => { if (glow != null) glow.enabled = false; });
        }
    }

    public IEnumerator RunMissilePhase()
    {
        yield return ResolveMissiles();
    }

    // ─────────────────────────────────────────────────
    // 单格结算
    // ─────────────────────────────────────────────────

    // 当前正在持续维持高亮的格子索引（-1 表示无）
    private int _sustainedHighlightIndex = -1;
    private Coroutine _sustainCoroutine = null;

    /// <summary>
    /// 启动持续高亮协程：每帧检查 glow 是否还亮，被意外关掉时立即重新点亮。
    /// 直到 StopSustainHighlight() 调用才停止。
    /// </summary>
    private void StartSustainHighlight(int index)
    {
        StopSustainHighlight(); // 先停掉上一个
        _sustainedHighlightIndex = index;
        _sustainCoroutine = StartCoroutine(SustainHighlightLoop(index));
    }

    private void StopSustainHighlight()
    {
        if (_sustainCoroutine != null)
        {
            StopCoroutine(_sustainCoroutine);
            _sustainCoroutine = null;
        }
        _sustainedHighlightIndex = -1;
    }

    private IEnumerator SustainHighlightLoop(int index)
    {
        while (true)
        {
            yield return null; // 每帧检查

            // 检查双方模块的 glow，如果 enabled=false 或 alpha 接近 0 则重新点亮
            CheckAndRestoreGlow(playerDeployZone, index);
            CheckAndRestoreGlow(aiDeployZone,     index);
        }
    }

    private void CheckAndRestoreGlow(DeployZone zone, int index)
    {
        if (zone == null) return;
        GameObject slotObj = zone.GetSlot(index);
        if (slotObj == null) return;
        DeploySlot slot = slotObj.GetComponent<DeploySlot>();
        if (slot == null || slot.OccupyingModule == null) return;

        Transform glowT = slot.OccupyingModule.transform.Find("Canvas/CardGlow");
        if (glowT == null) return;
        UnityEngine.UI.Image glow = glowT.GetComponent<UnityEngine.UI.Image>();
        if (glow == null) return;

        // 如果 glow 被关掉（enabled=false 且没有正在运行的淡入 tween），立即恢复
        if (!glow.enabled || glow.color.a < 0.05f)
        {
            ModuleInstance mi = slot.OccupyingModuleInstance;
            Color targetColor = (mi != null && IsCoolingDown(mi))
                ? new Color(1f, 0.15f, 0.15f, 1f)
                : new Color(0f, 1f, 0.9f, 1f);

            glow.DOKill(complete: false);
            targetColor.a = 1f; // 直接设为不透明，不再做淡入动画（持续维持阶段）
            glow.color    = targetColor;
            glow.enabled  = true;
        }
    }

    public IEnumerator ResolveSlot(int index)
    {
        ModuleInstance playerModule = GetModule(playerDeployZone, index);
        ModuleInstance aiModule     = GetModule(aiDeployZone,     index);

        if (playerModule == null && aiModule == null)
        {
            Debug.Log($"[Combat] 格子 {index}：双方无部件，跳过");
            yield break;
        }

        Debug.Log($"[Combat] ── 格子 {index} 开始结算 ──────────");

        // 启动持续高亮：整个格子结算期间持续维持 glow
        StartSustainHighlight(index);

        // 初始高亮（淡入动画）
        SetSlotHighlightWithMeleeCheck(playerDeployZone, playerModule, index);
        SetSlotHighlightWithMeleeCheck(aiDeployZone,     aiModule,     index);

        yield return ResolveFlipAndEffects(index, playerModule, aiModule);

        // 注意：互攻由 GameManager 在 AfterFlip 速攻窗口后调用 ResolveAttackPhase
    }

    /// <summary>
    /// 翻牌效果结算后、速攻窗口关闭后，由 GameManager 调用执行互攻阶段。
    /// </summary>
    public IEnumerator ResolveAttackPhase(int index)
    {
        if (IsGameOver()) { StopSustainHighlight(); ClearHighlight(index); yield break; }
        yield return new WaitForSeconds(stepDelay);

        ModuleInstance playerModule = GetAliveModule(playerDeployZone, index);
        ModuleInstance aiModule     = GetAliveModule(aiDeployZone,     index);

        // 持续高亮协程已在 ResolveSlot 启动，此处无需重复设置

        yield return new WaitForSeconds(stepDelay);

        yield return ResolveDrones(index, playerModule, aiModule);

        playerModule = GetAliveModule(playerDeployZone, index);
        aiModule     = GetAliveModule(aiDeployZone,     index);

        if (IsGameOver()) { StopSustainHighlight(); ClearHighlight(index); yield break; }
        yield return new WaitForSeconds(stepDelay);

        yield return ResolveMutualAttack(index, playerModule, aiModule);

        // 互攻结束：停止持续高亮，然后熄灭
        StopSustainHighlight();
        ClearHighlight(index);

        if (IsGameOver()) yield break;
    }

    private void SetSlotHighlight(DeployZone zone, int index, bool on, Color? color = null, bool skipIfActive = false)
    {
        if (zone == null) return;
        GameObject slotObj = zone.GetSlot(index);
        if (slotObj == null) return;

        DeploySlot slot = slotObj.GetComponent<DeploySlot>();
        if (slot == null || slot.OccupyingModule == null) return;

        Transform glowT = slot.OccupyingModule.transform.Find("Canvas/CardGlow");
        if (glowT == null) return;

        UnityEngine.UI.Image glow = glowT.GetComponent<UnityEngine.UI.Image>();
        if (glow == null) return;

        if (on)
        {
            // skipIfActive：仅当颜色已经是目标颜色时才跳过，避免覆盖正在播放的同色动画
            // 注意：颜色不同时（如橙→青）必须重新设置，不能跳过
            Color targetColor = color ?? new Color(0f, 1f, 0.9f, 1f);
            if (skipIfActive && glow.enabled)
            {
                // 只有颜色 RGB 一致时才跳过
                Color cur = glow.color;
                if (Mathf.Approximately(cur.r, targetColor.r) &&
                    Mathf.Approximately(cur.g, targetColor.g) &&
                    Mathf.Approximately(cur.b, targetColor.b))
                    return;
            }

            glow.DOKill(complete: false);
            targetColor.a = 0f;
            glow.color   = targetColor;
            glow.enabled = true;
            DOTween.To(() => glow.color.a, a => { var col = glow.color; col.a = a; glow.color = col; }, 1f, 0.2f)
                .SetTarget(glow);
        }
        else
        {
            glow.DOKill(complete: false);
            DOTween.To(() => glow.color.a, a => { var col = glow.color; col.a = a; glow.color = col; }, 0f, 0.3f)
                .SetTarget(glow)
                .OnComplete(() => { if (glow != null) glow.enabled = false; });
        }
    }

    private void ClearHighlight(int index)
    {
        SetSlotHighlight(playerDeployZone, index, false);
        SetSlotHighlight(aiDeployZone,     index, false);
    }

    /// <summary>
    /// 将指定格子的高亮从当前颜色（橙色）平滑过渡到霓虹青，
    /// 供 GameManager 在 BeforeSlot 窗口关闭后调用。
    /// </summary>
    public void TransitionSlotHighlight(int index)
    {
        TransitionZoneHighlight(playerDeployZone, index);
        TransitionZoneHighlight(aiDeployZone,     index);
    }

    private void TransitionZoneHighlight(DeployZone zone, int index)
    {
        if (zone == null) return;
        GameObject slotObj = zone.GetSlot(index);
        if (slotObj == null) return;
        DeploySlot slot = slotObj.GetComponent<DeploySlot>();
        if (slot == null || slot.OccupyingModule == null) return;

        Transform glowT = slot.OccupyingModule.transform.Find("Canvas/CardGlow");
        if (glowT == null) return;
        UnityEngine.UI.Image glow = glowT.GetComponent<UnityEngine.UI.Image>();
        if (glow == null) return;

        Color cyan = new Color(0f, 1f, 0.9f, 1f);
        glow.enabled = true;
        glow.DOKill();
        // R/G/B 分别 tween 到霓虹青，alpha 保持不变
        DOTween.To(() => glow.color.r, v => { var c = glow.color; c.r = v; glow.color = c; }, cyan.r, 0.3f);
        DOTween.To(() => glow.color.g, v => { var c = glow.color; c.g = v; glow.color = c; }, cyan.g, 0.3f);
        DOTween.To(() => glow.color.b, v => { var c = glow.color; c.b = v; glow.color = c; }, cyan.b, 0.3f);
    }

    /// <summary>
    /// 供 GameManager 在速攻窗口期间调用，将指定格子的高亮切换为橙色。
    /// 传入 on=false 时熄灭高亮。
    /// </summary>
    public void SetSlotWindowHighlight(int index, bool on)
    {
        Color orange = new Color(1f, 0.55f, 0f, 1f);
        SetSlotHighlight(playerDeployZone, index, on, orange);
        SetSlotHighlight(aiDeployZone,     index, on, orange);
    }

    // ─────────────────────────────────────────────────
    // 第1步：翻开 & 效果触发
    // ─────────────────────────────────────────────────

    private IEnumerator ResolveFlipAndEffects(int index,
        ModuleInstance playerModule, ModuleInstance aiModule)
    {
        bool playerFaceDown = playerModule != null && playerModule.IsFaceDown;
        bool aiFaceDown     = aiModule     != null && aiModule.IsFaceDown;

        // 情况A：双方都有部件
        if (playerModule != null && aiModule != null)
        {
            // 过热盖伏的模块本回合不翻开
            if (playerFaceDown && !playerModule.IsOverheated) playerModule.FlipFaceUp();
            if (aiFaceDown     && !aiModule.IsOverheated)     aiModule.FlipFaceUp();

            yield return new WaitForSeconds(stepDelay);

            // 双方都是同类型有翻开效果的模块时同时结算，否则顺序结算
            bool playerIsLaser     = !playerModule.IsFaceDown && playerModule.Asset?.ModuleType == ModuleType.Laser;
            bool aiIsLaser         = !aiModule.IsFaceDown     && aiModule.Asset?.ModuleType     == ModuleType.Laser;
            bool playerIsDroneHost = !playerModule.IsFaceDown && playerModule.Asset?.ModuleType == ModuleType.DroneHost;
            bool aiIsDroneHost     = !aiModule.IsFaceDown     && aiModule.Asset?.ModuleType     == ModuleType.DroneHost;

            bool shouldParallel = (playerIsLaser && aiIsLaser) || (playerIsDroneHost && aiIsDroneHost);

            if (shouldParallel)
            {
                bool c1Done = false, c2Done = false;
                StartCoroutine(RunAndFlag(TriggerFlipEffect(playerModule, aiModule, playerState, aiState), () => c1Done = true));
                StartCoroutine(RunAndFlag(TriggerFlipEffect(aiModule, playerModule, aiState, playerState), () => c2Done = true));
                yield return new WaitUntil(() => c1Done && c2Done);
            }
            else
            {
                if (!playerModule.IsFaceDown)
                    yield return TriggerFlipEffect(playerModule, aiModule, playerState, aiState);
                if (!aiModule.IsFaceDown)
                    yield return TriggerFlipEffect(aiModule, playerModule, aiState, playerState);
            }
        }
        // 情况B：一方已翻开，一方盖伏
        else if (playerModule != null && aiModule == null ||
                 playerModule == null && aiModule != null)
        {
            // 情况C 也在这里处理（对面无部件）
            ModuleInstance attacker = playerModule ?? aiModule;
            PlayerState    attackerOwner  = playerModule != null ? playerState : aiState;
            PlayerState    defenderPlayer = playerModule != null ? aiState     : playerState;

            bool wasDown = attacker.IsFaceDown;
            if (wasDown) attacker.FlipFaceUp();

            yield return new WaitForSeconds(stepDelay);

            if (attacker.IsAlive)
                yield return TriggerFlipEffect(attacker, null, attackerOwner, defenderPlayer);
        }
        // 情况B（严格）：双方都有，一方翻开一方盖伏
        // 上面已覆盖，这里无需额外处理
    }

    private IEnumerator RunAndFlag(IEnumerator coroutine, System.Action onDone)
    {
        yield return StartCoroutine(coroutine);
        onDone?.Invoke();
    }

    // ─────────────────────────────────────────────────
    // 翻开效果分发
    // ─────────────────────────────────────────────────

    private IEnumerator TriggerFlipEffect(ModuleInstance source, ModuleInstance target,
        PlayerState sourceOwner, PlayerState targetOwner)
    {
        if (source == null || !source.IsAlive) yield break;
        if (source.Asset == null) yield break;

        switch (source.Asset.ModuleType)
        {
            case ModuleType.Laser:
                yield return ResolveLaserFlip(source, target, targetOwner);
                break;

            case ModuleType.DroneHost:
                yield return ResolveDropHostFlip(source, sourceOwner);
                break;

            default:
                // 其他类型翻开无即时效果
                break;
        }
    }

    // 激光翻开伤害
    private IEnumerator ResolveLaserFlip(ModuleInstance laser,
        ModuleInstance target, PlayerState targetOwner)
    {
        int dmg = laser.Attack;
        Debug.Log($"[Combat] 激光 {laser.Asset.GetDisplayName()} 翻开，对 " +
                  $"{(target != null ? target.Asset.GetDisplayName() : "本体")} 造成 {dmg} 伤害");

        // ── 激光特效 ──────────────────────────────────
        if (LaserEffect.Instance != null)
        {
            Transform toTransform = target?.transform;
            Vector3   toWorld     = toTransform != null ? toTransform.position
                                  : (targetOwner != null && targetOwner.PortraitTransform != null
                                     ? targetOwner.PortraitTransform.position
                                     : laser.transform.position + Vector3.right * 2f);
            yield return StartCoroutine(
                LaserEffect.Instance.Play(laser.transform, toTransform, toWorld));
        }

        if (target != null && target.IsAlive)
        {
            target.TakeDamage(dmg);
            DamageEffect.Create(target.transform.position, dmg, target.MaxHealth);
            if (!target.IsAlive) yield return DestroyModuleVisual(target);
        }
        else
        {
            targetOwner?.TakeDamageDirectly(dmg);
        }

        yield return new WaitForSeconds(stepDelay);
    }

    // ─────────────────────────────────────────────────
    // 无人机母体翻开：生产无人机并吸附到母体自身
    // ─────────────────────────────────────────────────

    private IEnumerator ResolveDropHostFlip(ModuleInstance host, PlayerState hostOwner)
    {
        if (host == null || !host.IsAlive || host.Asset == null) yield break;

        int count = host.Asset.DronesToSpawn;
        if (count <= 0) yield break;

        DroneDeployZone droneZone = (hostOwner == playerState) ? playerDroneZone : aiDroneZone;

        Debug.Log($"[Combat] {host.Asset.GetDisplayName()} 无人机母体翻开，生产 {count} 架 {host.Asset.DroneType} 无人机");

        for (int i = 0; i < count; i++)
        {
            StartCoroutine(SpawnAndAttachDrone(host, host.Asset.DroneType, host.Asset, droneZone, host.transform.position));
            if (i < count - 1)
                yield return new WaitForSeconds(0.15f);
        }

        // 等待动画大致完成再继续
        float animTotalDur = droneSpawnRiseDur + droneSpawnShakeDur + droneSpawnRiseDur * 0.5f
                           + droneSpawnTravelDur + droneSpawnLandDur + droneSpawnFadeInDur;
        yield return new WaitForSeconds(animTotalDur + 0.15f * (count - 1));

        yield return new WaitForSeconds(stepDelay);
    }

    /// <summary>实例化一架无人机并附着到目标模块</summary>
    /// <summary>
    /// 根据无人机类型从己方部署区选择最优吸附目标。
    /// 找不到优先目标时返回 null（调用方保持原始 target）。
    /// </summary>
    private ModuleInstance SelectDroneTarget(DroneType type, ModuleInstance host, DeployZone zone)
    {
        if (zone == null) return null;
        int currentSlot = SceneRefs.Instance?.GameManager?.CurrentCombatSlot ?? -1;

        var candidates = new System.Collections.Generic.List<ModuleInstance>();
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            ModuleInstance m = GetAliveModule(zone, i);
            if (m == null || m.IsFaceDown) continue;
            candidates.Add(m);
        }
        if (candidates.Count == 0) return null;

        switch (type)
        {
            case DroneType.Heal:
            {
                // 优先血量最低的受损模块
                ModuleInstance best = null;
                int lowestHp = int.MaxValue;
                foreach (var m in candidates)
                {
                    if (m.CurrentHealth < m.MaxHealth && m.CurrentHealth < lowestHp)
                    {
                        lowestHp = m.CurrentHealth;
                        best = m;
                    }
                }
                if (best != null) return best;
                // 回退：随机已翻开模块
                return candidates[Random.Range(0, candidates.Count)];
            }

            case DroneType.Attack:
            {
                // 优先 SlotIndex >= currentSlot 的模块（含本格及之后的未结算模块）
                var preferred = new System.Collections.Generic.List<ModuleInstance>();
                foreach (var m in candidates)
                    if (m.SlotIndex >= currentSlot) preferred.Add(m);
                if (preferred.Count > 0) return preferred[Random.Range(0, preferred.Count)];
                // 回退：随机已翻开模块
                return candidates[Random.Range(0, candidates.Count)];
            }

            case DroneType.Builder:
            {
                // 优先 SlotIndex < currentSlot 的已结算模块
                var preferred = new System.Collections.Generic.List<ModuleInstance>();
                foreach (var m in candidates)
                    if (m.SlotIndex < currentSlot) preferred.Add(m);
                if (preferred.Count > 0) return preferred[Random.Range(0, preferred.Count)];
                // 回退：随机已翻开模块
                return candidates[Random.Range(0, candidates.Count)];
            }

            default:
                return null;
        }
    }

    private IEnumerator SpawnAndAttachDrone(ModuleInstance target, DroneType type,
        CardAsset hostAsset, DroneDeployZone droneZone, Vector3 hostWorldPos)
    {
        if (target == null || !target.IsAlive) yield break;
        if (dronePrefab == null)
        {
            Debug.LogError("[CombatEngine] dronePrefab 未赋值！");
            yield break;
        }

        // 根据无人机类型选择最优吸附目标
        DeployZone ownerZone = (target.Owner == playerState) ? playerDeployZone : aiDeployZone;
        ModuleInstance bestTarget = SelectDroneTarget(type, target, ownerZone);
        if (bestTarget != null) target = bestTarget;

        // 优先从母体的 DroneAsset 读数据，没有则回退到母体自身字段
        CardAsset droneAsset = hostAsset.DroneAsset != null ? hostAsset.DroneAsset : hostAsset;

        // 从母体位置生成无人机，初始透明
        GameObject droneObj = Instantiate(dronePrefab, hostWorldPos, Quaternion.identity);

        // 挂载 OCM 并读取无人机 Asset
        OneCardManager ocm = droneObj.GetComponent<OneCardManager>();
        if (ocm != null)
        {
            ocm.cardAsset = droneAsset;
            ocm.ReadCardFromAsset();
        }

        // 挂载自定义脚本
        if (!string.IsNullOrEmpty(droneAsset.DroneScriptName))
        {
            System.Type scriptType = System.Type.GetType(droneAsset.DroneScriptName);
            if (scriptType != null && typeof(MonoBehaviour).IsAssignableFrom(scriptType))
                droneObj.AddComponent(scriptType);
            else
                Debug.LogWarning($"[CombatEngine] DroneScriptName '{droneAsset.DroneScriptName}' 找不到对应脚本类型");
        }

        DroneInstance drone = droneObj.GetComponent<DroneInstance>();
        if (drone == null) drone = droneObj.AddComponent<DroneInstance>();

        drone.Initialize(
            type,
            droneAsset.DroneAttack,
            droneAsset.DroneHeal,
            droneAsset.DroneHealth > 0 ? droneAsset.DroneHealth : 1,
            target
        );

        target.AttachDrone(drone);

        // ── 飞行动画 ──────────────────────────────────
        // 1. 从母体位置飞起
        Vector3 risePos = hostWorldPos + Vector3.up * droneSpawnRiseDist;
        yield return droneObj.transform.DOMove(risePos, droneSpawnRiseDur)
            .SetEase(Ease.OutQuad).WaitForCompletion();

        // 2. 抖动
        yield return droneObj.transform
            .DOShakePosition(droneSpawnShakeDur, 0.05f, 10, 90f, false, true)
            .WaitForCompletion();

        // 3. 向下移动一些
        Vector3 dropPos = risePos + Vector3.down * droneSpawnDropDist;
        yield return droneObj.transform.DOMove(dropPos, droneSpawnRiseDur * 0.5f)
            .SetEase(Ease.InOutQuad).WaitForCompletion();

        // 4. 平移到目标列的X位置（保持当前Y）
        Vector3 targetWorldPos = target.transform.position;
        Vector3 travelPos = new Vector3(targetWorldPos.x, dropPos.y, dropPos.z);
        yield return droneObj.transform.DOMove(travelPos, droneSpawnTravelDur)
            .SetEase(Ease.InOutQuad).WaitForCompletion();

        // 5. 降落到目标位置
        yield return droneObj.transform.DOMove(targetWorldPos, droneSpawnLandDur)
            .SetEase(Ease.InQuad).WaitForCompletion();

        // 6. 注册到 DroneDeployZone，槽位淡入
        if (droneZone != null)
        {
            GameObject slot = droneZone.AddDroneToColumn(target.SlotIndex, droneObj);
            if (slot != null)
            {
                CanvasGroup cg = slot.GetComponent<CanvasGroup>();
                if (cg == null) cg = slot.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                yield return cg.DOFade(1f, droneSpawnFadeInDur).WaitForCompletion();
            }
        }

        Debug.Log($"[CombatEngine] 生成 {type} 无人机（{droneAsset.GetDisplayName()}），附着到 {target.Asset.GetDisplayName()} 格{target.SlotIndex}");
    }

    // ─────────────────────────────────────────────────
    // 第2步：无人机攻击/治疗/建造
    // ─────────────────────────────────────────────────

    private IEnumerator ResolveDrones(int index,
        ModuleInstance playerModule, ModuleInstance aiModule)
    {
        // 伤害型：双方同时
        bool a1 = false, a2 = false;
        StartCoroutine(RunAndFlag(ResolveDroneAttacks(playerModule, aiModule), () => a1 = true));
        StartCoroutine(RunAndFlag(ResolveDroneAttacks(aiModule, playerModule), () => a2 = true));
        yield return new WaitUntil(() => a1 && a2);

        // 治疗型：双方同时（无协程，直接调用）
        ResolveDroneHeals(playerModule);
        ResolveDroneHeals(aiModule);

        // 建造型：双方同时
        bool b1 = false, b2 = false;
        StartCoroutine(RunAndFlag(ResolveDroneBuilders(playerModule, playerDeployZone, playerDroneZone), () => b1 = true));
        StartCoroutine(RunAndFlag(ResolveDroneBuilders(aiModule,     aiDeployZone,     aiDroneZone),     () => b2 = true));
        yield return new WaitUntil(() => b1 && b2);

        // 清理死亡无人机引用
        playerModule?.PurgeDeadDrones();
        aiModule?.PurgeDeadDrones();

        yield return new WaitForSeconds(stepDelay);
    }

    /// <summary>执行 attacker 模块上所有伤害型无人机，攻击 defender（或本体）</summary>
    private IEnumerator ResolveDroneAttacks(ModuleInstance attacker, ModuleInstance defender)
    {
        if (attacker == null || !attacker.IsAlive) yield break;
        if (attacker.IsFaceDown) yield break; // 盖伏中不生效

        foreach (DroneInstance drone in new List<DroneInstance>(attacker.Drones))
        {
            if (drone == null || !drone.IsAlive) continue;
            if (drone.Type != DroneType.Attack) continue;

            int dmg = drone.Attack;

            if (defender != null && defender.IsAlive)
            {
                Debug.Log($"[Combat] 伤害型无人机攻击 {defender.Asset.GetDisplayName()} {dmg} 点");
                defender.TakeDamage(dmg);
                DamageEffect.Create(defender.transform.position, dmg, defender.MaxHealth);
                if (!defender.IsAlive)
                    yield return DestroyModuleVisual(defender);
            }
            else
            {
                // 对位无模块，攻击敌方本体
                PlayerState enemyState = (attacker.Owner == playerState) ? aiState : playerState;
                Debug.Log($"[Combat] 伤害型无人机直攻敌方本体 {dmg} 点");
                enemyState?.TakeDamageDirectly(dmg);
            }

            if (IsGameOver()) yield break;
        }
    }

    /// <summary>执行 module 上所有治疗型无人机</summary>
    private void ResolveDroneHeals(ModuleInstance module)
    {
        if (module == null || !module.IsAlive) return;
        if (module.IsFaceDown) return; // 盖伏中不生效

        foreach (DroneInstance drone in new List<DroneInstance>(module.Drones))
        {
            if (drone == null || !drone.IsAlive) continue;
            if (drone.Type != DroneType.Heal) continue;

            int healed = module.Heal(drone.HealAmount);
            if (healed > 0)
            {
                Debug.Log($"[Combat] 治疗型无人机治疗 {module.Asset.GetDisplayName()} {healed} 点");
                DamageEffect.Create(module.transform.position, -healed);
            }
        }
    }

    /// <summary>执行 module 上所有建造型无人机：增加格子后消失</summary>
    private IEnumerator ResolveDroneBuilders(ModuleInstance module,
        DeployZone deployZone, DroneDeployZone droneZone)
    {
        if (module == null || !module.IsAlive) yield break;
        if (module.IsFaceDown) yield break;

        foreach (DroneInstance drone in new List<DroneInstance>(module.Drones))
        {
            if (drone == null || !drone.IsAlive) continue;
            if (drone.Type != DroneType.Builder) continue;

            int newIndex = deployZone.AddSlot();
            if (newIndex >= 0)
            {
                droneZone?.AddColumn();
                Debug.Log($"[Combat] 建造型无人机触发，新增格子索引 {newIndex}");
            }
            else
            {
                Debug.Log("[Combat] 建造型无人机触发，但格子已达上限");
            }

            // 建造型一次性，消耗后销毁（同时移除无人机部署格）
            drone.MarkConsumed();
            module.RemoveDrone(drone);
            if (drone.gameObject != null)
            {
                Transform slot = drone.transform.parent;
                if (slot != null)
                {
                    DroneDeployZone dz = slot.GetComponentInParent<DroneDeployZone>();
                    if (dz != null)
                        dz.RemoveDroneSlot(module.SlotIndex, slot.gameObject);
                    else
                        Destroy(slot.gameObject);
                }
                else
                {
                    Destroy(drone.gameObject);
                }
            }

            yield return new WaitForSeconds(stepDelay);
        }
    }

    // ─────────────────────────────────────────────────
    // 第3步：互相攻击（实弹/冷兵器）
    // ─────────────────────────────────────────────────

    private IEnumerator ResolveMutualAttack(int index,
        ModuleInstance playerModule, ModuleInstance aiModule)
    {
        int playerDmg = 0;
        int aiDmg     = 0;
        bool playerCanAttack = CanMeleeAttack(playerModule);
        bool aiCanAttack     = CanMeleeAttack(aiModule);

        if (playerCanAttack) playerDmg = playerModule.Attack;
        if (aiCanAttack)     aiDmg     = aiModule.Attack;

        // ── 近战弹出特效：先回缩蓄力，再向对方冲刺弹回（仅 Melee）──
        bool playerIsMelee = playerCanAttack && playerModule.Asset.ModuleType == ModuleType.Melee;
        bool aiIsMelee     = aiCanAttack     && aiModule    .Asset.ModuleType == ModuleType.Melee;

        if (playerIsMelee)
        {
            Vector3 dir    = aiModule != null
                ? (aiModule.transform.position - playerModule.transform.position).normalized
                : Vector3.up;
            Vector3 origin = playerModule.transform.position;
            playerModule.transform
                .DOMove(origin - dir * meleeWindbackDist, meleeWindbackTime).SetEase(Ease.OutQuad)
                .OnComplete(() => playerModule.transform
                    .DOMove(origin + dir * meleePunchDist, meleePunchOut).SetEase(Ease.InQuad)
                    .OnComplete(() => playerModule.transform
                        .DOMove(origin, meleePunchReturn).SetEase(Ease.OutQuad)));
        }

        if (aiIsMelee)
        {
            Vector3 dir    = playerModule != null
                ? (playerModule.transform.position - aiModule.transform.position).normalized
                : Vector3.down;
            Vector3 origin = aiModule.transform.position;
            aiModule.transform
                .DOMove(origin - dir * meleeWindbackDist, meleeWindbackTime).SetEase(Ease.OutQuad)
                .OnComplete(() => aiModule.transform
                    .DOMove(origin + dir * meleePunchDist, meleePunchOut).SetEase(Ease.InQuad)
                    .OnComplete(() => aiModule.transform
                        .DOMove(origin, meleePunchReturn).SetEase(Ease.OutQuad)));
        }

        // 有近战动画则等蓄力+冲刺到位再结算，否则直接结算
        if (playerIsMelee || aiIsMelee)
            yield return new WaitForSeconds(meleeWindbackTime + meleePunchOut);

        // 屏幕震动（仅近战）
        if (playerIsMelee || aiIsMelee)
            Camera.main?.transform.DOShakePosition(0.18f, 12f, 10, 90f, false, true);

        // ── 同时结算伤害 ─────────────────────────────
        if (playerDmg > 0)
        {
            if (aiModule != null && aiModule.IsAlive)
            {
                Debug.Log($"[Combat] 玩家 {playerModule.Asset.GetDisplayName()} 攻击 AI {aiModule.Asset.GetDisplayName()} {playerDmg}点");
                aiModule.TakeDamage(playerDmg);
                DamageEffect.Create(aiModule.transform.position, playerDmg, aiModule.MaxHealth);
            }
            else
            {
                Debug.Log($"[Combat] 玩家 {playerModule.Asset.GetDisplayName()} 直攻 AI 本体 {playerDmg}点");
                aiState?.TakeDamageDirectly(playerDmg);
            }
        }

        if (aiDmg > 0)
        {
            if (playerModule != null && playerModule.IsAlive)
            {
                Debug.Log($"[Combat] AI {aiModule.Asset.GetDisplayName()} 攻击玩家 {playerModule.Asset.GetDisplayName()} {aiDmg}点");
                playerModule.TakeDamage(aiDmg);
                DamageEffect.Create(playerModule.transform.position, aiDmg, playerModule.MaxHealth);
            }
            else
            {
                Debug.Log($"[Combat] AI {aiModule.Asset.GetDisplayName()} 直攻玩家本体 {aiDmg}点");
                playerState?.TakeDamageDirectly(aiDmg);
            }
        }

        // 冷兵器攻击后进入冷却，刷新发光颜色为红色
        if (playerIsMelee && playerModule.IsAlive)
        {
            playerModule.StartCooldown();
            SetSlotHighlightWithMeleeCheck(playerDeployZone, playerModule, index);
        }
        if (aiIsMelee && aiModule.IsAlive)
        {
            aiModule.StartCooldown();
            SetSlotHighlightWithMeleeCheck(aiDeployZone, aiModule, index);
        }

        // 等弹回完成
        if (playerIsMelee || aiIsMelee)
            yield return new WaitForSeconds(meleePunchReturn);

        // 死亡模块销毁
        if (playerModule != null && !playerModule.IsAlive)
            yield return DestroyModuleVisual(playerModule);
        if (aiModule != null && !aiModule.IsAlive)
            yield return DestroyModuleVisual(aiModule);
    }

    // 判断模块是否可以在互相攻击阶段攻击
    private bool CanMeleeAttack(ModuleInstance m)
    {
        if (m == null || !m.IsAlive) return false;
        if (m.Asset == null) return false;
        if (m.IsFaceDown) return false; // 盖伏模块不能攻击

        switch (m.Asset.ModuleType)
        {
            case ModuleType.Ballistic:
                return true;
            case ModuleType.Melee:
                return m.CanMeleeAttack;
            default:
                return false;
        }
    }

    /// <summary>冷兵器处于冷却中（存活但本回合不能攻击）</summary>
    private bool IsCoolingDown(ModuleInstance m)
    {
        if (m == null || !m.IsAlive) return false;
        if (m.Asset == null) return false;
        return m.Asset.ModuleType == ModuleType.Melee && !m.CanMeleeAttack;
    }

    private void SetSlotHighlightWithMeleeCheck(DeployZone zone, ModuleInstance module, int index)
    {
        if (IsCoolingDown(module))
            SetSlotHighlight(zone, index, true, new Color(1f, 0.15f, 0.15f, 1f));
        else
            SetSlotHighlight(zone, index, true, skipIfActive: true);
    }

    // ─────────────────────────────────────────────────
    // 导弹阶段
    // ─────────────────────────────────────────────────

    private IEnumerator ResolveMissiles()
    {
        int slotCount = Mathf.Max(
            playerDeployZone != null ? playerDeployZone.CurrentSlotCount : 0,
            aiDeployZone     != null ? aiDeployZone.CurrentSlotCount     : 0
        );

        bool anyMissile = false;

        for (int i = 0; i < slotCount; i++)
        {
            ModuleInstance playerMissile = GetMissileAt(playerDeployZone, i);
            ModuleInstance aiMissile     = GetMissileAt(aiDeployZone,     i);

            if (playerMissile == null && aiMissile == null) continue;

            anyMissile = true;
            Debug.Log($"[Combat] 导弹阶段：格子 {i} 双方同时发射");

            // 双方导弹同时启动，等两个都完成
            bool playerDone = playerMissile == null;
            bool aiDone     = aiMissile     == null;

            if (playerMissile != null && playerMissile.IsAlive)
                StartCoroutine(RunAndFlag(FireMissile(playerMissile, aiDeployZone, aiState), () => playerDone = true));
            else
                playerDone = true;

            if (aiMissile != null && aiMissile.IsAlive)
                StartCoroutine(RunAndFlag(FireMissile(aiMissile, playerDeployZone, playerState), () => aiDone = true));
            else
                aiDone = true;

            yield return new WaitUntil(() => playerDone && aiDone);
            yield return new WaitForSeconds(missileDelay);
        }

        if (!anyMissile)
            Debug.Log("[Combat] 导弹阶段：无导弹");
    }

    private ModuleInstance GetMissileAt(DeployZone zone, int index)
    {
        if (zone == null) return null;
        ModuleInstance m = GetAliveModule(zone, index);
        if (m != null && m.Asset.ModuleType == ModuleType.Missile && !m.IsFaceDown)
            return m;
        return null;
    }

    private IEnumerator FireMissile(ModuleInstance missile,
        DeployZone enemyZone, PlayerState enemyPlayer)
    {
        // 霓虹青高亮：此导弹正在触发
        SetModuleGlow(missile, true, new Color(0f, 1f, 0.9f, 1f));
        yield return new WaitForSeconds(0.2f);

        int shotCount = Mathf.Max(1, missile.Asset.MissileCount);

        for (int shot = 0; shot < shotCount; shot++)
        {
            if (!missile.IsAlive) break;

            // 每发独立重建随机池（前几发可能摧毁了目标）
            // 规则：有模块的格子各占一个位置，空格各贡献一次本体机会
            List<int> targetSlots = new List<int>();
            int emptySlotCount = 0;

            int slotCount = enemyZone != null ? enemyZone.CurrentSlotCount : 0;
            for (int i = 0; i < slotCount; i++)
            {
                ModuleInstance m = GetAliveModule(enemyZone, i);
                if (m != null)
                    targetSlots.Add(i);
                else
                    emptySlotCount++;
            }

            // 本体加入随机池的次数 = 空格数量
            int totalTargets = targetSlots.Count + emptySlotCount;
            if (totalTargets == 0)
            {
                Debug.Log($"[Combat] 导弹 {missile.Asset.GetDisplayName()} 第{shot+1}发无有效目标");
                break;
            }

            int roll = Random.Range(0, totalTargets);

            if (roll >= targetSlots.Count)
            {
                Debug.Log($"[Combat] 导弹第{shot+1}发命中敌方本体 {missile.Attack} 点");

                // 命中本体飞行动画，落地时结算伤害并显示数字
                Transform portraitTarget = (enemyPlayer == playerState) ? playerPortraitTarget : aiPortraitTarget;
                Vector3 portraitPos = portraitTarget != null
                    ? portraitTarget.position
                    : (enemyPlayer?.PortraitTransform != null ? enemyPlayer.PortraitTransform.position : missile.transform.position);
                if (missileProjectilePrefab != null)
                {
                    MissileModuleVFX launcherVFX = missile.GetComponent<MissileModuleVFX>();
                    Vector3[] muzzles = launcherVFX != null
                        ? launcherVFX.GetTubeMuzzlePositions()
                        : new Vector3[] { missile.transform.position };
                    Vector3 startPos  = muzzles[shot % muzzles.Length];
                    Vector3 targetPos = portraitPos;
                    int dmg = missile.Attack;
                    GameObject proj = Instantiate(missileProjectilePrefab, startPos, Quaternion.identity);
                    MissileProjectile mp = proj.GetComponent<MissileProjectile>();
                    if (mp != null) yield return mp.Launch(startPos, targetPos, missile.transform.forward,
                        hitPos =>
                        {
                            if (enemyPlayer != null) enemyPlayer.SuppressDamageEffect = true;
                            enemyPlayer?.TakeDamageDirectly(dmg);
                            if (enemyPlayer != null) enemyPlayer.SuppressDamageEffect = false;
                            DamageEffect.Create(hitPos, dmg, 0);
                        });
                }
                else
                {
                    // 无弹体时直接结算
                    enemyPlayer?.TakeDamageDirectly(missile.Attack);
                }
            }
            else
            {
                int hitSlot = targetSlots[roll];
                ModuleInstance hitModule = GetAliveModule(enemyZone, hitSlot);

                Debug.Log($"[Combat] 导弹第{shot+1}发命中格子 {hitSlot} " +
                          $"{(hitModule != null ? hitModule.Asset.GetDisplayName() : "空")} {missile.Attack} 点");

                // 命中模块飞行动画，落地时结算伤害并显示数字
                if (missileProjectilePrefab != null)
                {
                    MissileModuleVFX launcherVFX = missile.GetComponent<MissileModuleVFX>();
                    Vector3[] muzzles = launcherVFX != null
                        ? launcherVFX.GetTubeMuzzlePositions()
                        : new Vector3[] { missile.transform.position };
                    Vector3 startPos  = muzzles[shot % muzzles.Length];
                    ModuleInstance hitModule2 = hitModule;
                    Transform portraitTarget2 = (enemyPlayer == playerState) ? playerPortraitTarget : aiPortraitTarget;
                    Vector3 targetPos = hitModule2 != null
                        ? hitModule2.transform.position
                        : (portraitTarget2 != null
                            ? portraitTarget2.position
                            : (enemyPlayer?.PortraitTransform != null
                                ? enemyPlayer.PortraitTransform.position
                                : missile.transform.position + Vector3.up * 2f));
                    int dmg = missile.Attack;
                    GameObject proj = Instantiate(missileProjectilePrefab, startPos, Quaternion.identity);
                    MissileProjectile mp = proj.GetComponent<MissileProjectile>();
                    if (mp != null) yield return mp.Launch(startPos, targetPos, missile.transform.forward,
                        hitPos =>
                        {
                            if (hitModule2 != null && hitModule2.IsAlive)
                            {
                                hitModule2.TakeDamage(dmg);
                                DamageEffect.Create(hitPos, dmg, hitModule2.MaxHealth);
                            }
                        });
                }
                else
                {
                    // 无弹体时直接结算
                    if (hitModule != null)
                    {
                        hitModule.TakeDamage(missile.Attack);
                        DamageEffect.Create(hitModule.transform.position, missile.Attack, hitModule.MaxHealth);
                    }
                }

                // 死亡判断和溅射在弹体落地后执行
                if (hitModule != null && !hitModule.IsAlive)
                    yield return DestroyModuleVisual(hitModule);
                yield return ResolveSplash(missile, enemyZone, hitSlot);
            }

            if (shot < shotCount - 1)
                yield return new WaitForSeconds(missileDelay);
        }

        // 所有发射完毕：熄灭霓虹青
        SetModuleGlow(missile, false);

        yield return new WaitForSeconds(stepDelay);
    }

    // 导弹溅射
    private IEnumerator ResolveSplash(ModuleInstance missile,
        DeployZone enemyZone, int hitSlot)
    {
        int splashDmg   = missile.Asset.SplashDamage;
        int splashRange = missile.Asset.SplashRange;

        if (splashDmg <= 0 || splashRange <= 0) yield break;

        int maxSlot = enemyZone.CurrentSlotCount - 1;

        for (int offset = 1; offset <= splashRange; offset++)
        {
            // 左侧
            int leftSlot = hitSlot - offset;
            if (leftSlot >= 0)
            {
                ModuleInstance left = GetAliveModule(enemyZone, leftSlot);
                if (left != null)
                {
                    Debug.Log($"[Combat] 溅射命中左侧格 {leftSlot} {left.Asset.GetDisplayName()} {splashDmg} 点");
                    left.TakeDamage(splashDmg);
                    DamageEffect.Create(left.transform.position, splashDmg, left.MaxHealth);
                    if (!left.IsAlive) yield return DestroyModuleVisual(left);
                }
            }

            // 右侧
            int rightSlot = hitSlot + offset;
            if (rightSlot <= maxSlot)
            {
                ModuleInstance right = GetAliveModule(enemyZone, rightSlot);
                if (right != null)
                {
                    Debug.Log($"[Combat] 溅射命中右侧格 {rightSlot} {right.Asset.GetDisplayName()} {splashDmg} 点");
                    right.TakeDamage(splashDmg);
                    DamageEffect.Create(right.transform.position, splashDmg, right.MaxHealth);
                    if (!right.IsAlive) yield return DestroyModuleVisual(right);
                }
            }
        }
    }

    // ─────────────────────────────────────────────────
    // 模块死亡视觉：从上往下溶解消失
    // ─────────────────────────────────────────────────

    private IEnumerator DestroyModuleVisual(ModuleInstance module)
    {
        if (module == null) yield break;
        GameObject go = module.gameObject;
        if (go == null) yield break;

        ModuleDeathEffect effect = go.GetComponent<ModuleDeathEffect>();
        if (effect != null)
        {
            yield return StartCoroutine(effect.PlayAndDestroy());
        }
        else
        {
            // 兜底：没有挂脚本时直接销毁
            Debug.LogWarning("[CombatEngine] ModuleDeathEffect 未挂载，直接销毁。");
            Destroy(go);
        }
    }

    // ─────────────────────────────────────────────────
    // 辅助方法
    // ─────────────────────────────────────────────────

    private ModuleInstance GetModule(DeployZone zone, int index)
    {
        if (zone == null) return null;
        GameObject slotObj = zone.GetSlot(index);
        if (slotObj == null) return null;
        DeploySlot slot = slotObj.GetComponent<DeploySlot>();
        return slot?.OccupyingModuleInstance;
    }

    private ModuleInstance GetAliveModule(DeployZone zone, int index)
    {
        ModuleInstance m = GetModule(zone, index);
        return (m != null && m.IsAlive) ? m : null;
    }

    private bool IsGameOver()
    {
        if (playerState != null && playerState.IsDead) return true;
        if (aiState     != null && aiState.IsDead)     return true;
        return false;
    }
}