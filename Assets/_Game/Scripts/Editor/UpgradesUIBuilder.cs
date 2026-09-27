#if UNITY_EDITOR

using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Построитель экрана «Улучшения» в главном меню.
/// Пункт меню: Tools -> Bullet Rush -> Build Upgrades UI.
///
/// Собирает светлый wireframe-макет магазина постоянных улучшений:
///  - шапка: «УЛУЧШЕНИЯ» слева, подзаголовок по центру, монеты справа
///    (опыта и уровня игрока на этом экране нет);
///  - горизонтальный ряд из четырёх карточек со стрелками по краям;
///  - точки пагинации снизу;
///  - кнопка «ОБНОВИТЬ» за монеты и «Назад».
///
/// Иерархия намеренно плоская и с осмысленными именами, чтобы её
/// можно было перенести в Figma и заменить элементы один на один.
///
/// За один клик:
///  - удаляет старую панель «Улучшения», если она есть;
///  - создаёт панель и подключает все ссылки на
///    MainMenuUI / PermanentUpgradesUI / SubPanelUI;
///  - добавляет PermanentUpgradeApplier на игрока (постоянные
///    бонусы применяются на старте забега).
/// </summary>
public static class UpgradesUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Upgrades UI";

    private const string PanelName = "UpgradesPanel";

    private const int CardCount = PermanentUpgradesUI.CardsPerPage;

    private const float CardWidth = 360f;
    private const float CardHeight = 540f;
    private const float CardGap = 24f;
    private const float CardPadding = 22f;
    private const float RowSpacing = 10f;
    private const float FrameInset = 3f;

    private const int RefreshCost = 50;

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

        SerializedObject menuSo = new SerializedObject(menu);

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
            UpgradesWireframeTheme.PanelBackground;

        Undo.RegisterCreatedObjectUndo(panelObject, MenuPath);

        // =====================================================
        // 2. Фон: мягкие пятна и простые формы
        // =====================================================
        BuildBackground(panelRect, barSprite, blobSprite);

        // =====================================================
        // 3. Шапка
        // =====================================================
        TextMeshProUGUI coinsText = BuildTopBar(panelRect, cardSprite);

        // =====================================================
        // 4. Ряд карточек со стрелками
        // =====================================================
        var cards = new List<PermanentUpgradeCardView>();
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
        cardArea.offsetMin = new Vector2(0f, -720f);
        cardArea.offsetMax = new Vector2(0f, -180f);

        RectTransform cardRow = AddStretch(
            cardArea,
            "CardRow",
            150f,
            150f,
            0f,
            0f
        );

        HorizontalLayoutGroup rowLayout =
            cardRow.gameObject.AddComponent<HorizontalLayoutGroup>();

        rowLayout.spacing = CardGap;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandHeight = false;

        for (int i = 0; i < CardCount; i++)
            cards.Add(BuildCard(cardRow, i, cardSprite, barSprite));

        BuildArrow(cardArea, "PreviousButton", "<", false, cardSprite,
            arrows, arrowImages);

        BuildArrow(cardArea, "NextButton", ">", true, cardSprite,
            arrows, arrowImages);

        // =====================================================
        // 5. Точки пагинации
        // =====================================================
        GameObject dotTemplate =
            BuildDotTemplate(panelRect, circleSprite);

        // =====================================================
        // 6. Нижние кнопки
        // =====================================================
        Button backButton = BuildBackButton(panelRect, cardSprite);

        (Button refreshButton, Image refreshImage, TMP_Text refreshCost) =
            BuildRefreshButton(panelRect, cardSprite, barSprite);

        // =====================================================
        // 7. Компоненты панели
        // =====================================================
        PermanentUpgradesUI upgradesUI =
            panelObject.AddComponent<PermanentUpgradesUI>();

        SerializedObject upgradesSo =
            new SerializedObject(upgradesUI);

        upgradesSo.FindProperty("coinsText").objectReferenceValue =
            coinsText;

        SerializedProperty slots =
            upgradesSo.FindProperty("cardSlots");

        slots.arraySize = cards.Count;

        for (int i = 0; i < cards.Count; i++)
        {
            slots.GetArrayElementAtIndex(i).objectReferenceValue =
                cards[i];
        }

        upgradesSo.FindProperty("previousButton").objectReferenceValue =
            arrows[0];

        upgradesSo.FindProperty("previousImage").objectReferenceValue =
            arrowImages[0];

        upgradesSo.FindProperty("nextButton").objectReferenceValue =
            arrows[1];

        upgradesSo.FindProperty("nextImage").objectReferenceValue =
            arrowImages[1];

        upgradesSo.FindProperty("dotsRoot").objectReferenceValue =
            dotTemplate.transform.parent as RectTransform;

        upgradesSo.FindProperty("dotTemplate").objectReferenceValue =
            dotTemplate;

        upgradesSo.FindProperty("refreshButton").objectReferenceValue =
            refreshButton;

        upgradesSo.FindProperty("refreshImage").objectReferenceValue =
            refreshImage;

        upgradesSo.FindProperty("refreshCostText").objectReferenceValue =
            refreshCost;

        upgradesSo.FindProperty("refreshCost").intValue = RefreshCost;

        upgradesSo.ApplyModifiedProperties();

        SubPanelUI subPanel = panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);

        subSo.FindProperty("backButton").objectReferenceValue = backButton;

        subSo.ApplyModifiedProperties();

        // =====================================================
        // 8. Подключаем к главному меню и игроку
        // =====================================================
        menuSo.FindProperty("upgradesButton").objectReferenceValue =
            upgradesButton;

        menuSo.FindProperty("upgradesPanel").objectReferenceValue =
            panelObject;

        menuSo.ApplyModifiedProperties();

        EnsurePlayerUpgradeApplier();

        // =====================================================
        // 9. Финал
        // =====================================================
        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        int pageCount = Mathf.CeilToInt(
            PermanentUpgrades.Stats.Count / (float)CardCount
        );

        EditorUtility.DisplayDialog(
            "Build Upgrades UI",
            "Магазин «Улучшения» собран.\n\n" +
            $"Параметров: {PermanentUpgrades.Stats.Count}, " +
            $"уровней на параметр: {PermanentUpgrades.MaxLevel}.\n" +
            $"Страниц: {pageCount}, " +
            $"карточек на странице: {CardCount}.\n" +
            $"Цена уровня: 10 -> 15 -> 20 -> 25 -> 35 -> 45 -> 60 -> " +
            "80 -> 110 -> 150.\n\n" +
            "Это wireframe-заглушка: цвета собраны в " +
            "UpgradesWireframeTheme, формы — в " +
            "Assets/_Game/UI/Placeholder. Опыта и уровня игрока " +
            "на экране нет, прогресс на карточке = уровень " +
            "улучшения.",
            "OK"
        );
    }

    // =====================================================
    // BACKGROUND
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
            UpgradesWireframeTheme.Wash(
                UpgradesWireframeTheme.DecorWash,
                0.55f
            ),
            new Vector2(0.16f, 0.78f),
            new Vector2(-380f, 40f),
            720f,
            720f,
            -14f
        );

        AddDecorativeImage(
            decor,
            "BlobRight",
            blobSprite,
            UpgradesWireframeTheme.Wash(
                UpgradesWireframeTheme.DecorWash,
                0.45f
            ),
            new Vector2(0.88f, 0.16f),
            new Vector2(320f, -20f),
            640f,
            640f,
            10f
        );

        AddDecorativeImage(
            decor,
            "BandTop",
            barSprite,
            UpgradesWireframeTheme.Wash(
                UpgradesWireframeTheme.PanelWash,
                0.9f
            ),
            new Vector2(0.5f, 1f),
            new Vector2(0f, 0f),
            1500f,
            16f
        );

        AddDecorativeImage(
            decor,
            "BandBottom",
            barSprite,
            UpgradesWireframeTheme.Wash(
                UpgradesWireframeTheme.PanelWash,
                0.7f
            ),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 0f),
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

        // Фон не должен перехватывать клики.
        image.raycastTarget = false;
    }

    // =====================================================
    // TOP BAR
    // =====================================================

    private static TextMeshProUGUI BuildTopBar(
        RectTransform panelRect,
        Sprite cardSprite)
    {
        RectTransform topBar = AddBlock(
            panelRect,
            "TopBar",
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 150f),
            new Vector2(0.5f, 1f)
        );

        topBar.anchorMin = Vector2.zero;
        topBar.anchorMax = Vector2.one;
        topBar.offsetMin = new Vector2(0f, -150f);
        topBar.offsetMax = new Vector2(0f, 0f);

        TextMeshProUGUI title = AddText(
            topBar,
            "TitleText",
            "УЛУЧШЕНИЯ",
            52,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold,
            UpgradesWireframeTheme.TextPrimary
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
            "Усиль своего героя",
            26,
            TextAlignmentOptions.Center,
            FontStyles.Normal,
            UpgradesWireframeTheme.TextSecondary
        );

        SetBlock(
            subtitle.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 4f),
            new Vector2(520f, 40f),
            new Vector2(0.5f, 0.5f)
        );

        // Монеты: только их количество, без прогресса аккаунта.
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
            UpgradesWireframeTheme.Surface,
            cardSprite,
            true
        ).raycastTarget = false;

        Image coinIcon = AddImage(
            coinsBlock,
            "CoinIcon",
            UpgradesWireframeTheme.DamageAccent,
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

        TextMeshProUGUI coinsText = AddText(
            coinsBlock,
            "CoinsText",
            "48",
            34,
            TextAlignmentOptions.MidlineRight,
            FontStyles.Bold,
            UpgradesWireframeTheme.TextPrimary
        );

        SetBlock(
            coinsText.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(-22f, 0f),
            new Vector2(200f, 44f),
            new Vector2(1f, 0.5f)
        );

        return coinsText;
    }

    // =====================================================
    // CARD
    // =====================================================

    private static PermanentUpgradeCardView BuildCard(
        RectTransform parent,
        int index,
        Sprite cardSprite,
        Sprite barSprite)
    {
        RectTransform card = AddBlock(
            parent,
            $"Card_{index + 1}",
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(CardWidth, CardHeight),
            new Vector2(0.5f, 0.5f)
        );

        LayoutElement cardLayout =
            card.gameObject.AddComponent<LayoutElement>();

        cardLayout.preferredWidth = CardWidth;
        cardLayout.preferredHeight = CardHeight;

        Image shadow = AddImage(
            card,
            "CardShadow",
            UpgradesWireframeTheme.CardShadow,
            cardSprite,
            true
        );

        shadow.rectTransform.offsetMin = new Vector2(-6f, -10f);
        shadow.rectTransform.offsetMax = new Vector2(6f, 6f);
        shadow.raycastTarget = false;

        Image frame = AddImage(
            card,
            "CardFrame",
            UpgradesWireframeTheme.DamageAccent,
            cardSprite,
            true
        );

        frame.raycastTarget = false;

        RectTransform face = AddStretch(
            card,
            "CardFace",
            FrameInset,
            FrameInset,
            FrameInset,
            FrameInset
        );

        Image surface = AddImage(
            face,
            "CardSurface",
            UpgradesWireframeTheme.Surface,
            cardSprite,
            true
        );

        Button selectTarget = surface.gameObject.AddComponent<Button>();

        selectTarget.transition = Selectable.Transition.None;

        VerticalLayoutGroup faceLayout =
            face.gameObject.AddComponent<VerticalLayoutGroup>();

        faceLayout.spacing = RowSpacing;
        faceLayout.padding = new RectOffset(
            (int)CardPadding,
            (int)CardPadding,
            (int)CardPadding,
            (int)CardPadding
        );
        faceLayout.childAlignment = TextAnchor.UpperLeft;
        faceLayout.childControlWidth = true;
        faceLayout.childForceExpandWidth = true;
        faceLayout.childControlHeight = true;
        faceLayout.childForceExpandHeight = false;

        // --- верх: место под иконку ---
        Image iconArea = AddImage(
            face,
            "IconArea",
            UpgradesWireframeTheme.Wash(
                UpgradesWireframeTheme.DamageAccent,
                0.12f
            ),
            cardSprite,
            true
        );

        AddFixedHeight(iconArea.gameObject, 132f, 1f);

        iconArea.raycastTarget = false;

        Image iconMark = AddImage(
            iconArea.transform,
            "IconMark",
            UpgradesWireframeTheme.DamageAccent,
            cardSprite,
            true
        );

        SetBlock(
            iconMark.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 8f),
            new Vector2(76f, 76f),
            new Vector2(0.5f, 0.5f)
        );

        iconMark.raycastTarget = false;

        TextMeshProUGUI categoryText = AddText(
            iconArea.transform,
            "CategoryText",
            "УРОН",
            17,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            UpgradesWireframeTheme.DamageAccent
        );

        SetBlock(
            categoryText.rectTransform,
            new Vector2(0.5f, 0f),
            new Vector2(0f, 10f),
            new Vector2(280f, 24f),
            new Vector2(0.5f, 0f)
        );

        categoryText.raycastTarget = false;

        // --- название и описание ---
        TextMeshProUGUI nameText = AddText(
            face,
            "NameText",
            "УРОН",
            30,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            UpgradesWireframeTheme.TextPrimary
        );

        AddFixedHeight(nameText.gameObject, 42f);

        nameText.raycastTarget = false;

        TextMeshProUGUI descriptionText = AddText(
            face,
            "DescriptionText",
            "Увеличивает урон всего оружия.",
            19,
            TextAlignmentOptions.Top,
            FontStyles.Normal,
            UpgradesWireframeTheme.TextSecondary
        );

        AddFixedHeight(descriptionText.gameObject, 50f);

        descriptionText.textWrappingMode = TextWrappingModes.Normal;
        descriptionText.overflowMode = TextOverflowModes.Truncate;
        descriptionText.raycastTarget = false;

        // --- уровень улучшения и его прогресс ---
        TextMeshProUGUI levelText = AddText(
            face,
            "LevelText",
            "УРОВЕНЬ 1 / 10",
            21,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            UpgradesWireframeTheme.TextPrimary
        );

        AddFixedHeight(levelText.gameObject, 28f);

        levelText.raycastTarget = false;

        Slider levelBar = AddProgressBar(
            face,
            "LevelBar",
            12f,
            barSprite
        );

        // --- текущий и следующий бонус ---
        TextMeshProUGUI currentValue = AddBonusRow(
            face,
            "CurrentRow",
            "СЕЙЧАС",
            26,
            UpgradesWireframeTheme.DamageAccent
        );

        TextMeshProUGUI nextValue = AddBonusRow(
            face,
            "NextRow",
            "ДАЛЬШЕ",
            22,
            UpgradesWireframeTheme.TextSecondary
        );

        // --- кнопка покупки ---
        RectTransform buyRow = BuildBuyRow(face, cardSprite, barSprite);

        // --- состояние «максимум» ---
        Image maxedImage = BuildMaxedRow(face, cardSprite);

        // В макете показываем обычное состояние — на максимуме
        // строку включит PermanentUpgradeCardView.Refresh.
        maxedImage.gameObject.SetActive(false);

        // --- индикатор выбранной карточки ---
        // Висит на Card, а не на CardFace: иначе VerticalLayoutGroup
        // карточки попытается им управлять и обнулит размер.
        Image selectedIndicator = AddImage(
            card,
            "SelectedIndicator",
            UpgradesWireframeTheme.DamageAccent,
            barSprite,
            true
        );

        SetBlock(
            selectedIndicator.rectTransform,
            new Vector2(0.5f, 1f),
            new Vector2(0f, -FrameInset),
            new Vector2(70f, 6f),
            new Vector2(0.5f, 1f)
        );

        selectedIndicator.raycastTarget = false;
        selectedIndicator.gameObject.SetActive(false);

        PermanentUpgradeCardView view =
            face.gameObject.AddComponent<PermanentUpgradeCardView>();

        SerializedObject viewSo = new SerializedObject(view);

        viewSo.FindProperty("frame").objectReferenceValue = frame;
        viewSo.FindProperty("face").objectReferenceValue = face;
        viewSo.FindProperty("selectedIndicator").objectReferenceValue =
            selectedIndicator;

        viewSo.FindProperty("iconArea").objectReferenceValue = iconArea;
        viewSo.FindProperty("iconMark").objectReferenceValue = iconMark;
        viewSo.FindProperty("categoryText").objectReferenceValue =
            categoryText;

        viewSo.FindProperty("nameText").objectReferenceValue = nameText;
        viewSo.FindProperty("descriptionText").objectReferenceValue =
            descriptionText;
        viewSo.FindProperty("levelText").objectReferenceValue = levelText;
        viewSo.FindProperty("currentValueText").objectReferenceValue =
            currentValue;
        viewSo.FindProperty("nextValueText").objectReferenceValue = nextValue;

        viewSo.FindProperty("levelBar").objectReferenceValue = levelBar;

        viewSo.FindProperty("selectTarget").objectReferenceValue =
            selectTarget;
        viewSo.FindProperty("buyButton").objectReferenceValue =
            buyRow.GetComponent<Button>();
        viewSo.FindProperty("buyImage").objectReferenceValue =
            buyRow.GetComponent<Image>();
        viewSo.FindProperty("buyCostBadge").objectReferenceValue =
            buyRow.Find("CostBadge").GetComponent<Image>();
        viewSo.FindProperty("buyCoinIcon").objectReferenceValue =
            buyRow.Find("CostBadge/CoinIcon").GetComponent<Image>();
        viewSo.FindProperty("buyLabel").objectReferenceValue =
            buyRow.Find("LabelText").GetComponent<TextMeshProUGUI>();
        viewSo.FindProperty("buyCost").objectReferenceValue =
            buyRow.Find("CostBadge/CostText").GetComponent<TextMeshProUGUI>();

        viewSo.FindProperty("maxedImage").objectReferenceValue = maxedImage;
        viewSo.FindProperty("maxedLabel").objectReferenceValue =
            maxedImage.transform.Find("LabelText")
                .GetComponent<TextMeshProUGUI>();

        viewSo.ApplyModifiedProperties();

        return view;
    }

    /// <summary>
    /// Строка «СЕЙЧАС» / «ДАЛЬШЕ»: подпись слева, значение справа.
    /// </summary>
    private static TextMeshProUGUI AddBonusRow(
        Transform parent,
        string name,
        string label,
        int valueSize,
        Color valueColor)
    {
        RectTransform row = AddBlock(
            parent,
            name,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(316f, 28f),
            new Vector2(0.5f, 0.5f)
        );

        AddFixedHeight(row.gameObject, 28f);

        TextMeshProUGUI labelText = AddText(
            row,
            "LabelText",
            label,
            16,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Normal,
            UpgradesWireframeTheme.TextMuted
        );

        SetBlock(
            labelText.rectTransform,
            new Vector2(0f, 0.5f),
            Vector2.zero,
            new Vector2(120f, 28f),
            new Vector2(0f, 0.5f)
        );

        labelText.raycastTarget = false;

        TextMeshProUGUI valueText = AddText(
            row,
            "ValueText",
            "+5%",
            valueSize,
            TextAlignmentOptions.MidlineRight,
            FontStyles.Bold,
            valueColor
        );

        SetBlock(
            valueText.rectTransform,
            new Vector2(1f, 0.5f),
            Vector2.zero,
            new Vector2(180f, 28f),
            new Vector2(1f, 0.5f)
        );

        valueText.raycastTarget = false;

        return valueText;
    }

    private static RectTransform BuildBuyRow(
        Transform parent,
        Sprite cardSprite,
        Sprite barSprite)
    {
        Image buyImage = AddImage(
            parent,
            "BuyButton",
            UpgradesWireframeTheme.DamageAccent,
            cardSprite,
            true
        );

        RectTransform row = buyImage.rectTransform;

        AddFixedHeight(row.gameObject, 74f);

        Button button = buyImage.gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = buyImage;

        TextMeshProUGUI label = AddText(
            row,
            "LabelText",
            "УЛУЧШИТЬ",
            23,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold,
            Color.white
        );

        SetBlock(
            label.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(20f, 0f),
            new Vector2(200f, 40f),
            new Vector2(0f, 0.5f)
        );

        label.raycastTarget = false;

        // Цена — самое заметное место кнопки.
        Image badge = AddImage(
            row,
            "CostBadge",
            UpgradesWireframeTheme.White(0.22f),
            cardSprite,
            true
        );

        SetBlock(
            badge.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(-16f, 0f),
            new Vector2(96f, 44f),
            new Vector2(1f, 0.5f)
        );

        badge.raycastTarget = false;

        Image coinIcon = AddImage(
            badge.transform,
            "CoinIcon",
            Color.white,
            barSprite,
            true
        );

        SetBlock(
            coinIcon.rectTransform,
            new Vector2(0f, 0.5f),
            new Vector2(10f, 0f),
            new Vector2(22f, 22f),
            new Vector2(0f, 0.5f)
        );

        coinIcon.raycastTarget = false;

        TextMeshProUGUI cost = AddText(
            badge.transform,
            "CostText",
            "15",
            24,
            TextAlignmentOptions.MidlineRight,
            FontStyles.Bold,
            Color.white
        );

        SetBlock(
            cost.rectTransform,
            new Vector2(1f, 0.5f),
            new Vector2(-10f, 0f),
            new Vector2(60f, 28f),
            new Vector2(1f, 0.5f)
        );

        cost.raycastTarget = false;

        return row;
    }

    private static Image BuildMaxedRow(Transform parent, Sprite cardSprite)
    {
        Image maxedImage = AddImage(
            parent,
            "MaxedRow",
            UpgradesWireframeTheme.SurfaceDisabled,
            cardSprite,
            true
        );

        RectTransform row = maxedImage.rectTransform;

        AddFixedHeight(row.gameObject, 74f);

        maxedImage.raycastTarget = false;

        TextMeshProUGUI label = AddText(
            row,
            "LabelText",
            "МАКСИМАЛЬНЫЙ УРОВЕНЬ",
            19,
            TextAlignmentOptions.Center,
            FontStyles.Bold,
            UpgradesWireframeTheme.TextMuted
        );

        SetFullStretch(label.rectTransform);

        label.raycastTarget = false;

        return maxedImage;
    }

    private static Slider AddProgressBar(
        Transform parent,
        string name,
        float height,
        Sprite sprite)
    {
        RectTransform rect = AddBlock(
            parent,
            name,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(316f, height),
            new Vector2(0.5f, 0.5f)
        );

        AddFixedHeight(rect.gameObject, height);

        Image track = rect.gameObject.AddComponent<Image>();

        track.sprite = sprite;
        track.type = Image.Type.Sliced;
        track.color = UpgradesWireframeTheme.Track;
        track.raycastTarget = false;

        RectTransform fillArea = AddStretch(
            rect,
            "FillArea",
            3f,
            3f,
            3f,
            3f
        );

        RectTransform fill = AddStretch(
            fillArea,
            "Fill",
            0f,
            0f,
            0f,
            0f
        );

        // Slider тянет заливку по этой оси.
        fill.anchorMin = new Vector2(0f, 0f);
        fill.anchorMax = new Vector2(0f, 1f);
        fill.offsetMin = Vector2.zero;
        fill.offsetMax = Vector2.zero;

        Image fillImage = fill.gameObject.AddComponent<Image>();

        fillImage.sprite = sprite;
        fillImage.type = Image.Type.Sliced;
        fillImage.color = UpgradesWireframeTheme.DamageAccent;
        fillImage.raycastTarget = false;

        Slider slider = rect.gameObject.AddComponent<Slider>();

        slider.transition = Selectable.Transition.None;
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
            UpgradesWireframeTheme.Surface,
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
            UpgradesWireframeTheme.TextSecondary
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
            UpgradesWireframeTheme.DotInactive,
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
    // BOTTOM BUTTONS
    // =====================================================

    private static Button BuildBackButton(
        RectTransform parent,
        Sprite cardSprite)
    {
        Image image = AddImage(
            parent,
            "BackButton",
            UpgradesWireframeTheme.Surface,
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
            UpgradesWireframeTheme.TextSecondary
        );

        SetFullStretch(label.rectTransform);

        label.raycastTarget = false;

        return button;
    }

    private static (
        Button button,
        Image image,
        TMP_Text cost
    ) BuildRefreshButton(
        RectTransform parent,
        Sprite cardSprite,
        Sprite barSprite)
    {
        Image image = AddImage(
            parent,
            "RefreshButton",
            UpgradesWireframeTheme.Action,
            cardSprite,
            true
        );

        SetBlock(
            image.rectTransform,
            new Vector2(1f, 0f),
            new Vector2(-80f, 58f),
            new Vector2(300f, 84f),
            new Vector2(1f, 0f)
        );

        Button button = image.gameObject.AddComponent<Button>();

        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        TextMeshProUGUI label = AddText(
            image.transform,
            "LabelText",
            "ОБНОВИТЬ",
            24,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Bold,
            Color.white
        );

        SetBlock(
            label.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(22f, -18f),
            new Vector2(200f, 32f),
            new Vector2(0f, 1f)
        );

        label.raycastTarget = false;

        Image coinIcon = AddImage(
            image.transform,
            "CoinIcon",
            Color.white,
            barSprite,
            true
        );

        SetBlock(
            coinIcon.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(24f, 18f),
            new Vector2(20f, 20f),
            new Vector2(0f, 0f)
        );

        coinIcon.raycastTarget = false;

        TextMeshProUGUI cost = AddText(
            image.transform,
            "CostText",
            $"{RefreshCost} монет",
            20,
            TextAlignmentOptions.MidlineLeft,
            FontStyles.Normal,
            UpgradesWireframeTheme.White(0.75f)
        );

        SetBlock(
            cost.rectTransform,
            new Vector2(0f, 0f),
            new Vector2(52f, 18f),
            new Vector2(200f, 24f),
            new Vector2(0f, 0f)
        );

        cost.raycastTarget = false;

        return (button, image, cost);
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

        var children = new List<Transform>();

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
            menuSo.FindProperty("upgradesButton")
                .objectReferenceValue as Button;

        if (existing != null)
            return existing;

        if (shopButton == null)
            return null;

        GameObject buttonObject = Object.Instantiate(
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
        GameObject player = GameObject.FindGameObjectWithTag("Player");

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
    // HELPERS
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

    /// <summary>
    /// Ставит существующий RectTransform по якорю: точка опоры,
    /// позиция и размер.
    /// </summary>
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

    /// <summary>
    /// Растягивает элемент на весь родитель с отступами
    /// слева, справа, снизу и сверху.
    /// </summary>
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
