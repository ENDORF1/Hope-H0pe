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
}