using UnityEngine;

/// <summary>
/// 调试用快速启动器。
/// 挂在场景任意对象上，勾选 Enable Quick Start 后，
/// 跳过所有开场动画、对话、MessageManager 播报，直接发牌进入速攻窗口。
///
/// 运行时按 F1 可随时切换开关（下次运行生效）。
/// </summary>
public class QuickStartDebugger : MonoBehaviour
{
    [Header("开关")]
    [Tooltip("勾选后跳过所有开场流程，直接发牌")]
    public bool enableQuickStart = true;

    [Header("引用")]
    [SerializeField] private GameManager gameManager;

    void Awake()
    {
        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        if (gameManager == null)
        {
            Debug.LogWarning("[QuickStart] 找不到 GameManager，脚本无效。");
            return;
        }

        gameManager.SkipIntroForDebug = enableQuickStart;
        Debug.Log($"[QuickStart] 快速启动 = {enableQuickStart}");
    }

    void Update()
    {
        // 按 F1 切换（下次运行生效）
        if (Input.GetKeyDown(KeyCode.F1))
        {
            enableQuickStart = !enableQuickStart;
            Debug.Log($"[QuickStart] 开关切换 → {(enableQuickStart ? "开启" : "关闭")}（下次运行生效）");
        }
    }
}