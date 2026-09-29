#if UNITY_EDITOR

using UnityEditor;
using UnityEngine;

/// <summary>
/// Быстрый старт для системы карт: создаёт шесть GameMap-ассетов
/// (лес, пляж, горы, пустыня, космос, город) с заготовленными цветами
/// неба/тумана, плоскими материалами подложек, нейтральной серой
/// палитрой блоков арены и стабильными layoutSeed.
///
/// Это заглушки для проверки механики слайда: настоящие текстуры,
/// декор и материалы подставляются потом в инспекторе. Повторный
/// запуск ничего не перезаписывает.
/// </summary>
public static class MapFactoryMenu
{
    private const string Folder = "Assets/Data/Maps";
    private const string MaterialsFolder = "Assets/Data/Maps/Materials";

    /// <summary>
    /// Нейтральные серые блоки боевой зоны — общий слой для всех карт.
    /// Совпадает с фолбэком WorldStructureGenerator.NeutralBlockColors.
    /// </summary>
    private static readonly Color[] NeutralBlockPalette =
    {
        new Color(0.58f, 0.62f, 0.66f),
        new Color(0.67f, 0.71f, 0.74f),
        new Color(0.77f, 0.79f, 0.81f)
    };

    [MenuItem("ArcadeSurvivor/Create Demo Maps")]
    public static void CreateDemoMaps()
    {
        EnsureFolder(Folder);
        EnsureFolder(MaterialsFolder);

        CreateMap(
            "Map_Les", "Лес",
            new Color(0.16f, 0.27f, 0.22f),
            new Color(0.22f, 0.27f, 0.22f),
            true,
            new Color(0.16f, 0.22f, 0.18f),
            25f, 60f,
            new Color(0.24f, 0.31f, 0.20f),
            3101
        );

        CreateMap(
            "Map_Plyazh", "Пляж",
            new Color(0.48f, 0.68f, 0.82f),
            new Color(0.50f, 0.52f, 0.50f),
            true,
            new Color(0.55f, 0.66f, 0.72f),
            45f, 90f,
            new Color(0.83f, 0.76f, 0.58f),
            2202
        );

        CreateMap(
            "Map_Gory", "Горы",
            new Color(0.62f, 0.68f, 0.74f),
            new Color(0.45f, 0.46f, 0.50f),
            true,
            new Color(0.60f, 0.64f, 0.68f),
            30f, 80f,
            new Color(0.45f, 0.47f, 0.50f),
            4404
        );

        CreateMap(
            "Map_Pustynya", "Пустыня",
            new Color(0.93f, 0.79f, 0.58f),
            new Color(0.60f, 0.53f, 0.42f),
            true,
            new Color(0.85f, 0.75f, 0.60f),
            40f, 95f,
            new Color(0.87f, 0.71f, 0.44f),
            5505
        );

        CreateMap(
            "Map_Kosmos", "Космос",
            new Color(0.01f, 0.01f, 0.04f),
            new Color(0.10f, 0.10f, 0.14f),
            false,
            new Color(0.01f, 0.01f, 0.04f),
            0f, 0f,
            new Color(0.12f, 0.12f, 0.16f),
            7707
        );

        CreateMap(
            "Map_Gorod", "Город",
            new Color(0.36f, 0.42f, 0.50f),
            new Color(0.34f, 0.36f, 0.40f),
            true,
            new Color(0.30f, 0.33f, 0.38f),
            30f, 75f,
            new Color(0.30f, 0.31f, 0.33f),
            8808
        );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[MapFactory] Шесть заготовок карт созданы в " +
            Folder + ". Назначь их в EnvironmentController."
        );
    }

    private static void CreateMap(
        string assetName,
        string displayName,
        Color sky,
        Color ambient,
        bool fog,
        Color fogColor,
        float fogStart,
        float fogEnd,
        Color groundColor,
        int layoutSeed)
    {
        string path = Folder + "/" + assetName + ".asset";

        GameMap map =
            AssetDatabase.LoadAssetAtPath<GameMap>(path);

        if (map == null)
        {
            map = ScriptableObject.CreateInstance<GameMap>();
            AssetDatabase.CreateAsset(map, path);
        }

        Material ground =
            EnsureGroundMaterial(
                assetName,
                groundColor
            );

        map.name = assetName;
        map.mapName = displayName;
        map.groundMaterial = ground;
        map.skyColor = sky;
        map.ambientLightColor = ambient;
        map.fogEnabled = fog;
        map.fogColor = fogColor;
        map.fogStartDistance = fogStart;
        map.fogEndDistance = fogEnd;

        // Блоки боевой зоны нейтральные — единый слой всех карт,
        // независимо от темы окружения.
        map.structurePalette = NeutralBlockPalette;

        // Геометрия арены стабильна для конкретной карты.
        map.layoutSeed = layoutSeed;

        EditorUtility.SetDirty(map);
    }

    /// <summary>
    /// Плоский цветной материал URP/Lit для подложки. Создаётся один
    /// раз на карту, дальше его можно заменить на текстурированный.
    /// </summary>
    private static Material EnsureGroundMaterial(
        string mapAssetName,
        Color color)
    {
        string path =
            MaterialsFolder + "/" +
            mapAssetName + "_Ground.mat";

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
            return material;

        material = new Material(
            Shader.Find("Universal Render Pipeline/Lit")
        );

        material.name = mapAssetName + "_Ground";

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

        // У подложки не должно быть своей нормали для юни-плоскости.
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0.4f);

        AssetDatabase.CreateAsset(material, path);

        return material;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        int separatorIndex = path.LastIndexOf('/');

        if (separatorIndex <= 0)
            return;

        string parent = path.Substring(0, separatorIndex);
        string leaf = path.Substring(separatorIndex + 1);

        if (!AssetDatabase.IsValidFolder(parent))
            EnsureFolder(parent);

        AssetDatabase.CreateFolder(parent, leaf);
    }
}

#endif