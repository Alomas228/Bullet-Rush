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
///  - удаляет старую панель «Снаряжение» и кнопку «Снаряжение», если они есть;
///  - создаёт кнопку «Снаряжение» рядом с остальными кнопками меню;
///  - создаёт панель «Снаряжение» с тремя вкладками: Магазин, Способности, Одежда
///    (шапка + табы + три ScrollRect с шаблонами карточек + кнопка «Назад»);
///  - подключает все ссылки на MainMenuUI / EquipmentUI / SubPanelUI;
///  - добавляет PlayerEquipmentApplier на игрока (применяет снаряжение в забеге);
///  - автоматически регистрирует все AbilityData/ClothingData-ассеты проекта
///    в списках UpgradeManager (availableAbilities / availableClothing).
/// </summary>
public static class EquipmentUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Equipment UI";

    private const float WeaponCardHeight = 250f;
    private const float AbilityCardHeight = 210f;
    private const float ClothingCardHeight = 210f;
    private const float BackButtonHeight = 60f;

    [MenuItem(MenuPath)]
    public static void Build()
    {
        MainMenuUI menu =
            Object.FindAnyObjectByType<MainMenuUI>();

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
        Transform buttonsRoot = shopButton.transform.parent;

        // Старую панель и кнопку удаляем, чтобы не было дублей при пересборке.
        DeletePreviousEquipment(buttonsRoot, panelParent, menuSo);

        // =====================================================
        // 1. Кнопка «Снаряжение» (клонируем магазин, сохраняя стиль)
        // =====================================================
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
            "Оружие: —\nСпособности: —\nОбраз: —",
            18,
            TextAlignmentOptions.Center,
            FontStyles.Normal
        );

        equippedText.color = new Color(0.9f, 0.9f, 0.85f, 1f);
        equippedText.textWrappingMode = TextWrappingModes.NoWrap;

        SetAnchors(
            equippedText.rectTransform,
            0.5f, 1f,
            new Vector2(0f, -88f),
            new Vector2(640f, 60f)
        );

        // =====================================================
        // 4. Табы: Магазин / Способности / Одежда
        // =====================================================
        RectTransform tabBar = AddRect(
            panelRect,
            "TabBar",
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(24f, -224f),
            new Vector2(-24f, -160f)
        );

        HorizontalLayoutGroup tabLayout =
            tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();

        tabLayout.spacing = 10f;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandHeight = true;
        tabLayout.padding = new RectOffset(0, 0, 0, 0);

        string[] tabLabels = { "Магазин", "Способности", "Одежда" };

        Button[] tabButtons = new Button[tabLabels.Length];

        for (int i = 0; i < tabLabels.Length; i++)
        {
            tabButtons[i] = AddTabButton(tabBar, tabLabels[i], $"Tab{i}");
        }

        // =====================================================
        // 5. Области прокрутки для каждой вкладки
        // =====================================================
        string[] areaNames =
        {
            "ShopScrollArea",
            "AbilitiesScrollArea",
            "ClothingScrollArea"
        };

        float[] cardHeights =
        {
            WeaponCardHeight,
            AbilityCardHeight,
            ClothingCardHeight
        };

        RectTransform[] areaRoots = new RectTransform[areaNames.Length];
        GameObject[] cardTemplates = new GameObject[areaNames.Length];

        for (int i = 0; i < areaNames.Length; i++)
        {
            (RectTransform root, RectTransform _, GameObject card) =
                BuildTabScrollArea(
                    panelRect,
                    areaNames[i],
                    cardHeights[i]
                );

            areaRoots[i] = root;
            cardTemplates[i] = card;
        }

        // =====================================================
        // 6. Кнопка «Назад»
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
        // 7. Компоненты панели
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

        SetObjectArray(equipmentSo, "tabButtons", tabButtons);
        SetObjectArray(equipmentSo, "tabContentRoots", areaRoots);
        SetObjectArray(equipmentSo, "cardTemplates", cardTemplates);

        equipmentSo.ApplyModifiedProperties();

        SubPanelUI subPanel = panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);

        subSo.FindProperty("backButton").objectReferenceValue = backButton;

        subSo.ApplyModifiedProperties();

        // =====================================================
        // 8. Подключаем к главному меню, игроку и UpgradeManager
        // =====================================================
        menuSo.FindProperty("equipmentButton").objectReferenceValue =
            equipButton;
        menuSo.FindProperty("equipmentPanel").objectReferenceValue =
            panelObject;

        menuSo.ApplyModifiedProperties();

        EnsurePlayerEquipmentApplier();

        UpgradeManager upgradeManager =
            Object.FindAnyObjectByType<UpgradeManager>();

        if (upgradeManager != null)
        {
            RegisterStoreAssets<AbilityData>(
                upgradeManager,
                "availableAbilities",
                "t:AbilityData"
            );

            RegisterStoreAssets<ClothingData>(
                upgradeManager,
                "availableClothing",
                "t:ClothingData"
            );
        }

        // =====================================================
        // 9. Финал
        // =====================================================
        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        EditorUtility.DisplayDialog(
            "Build Equipment UI",
            "Панель «Снаряжение» создана и подключена.\n\n" +
            "Вкладки: Магазин (оружие), Способности, Одежда.\n\n" +
            "Как добавить предметы:\n" +
            "1. Assets -> Create -> Arcade Survivor -> Ability / Clothing.\n" +
            "2. Заполни имя, цену, уровень разблокировки и хар-ки.\n" +
            "3. Запусти сборку снова — ассеты автоматически попадут\n" +
            "   в UpgradeManager (availableAbilities / availableClothing).\n\n" +
            "Снаряжение применяется на старте забега (PlayerEquipmentApplier " +
            "добавлен на игрока). Стилизация карточек — на твоё усмотрение.",
            "OK"
        );
    }

    // =====================================================
    // CLEANUP
    // =====================================================

    private static void DeletePreviousEquipment(
        Transform buttonsRoot,
        Transform panelParent,
        SerializedObject menuSo)
    {
        // Сначала разрываем старые ссылки на главном меню.
        menuSo.FindProperty("equipmentButton").objectReferenceValue = null;
        menuSo.FindProperty("equipmentPanel").objectReferenceValue = null;

        menuSo.ApplyModifiedProperties();

        if (buttonsRoot != null)
        {
            var children =
                new System.Collections.Generic.List<Transform>();

            foreach (Transform child in buttonsRoot)
                children.Add(child);

            foreach (Transform child in children)
            {
                if (child.name == "EquipmentButton")
                    Object.DestroyImmediate(child.gameObject);
            }
        }

        if (panelParent != null)
        {
            var children =
                new System.Collections.Generic.List<Transform>();

            foreach (Transform child in panelParent)
                children.Add(child);

            foreach (Transform child in children)
            {
                if (child.name == "EquipmentPanel")
                    Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    // =====================================================
    // PLAYER / UPGRADE MANAGER
    // =====================================================

    private static bool EnsurePlayerEquipmentApplier()
    {
        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player == null)
            return false;

        if (player.GetComponent<PlayerEquipmentApplier>() == null)
        {
            player.AddComponent<PlayerEquipmentApplier>();

            EditorUtility.SetDirty(player);
            EditorSceneManager.MarkSceneDirty(player.scene);
        }

        return true;
    }

    private static void RegisterStoreAssets<T>(
        UpgradeManager manager,
        string fieldName,
        string assetFilter) where T : ScriptableObject
    {
        if (manager == null)
            return;

        SerializedObject managerSo =
            new SerializedObject(manager);

        SerializedProperty list =
            managerSo.FindProperty(fieldName);

        if (list == null)
            return;

        string[] guids =
            AssetDatabase.FindAssets(assetFilter);

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            T asset =
                AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
                continue;

            bool alreadyPresent = false;

            for (int i = 0; i < list.arraySize; i++)
            {
                Object existing =
                    list.GetArrayElementAtIndex(i).objectReferenceValue;

                if (existing == asset)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (alreadyPresent)
                continue;

            list.InsertArrayElementAtIndex(list.arraySize);
            list.GetArrayElementAtIndex(list.arraySize - 1)
                .objectReferenceValue = asset;
        }

        managerSo.ApplyModifiedProperties();

        EditorUtility.SetDirty(manager);
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
    }

    // =====================================================
    // TAB AREA CONSTRUCTION
    // =====================================================

    private static Button AddTabButton(
        Transform parent,
        string label,
        string objectName)
    {
        RectTransform rect = AddRect(
            parent,
            objectName,
            new Vector2(0f, 0f),
            new Vector2(1f, 1f),
            Vector2.zero,
            Vector2.zero
        );

        Image image = rect.gameObject.AddComponent<Image>();
        image.color = new Color(0.16f, 0.16f, 0.22f, 0.9f);

        LayoutElement layout =
            rect.gameObject.AddComponent<LayoutElement>();

        layout.flexibleWidth = 1f;
        layout.flexibleHeight = 1f;

        Button button = rect.gameObject.AddComponent<Button>();

        TextMeshProUGUI text = AddText(
            rect,
            "Text",
            label,
            24,
            TextAlignmentOptions.Center,
            FontStyles.Bold
        );

        SetFullStretch(text.rectTransform);

        return button;
    }

    private static (
        RectTransform root,
        RectTransform content,
        GameObject card
    ) BuildTabScrollArea(
        RectTransform panelRect,
        string areaName,
        float cardHeight)
    {
        RectTransform scrollArea = AddRect(
            panelRect,
            areaName,
            Vector2.zero,
            Vector2.one,
            new Vector2(24f, 84f),        // снизу: над кнопкой «Назад»
            new Vector2(-24f, -232f)      // сверху: под табами и шапкой
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

        // 4 Полоса прокрутки (вертикальная)
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

        return cardRect;
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

        text.textWrappingMode = TextWrappingModes.Normal;
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

    private static void SetObjectArray(
        SerializedObject so,
        string propertyName,
        Object[] values)
    {
        SerializedProperty property =
            so.FindProperty(propertyName);

        if (property == null)
            return;

        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i)
                .objectReferenceValue = values[i];
        }
    }
}

#endif