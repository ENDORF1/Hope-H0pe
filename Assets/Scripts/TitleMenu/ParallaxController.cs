using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 鼠标视差控制器。挂在 Canvas 上。
/// 鼠标从屏幕中心偏移时，各层 RectTransform 按各自强度系数产生位移，
/// 造成多层纵深的视差效果。
/// </summary>
public class ParallaxController : MonoBehaviour
{
    // ═══════════════════════════════════════════════════
    // 数据结构
    // ═══════════════════════════════════════════════════

    [System.Serializable]
    public class ParallaxLayer
    {
        [Tooltip("参与视差的 RectTransform（直接拖入）")]
        public RectTransform target;

        [Tooltip("水平方向强度。正值：跟随鼠标同向；负值：反向。推荐范围 -1 ~ 1。")]
        public float strengthX = 0.3f;

        [Tooltip("垂直方向强度。正值：跟随鼠标同向；负值：反向。推荐范围 -1 ~ 1。")]
        public float strengthY = 0.3f;
    }

    // ═══════════════════════════════════════════════════
    // Inspector 参数
    // ═══════════════════════════════════════════════════

    [Header("── 视差层列表（从远到近排列，方便阅读）")]
    [SerializeField] private List<ParallaxLayer> layers = new List<ParallaxLayer>();

    [Header("── 全局参数")]

    [Tooltip("位移的最大幅度（像素）。所有层的实际位移 = 强度系数 × 此值。")]
    [SerializeField] private float maxDisplacement = 40f;

    [Tooltip("跟随鼠标的平滑速度。越小越滞后，越大越跟手。推荐 3~8。")]
    [SerializeField] private float smoothSpeed = 5f;

    [Tooltip("是否在编辑器里显示每层当前位移的 Gizmo（仅用于调试）。")]
    [SerializeField] private bool debugGizmos = false;

    // ═══════════════════════════════════════════════════
    // 内部状态
    // ═══════════════════════════════════════════════════

    // 记录每层的原始 anchoredPosition，运行时以此为基准叠加偏移
    private Vector2[] _originPositions;

    // 当前平滑后的鼠标归一化偏移（-1 ~ 1，屏幕中心为0）
    private Vector2 _smoothOffset = Vector2.zero;

    // ═══════════════════════════════════════════════════
    // 生命周期
    // ═══════════════════════════════════════════════════

    void Start()
    {
        CacheOrigins();
    }

    void Update()
    {
        UpdateOffset();
        ApplyLayers();
    }

    // ═══════════════════════════════════════════════════
    // 核心逻辑
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 记录每层 RectTransform 的初始 anchoredPosition。
    /// 必须在 Start 时调用，确保 UI 布局已完成。
    /// </summary>
    private void CacheOrigins()
    {
        _originPositions = new Vector2[layers.Count];
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].target != null)
                _originPositions[i] = layers[i].target.anchoredPosition;
        }
    }

    /// <summary>
    /// 把鼠标屏幕坐标转换成 -1~1 的归一化偏移，并做平滑插值。
    /// </summary>
    private void UpdateOffset()
    {
        // 鼠标位置归一化到 -1~1（屏幕中心为原点）
        Vector2 mousePos   = Input.mousePosition;
        Vector2 normalized = new Vector2(
            (mousePos.x / Screen.width  - 0.5f) * 2f,
            (mousePos.y / Screen.height - 0.5f) * 2f
        );

        // 平滑插值，避免跳变
        _smoothOffset = Vector2.Lerp(_smoothOffset, normalized, smoothSpeed * Time.deltaTime);
    }

    /// <summary>
    /// 把平滑后的偏移乘以各层系数，写入 anchoredPosition。
    /// </summary>
    private void ApplyLayers()
    {
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].target == null) continue;

            Vector2 offset = new Vector2(
                _smoothOffset.x * layers[i].strengthX * maxDisplacement,
                _smoothOffset.y * layers[i].strengthY * maxDisplacement
            );

            layers[i].target.anchoredPosition = _originPositions[i] + offset;
        }
    }

    // ═══════════════════════════════════════════════════
    // 公共接口
    // ═══════════════════════════════════════════════════

    /// <summary>
    /// 场景切换前调用，把所有层复位到原始位置，避免残影。
    /// </summary>
    public void ResetAllLayers()
    {
        if (_originPositions == null) return;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].target != null)
                layers[i].target.anchoredPosition = _originPositions[i];
        }
    }

    // ═══════════════════════════════════════════════════
    // Gizmo（调试用）
    // ═══════════════════════════════════════════════════

    void OnDrawGizmosSelected()
    {
        if (!debugGizmos || _originPositions == null) return;
        for (int i = 0; i < layers.Count; i++)
        {
            if (layers[i].target == null) continue;
            Gizmos.color = Color.cyan;
            Vector3 origin = layers[i].target.position;
            Gizmos.DrawWireSphere(origin, 10f);
        }
    }
}
