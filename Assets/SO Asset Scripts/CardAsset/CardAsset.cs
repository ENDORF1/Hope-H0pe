using UnityEngine;
using System.Collections.Generic;

public enum AttackBehaviorType
{
    [InspectorName("实弹（每回合攻击）")]   Ballistic,
    [InspectorName("冷兵器（翻开后冷却）")] Melee,
}

// ─────────────────────────────────────────────────────
// 卡牌类型
// 通过卡背图片区分，代码层用枚举明确
// ─────────────────────────────────────────────────────
public enum CardType
{
    Module,     // 模块牌：抽到后直接塞入部署区，卡背朝下
    QuickPlay   // 速攻牌：抽到后进入手牌，玩家自己翻面
}

// ─────────────────────────────────────────────────────
// 模块类型
// ─────────────────────────────────────────────────────
public enum ModuleType
{
    Melee,       // 冷兵器
    Missile,     // 导弹
    Laser,       // 激光
    Ballistic,   // 实弹
    DroneHost,   // 无人机母体
    Sixth        // 第六类（待命名）
}

// ─────────────────────────────────────────────────────
// 无人机类型
// ─────────────────────────────────────────────────────
public enum DroneType
{
    Attack,   // 攻击型：每回合攻击对位模块，永久
    Heal,     // 治疗型：每回合治疗附着模块，永久
    Builder,  // 建造型：下回合提供额外部署格，一次性
    Shield    // 护盾型：10血0攻，优先承受伤害
}

// ─────────────────────────────────────────────────────
// 速攻牌使用时机
// ─────────────────────────────────────────────────────
public enum QuickPlayTiming
{
    // ── 精确时点 ──────────────────────────────────────
    [InspectorName("任意时刻")]               AnyTime,
    [InspectorName("抽牌前")]                 BeforeDraw,
    [InspectorName("抽牌后／部署前")]         AfterDraw,
    [InspectorName("战斗前")]                 AfterDeploy,
    [InspectorName("某格翻牌前")]             BeforeFlip,
    [InspectorName("某格翻牌后")]             AfterFlip,
    [InspectorName("某格效果触发前")]         BeforeEffect,
    [InspectorName("某格效果触发后")]         AfterEffect,
    [InspectorName("战斗阶段全部结算完毕后")] AfterBattle,
    [InspectorName("导弹发射前")]             BeforeMissile,
    [InspectorName("导弹结算后")]             AfterMissile,
    [InspectorName("回合结束时")]             TurnEnd,

    // ── 粗略时段（整个阶段内均可使用）────────────────
    [InspectorName("整个战斗阶段")]               DuringBattle,
    [InspectorName("整个导弹阶段")]               DuringMissile,
    [InspectorName("战斗阶段＋导弹阶段")]         DuringBattleAndMissile,
    [InspectorName("抽牌阶段＋战斗阶段")]         DuringDrawAndBattle,
    [InspectorName("非抽牌阶段（战斗起）")]       AfterDrawPhase,
}

// ─────────────────────────────────────────────────────
// 速攻牌目标
// ─────────────────────────────────────────────────────
public enum QuickPlayTarget
{
    // ── 无目标 ────────────────────────────────────────
    NoTarget,               // 无需选择目标，向上拖直接触发

    // ── 单个模块 ──────────────────────────────────────
    YourModule,             // 己方任意一个模块
    EnemyModule,            // 敌方任意一个模块
    AnyModule,              // 任意一个模块（己方或敌方）

    // ── 全体模块 ──────────────────────────────────────
    AllYourModules,         // 己方所有模块
    AllEnemyModules,        // 敌方所有模块
    AllModules,             // 所有模块（双方）

    // ── 随机模块 ──────────────────────────────────────
    RandomYourModule,       // 随机一个己方模块
    RandomEnemyModule,      // 随机一个敌方模块

    // ── 玩家本体 ──────────────────────────────────────
    YourPlayer,             // 己方玩家本体
    EnemyPlayer,            // 敌方玩家本体

    // ── 模块或玩家本体（任意对象）────────────────────
    YourObject,             // 己方任意对象（模块 或 玩家本体）
    EnemyObject,            // 敌方任意对象（模块 或 玩家本体）
    AnyObject,              // 任意对象（双方模块 或 双方玩家本体）

    // ── 部署格 ────────────────────────────────────────
    YourSlot,               // 己方任意部署格（含空格）
    EnemySlot,              // 敌方任意部署格（含空格）
    AnySlot,                // 任意部署格（双方）
    YourEmptySlot,          // 己方空格
    EnemyEmptySlot,         // 敌方空格

    // ── 无人机 ────────────────────────────────────────
    YourDrones,             // 己方所有无人机
    EnemyDrones,            // 敌方所有无人机
    AnyDrone,               // 任意一个无人机
    RandomYourDrone,        // 随机一个己方无人机
    RandomEnemyDrone,       // 随机一个敌方无人机

    // ── 手牌 ──────────────────────────────────────────
    YourHand,               // 己方整手牌
    EnemyHand,              // 敌方整手牌
    YourHandCard,           // 己方手牌中指定一张
    EnemyHandCard,          // 敌方手牌中指定一张
    RandomEnemyHand,        // 随机敌方手牌一张

    // ── 牌库 ──────────────────────────────────────────
    YourDeck,               // 己方牌库
    EnemyDeck,              // 敌方牌库
    TopOfYourDeck,          // 己方牌库顶
    TopOfEnemyDeck,         // 敌方牌库顶
    YourDiscard,            // 己方弃牌堆
    EnemyDiscard,           // 敌方弃牌堆

    // ── 玩家总血量（直接伤害/治疗本体，不经过模块）──
    YourTotalHealth,        // 己方总血量
    EnemyTotalHealth,       // 敌方总血量
}

// ─────────────────────────────────────────────────────
// 效果类型枚举（常用效果）
// ─────────────────────────────────────────────────────
public enum EffectType
{
    [InspectorName("造成固定伤害")]           DealDamage,
    [InspectorName("造成随机伤害")]           DealDamageRandom,
    [InspectorName("每模块造成X伤害")]        DealDamagePerModule,

    [InspectorName("治疗固定量")]             Heal,
    [InspectorName("每无人机治疗X")]          HealPerDrone,

    [InspectorName("过热（盖伏）")]           Overheat,
    [InspectorName("摧毁模块")]               DestroyModule,
    [InspectorName("摧毁敌方所有模块")]       DestroyAllEnemyModules,

    [InspectorName("抽X张牌")]               Draw,
    [InspectorName("抽取所有剩余牌")]         DrawAll,
    [InspectorName("摧毁敌方手牌")]           DestroyEnemyHand,
    [InspectorName("摧毁敌方牌库")]           DestroyEnemyDeck,
    [InspectorName("摧毁敌方部署区和手牌")]   DestroyEnemyDeploy,
    [InspectorName("偷取卡牌")]               StealCard,
    [InspectorName("弃牌")]                   Discard,

    [InspectorName("生成无人机")]             SpawnDrones,
    [InspectorName("摧毁无人机")]             DestroyDrones,

    [InspectorName("揭示敌方场地")]           RevealEnemyField,
    [InspectorName("给对手额外回合")]         ExtraTurn,
    [InspectorName("对手本体免疫伤害")]       ImmunityToPlayer,
    [InspectorName("窃取生命值")]             StealHealth,

    [InspectorName("自定义脚本")]             Custom
}

// ─────────────────────────────────────────────────────
// 单个效果数据
// ─────────────────────────────────────────────────────
[System.Serializable]
public class CardEffect
{
    [Header("效果类型")]
    [Tooltip("选择这个效果做什么：造成伤害、治疗、摧毁模块……")]
    public EffectType EffectType;

    [Tooltip("效果作用于谁：敌方模块、己方模块、任意模块、玩家本体……")]
    public QuickPlayTarget Target;

    [Header("数值")]
    [Tooltip("主数值。\n• 造成伤害 / 治疗 → 填伤害量或治疗量\n• 抽牌 → 填张数\n• 随机伤害 → 填最小值\n• 其他效果填0即可忽略")]
    public int 数值;

    [Tooltip("仅【随机伤害】时填写：随机范围的最大值。\n例：最小值=3，最大值=6，则随机造成3~6点伤害。\n其他效果填0忽略。")]
    public int 随机最大值;

    [Tooltip("仅【生成无人机】时填写：生成几个无人机。\n其他效果填0忽略。")]
    public int 无人机数量;

    [Tooltip("仅【生成无人机】时填写：生成哪种无人机。\n其他效果忽略。")]
    public DroneType 无人机类型;

    [Header("自定义脚本（效果类型选 Custom 时才填）")]
    [Tooltip("脚本类名，留空则不执行。")]
    public string ScriptName;

    [Tooltip("传给自定义脚本的参数1。")]
    public int ScriptParam1;

    [Tooltip("传给自定义脚本的参数2。")]
    public int ScriptParam2;

    [Header("效果描述（仅UI显示用，不影响逻辑）")]
    [TextArea(1, 2)]
    [Tooltip("在卡牌详情界面显示的文字说明，不填也不影响效果执行。")]
    public string 效果说明;

    // 保留原字段名供代码访问（属性转发）
    public int IntValue  => 数值;
    public int IntValue2 => 随机最大值;
    public int DroneCount => 无人机数量;
    public DroneType DroneType => 无人机类型;
    public string Description => 效果说明;
}

// ─────────────────────────────────────────────────────
// 角色对话数据
// ─────────────────────────────────────────────────────
public enum DialogueSpeaker
{
    Player,   // 玩家角色说
    AI,       // AI 角色说
    Narrator  // 旁白
}

[System.Serializable]
public class CardDialogue
{
    public DialogueSpeaker Speaker;
    [TextArea(1, 3)]
    public string Line;
}

// ─────────────────────────────────────────────────────
// CardAsset 主体
// ─────────────────────────────────────────────────────
[CreateAssetMenu(fileName = "NewCard", menuName = "Cards/CardAsset")]
public class CardAsset : ScriptableObject
{
    [Header("基本信息")]
    public string CardName;
    [TextArea(2, 3)]
    public string Description;
    public Sprite CardImage;
    public Sprite CardBackImage;

    [Header("卡名动画（可选）")]
    [Tooltip("勾选后对卡名应用 TMP 动画效果")]
    public bool NameAnimated = false;

    [Tooltip("TMP 动画标签名。留空默认 waving。\n可用：waving / spreading / palette / pivoting / shearing / dangling / shaking / swinging / jumping / growing / fading / funky / changing / sketchy / pivotingc")]
    public string NameAnimTag = "waving";

    [Tooltip("卡名颜色。UsePalette 勾选时此项无效。")]
    public Color NameColor = Color.white;

    [Tooltip("勾选后在动画标签外套一层 palette 渐变色，此时 NameColor 无效")]
    public bool NameUsePalette = false;

    [Header("卡牌类型")]
    public CardType CardType;

    // ── 模块牌专属 ────────────────────────────────────
    [Header("模块牌属性（CardType = Module 时有效）")]
    public ModuleType ModuleType;
    public int Health;
    public int Attack;

    [Header("冷兵器专属")]
    public int CooldownTurns;         // 冷却回合数

    [Header("导弹专属")]
    public int MissileCount;          // 导弹数量
    public int SplashDamage;          // 溅射伤害
    public int SplashRange;           // 溅射范围

    [Header("无人机母体专属")]
    public DroneType DroneType;       // 生产的无人机类型
    public int DronesToSpawn;         // 生产数量
    [Tooltip("生产的无人机对应的 CardAsset（拖入无人机的 ScriptableObject）")]
    public CardAsset DroneAsset;      // 无人机专属 Asset

    [Header("无人机专属")]
    public int DroneAttack;           // 攻击型无人机攻击力
    public int DroneHeal;             // 治疗型无人机治疗量
    public int DroneHealth;           // 无人机血量
    public int BuildSlots;            // 建造型提供的格子数
    [Tooltip("无人机自定义逻辑脚本类名（留空则不挂载）")]
    public string DroneScriptName;    // 无人机自定义脚本

    [Header("模块自定义效果脚本")]
    public string ModuleScriptName;
    public int ModuleScriptParam;

    [Header("战斗")]
    [Tooltip("勾选后该模块参与互攻，攻击逻辑由 AttackBehavior 决定")]
    public bool CanAttack = false;

    [Tooltip("攻击逻辑对标的模块类型。CanAttack 勾选时生效。")]
    public AttackBehaviorType AttackBehavior = AttackBehaviorType.Ballistic;

    [Header("特殊手牌光效")]
    [Tooltip("勾选后，此牌在手牌窗口期可用时显示彩虹循环呼吸光而非普通绿光")]
    public bool RainbowGlowWhenPlayable = false;

    // ── 速攻牌专属 ────────────────────────────────────
    [Header("速攻牌属性（CardType = QuickPlay 时有效）")]
    public QuickPlayTiming UsableTiming;  // 可使用时机

    [Tooltip("勾选后，打出时需要玩家手动指定一个目标（拖拽到目标上松手）。\n不勾选则向上拖超过阈值直接触发效果。")]
    public bool RequiresTarget = false;   // ← 新增：是否需要手动指定目标

    [Header("速攻牌效果列表（可多个）")]
    public List<CardEffect> Effects = new List<CardEffect>();

    // ── 角色对话 ──────────────────────────────────────
    [Header("打出时角色对话（可多条，随机选一条）")]
    public List<CardDialogue> OnPlayDialogues = new List<CardDialogue>();

    // ─────────────────────────────────────────────────
    // 辅助方法
    // ─────────────────────────────────────────────────

    public string GetDisplayName()
    {
        return string.IsNullOrEmpty(CardName) ? name : CardName;
    }

    public string GetFullDescription()
    {
        return string.IsNullOrEmpty(Description) ? "" : Description;
    }
}