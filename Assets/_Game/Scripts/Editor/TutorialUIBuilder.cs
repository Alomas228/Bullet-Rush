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
///  - создаёт панель TutorialPanel на Canvas (затемнение + четыре
///    заготовки картинок WASD / прицел / стрельба / рывок + текст задачи
///    + счётчик «ШАГ X/8» + кнопку «Пропустить»);
///  - добавляет на панель Tips в меню кнопку «Пройти обучение заново»;
///  - создаёт объект TutorialManager и автоматически связывает его
///    с WaveManager / EnemySpawner / UpgradeUI и созданным UI;
///  - выключает raycast у всех элементов, кроме кнопки «Пропустить»,
///    чтобы игра продолжала принимать ввод во время показа инструкций.
///
/// После сборки останется вставить свои спрайты в Image_WASD /
/// Image_Aim / Image_Shoot / Image_Dash (и при желании — markerSprite).
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

        GameObject dash = CreateStepImage(
            panel.transform,
            "Image_Dash",
            new Color(0.15f, 0.15f, 0.22f, 0.95f)
        );

        // =====================================================
        // 2. Текст задачи (сверху по центру)
        // =====================================================
        TMP_FontAsset font = FindDefaultFont();

        GameObject textObject =
            CreateRect("ObjectiveText", panel.transform);

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -40f);
        textRect.sizeDelta = new Vector2(1150f, 300f);

        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();

        text.text = string.Empty;
        text.font = font;
        text.fontSize = 48f;
        text.lineSpacing = 1.1f;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        textObject.SetActive(false);

        // =====================================================
        // 3. Счётчик «ШАГ X/8» (слева сверху)
        // =====================================================
        GameObject progressObject =
            CreateRect("ProgressText", panel.transform);

        RectTransform progressRect =
            progressObject.GetComponent<RectTransform>();

        progressRect.anchorMin = new Vector2(0f, 1f);
        progressRect.anchorMax = new Vector2(0f, 1f);
        progressRect.pivot = new Vector2(0f, 1f);
        progressRect.anchoredPosition = new Vector2(30f, -30f);
        progressRect.sizeDelta = new Vector2(300f, 48f);

        TextMeshProUGUI progress =
            progressObject.AddComponent<TextMeshProUGUI>();

        progress.text = string.Empty;
        progress.font = font;
        progress.fontSize = 34f;
        progress.fontStyle = FontStyles.Bold;
        progress.alignment = TextAlignmentOptions.Left;
        progress.color = new Color(0.85f, 0.85f, 0.85f, 1f);
        progress.raycastTarget = false;

        progressObject.SetActive(false);

        // =====================================================
        // 4. Кнопка «Пропустить» (справа снизу)
        // =====================================================
        GameObject skip = CreateRect("SkipButton", panel.transform);

        RectTransform skipRect =
            skip.GetComponent<RectTransform>();

        skipRect.anchorMin = new Vector2(1f, 0f);
        skipRect.anchorMax = new Vector2(1f, 0f);
        skipRect.pivot = new Vector2(1f, 0f);
        skipRect.anchoredPosition = new Vector2(-40f, 40f);
        skipRect.sizeDelta = new Vector2(230f, 64f);

        Image skipImage = skip.AddComponent<Image>();
        skipImage.color = new Color(0f, 0f, 0f, 0.6f);
        skipImage.raycastTarget = true;

        Button skipButton = skip.AddComponent<Button>();
        skipButton.targetGraphic = skipImage;

        GameObject skipLabel =
            CreateRect("Label", skip.transform);

        RectTransform skipLabelRect =
            skipLabel.GetComponent<RectTransform>();

        Stretch(skipLabelRect);

        TextMeshProUGUI skipText =
            skipLabel.AddComponent<TextMeshProUGUI>();

        skipText.text = "ПРОПУСТИТЬ";
        skipText.font = font;
        skipText.fontSize = 30f;
        skipText.fontStyle = FontStyles.Bold;
        skipText.alignment = TextAlignmentOptions.Center;
        skipText.color = Color.white;
        skipText.raycastTarget = false;

        // Панель обучения кладём в самый низ Canvas, чтобы пауза,
        // меню и Game Over рисовались поверх неё.
        panel.transform.SetSiblingIndex(0);

        panel.SetActive(false);

        Undo.RegisterCreatedObjectUndo(
            panel,
            "Build Tutorial UI"
        );

        // =====================================================
        // 5. Кнопка «Пройти обучение заново» на панели Tips
        // =====================================================
        bool hasTipsButton =
            BuildMenuReplayButton(font);

        // =====================================================
        // 6. Менеджер + автосвязь
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
        SetRef(so, "dashImage", dash);
        SetRef(so, "objectiveText", text);
        SetRef(so, "progressText", progress);
        SetRef(so, "skipButton", skipButton);

        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);

        bool hasWaveManager =
            so.FindProperty("waveManager").objectReferenceValue != null;

        string message =
            "UI обучения создано.\n\n" +
            "1. Вставь свои спрайты в Image_WASD, Image_Aim, " +
            "Image_Shoot и Image_Dash.\n" +
            "2. Спрайт метки над врагом — поле markerSprite на " +
            "TutorialManager (если пусто, рисуется встроенная жёлтая стрелка).\n" +
            "3. Панель Tips в меню получила кнопку «Пройти обучение заново».\n\n" +
            (hasWaveManager
                ? "WaveManager найден — всё связано автоматически."
                : "ВНИМАНИЕ: WaveManager в сцене не найден! " +
                  "Назначь ссылки на TutorialManager вручную.");

        if (!hasTipsButton)
        {
            message +=
                "\n\nПРЕДУПРЕЖДЕНИЕ: MainMenuUI.tipsPanel не найден — " +
                "кнопка «Пройти обучение заново» не создана.";
        }

        EditorUtility.DisplayDialog(
            "Build Tutorial UI",
            message,
            "OK"
        );
    }

    private static bool BuildMenuReplayButton(TMP_FontAsset font)
    {
        MainMenuUI menu =
            Object.FindAnyObjectByType<MainMenuUI>();

        if (menu == null)
            return false;

        SerializedObject so =
            new SerializedObject(menu);

        SerializedProperty tipsProperty =
            so.FindProperty("tipsPanel");

        GameObject tipsPanel =
            tipsProperty != null
                ? tipsProperty.objectReferenceValue as GameObject
                : null;

        if (tipsPanel == null)
            return false;

        // Чистим старое, чтобы не было дублей при пересборке.
        Transform oldButton =
            tipsPanel.transform.Find("ReplayTutorialButton");

        if (oldButton != null)
            Object.DestroyImmediate(oldButton.gameObject);

        GameObject buttonRoot =
            CreateRect("ReplayTutorialButton", tipsPanel.transform);

        RectTransform rect =
            buttonRoot.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(40f, -40f);
        rect.sizeDelta = new Vector2(380f, 64f);

        Image image = buttonRoot.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.6f);
        image.raycastTarget = true;

        Button button = buttonRoot.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject =
            CreateRect("Label", buttonRoot.transform);

        Stretch(labelObject.GetComponent<RectTransform>());

        TextMeshProUGUI label =
            labelObject.AddComponent<TextMeshProUGUI>();

        label.text = "ПРОЙТИ ОБУЧЕНИЕ ЗАНОВО";
        label.font = font;
        label.fontSize = 26f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.raycastTarget = false;

        GameObject statusObject =
            CreateRect("StatusText", tipsPanel.transform);

        RectTransform statusRect =
            statusObject.GetComponent<RectTransform>();

        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(0f, 1f);
        statusRect.pivot = new Vector2(0f, 1f);
        statusRect.anchoredPosition = new Vector2(40f, -120f);
        statusRect.sizeDelta = new Vector2(900f, 48f);

        TextMeshProUGUI status =
            statusObject.AddComponent<TextMeshProUGUI>();

        status.text = string.Empty;
        status.font = font;
        status.fontSize = 24f;
        status.fontStyle = FontStyles.Bold;
        status.alignment = TextAlignmentOptions.Left;
        status.raycastTarget = false;

        TutorialReplayButton replay =
            buttonRoot.AddComponent<TutorialReplayButton>();

        SerializedObject replaySo =
            new SerializedObject(replay);

        SetRef(replaySo, "statusText", status);

        replaySo.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(
            tipsPanel,
            "Build Tutorial UI"
        );

        return true;
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

    private static TMP_FontAsset FindDefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        TMP_FontAsset[] all =
            Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

        foreach (TMP_FontAsset asset in all)
        {
            if (asset != null && asset.name.Contains("LiberationSans"))
                return asset;
        }

        foreach (TMP_FontAsset asset in all)
        {
            if (asset != null && !asset.name.StartsWith("Legacy"))
                return asset;
        }

        return null;
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