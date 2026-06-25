using UnityEngine;
using System.Collections;
using DG.Tweening;

/// <summary>
/// AI 牌库管理脚本。
/// 继承自 DeckManager，覆盖以下差异：
///   - 模块牌：直接飞入 AI 部署区，盖伏放置，无翻转动画
///   - 速攻牌：飞入 AI 手牌槽（等待 GameManager 在 AI 回合触发决策）
///   - 无 HoverPreview / FlipAnimation 逻辑
///   - 暂存区可选（AI 抽牌可不经过暂存区，节省时间）
/// </summary>
public class AIDeckManager : DeckManager
{
    [Header("AI 专用引用")]
    [SerializeField] private DeployZone  aiDeployZone;
    [SerializeField] private HandManager aiHandManager;   // AI 手牌区（速攻牌用）

    [Header("AI 抽牌动画")]
    [SerializeField] private float aiFlyDuration  = 0.3f;  // 飞入部署区时间
    [SerializeField] private bool  skipStagingArea = true; // AI 通常跳过暂停区

    // ─────────────────────────────────────────────────
    // 覆盖：单张卡处理（去掉暂存区停留，直接飞目标）
    // ─────────────────────────────────────────────────
    protected override IEnumerator ProcessCard(GameObject cardObj, CardAsset asset, bool isModule)
    {
        // AI 可选跳过暂存区
        if (!skipStagingArea && StagingArea != null)
        {
            yield return cardObj.transform
                .DOMove(StagingArea.position, DrawDuration)
                .SetEase(Ease.OutQuart)
                .WaitForCompletion();

            yield return new WaitForSeconds(StagingWait);
        }

        if (isModule)
            yield return StartCoroutine(AIFlyToDeployZone(cardObj, asset));
        else
            yield return StartCoroutine(AIFlyToHand(cardObj, asset));
    }

    // ─────────────────────────────────────────────────
    // 模块牌：飞入 AI 部署区，盖伏，无翻转动画
    // ─────────────────────────────────────────────────
    private IEnumerator AIFlyToDeployZone(GameObject cardObj, CardAsset asset)
    {
        if (aiDeployZone == null)
        {
            Debug.LogError("AIDeckManager: aiDeployZone 未赋值！");
            Destroy(cardObj);
            yield break;
        }

        DeploySlot targetSlot = aiDeployZone.GetNextEmptySlot();

        // 部署区满：飞入 AI 手牌区卡背朝上暂存，下回合有空位时自动填入
        if (targetSlot == null)
        {
            Debug.LogWarning("AIDeckManager: AI 部署区已满，模块牌转入手牌区暂存。");
            if (aiHandManager == null) { Destroy(cardObj); yield break; }

            Transform overflowSlot = aiHandManager.GetNextEmptySlot();
            if (overflowSlot == null) { Debug.LogWarning("AIDeckManager: AI 手牌区也满，模块牌丢弃。"); Destroy(cardObj); yield break; }

            yield return cardObj.transform
                .DOMove(overflowSlot.position, aiFlyDuration)
                .SetEase(Ease.InOutQuart)
                .WaitForCompletion();

            cardObj.transform.rotation = overflowSlot.rotation;

            BetterCardRotation rot = cardObj.GetComponent<BetterCardRotation>();
            if (rot != null) rot.ShowBack();

            aiHandManager.RegisterOverflowModule(cardObj);
            yield break;
        }

        yield return cardObj.transform
            .DOMove(targetSlot.transform.position, aiFlyDuration)
            .SetEase(Ease.InOutQuart)
            .WaitForCompletion();

        targetSlot.PlaceModule(cardObj, startFaceDown: true);

        BetterCardRotation r = cardObj.GetComponent<BetterCardRotation>();
        if (r != null) r.ShowBack();
    }

    // ─────────────────────────────────────────────────
    // 速攻牌：飞入 AI 手牌区，卡背朝上（AI 自己"知道"是什么牌）
    // ─────────────────────────────────────────────────
    private IEnumerator AIFlyToHand(GameObject cardObj, CardAsset asset)
    {
        if (aiHandManager == null)
        {
            Debug.LogWarning("AIDeckManager: aiHandManager 未赋值，速攻牌丢弃。");
            Destroy(cardObj);
            yield break;
        }

        Transform targetSlot = aiHandManager.GetNextEmptySlot();
        if (targetSlot == null)
        {
            // 手牌满：放回牌库顶，DrawSequence 的前置检查应已阻止到达这里
            Debug.LogWarning("AIDeckManager: AI 手牌已满，卡牌放回牌库顶。");
            AddToTop(cardObj.GetComponent<OneCardManager>()?.cardAsset);
            Destroy(cardObj);
            yield break;
        }

        yield return cardObj.transform
            .DOMove(targetSlot.position, aiFlyDuration)
            .SetEase(Ease.InOutQuart)
            .WaitForCompletion();

        cardObj.transform.rotation = targetSlot.rotation;

        aiHandManager.RegisterCard(cardObj, asset);

        BetterCardRotation rot = cardObj.GetComponent<BetterCardRotation>();
        if (rot != null) rot.ShowBack();
    }
}