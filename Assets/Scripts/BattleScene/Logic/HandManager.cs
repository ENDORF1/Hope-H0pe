using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;

/// <summary>
/// 手牌管理脚本（速攻牌 + 溢出模块牌共用同一排手牌槽）
/// 速攻牌：卡面朝上，可拖拽
/// 溢出模块牌：卡背朝上，等待下回合部署阶段自动填入部署区
/// 忽略上限时（天允终偿等）超出 handSlots 的牌飞入 overflowSlots
/// </summary>
public class HandManager : MonoBehaviour
{
    [Header("手牌槽位（场景中预先摆好的等间距空对象）")]
    [SerializeField] private List<Transform> handSlots = new List<Transform>();

    [Header("溢出区槽位（仅忽略上限时使用，如天允终偿）")]
    [SerializeField] private List<Transform> overflowSlots = new List<Transform>();

    // 速攻牌列表
    private List<GameObject> handCards = new List<GameObject>();

    // 溢出模块牌列表（卡背朝上暂存在手牌槽中）
    private List<GameObject> overflowModules = new List<GameObject>();

    public int  CardCount          => handCards.Count;
    public int  TotalCardCount     => handCards.Count + overflowModules.Count;
    public bool HasOverflowModules => overflowModules.Count > 0;

    private int TotalOccupied => handCards.Count + overflowModules.Count;

    /// <summary>
    /// 忽略手牌上限标志（天允终偿等效果使用）。
    /// 为 true 时超出 handSlots 的牌飞入 overflowSlots。
    /// </summary>
    private bool _ignoreHandLimit = false;
    public void SetIgnoreHandLimit(bool ignore) => _ignoreHandLimit = ignore;

    /// <summary>返回当前速攻牌手牌数量</summary>
    public int GetHandCount() => handCards.Count;

    /// <summary>清空所有手牌（速攻牌 + 溢出模块牌），不保留引用</summary>
    public void ClearAllCards() => ClearHand();

    // ─────────────────────────────────────────────────
    // 槽位查询
    // ─────────────────────────────────────────────────
    public Transform GetNextEmptySlot()
    {
        int index = TotalOccupied;

        if (index < handSlots.Count)
            return handSlots[index];

        // 超出正常上限
        if (!_ignoreHandLimit)
        {
            Debug.LogWarning($"HandManager: 手牌槽位已满（{handSlots.Count} 个）");
            return null;
        }

        // 忽略上限：飞入溢出区
        int overflowIndex = index - handSlots.Count;
        if (overflowSlots.Count == 0)
        {
            Debug.LogWarning("HandManager: overflowSlots 未配置");
            return null;
        }
        return overflowSlots[Mathf.Min(overflowIndex, overflowSlots.Count - 1)];
    }

    // ─────────────────────────────────────────────────
    // 速攻牌注册
    // ─────────────────────────────────────────────────
    /// <summary>每张速攻牌落槽注册时触发，供 GameManager 即时刷新高亮</summary>
    public static event System.Action OnCardRegistered;

    public void RegisterCard(GameObject cardObj, CardAsset asset)
    {
        if (cardObj == null) return;
        handCards.Add(cardObj);
        OnCardRegistered?.Invoke();
    }

    // ─────────────────────────────────────────────────
    // 溢出模块牌注册（卡背朝上，占用手牌槽）
    // ─────────────────────────────────────────────────
    public void RegisterOverflowModule(GameObject cardObj)
    {
        if (cardObj == null) return;
        overflowModules.Add(cardObj);
        Debug.Log($"[HandManager] 溢出模块牌入手暂存，当前溢出数：{overflowModules.Count}");
    }

    /// <summary>只读查看溢出模块列表，不移除</summary>
    public IReadOnlyList<GameObject> PeekOverflowModules() => overflowModules.AsReadOnly();

    /// <summary>移除已成功部署的溢出模块</summary>
    public void RemoveOverflowModule(GameObject cardObj)
    {
        overflowModules.Remove(cardObj);
    }

    // ─────────────────────────────────────────────────
    // 速攻牌打出或弃牌时移除
    // ─────────────────────────────────────────────────
    public void RemoveCard(GameObject cardObject)
    {
        if (!handCards.Contains(cardObject)) return;
        handCards.Remove(cardObject);
        DG.Tweening.DOTween.Kill(cardObject, complete: false);
        foreach (var img in cardObject.GetComponentsInChildren<UnityEngine.UI.Image>(true))
        {
            DG.Tweening.DOTween.Kill(img, complete: false);
            string glowId = "handglow_" + img.GetInstanceID();
            DG.Tweening.DOTween.Kill(glowId, complete: false);
            string portraitId = "portraitbodyglow_" + img.GetInstanceID();
            DG.Tweening.DOTween.Kill(portraitId, complete: false);
        }
        foreach (var t in cardObject.GetComponentsInChildren<UnityEngine.Transform>(true))
            DG.Tweening.DOTween.Kill(t, complete: false);
        Destroy(cardObject);
        CleanNulls();
        RearrangeHand();
    }

    /// <summary>清理列表中已销毁的空引用</summary>
    private void CleanNulls()
    {
        handCards.RemoveAll(c => c == null);
        overflowModules.RemoveAll(c => c == null);
    }

    public void ClearHand()
    {
        foreach (var card in handCards)
            if (card != null) Destroy(card);
        handCards.Clear();

        foreach (var card in overflowModules)
            if (card != null) Destroy(card);
        overflowModules.Clear();
    }

    public IReadOnlyList<GameObject> GetHandCards() => handCards.AsReadOnly();

    /// <summary>返回某张卡当前对应的槽位 Transform，找不到返回 null</summary>
    public Transform GetSlotForCard(GameObject cardObj)
    {
        int idx = handCards.IndexOf(cardObj);
        if (idx >= 0)
        {
            if (idx < handSlots.Count) return handSlots[idx];
            int oi = idx - handSlots.Count;
            return overflowSlots.Count > 0 ? overflowSlots[Mathf.Min(oi, overflowSlots.Count - 1)] : null;
        }

        int modIdx = overflowModules.IndexOf(cardObj);
        if (modIdx >= 0)
        {
            int total = handCards.Count + modIdx;
            if (total < handSlots.Count) return handSlots[total];
            int oi = total - handSlots.Count;
            return overflowSlots.Count > 0 ? overflowSlots[Mathf.Min(oi, overflowSlots.Count - 1)] : null;
        }

        return null;
    }

    // ─────────────────────────────────────────────────
    // 重排
    // ─────────────────────────────────────────────────
    public void RearrangeHand()
    {
        RearrangeList(handCards, 0);
        RearrangeList(overflowModules, handCards.Count);
    }

    private void RearrangeList(List<GameObject> list, int slotOffset)
    {
        for (int i = 0; i < list.Count; i++)
        {
            GameObject card = list[i];
            if (card == null) continue;

            QuickPlayDraggable drag = card.GetComponent<QuickPlayDraggable>();
            if (drag != null && drag.IsBeingDragged) continue;

            int index = slotOffset + i;
            Transform slot;
            if (index < handSlots.Count)
                slot = handSlots[index];
            else if (overflowSlots.Count > 0)
                slot = overflowSlots[Mathf.Min(index - handSlots.Count, overflowSlots.Count - 1)];
            else
                continue;

            card.transform.DOKill();
            card.transform.DOMove(slot.position, 0.2f).SetEase(Ease.OutQuart);
            card.transform.DORotate(slot.eulerAngles, 0.2f);
        }
    }
}