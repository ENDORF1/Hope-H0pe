using UnityEngine;

/// <summary>
/// 至亲者的遗愿（妹妹的遗物）
/// 效果：每次打出速攻牌，有一定概率额外抽一张牌。
///
/// 希望方速攻密集，触发频繁，像是愿望真的被回应了。
/// 熄忘方速攻少，偶尔触发，像是秩序里意外闯入了一个变量。
/// </summary>
public class SisterWishRelic : RelicBase
{
    [Header("至亲者的遗愿")]
    [Tooltip("打出速攻牌后额外抽牌的概率（0~1）")]
    [Range(0f, 1f)]
    public float TriggerChance = 0.2f;

    protected override void OnInitialized()
    {
        Debug.Log($"[SisterWishRelic] 至亲者的遗愿已激活，触发概率 {TriggerChance * 100f:F0}%");
    }

    /// <summary>
    /// 由 RunManager 在玩家打出速攻牌时调用。
    /// </summary>
    public override void OnPlayerPlayQuickCard()
    {
        if (PlayerDeckManager == null) return;
        if (PlayerDeckManager.IsEmpty) return;

        float roll = Random.value;
        if (roll < TriggerChance)
        {
            Debug.Log($"[SisterWishRelic] 遗愿触发（roll={roll:F2} < {TriggerChance}），额外抽一张牌");
            PlayerDeckManager.DrawAndDistribute(1);
        }
    }
}
