using UnityEngine;

/// <summary>
/// 至亲者的沉默（姐姐的遗物）
/// 效果：每回合结束时，根据己方场上存活的模块数量，对敌方本体造成等量次数的固定伤害。
/// 
/// 熄忘方场面稳定，模块多，伤害稳定积累——存在即压迫。
/// 希望方模块少，伤害少，但遗物始终在场，沉默地发挥着。
/// </summary>
public class SisterSilenceRelic : RelicBase
{
    [Header("至亲者的沉默")]
    [Tooltip("每个存活模块对敌方本体造成的固定伤害")]
    public int DamagePerModule = 2;

    protected override void OnInitialized()
    {
        Debug.Log($"[SisterSilenceRelic] 至亲者的沉默已激活，每模块 {DamagePerModule} 点伤害");
    }

    public override void OnTurnEnd(int turnNumber)
    {
        if (EnemyState == null || EnemyState.IsDead) return;

        int moduleCount = GetAlivePlayerModuleCount();
        if (moduleCount <= 0) return;

        int totalDamage = moduleCount * DamagePerModule;
        Debug.Log($"[SisterSilenceRelic] 回合结束，己方 {moduleCount} 个模块，" +
                  $"对敌方本体造成 {moduleCount} 次 × {DamagePerModule} = {totalDamage} 点伤害");

        // 逐次造成伤害（每次单独结算，符合规则描述的"等量次数"）
        for (int i = 0; i < moduleCount; i++)
        {
            if (EnemyState.IsDead) break;
            EnemyState.TakeDamageDirectly(DamagePerModule);
            DamageEffect.Create(
                EnemyState.PortraitTransform != null
                    ? EnemyState.PortraitTransform.position
                    : Vector3.zero,
                DamagePerModule
            );
        }
    }
}
