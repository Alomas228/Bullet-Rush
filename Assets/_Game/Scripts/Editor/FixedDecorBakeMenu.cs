using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Меню для ручной расстановки фиксированного дизайна карты.
/// В сцене автор выставляет объекты (горы, здания, акценты)
/// относительно центра арены, выделяет их, выбирает карту
/// в Project-окне и жмёт Bake Fixed Decor to Map: координаты
/// каждого объекта сохраняются в GameMap.fixedDecor и в точности
/// воспроизводятся на слое карты при запуске.
/// Дубли (тот же префаб в той же позиции) пропускаются, чтобы
/// повторная запись не плодила по нескольку одинаковых пунктов.
/// После записи предлагается удалить исходники из сцены — иначе
/// они останутся статичными и будут видны на всех картах.
/// </summary>
public static class FixedDecorBakeMenu
{
    private const string BakeMenu =
        "ArcadeSurvivor/Bake Fixed Decor to Map %#d";

    private const string DecorFolder =
        "Assets/Data/Maps/FixedDecor";

    [MenuItem(BakeMenu)]
    private static void Bake()
    {
        GameMap map = Selection.activeObject as GameMap;

        if (map == null && Selection.objects != null)
        {
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                GameMap candidate =
                    Selection.objects[i] as GameMap;

                if (candidate != null)
                {
                    map = candidate;
                    break;
                }
            }
        }

        if (map == null)
        {
            EditorUtility.DisplayDialog(
                "Bake Fixed Decor",
                "Выбери карту (GameMap asset) в Project-окне, " +
                "для которой печём декорации.",
                "OK"
            );
            return;
        }

        GameObject[] setup =
            Selection.gameObjects;

        if (setup == null || setup.Length == 0)
        {
            EditorUtility.DisplayDialog(
                "Bake Fixed Decor",
                "Нет выделенных объектов сцены. " +
                "Выдели поставленные вручную горы/декор.",
                "OK"
            );
            return;
        }

        EnsureDecorFolder();

        List<FixedDecorItem> baked = new List<FixedDecorItem>();

        for (int i = 0; i < setup.Length; i++)
        {
            GameObject source = setup[i];

            GameObject prefab =
                PrefabUtility.GetCorrespondingObjectFromSource(source);

            if (prefab == null)
            {
                string path =
                    DecorFolder + "/" + Sanitize(source.name) + ".prefab";

                prefab = PrefabUtility.SaveAsPrefabAsset(source, path);

                if (prefab == null)
                {
                    Debug.LogWarning(
                        "Не удалось сделать префаб из " + source.name,
                        source
                    );
                    continue;
                }
            }

            FixedDecorItem item = new FixedDecorItem
            {
                prefab = prefab,
                position = source.transform.position,
                rotation = source.transform.rotation.eulerAngles,
                scale = source.transform.lossyScale,
                keepColliders = false
            };

            baked.Add(item);
        }

        if (baked.Count == 0)
            return;

        Undo.RecordObject(map, "Bake Fixed Decor to Map");

        int oldLength =
            map.fixedDecor != null ? map.fixedDecor.Length : 0;

        FixedDecorItem[] merged =
            new FixedDecorItem[oldLength + baked.Count];

        for (int i = 0; i < oldLength; i++)
            merged[i] = map.fixedDecor[i];

        int added = 0;

        for (int i = 0; i < baked.Count; i++)
        {
            FixedDecorItem item = baked[i];

            if (ContainsItem(map.fixedDecor, oldLength, item))
            {
                Debug.Log(
                    "Пропущен дубль: " + item.prefab.name +
                    " уже есть в " + map.name +
                    " на этой же позиции.",
                    map
                );
                continue;
            }

            merged[oldLength + added] = item;
            added++;
        }

        if (added == 0)
        {
            EditorUtility.DisplayDialog(
                "Bake Fixed Decor",
                "Все выбранные объекты уже записаны в " +
                map.name + " на тех же позициях. Ничего не добавлено.",
                "OK"
            );
            return;
        }

        System.Array.Resize(
            ref merged,
            oldLength + added
        );

        map.fixedDecor = merged;

        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssets();

        Debug.Log(
            "Записано в " + map.name + ": " +
            added + " объектов фиксированного декора.",
            map
        );

        bool deleteSources =
            EditorUtility.DisplayDialog(
                "Bake Fixed Decor",
                "Записано " + added + " объектов в " + map.name + ".\n\n" +
                "Удалить исходные объекты из сцены? Если оставить, " +
                "они будут статичными и увидятся на ВСЕХ картах.",
                "Удалить",
                "Оставить"
            );

        if (!deleteSources)
            return;

        for (int i = 0; i < setup.Length; i++)
        {
            if (setup[i] != null &&
                !AssetDatabase.Contains(setup[i]))
            {
                Object.DestroyImmediate(setup[i]);
            }
        }
    }

    /// <summary>
    /// Есть ли уже в массиве (первые count элементов) такой же пункт:
    /// тот же префаб и практически та же позиция.
    /// </summary>
    private static bool ContainsItem(
        FixedDecorItem[] items,
        int count,
        FixedDecorItem item)
    {
        if (items == null || item == null || item.prefab == null)
            return false;

        for (int i = 0; i < count; i++)
        {
            FixedDecorItem other = items[i];

            if (other == null ||
                other.prefab == null)
            {
                continue;
            }

            if (other.prefab == item.prefab &&
                Vector3.Distance(other.position, item.position) < 0.01f)
            {
                return true;
            }
        }

        return false;
    }

    private static string Sanitize(string name)
    {
        string result = "";

        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];

            bool valid =
                char.IsLetterOrDigit(c) ||
                c == '_' ||
                c == '-';

            result += valid ? c : '_';
        }

        if (result.Length == 0)
            result = "Decor";

        return result;
    }

    private static void EnsureDecorFolder()
    {
        if (AssetDatabase.IsValidFolder(DecorFolder))
            return;

        const string root = "Assets/Data";

        if (!AssetDatabase.IsValidFolder(root))
            AssetDatabase.CreateFolder("Assets", "Data");

        const string maps = root + "/Maps";

        if (!AssetDatabase.IsValidFolder(maps))
            AssetDatabase.CreateFolder(root, "Maps");

        if (!AssetDatabase.IsValidFolder(DecorFolder))
            AssetDatabase.CreateFolder(maps, "FixedDecor");
    }
}