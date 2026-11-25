using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed partial class ArcadeMenuSkin
{
    // Experimental menu only. Turn this off to restore the previous layout and camera.
    public static readonly bool CampMenuLayoutEnabled = true;

    private const float CampSidebarWidth = 748f;
    private static readonly Vector2 CampTopLeft = new Vector2(0f, 1f);
    private static readonly Color CampPaper = ArcadeUiTheme.Rgb(0xF4EAD6);
    private static readonly Color CampButtonColor = ArcadeUiTheme.Rgb(0xDBD6BC);
    private static readonly Color CampGold = ArcadeUiTheme.Rgb(0xDDA95A);
    private static readonly Color CampInk = ArcadeUiTheme.Rgb(0x202723);
    private static readonly Color CampDark = ArcadeUiTheme.Rgb(0x293630);
    private static readonly Color CampQuiet = ArcadeUiTheme.Rgb(0xA8A690);
    private static readonly Color CampLeaf = ArcadeUiTheme.Rgb(0x78864A);

    private RectTransform campBoard;
    private RectTransform campSidebar;
    private RectTransform campSafeArea;
    private RectTransform campHeaderFrame;
    private RectTransform campMapFrame;
    private RectTransform campExtrasFrame;
    private RectTransform campExtrasSheet;
    private bool? campExtrasPortrait;
    private TMP_Text campLevelText;
    private Image campMapIcon;
    private GameObject campExtras;

    private static Sprite campCutSprite;
    private static Sprite campSidebarSprite;
    private static Sprite campGunSprite;
    private static Sprite campUpgradesSprite;
    private static Sprite campMapSprite;
    private static Sprite campLeafSprite;

    private void BuildCampMenuLegacy(RectTransform board)
    {
        campSafeArea = board;
        campBoard = CampRect(campSafeArea, "CampSidebarContent", 0, 0, CampSidebarWidth, 1080);
        board = campBoard;
        campSidebar = CampRect(rootRect, "CampSidebar", 0, 0, CampSidebarWidth, 1080);
        ArcadeUiFactory.Image(campSidebar, CampCut(true), CampPaper, true).raycastTarget = true;
        campSidebar.SetAsFirstSibling();

        TMP_Text logo = CampLabel(board, "Wordmark", 37, 77, 674, 112,
            "BULLETRUSH", 99, CampInk, ArcadeKitAssets.Display, TextAlignmentOptions.Center);
        logo.fontStyle = FontStyles.Italic;
        logo.characterSpacing = -1.5f;
        CampSolid(board, "BrandAccent", 431, 184, 62, 16, CampGold).rectTransform.localRotation = Quaternion.Euler(0, 0, -10);

        BuildCampRunOptions(board);

        playButton = CampAction(board, "PlayButton", 34, 278, 680, 146, CampGold,
            ArcadeKitAssets.Icon("play"), () => CampText("Играть", "Play", "Oyna"), Play, 76, 90);
        playButton.GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Italic;

        equipmentCard = new RailCard
        {
            Button = CampAction(board, "EquipmentCard", 34, 470, 680, 106, CampButtonColor,
                CampGun(), () => CampText("Снаряжение", "Equipment", "Ekipman"), OpenEquipment, 50, 100)
        };
        upgradesCard = new RailCard
        {
            Button = CampAction(board, "UpgradesCard", 34, 606, 680, 106, CampButtonColor,
                CampUpgrades(), () => CampText("Улучшения", "Upgrades", "Geliştirmeler"), OpenUpgrades, 50, 90)
        };
        CampAction(board, "MapsButton", 34, 742, 680, 106, CampButtonColor,
            CampMap(), () => CampText("Карты", "Maps", "Haritalar"), OpenMaps, 50, 90);

        dailyCard = new RailCard
        {
            Button = CampAction(board, "DailyReward", 34, 874, 332, 56, CampButtonColor,
                ArcadeKitAssets.Icon("gift"), () => CampText("Награды", "Rewards", "Ödüller"), OpenDaily, 27, 34)
        };
        dailyCard.Root = dailyCard.Button.gameObject;
        dailyCard.Badge = CampBadge(dailyCard.Button.transform);
        CampAction(board, "MoreButton", 382, 874, 332, 56, CampButtonColor,
            ArcadeKitAssets.Icon("plus"), () => CampText("Ещё", "More", "Daha fazla"), OpenCampExtras, 27, 34);

        missionsTile = CampTile(board, "MissionsTile", 34, 950, 304, 82,
            ArcadeKitAssets.Icon("scroll"), () => CampText("Задания", "Missions", "Görevler"), OpenMissions);
        achievementsTile = CampTile(board, "AchievementsTile", 370, 950, 344, 82,
            ArcadeKitAssets.Icon("trophy"), () => CampText("Достижения", "Achievements", "Başarımlar"), OpenAchievements);

        campHeaderFrame = ArcadeUiFactory.At(campSafeArea, "HeaderFrame", Vector2.one, Vector2.one,
            Vector2.zero, new Vector2(504,84));
        campMapFrame = ArcadeUiFactory.At(campSafeArea, "MapFrame", new Vector2(1,0), new Vector2(1,0),
            Vector2.zero, new Vector2(363,90));
        BuildCampHeader(campHeaderFrame);
        BuildCampMapChip(campMapFrame);
        BuildCampExtras(board);
        FitCampSidebar();
    }

    private void FitCampSidebar()
    {
        if (hubBoard != null) { FitHubLayout(); return; }
        if (campSidebar == null || campBoard == null || campSafeArea == null ||
            campHeaderFrame == null || campMapFrame == null) return;
        Vector2 available = campSafeArea.rect.size;
        if (available.x <= 0 || available.y <= 0) return;
        bool portrait = available.x < available.y;
        float scale = portrait
            ? ResponsiveUi.Fit(available, new Vector2(780, 1360), true)
            : ResponsiveUi.Fit(available, new Vector2(1320, 1080), true);
        Vector2 upper = portrait ? new Vector2(.5f, .5f) : new Vector2(0, .5f);
        SetCampFrame(campBoard, upper, upper, Vector2.zero, scale);
        SetCampFrame(campHeaderFrame, Vector2.one, Vector2.one, new Vector2(-22, -20) * scale, scale);
        Vector2 bottom = portrait ? new Vector2(.5f,0) : new Vector2(1,0);
        SetCampFrame(campMapFrame, bottom, bottom, new Vector2(portrait ? 0 : -22, 40) * scale, scale);

        // Landscape keeps the paper flush with the left edge, even on ultrawide.
        // Portrait keeps a centered paper sheet with the scene visible above/below.
        Rect safe = ResponsiveUi.SafeRect(rootRect);
        SetCampFrame(campSidebar, portrait ? Vector2.one*.5f : CampTopLeft,
            portrait ? Vector2.one*.5f : CampTopLeft, portrait ? safe.center-rootRect.rect.center : Vector2.zero, 1);
        float width = portrait ? Mathf.Min(safe.width, (CampSidebarWidth+12)*scale)
            : safe.xMin - rootRect.rect.xMin + CampSidebarWidth * scale;
        Vector2 size = new Vector2(width, portrait ? 1080*scale : rootRect.rect.height);
        if (campSidebar.sizeDelta != size) campSidebar.sizeDelta = size;
        FitCampExtras(portrait);
    }

    private void FitCampExtras(bool portrait)
    {
        if (campExtrasFrame == null || campExtrasSheet == null || campExtrasPortrait == portrait) return;
        campExtrasPortrait = portrait;
        UiArtboard.Resize(campExtrasFrame, portrait ? new Vector2(976,1000) : new Vector2(1424,764));
        campExtrasSheet.sizeDelta = portrait ? new Vector2(928,952) : new Vector2(1376,716);
        string[] names = { "Stats","Mastery","Leaderboards","Tips","Journey","CoinsAd","WeaponAd","Settings","Exit" };
        for (int i = 0; i < names.Length; i++)
        {
            if (!(campExtrasSheet.Find(names[i]) is RectTransform action)) continue;
            float x = portrait ? 34+(i%2)*436 : 40+(i%3)*436;
            float y = portrait ? 120+(i/2)*140 : 130+(i/3)*156;
            action.anchoredPosition = new Vector2(x,-y);
        }
        if (campExtrasSheet.Find("Title") is RectTransform title)
            title.sizeDelta = new Vector2(portrait ? 770 : 1100,80);
        if (campExtrasSheet.Find("BestWaveIcon") is RectTransform icon)
            icon.anchoredPosition = new Vector2(40,portrait ? -850 : -604);
        if (recordValueText != null)
        {
            recordValueText.rectTransform.anchoredPosition = new Vector2(86,portrait ? -836 : -590);
            recordValueText.rectTransform.sizeDelta = new Vector2(portrait ? 490 : 600,60);
        }
        if (versionText != null)
            versionText.rectTransform.anchoredPosition = new Vector2(portrait ? 580 : 1020,portrait ? -836 : -590);
    }

    private static void SetCampFrame(RectTransform frame, Vector2 anchor, Vector2 pivot, Vector2 offset, float scale)
    {
        if (frame.anchorMin != anchor) frame.anchorMin = anchor;
        if (frame.anchorMax != anchor) frame.anchorMax = anchor;
        if (frame.pivot != pivot) frame.pivot = pivot;
        if (frame.anchoredPosition != offset) frame.anchoredPosition = offset;
        Vector3 next = Vector3.one * scale;
        if (frame.localScale != next) frame.localScale = next;
    }

    private void BuildCampRunOptions(RectTransform board)
    {
        pveModeButton = CampAction(board, "PveButton", 34, 220, 143, 42, CampGold,
            null, () => "PVE", SelectPveMode, 25, 0, false);
        pveModeLabel = pveModeButton.GetComponentInChildren<TMP_Text>();
        pvpModeButton = CampAction(board, "PvpButton", 187, 220, 143, 42, CampButtonColor,
            null, () => "PVP", SelectPvpMode, 25, 0, false);
        pvpModeLabel = pvpModeButton.GetComponentInChildren<TMP_Text>();
        modifierButton = CampAction(board, "Modifier", 344, 220, 370, 42, CampButtonColor,
            ArcadeKitAssets.Icon("flame"), null, CycleModifier, 23, 25, false);
        modifierValueText = modifierButton.GetComponentInChildren<TMP_Text>();
        CampIcon(modifierButton.transform, "Next", ArcadeKitAssets.Icon("arrow"), 333, 13, 16, 16, CampInk);
        modifierValueText.rectTransform.sizeDelta = new Vector2(265, 42);
    }

    private void BuildCampHeader(RectTransform board)
    {
        RectTransform header = CampRect(board, "CampHeader", 0, 0, 504, 84);
        ArcadeUiFactory.Image(header, CampCut(), CampDark, true);
        CampSolid(header, "LevelRule", 162, 17, 2, 50, CampQuiet);
        CampSolid(header, "CoinRule", 398, 17, 2, 50, CampQuiet);

        RectTransform level = CampRect(header, "LevelButton", 0, 0, 162, 84);
        CampTransparentButton(level, OpenStats);
        campLevelText = CampLabel(level, "LevelValue", 8, 0, 146, 84, "", 36,
            CampPaper, ArcadeKitAssets.BodyBold, TextAlignmentOptions.Center);

        RectTransform coins = CampRect(header, "CoinsButton", 164, 0, 234, 84);
        CampTransparentButton(coins, ClaimBonusAd);
        CampIcon(coins, "Coin", ArcadeKitAssets.Coin, 28, 19, 46, 46, Color.white);
        coinsValueText = CampLabel(coins, "CoinsValue", 85, 0, 141, 84, "0", 38,
            CampPaper, ArcadeKitAssets.BodyBold, TextAlignmentOptions.MidlineLeft);

        RectTransform gear = CampRect(header, "SettingsButton", 400, 0, 104, 84);
        CampTransparentButton(gear, OpenSettings);
        CampIcon(gear, "Gear", ArcadeKitAssets.Icon("gear"), 29, 19, 46, 46, CampPaper);
    }

    private void BuildCampMapChip(RectTransform board)
    {
        Button chip = CampAction(board, "MapChip", 0, 0, 363, 90, CampPaper,
            CampLeafIcon(), null, OpenMaps, 45, 66);
        mapNameText = chip.GetComponentInChildren<TMP_Text>();
        mapNameText.rectTransform.sizeDelta = new Vector2(193, 90);
        campMapIcon = chip.transform.Find("Icon").GetComponent<Image>();
        campMapIcon.color = CampLeaf;
        CampIcon(chip.transform, "Next", ArcadeKitAssets.Icon("arrow"), 316, 31, 28, 28, CampInk);
    }

    private void BuildCampExtras(RectTransform board)
    {
        RectTransform overlay = ArcadeUiFactory.Stretch(rootRect, "CampExtras");
        campExtras = overlay.gameObject;
        ArcadeUiFactory.Image(overlay, null, new Color(.12f, .16f, .13f, .65f)).raycastTarget = true;
        var frame = UiArtboard.Create(overlay, new Vector2(1424,764));
        RectTransform sheet = CampRect(frame, "Sheet", 24, 24, 1376, 716);
        campExtrasFrame = frame;
        campExtrasSheet = sheet;
        ArcadeUiFactory.Image(sheet, CampCut(), CampPaper, true).raycastTarget = true;
        AddShadow(sheet, new Vector2(0, -8), .75f);
        TMP_Text title = CampLabel(sheet, "Title", 40, 22, 1100, 80, "", 49, CampInk, ArcadeKitAssets.Display);
        AddLangBinding(title, () => CampText("Дополнительно", "More", "Daha fazla"));
        WindowCloseButton.Create(sheet, CloseCampExtras);

        CampExtraAction(sheet, "Stats", 40, 130, "person", () => Lang.GetOr("menu.skin.stats", "Статистика"), OpenStats);
        CampExtraAction(sheet, "Mastery", 476, 130, "sword", () => Lang.GetOr("progression.mastery.button", "Мастерство"), OpenMastery);
        CampExtraAction(sheet, "Leaderboards", 912, 130, "trophy", () => Lang.GetOr("menu.leaderboard.title", "Рейтинги"), OpenLeaderboards);
        CampExtraAction(sheet, "Tips", 40, 286, "book", () => Lang.GetOr("menu.skin.tips", "Как играть"), OpenTips);
        CampExtraAction(sheet, "Journey", 476, 286, "award", () => GameplayJourney.T("Путь бойца", "Fighter's journey", "Savaşçının yolu"),
            () => GameplayJourney.Instance?.UI?.OpenJournal());
        CampAction(sheet, "CoinsAd", 912, 286, 424, 122, CampButtonColor, ArcadeKitAssets.Coin,
            () => CampText("Бонус монет", "Coin bonus", "Altın bonusu"), ClaimBonusAd, 32, 64);
        weaponActionButton = CampAction(sheet, "WeaponAd", 40, 442, 424, 122, CampButtonColor, CampGun(),
            () => CampText("Награда за рекламу", "Ad reward", "Reklam ödülü"), ClaimWeaponAd, 29, 58);
        TMP_Text rewardTitle = weaponActionButton.GetComponentInChildren<TMP_Text>();
        rewardTitle.rectTransform.anchoredPosition = new Vector2(94, -11);
        rewardTitle.rectTransform.sizeDelta = new Vector2(312, 52);
        weaponHintText = CampLabel(weaponActionButton.transform, "RewardHint", 94, 66, 306, 42, "", 23, CampInk, ArcadeKitAssets.Body);
        weaponBadge = CampBadge(weaponActionButton.transform);
        CampExtraAction(sheet, "Settings", 476, 442, "gear", () => Lang.GetOr("settings.title", "Настройки"), OpenSettings);
        CampExtraAction(sheet, "Exit", 912, 442, "exit", () => Lang.GetOr("menu.skin.exit", "Выход"), ExitGame);

        CampIcon(sheet, "BestWaveIcon", ArcadeKitAssets.Icon("trophy"), 40, 604, 32, 32, CampLeaf);
        recordValueText = CampLabel(sheet, "RecordValue", 86, 590, 600, 60, "", 28, CampInk, ArcadeKitAssets.BodyBold);
        versionText = CampLabel(sheet, "Version", 1020, 590, 315, 60, "v" + Application.version,
            24, CampInk, ArcadeKitAssets.Body, TextAlignmentOptions.MidlineRight);
        campExtras.SetActive(false);
    }

    private void CampExtraAction(RectTransform parent, string name, float x, float y,
        string icon, Func<string> label, UnityAction action)
    {
        CampAction(parent, name, x, y, 424, 122, CampButtonColor, ArcadeKitAssets.Icon(icon), label,
            () => { CloseCampExtras(); action?.Invoke(); }, 32, 64);
    }

    private void OpenCampExtras()
    {
        if (campExtras == null) return;
        RefreshAll();
        campExtras.SetActive(true);
    }

    private void CloseCampExtras()
    {
        if (campExtras != null) campExtras.SetActive(false);
    }

    private void RefreshCampMode()
    {
        if (!CampMenuLayoutEnabled) return;
        bool isPvp = GameModeManager.IsPvp;
        if (pveModeButton != null) pveModeButton.GetComponent<Image>().color = isPvp ? CampButtonColor : CampGold;
        if (pvpModeButton != null) pvpModeButton.GetComponent<Image>().color = isPvp ? CampGold : CampButtonColor;
        if (pveModeLabel != null) { pveModeLabel.color = CampInk; pveModeLabel.text = "PVE"; }
        if (pvpModeLabel != null) { pvpModeLabel.color = CampInk; pvpModeLabel.text = "PVP"; }
        if (modifierButton != null) modifierButton.interactable = !isPvp;
        RefreshCampLevel();
    }

    private void RefreshCampLevel()
    {
        if (campLevelText == null) return;
        if (GameModeManager.IsPvp && pvpProgression != null)
            campLevelText.text = pvpProgression.Rating + " RP";
        else
            campLevelText.text = CampText("Ур. ", "Lv. ", "Sv. ") + (xpManager != null ? xpManager.GlobalLevel : 1);
    }

    private void RefreshCampMapIcon(GameMap map)
    {
        if (campMapIcon == null) return;
        // Follow the committed scene, not the requested game mode during a wipe.
        bool forest = map != null && map.langKey == "les";
        campMapIcon.sprite = forest ? CampLeafIcon() : CampMap();
        campMapIcon.color = forest ? CampLeaf : CampInk;
    }

    private StatTile CampTile(RectTransform parent, string name, float x, float y, float w, float h,
        Sprite icon, Func<string> label, UnityAction onClick)
    {
        Button btn = CampAction(parent, name, x, y, w, h, CampButtonColor, icon, label, onClick, 32, 52);
        return new StatTile { Root = btn.gameObject, Button = btn, Background = btn.GetComponent<Image>(),
            Title = btn.GetComponentInChildren<TMP_Text>(), Badge = CampBadge(btn.transform) };
    }

    private GameObject CampBadge(Transform parent)
    {
        RectTransform badge = ArcadeUiFactory.At(parent, "Badge", Vector2.one, Vector2.one,
            new Vector2(-9, -8), new Vector2(18, 18));
        ArcadeUiFactory.Image(badge, ArcadeKitAssets.Knob, ArcadeUiTheme.Red);
        badge.gameObject.SetActive(false);
        return badge.gameObject;
    }

    private Button CampAction(RectTransform parent, string name, float x, float y, float w, float h,
        Color fill, Sprite sprite, Func<string> label, UnityAction onClick, float fontSize, float iconSize,
        bool shadow = true)
    {
        RectTransform rect = CampRect(parent, name, x, y, w, h);
        Image image = ArcadeUiFactory.Image(rect, CampCut(), fill, true);
        image.raycastTarget = true;
        if (shadow) AddShadow(rect, new Vector2(0, -5), .95f);
        Button btn = rect.gameObject.AddComponent<Button>();
        btn.targetGraphic = image;
        btn.transition = Selectable.Transition.None;
        btn.navigation = new Navigation { mode = Navigation.Mode.None };
        if (onClick != null) btn.onClick.AddListener(onClick);
        rect.gameObject.AddComponent<ArcadeHoverLift>().Bind(image, 1.008f, .055f);
        float left = 12;
        if (sprite != null)
        {
            float inset = h > 100 ? 44 : (h > 70 ? 24 : 14);
            CampIcon(rect, "Icon", sprite, inset, (h - iconSize) * .5f, iconSize, iconSize, CampInk);
            left = inset + iconSize + (h > 100 ? 30 : 14);
        }
        TMP_Text text = CampLabel(rect, "Label", left, 0, w - left - 14, h, "", fontSize,
            CampInk, ArcadeKitAssets.BodyBold, sprite != null ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center);
        if (label != null) AddLangBinding(text, label);
        return btn;
    }

    private static void CampTransparentButton(RectTransform rect, UnityAction action)
    {
        Image image = ArcadeUiFactory.Image(rect, null, Color.clear);
        image.raycastTarget = true;
        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        if (action != null) button.onClick.AddListener(action);
    }

    private static RectTransform CampRect(Transform parent, string name, float x, float y, float w, float h) =>
        ArcadeUiFactory.At(parent, name, CampTopLeft, CampTopLeft, new Vector2(x, -y), new Vector2(w, h));

    private static Image CampSolid(Transform parent, string name, float x, float y, float w, float h, Color color) =>
        ArcadeUiFactory.Image(CampRect(parent, name, x, y, w, h), null, color);

    private static Image CampIcon(Transform parent, string name, Sprite sprite, float x, float y, float w, float h, Color color)
    {
        Image img = ArcadeUiFactory.Image(CampRect(parent, name, x, y, w, h), sprite, color);
        img.preserveAspect = true;
        return img;
    }

    private static TMP_Text CampLabel(Transform parent, string name, float x, float y, float w, float h,
        string value, float fontSize, Color color, TMP_FontAsset font,
        TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        TMP_Text text = ArcadeUiFactory.Label(CampRect(parent, name, x, y, w, h), value, font, fontSize, color, align);
        text.enableAutoSizing = true;
        text.fontSizeMin = fontSize * .65f;
        text.fontSizeMax = fontSize;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    private static string CampText(string ru, string en, string tr) => Lang.Code == LangCode.Ru ? ru : Lang.Code == LangCode.Tr ? tr : en;

    private static Sprite CampCut(bool sidebar = false)
    {
        Sprite cached = sidebar ? campSidebarSprite : campCutSprite;
        if (cached != null) return cached;
        const int size = 128;
        int cut = sidebar ? 57 : 12;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int d = (size - 1 - x) + (size - 1 - y);
                if (!sidebar) d = Mathf.Min(d, Mathf.Min(x + y, Mathf.Min(x + size - 1 - y, size - 1 - x + y)));
                pixels[y * size + x] = new Color32(255, 255, 255, d > cut ? (byte)255 : d == cut ? (byte)128 : (byte)0);
            }
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        cached = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect,
            sidebar ? new Vector4(1, 1, cut + 1, cut + 1) : Vector4.one * (cut + 1));
        cached.name = sidebar ? "Camp paper corner" : "Camp chamfer";
        if (sidebar) campSidebarSprite = cached; else campCutSprite = cached;
        return cached;
    }

    private static Sprite CampGun() => campGunSprite != null ? campGunSprite : campGunSprite = CampGlyph("Camp rifle",
        Poly(5,40, 23,47, 23,57, 66,57, 66,52, 91,52, 91,57, 97,57, 97,43, 77,43, 77,37, 52,37,
            56,17, 44,17, 38,37, 28,37, 17,31, 5,31),
        Poly(36,63, 60,63, 67,57, 32,57), Poly(30,35, 38,35, 33,20, 24,20));

    private static Sprite CampUpgrades() => campUpgradesSprite != null ? campUpgradesSprite : campUpgradesSprite = CampGlyph("Camp upgrades",
        Poly(19,60, 50,86, 81,60, 81,42, 50,68, 19,42), Poly(19,31, 50,57, 81,31, 81,13, 50,39, 19,13));

    private static Sprite CampMap() => campMapSprite != null ? campMapSprite : campMapSprite = CampGlyph("Camp map",
        Poly(7,22, 30,34, 30,81, 7,69), Poly(35,34, 57,22, 57,69, 35,81), Poly(62,22, 92,36, 92,83, 62,69));

    private static Sprite CampLeafIcon() => campLeafSprite != null ? campLeafSprite : campLeafSprite = CampGlyph("Camp leaf",
        Poly(18,21, 24,16, 36,30, 50,32, 63,44, 74,63, 85,84, 64,82, 46,74, 32,61, 25,46, 29,35),
        Poly(13,12, 17,8, 64,59, 60,61));

    private static Vector2[] Poly(params float[] coordinates)
    {
        var result = new Vector2[coordinates.Length / 2];
        for (int i = 0; i < result.Length; i++) result[i] = new Vector2(coordinates[i * 2], coordinates[i * 2 + 1]);
        return result;
    }

    private static Sprite CampGlyph(string name, params Vector2[][] polygons)
    {
        const int size = 96;
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2((x + .5f) * 100f / size, (y + .5f) * 100f / size);
                bool inside = false;
                foreach (Vector2[] poly in polygons)
                {
                    bool hit = false;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                        if ((poly[i].y > p.y) != (poly[j].y > p.y) && p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                            hit = !hit;
                    inside |= hit;
                }
                pixels[y * size + x] = new Color32(255, 255, 255, inside ? (byte)255 : (byte)0);
            }
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite icon = Sprite.Create(texture, new Rect(0, 0, size, size), Vector2.one * .5f, 100);
        icon.name = name;
        return icon;
    }
}
