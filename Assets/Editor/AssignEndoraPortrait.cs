using UnityEditor;
using UnityEngine;

/// <summary>
/// 一次性工具：把 Assets/Sprites/Characters/ENDORA.png 设为 ENDORA CharacterAsset 的 Portrait，并补齐中文文案。
/// 命令行：Unity.exe -batchmode -quit -projectPath "..." -executeMethod AssignEndoraPortrait.Run
/// </summary>
public static class AssignEndoraPortrait
{
    const string PortraitPath = "Assets/Sprites/Characters/ENDORA.png";
    const string CharacterPath = "Assets/CARDS/VoidCharacters/ENDORA.asset";

    [MenuItem("Tools/Characters/Assign ENDORA Portrait")]
    public static void RunFromMenu() => Run(false);

    public static void Run() => Run(true);

    static void Run(bool exitWhenDone)
    {
        EnsureSpriteImport(PortraitPath);

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
        if (sprite == null)
        {
            Debug.LogError("[AssignEndoraPortrait] 无法加载 Sprite：" + PortraitPath);
            EditorApplication.Exit(1);
            return;
        }

        var character = AssetDatabase.LoadAssetAtPath<CharacterAsset>(CharacterPath);
        if (character == null)
        {
            Debug.LogError("[AssignEndoraPortrait] 找不到 CharacterAsset：" + CharacterPath);
            EditorApplication.Exit(1);
            return;
        }

        var old = character.Portrait;
        character.Portrait = sprite;
        character.CharacterName = "柩世";
        character.CharacterNameEn = "ENDORA";
        character.Faction = TitleScreenManager.Faction.Void;
        character.PromptLine = "修正噪声。";
        character.BackTextMain = "CARVE\nSILENCE";
        character.Description = "Carve Silence And The World Stills.";

        EditorUtility.SetDirty(character);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AssignEndoraPortrait] 已更新 ENDORA\n  Portrait 旧: {(old != null ? old.name : "null")}\n  Portrait 新: {sprite.name}");
        if (exitWhenDone) EditorApplication.Exit(0);
    }

    static void EnsureSpriteImport(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            importer = AssetImporter.GetAtPath(path) as TextureImporter;
        }

        if (importer == null) return;

        bool changed = false;
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }
        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }
        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();
        else
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
    }
}
