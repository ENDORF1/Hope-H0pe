using UnityEngine;

/// <summary>
/// 牌库/手牌/部署区联动测试
/// 挂在任意空 GameObject 上
/// </summary>
public class DeckTest : MonoBehaviour
{
    [Header("引用")]
    [SerializeField] private DeckManager deckManager;
    [SerializeField] private HandManager handManager;

    [Header("按键设置")]
    [SerializeField] private KeyCode drawKey      = KeyCode.Space; // 抽一轮（5张）
    [SerializeField] private KeyCode drawOneKey   = KeyCode.Q;     // 抽一张
    [SerializeField] private KeyCode clearHandKey = KeyCode.C;     // 清空手牌

    void Update()
    {
        if (Input.GetKeyDown(drawKey))
        {
            Debug.Log("=== 抽一轮（5张）===");
            deckManager.DrawAndDistribute(5);
            Debug.Log($"牌库剩余：{deckManager.CardCount} 张，手牌：{handManager.TotalCardCount} 张");
        }

        if (Input.GetKeyDown(drawOneKey))
        {
            Debug.Log("=== 抽一张 ===");
            deckManager.DrawAndDistribute(1);
            Debug.Log($"牌库剩余：{deckManager.CardCount} 张，手牌：{handManager.TotalCardCount} 张");
        }

        if (Input.GetKeyDown(clearHandKey))
        {
            handManager.ClearHand();
            Debug.Log("手牌已清空");
        }
    }

    void Start()
    {
        Debug.Log("=== DeckTest 启动 ===");
        Debug.Log($"Space：抽5张 | Q：抽1张 | C：清空手牌");
        Debug.Log($"牌库初始：{deckManager.CardCount} 张");
    }
}