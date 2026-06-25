using UnityEngine;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// 单张卡牌的 UI 管理脚本
/// 职责：从 CardAsset 读取数据并显示到 UI 上
/// 不负责游戏逻辑（抽牌、打出、伤害等）
/// </summary>
public class OneCardManager : MonoBehaviour
{
    [Header("卡牌数据")]
    public CardAsset cardAsset;

    /// <summary>预览窗口的 OneCardManager（悬停时同步显示）</summary>
    public OneCardManager PreviewManager;

    // ── 通用文本 ──────────────────────────────────────
    [Header("通用文本")]
    public TextMeshProUGUI NameText;
    public TextMeshProUGUI DescriptionText;

    // ── 模块牌文本 ────────────────────────────────────
    [Header("模块牌文本")]
    public TextMeshProUGUI HealthText;
    public TextMeshProUGUI AttackText;
    public TextMeshProUGUI ModuleTypeText;

    // 冷兵器
    public TextMeshProUGUI CooldownText;

    // 导弹
    public TextMeshProUGUI MissileCountText;
    public TextMeshProUGUI SplashDamageText;
    public TextMeshProUGUI SplashDamageValueText;  // 仅显示数值，无动画
    public TextMeshProUGUI SplashRangeText;

    // 无人机母体
    public TextMeshProUGUI DroneTypeText;
    public TextMeshProUGUI DronesToSpawnText;

    // 无人机本体
    public TextMeshProUGUI DroneAttackText;
    public TextMeshProUGUI DroneHealText;
    public TextMeshProUGUI DroneHealthText;
    public TextMeshProUGUI DroneBuildText;

    // ── 速攻牌文本 ────────────────────────────────────
    [Header("速攻牌文本")]
    public TextMeshProUGUI TimingText;       // 使用时机
    public TextMeshProUGUI EffectSummaryText; // 效果摘要（自动生成）

    // ── 图片 ──────────────────────────────────────────
    [Header("图片")]
    public Image CardGraphicImage;
    public Image CardBackImage;
    public Image CardFaceGlowImage;
    public Image ModuleIconImage;
    public Image QuickPlayIconImage;

    // ── 面板 ──────────────────────────────────────────
    [Header("UI 面板")]
    public GameObject ModulePanel;
    public GameObject QuickPlayPanel;
    public GameObject HealthPanel;
    public GameObject AttackPanel;
    public GameObject CooldownPanel;
    public GameObject MissilePanel;
    public GameObject DroneHostPanel;

    // ── 关键词样式词典 ────────────────────────────────
    [Header("关键词样式")]
    // 拖入 KeywordStyleDictionary 资产（Project 窗口 右键→Create→Cards→Keyword Style Dictionary）
    // 所有速攻牌共用同一份词典资产
    [SerializeField] private KeywordStyleDictionary keywordDict;

    [Header("使用时机 Wave 动画")]
    [SerializeField] private float timingWaveAmplitude = 2f;
    [SerializeField] private float timingWaveFrequency = 0.5f;

    // ── 高亮状态 ──────────────────────────────────────
    private bool _canBeUsedNow = false;
    public bool CanBeUsedNow
    {
        get => _canBeUsedNow;
        set
        {
            _canBeUsedNow = value;
            if (CardFaceGlowImage != null)
                CardFaceGlowImage.enabled = value;
        }
    }

    // ─────────────────────────────────────────────────
    void Awake()
    {
        if (cardAsset != null)
            ReadCardFromAsset();
    }

    // ─────────────────────────────────────────────────
    // 主入口：从 CardAsset 读取并刷新所有 UI
    // ─────────────────────────────────────────────────
    public void ReadCardFromAsset()
    {
        if (cardAsset == null) return;

        // 通用
        if (NameText != null)
            NameText.text = BuildNameText(cardAsset);
        if (DescriptionText != null)
            DescriptionText.text = KeywordStyler.Apply(cardAsset.GetFullDescription(), keywordDict);
        if (CardGraphicImage != null && cardAsset.CardImage != null)
            CardGraphicImage.sprite = cardAsset.CardImage;

        // 根据卡牌类型切换面板
        bool isModule = cardAsset.CardType == CardType.Module;
        if (ModulePanel != null)    ModulePanel.SetActive(isModule);
        if (QuickPlayPanel != null) QuickPlayPanel.SetActive(!isModule);

        if (isModule)
            ShowModuleInfo();
        else
            ShowQuickPlayInfo();

        // 同步预览窗口
        if (PreviewManager != null)
        {
            PreviewManager.cardAsset = cardAsset;
            PreviewManager.ReadCardFromAsset();
        }
    }

    // ─────────────────────────────────────────────────
    // 模块牌显示
    // ─────────────────────────────────────────────────
    private void ShowModuleInfo()
    {
        // 模块类型
        if (ModuleTypeText != null)
            ModuleTypeText.text = KeywordStyler.Apply(GetModuleTypeName(cardAsset.ModuleType), keywordDict);

        // 攻击/血量
        if (AttackPanel != null) AttackPanel.SetActive(true);
        if (HealthPanel != null) HealthPanel.SetActive(true);
        if (AttackText != null)  AttackText.text = cardAsset.Attack.ToString();
        if (HealthText != null)  HealthText.text = cardAsset.Health.ToString();

        // 隐藏所有专属面板，按类型按需开启
        SetActive(CooldownPanel,  false);
        SetActive(MissilePanel,   false);
        SetActive(DroneHostPanel, false);

        switch (cardAsset.ModuleType)
        {
            case ModuleType.Melee:
                SetActive(CooldownPanel, true);
                if (CooldownText != null)
                    CooldownText.text = $"<wave><color=#4499FF>冷却</color></wave><color=#FFFFFF>【{cardAsset.CooldownTurns}】回合</color>";
                break;

            case ModuleType.Missile:
                SetActive(MissilePanel, true);
                if (MissileCountText != null)
                    MissileCountText.text = $"<wave><color=#FF8800>导弹</color></wave><color=#FFFFFF>【{cardAsset.MissileCount}】发</color>";
                if (SplashDamageText != null)
                    SplashDamageText.text = $"<shake><color=#FF4444>溅射伤害</color></shake><color=#FFFFFF>【{cardAsset.SplashDamage}】</color>";
                if (SplashRangeText != null)
                    SplashRangeText.text = $"<shake><color=#FF4444>溅射范围</color></shake><color=#FFFFFF>【{cardAsset.SplashRange}】</color>";
                break;

            case ModuleType.DroneHost:
                SetActive(DroneHostPanel, true);
                if (DroneTypeText != null)
                    DroneTypeText.text = GetDroneTypeStyled(cardAsset.DroneType);
                if (DronesToSpawnText != null)
                    DronesToSpawnText.text = $"<swing><color=#FF8800>生成</color></swing><color=#FFFFFF>【{cardAsset.DronesToSpawn}】个</color>";
                break;

            case ModuleType.Laser:
            case ModuleType.Ballistic:
            case ModuleType.Sixth:
                // 无专属面板，描述文本已显示
                break;
        }
    }

    // ─────────────────────────────────────────────────
    // 速攻牌显示
    // ─────────────────────────────────────────────────
    private void ShowQuickPlayInfo()
    {
        // 隐藏模块专属面板
        SetActive(AttackPanel,   false);
        SetActive(HealthPanel,   false);
        SetActive(CooldownPanel, false);
        SetActive(MissilePanel,  false);
        SetActive(DroneHostPanel,false);

        // 使用时机
        if (TimingText != null)
            TimingText.text = $"<wave amplitude={timingWaveAmplitude} frequency={timingWaveFrequency}>{GetTimingName(cardAsset.UsableTiming)}</wave>";

        // 效果摘要：遍历所有效果自动生成描述，再经词典样式化
        if (EffectSummaryText != null)
        {
            string raw    = BuildEffectSummary(cardAsset.Effects);
            string styled = KeywordStyler.Apply(raw, keywordDict);
            EffectSummaryText.text = styled;
        }
    }

    // ─────────────────────────────────────────────────
    // 效果摘要生成
    // 规则：
    //   · 优先使用 CardEffect.Description 手填描述（词典也会对它生效）
    //   · 无手填描述时自动生成，数值用【】包裹，关键词与 KeywordStyleDictionary 对齐
    // ─────────────────────────────────────────────────
    private string BuildEffectSummary(List<CardEffect> effects)
    {
        if (effects == null || effects.Count == 0)
            return string.IsNullOrEmpty(cardAsset.Description) ? "无效果" : cardAsset.Description;

        var lines = new System.Text.StringBuilder();
        foreach (var e in effects)
        {
            // 优先使用手填的描述（词典样式化在外层统一处理，此处原样拼入）
            if (!string.IsNullOrEmpty(e.Description))
            {
                lines.AppendLine(e.Description);
                continue;
            }

            // 自动生成：数值用【】包裹，动词/名词与词典关键词精确对齐
            string target = GetTargetName(e.Target);
            switch (e.EffectType)
            {
                // ── 伤害 ─────────────────────────────────────────
                // 词典关键词：造成
                case EffectType.DealDamage:
                    lines.AppendLine($"对{target}造成【{e.IntValue}】点伤害"); break;
                case EffectType.DealDamageRandom:
                    lines.AppendLine($"对{target}造成【{e.IntValue}~{e.IntValue2}】点随机伤害"); break;
                case EffectType.DealDamagePerModule:
                    lines.AppendLine($"每个模块对{target}造成【{e.IntValue}】点伤害"); break;

                // ── 治疗 ─────────────────────────────────────────
                // 词典关键词：治疗
                case EffectType.Heal:
                    lines.AppendLine($"治疗{target}【{e.IntValue}】点"); break;
                case EffectType.HealPerDrone:
                    lines.AppendLine($"每个无人机治疗{target}【{e.IntValue}】点"); break;

                // ── 摧毁/过热 ────────────────────────────────────
                // 词典关键词：过热 / 摧毁所有 / 摧毁
                case EffectType.Overheat:
                    lines.AppendLine($"过热{target}"); break;
                case EffectType.DestroyModule:
                    lines.AppendLine($"摧毁{target}"); break;
                case EffectType.DestroyAllEnemyModules:
                    lines.AppendLine("摧毁所有敌方模块"); break;
                case EffectType.DestroyEnemyHand:
                    lines.AppendLine("摧毁敌方所有手牌"); break;
                case EffectType.DestroyEnemyDeck:
                    lines.AppendLine("摧毁敌方牌库"); break;
                case EffectType.DestroyEnemyDeploy:
                    lines.AppendLine("摧毁敌方部署区和手中的卡牌"); break;

                // ── 抽牌/弃置 ────────────────────────────────────
                // 词典关键词：抽取剩余所有 / 抽取所有 / 抽取 / 弃置
                case EffectType.Draw:
                    lines.AppendLine($"抽取【{e.IntValue}】张牌"); break;
                case EffectType.DrawAll:
                    lines.AppendLine("抽取剩余所有牌"); break;
                case EffectType.Discard:
                    lines.AppendLine($"弃置【{e.IntValue}】张牌"); break;

                // ── 手牌/偷取 ────────────────────────────────────
                // 词典关键词：偷取
                case EffectType.StealCard:
                    lines.AppendLine($"偷取{target}一张牌"); break;

                // ── 无人机 ───────────────────────────────────────
                // 词典关键词：生成 / 摧毁
                case EffectType.SpawnDrones:
                    lines.AppendLine($"生成【{e.DroneCount}】个{GetDroneTypeName(e.DroneType)}无人机"); break;
                case EffectType.DestroyDrones:
                    lines.AppendLine($"摧毁{target}所有无人机"); break;

                // ── 揭示 ─────────────────────────────────────────
                // 词典关键词：揭示
                case EffectType.RevealEnemyField:
                    lines.AppendLine("揭示敌方场上所有模块"); break;

                // ── 强力/规则改变 ────────────────────────────────
                // 词典关键词：额外回合 / 不再受到任何伤害（一级 palette）
                case EffectType.ExtraTurn:
                    lines.AppendLine("对手进行一个额外回合"); break;
                case EffectType.ImmunityToPlayer:
                    lines.AppendLine("对手不再受到任何伤害（模块仍可被摧毁）"); break;

                // ── 窃取生命 ─────────────────────────────────────
                // 词典关键词：窃取
                case EffectType.StealHealth:
                    lines.AppendLine($"窃取【{e.IntValue}】点生命值"); break;

                case EffectType.Custom:
                    // Description 为空则不输出任何文本（自定义效果应在 Description 里手填说明）
                    break;

                default:
                    break;
            }
        }
        return lines.ToString().TrimEnd();
    }

    // ─────────────────────────────────────────────────
    // 视觉更新（受伤、属性变更）
    // ─────────────────────────────────────────────────
    public void TakeDamage(int amount, int healthAfter)
    {
        if (amount != 0 && HealthText != null)
            HealthText.text = Mathf.Max(healthAfter, 0).ToString();
    }

    public void ChangeStats(int attackAfter, int healthAfter)
    {
        if (AttackText != null) AttackText.text = Mathf.Max(attackAfter, 0).ToString();
        if (HealthText != null) HealthText.text = Mathf.Max(healthAfter, 0).ToString();
        PreviewManager?.ChangeStats(attackAfter, healthAfter); // 同步预览
    }

    // ─────────────────────────────────────────────────
    // 卡名文本构建
    // ─────────────────────────────────────────────────
    private string BuildNameText(CardAsset asset)
    {
        string name = asset.GetDisplayName();
        if (!asset.NameAnimated) return name;

        string tag   = string.IsNullOrEmpty(asset.NameAnimTag) ? "waving" : asset.NameAnimTag;
        string inner = asset.NameUsePalette
            ? $"<{tag}>{name}</{tag}>"
            : $"<{tag}><color=#{ColorUtility.ToHtmlStringRGB(asset.NameColor)}>{name}</color></{tag}>";

        return asset.NameUsePalette ? $"<palette>{inner}</palette>" : inner;
    }

    // ─────────────────────────────────────────────────
    // 辅助方法
    // ─────────────────────────────────────────────────
    private void SetActive(GameObject go, bool active)
    {
        if (go != null) go.SetActive(active);
    }

    private string GetModuleTypeName(ModuleType type)
    {
        switch (type)
        {
            case ModuleType.Melee:     return "冷兵器";
            case ModuleType.Missile:   return "导弹";
            case ModuleType.Laser:     return "激光";
            case ModuleType.Ballistic: return "实弹";
            case ModuleType.DroneHost: return "无人机母体";
            case ModuleType.Sixth:     return "第六类";
            default:                   return "未知";
        }
    }

    private string GetDroneTypeName(DroneType type)
    {
        switch (type)
        {
            case DroneType.Attack:  return "攻击型";
            case DroneType.Heal:    return "治疗型";
            case DroneType.Builder: return "建造型";
            case DroneType.Shield:  return "护盾型";
            default:                return "未知";
        }
    }

    private string GetDroneTypeStyled(DroneType type)
    {
        switch (type)
        {
            case DroneType.Attack:
                return "<shake><color=#FF4444>攻击型无人机</color></shake>";
            case DroneType.Heal:
                return "<wave><color=#44FF88>治疗型无人机</color></wave>";
            case DroneType.Builder:
                return "<swing><color=#FFDD44>建造型无人机</color></swing>";
            case DroneType.Shield:
                return "<wave><color=#44AAFF>护盾型无人机</color></wave>";
            default:
                return "未知";
        }
    }

    private string GetTimingName(QuickPlayTiming timing)
    {
        switch (timing)
        {
            case QuickPlayTiming.AnyTime:                return "任意时刻";
            case QuickPlayTiming.BeforeDraw:             return "抽牌前";
            case QuickPlayTiming.AfterDraw:              return "抽牌后";
            case QuickPlayTiming.AfterDeploy:            return "战斗前";
            case QuickPlayTiming.BeforeFlip:             return "某格翻牌前";
            case QuickPlayTiming.AfterFlip:              return "某格翻牌后";
            case QuickPlayTiming.BeforeEffect:           return "效果触发前";
            case QuickPlayTiming.AfterEffect:            return "效果结算后";
            case QuickPlayTiming.AfterBattle:            return "战斗结算完毕后";
            case QuickPlayTiming.BeforeMissile:          return "导弹发射前";
            case QuickPlayTiming.AfterMissile:           return "导弹结算后";
            case QuickPlayTiming.TurnEnd:                return "回合结束时";
            case QuickPlayTiming.DuringBattle:           return "战斗阶段";
            case QuickPlayTiming.DuringMissile:          return "导弹阶段";
            case QuickPlayTiming.DuringBattleAndMissile: return "战斗＋导弹阶段";
            case QuickPlayTiming.DuringDrawAndBattle:    return "抽牌＋战斗阶段";
            case QuickPlayTiming.AfterDrawPhase:         return "战斗阶段起";
            default:                                     return "未知";
        }
    }

    private string GetTargetName(QuickPlayTarget target)
    {
        switch (target)
        {
            // 无目标
            case QuickPlayTarget.NoTarget:              return "";

            // 单个模块
            case QuickPlayTarget.YourModule:            return "我方一个模块";
            case QuickPlayTarget.EnemyModule:           return "敌方一个模块";
            case QuickPlayTarget.AnyModule:             return "任意一个模块";

            // 全体模块
            case QuickPlayTarget.AllYourModules:        return "我方所有模块";
            case QuickPlayTarget.AllEnemyModules:       return "敌方所有模块";
            case QuickPlayTarget.AllModules:            return "场上所有模块";

            // 随机模块
            case QuickPlayTarget.RandomYourModule:      return "随机一个我方模块";
            case QuickPlayTarget.RandomEnemyModule:     return "随机一个敌方模块";

            // 玩家本体
            case QuickPlayTarget.YourPlayer:            return "我方本体";
            case QuickPlayTarget.EnemyPlayer:           return "敌方本体";

            // 模块或本体
            case QuickPlayTarget.YourObject:            return "我方任意对象";
            case QuickPlayTarget.EnemyObject:           return "敌方任意对象";
            case QuickPlayTarget.AnyObject:             return "任意对象";

            // 部署格
            case QuickPlayTarget.YourSlot:              return "我方部署格";
            case QuickPlayTarget.EnemySlot:             return "敌方部署格";
            case QuickPlayTarget.AnySlot:               return "任意部署格";
            case QuickPlayTarget.YourEmptySlot:         return "我方空部署格";
            case QuickPlayTarget.EnemyEmptySlot:        return "敌方空部署格";

            // 无人机
            case QuickPlayTarget.YourDrones:            return "我方所有无人机";
            case QuickPlayTarget.EnemyDrones:           return "敌方所有无人机";
            case QuickPlayTarget.AnyDrone:              return "任意无人机";
            case QuickPlayTarget.RandomYourDrone:       return "随机一个我方无人机";
            case QuickPlayTarget.RandomEnemyDrone:      return "随机一个敌方无人机";

            // 手牌
            case QuickPlayTarget.YourHand:              return "我方所有手牌";
            case QuickPlayTarget.EnemyHand:             return "敌方所有手牌";
            case QuickPlayTarget.YourHandCard:          return "我方手牌中一张";
            case QuickPlayTarget.EnemyHandCard:         return "敌方手牌中一张";
            case QuickPlayTarget.RandomEnemyHand:       return "敌方随机一张手牌";

            // 牌库
            case QuickPlayTarget.YourDeck:              return "我方牌库";
            case QuickPlayTarget.EnemyDeck:             return "敌方牌库";
            case QuickPlayTarget.TopOfYourDeck:         return "我方牌库顶";
            case QuickPlayTarget.TopOfEnemyDeck:        return "敌方牌库顶";
            case QuickPlayTarget.YourDiscard:           return "我方弃牌堆";
            case QuickPlayTarget.EnemyDiscard:          return "敌方弃牌堆";

            // 总血量
            case QuickPlayTarget.YourTotalHealth:       return "我方总血量";
            case QuickPlayTarget.EnemyTotalHealth:      return "敌方总血量";

            default: return target.ToString();
        }
    }
}