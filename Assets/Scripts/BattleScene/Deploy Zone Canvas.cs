using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class DeployZone : MonoBehaviour
{
    [Header("格子设置")]
    [SerializeField] private int maxSlots = 8;
    [SerializeField] private int initialSlots = 5;
    [SerializeField] private float slotSpacing = 1.2f;
    [SerializeField] private bool expandRight = true;  // true = 向右延伸, false = 向左延伸

    [Header("格子对象")]
    [SerializeField] private GameObject slotPrefab;

    [Header("归属玩家（拖入对应的 PlayerState）")]
    [SerializeField] private PlayerState owner;

    private List<GameObject> slots = new List<GameObject>();

    public int CurrentSlotCount => slots.Count;
    public int MaxSlots => maxSlots;
    public int InitialSlots => initialSlots;

    [Header("开场淡入动画")]
    [SerializeField] private float introFadeInDuration = 0.3f;  // 每个格子淡入时长
    [SerializeField] private float introStagger        = 0.1f;  // 相邻格子的延迟间隔

    void Start()
    {
        ClearAllSlots();

        if (slotPrefab == null)
        {
            Debug.LogError("DeployZone: slotPrefab is not assigned!");
            return;
        }

        for (int i = 0; i < initialSlots; i++)
        {
            AddSlot();
        }

        // 初始设为透明，等开场动画淡入
        SetAllSlotsAlpha(0f);
    }

    /// <summary>
    /// 开场动画：所有格子从左往右依次淡入。
    /// 由 GameManager 在肖像翻面完成后调用。
    /// </summary>
    public IEnumerator PlayIntroAnimation()
    {
        for (int i = 0; i < slots.Count; i++)
        {
            if (slots[i] == null) continue;
            CanvasGroup cg = GetOrAddCanvasGroup(slots[i]);
            int captured = i;
            DOTween.To(() => cg.alpha, a => cg.alpha = a, 1f, introFadeInDuration)
                .SetDelay(captured * introStagger)
                .SetEase(Ease.OutQuad);
        }

        // 等待所有格子淡入完成
        float totalDuration = introFadeInDuration + (slots.Count - 1) * introStagger;
        yield return new WaitForSeconds(totalDuration);
    }

    private void SetAllSlotsAlpha(float alpha)
    {
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            CanvasGroup cg = GetOrAddCanvasGroup(slot);
            cg.alpha = alpha;
        }
    }

    private CanvasGroup GetOrAddCanvasGroup(GameObject slot)
    {
        CanvasGroup cg = slot.GetComponent<CanvasGroup>();
        if (cg == null) cg = slot.AddComponent<CanvasGroup>();
        return cg;
    }

    public int AddSlot()
    {
        if (slotPrefab == null)
        {
            Debug.LogError("DeployZone: slotPrefab is not assigned!");
            return -1;
        }

        if (slots.Count >= maxSlots)
        {
            Debug.Log("Max slots reached");
            return -1;
        }

        GameObject newSlot = Instantiate(slotPrefab, transform);
        newSlot.name = "Slot_" + slots.Count;
        newSlot.SetActive(true);

        DeploySlot deploySlot = newSlot.GetComponent<DeploySlot>();
        if (deploySlot != null)
            deploySlot.Initialize(slots.Count, owner);

        slots.Add(newSlot);
        RearrangeSlots();

        return slots.Count - 1;
    }

    public bool RemoveLastSlot()
    {
        if (slots.Count <= initialSlots)
        {
            Debug.Log("Cannot remove initial slots");
            return false;
        }

        ForceRemoveLastSlot();
        return true;
    }

    private void ForceRemoveLastSlot()
    {
        if (slots.Count <= 0) return;

        int lastIndex = slots.Count - 1;
        GameObject slotToRemove = slots[lastIndex];
        slots.RemoveAt(lastIndex);

        if (Application.isPlaying)
            Destroy(slotToRemove);
        else
            DestroyImmediate(slotToRemove);

        RearrangeSlots();
    }

    public bool RemoveSlotAt(int index)
    {
        if (index < 0 || index >= slots.Count) return false;
        if (index < initialSlots)
        {
            Debug.Log("Cannot remove initial slots");
            return false;
        }

        GameObject slotToRemove = slots[index];
        slots.RemoveAt(index);

        if (Application.isPlaying)
            Destroy(slotToRemove);
        else
            DestroyImmediate(slotToRemove);

        for (int i = 0; i < slots.Count; i++)
            slots[i].name = "Slot_" + i;

        RearrangeSlots();
        return true;
    }

    public void ResetToCount(int targetCount)
    {
        if (targetCount < 0 || targetCount > maxSlots)
        {
            Debug.LogError("Target count out of range: " + targetCount);
            return;
        }

        while (slots.Count < targetCount) AddSlot();
        while (slots.Count > targetCount) ForceRemoveLastSlot();
    }

    private void RearrangeSlots()
    {
        if (slots.Count == 0) return;

        RectTransform parentRect = GetComponent<RectTransform>();
        bool isUI = parentRect != null;

        if (isUI)
        {
            // 从 Canvas 左边缘开始，动态计算 Slot 宽度
            float canvasWidth = parentRect.rect.width;
            // 用 slotSpacing 作为格子宽度，避免 Instantiate 后 rect.width 还未初始化的问题
            float slotWidth = slotSpacing;

            // 根据方向决定起始点和延伸方向
            float dir = expandRight ? 1f : -1f;
            float startX = expandRight
                ? -canvasWidth / 2f + slotWidth / 2f      // 从左边缘向右
                : canvasWidth / 2f - slotWidth / 2f;      // 从右边缘向左

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;

                RectTransform rt = slots[i].GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchoredPosition = new Vector2(startX + dir * i * slotSpacing, 0f);
                }
                else
                {
                    Debug.LogWarning("Slot_" + i + " has no RectTransform!");
                }
            }
        }
        else
        {
            Vector3 centerPos = transform.position;
            float totalWidth = (slots.Count - 1) * slotSpacing;
            float startX = centerPos.x - totalWidth / 2f;

            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                slots[i].transform.position = new Vector3(startX + i * slotSpacing, centerPos.y, centerPos.z);
            }
        }
    }

    public GameObject GetSlot(int index)
    {
        if (index < 0 || index >= slots.Count) return null;
        return slots[index];
    }

    /// <summary>返回第一个空的 DeploySlot，没有则返回 null</summary>
    public DeploySlot GetNextEmptySlot()
    {
        foreach (var slot in slots)
        {
            if (slot == null) continue;
            DeploySlot ds = slot.GetComponent<DeploySlot>();
            if (ds != null && !ds.IsOccupied)
                return ds;
        }
        return null;
    }

    /// <summary>将模块卡牌放入下一个空格（向后兼容）</summary>
    public DeploySlot PlaceCard(GameObject cardObject)
    {
        DeploySlot slot = GetNextEmptySlot();
        if (slot == null)
        {
            Debug.LogWarning("DeployZone: 没有空格可放置模块牌！");
            return null;
        }
        cardObject.transform.SetParent(slot.transform, false);
        cardObject.transform.localPosition = Vector3.zero;
        slot.PlaceModule(cardObject, startFaceDown: true);
        return slot;
    }

    public void ClearAllSlots()
    {
        foreach (GameObject slot in slots)
        {
            if (slot == null) continue;
            if (Application.isPlaying)
                Destroy(slot);
            else
                DestroyImmediate(slot);
        }
        slots.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        int previewCount = slots.Count > 0 ? slots.Count : initialSlots;
        if (previewCount == 0) return;

        Vector3 centerPos = transform.position;
        float totalWidth = (previewCount - 1) * slotSpacing;
        float startX = centerPos.x - totalWidth / 2f;

        Gizmos.color = Color.green;
        for (int i = 0; i < previewCount; i++)
        {
            Vector3 previewPos = new Vector3(startX + i * slotSpacing, centerPos.y, centerPos.z);
            Gizmos.DrawWireCube(previewPos, Vector3.one * 0.8f);
        }
    }
}