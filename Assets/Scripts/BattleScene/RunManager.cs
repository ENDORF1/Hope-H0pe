using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 跨场景持久化管理器。
/// 负责：
///   1. 保存本局游戏积累的遗物列表
///   2. 处理姐妹专属掉落规则
///   3. 战斗开始时将遗物效果脚本挂载到场景并订阅事件
///
/// 使用方式：
///   - 场景初始化时调用 InitRelicsInScene()
///   - 战斗胜利时调用 ResolveRelicDrop() 处理掉落
///   - 玩家打出速攻牌时调用 NotifyQuickCardPlayed()
/// </summary>
public class RunManager : MonoBehaviour
{
    public static RunManager Instance { get; private set; }

    // ─────────────────────────────────────────────────
    // 姐妹专属遗物配置
    // ─────────────────────────────────────────────────

    [Header("姐妹专属遗物（在 Inspector 中填入）")]
    [Tooltip("姐姐的遗物：至亲者的沉默")]
    [SerializeField] private RelicAsset sisterRelicA; // 姐姐的遗物

    [Tooltip("妹妹的遗物：至亲者的遗愿")]
    [SerializeField] private RelicAsset sisterRelicB; // 妹妹的遗物

    [Tooltip("未授权的输出（无效果特殊道具）")]
    [SerializeField] private RelicAsset unauthorizedOutput;

    [Header("姐妹专属掉落概率")]
    [Tooltip("妹妹同时持有姐姐遗物时，击败妹妹获得两件遗物的概率")]
    [Range(0f, 1f)]
    [SerializeField] private float bothRelicChance = 0.05f;

    [Tooltip("击败妹妹时获得特殊道具（未授权的输出）而非遗物的概率")]
    [Range(0f, 1f)]
    [SerializeField] private float unauthorizedOutputChance = 0.1f;

    // ─────────────────────────────────────────────────
    // 运行时状态
    // ─────────────────────────────────────────────────

    /// <summary>当前持有的遗物列表</summary>
    private List<RelicAsset> _relics = new List<RelicAsset>();

    /// <summary>当前已挂载的遗物效果脚本实例</summary>
    private List<RelicBase> _relicInstances = new List<RelicBase>();

    /// <summary>当前订阅的 GameManager</summary>
    private GameManager _subscribedGameManager;

    // ─────────────────────────────────────────────────
    // 初始化
    // ─────────────────────────────────────────────────

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        UnsubscribeFromGameManager();
    }

    // ─────────────────────────────────────────────────
    // 遗物列表管理
    // ─────────────────────────────────────────────────

    /// <summary>当前持有的遗物（只读）</summary>
    public IReadOnlyList<RelicAsset> Relics => _relics.AsReadOnly();

    /// <summary>添加一件遗物</summary>
    public void AddRelic(RelicAsset relic)
    {
        if (relic == null) return;
        _relics.Add(relic); // 允许同一件遗物多次持有（后续可根据需求限制）
        Debug.Log($"[RunManager] 获得遗物：{relic.GetDisplayName()}");
    }

    /// <summary>是否持有指定遗物</summary>
    public bool HasRelic(RelicAsset relic) => relic != null && _relics.Contains(relic);

    /// <summary>清空所有遗物（游戏结束/重开时调用）</summary>
    public void ClearRelics()
    {
        _relics.Clear();
        _relicInstances.Clear();
        Debug.Log("[RunManager] 遗物列表已清空");
    }

    // ─────────────────────────────────────────────────
    // 姐妹专属掉落规则
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 处理击败指定敌人后的遗物掉落。
    /// 包含姐妹专属规则：
    ///   - 姐姐持有妹妹遗物时：只能得到姐姐的遗物
    ///   - 妹妹持有姐姐遗物时：小概率同时得到两件，小概率什么都不得只得到特殊道具
    /// </summary>
    /// <param name="enemyRelics">敌方当前持有的遗物列表</param>
    /// <param name="isEnemySister">敌方是否是姐姐</param>
    /// <param name="isEnemyLittleSister">敌方是否是妹妹</param>
    /// <param name="isPlayerSister">玩家是否是姐姐（用于判断未授权输出的版本）</param>
    /// <returns>本次掉落的遗物列表（可能为空，此时应给予特殊道具）</returns>
    public List<RelicAsset> ResolveRelicDrop(
        List<RelicAsset> enemyRelics,
        bool isEnemySister,
        bool isEnemyLittleSister,
        bool isPlayerSister)
    {
        var result = new List<RelicAsset>();

        if (enemyRelics == null || enemyRelics.Count == 0)
            return result;

        // ── 规则一：姐姐持有妹妹遗物时 ──────────────────
        // 击败持有妹妹遗物的姐姐，永远只能得到姐姐自己的遗物
        if (isEnemySister && enemyRelics.Contains(sisterRelicB))
        {
            Debug.Log("[RunManager] 姐姐守护了妹妹的遗物——只能得到姐姐的遗物");
            if (enemyRelics.Contains(sisterRelicA))
                result.Add(sisterRelicA);
            return result;
        }

        // ── 规则二：妹妹持有姐姐遗物时 ──────────────────
        if (isEnemyLittleSister && enemyRelics.Contains(sisterRelicA))
        {
            float roll = Random.value;

            // 小概率：什么遗物都得不到，只得到特殊道具
            if (roll < unauthorizedOutputChance)
            {
                Debug.Log("[RunManager] 妹妹赌输了——什么遗物都没得到，获得未授权的输出");
                // 返回空列表，调用方负责给予特殊道具
                // isPlayerSister 决定给哪个版本
                GiveUnauthorizedOutput(isPlayerSister);
                return result;
            }

            // 小概率：同时得到两件遗物
            float remainingRoll = (roll - unauthorizedOutputChance) / (1f - unauthorizedOutputChance);
            if (remainingRoll < bothRelicChance)
            {
                Debug.Log("[RunManager] 妹妹赌了一把，全失去了——同时得到两件遗物");
                result.AddRange(enemyRelics);
                return result;
            }
        }

        // ── 标准规则：随机得到一件 ────────────────────────
        int randomIndex = Random.Range(0, enemyRelics.Count);
        result.Add(enemyRelics[randomIndex]);
        Debug.Log($"[RunManager] 标准掉落：{enemyRelics[randomIndex].GetDisplayName()}");
        return result;
    }

    /// <summary>
    /// 给予玩家"未授权的输出"特殊道具。
    /// isForSister 决定显示哪个版本的图标和描述。
    /// </summary>
    private void GiveUnauthorizedOutput(bool isForSister)
    {
        if (unauthorizedOutput == null)
        {
            Debug.LogWarning("[RunManager] unauthorizedOutput 未赋值");
            return;
        }
        // 特殊道具不加入遗物列表，只做展示
        // 实际 UI 展示逻辑由调用方处理，这里只打印日志
        string desc = unauthorizedOutput.GetDescription(isForSister);
        string icon = isForSister ? "QAQ / <3" : "#@!";
        Debug.Log($"[RunManager] 给予特殊道具：{unauthorizedOutput.GetDisplayName()} | 图标:{icon} | {desc}");
    }

    // ─────────────────────────────────────────────────
    // 场景初始化：挂载遗物效果脚本
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 在战斗场景加载后调用。
    /// 将所有持有的遗物效果脚本动态挂载到 host 对象上，并订阅 GameManager 事件。
    /// </summary>
    public void InitRelicsInScene(
        GameObject  host,
        GameManager gameManager,
        PlayerState playerState,
        PlayerState enemyState,
        DeployZone  playerZone,
        DeployZone  enemyZone,
        DeckManager playerDeck)
    {
        if (host == null)
        {
            Debug.LogError("[RunManager] InitRelicsInScene: host 为空");
            return;
        }

        // 清理上一场的实例
        foreach (var old in _relicInstances)
            if (old != null) Destroy(old);
        _relicInstances.Clear();

        // 挂载当前所有遗物的效果脚本
        foreach (var relicAsset in _relics)
        {
            if (relicAsset == null) continue;
            if (string.IsNullOrEmpty(relicAsset.RelicScriptName)) continue;

            System.Type t = System.Type.GetType(relicAsset.RelicScriptName);
            if (t == null || !typeof(RelicBase).IsAssignableFrom(t))
            {
                Debug.LogWarning($"[RunManager] 找不到遗物脚本类型：{relicAsset.RelicScriptName}");
                continue;
            }

            RelicBase instance = (RelicBase)host.AddComponent(t);
            instance.Initialize(relicAsset, gameManager,
                playerState, enemyState, playerZone, enemyZone, playerDeck);
            _relicInstances.Add(instance);

            Debug.Log($"[RunManager] 挂载遗物效果：{relicAsset.GetDisplayName()}");
        }

        // 订阅 GameManager 事件
        SubscribeToGameManager(gameManager);
    }

    // ─────────────────────────────────────────────────
    // GameManager 事件订阅与转发
    // ─────────────────────────────────────────────────

    private void SubscribeToGameManager(GameManager gm)
    {
        UnsubscribeFromGameManager();
        _subscribedGameManager = gm;
        gm.OnTurnEnd += HandleTurnEnd;
        Debug.Log("[RunManager] 已订阅 GameManager 事件");
    }

    private void UnsubscribeFromGameManager()
    {
        if (_subscribedGameManager == null) return;
        _subscribedGameManager.OnTurnEnd -= HandleTurnEnd;
        _subscribedGameManager = null;
    }

    private void HandleTurnEnd()
    {
        int turnNumber = _subscribedGameManager != null ? _subscribedGameManager.TurnNumber : 0;
        foreach (var r in _relicInstances)
            if (r != null) r.OnTurnEnd(turnNumber);
    }

    // ─────────────────────────────────────────────────
    // 公开转发接口（由 GameManager 在对应时机调用）
    // ─────────────────────────────────────────────────

    public void NotifyTurnStart(int turnNumber)
    {
        foreach (var r in _relicInstances) r?.OnTurnStart(turnNumber);
    }

    public void NotifyAfterDraw()
    {
        foreach (var r in _relicInstances) r?.OnAfterDraw();
    }

    public void NotifyBeforeBattle()
    {
        foreach (var r in _relicInstances) r?.OnBeforeBattle();
    }

    public void NotifyAfterBattle()
    {
        foreach (var r in _relicInstances) r?.OnAfterBattle();
    }

    public void NotifyAfterMissile()
    {
        foreach (var r in _relicInstances) r?.OnAfterMissile();
    }

    /// <summary>玩家每次打出速攻牌时调用</summary>
    public void NotifyQuickCardPlayed()
    {
        foreach (var r in _relicInstances) r?.OnPlayerPlayQuickCard();
    }

    public void NotifyVictory()
    {
        foreach (var r in _relicInstances) r?.OnVictory();
    }

    public void NotifyDefeat()
    {
        foreach (var r in _relicInstances) r?.OnDefeat();
    }
}
