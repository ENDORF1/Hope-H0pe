using System.Collections.Generic;
using UnityEngine;

// ── 动画等级 ──────────────────────────────────────────────────────────
public enum KeywordTier
{
    /// <summary>只改颜色，无动画</summary>
    ColorOnly,
    /// <summary>颜色 + TMP 动画标签（wave / shake / jitter 等）</summary>
    Animated,
}

// ── 单条关键词 ────────────────────────────────────────────────────────
[System.Serializable]
public class KeywordEntry
{
    [Tooltip("要匹配的关键词原文，区分大小写")]
    public string  Keyword;

    [Tooltip("显示颜色")]
    public Color   Color = Color.white;

    [Tooltip("动画等级")]
    public KeywordTier Tier = KeywordTier.ColorOnly;

    [Tooltip("TMP 动画标签名（仅 Animated 等级有效）。留空默认 waving。\n" +
             "可用标签：waving / spreading / palette / pivoting / shearing / dangling /\n" +
             "shaking / swinging / jumping / growing / fading / funky / changing / sketchy / pivotingc")]
    public string  AnimTag = "waving";

    [Tooltip("wave/jitter 振幅（可选，留 0 使用 TMP 默认值）")]
    public float   Amplitude = 0f;

    [Tooltip("wave/jitter 频率（可选，留 0 使用 TMP 默认值）")]
    public float   Frequency = 0f;

    [Tooltip("勾选后在动画标签外再套一层 palette 渐变色。\n例：Animated + palette = <palette><waving>关键词</waving></palette>\nColorOnly + palette = <palette>关键词</palette>")]
    public bool    UsePalette = false;
}

// ── ScriptableObject 词典 ────────────────────────────────────────────
/// <summary>
/// 关键词样式词典。
/// 在 Project 窗口右键 → Create → Cards → Keyword Style Dictionary 创建资产。
/// 所有卡牌共用同一份资产，在 Inspector 里随时扩充条目即可。
/// </summary>
[CreateAssetMenu(menuName = "Cards/Keyword Style Dictionary", fileName = "KeywordStyleDictionary")]
public class KeywordStyleDictionary : ScriptableObject
{
    [Tooltip("关键词列表，靠前的条目优先匹配。建议把长词放在同根短词前面，防止短词先替换后长词无法匹配。")]
    public List<KeywordEntry> Entries = new List<KeywordEntry>();
}