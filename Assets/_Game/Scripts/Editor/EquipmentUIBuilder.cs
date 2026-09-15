#if UNITY_EDITOR

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Построитель панели «Снаряжение» в главном меню.
/// Пункт меню: Tools -> Bullet Rush -> Build Equipment UI.
///
/// За один клик:
///  - создаёт кнопку «Снаряжение» рядом с остальными кнопками меню;
///  - создаёт панель снаряжения (шапка + ScrollRect + один шаблон карточки
///    + кнопка «Назад»);
///  - подключает все ссылки на MainMenuUI / EquipmentUI / SubPanelUI.
/// </summary>
public static class EquipmentUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Equipment UI";

    private const float CardHeight = 250f;
    private const float BackButtonHeight = 60f;

    [MenuItem(MenuPath)]
    public static void Build()
    {
        MainMenuUI menu =
            Object.FindFirstObjectByType<MainMenuUI>();

        if (menu == null)
        {
            EditorUtility.DisplayDialog(
                "Build Equipment UI",
                "MainMenuUI не найден в сцене. Открой сцену Game и запусти пункт меню.",
                "OK"
            );

            return;
        }

        SerializedObject menuSo =
            new SerializedObject(menu);

        GameObject menuPanel =
            menuSo.FindProperty("menuPanel").objectReferenceValue as GameObject;

        Button shopButton =
            menuSo.FindProperty("shopButton").objectReferenceValue as Button;

        if (shopButton == null)
        {
            EditorUtility.DisplayDialog(
                "Build Equipment UI",
                "На главном меню не назначена кнопка shopButton. " +
                "Сначала назначь её в инспекторе MainMenuUI.",
                "OK"
            );

            return;
        }

        if (menuPanel == null)
        {
            EditorUtility.DisplayDialog(
                "Build Equipment UI",
                "menuPanel не назначен на MainMenuUI.",
                "OK"
            );

            return;
        }

        Transform panelParent = menuPanel.transform.parent;

        // =====================================================
        // 1. Кнопка «Снаряжение» (клонируем магазин, сохраняя стиль)
        // =====================================================
        Transform buttonsRoot = shopButton.transform.parent;

        GameObject equipButtonObject =
            Object.Instantiate(shopButton.gameObject, buttonsRoot);

        equipButtonObject.name = "EquipmentButton";

        Undo.RegisterCreatedObjectUndo(equipButtonObject, "Build Equipment UI");

        TextMeshProUGUI buttonLabel =
            equipButtonObject.GetComponentInChildren<TextMeshProUGUI>(true);

        if (buttonLabel != null)
            buttonLabel.text = "Снаряжение";

        Button equipButton = equipButtonObject.GetComponent<Button>();

        if (equipButton == null)
            equipButton = equipButtonObject.AddComponent<Button>();

        // =====================================================
        // 2. Панель (корень)
        // =====================================================
        GameObject panelObject =
            new GameObject("EquipmentPanel", typeof(RectTransform));

        panelObject.transform.SetParent(panelParent, false);

        RectTransform panelRect =
            panelObject.GetComponent<RectTransform>();

        SetFullStretch(panelRect);

        Image panelBg = panelObject.AddComponent<Image>();
        panelBg.color = new Color(0.07f, 0.07f, 0.11f, 0.97f);

        Undo.RegisterCreatedObjectUndo(panelObject, "Build Equipment UI");

        // =====================================================
        // 3. Шапка
        // =====================================================
        TextMeshProUGUI titleText = AddText(
            panelRect,
            "TitleText",
            "СНАРЯЖЕНИЕ",
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

        TextMeshProUGUI equippedText = AddText(
            panelRect,
            "EquippedText",
            "Снаряжение: —",
            22,
            TextAlignmentOptions.Center,
            FontStyles.Normal
        );

        equippedText.color = new Color(0.9f, 0.9f, 0.85f, 1f);

        SetAnchors(
            equippedText.rectTransform,
            0.5f, 1f,
            new Vector2(0f, -84f),
            new Vector2(600f, 40f)
        );

        // =====================================================
        // 4. Область прокрутки
        // =====================================================
        RectTransform scrollArea = AddRect(
            panelRect,
            "ScrollArea",
            Vector2.zero,
            Vector2.one,
            new Vector2(24f, 72f),        // снизу: над кнопкой «Назад»
            new Vector2(-24f, -132f)      // сверху: под шапкой
        );

        Image scrollBg = scrollArea.gameObject.AddComponent<Image>();
        scrollBg.color = new Color(0.12f, 0.12f, 0.17f, 0.95f);

        ScrollRect scrollRect =
            scrollArea.gameObject.AddComponent<ScrollRect>();

        // 4.1 Viewport
        RectTransform viewport = AddRect(
            scrollArea,
            "Viewport",
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );

        viewport.gameObject.AddComponent<RectMask2D>();

        // 4.2 Content + вертикальный список карточек
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

        // 4.3 Шаблон карточки
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

        cardLayout.preferredHeight = CardHeight;

        VerticalLayoutGroup cardRows =
            cardRect.gameObject.AddComponent<VerticalLayoutGroup>();

        cardRows.spacing = 4f;
        cardRows.childAlignment = TextAnchor.UpperLeft;
        cardRows.childControlWidth = true;
        cardRows.childForceExpandWidth = true;
        cardRows.childControlHeight = true;
        cardRows.childForceExpandHeight = false;
        cardRows.padding = new RectOffset(18, 18, 14, 14);

        AddCardText(cardRect, "Name", "X", 30, FontStyles.Bold);
        AddCardText(cardRect, "Type", "", 20, FontStyles.Normal);
        AddCardText(cardRect, "Stats", "", 18, FontStyles.Normal);
        AddCardText(cardRect, "Status", "", 20, FontStyles.Normal);

        Button actionButton = AddButton(cardRect, "ActionButton");

        RectTransform actionRect = actionButton.GetComponent<RectTransform>();
        actionRect.anchorMin = Vector2.zero;
        actionRect.anchorMax = Vector2.one;
        actionRect.offsetMin = Vector2.zero;
        actionRect.offsetMax = Vector2.zero;

        LayoutElement actionLayout =
            actionButton.gameObject.AddComponent<LayoutElement>();

        actionLayout.preferredHeight = 46f;

        TextMeshProUGUI actionText = AddText(
            actionButton.transform,
            "Text",
            "Купить",
            22,
            TextAlignmentOptions.Center,
            FontStyles.Bold
        );

        SetFullStretch(actionText.rectTransform);

        cardRect.gameObject.SetActive(false);

        // 4.4 Полоса прокрутки (вертикальная)
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

        // =====================================================
        // 5. Кнопка «Назад»
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
        // 6. Компоненты панели
        // =====================================================
        EquipmentUI equipmentUI =
            panelObject.AddComponent<EquipmentUI>();

        SerializedObject equipmentSo =
            new SerializedObject(equipmentUI);

        equipmentSo.FindProperty("equipmentPanel").objectReferenceValue =
            panelObject;
        equipmentSo.FindProperty("playerLevelText").objectReferenceValue =
            levelText;
        equipmentSo.FindProperty("playerCoinsText").objectReferenceValue =
            coinsText;
        equipmentSo.FindProperty("equippedText").objectReferenceValue =
            equippedText;
        equipmentSo.FindProperty("cardTemplate").objectReferenceValue =
            cardRect.gameObject;

        equipmentSo.ApplyModifiedProperties();

        SubPanelUI subPanel = panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);

        subSo.FindProperty("backButton").objectReferenceValue = backButton;

        subSo.ApplyModifiedProperties();

        // =====================================================
        // 7. Подключаем к главному меню
        // =====================================================
        menuSo.FindProperty("equipmentButton").objectReferenceValue =
            equipButton;
        menuSo.FindProperty("equipmentPanel").objectReferenceValue =
            panelObject;

        menuSo.ApplyModifiedProperties();

        // =====================================================
        // 8. Финал
        // =====================================================
        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        EditorUtility.DisplayDialog(
            "Build Equipment UI",
            "Панель «Снаряжение» создана и подключена.\n\n" +
            "1. Нажми Play — проверить панель.\n" +
            "2. Стилизация карточки CardTemplate и кнопок — на твоё усмотрение.\n" +
            "3. Цены и уровни разблокировки каждого оружия правятся в самих\n" +
            "   WeaponData-ассетах (поля Unlock Level / Price).",
            "OK"
        );
    }

    // =====================================================
    // HELPERS
    // =====================================================

    private static void AddCardText(
        RectTransform parent,
        string name,
        string content,
        int size,
        FontStyles style)
    {
        TextMeshProUGUI text = AddText(
            parent,
            name,
            content,
            size,
            TextAlignmentOptions.MidlineLeft,
            style
        );

        text.enableWordWrapping = true;
        text.color = new Color(0.92f, 0.92f, 0.92f, 1f);
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