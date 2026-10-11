#if UNITY_EDITOR

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Собирает экран достижений в сцене Game: кнопку в главном меню,
/// панель со скроллом, шаблон строки и проводку ссылок в MainMenuUI
/// и AchievementsUI. По стилю повторяет DailyRewardUIBuilder, чтобы
/// все меню собирались одинаково.
/// </summary>
public static class AchievementsUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Achievements";

    private const string PanelName = "AchievementsPanel";

    private const string RowTemplateName = "RowTemplate";

    private const float RowHeight = 140f;

    private static readonly Color PanelBackground =
        new Color(0f, 0f, 0f, 0.72f);

    private static readonly Color ButtonBackground =
        new Color(1f, 1f, 1f, 0.2f);

    private static readonly Color RowBackground =
        new Color(1f, 1f, 1f, 0.06f);

    [MenuItem(MenuPath)]
    public static void Build()
    {
        string report = BuildInternal();

        EditorUtility.DisplayDialog(
            "Build Achievements",
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
            "[AchievementsUIBuilder] " + report.Replace("\n", " ")
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

        Button achievementsButton =
            EnsureAchievementsButton(menuSo, menuPanel);

        DeletePreviousPanel(panelParent, menuSo);

        TMP_FontAsset font = FindDefaultFont();

        GameObject panelObject = CreatePanel(panelParent);

        TextMeshProUGUI title =
            CreateText(panelObject.transform, "TitleText", font, "{ach.title}",
                40f, TextAlignmentOptions.Left,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(40f, -30f), new Vector2(700f, 60f));

        TextMeshProUGUI progress =
            CreateText(panelObject.transform, "ProgressText", font, string.Empty,
                30f, TextAlignmentOptions.Right,
                new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-160f, -30f), new Vector2(260f, 60f));

        Button backButton = CreateBackButton(panelObject.transform, font);

        RectTransform content = CreateScroll(panelObject.transform);

        AchievementRowUI rowTemplate = CreateRowTemplate(panelObject.transform, font);

        AchievementsUI ui = panelObject.AddComponent<AchievementsUI>();

        SerializedObject uiSo = new SerializedObject(ui);

        SetRef(uiSo, "titleText", title);
        SetRef(uiSo, "progressText", progress);
        SetRef(uiSo, "closeButton", backButton);
        SetRef(uiSo, "content", content);
        SetRef(uiSo, "rowTemplate", rowTemplate);

        uiSo.ApplyModifiedPropertiesWithoutUndo();

        SubPanelUI subPanel = panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);
        SetRef(subSo, "backButton", backButton);
        subSo.ApplyModifiedPropertiesWithoutUndo();

        if (achievementsButton != null)
        {
            menuSo.FindProperty("achievementsButton")
                .objectReferenceValue = achievementsButton;
        }

        menuSo.FindProperty("achievementsPanel")
            .objectReferenceValue = panelObject;

        menuSo.ApplyModifiedPropertiesWithoutUndo();

        Undo.RegisterCreatedObjectUndo(panelObject, MenuPath);

        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        return "Экран достижений собран:\n" +
               "- панель: " + PanelName + "\n" +
               "- кнопка меню: " +
               (achievementsButton != null ? achievementsButton.name : "не создана") +
               "\n- источник кнопки: " + lastButtonSource;
    }

    // Порядок предпочтения: сначала берём уже назначенную кнопку,
    // затем любую из существующих кнопок меню, и только если нет
    // ни одной — создаём новую. shopButton больше не обязателен.
    private static readonly string[] ButtonSourceProperties =
    {
        "shopButton",
        "equipmentButton",
        "upgradesButton",
        "mapsButton",
        "dailyRewardButton",
        "profileButton",
        "tipsButton",
        "playButton"
    };

    private static string lastButtonSource = "не использовался";

    private static Button EnsureAchievementsButton(
        SerializedObject menuSo,
        GameObject menuPanel)
    {
        Button existing =
            menuSo.FindProperty("achievementsButton")?.objectReferenceValue as Button;

        if (existing != null)
        {
            lastButtonSource = "уже назначена";
            ResetLabel(existing);
            return existing;
        }

        GameObject found = GameObject.Find("Btn_Achievements");

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

                buttonObject.name = "Btn_Achievements";

                Undo.RegisterCreatedObjectUndo(buttonObject, MenuPath);

                existing = buttonObject.GetComponent<Button>();

                if (existing == null)
                    existing = buttonObject.AddComponent<Button>();

                lastButtonSource = source.name;
            }
        }
        else
        {
            lastButtonSource = "найдена готовая Btn_Achievements";
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
            label.text = "{ach.btn}";
    }

    /// <summary>
    /// Ищет любую кнопку меню: сначала по известным полям MainMenuUI,
    /// затем любую Button внутри menuPanel.
    /// </summary>
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
            "Btn_Achievements",
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
        label.text = "{ach.btn}";
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
        menuSo.FindProperty("achievementsPanel").objectReferenceValue = null;
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

    private static AchievementRowUI CreateRowTemplate(
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
                new Vector2(0f, -12f), new Vector2(-24f, 40f));

        TextMeshProUGUI description =
            CreateText(row.transform, "DescriptionText", font, string.Empty,
                22f, TextAlignmentOptions.TopLeft,
                new Vector2(0f, 0f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -52f), new Vector2(-24f, -92f));

        description.color = new Color(0.8f, 0.82f, 0.88f, 1f);

        TextMeshProUGUI reward =
            CreateText(row.transform, "RewardText", font, string.Empty,
                20f, TextAlignmentOptions.BottomLeft,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(-24f, 34f));

        reward.color = new Color(0.8f, 0.9f, 1f, 1f);

        TextMeshProUGUI progress =
            CreateText(row.transform, "ProgressText", font, string.Empty,
                20f, TextAlignmentOptions.BottomRight,
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 10f), new Vector2(-24f, 34f));

        AchievementRowUI rowUi = row.AddComponent<AchievementRowUI>();

        SerializedObject rowSo = new SerializedObject(rowUi);

        SetRef(rowSo, "titleLabel", title);
        SetRef(rowSo, "descriptionLabel", description);
        SetRef(rowSo, "progressLabel", progress);
        SetRef(rowSo, "rewardLabel", reward);
        SetRef(rowSo, "background", background);

        rowSo.ApplyModifiedPropertiesWithoutUndo();

        row.SetActive(false);

        return rowUi;
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
                "AchievementsUIBuilder: свойство '" + property + "' не найдено."
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
