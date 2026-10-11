#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Создаёт ассеты достижений по умолчанию в Resources/Achievements.
/// AchievementManager подхватывает их автоматически (Resources.LoadAll),
/// поэтому после этой команды достижения становятся обычными
/// ScriptableObject-ассетами, которые можно править в инспекторе.
/// Повторный запуск не перезаписывает уже существующие ассеты.
/// </summary>
public static class AchievementAssetFactory
{
    private const string MenuPath =
        "Tools/Bullet Rush/Create Achievement Assets";

    private const string ResourcesFolder =
        "Assets/_Game/Resources";

    private const string AchievementsFolder =
        "Assets/_Game/Resources/Achievements";

    [MenuItem(MenuPath)]
    public static void Create()
    {
        EnsureFolder(ResourcesFolder);
        EnsureFolder(AchievementsFolder);

        List<AchievementData> defaults = AchievementDefaults.Create();

        int created = 0;
        int skipped = 0;

        for (int i = 0; i < defaults.Count; i++)
        {
            AchievementData data = defaults[i];
            string path = AchievementsFolder + "/" + data.name + ".asset";

            if (AssetDatabase.LoadAssetAtPath<AchievementData>(path) != null)
            {
                skipped++;
                continue;
            }

            AssetDatabase.CreateAsset(data, path);
            created++;
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            $"[AchievementAssetFactory] Создано: {created}, " +
            $"уже было: {skipped}. Папка: {AchievementsFolder}"
        );

        EditorUtility.DisplayDialog(
            "Create Achievement Assets",
            $"Создано ассетов: {created}\nУже существовало: {skipped}\n\n" +
            "Папка: " + AchievementsFolder,
            "OK"
        );
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = System.IO.Path.GetDirectoryName(path)
            .Replace('\\', '/');

        string leaf = System.IO.Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}

#endif
