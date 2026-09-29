#if UNITY_EDITOR

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Генератор шести тем окружения (Лес, Пляж, Горы, Пустыня, Космос,
/// Город): создаёт EnvironmentTheme-ассеты с палитрами и рецептами
/// объектов из примитивов, назначает их на демо-карты. Повторный
/// запуск перезаписывает данные тем (удобно при правке рецептов),
/// не трогая ручные материалы и префабы карт.
/// </summary>
public static class EnvironmentThemeFactory
{
    private const string Folder = "Assets/Data/Maps/Environments";
    private const string MaterialsFolder = "Assets/Data/Maps/Materials";
    private const string MapsFolder = "Assets/Data/Maps";

    [MenuItem("ArcadeSurvivor/Create Environment Themes")]
    public static void CreateEnvironmentThemes()
    {
        EnsureFolder(Folder);
        EnsureFolder(MaterialsFolder);

        // Карты нужны для привязки тем — досоздаём, если их ещё нет.
        MapFactoryMenu.CreateDemoMaps();

        CreateTheme(
            "Env_Les", "Лес",
            LesPalette, LesGlow,
            LesObstacles, LesDecor, LesOutside,
            "Map_Les", 3101
        );

        CreateTheme(
            "Env_Plyazh", "Пляж",
            PlyazhPalette, PlyazhGlow,
            PlyazhObstacles, PlyazhDecor, PlyazhOutside,
            "Map_Plyazh", 2202
        );

        CreateTheme(
            "Env_Gory", "Горы",
            GoryPalette, GoryGlow,
            GoryObstacles, GoryDecor, GoryOutside,
            "Map_Gory", 4404
        );

        CreateTheme(
            "Env_Pustynya", "Пустыня",
            PustynyaPalette, PustynyaGlow,
            PustynyaObstacles, PustynyaDecor, PustynyaOutside,
            "Map_Pustynya", 5505
        );

        CreateTheme(
            "Env_Kosmos", "Космос",
            KosmosPalette, KosmosGlow,
            KosmosObstacles, KosmosDecor, KosmosOutside,
            "Map_Kosmos", 7707
        );

        CreateTheme(
            "Env_Gorod", "Город",
            GorodPalette, GorodGlow,
            GorodObstacles, GorodDecor, GorodOutside,
            "Map_Gorod", 8808
        );

        // В космосе почти нет света: поднимаем ambient чуть выше,
        // иначе объекты (камни, металл) сливаются с чёрным фоном.
        GameMap kosmos =
            AssetDatabase.LoadAssetAtPath<GameMap>(
                MapsFolder + "/Map_Kosmos.asset"
            );

        if (kosmos != null)
        {
            kosmos.ambientLightColor =
                new Color(0.22f, 0.24f, 0.34f);

            EditorUtility.SetDirty(kosmos);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log(
            "[EnvironmentFactory] Шесть тем окружения созданы в " +
            Folder + " и назначены на карты."
        );
    }

    // =============================================================
    // ЛЕС
    // =============================================================

    private static readonly Color[] LesPalette =
    {
        new Color(0.42f, 0.31f, 0.20f), // 0 ствол
        new Color(0.16f, 0.42f, 0.19f), // 1 крона тёмная
        new Color(0.30f, 0.55f, 0.25f), // 2 крона светлая
        new Color(0.46f, 0.46f, 0.48f), // 3 камень
        new Color(0.36f, 0.42f, 0.27f), // 4 мох
        new Color(0.93f, 0.84f, 0.45f)  // 5 цветы
    };

    private static readonly Color LesGlow =
        new Color(1.00f, 0.97f, 0.85f);

    private static EnvironmentObjectDef[] LesObstacles =>
        new[]
        {
            Def("Tree", 8, 0.8f, 1.3f, 0.7f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 0.6f, 0f, 0.3f, 1.2f, 0.3f),
                Part(EnvironmentShape.Sphere, 1, 0f, 1.75f, 0f, 1.4f, 1.1f, 1.4f),
                Part(EnvironmentShape.Sphere, 2, 0.35f, 1.35f, 0.2f, 0.7f, 0.6f, 0.7f),
                Part(EnvironmentShape.Sphere, 2, -0.35f, 2.1f, -0.15f, 0.55f, 0.55f, 0.55f)),
            Def("Stone", 5, 0.7f, 1.2f, 0.8f,
                Part(EnvironmentShape.Sphere, 3, 0f, 0.32f, 0f, 1.0f, 0.65f, 0.9f),
                Part(EnvironmentShape.Sphere, 3, 0.45f, 0.22f, 0.35f, 0.5f, 0.45f, 0.5f),
                Part(EnvironmentShape.Sphere, 4, -0.3f, 0.42f, 0.2f, 0.3f, 0.22f, 0.3f)),
            Def("Stump", 3, 0.8f, 1.1f, 0.45f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 0.28f, 0f, 0.65f, 0.56f, 0.65f),
                Part(EnvironmentShape.Cylinder, 1, 0f, 0.54f, 0f, 0.68f, 0.06f, 0.68f)),
            Def("FallenLog", 3, 0.9f, 1.3f, 1.0f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 0.28f, 0f, 0.5f, 1.6f, 0.5f, 0f, 0f, 90f),
                Part(EnvironmentShape.Cylinder, 0, 0.3f, 0.22f, 0f, 0.4f, 0.9f, 0.4f, 0f, 0f, 150f),
                Part(EnvironmentShape.Sphere, 4, 0.5f, 0.5f, 0.15f, 0.4f, 0.35f, 0.4f)),
            Corner(Def("CornerTree", 2, 1.8f, 2.4f, 1.3f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 1.1f, 0f, 0.7f, 2.2f, 0.7f),
                Part(EnvironmentShape.Sphere, 1, 0f, 3.2f, 0f, 3.0f, 2.3f, 3.0f),
                Part(EnvironmentShape.Sphere, 2, 0.9f, 2.5f, 0.6f, 1.6f, 1.3f, 1.6f),
                Part(EnvironmentShape.Sphere, 2, -0.8f, 3.7f, -0.5f, 1.2f, 1.2f, 1.2f)))
        };

    private static EnvironmentObjectDef[] LesDecor =>
        new[]
        {
            Def("Bush", 6, 0.6f, 1.0f, 0.5f,
                Part(EnvironmentShape.Sphere, 1, 0f, 0.25f, 0f, 0.7f, 0.55f, 0.7f),
                Part(EnvironmentShape.Sphere, 2, 0.3f, 0.4f, 0.1f, 0.4f, 0.4f, 0.4f)),
            Def("Flowers", 5, 0.5f, 0.7f, 0.3f,
                Part(EnvironmentShape.Sphere, 2, 0f, 0.15f, 0f, 0.5f, 0.4f, 0.5f),
                Part(EnvironmentShape.Sphere, 5, 0.12f, 0.35f, 0.05f, 0.2f, 0.18f, 0.2f),
                Part(EnvironmentShape.Sphere, 5, -0.18f, 0.33f, -0.1f, 0.15f, 0.14f, 0.15f))
        };

    private static EnvironmentObjectDef[] LesOutside =>
        new[]
        {
            Radial(Def("TallTree", 7, 1.5f, 2.3f, 0f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 1.5f, 0f, 0.5f, 3.0f, 0.5f),
                Part(EnvironmentShape.Sphere, 1, 0f, 3.3f, 0f, 2.6f, 2.0f, 2.6f),
                Part(EnvironmentShape.Sphere, 2, 0.5f, 2.5f, 0.3f, 1.2f, 1.0f, 1.2f),
                Part(EnvironmentShape.Sphere, 2, -0.4f, 4.0f, -0.2f, 1.0f, 1.0f, 1.0f))),
            Radial(Def("BigBush", 4, 1.4f, 2.0f, 0f,
                Part(EnvironmentShape.Sphere, 1, 0f, 0.4f, 0f, 1.4f, 0.9f, 1.4f),
                Part(EnvironmentShape.Sphere, 2, 0.35f, 0.55f, 0.15f, 0.8f, 0.7f, 0.8f))),
            Radial(Def("RockCluster", 3, 1.5f, 2.2f, 0f,
                Part(EnvironmentShape.Sphere, 3, 0f, 0.7f, 0f, 2.2f, 1.4f, 1.9f),
                Part(EnvironmentShape.Sphere, 3, 1.2f, 0.5f, 0.4f, 1.3f, 1.0f, 1.1f),
                Part(EnvironmentShape.Sphere, 4, -0.8f, 0.75f, 0.3f, 0.7f, 0.5f, 0.6f)))
        };

    // =============================================================
    // ПЛЯЖ
    // =============================================================

    private static readonly Color[] PlyazhPalette =
    {
        new Color(0.91f, 0.83f, 0.64f), // 0 песок
        new Color(0.60f, 0.46f, 0.27f), // 1 ствол пальмы
        new Color(0.24f, 0.52f, 0.28f), // 2 листья
        new Color(0.58f, 0.40f, 0.22f), // 3 ящик
        new Color(0.93f, 0.90f, 0.78f), // 4 ракушки/светлый камень
        new Color(0.13f, 0.38f, 0.60f), // 5 море
        new Color(0.52f, 0.45f, 0.34f)  // 6 дерево/брёвна
    };

    private static readonly Color PlyazhGlow =
        new Color(1.00f, 0.95f, 0.80f);

    private static EnvironmentObjectDef[] PlyazhObstacles =>
        new[]
        {
            Def("Palm", 6, 0.9f, 1.3f, 0.8f,
                Part(EnvironmentShape.Cylinder, 1, 0f, 0.8f, 0f, 0.34f, 1.6f, 0.34f, 0f, 0f, 5f),
                Part(EnvironmentShape.Capsule, 2, 0f, 1.8f, 0f, 0.36f, 1.5f, 0.36f, 0f, 0f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 1.8f, 0f, 0.36f, 1.5f, 0.36f, 0f, 60f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 1.8f, 0f, 0.36f, 1.5f, 0.36f, 0f, 120f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 1.8f, 0f, 0.36f, 1.5f, 0.36f, 0f, 180f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 1.8f, 0f, 0.36f, 1.5f, 0.36f, 0f, 240f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 1.8f, 0f, 0.36f, 1.5f, 0.36f, 0f, 300f, -90f),
                Part(EnvironmentShape.Sphere, 1, 0f, 1.78f, 0f, 0.25f, 0.25f, 0.25f)),
            Def("Crate", 3, 0.8f, 1.1f, 0.7f,
                Part(EnvironmentShape.Cube, 3, 0f, 0.45f, 0f, 0.9f, 0.9f, 0.9f),
                Part(EnvironmentShape.Cube, 6, 0f, 0.45f, 0.45f, 0.9f, 0.16f, 0.05f),
                Part(EnvironmentShape.Cube, 3, 0.42f, 1.0f, 0.15f, 0.55f, 0.55f, 0.55f)),
            Def("BeachRock", 4, 0.7f, 1.1f, 0.75f,
                Part(EnvironmentShape.Sphere, 4, 0f, 0.35f, 0f, 1.1f, 0.7f, 0.9f),
                Part(EnvironmentShape.Sphere, 4, 0.5f, 0.25f, 0.3f, 0.5f, 0.4f, 0.5f),
                Part(EnvironmentShape.Sphere, 6, -0.35f, 0.45f, 0.15f, 0.35f, 0.3f, 0.35f)),
            Def("Driftwood", 2, 0.95f, 1.35f, 1.0f,
                Part(EnvironmentShape.Cylinder, 6, 0f, 0.25f, 0f, 0.45f, 1.7f, 0.45f, 0f, 0f, 90f),
                Part(EnvironmentShape.Cylinder, 6, 0.6f, 0.4f, 0.1f, 0.3f, 0.7f, 0.3f, 0f, 0f, -45f),
                Part(EnvironmentShape.Sphere, 4, -0.5f, 0.35f, 0.2f, 0.3f, 0.25f, 0.3f)),
            Corner(Def("CornerPalm", 2, 1.9f, 2.5f, 1.3f,
                Part(EnvironmentShape.Cylinder, 1, 0f, 1.1f, 0f, 0.5f, 2.2f, 0.5f, 0f, 0f, 6f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.5f, 0f, 0.55f, 2.2f, 0.55f, 0f, 0f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.5f, 0f, 0.55f, 2.2f, 0.55f, 0f, 60f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.5f, 0f, 0.55f, 2.2f, 0.55f, 0f, 120f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.5f, 0f, 0.55f, 2.2f, 0.55f, 0f, 180f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.5f, 0f, 0.55f, 2.2f, 0.55f, 0f, 240f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.5f, 0f, 0.55f, 2.2f, 0.55f, 0f, 300f, -90f),
                Part(EnvironmentShape.Sphere, 1, 0f, 2.47f, 0f, 0.4f, 0.4f, 0.4f),
                Part(EnvironmentShape.Sphere, 4, 1.1f, 0.35f, 0.4f, 0.6f, 0.45f, 0.55f)))
        };

    private static EnvironmentObjectDef[] PlyazhDecor =>
        new[]
        {
            Def("Dune", 3, 1.4f, 1.9f, 0.2f,
                Part(EnvironmentShape.Sphere, 0, 0f, 0.18f, 0f, 2.6f, 0.36f, 1.8f),
                Part(EnvironmentShape.Sphere, 0, 0.4f, 0.28f, 0.2f, 1.6f, 0.26f, 1.2f)),
            Def("Shell", 5, 0.4f, 0.6f, 0.25f,
                Part(EnvironmentShape.Sphere, 4, 0f, 0.1f, 0f, 0.45f, 0.2f, 0.35f),
                Part(EnvironmentShape.Sphere, 0, 0.05f, 0.17f, 0.02f, 0.22f, 0.12f, 0.16f)),
            Def("LeafTrash", 4, 0.5f, 0.8f, 0.3f,
                Part(EnvironmentShape.Capsule, 6, 0f, 0.12f, 0f, 0.2f, 0.9f, 0.2f, 0f, 0f, 110f),
                Part(EnvironmentShape.Capsule, 6, 0.3f, 0.1f, 0.1f, 0.18f, 0.7f, 0.18f, 0f, 90f, 60f))
        };

    private static EnvironmentObjectDef[] PlyazhOutside =>
        new[]
        {
            Radial(Def("BigPalm", 6, 1.8f, 2.6f, 0f,
                Part(EnvironmentShape.Cylinder, 1, 0f, 1.3f, 0f, 0.5f, 2.6f, 0.5f, 0f, 0f, 5f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.9f, 0f, 0.5f, 2.2f, 0.5f, 0f, 0f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.9f, 0f, 0.5f, 2.2f, 0.5f, 0f, 60f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.9f, 0f, 0.5f, 2.2f, 0.5f, 0f, 120f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.9f, 0f, 0.5f, 2.2f, 0.5f, 0f, 180f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.9f, 0f, 0.5f, 2.2f, 0.5f, 0f, 240f, -90f),
                Part(EnvironmentShape.Capsule, 2, 0f, 2.9f, 0f, 0.5f, 2.2f, 0.5f, 0f, 300f, -90f),
                Part(EnvironmentShape.Sphere, 1, 0f, 2.87f, 0f, 0.32f, 0.32f, 0.32f))),
            Radial(Def("Tower", 1, 1.0f, 1.0f, 1.4f,
                Part(EnvironmentShape.Cylinder, 6, -0.35f, 0.5f, -0.35f, 0.12f, 1.0f, 0.12f),
                Part(EnvironmentShape.Cylinder, 6, 0.35f, 0.5f, -0.35f, 0.12f, 1.0f, 0.12f),
                Part(EnvironmentShape.Cylinder, 6, -0.35f, 0.5f, 0.35f, 0.12f, 1.0f, 0.12f),
                Part(EnvironmentShape.Cylinder, 6, 0.35f, 0.5f, 0.35f, 0.12f, 1.0f, 0.12f),
                Part(EnvironmentShape.Cube, 6, 0f, 0.5f, 0f, 1.2f, 0.12f, 1.2f),
                Part(EnvironmentShape.Cube, 4, 0f, 1.5f, 0f, 0.7f, 0.8f, 0.7f),
                Part(EnvironmentShape.Cube, 2, 0f, 1.95f, 0f, 1.1f, 0.1f, 1.1f))),
            Radial(Def("BigRock", 3, 1.6f, 2.2f, 0f,
                Part(EnvironmentShape.Sphere, 4, 0f, 0.6f, 0f, 2.2f, 1.2f, 1.8f),
                Part(EnvironmentShape.Sphere, 4, 1.1f, 0.4f, 0.3f, 1.2f, 0.8f, 1.0f),
                Part(EnvironmentShape.Sphere, 6, -0.7f, 0.7f, 0.2f, 0.6f, 0.4f, 0.5f))),
            Radial(Def("DriftwoodFar", 2, 1.6f, 2.2f, 0f,
                Part(EnvironmentShape.Cylinder, 6, 0f, 0.3f, 0f, 0.5f, 2.2f, 0.5f, 0f, 0f, 90f),
                Part(EnvironmentShape.Cylinder, 6, 0.9f, 0.45f, 0.1f, 0.35f, 0.9f, 0.35f, 0f, 0f, -40f)))
        };

    // =============================================================
    // ГОРЫ
    // =============================================================

    private static readonly Color[] GoryPalette =
    {
        new Color(0.34f, 0.36f, 0.40f), // 0 скала тёмная
        new Color(0.58f, 0.60f, 0.66f), // 1 скала светлая
        new Color(0.94f, 0.96f, 0.97f), // 2 снег
        new Color(0.16f, 0.32f, 0.20f), // 3 ель тёмная
        new Color(0.22f, 0.44f, 0.26f), // 4 ель светлая
        new Color(0.48f, 0.46f, 0.42f)  // 5 гравий
    };

    private static readonly Color GoryGlow =
        new Color(0.92f, 0.97f, 1.00f);

    private static EnvironmentObjectDef[] GoryObstacles =>
        new[]
        {
            Def("RockFormation", 6, 0.9f, 1.4f, 1.0f,
                Part(EnvironmentShape.Cube, 0, 0f, 0.5f, 0f, 1.2f, 1.0f, 0.9f, 0f, 30f, 10f),
                Part(EnvironmentShape.Cube, 1, 0.6f, 0.3f, 0.25f, 0.7f, 0.6f, 0.6f, 0f, -20f, 15f),
                Part(EnvironmentShape.Cube, 2, 0.1f, 0.95f, 0.1f, 0.85f, 0.12f, 0.6f)),
            Def("Boulder", 4, 0.8f, 1.2f, 0.85f,
                Part(EnvironmentShape.Sphere, 0, 0f, 0.4f, 0f, 1.0f, 0.8f, 0.95f),
                Part(EnvironmentShape.Sphere, 1, 0.4f, 0.52f, 0.2f, 0.45f, 0.4f, 0.4f),
                Part(EnvironmentShape.Sphere, 2, -0.15f, 0.62f, 0.05f, 0.3f, 0.2f, 0.25f)),
            Def("Fir", 5, 0.9f, 1.3f, 0.6f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 0.5f, 0f, 0.16f, 1.0f, 0.16f),
                Part(EnvironmentShape.Cylinder, 3, 0f, 1.15f, 0f, 1.9f, 0.55f, 1.9f),
                Part(EnvironmentShape.Cylinder, 4, 0f, 1.62f, 0f, 1.35f, 0.5f, 1.35f),
                Part(EnvironmentShape.Cylinder, 3, 0f, 2.05f, 0f, 0.8f, 0.45f, 0.8f),
                Part(EnvironmentShape.Cylinder, 4, 0f, 2.45f, 0f, 0.4f, 0.4f, 0.4f)),
            Def("CaveCliff", 2, 1.2f, 1.5f, 1.2f,
                Part(EnvironmentShape.Cube, 0, 0f, 0.9f, 0f, 2.0f, 1.8f, 1.2f),
                Part(EnvironmentShape.Cube, 1, 0.3f, 0.5f, 0.45f, 0.9f, 1.0f, 0.3f),
                Part(EnvironmentShape.Cube, 2, 0f, 1.75f, 0f, 2.1f, 0.14f, 1.3f)),
            Corner(Def("CornerSpire", 2, 1.6f, 2.2f, 1.4f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 1.2f, 0f, 1.7f, 2.4f, 1.7f),
                Part(EnvironmentShape.Cylinder, 1, 0f, 2.5f, 0f, 1.2f, 1.5f, 1.2f),
                Part(EnvironmentShape.Cylinder, 2, 0f, 3.4f, 0f, 0.7f, 0.8f, 0.7f),
                Part(EnvironmentShape.Cube, 1, 1.1f, 0.9f, 0.4f, 1.2f, 0.8f, 0.9f, 0f, 18f, 12f)))
        };

    private static EnvironmentObjectDef[] GoryDecor =>
        new[]
        {
            Def("Pebble", 5, 0.4f, 0.6f, 0.3f,
                Part(EnvironmentShape.Sphere, 1, 0f, 0.15f, 0f, 0.5f, 0.3f, 0.45f),
                Part(EnvironmentShape.Sphere, 5, 0.3f, 0.1f, 0.15f, 0.3f, 0.2f, 0.3f)),
            Def("GrassClump", 4, 0.5f, 0.8f, 0.3f,
                Part(EnvironmentShape.Cylinder, 4, 0f, 0.2f, 0f, 0.06f, 0.4f, 0.06f),
                Part(EnvironmentShape.Cylinder, 3, 0.15f, 0.18f, 0.05f, 0.06f, 0.36f, 0.06f, 0f, 0f, 15f),
                Part(EnvironmentShape.Cylinder, 4, -0.12f, 0.17f, -0.05f, 0.05f, 0.34f, 0.05f, 0f, 0f, -18f))
        };

    private static EnvironmentObjectDef[] GoryOutside =>
        new[]
        {
            Radial(Def("BigPeak", 4, 1.8f, 2.6f, 0f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 1.5f, 0f, 3.4f, 1.6f, 3.4f),
                Part(EnvironmentShape.Cylinder, 1, 0f, 2.45f, 0f, 2.4f, 1.1f, 2.4f),
                Part(EnvironmentShape.Cylinder, 2, 0f, 3.2f, 0f, 1.5f, 0.9f, 1.5f),
                Part(EnvironmentShape.Cylinder, 2, 0f, 3.9f, 0f, 0.7f, 0.7f, 0.7f))),
            Radial(Def("SnowyPeak", 3, 2.0f, 3.0f, 0f,
                Part(EnvironmentShape.Cylinder, 1, 0f, 2.0f, 0f, 3.8f, 2.2f, 3.8f),
                Part(EnvironmentShape.Cylinder, 2, 0f, 3.4f, 0f, 2.2f, 1.6f, 2.2f),
                Part(EnvironmentShape.Sphere, 2, 0f, 4.6f, 0f, 1.4f, 1.0f, 1.4f))),
            Radial(Def("RockField", 3, 1.5f, 2.2f, 0f,
                Part(EnvironmentShape.Cube, 0, 0f, 0.6f, 0f, 2.2f, 1.2f, 1.6f, 0f, 25f, 10f),
                Part(EnvironmentShape.Cube, 1, 1.0f, 0.4f, 0.3f, 1.2f, 0.8f, 0.9f, 0f, -15f, 15f),
                Part(EnvironmentShape.Cube, 2, -0.4f, 1.2f, 0.1f, 1.0f, 0.2f, 0.7f))),
            Radial(Def("BigFir", 3, 1.8f, 2.4f, 0f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 0.8f, 0f, 0.22f, 1.6f, 0.22f),
                Part(EnvironmentShape.Cylinder, 3, 0f, 1.8f, 0f, 3.0f, 0.8f, 3.0f),
                Part(EnvironmentShape.Cylinder, 4, 0f, 2.55f, 0f, 2.2f, 0.75f, 2.2f),
                Part(EnvironmentShape.Cylinder, 3, 0f, 3.2f, 0f, 1.4f, 0.65f, 1.4f),
                Part(EnvironmentShape.Cylinder, 4, 0f, 3.8f, 0f, 0.7f, 0.6f, 0.7f)))
        };

    // =============================================================
    // ПУСТЫНЯ
    // =============================================================

    private static readonly Color[] PustynyaPalette =
    {
        new Color(0.90f, 0.75f, 0.50f), // 0 песок
        new Color(0.28f, 0.50f, 0.26f), // 1 кактус
        new Color(0.55f, 0.43f, 0.31f), // 2 камень
        new Color(0.56f, 0.48f, 0.30f), // 3 сухой куст
        new Color(0.92f, 0.90f, 0.82f), // 4 кости
        new Color(0.62f, 0.36f, 0.20f), // 5 меса (красный)
        new Color(0.80f, 0.64f, 0.40f)  // 6 тень дюны
    };

    private static readonly Color PustynyaGlow =
        new Color(1.00f, 0.88f, 0.55f);

    private static EnvironmentObjectDef[] PustynyaObstacles =>
        new[]
        {
            Def("Cactus", 6, 0.9f, 1.4f, 0.55f,
                Part(EnvironmentShape.Cylinder, 1, 0f, 0.85f, 0f, 0.8f, 1.7f, 0.8f),
                Part(EnvironmentShape.Cylinder, 1, 0.65f, 1.0f, 0.05f, 0.4f, 0.8f, 0.4f, 0f, 0f, -25f),
                Part(EnvironmentShape.Cylinder, 1, -0.6f, 1.1f, -0.05f, 0.38f, 0.9f, 0.38f, 0f, 0f, 20f),
                Part(EnvironmentShape.Sphere, 4, 0f, 1.72f, 0f, 0.22f, 0.22f, 0.22f)),
            Def("DriedBush", 5, 0.8f, 1.2f, 0.5f,
                Part(EnvironmentShape.Sphere, 3, 0f, 0.25f, 0f, 0.75f, 0.5f, 0.75f),
                Part(EnvironmentShape.Sphere, 3, 0.38f, 0.16f, 0.2f, 0.42f, 0.3f, 0.42f),
                Part(EnvironmentShape.Cylinder, 2, 0f, 0.12f, 0f, 0.3f, 0.24f, 0.3f)),
            Def("Stone", 4, 0.7f, 1.1f, 0.8f,
                Part(EnvironmentShape.Sphere, 2, 0f, 0.33f, 0f, 1.0f, 0.66f, 0.9f),
                Part(EnvironmentShape.Sphere, 2, 0.42f, 0.24f, 0.3f, 0.5f, 0.42f, 0.5f),
                Part(EnvironmentShape.Sphere, 0, -0.3f, 0.4f, 0.15f, 0.32f, 0.26f, 0.3f)),
            Def("MesaMini", 3, 0.9f, 1.2f, 1.1f,
                Part(EnvironmentShape.Cube, 5, 0f, 0.7f, 0f, 1.2f, 1.4f, 1.0f, 0f, 20f, 0f),
                Part(EnvironmentShape.Cube, 0, -0.05f, 1.35f, 0.1f, 1.15f, 0.14f, 0.95f, 0f, 20f, 0f)),
            Def("Bones", 2, 0.8f, 1.1f, 0.6f,
                Part(EnvironmentShape.Cube, 4, 0f, 0.3f, 0f, 0.55f, 0.4f, 0.45f),
                Part(EnvironmentShape.Cylinder, 4, 0f, 0.3f, 0.28f, 0.12f, 0.6f, 0.12f, 0f, 0f, 90f),
                Part(EnvironmentShape.Cylinder, 4, 0f, 0.3f, -0.18f, 0.1f, 0.5f, 0.1f, 0f, 25f, 90f),
                Part(EnvironmentShape.Cylinder, 4, 0.2f, 0.16f, 0.3f, 0.1f, 0.35f, 0.1f),
                Part(EnvironmentShape.Cylinder, 4, -0.2f, 0.14f, 0.25f, 0.1f, 0.3f, 0.1f)),
            Corner(Def("CornerButte", 2, 1.7f, 2.3f, 1.4f,
                Part(EnvironmentShape.Cube, 5, 0f, 1.3f, 0f, 2.6f, 2.6f, 2.0f, 0f, 22f, 0f),
                Part(EnvironmentShape.Cube, 0, -0.15f, 2.4f, 0.15f, 2.8f, 0.5f, 2.3f, 0f, 22f, 0f),
                Part(EnvironmentShape.Cube, 2, 0.9f, 0.9f, 0.5f, 1.1f, 1.1f, 0.9f, 0f, 12f, -10f)))
        };

    private static EnvironmentObjectDef[] PustynyaDecor =>
        new[]
        {
            Def("Dune", 4, 1.4f, 1.9f, 0.2f,
                Part(EnvironmentShape.Sphere, 0, 0f, 0.16f, 0f, 2.6f, 0.32f, 1.8f),
                Part(EnvironmentShape.Sphere, 6, 0.35f, 0.22f, 0.2f, 1.5f, 0.22f, 1.1f)),
            Def("StoneSmall", 3, 0.5f, 0.7f, 0.35f,
                Part(EnvironmentShape.Sphere, 2, 0f, 0.2f, 0f, 0.6f, 0.4f, 0.55f))
        };

    private static EnvironmentObjectDef[] PustynyaOutside =>
        new[]
        {
            Radial(Def("Mesa", 3, 1.8f, 2.6f, 0f,
                Part(EnvironmentShape.Cube, 5, 0f, 1.5f, 0f, 3.0f, 3.0f, 2.4f, 0f, 30f, 0f),
                Part(EnvironmentShape.Cube, 0, -0.15f, 2.75f, 0.15f, 3.2f, 0.5f, 2.7f, 0f, 30f, 0f))),
            Radial(Def("BigDune", 3, 2.0f, 3.0f, 0f,
                Part(EnvironmentShape.Sphere, 0, 0f, 0.3f, 0f, 5.0f, 0.6f, 3.5f),
                Part(EnvironmentShape.Sphere, 6, 0.8f, 0.42f, 0.5f, 3.0f, 0.45f, 2.2f))),
            Radial(Def("CactusFar", 3, 1.5f, 2.0f, 0f,
                Part(EnvironmentShape.Cylinder, 1, 0f, 1.5f, 0f, 1.2f, 3.0f, 1.2f),
                Part(EnvironmentShape.Cylinder, 1, 1.0f, 1.7f, 0.1f, 0.6f, 1.4f, 0.6f, 0f, 0f, -22f),
                Part(EnvironmentShape.Cylinder, 1, -0.9f, 1.9f, -0.1f, 0.55f, 1.6f, 0.55f, 0f, 0f, 18f))),
            Radial(Def("RedButte", 2, 2.2f, 3.0f, 0f,
                Part(EnvironmentShape.Cube, 5, 0f, 1.5f, 0f, 2.6f, 3.0f, 2.0f, 0f, 15f, 0f),
                Part(EnvironmentShape.Cube, 0, -0.1f, 2.8f, 0.1f, 2.8f, 0.5f, 2.3f, 0f, 15f, 0f)))
        };

    // =============================================================
    // КОСМОС
    // =============================================================

    private static readonly Color[] KosmosPalette =
    {
        new Color(0.38f, 0.39f, 0.44f), // 0 металл
        new Color(0.22f, 0.23f, 0.27f), // 1 металл тёмный
        new Color(0.35f, 0.90f, 0.90f), // 2 кристалл
        new Color(0.52f, 0.52f, 0.56f), // 3 камень
        new Color(0.96f, 0.64f, 0.28f), // 4 оранжевый (свет/лампа)
        new Color(0.96f, 0.96f, 0.98f), // 5 звёзды
        new Color(0.20f, 0.55f, 0.65f), // 6 планета
        new Color(0.30f, 0.55f, 0.95f)  // 7 реактор
    };

    private static readonly Color KosmosGlow =
        new Color(0.35f, 0.90f, 0.90f);

    private static EnvironmentObjectDef[] KosmosObstacles =>
        new[]
        {
            Def("Asteroid", 5, 0.8f, 1.3f, 0.9f,
                Part(EnvironmentShape.Sphere, 3, 0f, 0.4f, 0f, 1.1f, 0.9f, 1.0f),
                Part(EnvironmentShape.Sphere, 3, 0.4f, 0.5f, 0.2f, 0.5f, 0.45f, 0.5f),
                Part(EnvironmentShape.Sphere, 2, -0.45f, 0.55f, 0.3f, 0.28f, 0.28f, 0.28f, 0f, 0f, 0f, true)),
            Def("BrokenPanel", 3, 0.9f, 1.3f, 0.8f,
                Part(EnvironmentShape.Cube, 0, 0f, 0.3f, 0f, 1.5f, 0.5f, 1.0f, 0f, 25f, 10f),
                Part(EnvironmentShape.Cube, 1, 0.2f, 0.42f, 0.1f, 0.8f, 0.14f, 0.7f, 0f, 25f, 10f),
                Part(EnvironmentShape.Cube, 4, -0.55f, 0.34f, 0.2f, 0.16f, 0.1f, 0.16f, 0f, 0f, 0f, true)),
            Def("CrystalShard", 4, 0.7f, 1.1f, 0.5f,
                Part(EnvironmentShape.Capsule, 2, 0f, 0.6f, 0f, 0.3f, 1.2f, 0.3f, 0f, 0f, 0f, true),
                Part(EnvironmentShape.Capsule, 2, -0.45f, 0.4f, 0.4f, 0.25f, 0.9f, 0.25f, 15f, 45f, 30f, true),
                Part(EnvironmentShape.Capsule, 2, 0.4f, 0.3f, -0.3f, 0.2f, 0.7f, 0.2f, -12f, -30f, 20f, true)),
            Def("Debris", 3, 0.8f, 1.2f, 0.7f,
                Part(EnvironmentShape.Cube, 1, 0f, 0.25f, 0f, 0.9f, 0.5f, 0.7f),
                Part(EnvironmentShape.Cube, 0, 0.4f, 0.35f, 0.15f, 0.4f, 0.3f, 0.4f, 0f, 30f, 15f),
                Part(EnvironmentShape.Cylinder, 1, -0.5f, 0.3f, 0.1f, 0.2f, 0.5f, 0.2f, 0f, 0f, 60f)),
            Corner(Def("CornerAsteroid", 2, 1.7f, 2.4f, 1.4f,
                Part(EnvironmentShape.Sphere, 3, 0f, 1.2f, 0f, 2.4f, 1.8f, 2.2f),
                Part(EnvironmentShape.Sphere, 3, 1.6f, 0.9f, 0.6f, 1.4f, 1.1f, 1.3f),
                Part(EnvironmentShape.Sphere, 2, -1.2f, 1.4f, -0.5f, 0.8f, 0.8f, 0.8f, 0f, 0f, 0f, true)))
        };

    private static EnvironmentObjectDef[] KosmosDecor =>
        new[]
        {
            Def("SparkleShard", 4, 0.4f, 0.7f, 0.3f,
                Part(EnvironmentShape.Capsule, 2, 0f, 0.2f, 0f, 0.2f, 0.7f, 0.2f, 5f, 0f, 10f, true),
                Part(EnvironmentShape.Cube, 5, 0.15f, 0.15f, 0.1f, 0.12f, 0.08f, 0.12f, 0f, 0f, 0f, true)),
            Def("PipeGlow", 3, 0.5f, 0.8f, 0.35f,
                Part(EnvironmentShape.Cylinder, 1, 0f, 0.2f, 0f, 0.25f, 0.4f, 0.25f),
                Part(EnvironmentShape.Cube, 7, 0f, 0.28f, 0f, 0.18f, 0.1f, 0.18f, 0f, 0f, 0f, true))
        };

    private static EnvironmentObjectDef[] KosmosOutside =>
        new[]
        {
            RingDef("Stars", 16, 0.8f, 1.3f, 0f, 55f, 200f,
                Part(EnvironmentShape.Cube, 5, 0f, 1.6f, 0f, 0.22f, 0.1f, 0.22f, 0f, 0f, 0f, true)),
            FixedDef("Planet", -185f, 0f,
                Part(EnvironmentShape.Sphere, 6, 0f, 6f, 0f, 40f, 40f, 40f, 0f, 60f, 0f, true)),
            FixedDef("Moon", 150f, 3f,
                Part(EnvironmentShape.Sphere, 3, 0f, 3f, 0f, 12f, 12f, 12f),
                Part(EnvironmentShape.Sphere, 3, 3f, 3.5f, 1f, 4f, 4f, 4f)),
            Def("StationWreck", 2, 1.0f, 1.5f, 0f,
                Part(EnvironmentShape.Cylinder, 0, 0f, 1.2f, 0f, 0.9f, 2.4f, 0.9f),
                Part(EnvironmentShape.Cylinder, 0, -1.4f, 1.0f, 0.2f, 0.5f, 1.2f, 0.5f, 0f, 0f, 100f),
                Part(EnvironmentShape.Cube, 4, -0.75f, 1.6f, 0.25f, 0.18f, 0.1f, 0.2f, 0f, 0f, 0f, true),
                Part(EnvironmentShape.Cube, 7, 0.1f, 2.2f, 0.1f, 0.24f, 0.12f, 0.14f, 0f, 0f, 0f, true)),
            Radial(Def("DarkRock", 3, 1.5f, 2.2f, 0f,
                Part(EnvironmentShape.Sphere, 3, 0f, 0.7f, 0f, 2.4f, 1.4f, 2.0f),
                Part(EnvironmentShape.Sphere, 3, 1.2f, 0.5f, 0.5f, 1.3f, 1.0f, 1.1f),
                Part(EnvironmentShape.Sphere, 2, -0.9f, 0.8f, 0.4f, 0.35f, 0.35f, 0.35f, 0f, 0f, 0f, true)))
        };

    // =============================================================
    // ГОРОД
    // =============================================================

    private static readonly Color[] GorodPalette =
    {
        new Color(0.28f, 0.29f, 0.31f), // 0 асфальт
        new Color(0.52f, 0.52f, 0.54f), // 1 бетон
        new Color(0.18f, 0.22f, 0.30f), // 2 тёмное стекло
        new Color(0.53f, 0.31f, 0.24f), // 3 кирпич
        new Color(1.00f, 0.85f, 0.50f), // 4 тёплый свет окон
        new Color(0.40f, 0.42f, 0.46f), // 5 металл
        new Color(0.30f, 0.47f, 0.28f), // 6 зелень
        new Color(0.30f, 0.75f, 0.90f)  // 7 неон
    };

    private static readonly Color GorodGlow =
        new Color(1.00f, 0.85f, 0.50f);

    private static EnvironmentObjectDef[] GorodObstacles =>
        new[]
        {
            Def("Container", 2, 0.9f, 1.2f, 1.0f,
                Part(EnvironmentShape.Cube, 5, 0f, 0.5f, 0f, 1.6f, 1.0f, 1.1f),
                Part(EnvironmentShape.Cube, 0, 0f, 0.9f, 0.02f, 1.65f, 0.08f, 1.15f),
                Part(EnvironmentShape.Cube, 4, 0f, 0.5f, 0.58f, 1.6f, 0.1f, 0.05f, 0f, 0f, 0f, true)),
            Def("LowWall", 3, 1.0f, 1.3f, 1.2f,
                Part(EnvironmentShape.Cube, 1, 0f, 0.4f, 0f, 2.2f, 0.8f, 0.4f),
                Part(EnvironmentShape.Cube, 5, 0f, 0.8f, 0f, 2.26f, 0.06f, 0.46f),
                Part(EnvironmentShape.Cube, 3, 0f, 0.5f, 0.24f, 0.3f, 0.5f, 0.06f)),
            Def("Car", 2, 1.0f, 1.3f, 1.1f,
                Part(EnvironmentShape.Cube, 5, 0f, 0.3f, 0f, 1.7f, 0.5f, 0.9f),
                Part(EnvironmentShape.Cube, 2, -0.1f, 0.62f, 0f, 0.9f, 0.4f, 0.8f),
                Part(EnvironmentShape.Cube, 0, -0.55f, 0.55f, 0f, 0.5f, 0.55f, 0.7f),
                Part(EnvironmentShape.Cube, 4, 0.15f, 0.35f, 0.47f, 0.4f, 0.12f, 0.08f, 0f, 0f, 0f, true)),
            Corner(Def("CornerSkyscraper", 2, 1.6f, 2.1f, 1.6f,
                Part(EnvironmentShape.Cube, 0, 0f, 1.6f, 0f, 2.2f, 3.2f, 2.0f),
                Part(EnvironmentShape.Cube, 1, 0f, 3.3f, 0f, 1.7f, 1.4f, 1.5f),
                Part(EnvironmentShape.Cube, 5, 0f, 4.15f, 0f, 1.1f, 0.5f, 1.0f),
                Part(EnvironmentShape.Cube, 2, 0f, 2.1f, 1.03f, 1.9f, 2.2f, 0.12f),
                Part(EnvironmentShape.Cube, 2, 0f, 2.1f, -1.03f, 1.9f, 2.2f, 0.12f),
                Part(EnvironmentShape.Cube, 4, 0.6f, 1.9f, 1.07f, 0.4f, 0.14f, 0.04f, 0f, 0f, 0f, true),
                Part(EnvironmentShape.Cube, 4, -0.7f, 2.6f, 1.07f, 0.4f, 0.14f, 0.06f, 0f, 0f, 0f, true),
                Part(EnvironmentShape.Cube, 4, 0.2f, 3.6f, 0.79f, 0.5f, 0.16f, 0.05f, 0f, 0f, 0f, true)))
        };

    private static EnvironmentObjectDef[] GorodDecor =>
        new[]
        {
            Def("StreetLamp", 3, 1.0f, 1.3f, 0.4f,
                Part(EnvironmentShape.Cylinder, 5, 0f, 1.4f, 0f, 0.12f, 2.8f, 0.12f),
                Part(EnvironmentShape.Cube, 4, 0f, 2.85f, 0f, 0.1f, 0.1f, 0.5f, 0f, 0f, 0f, true)),
            Def("Bush", 4, 0.6f, 0.9f, 0.5f,
                Part(EnvironmentShape.Sphere, 6, 0f, 0.25f, 0f, 0.7f, 0.5f, 0.7f),
                Part(EnvironmentShape.Sphere, 6, 0.3f, 0.4f, 0.1f, 0.4f, 0.35f, 0.4f)),
            Def("Bench", 2, 1.0f, 1.2f, 0.9f,
                Part(EnvironmentShape.Cube, 6, 0f, 0.4f, 0f, 1.4f, 0.1f, 0.4f),
                Part(EnvironmentShape.Cube, 1, 0f, 0.58f, 0.18f, 0.3f, 0.35f, 0.4f),
                Part(EnvironmentShape.Cylinder, 5, -0.6f, 0.2f, 0.1f, 0.07f, 0.4f, 0.07f),
                Part(EnvironmentShape.Cylinder, 5, 0.6f, 0.2f, -0.1f, 0.07f, 0.4f, 0.07f)),
            Def("Hydrant", 2, 0.8f, 1.0f, 0.4f,
                Part(EnvironmentShape.Cylinder, 5, 0f, 0.25f, 0f, 0.18f, 0.5f, 0.18f),
                Part(EnvironmentShape.Cylinder, 0, 0f, 0.5f, 0f, 0.24f, 0.08f, 0.24f),
                Part(EnvironmentShape.Cylinder, 5, 0f, 0.25f, 0.16f, 0.06f, 0.3f, 0.06f))
        };

    private static EnvironmentObjectDef[] GorodOutside =>
        new[]
        {
            Radial(Def("TowerBlock", 4, 1.6f, 2.2f, 0f,
                Part(EnvironmentShape.Cube, 0, 0f, 1.8f, 0f, 2.4f, 3.6f, 1.8f),
                Part(EnvironmentShape.Cube, 2, 0f, 1.8f, 0.92f, 2.3f, 3.5f, 0.1f),
                Part(EnvironmentShape.Cube, 2, 0f, 1.8f, -0.92f, 2.3f, 3.5f, 0.1f),
                Part(EnvironmentShape.Cube, 4, 0f, 2.4f, 0.95f, 1.8f, 0.12f, 0.05f, 0f, 0f, 0f, true),
                Part(EnvironmentShape.Cube, 4, 0f, 3.3f, 0.95f, 1.2f, 0.12f, 0.05f, 0f, 0f, 0f, true))),
            Radial(Def("OfficeBuilding", 3, 1.5f, 2.0f, 0f,
                Part(EnvironmentShape.Cube, 1, 0f, 1.2f, 0f, 2.6f, 2.4f, 1.6f),
                Part(EnvironmentShape.Cube, 2, 0f, 1.2f, 0.82f, 2.5f, 2.3f, 0.08f),
                Part(EnvironmentShape.Cube, 2, 0f, 1.2f, -0.82f, 2.5f, 2.3f, 0.08f),
                Part(EnvironmentShape.Cube, 4, 0.5f, 1.6f, 0.84f, 0.5f, 0.14f, 0.04f, 0f, 0f, 0f, true),
                Part(EnvironmentShape.Cube, 4, -0.6f, 2.3f, 0.84f, 0.5f, 0.14f, 0.04f, 0f, 0f, 0f, true))),
            FixedDef("Highway", 0f, 95f,
                Part(EnvironmentShape.Cube, 0, 0f, 0.1f, 0f, 300f, 0.2f, 12f),
                Part(EnvironmentShape.Cube, 7, 0f, 0.21f, 0f, 300f, 0.02f, 0.2f, 0f, 0f, 0f, true))
        };

    // =============================================================
    // ХЕЛПЕРЫ
    // =============================================================

    private static EnvironmentPart Part(
        EnvironmentShape shape,
        int color,
        float x,
        float y,
        float z,
        float sx = 1f,
        float sy = 1f,
        float sz = 1f,
        float rx = 0f,
        float ry = 0f,
        float rz = 0f,
        bool glow = false)
    {
        return new EnvironmentPart
        {
            shape = shape,
            color = color,
            glow = glow,
            position = new Vector3(x, y, z),
            euler = new Vector3(rx, ry, rz),
            scale = new Vector3(sx, sy, sz)
        };
    }

    private static EnvironmentObjectDef Def(
        string name,
        int count,
        float scaleMin,
        float scaleMax,
        float footprint,
        params EnvironmentPart[] parts)
    {
        return new EnvironmentObjectDef
        {
            name = name,
            count = count,
            scaleMin = scaleMin,
            scaleMax = scaleMax,
            footprint = footprint,
            parts = parts
        };
    }

    private static EnvironmentObjectDef RingDef(
        string name,
        int count,
        float scaleMin,
        float scaleMax,
        float footprint,
        float minRadius,
        float maxRadius,
        params EnvironmentPart[] parts)
    {
        EnvironmentObjectDef def =
            Def(
                name,
                count,
                scaleMin,
                scaleMax,
                footprint,
                parts
            );

        def.overrideMinRadius = minRadius;
        def.overrideMaxRadius = maxRadius;

        def.placement =
            EnvironmentObjectPlacement.RadialRing;

        return def;
    }

    private static EnvironmentObjectDef FixedDef(
        string name,
        float offsetX,
        float offsetZ,
        params EnvironmentPart[] parts)
    {
        EnvironmentObjectDef def =
            Def(name, 1, 1f, 1f, 0f, parts);

        def.fixedPlacement = true;
        def.fixedOffset =
            new Vector2(offsetX, offsetZ);

        def.placement =
            EnvironmentObjectPlacement.RadialRing;

        return def;
    }

    /// <summary>Деф по умолчанию (Def) расставляется SquareFrame по периметру — уже по умолчанию. </summary>
    private static EnvironmentObjectDef Frame(
        EnvironmentObjectDef def)
    {
        def.placement =
            EnvironmentObjectPlacement.SquareFrame;

        return def;
    }

    /// <summary>Крупные акцентные объекты в углах квадратной арены.</summary>
    private static EnvironmentObjectDef Corner(
        EnvironmentObjectDef def)
    {
        def.placement =
            EnvironmentObjectPlacement.Corner;

        return def;
    }

    /// <summary>Фон за стенами — кольцо вокруг арены (RadialRing).</summary>
    private static EnvironmentObjectDef Radial(
        EnvironmentObjectDef def)
    {
        def.placement =
            EnvironmentObjectPlacement.RadialRing;

        return def;
    }

    private static void CreateTheme(
        string assetName,
        string displayName,
        Color[] palette,
        Color glowColor,
        EnvironmentObjectDef[] obstacles,
        EnvironmentObjectDef[] decor,
        EnvironmentObjectDef[] outside,
        string mapAssetName,
        int seed)
    {
        string path =
            Folder + "/" + assetName + ".asset";

        EnvironmentTheme theme =
            AssetDatabase.LoadAssetAtPath<EnvironmentTheme>(path);

        if (theme == null)
        {
            theme = ScriptableObject.CreateInstance<EnvironmentTheme>();
            AssetDatabase.CreateAsset(theme, path);
        }

        theme.name = assetName;
        theme.themeName = displayName;
        theme.seed = seed;
        theme.palette = palette;
        theme.glowMaterial =
            EnsureGlowMaterial(assetName, glowColor);

        // Ручные назначения моделей переезжают на новый рецепт,
        // чтобы перезапуск генератора не стирал замены на свои модели.
        RestoreModels(
            theme,
            obstacles,
            decor,
            outside
        );

        theme.insideObstacles = obstacles;
        theme.insideDecor = decor;
        theme.outsideObjects = outside;

        EditorUtility.SetDirty(theme);

        GameMap map =
            AssetDatabase.LoadAssetAtPath<GameMap>(
                MapsFolder + "/" + mapAssetName + ".asset"
            );

        if (map == null)
            return;

        map.environmentTheme = theme;

        // Палитра структур карте НЕ назначается: нейтральные блоки
        // арены — общий серый слой для всех биомов (см. MapFactoryMenu).
        EditorUtility.SetDirty(map);
    }

    /// <summary>
    /// Переносит назначенные в инспекторе модели с текущих рецептов
    /// темы на новые. Поиск по имени дефа: замена своих моделей
    /// переживает перезапуск генератора.
    /// </summary>
    private static void RestoreModels(
        EnvironmentTheme theme,
        EnvironmentObjectDef[] obstacles,
        EnvironmentObjectDef[] decor,
        EnvironmentObjectDef[] outside)
    {
        var modelsByName =
            new Dictionary<string, GameObject>();

        CollectModels(theme.insideObstacles, modelsByName);
        CollectModels(theme.insideDecor, modelsByName);
        CollectModels(theme.outsideObjects, modelsByName);

        CopyModels(obstacles, modelsByName);
        CopyModels(decor, modelsByName);
        CopyModels(outside, modelsByName);
    }

    private static void CollectModels(
        EnvironmentObjectDef[] defs,
        Dictionary<string, GameObject> modelsByName)
    {
        if (defs == null)
            return;

        for (int i = 0; i < defs.Length; i++)
        {
            EnvironmentObjectDef def = defs[i];

            if (def == null ||
                def.model == null ||
                modelsByName.ContainsKey(def.name))
            {
                continue;
            }

            modelsByName[def.name] = def.model;
        }
    }

    private static void CopyModels(
        EnvironmentObjectDef[] defs,
        Dictionary<string, GameObject> modelsByName)
    {
        if (defs == null)
            return;

        for (int i = 0; i < defs.Length; i++)
        {
            EnvironmentObjectDef def = defs[i];

            if (def == null ||
                def.model != null)
            {
                continue;
            }

            if (modelsByName.TryGetValue(
                    def.name,
                    out GameObject model))
            {
                def.model = model;
            }
        }
    }

    /// <summary>
    /// Светящийся материал темы: создаётся как ассет, чтобы шейдер
    /// URP Unlit гарантированно попал в билд (в отличие от Shader.Find
    /// в рантайме, который может упереться в вырезанный шейдер).
    /// </summary>
    private static Material EnsureGlowMaterial(
        string assetName,
        Color color)
    {
        string path =
            MaterialsFolder + "/" + assetName + "_Glow.mat";

        Material material =
            AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material != null)
            return material;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Standard");

        material = new Material(shader)
        {
            name = assetName + "_Glow"
        };

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        else if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);

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