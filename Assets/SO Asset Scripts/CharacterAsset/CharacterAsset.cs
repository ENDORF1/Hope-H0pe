using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 角色数据资产（ScriptableObject）。
/// 在 Project 窗口右键 → Create → Characters → Character Asset 创建。
/// 每个角色对应一个 .asset 文件，新增角色不需要改代码。
/// </summary>
[CreateAssetMenu(fileName = "NewCharacter", menuName = "Characters/Character Asset")]
public class CharacterAsset : ScriptableObject
{
    [Header("基本信息")]
    [Tooltip("角色中文名，显示在选角界面")]
    public string CharacterName;

    [Tooltip("角色英文名/副标题")]
    public string CharacterNameEn;

    [Tooltip("所属阵营")]
    public TitleScreenManager.Faction Faction;

    [Tooltip("初始最大生命值")]
    public int MaxHealth = 100;

    [Tooltip("角色简介，显示在选角界面的描述栏")]
    [TextArea(3, 6)]
    public string Description;

    [Header("立绘")]
    [Tooltip("角色立绘，用于选角界面卡面展示")]
    public Sprite Portrait;

    [Header("选角提示")]
    [Tooltip("Hover 时显示的专属文案，留空则使用默认文案")]
    public string PromptLine;

    [Header("卡背")]
    [Tooltip("卡背主文字，飞入时显示")]
    public string BackTextMain = "MAKE\nA WISH";

    [Header("退场动画")]
    [Tooltip("此角色的退场动画资产，留空使用默认飞升")]
    public ExitAnimationAsset ExitAnimation;

    [Header("战斗界面")]
    [Tooltip("此角色在 Battle Scene 中的肖像 Prefab（带 OneCardManager 的预制体）")]
    public GameObject BattlePortraitPrefab;

    [Header("角色差异")]
    [Tooltip("初始部署格数量")]
    [Range(1, 8)]
    public int DeploySlots = 5;

    [Tooltip("牌库上限")]
    public int DeckCapacity = 30;

    [Header("初始牌库")]
    [Tooltip("本角色的起始卡组，游戏开始时写入 DeckManager")]
    public List<CardAsset> StartingDeck = new List<CardAsset>();
}