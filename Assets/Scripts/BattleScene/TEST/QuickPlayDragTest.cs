using UnityEngine;

/// <summary>
/// 独立拖拽测试脚本，完全绕开 GameManager 和 CardAsset。
/// 挂在任意空 GameObject 上，Inspector 拖入速攻牌对象。
///
///   T = 模拟速攻窗口开启（允许拖拽）
///   Y = 模拟速攻窗口关闭（禁止拖拽）
///   P = 打印所有手牌的 CanDrag 状态
/// </summary>
public class QuickPlayDragTest : MonoBehaviour
{
    [Header("拖入场景里的速攻牌对象（可多个）")]
    public GameObject[] quickPlayCards;

    void Start()
    {
        Debug.Log("[DragTest] 就绪");
        Debug.Log("  T = 开启拖拽");
        Debug.Log("  Y = 关闭拖拽");
        Debug.Log("  P = 打印状态");

        // 启动时先开启拖拽，方便直接测试
        SetCanDrag(true);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            SetCanDrag(true);
            Debug.Log("[DragTest] 拖拽已开启");
        }

        if (Input.GetKeyDown(KeyCode.Y))
        {
            SetCanDrag(false);
            Debug.Log("[DragTest] 拖拽已关闭");
        }

        if (Input.GetKeyDown(KeyCode.P))
            PrintStatus();
    }

    void SetCanDrag(bool value)
    {
        if (quickPlayCards == null) return;
        foreach (var card in quickPlayCards)
        {
            if (card == null) continue;
            var drag = card.GetComponent<QuickPlayDraggable>();
            if (drag != null)
            {
                drag.CanDrag = value;
                Debug.Log($"[DragTest] {card.name} CanDrag = {value}");
            }
            else
            {
                Debug.LogWarning($"[DragTest] {card.name} 上没有 QuickPlayDraggable！");
            }
        }
    }

    void PrintStatus()
    {
        if (quickPlayCards == null) return;
        foreach (var card in quickPlayCards)
        {
            if (card == null) continue;
            var drag = card.GetComponent<QuickPlayDraggable>();
            var sel  = card.GetComponent<QuickPlayTargetSelector>();
            Debug.Log($"[DragTest] {card.name} | CanDrag={drag?.CanDrag} | NeedsTarget={sel?.NeedsTarget()}");
        }
    }
}