#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Построитель панели «Улучшения» в главном меню.
/// Пункт меню: Tools -> Bullet Rush -> Build Upgrades UI.
///
/// За один клик:
///  - удаляет старую панель «Улучшения», если она есть;
///  - создаёт панель «Улучшения» (шапка + ScrollRect с шаблоном карточки
///    + кнопка «Назад») на месте панели «Снаряжение»;
///  - подключает все ссылки на MainMenuUI / PermanentUpgradesUI /
///    SubPanelUI;
///  - добавляет PermanentUpgradeApplier на игрока (навешивает
///    постоянные бонусы на старте забега).
///
/// Кнопка «Улучшения» в меню обычно уже есть — если её нет,
/// она клонируется из кнопки «Магазин».
/// </summary>
public static class UpgradesUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Upgrades UI";

    private const string PanelName = "UpgradesPanel";

    // Сумма строк карточки: 36 + 30 + 30 + 30 + 30 + 16 + 48
    // + вертикальные отступы 32 + интервалы 4 * 6 = 276.
    private const float UpgradeCardHeight = 276f;
    private const float BackButtonHeight = 60f;

    [MenuItem(MenuPath)]
    public static void Build()
    {
        MainMenuUI menu =
            Object.FindAnyObjectByType<MainMenuUI>();

        if (menu == null)
        {
            EditorUtility.DisplayDialog(
                "Build Upgrades UI",
                "MainMenuUI не найден в сцене. Открой сцену Game и запусти пункт меню.",
                "OK"
            );

            return;
        }

        SerializedObject menuSo =
            new SerializedObject(menu);

        GameObject menuPanel =
            menuSo.FindProperty("menuPanel").objectReferenceValue as GameObject;

        if (menuPanel == null)
        {
            EditorUtility.DisplayDialog(
                "Build Upgrades UI",
                "menuPanel не назначен на MainMenuUI.",
                "OK"
            );

            return;
        }

        Button shopButton =
            menuSo.FindProperty("shopButton").objectReferenceValue as Button;

        Transform panelParent = menuPanel.transform.parent;

        if (panelParent == null)
            panelParent = menuPanel.transform;

        // =====================================================
        // 0. Чистим старое и готовим кнопку «Улучшения»
        // =====================================================
        DeletePreviousPanel(panelParent, menuSo);

        Button upgradesButton = EnsureUpgradesButton(menuSo, shopButton);

        if (upgradesButton == null)
        {
            EditorUtility.DisplayDialog(
                "Build Upgrades UI",
                "Не найдена кнопка «Улучшения» в меню, а клонировать её " +
                "некому: не назначена кнопка shopButton.\n\n" +
                "Назначь shopButton в инспекторе MainMenuUI и запусти " +
                "пункт меню ещё раз.",
                "OK"
            );

            return;
        }

        // =====================================================
        // 1. Панель (корень)
        // =====================================================
        GameObject panelObject =
            new GameObject(PanelName, typeof(RectTransform));

        panelObject.transform.SetParent(panelParent, false);

        RectTransform panelRect =
            panelObject.GetComponent<RectTransform>();

        SetFullStretch(panelRect);

        Image panelBg = panelObject.AddComponent<Image>();
        panelBg.color = new Color(0.07f, 0.07f, 0.11f, 0.97f);

        Undo.RegisterCreatedObjectUndo(panelObject, MenuPath);

        // =====================================================
        // 2. Шапка
        // =====================================================
        TextMeshProUGUI titleText = AddText(
            panelRect,
            "TitleText",
            "УЛУЧШЕНИЯ",
            42,
            TextAlignmentOptions.Center,
            FontStyles.Bold
        );

        SetAnchors(
            titleText.rectTransform,
            0.5f, 1f,
            new Vector2(0f, -24f),
            new Vector2(500f, 60f)
        );

        TextMeshProUGUI levelText = AddText(
            panelRect,
            "LevelText",
            "LEVEL 1",
            26,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Normal
        );

        SetAnchors(
            levelText.rectTransform,
            0f, 1f,
            new Vector2(28f, -30f),
            new Vector2(320f, 60f)
        );

        TextMeshProUGUI coinsText = AddText(
            panelRect,
            "CoinsText",
            "МОНЕТЫ: 0",
            26,
            TextAlignmentOptions.MidlineRight,
            FontStyles.Normal
        );

        RectTransform coinsRect = coinsText.rectTransform;
        coinsRect.anchorMin = new Vector2(1f, 1f);
        coinsRect.anchorMax = new Vector2(1f, 1f);
        coinsRect.pivot = new Vector2(1f, 1f);
        coinsRect.anchoredPosition = new Vector2(-28f, -30f);
        coinsRect.sizeDelta = new Vector2(320f, 60f);

        TextMeshProUGUI summaryText = AddText(
            panelRect,
            "SummaryText",
            "Куплено улучшений: 0 / 0",
            22,
            TextAlignmentOptions.Center,
            FontStyles.Normal
        );

        summaryText.color = new Color(0.85f, 0.85f, 0.80f, 1f);
        summaryText.textWrappingMode = TextWrappingModes.NoWrap;

        SetAnchors(
            summaryText.rectTransform,
            0.5f, 1f,
            new Vector2(0f, -92f),
            new Vector2(640f, 60f)
        );

        // =====================================================
        // 3. Область прокрутки с карточками
        // =====================================================
        (RectTransform areaRoot, _, GameObject cardTemplate) =
            BuildScrollArea(panelRect, UpgradeCardHeight);

        // =====================================================
        // 4. Кнопка «Назад»
        // =====================================================
        Button backButton = AddButton(panelRect, "BackButton");
        backButton.image.color = new Color(0.20f, 0.30f, 0.45f, 0.95f);

        RectTransform backRect = backButton.GetComponent<RectTransform>();
        backRect.anchorMin = new Vector2(0f, 0f);
        backRect.anchorMax = new Vector2(0f, 0f);
        backRect.pivot = new Vector2(0f, 0f);
        backRect.anchoredPosition = new Vector2(24f, 12f);
        backRect.sizeDelta = new Vector2(220f, BackButtonHeight);

        TextMeshProUGUI backText = AddText(
            backButton.transform,
            "Text",
            "Назад",
            24,
            TextAlignmentOptions.Center,
            FontStyles.Normal
        );

        SetFullStretch(backText.rectTransform);

        // =====================================================
        // 5. Компоненты панели
        // =====================================================
        PermanentUpgradesUI upgradesUI =
            panelObject.AddComponent<PermanentUpgradesUI>();

        SerializedObject upgradesSo =
            new SerializedObject(upgradesUI);

        upgradesSo.FindProperty("playerLevelText").objectReferenceValue =
            levelText;
        upgradesSo.FindProperty("playerCoinsText").objectReferenceValue =
            coinsText;
        upgradesSo.FindProperty("summaryText").objectReferenceValue =
            summaryText;
        upgradesSo.FindProperty("scrollAreaRoot").objectReferenceValue =
            areaRoot;
        upgradesSo.FindProperty("cardTemplate").objectReferenceValue =
            cardTemplate;

        upgradesSo.ApplyModifiedProperties();

        SubPanelUI subPanel = panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);

        subSo.FindProperty("backButton").objectReferenceValue = backButton;

        subSo.ApplyModifiedProperties();

        // =====================================================
        // 6. Подключаем к главному меню и игроку
        // =====================================================
        menuSo.FindProperty("upgradesButton").objectReferenceValue =
            upgradesButton;
        menuSo.FindProperty("upgradesPanel").objectReferenceValue =
            panelObject;

        menuSo.ApplyModifiedProperties();

        EnsurePlayerUpgradeApplier();

        // =====================================================
        // 7. Финал
        // =====================================================
        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        EditorUtility.DisplayDialog(
            "Build Upgrades UI",
            "Панель «Улучшения» создана и подключена.\n\n" +
            $"Параметров: {PermanentUpgrades.Stats.Count}, " +
            $"уровней на параметр: {PermanentUpgrades.MaxLevel}.\n" +
            $"Цена уровня: 10 -> 15 -> 20 -> 25 -> 35 -> 45 -> 60 -> " +
            "80 -> 110 -> 150.\n\n" +
            "Постоянные бонусы применяются на старте забега " +
            "(PermanentUpgradeApplier добавлен на игрока).\n" +
            "Стилизация карточек — на твоё усмотрение.",
            "OK"
        );
    }

    // =====================================================
    // CLEANUP
    // =====================================================

    private static void DeletePreviousPanel(
        Transform panelParent,
        SerializedObject menuSo)
    {
        menuSo.FindProperty("upgradesPanel").objectReferenceValue = null;

        menuSo.ApplyModifiedProperties();

        if (panelParent == null)
            return;

        var children =
            new System.Collections.Generic.List<Transform>();

        foreach (Transform child in panelParent)
            children.Add(child);

        foreach (Transform child in children)
        {
            if (child.name == PanelName)
                Object.DestroyImmediate(child.gameObject);
        }
    }

    /// <summary>
    /// Кнопка «Улучшения» обычно уже есть в сцене — используем её.
    /// Если нет — клонируем «Магазин», чтобы сохранить стиль меню.
    /// </summary>
    private static Button EnsureUpgradesButton(
        SerializedObject menuSo,
        Button shopButton)
    {
        Button existing =
            menuSo.FindProperty("upgradesButton").objectReferenceValue as Button;

        if (existing != null)
            return existing;

        if (shopButton == null)
            return null;

        GameObject buttonObject =
            Object.Instantiate(
                shopButton.gameObject,
                shopButton.transform.parent
            );

        buttonObject.name = "UpgradesButton";

        Undo.RegisterCreatedObjectUndo(buttonObject, MenuPath);

        TextMeshProUGUI label =
            buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
            label.text = "Улучшения";

        Button button = buttonObject.GetComponent<Button>();

        if (button == null)
            button = buttonObject.AddComponent<Button>();

        return button;
    }

    private static bool EnsurePlayerUpgradeApplier()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return false;

        if (player.GetComponent<PermanentUpgradeApplier>() == null)
        {
            player.AddComponent<PermanentUpgradeApplier>();

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.scene);
        }

        return true;
    }

    // =====================================================
    // SCROLL AREA
    // =====================================================

    private static (
        RectTransform root,
        RectTransform content,
        GameObject card
    ) BuildScrollArea(
        RectTransform panelRect,
        float cardHeight)
    {
        RectTransform scrollArea = AddRect(
            panelRect,
            "UpgradesScrollArea",
            Vector2.zero,
            Vector2.one,
            new Vector2(24f, 84f),        // снизу: над кнопкой «Назад»
            new Vector2(-24f, -156f)      // сверху: под шапкой
        );

        Image scrollBg = scrollArea.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(0.12f, 0.12f, 0.17f, 0.95f);

        ScrollRect scrollRect =
            scrollArea.gameObject.AddComponent<ScrollRect>();

        // 1 Viewport
        RectTransform viewport = AddRect(
            scrollArea,
            "Viewport",
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );

        viewport.gameObject.AddComponent<RectMask2D>();

        // 2 Content + вертикальный список карточек
        RectTransform content = AddRect(
            viewport,
            "Content",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        content.pivot = new Vector2(0.5f, 1f);

        VerticalLayoutGroup layout =
            content.gameObject.AddComponent<VerticalLayoutGroup>();

        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.padding = new RectOffset(12, 12, 12, 12);

        ContentSizeFitter fitter =
            content.gameObject.AddComponent<ContentSizeFitter>();

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;

        // 3 Шаблон карточки
        RectTransform cardRect =
            BuildCardTemplate(content, cardHeight);

        // 4 Полоса прокрутки
        Scrollbar scrollbar = BuildVerticalScrollbar(scrollArea);

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.verticalScrollbar = scrollbar;
        scrollRect.verticalScrollbarVisibility =
            ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        scrollRect.verticalScrollbarSpacing = 4f;

        return (
            scrollArea,
            content,
            cardRect.gameObject
        );
    }

    private static RectTransform BuildCardTemplate(
        RectTransform content,
        float cardHeight)
    {
        RectTransform cardRect = AddRect(
            content,
            "CardTemplate",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        Image cardBg = cardRect.gameObject.AddComponent<Image>();
        cardBg.color = new Color(0.18f, 0.18f, 0.24f, 1f);

        LayoutElement cardLayout =
            cardRect.gameObject.AddComponent<LayoutElement>();

        cardLayout.preferredHeight = cardHeight;

        VerticalLayoutGroup cardRows =
            cardRect.gameObject.AddComponent<VerticalLayoutGroup>();

        cardRows.spacing = 4f;
        cardRows.childAlignment = TextAnchor.UpperLeft;
        cardRows.childControlWidth = true;
        cardRows.childForceExpandWidth = true;
        cardRows.childControlHeight = true;
        cardRows.childForceExpandHeight = false;
        cardRows.padding = new RectOffset(18, 18, 16, 16);

        AddCardText(cardRect, "Name", "Урон", 32, FontStyles.Bold, 36f);
        AddCardText(
            cardRect,
            "Type",
            "Описание параметра",
            20,
            FontStyles.Normal,
            30f
        );
        AddCardText(
            cardRect,
            "Stats",
            "Сейчас: без бонуса",
            20,
            FontStyles.Normal,
            30f,
            new Color(0.55f, 0.90f, 0.55f, 1f)
        );
        AddCardText(
            cardRect,
            "Next",
            "Дальше: +5% к урону",
            18,
            FontStyles.Normal,
            30f
        );
        AddCardText(
            cardRect,
            "Status",
            "УРОВЕНЬ 0 / 10",
            20,
            FontStyles.Bold,
            30f
        );

        BuildProgressBar(cardRect, 16f);

        Button actionButton = AddButton(cardRect, "ActionButton");

        RectTransform actionRect = actionButton.GetComponent<RectTransform>();
        actionRect.anchorMin = Vector2.zero;
        actionRect.anchorMax = Vector2.one;
        actionRect.offsetMin = Vector2.zero;
        actionRect.offsetMax = Vector2.zero;

        LayoutElement actionLayout =
            actionButton.gameObject.AddComponent<LayoutElement>();

        actionLayout.preferredHeight = 48f;

        TextMeshProUGUI actionText = AddText(
            actionButton.transform,
            "Text",
            "Улучшить",
            22,
            TextAlignmentOptions.Center,
            FontStyles.Bold
        );

        SetFullStretch(actionText.rectTransform);

        cardRect.gameObject.SetActive(false);

        return cardRect;
    }

    /// <summary>
    /// Горизонтальная полоса прогресса уровня: Background -> Fill.
    /// Заполняется значением slider.value в PermanentUpgradesUI.
    /// </summary>
    private static Slider BuildProgressBar(
        RectTransform parent,
        float height)
    {
        RectTransform sliderRect = AddRect(
            parent,
            "Progress",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );

        LayoutElement layout =
            sliderRect.gameObject.AddComponent<LayoutElement>();

        layout.preferredHeight = height;

        Image background =
            sliderRect.gameObject.AddComponent<Image>();

        background.color = new Color(0.10f, 0.10f, 0.14f, 1f);

        RectTransform fillArea = AddRect(
            sliderRect,
            "FillArea",
            Vector2.zero,
            Vector2.one,
            new Vector2(2f, 2f),
            new Vector2(-2f, -2f)
        );

        RectTransform fill = AddRect(
            fillArea,
            "Fill",
            Vector2.zero,
            new Vector2(0f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        Image fillImage = fill.gameObject.AddComponent<Image>();
        fillImage.color = new Color(0.35f, 0.70f, 0.40f, 1f);

        Slider slider = sliderRect.gameObject.AddComponent<Slider>();

        slider.interactable = false;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fill;
        slider.targetGraphic = fillImage;
        slider.value = 0f;

        return slider;
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private static void AddCardText(
        RectTransform parent,
        string name,
        string content,
        int size,
        FontStyles style,
        float height,
        Color? color = null)
    {
        TextMeshProUGUI text = AddText(
            parent,
            name,
            content,
            size,
            TextAlignmentOptions.MidlineLeft,
            style
        );

        // TMP не реализует ILayoutElement, поэтому высоту строки
        // задаём явно — иначе VerticalLayoutGroup схлопнет её в ноль.
        LayoutElement layout =
            text.gameObject.AddComponent<LayoutElement>();

        layout.preferredHeight = height;

        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.color = color ?? new Color(0.92f, 0.92f, 0.92f, 1f);
    }

    private static TextMeshProUGUI AddText(
        Transform parent,
        string name,
        string content,
        int size,
        TextAlignmentOptions alignment,
        FontStyles style)
    {
        GameObject textObject =
            new GameObject(name, typeof(RectTransform));

        textObject.transform.SetParent(parent, false);

        TextMeshProUGUI text =
            textObject.AddComponent<TextMeshProUGUI>();

        if (TMP_Settings.defaultFontAsset != null)
            text.font = TMP_Settings.defaultFontAsset;

        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.fontStyle = style;
        text.color = Color.white;

        return text;
    }

    private static Button AddButton(Transform parent, string name)
    {
        RectTransform rect = AddRect(
            parent,
            name,
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.25f, 0.55f, 0.25f, 1f);

        return rect.gameObject.AddComponent<Button>();
    }

    private static Scrollbar BuildVerticalScrollbar(RectTransform parent)
    {
        RectTransform scrollbarRect = AddRect(
            parent,
            "ScrollbarVertical",
            new Vector2(1f, 0f),
            new Vector2(1f, 1f),
            new Vector2(-34f, 8f),
            new Vector2(-12f, -8f)
        );

        scrollbarRect.pivot = new Vector2(1f, 0.5f);

        Image background =
            scrollbarRect.gameObject.AddComponent<Image>();

        background.color = new Color(0.16f, 0.16f, 0.22f, 0.9f);

        Scrollbar scrollbar =
            scrollbarRect.gameObject.AddComponent<Scrollbar>();

        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform slidingArea = AddRect(
            scrollbarRect,
            "SlidingArea",
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );

        RectTransform handle = AddRect(
            slidingArea,
            "Handle",
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            new Vector2(0.12f, 0.12f),
            new Vector2(-0.12f, -0.12f)
        );

        Image handleImage = handle.gameObject.AddComponent<Image>();
        handleImage.color = new Color(0.55f, 0.55f, 0.60f, 0.95f);

        scrollbar.targetGraphic = handleImage;
        scrollbar.handleRect = handle;

        return scrollbar;
    }

    private static RectTransform AddRect(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax)
    {
        GameObject rectObject =
            new GameObject(name, typeof(RectTransform));

        rectObject.transform.SetParent(parent, false);

        RectTransform rect =
            rectObject.GetComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        return rect;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void SetAnchors(
        RectTransform rect,
        float anchorX,
        float anchorY,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin = new Vector2(anchorX, anchorY);
        rect.anchorMax = new Vector2(anchorX, anchorY);
        rect.pivot = new Vector2(anchorX, anchorY);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}

#endif
