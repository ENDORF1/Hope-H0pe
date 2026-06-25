using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 挂在 entryContainer 上。
/// 每帧在 LateUpdate 里强制重建每个子对象的布局，读取实际高度（减去 Entry 内部 VLG 上下 padding 各 200），
/// 从下往上零间距叠放。不依赖 entryContainer 上的 Vertical Layout Group 或 Content Size Fitter。
/// </summary>
public class EntryLayoutGroup : MonoBehaviour
{
    [Tooltip("每个 Entry 内部 VLG 的上下 Padding 之和（Top 200 + Bottom 200 = 400）")]
    public float entryPaddingVertical = 400f;

    private RectTransform _rt;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        int count = _rt.childCount;
        if (count == 0) return;

        // 强制每个子对象立刻完成 CSF 高度计算
        for (int i = 0; i < count; i++)
        {
            RectTransform child = _rt.GetChild(i) as RectTransform;
            if (child != null)
                LayoutRebuilder.ForceRebuildLayoutImmediate(child);
        }

        // 计算总高度（去掉每个 Entry 的内部 padding）
        float totalHeight = 0f;
        for (int i = 0; i < count; i++)
        {
            RectTransform child = _rt.GetChild(i) as RectTransform;
            if (child == null) continue;
            totalHeight += Mathf.Max(0f, child.rect.height - entryPaddingVertical);
        }

        // 更新自身高度
        _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);

        // 从下往上叠放：第一个条目在最下，最后一个在最上
        float curY = 0f;
        for (int i = 0; i < count; i++)
        {
            RectTransform child = _rt.GetChild(i) as RectTransform;
            if (child == null) continue;

            float h = Mathf.Max(0f, child.rect.height - entryPaddingVertical);
            child.anchoredPosition = new Vector2(child.anchoredPosition.x, curY);
            curY += h;
        }
    }
}