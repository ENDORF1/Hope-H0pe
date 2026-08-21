using UnityEngine;

/// <summary>
/// 跨场景共享的全局游戏数据。
/// 静态类，不需要挂在任何 GameObject 上。
/// 静态变量在场景切换时天然保留，无需 DontDestroyOnLoad。
/// </summary>
public static class GameData
{
    /// <summary>
    /// 玩家在标题界面选择的阵营。
    /// TitleScreenManager.StartGame() 写入，CharacterSelect 场景读取。
    /// </summary>
    public static TitleScreenManager.Faction SelectedFaction { get; set; }
        = TitleScreenManager.Faction.Hope;

    /// <summary>
    /// 玩家在选角界面选择的角色。
    /// CharacterSelectManager 写入，战斗场景的 DeckManager / GameManager 读取。
    /// </summary>
    public static CharacterAsset SelectedCharacter { get; set; }

    /// <summary>
    /// 补齐缺失的玩家角色数据。
    /// 直接从战斗场景开始播放时不会经过选角界面，SelectedCharacter 为空，
    /// 此时按 preferred → Resources → 工程内任意 CharacterAsset 的顺序挑一个顶上。
    /// 返回最终的角色；工程里一个角色资产都没有时返回 null。
    /// </summary>
    public static CharacterAsset EnsureSelectedCharacter(CharacterAsset preferred = null)
    {
        if (SelectedCharacter != null) return SelectedCharacter;

        CharacterAsset fallback = preferred != null ? preferred : FindAnyCharacterAsset(SelectedFaction);
        if (fallback == null)
        {
            Debug.LogWarning("[GameData] 未选择角色，且工程中找不到任何 CharacterAsset。");
            return null;
        }

        SelectedCharacter = fallback;
        SelectedFaction   = fallback.Faction;
        Debug.LogWarning($"[GameData] 未经过选角界面，自动使用角色：{fallback.CharacterName}");
        return fallback;
    }

    /// <summary>
    /// 在工程中查找一个角色资产，优先返回指定阵营的。
    /// 运行时只能找到 Resources 下的资产；编辑器下会搜索整个工程。
    /// </summary>
    public static CharacterAsset FindAnyCharacterAsset(
        TitleScreenManager.Faction faction, CharacterAsset exclude = null)
    {
        CharacterAsset anyMatch = null;

        foreach (var candidate in LoadAllCharacterAssets())
        {
            if (candidate == null || candidate == exclude) continue;
            if (candidate.Faction == faction) return candidate;
            if (anyMatch == null) anyMatch = candidate;
        }

        return anyMatch;
    }

    private static CharacterAsset[] LoadAllCharacterAssets()
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:CharacterAsset");
        var editorResult = new CharacterAsset[guids.Length];
        for (int i = 0; i < guids.Length; i++)
        {
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            editorResult[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<CharacterAsset>(path);
        }
        return editorResult;
#else
        return Resources.LoadAll<CharacterAsset>(string.Empty);
#endif
    }
}