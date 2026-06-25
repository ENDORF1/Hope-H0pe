using UnityEngine;

/// <summary>
/// 遗物效果脚本基类。
/// 所有具体遗物效果脚本继承此类，重写需要的事件钩子。
/// 由 RunManager 在战斗场景初始化时动态挂载，并注入场景引用。
/// </summary>
public abstract class RelicBase : MonoBehaviour
{
    // ─────────────────────────────────────────────────
    // 资产与场景引用
    // ─────────────────────────────────────────────────

    /// <summary>该遗物对应的资产数据</summary>
    public RelicAsset Asset { get; private set; }

    protected GameManager   GameManager;
    protected PlayerState   PlayerState;
    protected PlayerState   EnemyState;
    protected DeployZone    PlayerDeployZone;
    protected DeployZone    EnemyDeployZone;
    protected DeckManager   PlayerDeckManager;

    // ─────────────────────────────────────────────────
    // 初始化
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 由 RunManager 挂载后立即调用，注入资产和场景引用。
    /// </summary>
    public void Initialize(RelicAsset asset, GameManager gameManager,
        PlayerState playerState, PlayerState enemyState,
        DeployZone playerZone, DeployZone enemyZone,
        DeckManager playerDeck)
    {
        Asset             = asset;
        GameManager       = gameManager;
        PlayerState       = playerState;
        EnemyState        = enemyState;
        PlayerDeployZone  = playerZone;
        EnemyDeployZone   = enemyZone;
        PlayerDeckManager = playerDeck;
        OnInitialized();
    }

    /// <summary>初始化完成后调用（替代 Awake/Start，此时所有引用已注入）</summary>
    protected virtual void OnInitialized() { }

    // ─────────────────────────────────────────────────
    // 辅助方法
    // ─────────────────────────────────────────────────

    /// <summary>统计己方场上存活的模块数量</summary>
    protected int GetAlivePlayerModuleCount()
    {
        if (PlayerDeployZone == null) return 0;
        int count = 0;
        for (int i = 0; i < PlayerDeployZone.CurrentSlotCount; i++)
        {
            var slotObj = PlayerDeployZone.GetSlot(i);
            if (slotObj == null) continue;
            var slot = slotObj.GetComponent<DeploySlot>();
            if (slot?.OccupyingModuleInstance != null && slot.OccupyingModuleInstance.IsAlive)
                count++;
        }
        return count;
    }

    /// <summary>统计敌方场上存活的模块数量</summary>
    protected int GetAliveEnemyModuleCount()
    {
        if (EnemyDeployZone == null) return 0;
        int count = 0;
        for (int i = 0; i < EnemyDeployZone.CurrentSlotCount; i++)
        {
            var slotObj = EnemyDeployZone.GetSlot(i);
            if (slotObj == null) continue;
            var slot = slotObj.GetComponent<DeploySlot>();
            if (slot?.OccupyingModuleInstance != null && slot.OccupyingModuleInstance.IsAlive)
                count++;
        }
        return count;
    }

    // ─────────────────────────────────────────────────
    // 事件钩子（子类按需重写）
    // ─────────────────────────────────────────────────

    /// <summary>每回合开始时调用</summary>
    public virtual void OnTurnStart(int turnNumber) { }

    /// <summary>每回合结束时调用</summary>
    public virtual void OnTurnEnd(int turnNumber) { }

    /// <summary>玩家抽牌后调用</summary>
    public virtual void OnAfterDraw() { }

    /// <summary>战斗阶段开始前调用</summary>
    public virtual void OnBeforeBattle() { }

    /// <summary>战斗阶段结束后调用</summary>
    public virtual void OnAfterBattle() { }

    /// <summary>导弹阶段结束后调用</summary>
    public virtual void OnAfterMissile() { }

    /// <summary>玩家打出速攻牌时调用</summary>
    public virtual void OnPlayerPlayQuickCard() { }

    /// <summary>胜利时调用</summary>
    public virtual void OnVictory() { }

    /// <summary>败北时调用</summary>
    public virtual void OnDefeat() { }
}
