using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 根据 CardAsset.Effects 列表逐条执行效果。
/// 无状态，可复用（AI 也调用同一个 Executor）。
/// </summary>
public class QuickPlayExecutor : MonoBehaviour
{
    // 拖入场景中的 DialogueManager，用于在打出速攻牌时触发角色对话
    [SerializeField] private DialogueManager dialogueManager;

    private DialogueManager GetDialogueManager()
        => dialogueManager != null ? dialogueManager : SceneRefs.Instance?.GameManager?.DialogueManager;

    /// <summary>
    /// 执行一张速攻牌的全部效果。
    /// target = 玩家拖拽指定的目标对象（无目标牌传 null）。
    /// </summary>
    public void Execute(
        CardAsset          card,
        GameObject         target,
        PlayerState        casterPlayer,
        PlayerState        enemyPlayer,
        DeployZone         casterZone,
        DeployZone         enemyZone,
        HandManager        casterHand,
        DeckManager        casterDeck = null)
    {
        if (card == null) return;

        foreach (var effect in card.Effects)
            ExecuteEffect(effect, target, casterPlayer, enemyPlayer,
                          casterZone, enemyZone, casterHand, casterDeck, card);

        // 效果执行完毕后，触发打牌对话
        // Custom 效果已在 ExecuteCustom 内触发，跳过避免重复
        bool hasCustom = false;
        foreach (var effect in card.Effects)
            if (effect.EffectType == EffectType.Custom) { hasCustom = true; break; }
        if (!hasCustom)
            GetDialogueManager()?.PlayCardDialogue(card);
    }

    // ─────────────────────────────────────────────────
    private void ExecuteEffect(
        CardEffect  e,
        GameObject  target,
        PlayerState caster,
        PlayerState enemy,
        DeployZone  casterZone,
        DeployZone  enemyZone,
        HandManager casterHand,
        DeckManager casterDeck,
        CardAsset   card = null)
    {
        switch (e.EffectType)
        {
            // ── 伤害 ──────────────────────────────────
            case EffectType.DealDamage:
                DealDamage(e, target, enemy, enemyZone); break;

            case EffectType.DealDamageRandom:
                int dmg = Random.Range(e.IntValue, e.IntValue2 + 1);
                DealDamageToTarget(target, enemy, dmg); break;

            case EffectType.DealDamagePerModule:
                int count = CountAliveModules(casterZone);
                DealDamageToTarget(target, enemy, e.IntValue * count); break;

            // ── 治疗 ──────────────────────────────────
            case EffectType.Heal:
                HealTarget(e, target, caster, casterZone); break;

            case EffectType.HealPerDrone:
                int droneCount = CountDrones(casterZone);
                HealTargetAmount(target, caster, e.IntValue * droneCount); break;

            // ── 模块操作 ──────────────────────────────
            case EffectType.Overheat:
                OverheatTarget(target); break;

            case EffectType.DestroyModule:
                DestroyModuleTarget(target, e.Target, caster, enemy,
                                    casterZone, enemyZone); break;

            case EffectType.DestroyAllEnemyModules:
                DestroyAllModules(enemyZone); break;

            // ── 抽牌 ──────────────────────────────────
            case EffectType.Draw:
                DrawCards(casterDeck, e.IntValue); break;

            case EffectType.DrawAll:
                DrawAllCards(casterDeck); break;

            // ── 手牌/牌库操作 ─────────────────────────
            case EffectType.DestroyEnemyHand:
                // TODO: 清除敌方手牌
                Debug.Log($"[Executor] DestroyEnemyHand — 待接入"); break;

            case EffectType.DestroyEnemyDeck:
                Debug.Log($"[Executor] DestroyEnemyDeck — 待接入"); break;

            case EffectType.DestroyEnemyDeploy:
                DestroyAllModules(enemyZone);
                Debug.Log($"[Executor] DestroyEnemyDeploy（手牌部分）— 待接入"); break;

            case EffectType.StealCard:
                Debug.Log($"[Executor] StealCard — 待接入"); break;

            case EffectType.Discard:
                Debug.Log($"[Executor] Discard — 待接入"); break;

            // ── 无人机 ────────────────────────────────
            case EffectType.SpawnDrones:
                SpawnDrones(e, target, casterZone); break;

            case EffectType.DestroyDrones:
                DestroyDrones(e, target, casterZone, enemyZone); break;

            // ── 特殊 ──────────────────────────────────
            case EffectType.RevealEnemyField:
                RevealEnemyField(enemyZone); break;

            case EffectType.ExtraTurn:
                Debug.Log($"[Executor] ExtraTurn — 待接入"); break;

            case EffectType.ImmunityToPlayer:
                Debug.Log($"[Executor] ImmunityToPlayer — 待接入"); break;

            case EffectType.StealHealth:
                StealHealth(e, caster, enemy, casterZone, enemyZone); break;

            case EffectType.Custom:
                ExecuteCustom(e, target, caster, enemy, casterZone, enemyZone, casterHand, casterDeck, card); break;
        }
    }

    // ─────────────────────────────────────────────────
    // 伤害
    // ─────────────────────────────────────────────────

    private void DealDamage(CardEffect e, GameObject target,
        PlayerState enemy, DeployZone enemyZone)
    {
        switch (e.Target)
        {
            case QuickPlayTarget.EnemyModule:
            case QuickPlayTarget.YourModule:
            case QuickPlayTarget.AnyModule:
            case QuickPlayTarget.EnemyObject:
            case QuickPlayTarget.YourObject:
            case QuickPlayTarget.AnyObject:
                DealDamageToTarget(target, enemy, e.IntValue); break;

            case QuickPlayTarget.AllEnemyModules:
                ForEachAliveModule(enemyZone, m => m.TakeDamage(e.IntValue)); break;

            case QuickPlayTarget.EnemyTotalHealth:
                enemy?.TakeDamageDirectly(e.IntValue); break;

            case QuickPlayTarget.RandomEnemyModule:
                var randEnemy = GetRandomAliveModule(enemyZone);
                randEnemy?.TakeDamage(e.IntValue); break;

            case QuickPlayTarget.RandomEnemyDrone:
                // TODO: 随机敌方无人机
                break;
        }
    }

    private void DealDamageToTarget(GameObject target, PlayerState enemy, int amount)
    {
        if (amount <= 0) return;
        if (target != null)
        {
            ModuleInstance m = target.GetComponentInParent<ModuleInstance>()
                            ?? target.GetComponent<ModuleInstance>()
                            ?? target.GetComponentInChildren<ModuleInstance>();
            if (m != null)
            {
                m.TakeDamage(amount);
                DamageEffect.Create(m.transform.position, amount, m.MaxHealth);
                return;
            }

            // 肖像目标：从 PlayerStateRef 精确判断是哪方
            PlayerStateRef stateRef = target.GetComponent<PlayerStateRef>()
                                   ?? target.GetComponentInParent<PlayerStateRef>();
            if (stateRef != null && stateRef.State != null)
            {
                stateRef.State.TakeDamageDirectly(amount);
                return;
            }
        }
        // 无目标或未命中 → 伤害敌方本体
        enemy?.TakeDamageDirectly(amount);
    }

    // ─────────────────────────────────────────────────
    // 治疗
    // ─────────────────────────────────────────────────

    private void HealTarget(CardEffect e, GameObject target,
        PlayerState caster, DeployZone casterZone)
    {
        switch (e.Target)
        {
            case QuickPlayTarget.YourModule:
            case QuickPlayTarget.AnyModule:
            case QuickPlayTarget.YourObject:
            case QuickPlayTarget.AnyObject:
                HealTargetAmount(target, caster, e.IntValue); break;

            case QuickPlayTarget.AllYourModules:
                ForEachAliveModule(casterZone, m => m.Heal(e.IntValue)); break;

            case QuickPlayTarget.YourTotalHealth:
                caster?.HealDirectly(e.IntValue); break;

            case QuickPlayTarget.RandomYourModule:
                var randOwn = GetRandomAliveModule(casterZone);
                randOwn?.Heal(e.IntValue); break;
        }
    }

    private void HealTargetAmount(GameObject target, PlayerState caster, int amount)
    {
        if (amount <= 0) return;
        if (target != null)
        {
            ModuleInstance m = target.GetComponentInParent<ModuleInstance>()
                            ?? target.GetComponent<ModuleInstance>()
                            ?? target.GetComponentInChildren<ModuleInstance>();
            if (m != null)
            {
                m.Heal(amount);
                DamageEffect.Create(m.transform.position, -amount);
                return;
            }

            // 肖像目标：从 PlayerStateRef 精确判断是哪方
            PlayerStateRef stateRef = target.GetComponent<PlayerStateRef>()
                                   ?? target.GetComponentInParent<PlayerStateRef>();
            if (stateRef != null && stateRef.State != null)
            {
                stateRef.State.HealDirectly(amount);
                return;
            }
        }
        caster?.HealDirectly(amount);
    }

    // ─────────────────────────────────────────────────
    // 模块操作
    // ─────────────────────────────────────────────────

    private void OverheatTarget(GameObject target)
    {
        if (target == null) return;
        ModuleInstance m = target.GetComponentInParent<ModuleInstance>()
                        ?? target.GetComponent<ModuleInstance>()
                        ?? target.GetComponentInChildren<ModuleInstance>();
        m?.OverheatFaceDown();
        Debug.Log($"[Executor] 过热：{m?.Asset?.GetDisplayName()}");
    }

    private void DestroyModuleTarget(GameObject target, QuickPlayTarget targetType,
        PlayerState caster, PlayerState enemy,
        DeployZone casterZone, DeployZone enemyZone)
    {
        switch (targetType)
        {
            case QuickPlayTarget.EnemyModule:
            case QuickPlayTarget.YourModule:
            case QuickPlayTarget.AnyModule:
            case QuickPlayTarget.EnemyObject:
            case QuickPlayTarget.YourObject:
            case QuickPlayTarget.AnyObject:
                var m = target?.GetComponentInParent<ModuleInstance>()
                     ?? target?.GetComponent<ModuleInstance>()
                     ?? target?.GetComponentInChildren<ModuleInstance>();
                m?.TakeDamage(m.MaxHealth); break; // 直接扣满血摧毁

            case QuickPlayTarget.RandomEnemyModule:
                GetRandomAliveModule(enemyZone)?.TakeDamage(int.MaxValue); break;

            case QuickPlayTarget.RandomYourModule:
                GetRandomAliveModule(casterZone)?.TakeDamage(int.MaxValue); break;
        }
    }

    private void DestroyAllModules(DeployZone zone)
    {
        ForEachAliveModule(zone, m => m.TakeDamage(int.MaxValue));
    }

    // ─────────────────────────────────────────────────
    // 抽牌
    // ─────────────────────────────────────────────────

    private void DrawCards(DeckManager deck, int count)
    {
        if (deck == null) { Debug.LogWarning("[Executor] DrawCards: casterDeck 未赋值"); return; }
        deck.DrawAndDistribute(count);
    }

    private void DrawAllCards(DeckManager deck)
    {
        if (deck == null) { Debug.LogWarning("[Executor] DrawAllCards: casterDeck 未赋值"); return; }
        int remaining = deck.CardCount;
        if (remaining > 0) deck.DrawAndDistribute(remaining);
    }

    // ─────────────────────────────────────────────────
    // 无人机
    // ─────────────────────────────────────────────────

    private void SpawnDrones(CardEffect e, GameObject target, DeployZone casterZone)
    {
        if (target == null) return;

        ModuleInstance host = target.GetComponentInParent<ModuleInstance>()
                           ?? target.GetComponent<ModuleInstance>()
                           ?? target.GetComponentInChildren<ModuleInstance>();

        if (host == null || !host.IsAlive || host.IsFaceDown)
        {
            Debug.LogWarning("[Executor] SpawnDrones: 目标模块无效或处于盖伏状态");
            return;
        }

        // 无人机实例化委托给 CombatEngine（持有 Prefab 引用）
        // 此处通过 SceneRefs 访问 CombatEngine
        CombatEngine engine = FindObjectOfType<CombatEngine>();
        if (engine == null)
        {
            Debug.LogWarning("[Executor] SpawnDrones: 找不到 CombatEngine");
            return;
        }

        int count = e.DroneCount > 0 ? e.DroneCount : 1;
        for (int i = 0; i < count; i++)
            engine.SpawnDroneOnModule(host, e.DroneType);

        Debug.Log($"[Executor] SpawnDrones x{count} {e.DroneType} → {host.Asset.GetDisplayName()}");
    }

    private void DestroyDrones(CardEffect e, GameObject target,
        DeployZone casterZone, DeployZone enemyZone)
    {
        switch (e.Target)
        {
            case QuickPlayTarget.EnemyDrones:
                DestroyAllDronesInZone(enemyZone); break;

            case QuickPlayTarget.YourDrones:
                DestroyAllDronesInZone(casterZone); break;

            case QuickPlayTarget.AnyDrone:
                if (target != null)
                {
                    DroneInstance drone = target.GetComponent<DroneInstance>();
                    if (drone != null && drone.IsAlive)
                    {
                        drone.Host?.RemoveDrone(drone);
                        Destroy(drone.gameObject);
                    }
                }
                break;
        }
    }

    private void DestroyAllDronesInZone(DeployZone zone)
    {
        if (zone == null) return;
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            GameObject slotObj = zone.GetSlot(i);
            if (slotObj == null) continue;
            DeploySlot slot = slotObj.GetComponent<DeploySlot>();
            ModuleInstance m = slot?.OccupyingModuleInstance;
            if (m == null) continue;
            foreach (DroneInstance drone in new System.Collections.Generic.List<DroneInstance>(m.Drones))
            {
                if (drone == null) continue;
                m.RemoveDrone(drone);
                if (drone.gameObject != null) Destroy(drone.gameObject);
            }
        }
    }

    // ─────────────────────────────────────────────────
    // 特殊
    // ─────────────────────────────────────────────────

    private void RevealEnemyField(DeployZone enemyZone)
    {
        if (enemyZone == null) return;
        for (int i = 0; i < enemyZone.CurrentSlotCount; i++)
        {
            GameObject slotObj = enemyZone.GetSlot(i);
            if (slotObj == null) continue;
            DeploySlot slot = slotObj.GetComponent<DeploySlot>();
            if (slot?.OccupyingModuleInstance != null &&
                slot.OccupyingModuleInstance.IsFaceDown)
            {
                // 视觉上翻开但不标记为正式翻开（仅显示）
                var rot = slot.OccupyingModuleInstance.GetComponent<BetterCardRotation>();
                rot?.ShowFront();
            }
        }
        Debug.Log("[Executor] 揭示敌方场地");
    }

    private void StealHealth(CardEffect e, PlayerState caster, PlayerState enemy,
        DeployZone casterZone, DeployZone enemyZone)
    {
        int amount = e.IntValue;
        enemy?.TakeDamageDirectly(amount);
        caster?.HealDirectly(amount);
        Debug.Log($"[Executor] StealHealth {amount}");
    }

    private void ExecuteCustom(CardEffect e, GameObject target,
        PlayerState caster, PlayerState enemy,
        DeployZone casterZone, DeployZone enemyZone,
        HandManager casterHand, DeckManager casterDeck,
        CardAsset card = null)
    {
        if (string.IsNullOrEmpty(e.ScriptName))
        {
            Debug.LogWarning("[Executor] Custom 效果的 ScriptName 为空");
            return;
        }

        System.Type t = System.Type.GetType(e.ScriptName);
        if (t == null)
        {
            Debug.LogWarning($"[Executor] 找不到 Custom 脚本类型：{e.ScriptName}");
            return;
        }

        var component = GetComponent(t) ?? gameObject.AddComponent(t);

        var method = t.GetMethod("Execute",
            new System.Type[] { typeof(PlayerState), typeof(PlayerState),
                                 typeof(HandManager), typeof(DeckManager) });
        if (method != null)
        {
            method.Invoke(component, new object[] { caster, enemy, casterHand, casterDeck });
            GetDialogueManager()?.PlayCardDialogue(card);
        }
        else
            Debug.LogWarning($"[Executor] {e.ScriptName} 缺少 Execute(PlayerState, PlayerState, HandManager, DeckManager) 方法");
    }

    // ─────────────────────────────────────────────────
    // 辅助
    // ─────────────────────────────────────────────────

    private void ForEachAliveModule(DeployZone zone, System.Action<ModuleInstance> action)
    {
        if (zone == null) return;
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            GameObject slotObj = zone.GetSlot(i);
            if (slotObj == null) continue;
            DeploySlot slot = slotObj.GetComponent<DeploySlot>();
            var m = slot?.OccupyingModuleInstance;
            if (m != null && m.IsAlive) action(m);
        }
    }

    private ModuleInstance GetRandomAliveModule(DeployZone zone)
    {
        var list = new List<ModuleInstance>();
        ForEachAliveModule(zone, m => list.Add(m));
        if (list.Count == 0) return null;
        return list[Random.Range(0, list.Count)];
    }

    private int CountAliveModules(DeployZone zone)
    {
        int c = 0;
        ForEachAliveModule(zone, _ => c++);
        return c;
    }

    private int CountDrones(DeployZone zone)
    {
        int total = 0;
        if (zone == null) return 0;
        for (int i = 0; i < zone.CurrentSlotCount; i++)
        {
            GameObject slotObj = zone.GetSlot(i);
            if (slotObj == null) continue;
            DeploySlot slot = slotObj.GetComponent<DeploySlot>();
            ModuleInstance m = slot?.OccupyingModuleInstance;
            if (m == null) continue;
            foreach (DroneInstance drone in m.Drones)
                if (drone != null && drone.IsAlive) total++;
        }
        return total;
    }
}