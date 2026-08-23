using UnityEditor;
using UnityEngine;

/// <summary>
/// 一次性工具：把 Assets/Sprites/Characters/EUDORA.png 设为 EUDORA CharacterAsset 的 Portrait。
/// 命令行：Unity.exe -batchmode -quit -projectPath "..." -executeMethod AssignEudoraPortrait.Run
/// </summary>
public static class AssignEudoraPortrait
{
    const string PortraitPath = "Assets/Sprites/Characters/EUDORA.png";
    const string CharacterPath = "Assets/CARDS/HopeCharacters/EUDORA.asset";

    public static void Run()
    {
        EnsureSpriteImport(PortraitPath);

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PortraitPath);
        if (sprite == null)
        {
            Debug.LogError("[AssignEudoraPortrait] 无法加载 Sprite：" + PortraitPath);
            EditorApplication.Exit(1);
            return;
        }

        var character = AssetDatabase.LoadAssetAtPath<CharacterAsset>(CharacterPath);
        if (character == null)
        {
            Debug.LogError("[AssignEudoraPortrait] 找不到 CharacterAsset：" + CharacterPath);
            EditorApplication.Exit(1);
            return;
        }

        var old = character.Portrait;
        character.Portrait = sprite;
        EditorUtility.SetDirty(character);
        AssetDatabase.SaveAssets();

        Debug.Log($"[AssignEudoraPortrait] 已更新 EUDORA.Portrait\n  旧: {(old != null ? old.name : "null")}\n  新: {sprite.name} ({PortraitPath})");
        EditorApplication.Exit(0);
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
