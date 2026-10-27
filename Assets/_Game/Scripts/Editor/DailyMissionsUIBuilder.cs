#if UNITY_EDITOR

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Собирает экран ежедневных миссий в сцене Game: кнопку в главном меню,
/// панель со скроллом, шаблон строки (с полосой прогресса и кнопкой
/// получения) и проводку ссылок в MainMenuUI и DailyMissionUI.
/// По стилю повторяет AchievementsUIBuilder, чтобы все меню собирались
/// одинаково.
/// </summary>
public static class DailyMissionsUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Daily Missions";

    private const string PanelName = "DailyMissionsPanel";

    private const string RowTemplateName = "RowTemplate";

    private const float RowHeight = 150f;

    private static readonly Color PanelBackground =
        new Color(0f, 0f, 0f, 0.72f);

    private static readonly Color ButtonBackground =
        new Color(1f, 1f, 1f, 0.2f);

    private static readonly Color RowBackground =
        new Color(1f, 1f, 1f, 0.06f);

    private static readonly Color ClaimBackground =
        new Color(0.2f, 0.55f, 0.3f, 1f);

    private static readonly Color FillColor =
        new Color(0.3f, 0.75f, 0.45f, 0.9f);

    [MenuItem(MenuPath)]
    public static void Build()
    {
        string report = BuildInternal();

        EditorUtility.DisplayDialog(
            "Build Daily Missions",
            report,
            "OK"
        );
    }

    public static void BuildFromCommandLine()
    {
        Scene scene = EditorSceneManager.OpenScene(
            "Assets/Scenes/Game.unity",
            OpenSceneMode.Single
        );

        string report = BuildInternal();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "[DailyMissionsUIBuilder] " + report.Replace("\n", " ")
        );

        EditorApplication.Exit(0);
    }

    private static string BuildInternal()
    {
        MainMenuUI menu = Object.FindAnyObjectByType<MainMenuUI>();

        if (menu == null)
        {
            return "MainMenuUI не найден в сцене. " +
                   "Открой сцену Game и повтори.";
        }

        SerializedObject menuSo = new SerializedObject(menu);

        GameObject menuPanel =
            menuSo.FindProperty("menuPanel")?.objectReferenceValue as GameObject;

        if (menuPanel == null)
            return "menuPanel не назначен на MainMenuUI.";

        Transform panelParent = menuPanel.transform.parent != null
            ? menuPanel.transform.parent
            : menuPanel.transform;

        Button missionsButton =
            EnsureMissionsButton(menuSo, menuPanel);

        DeletePreviousPanel(panelParent, menuSo);

        TMP_FontAsset font = FindDefaultFont();

        GameObject panelObject = CreatePanel(panelParent);

        TextMeshProUGUI title =
            CreateText(panelObject.transform, "Title", font, "{mission.title}",
                40f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -30f), new Vector2(700f, 60f));

        TextMeshProUGUI progress =
            CreateText(panelObject.transform, "ProgressText", font, string.Empty,
                30f, TextAlignmentOptions.Right,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-160f, -30f), new Vector2(260f, 60f));

        Button dailyTab = CreateMissionTab(
            panelObject.transform,
            "DailyTab",
            Lang.GetOr("mission.daily.tab", "ЕЖЕДНЕВНЫЕ"),
            new Vector2(-130f, -92f),
            font
        );
        Button weeklyTab = CreateMissionTab(
            panelObject.transform,
            "WeeklyTab",
            Lang.GetOr("mission.weekly.tab", "ЕЖЕНЕДЕЛЬНЫЕ"),
            new Vector2(130f, -92f),
            font
        );

        Button backButton = CreateBackButton(panelObject.transform, font);

        RectTransform content = CreateScroll(panelObject.transform);

        DailyMissionRowUI rowTemplate =
            CreateRowTemplate(panelObject.transform, font);

        DailyMissionUI ui = panelObject.AddComponent<DailyMissionUI>();

        SerializedObject uiSo = new SerializedObject(ui);

        SetRef(uiSo, "titleText", title);
        SetRef(uiSo, "progressText", progress);
        SetRef(uiSo, "closeButton", backButton);
        SetRef(uiSo, "content", content);
        SetRef(uiSo, "rowTemplate", rowTemplate);
        SetRef(uiSo, "dailyTabButton", dailyTab);
        SetRef(uiSo, "weeklyTabButton", weeklyTab);

        uiSo.ApplyModifiedPropertiesWithoutUndo();

        SubPanelUI subPanel = panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);
        SetRef(subSo, "backButton", backButton);
        subSo.ApplyModifiedPropertiesWithoutUndo();

        if (missionsButton != null)
        {
            menuSo.FindProperty("missionsButton")
                .objectReferenceValue = missionsButton;
        }

        menuSo.FindProperty("missionsPanel")
            .objectReferenceValue = panelObject;

        menuSo.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(panelObject, MenuPath);

        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        return "Экран ежедневных миссий собран:\n" +
               "- панель: " + PanelName + "\n" +
               "- кнопка меню: " +
               (missionsButton != null ? missionsButton.name : "не создана") +
               "\n- источник кнопки: " + lastButtonSource;
    }

    // Порядок предпочтения: сначала берём уже назначенную кнопку,
    // затем любую из существующих кнопок меню, и только если нет
    // ни одной — создаём новую.
    private static readonly string[] ButtonSourceProperties =
    {
        "shopButton",
        "equipmentButton",
        "upgradesButton",
        "mapsButton",
        "dailyRewardButton",
        "achievementsButton",
        "profileButton",
        "tipsButton",
        "playButton"
    };

    private static string lastButtonSource = "не использовался";

    private static Button EnsureMissionsButton(
        SerializedObject menuSo,
        GameObject menuPanel)
    {
        Button existing =
            menuSo.FindProperty("missionsButton")?.objectReferenceValue as Button;

        if (existing != null)
        {
            lastButtonSource = "уже назначена";
            ResetLabel(existing);
            return existing;
        }

        GameObject found = GameObject.Find("Btn_Missions");

        if (found != null)
        {
            existing = found.GetComponent<Button>();
        }

        if (existing == null)
        {
            Button source = FindSourceButton(menuSo, menuPanel);

            if (source != null)
            {
                GameObject buttonObject = Object.Instantiate(
                    source.gameObject,
                    source.transform.parent
                );

                buttonObject.name = "Btn_Missions";

                Undo.RegisterCreatedObjectUndo(buttonObject, MenuPath);

                existing = buttonObject.GetComponent<Button>();

                if (existing == null)
                    existing = buttonObject.AddComponent<Button>();

                lastButtonSource = source.name;
            }
        }
        else
        {
            lastButtonSource = "найдена готовая Btn_Missions";
        }

        if (existing == null)
        {
            existing = CreateFreshButton(menuPanel);
            lastButtonSource = "создана с нуля";
        }

        if (existing == null)
            return null;

        ResetLabel(existing);

        return existing;
    }

    private static void ResetLabel(Button button)
    {
        TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
            label.text = "{mission.btn}";
    }

    private static Button FindSourceButton(
        SerializedObject menuSo,
        GameObject menuPanel)
    {
        for (int i = 0; i < ButtonSourceProperties.Length; i++)
        {
            SerializedProperty property =
                menuSo.FindProperty(ButtonSourceProperties[i]);

            if (property == null)
                continue;

            Button candidate = property.objectReferenceValue as Button;

            if (candidate != null && candidate.gameObject != menuPanel)
                return candidate;
        }

        if (menuPanel != null)
        {
            Button[] all = menuPanel.GetComponentsInChildren<Button>(true);

            if (all.Length > 0)
                return all[0];
        }

        return null;
    }

    private static Button CreateFreshButton(GameObject menuPanel)
    {
        if (menuPanel == null)
            return null;

        GameObject buttonObject = new GameObject(
            "Btn_Missions",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );

        buttonObject.transform.SetParent(menuPanel.transform, false);

        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = new Vector2(0f, 60f);
        rect.sizeDelta = new Vector2(360f, 80f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.2f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;

        GameObject labelObject = new GameObject("LabelText", typeof(RectTransform));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.font = FindDefaultFont();
        label.text = "{mission.btn}";
        label.fontSize = 26f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.raycastTarget = false;

        Undo.RegisterCreatedObjectUndo(buttonObject, MenuPath);

        return button;
    }

    private static void DeletePreviousPanel(
        Transform panelParent,
        SerializedObject menuSo)
    {
        menuSo.FindProperty("missionsPanel").objectReferenceValue = null;
        menuSo.ApplyModifiedPropertiesWithoutUndo();

        if (panelParent == null)
            return;

        Transform old = panelParent.Find(PanelName);

        if (old != null)
            Object.DestroyImmediate(old.gameObject);
    }

    private static GameObject CreatePanel(Transform parent)
    {
        GameObject panel = new GameObject(PanelName, typeof(RectTransform));
        panel.transform.SetParent(parent, false);

        RectTransform rect = panel.GetComponent<RectTransform>();
        SetFullStretch(rect);

        panel.AddComponent<Image>().color = PanelBackground;

        return panel;
    }

    private static Button CreateMissionTab(
        Transform parent,
        string name,
        string label,
        Vector2 position,
        TMP_FontAsset font)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(240f, 52f);

        Image image = go.GetComponent<Image>();
        image.sprite = ArcadeKitAssets.RoundRect;
        image.type = Image.Type.Sliced;
        image.color = ArcadeUiTheme.PanelRaised;
        image.raycastTarget = true;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        TextMeshProUGUI text = CreateText(
            go.transform,
            "Label",
            font,
            label,
            16f,
            TextAlignmentOptions.Center,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );
        text.font = ArcadeKitAssets.BodyBold;
        text.color = ArcadeUiTheme.TextPrimary;
        text.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        SetFullStretch(text.rectTransform);

        return button;
    }

    private static RectTransform CreateScroll(Transform parent)
    {
        GameObject scrollObject = new GameObject("Scroll", typeof(RectTransform));
        scrollObject.transform.SetParent(parent, false);

        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(40f, 120f);
        scrollRect.offsetMax = new Vector2(-40f, -110f);

        ScrollRect scroll = scrollObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform));
        viewportObject.transform.SetParent(scrollObject.transform, false);

        RectTransform viewportRect = viewportObject.GetComponent<RectTransform>();
        SetFullStretch(viewportRect);

        Image viewportImage = viewportObject.AddComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.25f);

        viewportObject.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewportObject.transform, false);

        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewportRect;
        scroll.content = contentRect;

        return contentRect;
    }

    private static DailyMissionRowUI CreateRowTemplate(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject row = new GameObject(RowTemplateName, typeof(RectTransform));
        row.transform.SetParent(parent, false);

        RectTransform rect = row.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(0f, RowHeight);

        Image background = row.AddComponent<Image>();
        background.color = RowBackground;

        LayoutElement element = row.AddComponent<LayoutElement>();
        element.minHeight = RowHeight;
        element.preferredHeight = RowHeight;

        TextMeshProUGUI title =
            CreateText(row.transform, "TitleText", font, string.Empty,
                28f, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -12f), new Vector2(-220f, 40f));

        TextMeshProUGUI description =
            CreateText(row.transform, "DescriptionText", font, string.Empty,
                22f, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -52f), new Vector2(-220f, -92f));

        description.color = new Color(0.8f, 0.82f, 0.88f, 1f);

        TextMeshProUGUI reward =
            CreateText(row.transform, "RewardText", font, string.Empty,
                20f, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(-220f, 34f));

        reward.color = new Color(0.8f, 0.9f, 1f, 1f);

        TextMeshProUGUI progress =
            CreateText(row.transform, "ProgressText", font, string.Empty,
                20f, TextAlignmentOptions.TopRight,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-12f, -14f), new Vector2(200f, 34f));

        Slider progressBar =
            CreateProgressBar(row.transform, "ProgressBar", FillColor);

        RectTransform barRect = progressBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0f, 0f);
        barRect.offsetMin = new Vector2(12f, 46f);
        barRect.offsetMax = new Vector2(-12f, 52f);

        Button claimButton = CreateClaimButton(row.transform, font, out TextMeshProUGUI claimLabel);

        DailyMissionRowUI rowUi = row.AddComponent<DailyMissionRowUI>();

        SerializedObject rowSo = new SerializedObject(rowUi);

        SetRef(rowSo, "titleLabel", title);
        SetRef(rowSo, "descriptionLabel", description);
        SetRef(rowSo, "progressLabel", progress);
        SetRef(rowSo, "rewardLabel", reward);
        SetRef(rowSo, "background", background);
        SetRef(rowSo, "progressBar", progressBar);
        SetRef(rowSo, "claimButton", claimButton);
        SetRef(rowSo, "claimLabel", claimLabel);

        rowSo.ApplyModifiedPropertiesWithoutUndo();

        row.SetActive(false);

        return rowUi;
    }

    private static Button CreateClaimButton(
        Transform parent,
        TMP_FontAsset font,
        out TextMeshProUGUI label)
    {
        GameObject go = new GameObject(
            "ClaimButton",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button)
        );

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0.5f);
        rect.anchorMax = new Vector2(1f, 0.5f);
        rect.pivot = new Vector2(1f, 0.5f);
        rect.sizeDelta = new Vector2(180f, 54f);
        rect.anchoredPosition = new Vector2(-12f, -6f);

        Image image = go.GetComponent<Image>();
        image.color = ClaimBackground;

        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;

        label = CreateText(go.transform, "LabelText", font, "{mission.claim}",
            22f, TextAlignmentOptions.Center,
            Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
            Vector2.zero, Vector2.zero);

        SetFullStretch(label.rectTransform);

        return button;
    }

    private static Button CreateBackButton(
        Transform parent,
        TMP_FontAsset font)
    {
        GameObject block = new GameObject("BackButton", typeof(RectTransform));
        block.transform.SetParent(parent, false);

        RectTransform rect = block.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 0f);
        rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(40f, 30f);
        rect.sizeDelta = new Vector2(220f, 70f);

        Image image = block.AddComponent<Image>();
        image.color = ButtonBackground;

        Button button = block.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        TextMeshProUGUI label =
            CreateText(block.transform, "LabelText", font, "{eq.back}",
                24f, TextAlignmentOptions.Center,
                Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero);

        SetFullStretch(label.rectTransform);

        return button;
    }

    private static Slider CreateProgressBar(
        Transform parent,
        string name,
        Color fillColor)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);

        Image background = CreateImage(
            root.transform,
            "Background",
            new Color(1f, 1f, 1f, 0.12f)
        );

        SetFullStretch(background.rectTransform);

        Image fill = CreateImage(root.transform, "Fill", fillColor);

        SetFullStretch(fill.rectTransform);

        Slider slider = root.AddComponent<Slider>();

        slider.fillRect = fill.rectTransform;
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.value = 0f;

        return slider;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;

        return image;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        TMP_FontAsset font,
        string content,
        float size,
        TextAlignmentOptions alignment,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 sizeDelta)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = sizeDelta;

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        return text;
    }

    private static void SetRef(SerializedObject so, string property, Object value)
    {
        SerializedProperty found = so.FindProperty(property);

        if (found == null)
        {
            Debug.LogWarning(
                "DailyMissionsUIBuilder: свойство '" + property + "' не найдено."
            );

            return;
        }

        found.objectReferenceValue = value;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_FontAsset FindDefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        TMP_FontAsset[] all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

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
}

#endif
