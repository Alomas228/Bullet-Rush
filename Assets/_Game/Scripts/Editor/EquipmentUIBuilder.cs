#if UNITY_EDITOR

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Построитель экрана «Снаряжение» в главном меню.
/// Пункт меню: Tools -> Bullet Rush -> Build Equipment UI.
///
/// Тот же визуальный язык, что и на «Улучшениях» (светлая палитра,
/// белые карточки с тонкой рамкой, 9-slice заглушки, отсутствие
/// неона), но своя композиция:
///  - шапка: «СНАРЯЖЕНИЕ» слева, монеты и уровень справа;
///  - три вкладки Оружие / Способности / Одежда, на каждой под
///    названием написано, что сейчас надето;
///  - горизонтальный ряд из четырёх карточек со стрелками;
///  - точки пагинации и счётчик страниц, «Назад» слева снизу.
///
/// Вертикальной прокрутки нет: предметы листаются постранично.
///
/// За один клик:
///  - удаляет старую панель «Снаряжение» и кнопку «Снаряжение»;
///  - создаёт кнопку «Снаряжение» рядом с остальными кнопками меню;
///  - создаёт панель и подключает все ссылки на
///    MainMenuUI / EquipmentUI / SubPanelUI;
///  - добавляет PlayerEquipmentApplier на игрока;
///  - регистрирует ассеты AbilityData / ClothingData в UpgradeManager,
///    чтобы они попали в списки магазина.
/// </summary>
public static class EquipmentUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Equipment UI";

    private const string LayoutMenuPath =
        "Tools/Bullet Rush/Equipment Layout Settings";

    private const string PanelName = "EquipmentPanel";
    private const string ButtonName = "EquipmentButton";

    /// <summary>
    /// Открывает ассет размеров. Создаёт его, если ещё нет.
    /// </summary>
    [MenuItem(LayoutMenuPath)]
    public static void OpenLayoutSettings()
    {
        EquipmentLayoutSettings layout = EquipmentLayoutSettings.Load();

        AssetDatabase.SaveAssets();

        Selection.activeObject = layout;

        EditorGUIUtility.PingObject(layout);
    }

    [MenuItem(MenuPath)]
    public static void Build()
    {
        EquipmentLayoutSettings layout = EquipmentLayoutSettings.Load();

        MainMenuUI menu =
            Object.FindAnyObjectByType<MainMenuUI>();

        if (menu == null)
        {
            EditorUtility.DisplayDialog(
                "Build Equipment UI",
                "MainMenuUI не найден в сцене. " +
                "Открой сцену Game и запусти пункт меню.",
                "OK"
            );

            return;
        }

        SerializedObject menuSo = new SerializedObject(menu);

        GameObject menuPanel =
            menuSo.FindProperty("menuPanel")
                .objectReferenceValue as GameObject;

        if (menuPanel == null)
        {
            EditorUtility.DisplayDialog(
                "Build Equipment UI",
                "menuPanel не назначен на MainMenuUI.",
                "OK"
            );

            return;
        }

        Button shopButton =
            menuSo.FindProperty("shopButton")
                .objectReferenceValue as Button;

        Transform panelParent = menuPanel.transform.parent;

        if (panelParent == null)
            panelParent = menuPanel.transform;

        Transform buttonsRoot = shopButton != null
            ? shopButton.transform.parent
            : null;

        // =====================================================
        // 0. Чистим старое, готовим кнопку «Снаряжение»
        // =====================================================
        DeletePrevious(buttonsRoot, panelParent, menuSo);

        Button equipmentButton =
            EnsureEquipmentButton(menuSo, shopButton);

        if (equipmentButton == null)
        {
            EditorUtility.DisplayDialog(
                "Build Equipment UI",
                "Не найдена кнопка «Снаряжение» в меню, а клонировать её " +
                "некому: не назначена кнопка shopButton.\n\n" +
                "Назначь shopButton в инспекторе MainMenuUI и запусти " +
                "пункт меню ещё раз.",
                "OK"
            );

            return;
        }

        // Заглушки-формы нужны и фону, и карточкам.
        WireframePlaceholderSprites.EnsureAll();

        Sprite cardSprite = WireframePlaceholderSprites.CardSprite;
        Sprite barSprite = WireframePlaceholderSprites.BarSprite;
        Sprite circleSprite = WireframePlaceholderSprites.CircleSprite;
        Sprite blobSprite = WireframePlaceholderSprites.BlobSprite;

        // =====================================================
        // 1. Панель (корень)
        // =====================================================
        GameObject panelObject =
            new GameObject(PanelName, typeof(RectTransform));

        panelObject.transform.SetParent(panelParent, false);

        RectTransform panelRect =
            panelObject.GetComponent<RectTransform>();

        SetFullStretch(panelRect);

        panelRect.gameObject.AddComponent<Image>().color =
            EquipmentWireframeTheme.PanelBackground;

        Undo.RegisterCreatedObjectUndo(panelObject, MenuPath);

        // =====================================================
        // 2. Фон
        // =====================================================
        BuildBackground(panelRect, barSprite, blobSprite);

        // =====================================================
        // 3. Шапка
        // =====================================================
        (TMP_Text coins, TMP_Text level) =
            BuildTopBar(panelRect, cardSprite, layout);

        // =====================================================
        // 4. Вкладки
        // =====================================================
        (List<Button> tabButtons, List<TMP_Text> tabValues) =
            BuildTabBar(panelRect, cardSprite, layout);

        // =====================================================
        // 5. Ряд карточек со стрелками
        // =====================================================
        var cards = new List<EquipmentCardView>();
        var arrows = new List<Button>();
        var arrowImages = new List<Image>();

        RectTransform cardArea = AddBand(
            panelRect,
            "CardArea",
            layout.cardAreaTop,
            layout.cardAreaBottom
        );

        RectTransform cardRow = AddStretch(
            cardArea,
            "CardRow",
            layout.rowPadding,
            layout.rowPadding,
            0f,
            0f
        );

        HorizontalLayoutGroup rowLayout =
            cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();

        rowLayout.spacing = layout.cardGap;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandHeight = false;

        for (int i = 0; i < layout.cardsPerPage; i++)
        {
            cards.Add(
                BuildCard(
                    cardRow,
                    i,
                    cardSprite,
                    barSprite,
                    circleSprite,
                    layout
                )
            );
        }

        BuildArrow(cardArea, "PreviousButton", "<", false, cardSprite,
            arrows, arrowImages);

        BuildArrow(cardArea, "NextButton", ">", true, cardSprite,
            arrows, arrowImages);

        // Пустая категория: заглушка на месте карточек.
        (GameObject emptyState, TMP_Text emptyStateText) =
            BuildEmptyState(cardArea, cardSprite);

        // =====================================================
        // 6. Пагинация и счётчик страниц
        // =====================================================
        GameObject dotTemplate =
            BuildDotTemplate(panelRect, circleSprite);

        TMP_Text pageCounter = BuildPageCounter(panelRect, cardSprite);

        // =====================================================
        // 7. Кнопка «Назад»
        // =====================================================
        Button backButton = BuildBackButton(panelRect, cardSprite);

        // =====================================================
        // 8. Компоненты панели
        // =====================================================
        EquipmentUI equipmentUI =
            panelObject.AddComponent<EquipmentUI>();

        SerializedObject equipmentSo =
            new SerializedObject(equipmentUI);

        equipmentSo.FindProperty("coinsText").objectReferenceValue =
            coins;

        equipmentSo.FindProperty("levelText").objectReferenceValue =
            level;

        SetObjectArray(equipmentSo, "tabButtons", TabObjects(tabButtons));

        SetObjectArray(
            equipmentSo,
            "tabValueTexts",
            TextObjects(tabValues)
        );

        SetObjectArray(equipmentSo, "cardSlots", CardObjects(cards));

        equipmentSo.FindProperty("previousButton").objectReferenceValue =
            arrows[0];

        equipmentSo.FindProperty("previousImage").objectReferenceValue =
            arrowImages[0];

        equipmentSo.FindProperty("nextButton").objectReferenceValue =
            arrows[1];

        equipmentSo.FindProperty("nextImage").objectReferenceValue =
            arrowImages[1];

        equipmentSo.FindProperty("dotsRoot").objectReferenceValue =
            dotTemplate.transform.parent as RectTransform;

        equipmentSo.FindProperty("dotTemplate").objectReferenceValue =
            dotTemplate;

        equipmentSo.FindProperty("pageCounterText").objectReferenceValue =
            pageCounter;

        equipmentSo.FindProperty("emptyState").objectReferenceValue =
            emptyState;

        equipmentSo.FindProperty("emptyStateText").objectReferenceValue =
            emptyStateText;

        // Размеры приходят из ассета, поэтому число карточек на
        // странице тоже нужно записать в рантайм — пагинация
        // считает страницы по нему.
        equipmentSo.FindProperty("cardsPerPage").intValue =
            layout.cardsPerPage;

        equipmentSo.ApplyModifiedProperties();

        SubPanelUI subPanel = panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);

        subSo.FindProperty("backButton").objectReferenceValue = backButton;

        subSo.ApplyModifiedProperties();

        // =====================================================
        // 9. Подключаем к меню, игроку и UpgradeManager
        // =====================================================
        menuSo.FindProperty("equipmentButton").objectReferenceValue =
            equipmentButton;

        menuSo.FindProperty("equipmentPanel").objectReferenceValue =
            panelObject;

        menuSo.ApplyModifiedProperties();

        EnsurePlayerEquipmentApplier();
        RegisterStoreAssets();

        // =====================================================
        // 10. Финал
        // =====================================================
        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        EditorUtility.DisplayDialog(
            "Build Equipment UI",
            "Панель «Снаряжение» собрана и подключена.\n\n" +
            $"Карточек на странице: {layout.cardsPerPage}. Страниц: " +
            $"{PageCount(CountStoreItems(), layout.cardsPerPage)}.\n" +
            $"Карточка: {layout.cardWidth:0}x{layout.cardHeight:0}, " +
            $"зазор {layout.cardGap:0}.\n\n" +
            "Вкладки: Оружие, Способности, Одежда. Под названием " +
            "вкладки показано, что сейчас надето.\n\n" +
            "Как поменять размеры:\n" +
            "Tools -> Bullet Rush -> Equipment Layout Settings, " +
            "затем пересобрать экран.\n\n" +
            "Как добавить предметы:\n" +
            "1. Assets -> Create -> Arcade Survivor -> Ability / Clothing.\n" +
            "2. Заполни имя, цену, уровень разблокировки и хар-ки.\n" +
            "3. Запусти сборку снова — ассеты автоматически попадут " +
            "в UpgradeManager.\n\n" +
            "Все цвета — в EquipmentWireframeTheme, формы — в папке " +
            "Assets/_Game/UI/Placeholder. В Figma меняешь их местами.",
            "OK"
        );
    }

    // =====================================================
    // ФОН
    // =====================================================

    private static void BuildBackground(
        RectTransform panelRect,
        Sprite barSprite,
        Sprite blobSprite)
    {
        RectTransform decor = AddStretch(
            panelRect,
            "BackgroundShapes",
            0f,
            0f,
            0f,
            0f
        );

        AddDecorativeImage(
            decor,
            "BlobLeft",
            blobSprite,
            EquipmentWireframeTheme.Wash(
                EquipmentWireframeTheme.DecorWash,
                0.5f
            ),
            new Vector2(0.14f, 0.8f),
            new Vector2(-360f, 30f),
            700f,
            700f,
            -10f
        );

        AddDecorativeImage(
            decor,
            "BlobRight",
            blobSprite,
            EquipmentWireframeTheme.Wash(
                EquipmentWireframeTheme.DecorWash,
                0.42f
            ),
            new Vector2(0.9f, 0.14f),
            new Vector2(300f, -20f),
            620f,
            620f,
            12f
        );

        AddDecorativeImage(
            decor,
            "BandTop",
            barSprite,
            EquipmentWireframeTheme.Wash(
                EquipmentWireframeTheme.PanelWash,
                0.9f
            ),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            1500f,
            16f
        );

        AddDecorativeImage(
            decor,
            "BandBottom",
            barSprite,
            EquipmentWireframeTheme.Wash(
                EquipmentWireframeTheme.PanelWash,
                0.7f
            ),
            new Vector2(0.5f, 0f),
            Vector2.zero,
            1100f,
            12f
        );
    }

    private static void AddDecorativeImage(
        Transform parent,
        string name,
        Sprite sprite,
        Color color,
        Vector2 anchor,
        Vector2 position,
        float width,
        float height,
        float rotation = 0f)
    {
        Image image = AddImage(
            parent,
            name,
            color,
            sprite,
            sprite != null && rotation == 0f
        );

        RectTransform rect = image.rectTransform;

        SetBlock(
            rect,
            anchor,
            position,
            new Vector2(width, height),
            anchor
        );

        rect.localRotation = Quaternion.Euler(0f, 0f, rotation);

        image.raycastTarget = false;
    }

    // =====================================================
    // ШАПКА
    // =====================================================

    private static (TMP_Text coins, TMP_Text level) BuildTopBar(
        RectTransform panelRect,
        Sprite cardSprite,
        EquipmentLayoutSettings layout)
    {
        RectTransform topBar = AddBand(
            panelRect,
            "TopBar",
            0f,
            0f
        );

        topBar.offsetMin = new Vector2(0f, -layout.topBarHeight);
        topBar.offsetMax = new Vector2(0f, 0f);

        TextMeshProUGUI title = AddText(
            topBar,
            "TitleText",
            "СНАРЯЖЕНИЕ",
            52,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold,
            EquipmentWireframeTheme.TextPrimary
        );

        SetBlock(
            title.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(80f, 0f),
            new Vector2(640f, 70f),
            new Vector2(0f, 0.5f)
        );

        TextMeshProUGUI subtitle = AddText(
            topBar,
            "SubtitleText",
            "Собери боевой набор",
            26,
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            EquipmentWireframeTheme.TextSecondary
        );

        SetBlock(
            subtitle.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 4f),
            new Vector2(520f, 40f),
            new Vector2(0.5f, 0.5f)
        );

        // Уровень игрока нужен, потому что предметы открываются
        // по уровню, но показан компактно рядом с монетами.
        RectTransform levelChip = AddBlock(
            topBar,
            "LevelChip",
            new Vector2(1f, 0.5f),
            new Vector2(-396f, 0f),
            new Vector2(150f, 78f),
            new Vector2(1f, 0.5f)
        );

        AddImage(
            levelChip,
            "Surface",
            EquipmentWireframeTheme.Surface,
            cardSprite,
            true
        ).raycastTarget = false;

        TextMeshProUGUI level = AddText(
            levelChip,
            "LevelText",
            "УР. 1",
            26,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            EquipmentWireframeTheme.TextSecondary
        );

        SetFullStretch(level.rectTransform);

        level.raycastTarget = false;

        RectTransform coinsBlock = AddBlock(
            topBar,
            "CoinsBlock",
            new Vector2(1f, 0.5f),
            new Vector2(-80f, 0f),
            new Vector2(300f, 78f),
            new Vector2(1f, 0.5f)
        );

        AddImage(
            coinsBlock,
            "CoinsSurface",
            EquipmentWireframeTheme.Surface,
            cardSprite,
            true
        ).raycastTarget = false;

        Image coinIcon = AddImage(
            coinsBlock,
            "CoinIcon",
            EquipmentWireframeTheme.RarityCommon,
            null,
            false
        );

        SetBlock(
            coinIcon.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(22f, 0f),
            new Vector2(30f, 30f),
            new Vector2(0f, 0.5f)
        );

        coinIcon.raycastTarget = false;

        TextMeshProUGUI coins = AddText(
            coinsBlock,
            "CoinsText",
            "48",
            34,
            TextAlignmentOptions.MidlineRight,
            FontStyles.Bold,
            EquipmentWireframeTheme.TextPrimary
        );

        SetBlock(
            coins.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(-22f, 0f),
            new Vector2(200f, 44f),
            new Vector2(1f, 0.5f)
        );

        coins.raycastTarget = false;

        return (coins, level);
    }

    // =====================================================
    // ВКЛАДКИ
    // =====================================================

    private static (List<Button> buttons, List<TMP_Text> values)
        BuildTabBar(
            RectTransform panelRect,
            Sprite cardSprite,
            EquipmentLayoutSettings layout)
    {
        RectTransform tabBar = AddBlock(
            panelRect,
            "TabBar",
            new Vector2(0.5f, 1f),
            new Vector2(0f, -layout.topBarHeight),
            new Vector2(1360f, layout.tabBarHeight),
            new Vector2(0.5f, 1f)
        );
        HorizontalLayoutGroup tabLayout =
            tabBar.gameObject.AddComponent<HorizontalLayoutGroup>();

        tabLayout.spacing = 16f;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandHeight = true;

        string[] labels = { "ОРУЖИЕ", "СПОСОБНОСТИ", "ОДЕЖДА" };

        var buttons = new List<Button>();
        var values = new List<TMP_Text>();

        for (int i = 0; i < labels.Length; i++)
        {
            Image surface = AddImage(
                tabBar,
                $"Tab{i + 1}",
                i == 0
                    ? EquipmentWireframeTheme.Surface
                    : EquipmentWireframeTheme.SurfaceMuted,
                cardSprite,
                true
            );

            Button button = surface.gameObject.AddComponent<Button>();

            button.transition = Selectable.Transition.None;
            button.targetGraphic = surface;

            TextMeshProUGUI label = AddText(
                surface.transform,
                "LabelText",
                labels[i],
                24,
                TextAlignmentOptions.Center,
                FontStyles.Bold,
                i == 0
                    ? EquipmentWireframeTheme.TextPrimary
                    : EquipmentWireframeTheme.TextSecondary
            );

            SetBlock(
                label.rectTransform,
                new Vector2(0.5f, 1f),
                new Vector2(0f, -12f),
                new Vector2(400f, 30f),
                new Vector2(0.5f, 1f)
            );

            label.raycastTarget = false;

            // Под вкладкой — то, что сейчас надето. Это
            // заменяет отдельную строку «снаряжено» в шапке.
            TextMeshProUGUI value = AddText(
                surface.transform,
                "ValueText",
                "не выбрано",
                18,
                TextAlignmentOptions.Top,
                FontStyles.Normal,
                EquipmentWireframeTheme.TextMuted
            );

            SetBlock(
                value.rectTransform,
                new Vector2(0.5f, 0f),
                new Vector2(0f, 10f),
                new Vector2(400f, 40f),
                new Vector2(0.5f, 0f)
            );

            value.textWrappingMode = TextWrappingModes.Normal;
            value.overflowMode = TextOverflowModes.Ellipsis;
            value.raycastTarget = false;

            buttons.Add(button);
            values.Add(value);
        }

        return (buttons, values);
    }

    // =====================================================
    // КАРТОЧКА
    // =====================================================

    private static EquipmentCardView BuildCard(
        RectTransform parent,
        int index,
        Sprite cardSprite,
        Sprite barSprite,
        Sprite circleSprite,
        EquipmentLayoutSettings layout)
    {
        GameObject cardObject =
            new GameObject($"Card_{index + 1}", typeof(RectTransform));

        cardObject.transform.SetParent(parent, false);

        RectTransform card = cardObject.GetComponent<RectTransform>();

        LayoutElement cardLayout =
            cardObject.AddComponent<LayoutElement>();

        cardLayout.preferredWidth = layout.cardWidth;
        cardLayout.preferredHeight = layout.cardHeight;

        EquipmentCardView view =
            cardObject.AddComponent<EquipmentCardView>();

        // --- тень ---
        Image shadow = AddImage(
            card,
            "Shadow",
            EquipmentWireframeTheme.CardShadow,
            cardSprite,
            true
        );

        SetBlock(
            shadow.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -6f),
            new Vector2(layout.cardWidth, layout.cardHeight),
            new Vector2(0.5f, 0.5f)
        );

        shadow.raycastTarget = false;

        // --- рамка ---
        Image frame = AddImage(
            card,
            "Frame",
            EquipmentWireframeTheme.Track,
            cardSprite,
            true
        );

        SetBlock(
            frame.rectTransform,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(layout.cardWidth, layout.cardHeight),
            new Vector2(0.5f, 0.5f)
        );

        frame.raycastTarget = false;

        // --- лицо: вся площадь карточки кликабельна ---
        Image surface = AddImage(
            card,
            "Surface",
            EquipmentWireframeTheme.Surface,
            cardSprite,
            true
        );

        SetFullStretch(surface.rectTransform);

        Button cardButton = cardObject.AddComponent<Button>();

        cardButton.transition = Selectable.Transition.None;
        cardButton.targetGraphic = surface;

        // --- акцентная полоса сверху: цвет редкости ---
        Image accentLine = AddImage(
            card,
            "AccentLine",
            EquipmentWireframeTheme.RarityCommon,
            barSprite,
            true
        );

        SetBlock(
            accentLine.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -10f),
            new Vector2(
                Mathf.Max(8f, layout.cardWidth - layout.accentLineInset),
                6f
            ),
            new Vector2(0.5f, 1f)
        );

        accentLine.raycastTarget = false;

        // --- содержимое карточки ---
        RectTransform face = AddStretch(
            card,
            "CardFace",
            layout.frameInset,
            layout.frameInset,
            layout.frameInset,
            layout.frameInset
        );

        VerticalLayoutGroup faceLayout =
            face.gameObject.AddComponent<VerticalLayoutGroup>();

        faceLayout.spacing = layout.rowSpacing;
        faceLayout.childAlignment = TextAnchor.UpperCenter;
        faceLayout.childControlWidth = true;
        faceLayout.childForceExpandWidth = true;
        faceLayout.childControlHeight = true;
        faceLayout.childForceExpandHeight = false;
        faceLayout.padding = new RectOffset(
            Mathf.RoundToInt(layout.facePaddingLeft),
            Mathf.RoundToInt(layout.facePaddingRight),
            Mathf.RoundToInt(layout.facePaddingTop),
            Mathf.RoundToInt(layout.facePaddingBottom)
        );

        Image iconWash = BuildIconArea(
            face,
            circleSprite,
            barSprite,
            layout,
            out Image iconDisc,
            out TMP_Text typeText
        );

        TextMeshProUGUI nameText = AddCardText(
            face,
            "NameText",
            "Предмет",
            26,
            FontStyles.Bold,
            EquipmentWireframeTheme.TextPrimary
        );

        AddFixedHeight(nameText.gameObject, layout.nameHeight);

        RectTransform rarityRow = BuildRarityRow(
            face,
            layout,
            out TMP_Text rarityText
        );

        TextMeshProUGUI statsText = AddCardText(
            face,
            "StatsText",
            "Урон: 5\nТемп: 3/с",
            18,
            FontStyles.Normal,
            EquipmentWireframeTheme.TextSecondary
        );

        statsText.textWrappingMode = TextWrappingModes.Normal;
        statsText.overflowMode = TextOverflowModes.Ellipsis;

        AddFixedHeight(statsText.gameObject, layout.statsHeight);

        // --- плашка состояния ---
        (Image stateImage, TMP_Text stateText) =
            BuildStateRow(face, cardSprite, layout);

        // --- строка действия ---
        (
            Image actionImage,
            TMP_Text actionLabel,
            Button actionButton,
            Image priceImage,
            TMP_Text priceText
        ) = BuildActionRow(face, cardSprite, barSprite, layout);

        SetView(
            view,
            surface,
            frame,
            accentLine,
            iconWash,
            iconDisc,
            typeText,
            nameText,
            rarityText,
            rarityRow,
            statsText,
            stateImage,
            stateText,
            actionImage,
            actionLabel,
            actionButton,
            priceImage,
            priceText,
            cardButton
        );

        return view;
    }

    /// <summary>
    /// Место под иконку предмета: подложка акцентного цвета,
    /// круг-заглушка и подпись типа снизу.
    /// </summary>
    private static Image BuildIconArea(
        RectTransform parent,
        Sprite circleSprite,
        Sprite barSprite,
        EquipmentLayoutSettings layout,
        out Image disc,
        out TMP_Text typeText)
    {
        Image wash = AddImage(
            parent,
            "IconArea",
            EquipmentWireframeTheme.Wash(
                EquipmentWireframeTheme.RarityCommon,
                0.16f
            ),
            barSprite,
            true
        );

        AddFixedHeight(wash.gameObject, layout.iconAreaHeight, 1f);

        disc = AddImage(
            wash.transform,
            "IconDisc",
            EquipmentWireframeTheme.Wash(
                EquipmentWireframeTheme.RarityCommon,
                0.55f
            ),
            circleSprite,
            true
        );

        SetBlock(
            disc.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 8f),
            new Vector2(layout.iconDiscSize, layout.iconDiscSize),
            new Vector2(0.5f, 0.5f)
        );

        disc.raycastTarget = false;

        typeText = AddText(
            wash.transform,
            "TypeText",
            "Винтовка",
            17,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold,
            EquipmentWireframeTheme.RarityCommon
        );

        SetBlock(
            typeText.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(12f, 8f),
            new Vector2(
                Mathf.Max(40f, layout.cardWidth - 36f),
                layout.typeTextHeight
            ),
            new Vector2(0f, 0f)
        );

        typeText.raycastTarget = false;

        return wash;
    }

    /// <summary>
    /// Строка редкости. У одежды и способностей её нет, поэтому
    /// ряд выключается — вертикальный список не оставляет дырки.
    /// </summary>
    private static RectTransform BuildRarityRow(
        RectTransform parent,
        EquipmentLayoutSettings layout,
        out TMP_Text rarityText)
    {
        RectTransform row = AddBlock(
            parent,
            "RarityRow",
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, layout.rarityHeight),
            new Vector2(0.5f, 0.5f)
        );

        row.gameObject.AddComponent<LayoutElement>().preferredHeight =
            layout.rarityHeight;

        rarityText = AddText(
            row,
            "RarityText",
            "Обычное",
            17,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold,
            EquipmentWireframeTheme.RarityCommon
        );

        SetFullStretch(rarityText.rectTransform);

        rarityText.raycastTarget = false;

        return row;
    }

    private static (Image image, TMP_Text text) BuildStateRow(
        RectTransform parent,
        Sprite cardSprite,
        EquipmentLayoutSettings layout)
    {
        Image image = AddImage(
            parent,
            "StateRow",
            EquipmentWireframeTheme.Track,
            cardSprite,
            true
        );

        AddFixedHeight(image.gameObject, layout.stateHeight);

        TMP_Text text = AddText(
            image.transform,
            "StateText",
            "НЕ КУПЛЕНО",
            16,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            EquipmentWireframeTheme.TextSecondary
        );

        SetFullStretch(text.rectTransform);

        text.raycastTarget = false;

        return (image, text);
    }

    private static (
        Image image,
        TMP_Text label,
        Button button,
        Image priceImage,
        TMP_Text priceText
    ) BuildActionRow(
        RectTransform parent,
        Sprite cardSprite,
        Sprite barSprite,
        EquipmentLayoutSettings layout)
    {
        Image image = AddImage(
            parent,
            "ActionRow",
            EquipmentWireframeTheme.Action,
            cardSprite,
            true
        );

        AddFixedHeight(image.gameObject, layout.actionHeight);

        Button button = image.gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        TMP_Text label = AddText(
            image.transform,
            "LabelText",
            "КУПИТЬ",
            22,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold,
            EquipmentWireframeTheme.White(0.95f)
        );

        SetBlock(
            label.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(18f, 0f),
            new Vector2(200f, 36f),
            new Vector2(0f, 0.5f)
        );

        label.raycastTarget = false;

        // Цена в отдельном бейдже, как на «Улучшениях».
        Image priceImage = AddImage(
            image.transform,
            "PriceBadge",
            EquipmentWireframeTheme.White(0.22f),
            barSprite,
            true
        );

        SetBlock(
            priceImage.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f),
            new Vector2(86f, 40f),
            new Vector2(1f, 0.5f)
        );

        priceImage.raycastTarget = false;

        TMP_Text priceText = AddText(
            priceImage.transform,
            "PriceText",
            "120",
            21,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            EquipmentWireframeTheme.White(0.95f)
        );

        SetFullStretch(priceText.rectTransform);

        priceText.raycastTarget = false;

        return (image, label, button, priceImage, priceText);
    }

    /// <summary>
    /// Раздаёт карточке ссылки на элементы иерархии. Сделано
    /// отдельным методом, чтобы порядок вызовов в BuildCard
    /// читался как «создали элементы — потом связали».
    /// </summary>
    private static void SetView(
        EquipmentCardView view,
        Image surface,
        Image frame,
        Image accentLine,
        Image iconWash,
        Image iconDisc,
        TMP_Text typeText,
        TMP_Text nameText,
        TMP_Text rarityText,
        RectTransform rarityRow,
        TMP_Text statsText,
        Image stateImage,
        TMP_Text stateText,
        Image actionImage,
        TMP_Text actionLabel,
        Button actionButton,
        Image priceImage,
        TMP_Text priceText,
        Button cardButton)
    {
        var so = new SerializedObject(view);

        SetObject(so, "surfaceImage", surface);
        SetObject(so, "frameImage", frame);
        SetObject(so, "accentLine", accentLine);
        SetObject(so, "cardButton", cardButton);

        SetObject(so, "iconWash", iconWash);
        SetObject(so, "iconDisc", iconDisc);
        SetObject(so, "typeText", typeText);

        SetObject(so, "nameText", nameText);
        SetObject(so, "rarityText", rarityText);
        SetObject(so, "rarityRow", rarityRow);
        SetObject(so, "statsText", statsText);

        SetObject(so, "stateImage", stateImage);
        SetObject(so, "stateText", stateText);

        SetObject(so, "actionImage", actionImage);
        SetObject(so, "actionLabel", actionLabel);
        SetObject(so, "actionButton", actionButton);
        SetObject(so, "priceImage", priceImage);
        SetObject(so, "priceText", priceText);

        so.ApplyModifiedProperties();
    }

    // =====================================================
    // ПАГИНАЦИЯ
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
            EquipmentWireframeTheme.Surface,
            sprite,
            true
        );

        RectTransform rect = image.rectTransform;

        SetBlock(
            rect,
            right ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f),
            new Vector2(right ? -26f : 26f, 0f),
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
            EquipmentWireframeTheme.TextSecondary
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
            new Vector2(-100f, 156f),
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
            EquipmentWireframeTheme.DotInactive,
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

    /// <summary>
    /// Счётчик страниц вместо кнопки обновления: на «Снаряжении»
    /// обновлять нечего, зато предметов много и важно понимать,
    /// где ты находишься.
    /// </summary>
    private static TMP_Text BuildPageCounter(
        RectTransform parent,
        Sprite cardSprite)
    {
        Image image = AddImage(
            parent,
            "PageCounter",
            EquipmentWireframeTheme.Surface,
            cardSprite,
            true
        );

        SetBlock(
            image.rectTransform,
            new Vector2(1f, 0f),
            new Vector2(-80f, 149f),
            new Vector2(180f, 40f),
            new Vector2(1f, 0f)
        );

        image.raycastTarget = false;

        TMP_Text text = AddText(
            image.transform,
            "Text",
            "01 / 04",
            20,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            EquipmentWireframeTheme.TextSecondary
        );

        SetFullStretch(text.rectTransform);

        text.raycastTarget = false;

        return text;
    }

    // =====================================================
    // ПУСТАЯ КАТЕГОРИЯ
    // =====================================================

    private static (GameObject root, TMP_Text text) BuildEmptyState(
        RectTransform parent,
        Sprite cardSprite)
    {
        Image surface = AddImage(
            parent,
            "EmptyState",
            EquipmentWireframeTheme.SurfaceMuted,
            cardSprite,
            true
        );

        SetBlock(
            surface.rectTransform,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(640f, 160f),
            new Vector2(0.5f, 0.5f)
        );

        surface.raycastTarget = false;

        TMP_Text text = AddText(
            surface.transform,
            "Text",
            "В этой категории пока нет предметов.",
            22,
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            EquipmentWireframeTheme.TextSecondary
        );

        SetFullStretch(text.rectTransform);

        text.raycastTarget = false;

        surface.gameObject.SetActive(false);

        return (surface.gameObject, text);
    }

    private static Button BuildBackButton(
        RectTransform parent,
        Sprite cardSprite)
    {
        Image image = AddImage(
            parent,
            "BackButton",
            EquipmentWireframeTheme.Surface,
            cardSprite,
            true
        );

        SetBlock(
            image.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(80f, 48f),
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
            EquipmentWireframeTheme.TextSecondary
        );

        SetFullStretch(label.rectTransform);

        label.raycastTarget = false;

        return button;
    }

    // =====================================================
    // ЧИСТКА И КНОПКА
    // =====================================================

    private static void DeletePrevious(
        Transform buttonsRoot,
        Transform panelParent,
        SerializedObject menuSo)
    {
        menuSo.FindProperty("equipmentButton").objectReferenceValue = null;
        menuSo.FindProperty("equipmentPanel").objectReferenceValue = null;

        menuSo.ApplyModifiedProperties();

        DestroyChildrenNamed(buttonsRoot, ButtonName);
        DestroyChildrenNamed(panelParent, PanelName);
    }

    private static void DestroyChildrenNamed(
        Transform parent,
        string name)
    {
        if (parent == null)
            return;

        var children = new List<Transform>();

        foreach (Transform child in parent)
            children.Add(child);

        foreach (Transform child in children)
        {
            if (child.name == name)
                Object.DestroyImmediate(child.gameObject);
        }
    }

    private static Button EnsureEquipmentButton(
        SerializedObject menuSo,
        Button shopButton)
    {
        Button existing =
            menuSo.FindProperty("equipmentButton")
                .objectReferenceValue as Button;

        if (existing != null)
            return existing;

        if (shopButton == null)
            return null;

        GameObject buttonObject = Object.Instantiate(
            shopButton.gameObject,
            shopButton.transform.parent
        );

        buttonObject.name = ButtonName;

        Undo.RegisterCreatedObjectUndo(buttonObject, MenuPath);

        TextMeshProUGUI label =
            buttonObject.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
            label.text = "Снаряжение";

        Button button = buttonObject.GetComponent<Button>();

        if (button == null)
            button = buttonObject.AddComponent<Button>();

        return button;
    }

    private static bool EnsurePlayerEquipmentApplier()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");

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

    // =====================================================
    // АССЕТЫ МАГАЗИНА
    // =====================================================

    private static void RegisterStoreAssets()
    {
        UpgradeManager manager =
            Object.FindAnyObjectByType<UpgradeManager>();

        if (manager == null)
            return;

        RegisterStoreAssets<AbilityData>(
            manager,
            "availableAbilities",
            "t:AbilityData"
        );

        RegisterStoreAssets<ClothingData>(
            manager,
            "availableClothing",
            "t:ClothingData"
        );
    }

    private static void RegisterStoreAssets<T>(
        UpgradeManager manager,
        string fieldName,
        string assetFilter) where T : ScriptableObject
    {
        SerializedObject managerSo = new SerializedObject(manager);

        SerializedProperty list = managerSo.FindProperty(fieldName);

        if (list == null)
            return;

        foreach (string guid in AssetDatabase.FindAssets(assetFilter))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

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

    private static int CountStoreItems()
    {
        UpgradeManager manager =
            Object.FindAnyObjectByType<UpgradeManager>();

        if (manager == null)
            return 0;

        return
            manager.GetAvailableWeapons().Count
            + manager.GetAvailableAbilities().Count
            + manager.GetAvailableClothing().Count;
    }

    private static int PageCount(int itemCount, int cardsPerPage)
    {
        if (itemCount <= 0 || cardsPerPage <= 0)
            return 0;

        return Mathf.CeilToInt(itemCount / (float)cardsPerPage);
    }

    // =====================================================
    // ХЕЛПЕРЫ
    // =====================================================

    private static void AddFixedHeight(
        GameObject target,
        float height,
        float flexibleHeight = -1f)
    {
        LayoutElement layout = target.GetComponent<LayoutElement>();

        if (layout == null)
            layout = target.AddComponent<LayoutElement>();

        layout.preferredHeight = height;

        if (flexibleHeight > 0f)
            layout.flexibleHeight = flexibleHeight;
    }

    private static TextMeshProUGUI AddCardText(
        RectTransform parent,
        string name,
        string content,
        int size,
        FontStyles style,
        Color color)
    {
        TextMeshProUGUI text = AddText(
            parent,
            name,
            content,
            size,
            TextAlignmentOptions.MidlineLeft,
            style,
            color
        );

        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        return text;
    }

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
        return AddRect(
            parent,
            name,
            Vector2.zero,
            Vector2.one,
            new Vector2(left, bottom),
            new Vector2(-right, -top)
        );
    }

    /// <summary>
    /// Горизонтальная полоса на всю ширину: отступы задаются сверху
    /// и снизу в пикселях от краёв панели.
    /// </summary>
    private static RectTransform AddBand(
        Transform parent,
        string name,
        float top,
        float bottom)
    {
        RectTransform rect = AddRect(
            parent,
            name,
            Vector2.zero,
            Vector2.one,
            Vector2.zero,
            Vector2.zero
        );

        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(0f, bottom);
        rect.offsetMax = new Vector2(0f, -top);

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

    private static void SetObject(SerializedObject so, string name, Object value)
    {
        SerializedProperty property = so.FindProperty(name);

        if (property == null)
            return;

        property.objectReferenceValue = value;
    }

    private static void SetObjectArray(
        SerializedObject so,
        string name,
        Object[] values)
    {
        SerializedProperty property = so.FindProperty(name);

        if (property == null)
            return;

        property.arraySize = values.Length;

        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue =
                values[i];
        }
    }

    private static Object[] TabObjects(List<Button> buttons)
    {
        var values = new Object[buttons.Count];

        for (int i = 0; i < buttons.Count; i++)
            values[i] = buttons[i];

        return values;
    }

    private static Object[] TextObjects(List<TMP_Text> texts)
    {
        var values = new Object[texts.Count];

        for (int i = 0; i < texts.Count; i++)
            values[i] = texts[i];

        return values;
    }

    private static Object[] CardObjects(List<EquipmentCardView> cards)
    {
        var values = new Object[cards.Count];

        for (int i = 0; i < cards.Count; i++)
            values[i] = cards[i];

        return values;
    }
}

#endif
