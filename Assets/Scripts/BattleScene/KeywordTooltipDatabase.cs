using System.Collections.Generic;
using UnityEngine;

// ── 单条关键词解释 ────────────────────────────────────────────────────
[System.Serializable]
public class KeywordTooltipEntry
{
    [Tooltip("关键词原文，需与 KeywordStyleDictionary 中的 Keyword 字段完全一致")]
    public string Keyword;

    [TextArea(2, 5)]
    [Tooltip("该关键词的效果解释，显示在弹窗中")]
    public string Explanation;
}

// ── ScriptableObject 词典 ────────────────────────────────────────────
/// <summary>
/// 关键词解释词典。
/// 与 KeywordStyleDictionary 配合使用——后者负责样式，本资产负责解释文本。
/// 两者通过 Keyword 字段关联，保持一致即可。
///
/// 在 Project 窗口右键 → Create → Cards → Keyword Tooltip Database 创建资产。
/// </summary>
[CreateAssetMenu(menuName = "Cards/Keyword Tooltip Database", fileName = "KeywordTooltipDatabase")]
public class KeywordTooltipDatabase : ScriptableObject
{
    [Tooltip("关键词解释列表，顺序不影响匹配")]
    public List<KeywordTooltipEntry> Entries = new List<KeywordTooltipEntry>();

    // ─────────────────────────────────────────────────
    // 运行时查询
    // ─────────────────────────────────────────────────

    /// <summary>
    /// 扫描输入文本，返回所有匹配到的关键词解释条目（去重，保持词典顺序）。
    /// </summary>
    public List<KeywordTooltipEntry> FindMatches(string text)
    {
        var result = new List<KeywordTooltipEntry>();
        if (string.IsNullOrEmpty(text)) return result;

        foreach (var entry in Entries)
        {
            if (string.IsNullOrEmpty(entry.Keyword)) continue;
            if (text.Contains(entry.Keyword))
                result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// 根据关键词精确查找单条解释，找不到返回 null。
    /// </summary>
    public KeywordTooltipEntry FindByKeyword(string keyword)
    {
        foreach (var entry in Entries)
            if (entry.Keyword == keyword) return entry;
        return null;
    }
}
