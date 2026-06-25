using UnityEngine;

/// <summary>
/// 遗物静态数据资产。
/// 每个遗物创建一个 ScriptableObject 实例。
/// 效果逻辑通过 RelicScriptName 动态挂载（与 CardAsset.ModuleScriptName 做法一致）。
/// </summary>
[CreateAssetMenu(fileName = "NewRelic", menuName = "希望熄忘/Relic Asset")]
public class RelicAsset : ScriptableObject
{
    [Header("基础信息")]
    public string RelicName;

    [TextArea(2, 4)]
    public string Description;

    [Header("图标")]
    public Sprite Icon;

    // ─────────────────────────────────────────────────
    // 姐妹专属双版本支持
    // ─────────────────────────────────────────────────

    [Header("姐妹专属（留空则不启用）")]
    [Tooltip("启用后根据持有者是否为姐姐，显示不同图标和描述")]
    public bool IsSisterRelic = false;

    [Tooltip("对姐姐显示的图标（中文版用 QAQ 图，英文版用 <3 图）")]
    public Sprite IconForSister;

    [TextArea(2, 4)]
    [Tooltip("对姐姐显示的描述文本")]
    public string DescriptionForSister;

    // ─────────────────────────────────────────────────
    // 效果脚本
    // ─────────────────────────────────────────────────

    [Header("效果脚本")]
    [Tooltip("继承自 RelicBase 的效果脚本类名。留空则无效果（纯展示用）。")]
    public string RelicScriptName;

    // ─────────────────────────────────────────────────
    // 公开接口
    // ─────────────────────────────────────────────────

    /// <summary>显示名称（供 UI 调用）</summary>
    public string GetDisplayName() =>
        string.IsNullOrEmpty(RelicName) ? name : RelicName;

    /// <summary>
    /// 获取当前应显示的描述文本。
    /// isForSister = true 时返回姐姐专属版本（如果存在）。
    /// </summary>
    public string GetDescription(bool isForSister = false)
    {
        if (isForSister && IsSisterRelic && !string.IsNullOrEmpty(DescriptionForSister))
            return DescriptionForSister;
        return Description;
    }

    /// <summary>
    /// 获取当前应显示的图标。
    /// isForSister = true 时返回姐姐专属图标（如果存在）。
    /// </summary>
    public Sprite GetIcon(bool isForSister = false)
    {
        if (isForSister && IsSisterRelic && IconForSister != null)
            return IconForSister;
        return Icon;
    }
}
