using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed partial class ArcadeMenuSkin
{
    private void BuildPanels()
    {
        settingsPanel = BuildSettingsPanel();
        statsPanel = BuildStatsPanel();
        tipsPanel = BuildTipsPanel();
        leaderboardPanel = BuildLeaderboardPanel();

        settingsPanel.SetActive(false);
        statsPanel.SetActive(false);
        tipsPanel.SetActive(false);
        leaderboardPanel.SetActive(false);
    }

    private GameObject BuildSettingsPanel()
    {
        RectTransform root = BuildPanelBackdrop("SkinSettingsPanel");
        RectTransform card = BuildPanelCard(root, "Card", new Vector2(980f, 740f));

        BuildPanelHeader(
            card,
            () => Lang.GetOr("settings.title", "НАСТРОЙКИ"),
            () => CloseCustomPanel(settingsPanel)
        );

        TMP_Text sound = SectionTitle(card, "SoundTitle", "ЗВУК", -128f);

        AddLangBinding(sound, () => Lang.GetOr("settings.sfx_section", "ЗВУК"));

        sfxSlider = BuildSliderRow(
            card,
            "SfxRow",
            () => Lang.GetOr("settings.sfx", "ПРОМЕЖУТОЧНЫЕ ЭФФЕКТЫ"),
            -170f,
            SettingsManager.Instance != null ? SettingsManager.Instance.SfxVolume : 1f,
            out sfxValueText,
            value =>
            {
                if (SettingsManager.Instance != null)
                    SettingsManager.Instance.SetSFXVolume(value);
            }
        );

        musicSlider = BuildSliderRow(
            card,
            "MusicRow",
            () => Lang.GetOr("settings.music", "ГРОМКОСТЬ МУЗЫКИ"),
            -246f,
            SettingsManager.Instance != null ? SettingsManager.Instance.MusicVolume : 0.5f,
            out musicValueText,
            value =>
            {
                if (SettingsManager.Instance != null)
                    SettingsManager.Instance.SetMusicVolume(value);
            }
        );

        uiSlider = BuildSliderRow(
            card,
            "UiRow",
            () => Lang.GetOr("settings.ui", "ГРОМКОСТЬ ИНТЕРФЕЙСА"),
            -322f,
            SettingsManager.Instance != null ? SettingsManager.Instance.UiVolume : 0.8f,
            out uiValueText,
            value =>
            {
                if (SettingsManager.Instance != null)
                    SettingsManager.Instance.SetUiVolume(value);
            }
        );

        TMP_Text effects = SectionTitle(card, "EffectsTitle", "ЭФФЕКТЫ", -398f);

        AddLangBinding(effects, () => Lang.GetOr("settings.effects", "ЭФФЕКТЫ"));

        BuildToggle(
            card,
            "ShakeRow",
            () => Lang.GetOr("settings.shake_damage", "ТРЯСКА ЭКРАНА"),
            new Vector2(36f, -438f),
            out shakeImage,
            out shakeLabel,
            ToggleScreenShake
        );

        BuildToggle(
            card,
            "FireShakeRow",
            () => Lang.GetOr("settings.shake_fire", "ТРЯСКА ОТ ВЫСТРЕЛОВ"),
            new Vector2(36f, -506f),
            out fireShakeImage,
            out fireShakeLabel,
            ToggleFireShake
        );

        TMP_Text language = SectionTitle(card, "LanguageTitle", "ЯЗЫК", -580f);

        AddLangBinding(language, () => Lang.GetOr("settings.language", "ЯЗЫК"));

        BuildLangPills(
            card,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(36f, -616f),
            alpha: 1f
        );

        RectTransform reset = ArcadeUiFactory.At(
            card,
            "ResetButton",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(36f, 24f),
            new Vector2(300f, 56f)
        );
        Button resetButton = ArcadeUiFactory.Button(
            reset,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.PanelRaised,
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextSecondary,
            ResetSettings
        );

        TMP_Text resetLabel = reset.Find("Label").GetComponent<TMP_Text>();
        AddLangBinding(
            resetLabel,
            () => Lang.GetOr("settings.reset", "СБРОСИТЬ НАСТРОЙКИ")
        );

        RectTransform close = ArcadeUiFactory.At(
            card,
            "CloseButton",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-36f, 24f),
            new Vector2(240f, 56f)
        );
        Button closeButton = ArcadeUiFactory.Button(
            close,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Gold,
            "",
            ArcadeKitAssets.BodyBold,
            18f,
            ArcadeUiTheme.TextDark,
            () => CloseCustomPanel(settingsPanel)
        );

        TMP_Text closeLabel = close.Find("Label").GetComponent<TMP_Text>();
        AddLangBinding(closeLabel, () => Lang.GetOr("settings.close", "ЗАКРЫТЬ"));

        return root.gameObject;
    }

    private GameObject BuildStatsPanel()
    {
        RectTransform root = BuildPanelBackdrop("SkinStatsPanel");
        RectTransform card = BuildPanelCard(root, "Card", new Vector2(1020f, 680f));

        BuildPanelHeader(
            card,
            () => Lang.GetOr("menu.skin.stats", "СТАТИСТИКА"),
            () => CloseCustomPanel(statsPanel)
        );

        RectTransform avatar = ArcadeUiFactory.At(
            card,
            "Avatar",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -116f),
            new Vector2(124f, 138f)
        );
        ArcadeUiFactory.Image(avatar, ArcadeKitAssets.HexFill, ArcadeUiTheme.Panel);
        RectTransform avatarRing = ArcadeUiFactory.At(
            card,
            "AvatarRing",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -116f),
            new Vector2(134f, 148f)
        );
        ArcadeUiFactory.Image(avatarRing, ArcadeKitAssets.HexRing, ArcadeUiTheme.Gold);
        RectTransform avatarIcon = ArcadeUiFactory.At(
            card,
            "AvatarIcon",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -116f),
            new Vector2(60f, 60f)
        );
        ArcadeUiFactory.Image(avatarIcon, ArcadeKitAssets.Icon("person"), ArcadeUiTheme.Gold);

        statsNameText = ArcadeUiFactory.Label(
            card,
            "Name",
            "",
            ArcadeKitAssets.BodyBold,
            24f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -272f),
            new Vector2(300f, 32f),
            true
        );

        RectTransform auth = ArcadeUiFactory.At(
            card,
            "AuthButton",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -320f),
            new Vector2(280f, 52f)
        );
        statsAuthButton = ArcadeUiFactory.Button(
            auth,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.PanelRaised,
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextPrimary,
            OpenAuthDialog
        );
        statsAuthLabel = auth.Find("Label").GetComponent<TMP_Text>();

        statsLevelText = ArcadeUiFactory.Label(
            card,
            "Level",
            "1",
            ArcadeKitAssets.Display,
            30f,
            ArcadeUiTheme.GoldBright,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -400f),
            new Vector2(120f, 38f)
        );

        RectTransform levelBar = ArcadeUiFactory.At(
            card,
            "LevelBar",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -450f),
            new Vector2(300f, 14f)
        );
        statsXpBar = ArcadeUiFactory.Bar(
            levelBar,
            0f,
            false,
            ArcadeUiTheme.BackgroundDeep,
            ArcadeUiTheme.Gold
        );

        statsXpText = ArcadeUiFactory.Label(
            card,
            "LevelXp",
            "",
            ArcadeKitAssets.Body,
            15f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -476f),
            new Vector2(300f, 24f)
        );

        statsAchievementsText = BuildStatsTile(
            card,
            "Achievements",
            () => Lang.GetOr("menu.skin.achievements", "ДОСТИЖЕНИЯ"),
            "trophy",
            ArcadeUiTheme.Purple,
            new Vector2(400f, -116f)
        );

        statsBestWaveText = BuildStatsTile(
            card,
            "BestWave",
            () => Lang.GetOr("menu.wave_short", "ЛУЧШАЯ ВОЛНА"),
            "flame",
            ArcadeUiTheme.Orange,
            new Vector2(710f, -116f)
        );

        statsBestScoreText = BuildStatsTile(
            card,
            "BestScore",
            () => Lang.GetOr("menu.skin.best_score", "ЛУЧШИЙ СЧЁТ"),
            "star",
            ArcadeUiTheme.Gold,
            new Vector2(400f, -266f)
        );

        statsRunsText = BuildStatsTile(
            card,
            "Runs",
            () => Lang.GetOr("menu.skin.runs", "ЗАБЕГИ"),
            "play",
            ArcadeUiTheme.Cyan,
            new Vector2(710f, -266f)
        );

        statsCoinsText = BuildStatsTile(
            card,
            "Coins",
            () => Lang.GetOr("menu.skin.coins", "МОНЕТЫ"),
            "coin",
            ArcadeUiTheme.Gold,
            new Vector2(400f, -416f)
        );

        statsWeaponsText = BuildStatsTile(
            card,
            "Weapons",
            () => Lang.GetOr("menu.skin.weapons", "ОРУЖИЕ"),
            "sword",
            ArcadeUiTheme.Green,
            new Vector2(710f, -416f)
        );

        RectTransform close = ArcadeUiFactory.At(
            card,
            "CloseButton",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-36f, 32f),
            new Vector2(240f, 56f)
        );
        Button closeButton = ArcadeUiFactory.Button(
            close,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Gold,
            "",
            ArcadeKitAssets.BodyBold,
            18f,
            ArcadeUiTheme.TextDark,
            () => CloseCustomPanel(statsPanel)
        );

        TMP_Text closeLabel = close.Find("Label").GetComponent<TMP_Text>();
        AddLangBinding(closeLabel, () => Lang.GetOr("settings.close", "ЗАКРЫТЬ"));

        return root.gameObject;
    }

    private GameObject BuildTipsPanel()
    {
        RectTransform root = BuildPanelBackdrop("SkinTipsPanel");
        RectTransform card = BuildPanelCard(root, "Card", new Vector2(1020f, 700f));

        BuildPanelHeader(
            card,
            () => Lang.GetOr("menu.skin.tips", "КАК ИГРАТЬ"),
            () => CloseCustomPanel(tipsPanel)
        );

        for (int i = 0; i < 6; i++)
        {
            int index = i + 1;

            RectTransform dot = ArcadeUiFactory.At(
                card,
                "TipDot" + index,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(44f, -122f - i * 66f),
                new Vector2(18f, 18f)
            );
            ArcadeUiFactory.Image(dot, ArcadeKitAssets.Spark, ArcadeUiTheme.Gold);

            TMP_Text tip = ArcadeUiFactory.Label(
                card,
                "Tip" + index,
                "",
                ArcadeKitAssets.Body,
                19f,
                ArcadeUiTheme.TextSecondary,
                TextAlignmentOptions.TopLeft,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(76f, -128f - i * 66f),
                new Vector2(900f, 58f)
            );

            AddLangBinding(tip, () => Lang.GetOr(
                "menu.skin.tips." + index,
                TipFallback(index)
            ));
        }

        RectTransform replay = ArcadeUiFactory.At(
            card,
            "ReplayButton",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(36f, 32f),
            new Vector2(340f, 56f)
        );
        tipsReplayButton = ArcadeUiFactory.Button(
            replay,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Gold,
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextDark,
            ReplayTutorial
        );

        TMP_Text replayLabel = replay.Find("Label").GetComponent<TMP_Text>();
        AddLangBinding(
            replayLabel,
            () => Lang.GetOr("menu.tutorial_replay", "ПОВТОРИТЬ ОБУЧЕНИЕ")
        );

        tipsStatusText = ArcadeUiFactory.Label(
            card,
            "Status",
            "",
            ArcadeKitAssets.Body,
            15f,
            ArcadeUiTheme.TextMuted,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(396f, 46f),
            new Vector2(360f, 28f)
        );

        RectTransform close = ArcadeUiFactory.At(
            card,
            "CloseButton",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-36f, 32f),
            new Vector2(240f, 56f)
        );
        Button closeButton = ArcadeUiFactory.Button(
            close,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.PanelRaised,
            "",
            ArcadeKitAssets.BodyBold,
            18f,
            ArcadeUiTheme.TextPrimary,
            () => CloseCustomPanel(tipsPanel)
        );

        TMP_Text closeLabel = close.Find("Label").GetComponent<TMP_Text>();
        AddLangBinding(closeLabel, () => Lang.GetOr("settings.close", "ЗАКРЫТЬ"));

        return root.gameObject;
    }

    private GameObject BuildLeaderboardPanel()
    {
        RectTransform root = BuildPanelBackdrop("SkinLeaderboardPanel");
        RectTransform card = BuildPanelCard(root, "Card", new Vector2(1020f, 720f));
        BuildPanelHeader(
            card,
            () => Lang.GetOr("menu.leaderboard.title", "РЕЙТИНГИ"),
            () => CloseCustomPanel(leaderboardPanel)
        );

        leaderboardTabs = new Button[4];
        for (int i = 0; i < leaderboardTabs.Length; i++)
        {
            int tab = i;
            float width = 218f;
            RectTransform tabRect = ArcadeUiFactory.At(
                card,
                "LeaderboardTab" + i,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(40f + i * (width + 12f), -112f),
                new Vector2(width, 52f)
            );
            leaderboardTabs[i] = ArcadeUiFactory.Button(
                tabRect,
                ArcadeKitAssets.RoundRect,
                ArcadeUiTheme.PanelRaised,
                "",
                ArcadeKitAssets.BodyBold,
                14f,
                ArcadeUiTheme.TextPrimary,
                () => SelectLeaderboardTab(tab)
            );
            TMP_Text label = tabRect.Find("Label").GetComponent<TMP_Text>();
            AddLangBinding(label, () => Lang.GetOr(
                "menu.leaderboard.tab." + tab,
                LeaderboardTabFallback(tab)
            ));
        }

        RectTransform viewport = ArcadeUiFactory.At(
            card,
            "LeaderboardViewport",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(46f, -184f),
            new Vector2(928f, 430f)
        );
        Image viewportImage = ArcadeUiFactory.Image(viewport, null, Color.white);
        viewportImage.raycastTarget = false;
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = ArcadeUiFactory.Stretch(viewport, "Content");
        leaderboardText = ArcadeUiFactory.Label(
            content,
            Lang.GetOr("lb.loading", "ЗАГРУЗКА...").ToUpperInvariant(),
            ArcadeKitAssets.Body,
            18f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.TopLeft
        );
        leaderboardText.enableWordWrapping = false;
        leaderboardText.overflowMode = TextOverflowModes.Overflow;
        leaderboardUi = gameObject.AddComponent<LeaderboardUI>();
        leaderboardUi.Configure(LeaderboardName(0), leaderboardText, true);
        RefreshLeaderboardTabs();

        RectTransform close = ArcadeUiFactory.At(
            card,
            "CloseButton",
            new Vector2(1f, 0f),
            new Vector2(1f, 0f),
            new Vector2(-36f, 28f),
            new Vector2(240f, 56f)
        );
        ArcadeUiFactory.Button(
            close,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.PanelRaised,
            "",
            ArcadeKitAssets.BodyBold,
            18f,
            ArcadeUiTheme.TextPrimary,
            () => CloseCustomPanel(leaderboardPanel)
        );
        AddLangBinding(
            close.Find("Label").GetComponent<TMP_Text>(),
            () => Lang.GetOr("settings.close", "ЗАКРЫТЬ")
        );

        return root.gameObject;
    }

    private void SelectLeaderboardTab(int index)
    {
        selectedLeaderboardTab = Mathf.Clamp(index, 0, 3);
        RefreshLeaderboardTabs();
        if (leaderboardUi != null)
        {
            leaderboardUi.Configure(
                LeaderboardName(selectedLeaderboardTab), leaderboardText,
                selectedLeaderboardTab == 0
            );
            leaderboardUi.Refresh();
        }
    }

    private void RefreshLeaderboardTabs()
    {
        if (leaderboardTabs == null)
            return;

        for (int i = 0; i < leaderboardTabs.Length; i++)
        {
            bool selected = i == selectedLeaderboardTab;
            Image image = leaderboardTabs[i] != null
                ? leaderboardTabs[i].GetComponent<Image>()
                : null;
            TMP_Text label = leaderboardTabs[i] != null
                ? leaderboardTabs[i].GetComponentInChildren<TMP_Text>()
                : null;
            if (image != null)
                image.color = selected ? ArcadeUiTheme.Gold : ArcadeUiTheme.PanelRaised;
            if (label != null)
                label.color = selected ? ArcadeUiTheme.TextDark : ArcadeUiTheme.TextPrimary;
        }
    }

    private static string LeaderboardName(int tab)
    {
        switch (tab)
        {
            case 0: return YandexGameManager.LeaderboardPvpRating;
            case 1: return YandexGameManager.LeaderboardBestScoreDaily;
            case 2: return YandexGameManager.LeaderboardBestScoreWeekly;
            default: return YandexGameManager.LeaderboardBestScoreMonthly;
        }
    }

    private static string LeaderboardTabFallback(int tab)
    {
        switch (tab)
        {
            case 0: return "PVP";
            case 1: return "ДЕНЬ";
            case 2: return "НЕДЕЛЯ";
            default: return "МЕСЯЦ";
        }
    }

    private static string TipFallback(int index)
    {
        switch (index)
        {
            case 1:
                return "Двигайся и держи дистанцию — враги не должны окружить тебя.";
            case 2:
                return "Оружие стреляет само. Собирай опыт и выбирай улучшения после волн.";
            case 3:
                return "Монеты сохраняются между забегами — трать их в улучшениях и снаряжении.";
            case 4:
                return "Боссы появляются каждые 10 волн. Следи за телеграфами атак.";
            case 5:
                return "Модификаторы меняют правила забега. Выбирай их в главном меню.";
            case 6:
                return "Ежедневный бонус и задания дают монеты и опыт — забирай их перед забегом.";
            default:
                return string.Empty;
        }
    }

    private RectTransform BuildPanelBackdrop(string name)
    {
        RectTransform root = ArcadeUiFactory.Stretch(menuRoot, name);
        Image dim = ArcadeUiFactory.Image(root, null, ArcadeUiTheme.Backdrop);
        dim.raycastTarget = true;
        return root;
    }

    private RectTransform BuildPanelCard(
        RectTransform parent,
        string name,
        Vector2 size)
    {
        RectTransform card = ArcadeUiFactory.At(
            parent,
            name,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            size
        );

        Image image = ArcadeUiFactory.Image(
            card,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.99f),
            true
        );
        image.raycastTarget = true;
        AddShadow(card, new Vector2(0f, -8f), 0.5f);

        return card;
    }

    private void BuildPanelHeader(
        RectTransform card,
        Func<string> title,
        UnityAction onClose)
    {
        TMP_Text titleText = ArcadeUiFactory.Label(
            card,
            "Title",
            "",
            ArcadeKitAssets.Display,
            34f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -30f),
            new Vector2(700f, 46f),
            true
        );
        AddLangBinding(titleText, title);

        RectTransform line = ArcadeUiFactory.At(
            card,
            "TitleLine",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -84f),
            new Vector2(900f, 4f)
        );
        ArcadeUiFactory.Image(line, null, ArcadeUiTheme.Alpha(ArcadeUiTheme.Gold, 0.5f));

        RectTransform close = ArcadeUiFactory.At(
            card,
            "Close",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-28f, -24f),
            new Vector2(56f, 56f)
        );
        ArcadeUiFactory.Button(
            close,
            ArcadeKitAssets.Knob,
            ArcadeUiTheme.PanelRaised,
            null,
            ArcadeKitAssets.Body,
            16f,
            ArcadeUiTheme.TextPrimary,
            onClose
        );

        RectTransform icon = ArcadeUiFactory.At(
            close,
            "Icon",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(24f, 24f)
        );
        ArcadeUiFactory.Image(icon, ArcadeKitAssets.Icon("close"), ArcadeUiTheme.TextPrimary);
    }

    private TMP_Text SectionTitle(
        RectTransform card,
        string name,
        string fallback,
        float y)
    {
        TMP_Text label = ArcadeUiFactory.Label(
            card,
            name,
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.Gold,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(40f, y),
            new Vector2(400f, 28f),
            true
        );

        return label;
    }

    private Slider BuildSliderRow(
        RectTransform card,
        string name,
        Func<string> label,
        float y,
        float value,
        out TMP_Text valueText,
        UnityAction<float> onChange)
    {
        TMP_Text caption = ArcadeUiFactory.Label(
            card,
            name + "Label",
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(40f, y),
            new Vector2(360f, 30f),
            true
        );
        AddLangBinding(caption, label);

        RectTransform bar = ArcadeUiFactory.At(
            card,
            name + "Bar",
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(430f, y),
            new Vector2(420f, 22f)
        );
        Slider slider = ArcadeUiFactory.Bar(
            bar,
            value,
            true,
            ArcadeUiTheme.BackgroundDeep,
            ArcadeUiTheme.Gold,
            26f
        );

        valueText = ArcadeUiFactory.Label(
            card,
            name + "Value",
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.GoldBright,
            TextAlignmentOptions.Right,
            new Vector2(1f, 1f),
            new Vector2(1f, 0.5f),
            new Vector2(-40f, y),
            new Vector2(90f, 30f)
        );

        TMP_Text valueLabel = valueText;
        valueLabel.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";

        slider.onValueChanged.AddListener(next =>
        {
            onChange?.Invoke(next);
            valueLabel.text = Mathf.RoundToInt(next * 100f) + "%";
        });

        return slider;
    }

    private void BuildToggle(
        RectTransform card,
        string name,
        Func<string> label,
        Vector2 position,
        out Image image,
        out TMP_Text labelText,
        UnityAction onClick)
    {
        RectTransform rect = ArcadeUiFactory.At(
            card,
            name,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            position,
            new Vector2(430f, 58f)
        );

        image = ArcadeUiFactory.Image(
            rect,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.PanelRaised,
            true
        );
        image.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        rect.gameObject.AddComponent<ArcadeHoverLift>().Bind(image, 1.02f, 0.06f);

        TMP_Text caption = ArcadeUiFactory.Label(
            rect,
            "Label",
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(24f, 0f),
            new Vector2(280f, 30f),
            true
        );
        AddLangBinding(caption, label);

        labelText = ArcadeUiFactory.Label(
            rect,
            "State",
            "",
            ArcadeKitAssets.BodyBold,
            16f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Right,
            new Vector2(1f, 0.5f),
            new Vector2(1f, 0.5f),
            new Vector2(-24f, 0f),
            new Vector2(110f, 26f),
            true
        );
    }

    private TMP_Text BuildStatsTile(
        RectTransform card,
        string name,
        Func<string> label,
        string icon,
        Color accent,
        Vector2 position)
    {
        RectTransform rect = ArcadeUiFactory.At(
            card,
            name + "Tile",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            position,
            new Vector2(270f, 130f)
        );
        ArcadeUiFactory.Image(
            rect,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.95f),
            true
        );
        AddShadow(rect, new Vector2(0f, -4f), 0.32f);

        RectTransform iconRect = ArcadeUiFactory.At(
            rect,
            "Icon",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(20f, -20f),
            new Vector2(30f, 30f)
        );
        ArcadeUiFactory.Image(iconRect, ArcadeKitAssets.Icon(icon), accent);

        TMP_Text caption = ArcadeUiFactory.Label(
            rect,
            "Label",
            "",
            ArcadeKitAssets.BodyBold,
            14f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(60f, -20f),
            new Vector2(190f, 30f),
            true
        );
        AddLangBinding(caption, label);

        return ArcadeUiFactory.Label(
            rect,
            "Value",
            "0",
            ArcadeKitAssets.Display,
            30f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.BottomLeft,
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(20f, 20f),
            new Vector2(230f, 48f)
        );
    }

    private void ToggleScreenShake()
    {
        SettingsManager settings = SettingsManager.Instance;

        if (settings == null)
            return;

        settings.SetScreenShake(!settings.ScreenShakeEnabled);
        RefreshSettingsValues();
    }

    private void ToggleFireShake()
    {
        SettingsManager settings = SettingsManager.Instance;

        if (settings == null)
            return;

        settings.SetFireShake(!settings.FireShakeEnabled);
        RefreshSettingsValues();
    }

    private void ResetSettings()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.ResetToDefaults();

        RefreshSettingsValues();
        ShowToast(Lang.GetOr("settings.reset_done", "НАСТРОЙКИ СБРОШЕНЫ"));
    }
}
