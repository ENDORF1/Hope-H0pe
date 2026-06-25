using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// 过热状态指示器：在卡背上显示一个红色方框。
/// 挂在模块卡 Prefab 根对象上，由 ModuleInstance 调用 Show/Hide。
/// </summary>
public class OverheatIndicator : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("卡背 RectTransform（card_rotation 的 CardBack 字段）")]
    [SerializeField] private RectTransform cardBack;

    [Header("样式")]
    [SerializeField] private Color  borderColor     = new Color(1f, 0.15f, 0.15f, 1f);
    [SerializeField] private float  borderThickness = 6f;
    [SerializeField] private float  padding         = 8f;   // 红框与卡背边缘的间距
    [SerializeField] private float  fadeTime        = 0.2f;

    // 四条边
    private Image _top, _bottom, _left, _right;
    private bool  _built = false;

    void Awake()
    {
        BuildBorder();
        SetVisible(false, instant: true);
    }

    public void Show()
    {
        SetVisible(true);
    }

    public void Hide()
    {
        SetVisible(false);
    }

    // ── 构建四条边框 Image ────────────────────────────
    private void BuildBorder()
    {
        if (_built || cardBack == null) return;
        _built = true;

        _top    = CreateEdge("OH_Top");
        _bottom = CreateEdge("OH_Bottom");
        _left   = CreateEdge("OH_Left");
        _right  = CreateEdge("OH_Right");

        LayoutEdges();
    }

    private void LayoutEdges()
    {
        if (cardBack == null) return;

        float w = cardBack.rect.width;
        float h = cardBack.rect.height;
        float t = borderThickness;
        float p = padding;

        // 上边
        SetRect(_top,    new Vector2(p,     h - p - t), new Vector2(w - p,     h - p));
        // 下边
        SetRect(_bottom, new Vector2(p,     p),         new Vector2(w - p,     p + t));
        // 左边
        SetRect(_left,   new Vector2(p,     p + t),     new Vector2(p + t,     h - p - t));
        // 右边
        SetRect(_right,  new Vector2(w - p - t, p + t), new Vector2(w - p,     h - p - t));
    }

    private Image CreateEdge(string edgeName)
    {
        GameObject go = new GameObject(edgeName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(cardBack, false);

        Image img = go.GetComponent<Image>();
        img.color  = new Color(borderColor.r, borderColor.g, borderColor.b, 0f);
        img.raycastTarget = false;

        // 置顶显示
        go.transform.SetAsLastSibling();
        return img;
    }

    private void SetRect(Image img, Vector2 anchorMin, Vector2 anchorMax)
    {
        if (img == null) return;
        RectTransform rt = img.rectTransform;

        float w = cardBack.rect.width;
        float h = cardBack.rect.height;

        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.zero;
        rt.pivot     = Vector2.zero;

        rt.anchoredPosition = anchorMin;
        rt.sizeDelta        = anchorMax - anchorMin;
    }

    private void SetVisible(bool visible, bool instant = false)
    {
        float alpha = visible ? 1f : 0f;
        foreach (var img in new[] { _top, _bottom, _left, _right })
        {
            if (img == null) continue;
            img.DOKill();
            if (instant) { var c = img.color; c.a = alpha; img.color = c; }
            else         img.DOFade(alpha, fadeTime);
        }
    }
}