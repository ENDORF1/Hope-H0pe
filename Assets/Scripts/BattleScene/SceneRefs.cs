using UnityEngine;

/// <summary>
/// 场景全局引用单例。
/// 挂在场景中任意常驻对象上，在 Inspector 里填好所有引用。
/// 所有通过 CardAsset 动态挂载的自定义效果脚本（WanWuGuiXu、TianYunZhongChang 等）
/// 都从这里获取场景引用，无需在 Prefab 上手动配置。
/// </summary>
public class SceneRefs : MonoBehaviour
{
    public static SceneRefs Instance { get; private set; }

    [Header("核心管理器")]
    [Tooltip("拖入场景中的 GameManager 对象")]
    public GameManager   GameManager;

    [Tooltip("拖入玩家的 DeckManager 对象（挂在 DeckCardBack 等玩家牌库对象上）")]
    public DeckManager   PlayerDeckManager;

    [Tooltip("拖入 AI 的 DeckManager 对象")]
    public DeckManager   AiDeckManager;

    [Tooltip("拖入玩家的 HandManager 对象（挂在 Handvisual 等玩家手牌区对象上）")]
    public HandManager   PlayerHandManager;

    [Tooltip("拖入 AI 的 HandManager 对象")]
    public HandManager   AiHandManager;

    [Header("部署区")]
    [Tooltip("拖入玩家的 DeployZone 对象")]
    public DeployZone    PlayerDeployZone;

    [Tooltip("拖入 AI 的 DeployZone 对象")]
    public DeployZone    AiDeployZone;

    [Header("玩家状态")]
    [Tooltip("拖入玩家的 PlayerState 对象（挂在 Lower Player Area 等玩家根对象上）")]
    public PlayerState   PlayerState;

    [Tooltip("拖入 AI 的 PlayerState 对象（挂在 Higher Player Area 等 AI 根对象上）")]
    public PlayerState   AiState;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}