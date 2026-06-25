using UnityEngine;

/// <summary>
/// GameManager 流程测试脚本
/// 挂在场景任意对象上，运行后观察 Console 和 MessageManager 动画
///
///   Space - 手动推进（模拟玩家点击结束窗口按钮）
///   P     - 打印当前状态
///   R     - 重启游戏循环（重新加载场景）
/// </summary>
[RequireComponent(typeof(GameManager))]
public class GameManagerTest : MonoBehaviour
{
    private GameManager _gm;

    void Awake()
    {
        _gm = GetComponent<GameManager>();
    }

    void Start()
    {
        Debug.Log("[GMTest] 测试就绪");
        Debug.Log("  Space = 结束当前速攻窗口");
        Debug.Log("  P     = 打印当前状态");
        Debug.Log("  R     = 重启");
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _gm.OnPlayerEndWindow();
            Debug.Log($"[GMTest] 手动结束窗口 | 当前时点: {_gm.CurrentTiming}");
        }

        if (Input.GetKeyDown(KeyCode.P))
            PrintStatus();

        if (Input.GetKeyDown(KeyCode.R))
            UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    void PrintStatus()
    {
        Debug.Log($"[GMTest] ── 当前状态 ──────────────────");
        Debug.Log($"  回合数:     {_gm.TurnNumber}");
        Debug.Log($"  当前阶段:   {_gm.CurrentPhase}");
        Debug.Log($"  当前时点:   {_gm.CurrentTiming}");
        Debug.Log($"  战斗格索引: {_gm.CurrentCombatSlot}");
        Debug.Log($"────────────────────────────────────");
    }
}
