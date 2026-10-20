#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class ArcadeUiRedesignBuilder
{
    private const string MenuPath =
        "Tools/ArcadeSurvivor/Build/New Design";

    private const string ScenePath = "Assets/Scenes/Game.unity";
    private const string DialogTitle = "Arcade Survivor - New Design";

    private enum ButtonKind
    {
        Primary,
        Accent,
        Secondary,
        Icon,
        Tab,
        Language,
        Card,
        Dot
    }

    [MenuItem(MenuPath, false, 1)]
    public static void BuildNewDesign()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Show("Exit Play Mode before building the new design.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        Scene scene = EditorSceneManager.GetActiveScene();

        if (scene.path != ScenePath)
        {
            if (!File.Exists(ScenePath))
            {
                Show("Game scene was not found at " + ScenePath + ".");
                return;
            }

            scene = EditorSceneManager.OpenScene(
                ScenePath,
                OpenSceneMode.Single
            );
        }

        MainMenuUI menu = FindMenu(scene);

        if (menu == null)
        {
            Show("MainMenuUI was not found in the Game scene.");
            return;
        }

        SerializedObject menuObject = new SerializedObject(menu);
        SerializedProperty menuPanelProperty =
            menuObject.FindProperty("menuPanel");

        GameObject menuPanel = menuPanelProperty != null
            ? menuPanelProperty.objectReferenceValue as GameObject
            : null;

        if (menuPanel == null)
        {
            Show("MainMenuUI.menuPanel is not assigned.");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Arcade Survivor New Design");

        ArcadeUiAtlas.Ensure();

        if (ArcadeUiAtlas.Backdrop == null)
        {
            Show("UI atlas could not be generated. Check the Console.");
            return;
        }

        Transform menuRoot = menuPanel.transform;
        GameObject gameCanvas = FindRoot(scene, "Canvas");

        Undo.RegisterFullObjectHierarchyUndo(
            menuRoot.gameObject,
            "Arcade UI - Menu"
        );

        if (gameCanvas != null)
        {
            Undo.RegisterFullObjectHierarchyUndo(
                gameCanvas,
                "Arcade UI - Gameplay"
            );
        }

        BuildMenuChrome(menuRoot);
        LayoutMainMenu(menuRoot);
        UpgradeDailyMissionBars(menuRoot);

        StyleRoot(menuRoot);

        if (gameCanvas != null)
            StyleRoot(gameCanvas.transform);

        ApplyPanelBackgrounds(menuRoot, gameCanvas);
        StyleTopBars(menuRoot);
        StyleBrand(menuRoot);
        StyleMainMenuDetails(menuRoot);
        StyleGameplayDetails(gameCanvas);

        int imageCount;
        int atlasImageCount;
        int textureCount;

        Audit(
            menuRoot,
            gameCanvas != null ? gameCanvas.transform : null,
            out imageCount,
            out atlasImageCount,
            out textureCount
        );

        EditorSceneManager.MarkSceneDirty(scene);
        bool saved = EditorSceneManager.SaveScene(scene);

        AssetDatabase.SaveAssets();
        Undo.CollapseUndoOperations(undoGroup);

        Show(
            "Arcade Survivor UI redesigned.\n\n" +
            "UI images: " + imageCount + "\n" +
            "Using shared atlas: " + atlasImageCount + "\n" +
            "Unique UI textures: " + textureCount + "\n\n" +
            "Atlas: " + ArcadeUiAtlas.AtlasPath + "\n" +
            "Scene saved: " + (saved ? ScenePath : "not saved")
        );
    }

    private static MainMenuUI FindMenu(Scene scene)
    {
        MainMenuUI[] menus = Object.FindObjectsByType<MainMenuUI>(
            FindObjectsInactive.Include
        );

        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] != null && menus[i].gameObject.scene == scene)
                return menus[i];
        }

        return null;
    }

    private static GameObject FindRoot(Scene scene, string name)
    {
        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i] != null && roots[i].name == name)
                return roots[i];
        }

        return null;
    }

    private static void BuildMenuChrome(Transform menuRoot)
    {
        DestroyChrome(menuRoot, "ArcadeUi_Brand");
        DestroyChrome(menuRoot, "ArcadeUi_RailLeft");
        DestroyChrome(menuRoot, "ArcadeUi_RailRight");
        DestroyChrome(menuRoot, "ArcadeUi_PlayGlow");

        RectTransform brand = CreateRect("ArcadeUi_Brand", menuRoot);

        SetRect(
            brand,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(96f, -70f),
            new Vector2(720f, 140f)
        );

        TMP_Text title = CreateText(
            "BrandTitle",
            brand,
            "BULLET RUSH",
            76f,
            TextAlignmentOptions.Left,
            ArcadeUiPalette.TextPrimary
        );

        SetRect(
            title.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            Vector2.zero,
            new Vector2(720f, 82f)
        );

        title.fontStyle |= FontStyles.Bold;
        title.characterSpacing = 2f;

        TMP_Text subtitle = CreateText(
            "BrandSubtitle",
            brand,
            "ARCADE SURVIVOR",
            22f,
            TextAlignmentOptions.Left,
            ArcadeUiPalette.Cyan
        );

        SetRect(
            subtitle.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(2f, -84f),
            new Vector2(720f, 30f)
        );

        subtitle.fontStyle |= FontStyles.Bold;
        subtitle.characterSpacing = 16f;

        Image accent = CreateImage(
            "BrandAccent",
            brand,
            ArcadeUiAtlas.Bar,
            ArcadeUiPalette.Magenta
        );

        SetRect(
            accent.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(2f, -122f),
            new Vector2(170f, 4f)
        );

        Image leftRail = CreateImage(
            "ArcadeUi_RailLeft",
            menuRoot,
            ArcadeUiAtlas.Panel,
            ArcadeUiPalette.Surface
        );

        leftRail.color = new Color(
            ArcadeUiPalette.Surface.r,
            ArcadeUiPalette.Surface.g,
            ArcadeUiPalette.Surface.b,
            0.58f
        );

        SetRect(
            leftRail.rectTransform,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(72f, -284f),
            new Vector2(392f, 464f)
        );

        Image rightRail = CreateImage(
            "ArcadeUi_RailRight",
            menuRoot,
            ArcadeUiAtlas.Panel,
            ArcadeUiPalette.Surface
        );

        rightRail.color = new Color(
            ArcadeUiPalette.Surface.r,
            ArcadeUiPalette.Surface.g,
            ArcadeUiPalette.Surface.b,
            0.58f
        );

        SetRect(
            rightRail.rectTransform,
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-72f, -284f),
            new Vector2(392f, 464f)
        );

        Image playGlow = CreateImage(
            "ArcadeUi_PlayGlow",
            menuRoot,
            ArcadeUiAtlas.Glow,
            ArcadeUiPalette.Cyan
        );

        playGlow.color = ArcadeUiPalette.Wash(ArcadeUiPalette.Cyan, 0.20f);

        SetRect(
            playGlow.rectTransform,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -300f),
            new Vector2(720f, 360f)
        );

        Transform start = FindDirect(menuRoot, "StartButton");

        if (start != null)
        {
            playGlow.transform.SetSiblingIndex(
                start.GetSiblingIndex()
            );
        }

        leftRail.transform.SetSiblingIndex(0);
        rightRail.transform.SetSiblingIndex(1);
    }

    private static void LayoutMainMenu(Transform menuRoot)
    {
        LayoutMenuButton(
            menuRoot,
            "UpgradesButton",
            false,
            0
        );

        LayoutMenuButton(
            menuRoot,
            "EquipmentButton",
            false,
            1
        );

        LayoutMenuButton(
            menuRoot,
            "MapsButton",
            false,
            2
        );

        LayoutMenuButton(
            menuRoot,
            "Btn_Achievements",
            false,
            3
        );

        LayoutMenuButton(
            menuRoot,
            "ProfileButton",
            true,
            0
        );

        LayoutMenuButton(
            menuRoot,
            "TipsButton",
            true,
            1
        );

        LayoutMenuButton(
            menuRoot,
            "Btn_Missions",
            true,
            2
        );

        LayoutMenuButton(
            menuRoot,
            "Btn_DailyReward",
            true,
            3
        );

        SetRectByName(
            menuRoot,
            "StartButton",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -300f),
            new Vector2(440f, 112f)
        );

        SetRectByName(
            menuRoot,
            "RunModifierDropdown",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -72f),
            new Vector2(440f, 64f)
        );

        SetRectByName(
            menuRoot,
            "RunModifierDropdownPanelAnchor",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -104f),
            new Vector2(440f, 260f)
        );

        SetRectByName(
            menuRoot,
            "LevelPanel",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-96f, -70f),
            new Vector2(372f, 126f)
        );

        SetRectByName(
            menuRoot,
            "PlayerNameText",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-96f, -204f),
            new Vector2(372f, 30f)
        );

        SetRectByName(
            menuRoot,
            "PersonalBestLabel",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(96f, -222f),
            new Vector2(600f, 34f)
        );

        SetRectByName(
            menuRoot,
            "LanguageLayotButtons",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-96f, 56f),
            new Vector2(300f, 52f)
        );
    }

    private static void LayoutMenuButton(
        Transform menuRoot,
        string name,
        bool rightSide,
        int index)
    {
        Transform button = FindDirect(menuRoot, name);

        if (button == null)
            return;

        RectTransform rect = button as RectTransform;

        if (rect == null)
            return;

        Vector2 anchor = rightSide
            ? new Vector2(1f, 1f)
            : new Vector2(0f, 1f);

        float x = rightSide ? -264f : 264f;
        float y = -364f - index * 104f;

        SetRect(
            rect,
            anchor,
            anchor,
            new Vector2(0.5f, 0.5f),
            new Vector2(x, y),
            new Vector2(344f, 88f)
        );
    }

    private static void SetRectByName(
        Transform root,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        Transform target = FindDirect(root, name);

        if (target is RectTransform rect)
            SetRect(rect, anchorMin, anchorMax, pivot, position, size);
    }

    private static void StyleRoot(Transform root)
    {
        DisableDecor(root);

        List<Image> images = Collect<Image>(root);

        for (int i = 0; i < images.Count; i++)
            StyleImage(images[i]);

        List<TMP_Text> texts = Collect<TMP_Text>(root);

        for (int i = 0; i < texts.Count; i++)
            StyleText(texts[i]);

        List<Text> legacyTexts = Collect<Text>(root);

        for (int i = 0; i < legacyTexts.Count; i++)
            StyleLegacyText(legacyTexts[i]);

        List<Button> buttons = Collect<Button>(root);

        for (int i = 0; i < buttons.Count; i++)
            StyleButton(buttons[i]);

        List<Toggle> toggles = Collect<Toggle>(root);

        for (int i = 0; i < toggles.Count; i++)
            StyleToggle(toggles[i]);

        List<TMP_Dropdown> dropdowns = Collect<TMP_Dropdown>(root);

        for (int i = 0; i < dropdowns.Count; i++)
            StyleDropdown(dropdowns[i]);
    }

    private static void DisableDecor(Transform root)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            Transform current = all[i];

            if (current.name != "BackgroundShapes" &&
                current.name != "Shadow" &&
                current.name != "CardShadow")
            {
                continue;
            }

            if (!current.gameObject.activeSelf)
                continue;

            Undo.RecordObject(current.gameObject, "Disable UI decor");
            current.gameObject.SetActive(false);
        }
    }

    private static void StyleImage(Image image)
    {
        if (image == null)
            return;

        Transform transform = image.transform;
        string path = Path(transform);
        string name = transform.name;

        if (path.Contains("BackgroundShapes"))
            return;

        if (IsPanelRoot(name))
            return;

        if (name == "ButtonLayot" || name == "ButtonLayout")
        {
            image.sprite = null;
            image.color = Color.clear;
            image.raycastTarget = false;

            return;
        }

        if (path.EndsWith("UpgradeSlots"))
        {
            image.sprite = null;
            image.color = Color.clear;
            image.raycastTarget = false;

            return;
        }

        if (path.Contains("CardFace/StateRow"))
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Pill,
                ArcadeUiPalette.SurfaceMuted,
                true
            );

            return;
        }

        if (path.Contains("MapsPanel/CardArea/CardRow/"))
        {
            if (name == "Background" ||
                name == "LandStripe" ||
                name == "Icon" ||
                name == "IconDisc" ||
                name == "LockOverlay")
            {
                return;
            }
        }

        if (name == "Frame")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Frame,
                path.Contains("MapsPanel/CardArea/CardRow/")
                    ? MapSelectionTheme.FrameIdle
                    : ArcadeUiPalette.Cyan,
                true
            );

            return;
        }

        if (name == "CardSurface")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Surface,
                ArcadeUiPalette.Surface,
                true
            );

            if (!image.TryGetComponent(out LayoutElement layout))
                layout = Undo.AddComponent<LayoutElement>(image.gameObject);

            layout.ignoreLayout = true;

            SetRect(
                image.rectTransform,
                Vector2.zero,
                Vector2.one,
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.zero
            );

            return;
        }

        if (name == "Surface" ||
            path.EndsWith("/Surface"))
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Surface,
                ArcadeUiPalette.Surface,
                true
            );

            return;
        }

        if (name == "IconArea" || name == "IconWash")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Surface,
                ArcadeUiPalette.Wash(ArcadeUiPalette.Cyan, 0.12f),
                true
            );

            return;
        }

        if (name == "IconDisc")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Circle,
                ArcadeUiPalette.Wash(ArcadeUiPalette.Cyan, 0.36f),
                false
            );

            return;
        }

        if (name == "IconMark")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Star,
                ArcadeUiPalette.Cyan,
                false
            );

            return;
        }

        if (name == "AccentLine" || name == "SelectedIndicator")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Bar,
                ArcadeUiPalette.Cyan,
                true
            );

            return;
        }

        if (name == "PageCounter")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Pill,
                ArcadeUiPalette.SurfaceMuted,
                true
            );

            return;
        }

        if (name == "EmptyState")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Surface,
                ArcadeUiPalette.SurfaceMuted,
                true
            );

            return;
        }

        if (name == "RowTemplate" ||
            name.StartsWith("Slot_") ||
            name.StartsWith("UpgradeCard_"))
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Surface,
                ArcadeUiPalette.Surface,
                true
            );

            return;
        }

        if (name == "LevelChip" || name == "RecordChip")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Pill,
                ArcadeUiPalette.Overlay,
                true
            );

            return;
        }

        if (name == "Selected")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Pill,
                ArcadeUiPalette.Cyan,
                true
            );

            return;
        }

        if (name == "CostBadge" || name == "PriceBadge")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Pill,
                ArcadeUiPalette.White(0.18f),
                true
            );

            return;
        }

        if (name == "CoinsSurface")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Pill,
                ArcadeUiPalette.SurfaceMuted,
                true
            );

            return;
        }

        if (name == "claimedOverlay")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Panel,
                ArcadeUiPalette.Overlay,
                true
            );

            return;
        }

        if (name == "ProgressBar" || name == "LevelBar")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Track,
                ArcadeUiPalette.Track,
                true
            );

            image.raycastTarget = false;

            return;
        }

        if (name == "Divider")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Bar,
                ArcadeUiPalette.Frame,
                true
            );

            return;
        }

        if (name == "Arrow")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Arrow,
                ArcadeUiPalette.Cyan,
                false
            );

            return;
        }

        if (name == "Checkmark")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Check,
                ArcadeUiPalette.Cyan,
                false
            );

            return;
        }

        if (name == "Handle")
        {
            SetSprite(
                image,
                path.Contains("Scrollbar")
                    ? ArcadeUiAtlas.Bar
                    : ArcadeUiAtlas.Circle,
                ArcadeUiPalette.Cyan,
                path.Contains("Scrollbar")
            );

            return;
        }

        if (name == "CoinIcon")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Coin,
                path.Contains("CostBadge") ||
                path.Contains("PriceBadge") ||
                path.Contains("BuyButton") ||
                path.Contains("ActionRow")
                    ? ArcadeUiPalette.BackgroundDeep
                    : ArcadeUiPalette.Gold,
                false
            );

            return;
        }

        if (name == "Fill")
        {
            SetBarSprite(
                image,
                ArcadeUiAtlas.Bar,
                FillColor(path)
            );

            return;
        }

        if (name == "Background")
        {
            if (path.Contains("DailyRewardPanel/Slot_"))
            {
                SetSprite(
                    image,
                    ArcadeUiAtlas.Surface,
                    ArcadeUiPalette.Surface,
                    true
                );

                return;
            }

            if (path.Contains("Toggle"))
            {
                SetSprite(
                    image,
                    ArcadeUiAtlas.Toggle,
                    ArcadeUiPalette.Track,
                    true
                );

                return;
            }

            SetSprite(
                image,
                ArcadeUiAtlas.Track,
                ArcadeUiPalette.Track,
                true
            );

            return;
        }

        if (name == "Item Background")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Pill,
                ArcadeUiPalette.SurfaceMuted,
                true
            );

            return;
        }

        if (name == "Item Checkmark")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Check,
                ArcadeUiPalette.Cyan,
                false
            );

            return;
        }

        if (name == "DimBackground")
        {
            SetOverlayImage(image);
            return;
        }

        if (name == "BossHpPanel" ||
            name == "CooldownUI" ||
            name == "LevelPanel" ||
            name == "ComboPanel" ||
            name == "PersonalBestHUD")
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Surface,
                new Color(
                    ArcadeUiPalette.Surface.r,
                    ArcadeUiPalette.Surface.g,
                    ArcadeUiPalette.Surface.b,
                    0.72f
                ),
                true
            );

            return;
        }

        if (name == "Image_Aim" ||
            name == "Image_Dash" ||
            name == "Image_Shoot" ||
            name == "Image_WASD")
        {
            image.color = ArcadeUiPalette.Cyan;
        }
    }

    private static void StyleText(TMP_Text text)
    {
        if (text == null)
            return;

        Transform transform = text.transform;
        string path = Path(transform);
        string name = transform.name;

        if (text.font == null)
            text.font = TMP_Settings.defaultFontAsset;

        text.raycastTarget = false;

        if (path.Contains("MapsPanel/CardArea/CardRow/") &&
            (name == "NameText" ||
             name == "MechanicText" ||
             name == "DescriptionText"))
        {
            if (name == "NameText")
                text.fontStyle |= FontStyles.Bold;

            return;
        }

        Color color = TextColor(name);

        text.color = color;

        if (IsBoldText(name))
            text.fontStyle |= FontStyles.Bold;

        if (name == "Title" && path.Contains("SettingPanelMini"))
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = 24f;
            text.fontSizeMax = 32f;
            text.alignment = TextAlignmentOptions.MidlineLeft;

            RectTransform titleRect = text.rectTransform;
            titleRect.anchoredPosition = new Vector2(
                -140f,
                titleRect.anchoredPosition.y
            );
        }

        if (name == "PersonalBestLabel")
        {
            text.alignment = TextAlignmentOptions.Left;
            text.fontSize = 24f;
        }
        else if (name == "PlayerNameText")
        {
            text.alignment = TextAlignmentOptions.Right;
            text.fontSize = 24f;
        }
        else if (name == "PlayerLevelText")
        {
            text.fontSize = 26f;
        }
        else if (name == "PlayerXpText")
        {
            text.fontSize = 16f;
        }
    }

    private static Color TextColor(string name)
    {
        if (name == "SubtitleText" ||
            name == "DescriptionText" ||
            name == "ProgressText" ||
            name == "PlayerXpText" ||
            name == "XpProgressText" ||
            name == "StatsText" ||
            name == "MechanicText" ||
            name == "Caption" ||
            name == "StateText" ||
            name == "RankTitleText" ||
            name == "ApproachText")
        {
            return ArcadeUiPalette.TextSecondary;
        }

        if (name == "CoinsText" ||
            name == "CoinsEarnedText" ||
            name == "CostText" ||
            name == "PriceText" ||
            name == "Coin" ||
            name == "RewardText" ||
            name == "PersonalBestLabel" ||
            name == "NewRecordText" ||
            name == "WavePbText" ||
            name == "waveCompleteText" ||
            name == "ComboMultiplierText")
        {
            return ArcadeUiPalette.Gold;
        }

        if (name == "ScoreText" ||
            name == "FinalScoreText" ||
            name == "ComboCountText" ||
            name == "CountdownText" ||
            name == "WaveCounter")
        {
            return ArcadeUiPalette.Cyan;
        }

        if (name == "LevelUpText")
            return ArcadeUiPalette.Lime;

        if (name == "BaseScoreText")
            return ArcadeUiPalette.TextSecondary;

        return ArcadeUiPalette.TextPrimary;
    }

    private static bool IsBoldText(string name)
    {
        return name == "Title" ||
               name == "TitleText" ||
               name == "GameOverText" ||
               name == "UpgradeTitle" ||
               name == "PlayerLevelText" ||
               name == "PlayerNameText" ||
               name == "ScoreText" ||
               name == "FinalScoreText" ||
               name == "CoinsText" ||
               name == "CoinsEarnedText" ||
               name == "WaveText" ||
               name == "WaveCounter" ||
               name == "BossName" ||
               name == "LevelUpText" ||
               name == "waveCompleteText" ||
               name == "CountdownText" ||
               name == "PrepareText" ||
               name == "NameText" ||
               name == "Name" ||
               name == "ValueText" ||
               name == "CostText" ||
               name == "PriceText" ||
               name == "RewardText" ||
               name == "RankText" ||
               name == "ComboCountText";
    }

    private static void StyleButton(Button button)
    {
        if (button == null)
            return;

        Transform transform = button.transform;
        string path = Path(transform);
        string name = transform.name;
        bool isPauseMini = path.Contains("PauseMiniPanel");
        bool isUpgradeCardButton =
            name == "Button" &&
            path.Contains("UpgradePanel/UpgradeSlots");

        ButtonKind kind = ButtonKind.Secondary;

        if (isUpgradeCardButton)
        {
            kind = ButtonKind.Card;
        }
        else if (name.StartsWith("Card_") ||
                 name == "CardSurface")
        {
            kind = ButtonKind.Card;
        }
        else if (name == "DotTemplate")
        {
            kind = ButtonKind.Dot;
        }
        else if (name == "RuButton" ||
                 name == "EnButton" ||
                 name == "TrButton")
        {
            kind = ButtonKind.Language;
        }
        else if (name == "Tab1" ||
                 name == "Tab2" ||
                 name == "Tab3")
        {
            kind = ButtonKind.Tab;
        }
        else if (name == "PreviousButton" ||
                 name == "NextButton" ||
                 name == "PauseButton")
        {
            kind = ButtonKind.Icon;
        }
        else if (name == "BuyButton" ||
                 name == "ClaimButton" ||
                 name == "ActionRow")
        {
            kind = ButtonKind.Accent;
        }
        else if (name == "StartButton" ||
                 name == "ResumeButton" ||
                 (name == "RestartButton" && !isPauseMini))
        {
            kind = ButtonKind.Primary;
        }

        Image image = button.targetGraphic as Image;

        if (image == null)
            image = button.GetComponent<Image>();

        if (kind != ButtonKind.Card && image != null)
        {
            Sprite sprite = ArcadeUiAtlas.Secondary;
            Color color = ArcadeUiPalette.SurfaceMuted;
            bool sliced = true;

            if (kind == ButtonKind.Primary)
            {
                sprite = ArcadeUiAtlas.Primary;
                color = ArcadeUiPalette.Cyan;
            }
            else if (kind == ButtonKind.Accent)
            {
                sprite = ArcadeUiAtlas.Primary;
                color = ArcadeUiPalette.Gold;
            }
            else if (kind == ButtonKind.Icon)
            {
                sprite = ArcadeUiAtlas.Circle;
                color = ArcadeUiPalette.Surface;
                sliced = false;
            }
            else if (kind == ButtonKind.Tab)
            {
                color = ArcadeUiPalette.SurfaceMuted;
            }
            else if (kind == ButtonKind.Language)
            {
                sprite = ArcadeUiAtlas.Pill;
                color = Color.white;
            }
            else if (kind == ButtonKind.Dot)
            {
                sprite = ArcadeUiAtlas.Circle;
                color = ArcadeUiPalette.Track;
                sliced = false;
            }

            SetSprite(image, sprite, color, sliced);
        }

        if (isUpgradeCardButton && image != null)
        {
            SetSprite(
                image,
                ArcadeUiAtlas.Surface,
                ArcadeUiPalette.SurfaceMuted,
                true
            );

            button.transform.SetAsFirstSibling();
        }

        if (name == "PauseButton" &&
            button.transform is RectTransform pauseRect)
        {
            pauseRect.sizeDelta = new Vector2(76f, 76f);
        }

        button.transition = Selectable.Transition.ColorTint;
        button.colors = ButtonColors(kind);

        if (isUpgradeCardButton)
            return;

        TMP_Text label = FindLabel(transform);

        if (label == null || kind == ButtonKind.Card)
            return;

        label.fontStyle |= FontStyles.Bold;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.enableAutoSizing = true;

        if (isPauseMini)
        {
            label.fontSizeMin = 18f;
            label.fontSizeMax = 22f;
        }
        else if (name == "StartButton" ||
                 name == "ResumeButton" ||
                 name == "RestartButton")
        {
            label.fontSizeMin = 26f;
            label.fontSizeMax = 44f;
        }
        else if (kind == ButtonKind.Icon)
        {
            label.fontSizeMin = 20f;
            label.fontSizeMax = 36f;
        }
        else if (kind == ButtonKind.Tab ||
                 kind == ButtonKind.Language)
        {
            label.fontSizeMin = 14f;
            label.fontSizeMax = 22f;
        }
        else
        {
            label.fontSizeMin = 16f;
            label.fontSizeMax = 28f;
        }

        if (kind == ButtonKind.Primary ||
            kind == ButtonKind.Accent)
        {
            label.color = ArcadeUiPalette.BackgroundDeep;
        }
        else if (kind == ButtonKind.Icon)
        {
            label.color = ArcadeUiPalette.Cyan;
        }
        else
        {
            label.color = ArcadeUiPalette.TextPrimary;
        }

        if (name == "PauseButton")
        {
            label.text = "II";
            label.enableAutoSizing = false;
            label.fontSize = 32f;
            label.characterSpacing = 4f;
            label.color = ArcadeUiPalette.Cyan;
            label.fontStyle |= FontStyles.Bold;
        }
    }

    private static void StyleLegacyText(Text text)
    {
        if (text == null)
            return;

        text.raycastTarget = false;
        text.color = ArcadeUiPalette.TextPrimary;
        text.fontSize = 16;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleLeft;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
    }

    private static ColorBlock ButtonColors(ButtonKind kind)
    {
        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;

        if (kind == ButtonKind.Language)
        {
            colors.normalColor = ArcadeUiPalette.Surface;
            colors.highlightedColor = ArcadeUiPalette.SurfaceMuted;
            colors.pressedColor = ArcadeUiPalette.SurfaceDisabled;
            colors.selectedColor = ArcadeUiPalette.SurfaceMuted;
            colors.disabledColor = new Color(0.14f, 0.58f, 0.68f, 1f);
            return colors;
        }

        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.18f, 1.28f, 1.42f, 1f);
        colors.pressedColor = new Color(0.78f, 0.84f, 0.94f, 1f);
        colors.selectedColor = Color.white;

        if (kind == ButtonKind.Primary ||
            kind == ButtonKind.Accent)
        {
            colors.highlightedColor = new Color(1.14f, 1.14f, 1.14f, 1f);
            colors.pressedColor = new Color(0.82f, 0.88f, 0.96f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.55f);
        }
        else if (kind == ButtonKind.Card)
        {
            colors.highlightedColor = new Color(1.06f, 1.10f, 1.18f, 1f);
            colors.pressedColor = new Color(0.90f, 0.94f, 1f, 1f);
            colors.disabledColor = new Color(0.52f, 0.52f, 0.55f, 0.55f);
        }
        else if (kind == ButtonKind.Icon)
        {
            colors.disabledColor = new Color(0.40f, 0.45f, 0.55f, 0.6f);
        }
        else
        {
            colors.disabledColor = new Color(0.45f, 0.50f, 0.62f, 0.6f);
        }

        return colors;
    }

    private static void StyleToggle(Toggle toggle)
    {
        if (toggle == null)
            return;

        if (Path(toggle.transform).Contains("RunModifierDropdownPanelAnchor"))
            return;

        bool isSettingsToggle = Path(toggle.transform).Contains(
            "SettingPanelMini"
        );

        Image background = toggle.targetGraphic as Image;

        if (background != null)
        {
            SetSprite(
                background,
                ArcadeUiAtlas.Toggle,
                ArcadeUiPalette.Track,
                true
            );

            if (isSettingsToggle)
            {
                background.rectTransform.anchoredPosition = new Vector2(
                    12f,
                    -12f
                );
                background.rectTransform.sizeDelta = new Vector2(24f, 24f);
            }
        }

        if (toggle.graphic is Image checkmark)
        {
            SetSprite(
                checkmark,
                ArcadeUiAtlas.Check,
                ArcadeUiPalette.Cyan,
                false
            );

            checkmark.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            checkmark.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            checkmark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            checkmark.rectTransform.anchoredPosition = Vector2.zero;
            checkmark.rectTransform.sizeDelta = new Vector2(
                isSettingsToggle ? 20f : 18f,
                isSettingsToggle ? 20f : 18f
            );
        }

        if (isSettingsToggle)
        {
            Text legacyLabel = toggle.GetComponentInChildren<Text>(true);

            if (legacyLabel != null)
            {
                RectTransform labelRect = legacyLabel.rectTransform;
                labelRect.offsetMin = new Vector2(
                    36f,
                    labelRect.offsetMin.y
                );
                labelRect.offsetMax = new Vector2(
                    -8f,
                    labelRect.offsetMax.y
                );
            }
        }

        toggle.transition = Selectable.Transition.ColorTint;
        toggle.colors = ButtonColors(ButtonKind.Secondary);

        TMP_Text label = FindLabel(toggle.transform);

        if (label != null)
        {
            label.color = ArcadeUiPalette.TextPrimary;
            label.fontStyle |= FontStyles.Bold;
        }
    }

    private static void StyleDropdown(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
            return;

        Image target = dropdown.targetGraphic as Image;

        if (target == null)
            target = dropdown.GetComponent<Image>();

        if (target != null)
        {
            SetSprite(
                target,
                ArcadeUiAtlas.Secondary,
                ArcadeUiPalette.Surface,
                true
            );
        }

        dropdown.transition = Selectable.Transition.ColorTint;
        dropdown.colors = ButtonColors(ButtonKind.Secondary);

        if (dropdown.captionText != null)
        {
            dropdown.captionText.fontStyle |= FontStyles.Bold;
            dropdown.captionText.color = ArcadeUiPalette.TextPrimary;
            dropdown.captionText.enableAutoSizing = true;
            dropdown.captionText.fontSizeMin = 16f;
            dropdown.captionText.fontSizeMax = 24f;
        }

        Transform arrow = FindDescendant(dropdown.transform, "Arrow");

        if (arrow != null && arrow.TryGetComponent(out Image arrowImage))
        {
            SetSprite(
                arrowImage,
                ArcadeUiAtlas.Arrow,
                ArcadeUiPalette.Cyan,
                false
            );
        }

        if (dropdown.template != null)
        {
            FixDropdownTemplate(dropdown.template);

            Image templateImage =
                dropdown.template.GetComponent<Image>();

            if (templateImage != null)
            {
                SetSprite(
                    templateImage,
                    ArcadeUiAtlas.Panel,
                    ArcadeUiPalette.PanelBackground,
                    true
                );
            }
        }

        if (dropdown.template != null)
        {
            Transform itemBackground = FindDescendant(
                dropdown.template,
                "Item Background"
            );

            if (itemBackground != null &&
                itemBackground.TryGetComponent(out Image itemBackgroundImage))
            {
                SetSprite(
                    itemBackgroundImage,
                    ArcadeUiAtlas.Pill,
                    ArcadeUiPalette.SurfaceMuted,
                    true
                );
            }

            Transform itemCheckmark = FindDescendant(
                dropdown.template,
                "Item Checkmark"
            );

            if (itemCheckmark != null &&
                itemCheckmark.TryGetComponent(out Image itemCheckmarkImage))
            {
                SetSprite(
                    itemCheckmarkImage,
                    ArcadeUiAtlas.Check,
                    ArcadeUiPalette.Cyan,
                    false
                );
            }

            Transform scrollbar = FindDescendant(
                dropdown.template,
                "Scrollbar"
            );

            if (scrollbar is RectTransform scrollbarRect)
            {
                scrollbarRect.anchorMin = new Vector2(1f, 0f);
                scrollbarRect.anchorMax = new Vector2(1f, 1f);
                scrollbarRect.pivot = new Vector2(1f, 0.5f);
                scrollbarRect.anchoredPosition = new Vector2(-2f, 0f);
                scrollbarRect.sizeDelta = new Vector2(12f, -16f);

                if (scrollbar.TryGetComponent(out Image scrollbarImage))
                {
                    SetSprite(
                        scrollbarImage,
                        ArcadeUiAtlas.Pill,
                        ArcadeUiPalette.Track,
                        true
                    );
                }
            }

            Transform handle = FindDescendant(
                dropdown.template,
                "Handle"
            );

            if (handle != null &&
                handle.TryGetComponent(out Image handleImage))
            {
                SetSprite(
                    handleImage,
                    ArcadeUiAtlas.Pill,
                    ArcadeUiPalette.Cyan,
                    true
                );

                handleImage.raycastTarget = false;

                if (handle is RectTransform handleRect)
                    handleRect.sizeDelta = new Vector2(-6f, -8f);
            }

            dropdown.template.gameObject.SetActive(false);
        }

        if (dropdown.itemText != null)
        {
            dropdown.itemText.color = ArcadeUiPalette.TextPrimary;
            dropdown.itemText.fontSize = 22f;
            dropdown.itemText.enableAutoSizing = false;
            dropdown.itemText.alignment = TextAlignmentOptions.Left;
        }
    }

    private static void FixDropdownTemplate(RectTransform template)
    {
        if (template == null)
            return;

        Transform viewport = FindDescendant(template, "Viewport");

        if (viewport is RectTransform viewportRect)
        {
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.pivot = new Vector2(0.5f, 0.5f);
            viewportRect.anchoredPosition = Vector2.zero;
            viewportRect.sizeDelta = Vector2.zero;
            viewportRect.offsetMin = new Vector2(8f, 8f);
            viewportRect.offsetMax = new Vector2(-24f, -8f);

            if (viewport.TryGetComponent(out Image viewportImage))
            {
                SetSprite(
                    viewportImage,
                    ArcadeUiAtlas.Surface,
                    Color.white,
                    true
                );

                viewportImage.raycastTarget = false;
            }

            if (viewport.TryGetComponent(out Mask viewportMask))
                viewportMask.showMaskGraphic = false;
        }

        Transform content = FindDescendant(template, "Content");

        if (content is RectTransform contentRect)
        {
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = Vector2.zero;
        }

        Transform item = FindDescendant(template, "Item");

        if (item is RectTransform itemRect)
        {
            itemRect.anchorMin = new Vector2(0f, 1f);
            itemRect.anchorMax = new Vector2(1f, 1f);
            itemRect.pivot = new Vector2(0.5f, 1f);
            itemRect.anchoredPosition = Vector2.zero;
            itemRect.sizeDelta = new Vector2(0f, 46f);
        }

        Transform itemBackground = FindDescendant(
            template,
            "Item Background"
        );

        if (itemBackground is RectTransform itemBackgroundRect)
        {
            itemBackgroundRect.anchorMin = Vector2.zero;
            itemBackgroundRect.anchorMax = Vector2.one;
            itemBackgroundRect.pivot = new Vector2(0.5f, 0.5f);
            itemBackgroundRect.anchoredPosition = Vector2.zero;
            itemBackgroundRect.sizeDelta = Vector2.zero;
        }

        Transform itemCheckmark = FindDescendant(
            template,
            "Item Checkmark"
        );

        if (itemCheckmark is RectTransform checkmarkRect)
        {
            checkmarkRect.anchorMin = new Vector2(0f, 0.5f);
            checkmarkRect.anchorMax = new Vector2(0f, 0.5f);
            checkmarkRect.pivot = new Vector2(0.5f, 0.5f);
            checkmarkRect.anchoredPosition = new Vector2(18f, 0f);
            checkmarkRect.sizeDelta = new Vector2(18f, 18f);
        }

        Transform itemLabel = FindDescendant(template, "Item Label");

        if (itemLabel is RectTransform itemLabelRect)
        {
            itemLabelRect.anchorMin = Vector2.zero;
            itemLabelRect.anchorMax = Vector2.one;
            itemLabelRect.pivot = new Vector2(0.5f, 0.5f);
            itemLabelRect.anchoredPosition = Vector2.zero;
            itemLabelRect.sizeDelta = Vector2.zero;
            itemLabelRect.offsetMin = new Vector2(44f, 4f);
            itemLabelRect.offsetMax = new Vector2(-12f, -4f);

            if (itemLabel.TryGetComponent(out TMP_Text itemLabelText))
            {
                itemLabelText.color = ArcadeUiPalette.TextPrimary;
                itemLabelText.fontSize = 22f;
                itemLabelText.enableAutoSizing = false;
                itemLabelText.alignment = TextAlignmentOptions.Left;
                itemLabelText.fontStyle |= FontStyles.Bold;
            }
        }

        Transform scrollbar = FindDescendant(template, "Scrollbar");

        if (scrollbar is RectTransform scrollbarRect)
        {
            scrollbarRect.anchorMin = new Vector2(1f, 0f);
            scrollbarRect.anchorMax = new Vector2(1f, 1f);
            scrollbarRect.pivot = new Vector2(1f, 0.5f);
            scrollbarRect.anchoredPosition = new Vector2(-2f, 0f);
            scrollbarRect.sizeDelta = new Vector2(12f, -16f);
        }

        Transform slidingArea = FindDescendant(template, "Sliding Area");

        if (slidingArea is RectTransform slidingRect)
        {
            slidingRect.anchorMin = Vector2.zero;
            slidingRect.anchorMax = Vector2.one;
            slidingRect.pivot = new Vector2(0.5f, 0.5f);
            slidingRect.anchoredPosition = Vector2.zero;
            slidingRect.sizeDelta = Vector2.zero;
        }
    }

    private static void ApplyPanelBackgrounds(
        Transform menuRoot,
        GameObject gameCanvas)
    {
        Color subPanelTint = new Color(
            ArcadeUiPalette.PanelBackground.r,
            ArcadeUiPalette.PanelBackground.g,
            ArcadeUiPalette.PanelBackground.b,
            0.86f
        );

        SetPanel(
            menuRoot,
            "Menu",
            null,
            new Color(
                ArcadeUiPalette.Background.r,
                ArcadeUiPalette.Background.g,
                ArcadeUiPalette.Background.b,
                0.2f
            ),
            false
        );

        SetPanel(
            menuRoot,
            "UpgradesPanel",
            null,
            subPanelTint,
            false
        );

        SetPanel(
            menuRoot,
            "EquipmentPanel",
            null,
            subPanelTint,
            false
        );

        SetPanel(
            menuRoot,
            "MapsPanel",
            null,
            subPanelTint,
            false
        );

        SetPanel(
            menuRoot,
            "AchievementsPanel",
            null,
            subPanelTint,
            false
        );

        SetPanel(
            menuRoot,
            "DailyMissionsPanel",
            null,
            subPanelTint,
            false
        );

        SetPanel(
            menuRoot,
            "DailyRewardPanel",
            null,
            subPanelTint,
            false
        );

        SetPanel(
            menuRoot,
            "LevelPanel",
            ArcadeUiAtlas.Panel,
            new Color(
                ArcadeUiPalette.Surface.r,
                ArcadeUiPalette.Surface.g,
                ArcadeUiPalette.Surface.b,
                0.94f
            ),
            true
        );

        if (gameCanvas == null)
            return;

        Transform canvas = gameCanvas.transform;

        SetPanel(
            canvas,
            "GameOverPanel",
            ArcadeUiAtlas.Panel,
            new Color(
                ArcadeUiPalette.Surface.r,
                ArcadeUiPalette.Surface.g,
                ArcadeUiPalette.Surface.b,
                0.98f
            ),
            true
        );

        SetPanel(
            canvas,
            "LeaderboardPanel",
            ArcadeUiAtlas.Panel,
            new Color(
                ArcadeUiPalette.Surface.r,
                ArcadeUiPalette.Surface.g,
                ArcadeUiPalette.Surface.b,
                0.97f
            ),
            true
        );

        SetOverlay(canvas, "PausePanel");
        SetOverlay(canvas, "SettingPanel");
        HideOverlay(canvas, "PausePanel");
        HideOverlay(canvas, "SettingPanel");

        SetPanel(
            canvas,
            "PauseMiniPanel",
            ArcadeUiAtlas.Panel,
            ArcadeUiPalette.Surface,
            true
        );

        SetPanel(
            canvas,
            "SettingPanelMini",
            ArcadeUiAtlas.Panel,
            ArcadeUiPalette.Surface,
            true
        );

        SetPanel(
            canvas,
            "UpgradePanel",
            ArcadeUiAtlas.Panel,
            ArcadeUiPalette.Surface,
            true
        );

        Transform dim = FindDescendant(canvas, "DimBackground");

        if (dim != null && dim.TryGetComponent(out Image dimImage))
            SetOverlayImage(dimImage);

        Transform wave = FindDescendant(canvas, "WavePanel");

        if (wave != null && wave.TryGetComponent(out Image waveImage))
        {
            waveImage.sprite = null;
            waveImage.color = Color.clear;
            waveImage.raycastTarget = false;
        }
    }

    private static void StyleTopBars(Transform menuRoot)
    {
        Transform topBar = FindDescendant(menuRoot, "UpgradesPanel");

        if (topBar != null)
            StyleTopBar(topBar);

        topBar = FindDescendant(menuRoot, "EquipmentPanel");

        if (topBar != null)
            StyleTopBar(topBar);
    }

    private static void StyleTopBar(Transform panel)
    {
        Transform bar = FindDirect(panel, "TopBar");

        if (bar == null)
            return;

        if (bar.TryGetComponent(out Image oldImage))
        {
            oldImage.sprite = null;
            oldImage.color = Color.clear;
            oldImage.raycastTarget = false;
        }

        Transform plate = FindDirect(bar, "ArcadeUi_TopBarPlate");

        if (plate == null)
        {
            plate = CreateImage(
                "ArcadeUi_TopBarPlate",
                bar,
                ArcadeUiAtlas.Panel,
                ArcadeUiPalette.Surface
            ).transform;
        }

        plate.SetSiblingIndex(0);

        if (!plate.TryGetComponent(out Image image))
            image = Undo.AddComponent<Image>(plate.gameObject);

        image.sprite = ArcadeUiAtlas.Panel;
        image.type = Image.Type.Sliced;
        image.color = new Color(
            ArcadeUiPalette.Surface.r,
            ArcadeUiPalette.Surface.g,
            ArcadeUiPalette.Surface.b,
            0.52f
        );
        image.raycastTarget = false;
        image.pixelsPerUnitMultiplier = 1f;

        SetRect(
            plate as RectTransform,
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(0.5f, 1f),
            Vector2.zero,
            new Vector2(0f, 112f)
        );
    }

    private static void StyleBrand(Transform menuRoot)
    {
        Transform brand = FindDirect(menuRoot, "ArcadeUi_Brand");

        if (brand == null)
            return;

        Transform title = FindDirect(brand, "BrandTitle");

        if (title != null && title.TryGetComponent(out TMP_Text titleText))
        {
            titleText.color = ArcadeUiPalette.TextPrimary;
            titleText.fontStyle |= FontStyles.Bold;
            titleText.fontSize = 76f;
            titleText.characterSpacing = 2f;
        }

        Transform subtitle = FindDirect(brand, "BrandSubtitle");

        if (subtitle != null &&
            subtitle.TryGetComponent(out TMP_Text subtitleText))
        {
            subtitleText.color = ArcadeUiPalette.Cyan;
            subtitleText.fontStyle |= FontStyles.Bold;
            subtitleText.fontSize = 22f;
            subtitleText.characterSpacing = 16f;
        }

        Transform accent = FindDirect(brand, "BrandAccent");

        if (accent != null && accent.TryGetComponent(out Image accentImage))
        {
            accentImage.sprite = ArcadeUiAtlas.Bar;
            accentImage.type = Image.Type.Sliced;
            accentImage.color = ArcadeUiPalette.Magenta;
        }
    }

    private static void StyleMainMenuDetails(Transform menuRoot)
    {
        TMP_Text personalBest = FindText(menuRoot, "PersonalBestLabel");

        if (personalBest != null)
        {
            personalBest.color = ArcadeUiPalette.Gold;
            personalBest.fontStyle |= FontStyles.Bold;
        }

        TMP_Text playerName = FindText(menuRoot, "PlayerNameText");

        if (playerName != null)
            playerName.alignment = TextAlignmentOptions.Right;

        Transform startButton = FindDirect(menuRoot, "StartButton");

        if (startButton != null)
        {
            TMP_Text startLabel = FindLabel(startButton);

            if (startLabel != null)
                startLabel.characterSpacing = 4f;
        }
    }

    private static void StyleGameplayDetails(GameObject gameCanvas)
    {
        if (gameCanvas == null)
            return;

        Transform canvas = gameCanvas.transform;
        Transform tutorial = FindDescendant(canvas, "TutorialPanel");

        if (tutorial == null)
            return;

        string[] names =
        {
            "Image_Aim",
            "Image_Dash",
            "Image_Shoot",
            "Image_WASD"
        };

        for (int i = 0; i < names.Length; i++)
        {
            Transform imageTransform =
                FindDescendant(tutorial, names[i]);

            if (imageTransform != null &&
                imageTransform.TryGetComponent(out Image image))
            {
                image.color = ArcadeUiPalette.Cyan;
            }
        }
    }

    private static void UpgradeDailyMissionBars(Transform menuRoot)
    {
        DailyMissionRowUI[] rows =
            menuRoot.GetComponentsInChildren<DailyMissionRowUI>(true);

        for (int i = 0; i < rows.Length; i++)
        {
            DailyMissionRowUI row = rows[i];

            if (row == null)
                continue;

            SerializedObject rowObject = new SerializedObject(row);
            SerializedProperty barProperty =
                rowObject.FindProperty("progressBar");

            if (barProperty == null ||
                barProperty.objectReferenceValue != null)
            {
                continue;
            }

            Transform oldFill = FindDescendant(row.transform, "ProgressFill");

            if (oldFill == null ||
                !oldFill.TryGetComponent(out Image oldImage))
            {
                continue;
            }

            Slider bar = ConvertToProgressBar(oldFill, oldImage);

            barProperty.objectReferenceValue = bar;
            rowObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static Slider ConvertToProgressBar(
        Transform oldFill,
        Image oldImage)
    {
        GameObject root = oldFill.gameObject;

        Undo.RegisterFullObjectHierarchyUndo(
            root,
            "Arcade UI - Progress Bar"
        );

        root.name = "ProgressBar";

        oldImage.sprite = null;
        oldImage.type = Image.Type.Simple;
        oldImage.color = new Color(1f, 1f, 1f, 0.12f);
        oldImage.raycastTarget = false;

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform)
        );

        Undo.RegisterCreatedObjectUndo(
            fillObject,
            "Arcade UI - Progress Fill"
        );

        fillObject.transform.SetParent(root.transform, false);

        Image fillImage = fillObject.AddComponent<Image>();

        fillImage.color = ArcadeUiPalette.Lime;
        fillImage.raycastTarget = false;

        RectTransform fillRect = fillImage.rectTransform;

        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        Slider slider = Undo.AddComponent<Slider>(root.gameObject);

        slider.fillRect = fillRect;
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

    private static void SetPanel(
        Transform root,
        string name,
        Sprite sprite,
        Color color,
        bool sliced)
    {
        Transform panel = FindDescendant(root, name);

        if (panel == null || !panel.TryGetComponent(out Image image))
            return;

        SetSprite(image, sprite, color, sliced);
    }

    private static void SetOverlay(Transform root, string name)
    {
        Transform panel = FindDescendant(root, name);

        if (panel != null && panel.TryGetComponent(out Image image))
            SetOverlayImage(image);
    }

    private static void SetOverlayImage(Image image)
    {
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = ArcadeUiPalette.Overlay;
        image.raycastTarget = true;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private static void HideOverlay(Transform root, string name)
    {
        Transform overlay = FindDescendant(root, name);

        if (overlay != null)
            overlay.gameObject.SetActive(false);
    }

    private static void SetSprite(
        Image image,
        Sprite sprite,
        Color color,
        bool sliced)
    {
        image.sprite = sprite;
        image.color = color;
        image.type = sliced &&
                     sprite != null &&
                     sprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = 1f;
    }

    private static void SetBarSprite(
        Image image,
        Sprite sprite,
        Color color)
    {
        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null && sprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillAmount = 1f;
        image.preserveAspect = false;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = false;
    }

    private static Color FillColor(string path)
    {
        if (path.Contains("HealthSlider"))
            return ArcadeUiPalette.Lime;

        if (path.Contains("BossHpSlider"))
            return ArcadeUiPalette.Magenta;

        if (path.Contains("DashSlider"))
            return ArcadeUiPalette.Gold;

        if (path.Contains("FireSlider"))
            return ArcadeUiPalette.Magenta;

        if (path.Contains("DailyMissionsPanel"))
            return ArcadeUiPalette.Lime;

        return ArcadeUiPalette.Cyan;
    }

    private static bool IsPanelRoot(string name)
    {
        return name == "Menu" ||
               name == "UpgradesPanel" ||
               name == "EquipmentPanel" ||
               name == "MapsPanel" ||
               name == "AchievementsPanel" ||
               name == "DailyMissionsPanel" ||
               name == "DailyRewardPanel" ||
               name == "GameOverPanel" ||
               name == "LeaderboardPanel" ||
               name == "PausePanel" ||
               name == "PauseMiniPanel" ||
               name == "SettingPanel" ||
               name == "SettingPanelMini" ||
               name == "UpgradePanel" ||
               name == "WavePanel" ||
               name == "DimBackground";
    }

    private static TMP_Text FindLabel(Transform parent)
    {
        TMP_Text[] texts = parent.GetComponentsInChildren<TMP_Text>(true);

        return texts.Length > 0 ? texts[0] : null;
    }

    private static TMP_Text FindText(Transform root, string name)
    {
        Transform target = FindDescendant(root, name);

        return target != null && target.TryGetComponent(out TMP_Text text)
            ? text
            : null;
    }

    private static Transform FindDirect(Transform parent, string name)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);

            if (child.name == name)
                return child;
        }

        return null;
    }

    private static Transform FindDescendant(Transform root, string name)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == name)
                return all[i];
        }

        return null;
    }

    private static List<T> Collect<T>(Transform root) where T : Component
    {
        T[] all = Object.FindObjectsByType<T>(
            FindObjectsInactive.Include
        );

        List<T> result = new List<T>();

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].transform.IsChildOf(root))
                result.Add(all[i]);
        }

        return result;
    }

    private static void Audit(
        Transform menuRoot,
        Transform gameRoot,
        out int imageCount,
        out int atlasImageCount,
        out int textureCount)
    {
        imageCount = 0;
        atlasImageCount = 0;

        HashSet<Texture> textures = new HashSet<Texture>();
        Texture atlasTexture = ArcadeUiAtlas.Panel.texture;

        CountImages(
            menuRoot,
            atlasTexture,
            textures,
            ref imageCount,
            ref atlasImageCount
        );

        if (gameRoot != null)
        {
            CountImages(
                gameRoot,
                atlasTexture,
                textures,
                ref imageCount,
                ref atlasImageCount
            );
        }

        textureCount = textures.Count;
    }

    private static void CountImages(
        Transform root,
        Texture atlasTexture,
        HashSet<Texture> textures,
        ref int imageCount,
        ref int atlasImageCount)
    {
        List<Image> images = Collect<Image>(root);

        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];

            if (image == null)
                continue;

            imageCount++;

            if (image.sprite == null)
                continue;

            Texture texture = image.sprite.texture;

            if (texture == null)
                continue;

            textures.Add(texture);

            if (texture == atlasTexture)
                atlasImageCount++;
        }
    }

    private static void DestroyChrome(Transform menuRoot, string name)
    {
        Transform existing = FindDirect(menuRoot, name);

        if (existing != null)
            Undo.DestroyObjectImmediate(existing.gameObject);
    }

    private static RectTransform CreateRect(
        string name,
        Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));

        Undo.RegisterCreatedObjectUndo(go, "Create Arcade UI");
        go.transform.SetParent(parent, false);

        return go.GetComponent<RectTransform>();
    }

    private static Image CreateImage(
        string name,
        Transform parent,
        Sprite sprite,
        Color color)
    {
        GameObject go = new GameObject(
            name,
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(Image)
        );

        Undo.RegisterCreatedObjectUndo(go, "Create Arcade UI");
        go.transform.SetParent(parent, false);

        Image image = go.GetComponent<Image>();

        image.sprite = sprite;
        image.color = color;
        image.type = sprite != null &&
                     sprite.border.sqrMagnitude > 0f
            ? Image.Type.Sliced
            : Image.Type.Simple;
        image.raycastTarget = false;
        image.pixelsPerUnitMultiplier = 1f;

        return image;
    }

    private static TMP_Text CreateText(
        string name,
        Transform parent,
        string content,
        float size,
        TextAlignmentOptions alignment,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));

        Undo.RegisterCreatedObjectUndo(go, "Create Arcade UI");
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        text.font = TMP_Settings.defaultFontAsset;
        text.text = content;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        return text;
    }

    private static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        if (rect == null)
            return;

        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static string Path(Transform transform)
    {
        StringBuilder builder = new StringBuilder(transform.name);

        Transform current = transform.parent;

        while (current != null)
        {
            builder.Insert(0, current.name + "/");
            current = current.parent;
        }

        return builder.ToString();
    }

    private static void Show(string message)
    {
        if (Application.isBatchMode)
        {
            Debug.Log(message);
            return;
        }

        EditorUtility.DisplayDialog(DialogTitle, message, "OK");
    }
}

#endif
