using UnityEngine;

/// <summary>
/// 挂在任何带 TMPEffects 动效标签的文字对象上。
/// 每帧在 LateUpdate 里：
///   1. 强制重置 localRotation / localScale，对抗 TMPEffects 乱写 Transform 的问题。
///   2. 把 anchoredPosition 取整到最近像素，消除亚像素偏移导致的模糊。
/// 位置本身由外部动画（DOTween 等）自由控制，此脚本不锁定位置。
/// </summary>
public class FixTimingTextTransform : MonoBehaviour
{
    private RectTransform _rt;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
    }

    void LateUpdate()
    {
        // 重置 TMPEffects 乱写的 rotation / scale
        _rt.localRotation = Quaternion.identity;
        _rt.localScale    = Vector3.one;

        // 像素对齐：位置取整，消除亚像素模糊
        Vector2 p = _rt.anchoredPosition;
        _rt.anchoredPosition = new Vector2(Mathf.Round(p.x), Mathf.Round(p.y));
    }
}