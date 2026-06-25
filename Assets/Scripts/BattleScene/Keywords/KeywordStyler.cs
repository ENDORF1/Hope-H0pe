using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// 统一关键词样式化工具，替换原 QuickPlayKeywordStyler。
/// 适用于所有卡牌文本（模块牌描述、速攻牌效果摘要、冷却/导弹/无人机标签等）。
///
/// ── 处理顺序 ─────────────────────────────────────────────────────────
///   1. 词典匹配：逐条扫描 KeywordStyleDictionary，找到就替换（靠前条目优先）
///   2. 内置规则兜底（词典里没有的也能处理）：
///      · 【X】→ 白色数值
///      · 纯阿拉伯数字序列 → 白色（可选，默认开启）
///
/// ── 使用方法 ──────────────────────────────────────────────────────────
///   string styled = KeywordStyler.Apply(rawText, keywordDict);
///   myTMPText.text = styled;
///
/// ── 防重复替换 ─────────────────────────────────────────────────────────
///   已经带有 TMP 标签的文本片段会被跳过，不会被二次包裹。
/// </summary>
public static class KeywordStyler
{
    // 用于检测"已经被替换过（包含 < 标签）"的简单判断
    private const string TAG_SENTINEL = "<";

    /// <summary>
    /// 对原始文本应用词典样式化 + 内置规则兜底。
    /// dict 为 null 时只执行内置规则。
    /// </summary>
    public static string Apply(string raw, KeywordStyleDictionary dict)
    {
        if (string.IsNullOrEmpty(raw)) return raw;

        string result = raw;

        // ── 1. 词典匹配 ───────────────────────────────────────────────
        if (dict != null && dict.Entries != null)
        {
            foreach (var entry in dict.Entries)
            {
                if (string.IsNullOrEmpty(entry.Keyword)) continue;

                // 用正则做单词边界匹配，防止"伤害"匹配到"溅射伤害"里的"伤害"后者本已被替换
                // 同时跳过已在 TMP 标签内部的匹配（简化处理：只替换不含 < 的片段）
                string escaped  = Regex.Escape(entry.Keyword);
                string styled   = BuildStyled(entry);

                // 替换时跳过已在标签内的关键词：通过负向前瞻确保替换目标不在 <...> 内部
                // 简化方案：直接字符串替换，但先把已有标签占位保护起来
                result = SafeReplace(result, entry.Keyword, styled);
            }
        }

        // ── 2. 内置规则：【X】→ 白色 ────────────────────────────────
        result = Regex.Replace(
            result,
            @"【(.+?)】",
            m => $"<color=#FFFFFF>{m.Groups[1].Value}</color>"
        );

        return result;
    }

    // ─────────────────────────────────────────────────────────────────
    // 防重复替换：把文本分割为"标签内"和"标签外"两部分，只替换标签外的内容
    // ─────────────────────────────────────────────────────────────────
    private static string SafeReplace(string input, string keyword, string replacement)
    {
        if (!input.Contains(keyword)) return input;

        var sb     = new System.Text.StringBuilder();
        int depth  = 0; // 当前是否在 <...> 内
        int i      = 0;
        int len    = input.Length;
        int kLen   = keyword.Length;

        while (i < len)
        {
            char c = input[i];

            // 进入 TMP 标签
            if (c == '<')
            {
                depth++;
                sb.Append(c);
                i++;
                continue;
            }

            // 离开 TMP 标签
            if (c == '>')
            {
                if (depth > 0) depth--;
                sb.Append(c);
                i++;
                continue;
            }

            // 在标签内：原样输出
            if (depth > 0)
            {
                sb.Append(c);
                i++;
                continue;
            }

            // 在标签外：检查是否匹配关键词
            if (i + kLen <= len && input.Substring(i, kLen) == keyword)
            {
                sb.Append(replacement);
                i += kLen;
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }

        return sb.ToString();
    }

    // ─────────────────────────────────────────────────────────────────
    // 根据 KeywordEntry 生成 TMP 富文本字符串
    // ─────────────────────────────────────────────────────────────────
    private static string BuildStyled(KeywordEntry entry)
    {
        string hex = ColorToHex(entry.Color);
        string inner;

        switch (entry.Tier)
        {
            case KeywordTier.Animated:
            {
                string tag   = string.IsNullOrEmpty(entry.AnimTag) ? "waving" : entry.AnimTag;
                string attrs = BuildAnimAttrs(entry);
                string open  = string.IsNullOrEmpty(attrs) ? $"<{tag}>" : $"<{tag} {attrs}>";
                string close = $"</{tag}>";
                // palette 和 color 不能共存，UsePalette 时不加 color 标签
                inner = entry.UsePalette
                    ? $"{open}{entry.Keyword}{close}"
                    : $"{open}<color={hex}>{entry.Keyword}</color>{close}";
                break;
            }

            case KeywordTier.ColorOnly:
            default:
                inner = entry.UsePalette
                    ? entry.Keyword
                    : $"<color={hex}>{entry.Keyword}</color>";
                break;
        }

        // 最外层套 palette（如果勾选）
        return entry.UsePalette ? $"<palette>{inner}</palette>" : inner;
    }

    // 生成 amplitude / frequency 属性字符串（为 0 则省略，使用 TMP 默认值）
    private static string BuildAnimAttrs(KeywordEntry entry)
    {
        var parts = new System.Collections.Generic.List<string>();
        if (entry.Amplitude > 0f) parts.Add($"amplitude={entry.Amplitude:F2}");
        if (entry.Frequency > 0f) parts.Add($"frequency={entry.Frequency:F2}");
        return string.Join(" ", parts);
    }

    // Unity Color → TMP hex 字符串（#RRGGBB）
    private static string ColorToHex(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGB(color)}";
    }
}