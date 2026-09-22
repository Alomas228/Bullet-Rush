#if UNITY_EDITOR

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Строитель UI обучения в сцене.
/// Пункт меню: Tools -> Bullet Rush -> Build Tutorial UI.
///
/// За один клик:
///  - создаёт панель TutorialPanel на Canvas (затемнение + три
///    заготовки картинок WASD / прицел / стрельба + текст задачи);
///  - создаёт объект TutorialManager и автоматически связывает его
///    с WaveManager / EnemySpawner / UpgradeUI и созданным UI;
///  - выключает raycast у всех элементов, чтобы игра продолжала
///    принимать ввод во время показа инструкций.
///
/// После сборки останется вставить свои спрайты в Image_WASD /
/// Image_Aim / Image_Shoot (и при желании — markerSprite).
/// </summary>
public static class TutorialUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Tutorial UI";

    [MenuItem(MenuPath)]
    public static void Build()
    {
        Canvas canvas = FindMainCanvas();

        if (canvas == null)
        {
            EditorUtility.DisplayDialog(
                "Build Tutorial UI",
                "Screen-space Canvas не найден. Открой сцену Game и повтори.",
                "OK"
            );

            return;
        }

        // Старые объекты убираем, чтобы не было дублей при пересборке.
        Transform oldPanel = canvas.transform.Find("TutorialPanel");

        if (oldPanel != null)
            Object.DestroyImmediate(oldPanel.gameObject);

        GameObject oldManager = GameObject.Find("TutorialManager");

        if (oldManager != null)
            Object.DestroyImmediate(oldManager);

        // =====================================================
        // 1. Панель (без Image — ничего не рисует и не блокирует)
        // =====================================================
        GameObject panel = CreateRect("TutorialPanel", canvas.transform);
        Stretch(panel.GetComponent<RectTransform>());

        GameObject dim = CreateRect("DimBackground", panel.transform);
        Stretch(dim.GetComponent<RectTransform>());

        Image dimImage = dim.AddComponent<Image>();
        dimImage.color = new Color(0f, 0f, 0f, 0.55f);
        dimImage.raycastTarget = false;
        dim.SetActive(false);

        GameObject wasd = CreateStepImage(
            panel.transform,
            "Image_WASD",
            new Color(0.12f, 0.14f, 0.18f, 0.95f)
        );

        GameObject aim = CreateStepImage(
            panel.transform,
            "Image_Aim",
            new Color(0.12f, 0.18f, 0.12f, 0.95f)
        );

        GameObject shoot = CreateStepImage(
            panel.transform,
            "Image_Shoot",
            new Color(0.18f, 0.12f, 0.12f, 0.95f)
        );

        // =====================================================
        // 2. Текст задачи (сверху по центру)
        // =====================================================
        GameObject textObject =
            CreateRect("ObjectiveText", panel.transform);

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -30f);
        textRect.sizeDelta = new Vector2(1100f, 160f);

        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();

        text.text = string.Empty;
        text.fontSize = 56f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        textObject.SetActive(false);
        panel.SetActive(false);

        Undo.RegisterCreatedObjectUndo(
            panel,
            "Build Tutorial UI"
        );

        // =====================================================
        // 3. Менеджер + автосвязь
        // =====================================================
        GameObject managerObject =
            new GameObject("TutorialManager");

        Undo.RegisterCreatedObjectUndo(
            managerObject,
            "Build Tutorial UI"
        );

        TutorialManager manager =
            managerObject.AddComponent<TutorialManager>();

        SerializedObject so = new SerializedObject(manager);

        SetRef(
            so,
            "waveManager",
            Object.FindAnyObjectByType<WaveManager>()
        );

        SetRef(
            so,
            "enemySpawner",
            Object.FindAnyObjectByType<EnemySpawner>()
        );

        SetRef(
            so,
            "upgradeUI",
            Object.FindAnyObjectByType<UpgradeUI>()
        );

        SetRef(so, "tutorialPanel", panel);
        SetRef(so, "dimBackground", dim);
        SetRef(so, "wasdImage", wasd);
        SetRef(so, "aimImage", aim);
        SetRef(so, "shootImage", shoot);
        SetRef(so, "objectiveText", text);

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        bool hasWaveManager =
            so.FindProperty("waveManager").objectReferenceValue != null;

        EditorUtility.DisplayDialog(
            "Build Tutorial UI",
            "UI обучения создано.\n\n" +
            "1. Вставь свои спрайты в Image_WASD, Image_Aim, Image_Shoot.\n" +
            "2. Текст задачи и затемнение можно крутить на панели TutorialPanel.\n" +
            "3. Спрайт метки над врагом — поле markerSprite на TutorialManager " +
            "(если пусто, рисуется встроенная жёлтая стрелка).\n\n" +
            (hasWaveManager
                ? "WaveManager найден — всё связано автоматически."
                : "ВНИМАНИЕ: WaveManager в сцене не найден! Назначь ссылки на TutorialManager вручную."),
            "OK"
        );
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static void SetRef(
        SerializedObject so,
        string property,
        Object value)
    {
        SerializedProperty found = so.FindProperty(property);

        if (found == null)
        {
            Debug.LogWarning(
                $"TutorialUIBuilder: свойство '{property}' не найдено."
            );

            return;
        }

        found.objectReferenceValue = value;
    }

    private static Canvas FindMainCanvas()
    {
        Canvas[] canvases =
            Object.FindObjectsByType<Canvas>(
                FindObjectsInactive.Include
            );

        Canvas best = null;

        foreach (Canvas canvas in canvases)
        {
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay &&
                canvas.renderMode != RenderMode.ScreenSpaceCamera)
                continue;

            if (best == null ||
                canvas.gameObject.name == "Canvas")
            {
                best = canvas;
            }

            if (best != null &&
                best.gameObject.name == "Canvas")
                break;
        }

        return best;
    }

    private static GameObject CreateRect(
        string name,
        Transform parent)
    {
        GameObject go =
            new GameObject(name, typeof(RectTransform));

        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static GameObject CreateStepImage(
        Transform parent,
        string name,
        Color placeholderColor)
    {
        GameObject go = CreateRect(name, parent);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(760f, 430f);

        Image image = go.AddComponent<Image>();
        image.color = placeholderColor;
        image.preserveAspect = true;
        image.raycastTarget = false;

        go.SetActive(false);
        return go;
    }
}

#endif
