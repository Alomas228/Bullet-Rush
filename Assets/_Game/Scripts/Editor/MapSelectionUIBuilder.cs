#if UNITY_EDITOR

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Построитель панели «Карты» в главном меню.
/// Пункт меню: Tools -> Bullet Rush -> Build Maps UI.
///
/// За один клик:
///  - удаляет старую панель «Карты», если она есть;
///  - создаёт панель: заголовок, ряд карточек карт со стрелками,
///    точки пагинации и кнопку «Назад»;
///  - создаёт/находит кнопку «Карты» в главном меню;
///  - подключает ссылки на MainMenuUI / MapSelectionUI / SubPanelUI;
///  - создаёт объект EnvironmentController в сцене (если его нет)
///    и подставляет в него пять заготовленных карт из
///    Assets/Data/Maps (если они созданы пунктом
///    ArcadeSurvivor -> Create Demo Maps).
///
/// Дизайн — светлая wireframe-заглушка в конвенции «Улучшений» и
/// «Снаряжения»: цвета собраны в MapSelectionTheme, формы — в
/// Assets/_Game/UI/Placeholder.
/// </summary>
public static class MapSelectionUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Maps UI";

    private const string PanelName = "MapsPanel";

    private const int CardCount = MapSelectionUI.CardsPerPage;

    private const float CardWidth = 300f;
    private const float CardHeight = 420f;
    private const float CardGap = 20f;

    private const string DemoMapsFolder = "Assets/Data/Maps";

    private static readonly string[] DemoMapNames =
    {
        "Map_Les",
        "Map_Plyazh",
        "Map_Gory",
        "Map_Pustynya",
        "Map_Kosmos"
    };

    [MenuItem(MenuPath)]
    public static void Build()
    {
        MainMenuUI menu =
            Object.FindAnyObjectByType<MainMenuUI>();

        if (menu == null)
        {
            EditorUtility.DisplayDialog(
                "Build Maps UI",
                "MainMenuUI не найден в сцене. Открой сцену Game и " +
                "запусти пункт меню.",
                "OK"
            );

            return;
        }

        SerializedObject menuSo = new SerializedObject(menu);

        GameObject menuPanel =
            menuSo.FindProperty("menuPanel").objectReferenceValue as GameObject;

        if (menuPanel == null)
        {
            EditorUtility.DisplayDialog(
                "Build Maps UI",
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
        // 0. Чистим старое и готовим кнопку «Карты»
        // =====================================================
        DeletePreviousPanel(panelParent, menuSo);

        Button mapsButton = EnsureMapsButton(menuSo, shopButton);

        if (mapsButton == null)
        {
            EditorUtility.DisplayDialog(
                "Build Maps UI",
                "Не найдена кнопка «Карты» в меню, а клонировать её " +
                "некому: не назначена кнопка shopButton.\n\n" +
                "Назначь shopButton в инспекторе MainMenuUI и запусти " +
                "пункт меню ещё раз.",
                "OK"
            );

            return;
        }

        WireframePlaceholderSprites.EnsureAll();

        Sprite cardSprite = WireframePlaceholderSprites.CardSprite;
        Sprite barSprite = WireframePlaceholderSprites.BarSprite;
        Sprite circleSprite = WireframePlaceholderSprites.CircleSprite;

        // =====================================================
        // 1. Панель (корень)
        // =====================================================
        GameObject panelObject =
            new GameObject(PanelName, typeof(RectTransform));

        panelObject.transform.SetParent(panelParent, false);

        RectTransform panelRect =
            panelObject.GetComponent<RectTransform>();

        SetFullStretch(panelRect);

        // Полупрозрачный фон: за панелью видно, как уезжает/приезжает мир.
        Image panelImage = panelRect.gameObject.AddComponent<Image>();

        panelImage.color = MapSelectionTheme.PanelBackground;

        Undo.RegisterCreatedObjectUndo(panelObject, MenuPath);

        // =====================================================
        // 2. Шапка
        // =====================================================
        BuildHeader(panelRect);

        // =====================================================
        // 3. Ряд карточек со стрелками
        // =====================================================
        var cards = new List<MapCardView>();

        var arrows = new List<Button>();
        var arrowImages = new List<Image>();

        RectTransform cardArea = AddStretch(
            panelRect,
            "CardArea",
            0f,
            0f,
            0f,
            0f
        );

        cardArea.anchorMin = new Vector2(0f, 1f);
        cardArea.anchorMax = new Vector2(1f, 1f);
        cardArea.pivot = new Vector2(0.5f, 1f);
        cardArea.offsetMin = new Vector2(0f, -760f);
        cardArea.offsetMax = new Vector2(0f, -240f);

        RectTransform cardRow = AddStretch(
            cardArea,
            "CardRow",
            105f,
            105f,
            0f,
            0f
        );

        HorizontalLayoutGroup layout =
            cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();

        layout.spacing = CardGap;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;

        for (int i = 0; i < CardCount; i++)
        {
            cards.Add(
                BuildMapCard(
                    cardRow,
                    i,
                    cardSprite,
                    barSprite,
                    circleSprite
                )
            );
        }

        BuildArrow(cardArea, "PreviousButton", "<", false, cardSprite,
            arrows, arrowImages);

        BuildArrow(cardArea, "NextButton", ">", true, cardSprite,
            arrows, arrowImages);

        // =====================================================
        // 4. Точки пагинации
        // =====================================================
        GameObject dotTemplate =
            BuildDotTemplate(panelRect, circleSprite);

        // =====================================================
        // 5. Кнопка «Назад»
        // =====================================================
        Button backButton = BuildBackButton(panelRect, cardSprite);

        // =====================================================
        // 6. Компоненты панели
        // =====================================================
        MapSelectionUI selectionUI =
            panelObject.AddComponent<MapSelectionUI>();

        SerializedObject selectionSo =
            new SerializedObject(selectionUI);

        SerializedProperty slots =
            selectionSo.FindProperty("cardSlots");

        slots.arraySize = cards.Count;

        for (int i = 0; i < cards.Count; i++)
        {
            slots.GetArrayElementAtIndex(i).objectReferenceValue =
                cards[i];
        }

        selectionSo.FindProperty("previousButton").objectReferenceValue =
            arrows[0];

        selectionSo.FindProperty("previousImage").objectReferenceValue =
            arrowImages[0];

        selectionSo.FindProperty("nextButton").objectReferenceValue =
            arrows[1];

        selectionSo.FindProperty("nextImage").objectReferenceValue =
            arrowImages[1];

        selectionSo.FindProperty("dotsRoot").objectReferenceValue =
            dotTemplate.transform.parent as RectTransform;

        selectionSo.FindProperty("dotTemplate").objectReferenceValue =
            dotTemplate;

        selectionSo.ApplyModifiedProperties();

        SubPanelUI subPanel = panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);

        subSo.FindProperty("backButton").objectReferenceValue = backButton;

        subSo.ApplyModifiedProperties();

        // =====================================================
        // 7. Подключаем к главному меню
        // =====================================================
        menuSo.FindProperty("mapsButton").objectReferenceValue =
            mapsButton;

        menuSo.FindProperty("mapsPanel").objectReferenceValue =
            panelObject;

        menuSo.ApplyModifiedProperties();

        string mapsInfo = EnsureEnvironmentController();

        // =====================================================
        // 8. Финал
        // =====================================================
        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        EditorUtility.DisplayDialog(
            "Build Maps UI",
            "Панель «Карты» собрана.\n\n" +
            $"Карточек на странице: {CardCount}.\n\n" +
            mapsInfo,
            "OK"
        );
    }

    // =====================================================
    // HEADER
    // =====================================================

    private static void BuildHeader(RectTransform panelRect)
    {
        TextMeshProUGUI title = AddText(
            panelRect,
            "TitleText",
            "ВЫБОР КАРТЫ",
            44,
            TextAlignmentOptions.Left,
            FontStyles.Bold,
            MapSelectionTheme.TextPrimary
        );

        SetBlock(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(80f, -70f),
            new Vector2(900f, 64f),
            new Vector2(0f, 1f)
        );

        title.raycastTarget = false;

        TextMeshProUGUI subtitle = AddText(
            panelRect,
            "SubtitleText",
            "Мир уедет и приедет новый — арена та же, механики те же",
            24,
            TextAlignmentOptions.Left,
            FontStyles.Normal,
            MapSelectionTheme.TextSecondary
        );

        SetBlock(
            subtitle.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(82f, -140f),
            new Vector2(1100f, 36f),
            new Vector2(0f, 1f)
        );

        subtitle.raycastTarget = false;
    }

    // =====================================================
    // MAP CARD
    // =====================================================

    private static MapCardView BuildMapCard(
        RectTransform parent,
        int index,
        Sprite cardSprite,
        Sprite barSprite,
        Sprite circleSprite)
    {
        GameObject cardObject =
            new GameObject($"Card_{index + 1}", typeof(RectTransform));

        cardObject.transform.SetParent(parent, false);

        RectTransform cardRect = cardObject.GetComponent<RectTransform>();

        LayoutElement layout =
            cardObject.AddComponent<LayoutElement>();

        layout.preferredWidth = CardWidth;
        layout.preferredHeight = CardHeight;

        // Рамка-фон: цветная, под ней видны только рёбра 6px.
        Image frame = AddImage(
            cardRect,
            "Frame",
            MapSelectionTheme.FrameIdle,
            cardSprite,
            true
        );

        // Заливка «неба» с отступом — даёт рамку вокруг карточки.
        Image background = AddImage(
            cardRect,
            "Background",
            new Color(0.55f, 0.65f, 0.75f),
            cardSprite,
            true
        );

        background.rectTransform.offsetMin = new Vector2(6f, 6f);
        background.rectTransform.offsetMax = new Vector2(-6f, -6f);

        // Полоса земли снизу (цвет подложки карты).
        Image land = AddImage(
            cardRect,
            "LandStripe",
            new Color(0.5f, 0.5f, 0.5f),
            barSprite,
            true
        );

        land.rectTransform.anchorMin = new Vector2(0f, 0f);
        land.rectTransform.anchorMax = new Vector2(1f, 0f);
        land.rectTransform.offsetMin = new Vector2(6f, 6f);
        land.rectTransform.offsetMax = new Vector2(-6f, 104f);

        land.raycastTarget = false;

        // Диск-подложка иконки.
        Image iconDisc = AddImage(
            cardRect,
            "IconDisc",
            new Color(0.45f, 0.45f, 0.45f),
            circleSprite,
            false
        );

        SetBlock(
            iconDisc.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 20f),
            new Vector2(150f, 150f),
            new Vector2(0.5f, 0.5f)
        );

        iconDisc.raycastTarget = false;

        // Иконка карты (GameMap.icon), если есть.
        Image icon = AddImage(
            cardRect,
            "Icon",
            Color.white,
            null,
            false
        );

        SetBlock(
            icon.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 20f),
            new Vector2(110f, 110f),
            new Vector2(0.5f, 0.5f)
        );

        icon.raycastTarget = false;

        TextMeshProUGUI nameText = AddText(
            cardRect,
            "NameText",
            $"Карта {index + 1}",
            28,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            MapSelectionTheme.TextPrimary
        );

        SetBlock(
            nameText.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0f, 50f),
            new Vector2(280f, 44f),
            new Vector2(0.5f, 0f)
        );

        nameText.raycastTarget = false;

        // Чип «ВЫБРАНО».
        Image chip = AddImage(
            cardRect,
            "Selected",
            MapSelectionTheme.ChipBackground,
            barSprite,
            true
        );

        SetBlock(
            chip.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(18f, -16f),
            new Vector2(156f, 44f),
            new Vector2(0f, 1f)
        );

        chip.raycastTarget = false;

        TextMeshProUGUI chipLabel = AddText(
            chip.transform,
            "LabelText",
            "ВЫБРАНО",
            20,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            MapSelectionTheme.White(1f)
        );

        SetFullStretch(chipLabel.rectTransform);

        chipLabel.raycastTarget = false;

        chip.gameObject.SetActive(false);

        Button button = cardObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;

        MapCardView view = cardObject.AddComponent<MapCardView>();

        SerializedObject viewSo = new SerializedObject(view);

        viewSo.FindProperty("frameImage").objectReferenceValue = frame;
        viewSo.FindProperty("backgroundImage").objectReferenceValue = background;
        viewSo.FindProperty("landImage").objectReferenceValue = land;
        viewSo.FindProperty("iconDisc").objectReferenceValue = iconDisc;
        viewSo.FindProperty("iconImage").objectReferenceValue = icon;
        viewSo.FindProperty("nameText").objectReferenceValue = nameText;
        viewSo.FindProperty("selectedChip").objectReferenceValue =
            chip.rectTransform;
        viewSo.FindProperty("selectButton").objectReferenceValue = button;

        viewSo.ApplyModifiedProperties();

        return view;
    }

    // =====================================================
    // PAGINATION
    // =====================================================

    private static void BuildArrow(
        RectTransform parent,
        string name,
        string glyph,
        bool right,
        Sprite sprite,
        List<Button> buttons,
        List<Image> images)
    {
        Image image = AddImage(
            parent,
            name,
            MapSelectionTheme.Surface,
            sprite,
            true
        );

        RectTransform rect = image.rectTransform;

        SetBlock(
            rect,
            right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
            new Vector2(right ? -30f : 30f, 0f),
            new Vector2(64f, 64f),
            right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f)
        );

        Button button = image.gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        TextMeshProUGUI label = AddText(
            rect,
            "LabelText",
            glyph,
            34,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            MapSelectionTheme.TextSecondary
        );

        SetFullStretch(label.rectTransform);

        label.raycastTarget = false;

        buttons.Add(button);
        images.Add(image);
    }

    private static GameObject BuildDotTemplate(
        RectTransform parent,
        Sprite circleSprite)
    {
        RectTransform pagination = AddBlock(
            parent,
            "Pagination",
            new Vector2(0.5f, 0f),
            new Vector2(0f, 268f),
            new Vector2(600f, 26f),
            new Vector2(0.5f, 0.5f)
        );

        HorizontalLayoutGroup dotsLayout =
            pagination.gameObject.AddComponent<HorizontalLayoutGroup>();

        dotsLayout.spacing = 14f;
        dotsLayout.childAlignment = TextAnchor.MiddleCenter;
        dotsLayout.childControlWidth = true;
        dotsLayout.childForceExpandWidth = false;
        dotsLayout.childControlHeight = true;
        dotsLayout.childForceExpandHeight = false;

        Image dot = AddImage(
            pagination,
            "DotTemplate",
            MapSelectionTheme.DotInactive,
            circleSprite,
            true
        );

        LayoutElement dotLayout = dot.gameObject.AddComponent<LayoutElement>();

        dotLayout.preferredWidth = 12f;
        dotLayout.preferredHeight = 12f;

        Button dotButton = dot.gameObject.AddComponent<Button>();

        dotButton.transition = Selectable.Transition.None;
        dotButton.targetGraphic = dot;

        dot.gameObject.SetActive(false);

        return dot.gameObject;
    }

    // =====================================================
    // BOTTOM BUTTON
    // =====================================================

    private static Button BuildBackButton(
        RectTransform parent,
        Sprite cardSprite)
    {
        Image image = AddImage(
            parent,
            "BackButton",
            MapSelectionTheme.Surface,
            cardSprite,
            true
        );

        SetBlock(
            image.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(80f, 58f),
            new Vector2(220f, 84f),
            new Vector2(0f, 0f)
        );

        Button button = image.gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        TextMeshProUGUI label = AddText(
            image.transform,
            "LabelText",
            "НАЗАД",
            24,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            MapSelectionTheme.TextSecondary
        );

        SetFullStretch(label.rectTransform);

        label.raycastTarget = false;

        return button;
    }

    // =====================================================
    // ENVIRONMENT CONTROLLER
    // =====================================================

    /// <summary>
    /// Находит EnvironmentController в сцене, а если его нет — создаёт,
    /// и всегда перепривязывает заготовленные карты (Map_Les ... Map_Kosmos),
    /// чтобы повторы билда подхватывали обновлённые темы. Возвращает
    /// строку-отчёт для диалога.
    /// </summary>
    private static string EnsureEnvironmentController()
    {
        var demoMaps = LoadDemoMaps();

        if (demoMaps.Count == 0)
        {
            Debug.LogWarning(
                "[Maps] Демо-карты не созданы. " +
                "Запусти ArcadeSurvivor -> Create Demo Maps.");

            return
                "Демо-карты не найдены: запусти " +
                "ArcadeSurvivor -> Create Demo Maps и повтори.";
        }

        EnvironmentController environment =
            Object.FindAnyObjectByType<EnvironmentController>();

        if (environment == null)
        {
            GameObject environmentObject =
                new GameObject("EnvironmentManager");

            environmentObject.transform.position = Vector3.zero;

            Undo.RegisterCreatedObjectUndo(
                environmentObject,
                MenuPath
            );

            environment =
                environmentObject.AddComponent<EnvironmentController>();
        }

        // Карты перепривязываем всегда: темы и палитры могли
        // обновиться, и повторный билд должен их подтянуть.
        SerializedObject envSo = new SerializedObject(environment);

        SerializedProperty mapsProperty =
            envSo.FindProperty("maps");

        mapsProperty.arraySize = demoMaps.Count;

        for (int i = 0; i < demoMaps.Count; i++)
        {
            mapsProperty.GetArrayElementAtIndex(i).objectReferenceValue =
                demoMaps[i];
        }

        envSo.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(
            environment.gameObject.scene
        );

        return $"Карт назначено: {demoMaps.Count} на " +
               "EnvironmentManager.";
    }

    private static List<GameMap> LoadDemoMaps()
    {
        var maps = new List<GameMap>();

        for (int i = 0; i < DemoMapNames.Length; i++)
        {
            string path =
                $"{DemoMapsFolder}/{DemoMapNames[i]}.asset";

            GameMap map =
                AssetDatabase.LoadAssetAtPath<GameMap>(path);

            if (map != null)
                maps.Add(map);
        }

        return maps;
    }

    // =====================================================
    // MAIN MENU BUTTON
    // =====================================================

    /// <summary>
    /// Кнопка «Карты» обычно уже есть в сцене — используем её.
    /// Если нет — клонируем «Магазин», чтобы сохранить стиль меню.
    /// </summary>
    private static Button EnsureMapsButton(
        SerializedObject menuSo,
        Button shopButton)
    {
        Button existing =
            menuSo.FindProperty("mapsButton")
                .objectReferenceValue as Button;

        if (existing != null)
            return existing;

        if (shopButton == null)
            return null;

        GameObject buttonObject = Object.Instantiate(
            shopButton.gameObject,
            shopButton.transform.parent
        );

        buttonObject.name = "MapsButton";

        Undo.RegisterCreatedObjectUndo(buttonObject, MenuPath);

        TextMeshProUGUI label =
            buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
            label.text = "Карты";

        Button button = buttonObject.GetComponent<Button>();

        if (button == null)
            button = buttonObject.AddComponent<Button>();

        return button;
    }

    // =====================================================
    // CLEANUP
    // =====================================================

    private static void DeletePreviousPanel(
        Transform panelParent,
        SerializedObject menuSo)
    {
        menuSo.FindProperty("mapsPanel").objectReferenceValue = null;

        menuSo.ApplyModifiedProperties();

        if (panelParent == null)
            return;

        var children = new List<Transform>();

        foreach (Transform child in panelParent)
            children.Add(child);

        foreach (Transform child in children)
        {
            if (child.name == PanelName)
                Object.DestroyImmediate(child.gameObject);
        }
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private static RectTransform AddBlock(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Vector2 pivot)
    {
        RectTransform rect = AddRect(
            parent,
            name,
            anchor,
            anchor,
            Vector2.zero,
            Vector2.zero
        );

        SetBlock(rect, anchor, position, size, pivot);

        return rect;
    }

    private static void SetBlock(
        RectTransform rect,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Vector2 pivot)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static RectTransform AddStretch(
        Transform parent,
        string name,
        float left,
        float right,
        float bottom,
        float top)
    {
        RectTransform rect = AddRect(
            parent,
            name,
            Vector2.zero,
            Vector2.one,
            new Vector2(left, bottom),
            new Vector2(-right, -top)
        );

        return rect;
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

        RectTransform rect = rectObject.GetComponent<RectTransform>();

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;

        return rect;
    }

    private static Image AddImage(
        Transform parent,
        string name,
        Color color,
        Sprite sprite,
        bool sliced)
    {
        RectTransform rect = AddRect(
            parent,
            name,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );

        Image image = rect.gameObject.AddComponent<Image>();

        image.color = color;

        if (sprite != null)
        {
            image.sprite = sprite;
            image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
        }

        return image;
    }

    private static TextMeshProUGUI AddText(
        Transform parent,
        string name,
        string content,
        int size,
        TextAlignmentOptions alignment,
        FontStyles style,
        Color color)
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
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;

        return text;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}

#endif