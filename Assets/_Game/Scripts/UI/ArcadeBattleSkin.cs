using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Скину боевого интерфейса в том же стиле, что и меню
/// (ArcadeMenuSkin): те же панели, кнопки, слайдеры и шрифты.
///
/// Что делает при загрузке сцены:
///   • комбо — без плашки, только текст;
///   • пауза — тёмный полупрозрачный фон без закруглений
///     и карточка кнопок в стиле меню;
///   • настройки в бою — точная копия панели настроек меню;
///   • прогресс-бары остаются на Slider и получают вид меню.
/// </summary>
[DisallowMultipleComponent]
public sealed class ArcadeBattleSkin : MonoBehaviour
{
    /// <summary>
    /// Фон паузы и настроек: просто тёмный и полупрозрачный,
    /// без спрайта — поэтому на весь экран и без закруглений.
    /// </summary>
    private static readonly Color OverlayColor =
        ArcadeUiTheme.Backdrop;

    private sealed class TextBinding
    {
        public TMP_Text Label;
        public Func<string> Value;
    }

    private sealed class LangPill
    {
        public Image Background;
        public TMP_Text Label;
        public LangCode Code;
    }

    private readonly List<TextBinding> bindings = new List<TextBinding>();
    private readonly List<LangPill> langPills = new List<LangPill>();

    private GameObject pauseRoot;
    private GameObject settingsRoot;

    private Slider sfxSlider;
    private Slider musicSlider;
    private Slider uiSlider;
    private TMP_Text sfxValueText;
    private TMP_Text musicValueText;
    private TMP_Text uiValueText;
    private Image shakeImage;
    private TMP_Text shakeLabel;
    private Image fireShakeImage;
    private TMP_Text fireShakeLabel;

    private bool built;

    // =========================================================
    // BOOTSTRAP
    // =========================================================

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        Attach();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Attach();
    }

    private static void Attach()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(
            FindObjectsInactive.Include
        );

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];

            if (canvas == null || canvas.transform.parent != null)
                continue;

            if (FindDeep(canvas.transform, "PausePanel") == null)
                continue;

            if (canvas.GetComponent<ArcadeBattleSkin>() != null)
                continue;

            canvas.gameObject.AddComponent<ArcadeBattleSkin>();
            return;
        }
    }

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (PauseManager.Instance != null)
            PauseManager.Instance.OnPauseChanged += HandlePauseChanged;

        Lang.OnLanguageChanged += HandleLanguageChanged;
    }

    /// <summary>
    /// Стилизация — в Start, а не в Awake: оба скина (ArcadePanelSkin
    /// и этот) бутстрепятся после загрузки сцены в произвольном
    /// порядке. Start гарантированно позже всех бутстрепов, поэтому
    /// боевой скин перекрывает общие стили HUD, а не наоборот.
    /// </summary>
    private void Start()
    {
        if (built)
            return;

        built = true;

        StyleCombo();
        StylePause();
        BuildSettings();
        StyleHud();
        var mobile = GetComponent<MobileControls>();
        if (mobile == null) mobile = gameObject.AddComponent<MobileControls>();
        mobile.BindPause(FindDeep(transform,"PauseButton")?.GetComponent<Button>());

        EnsureButtonsClickable(pauseRoot);
        EnsureButtonsClickable(settingsRoot);
    }

    private void OnEnable()
    {
        RefreshSettingsValues();
        RefreshLangPills();
    }

    private void OnDestroy()
    {
        if (PauseManager.Instance != null)
            PauseManager.Instance.OnPauseChanged -= HandlePauseChanged;

        Lang.OnLanguageChanged -= HandleLanguageChanged;
    }

    private void HandlePauseChanged(bool paused)
    {
        // Меню и бой живут в одной сцене: значения настроек могли
        // поменяться в меню — при паузе синхронизируем панель.
        if (paused)
        {
            if (pauseRoot != null) pauseRoot.transform.SetAsLastSibling();
            RefreshSettingsValues();
        }
    }

    private void HandleLanguageChanged(LangCode code)
    {
        for (int i = 0; i < bindings.Count; i++)
        {
            TextBinding binding = bindings[i];

            if (binding != null && binding.Label != null && binding.Value != null)
                binding.Label.text = binding.Value();
        }

        RefreshLangPills();
        RefreshSettingsValues();
    }

    // =========================================================
    // COMBO — только текст, без плашки
    // =========================================================

    private void StyleCombo()
    {
        Transform combo = FindDeep(transform, "ComboPanel");

        if (combo == null)
            return;

        Image plate = combo.GetComponent<Image>();

        if (plate != null)
            plate.enabled = false;

        StyleComboText(combo.Find("Caption"), ArcadeKitAssets.BodyBold, 15f, MenuSheet.Paper, false);
        StyleComboText(combo.Find("ComboCountText"), ArcadeKitAssets.Display, 34f, MenuSheet.Paper, false);
        StyleComboText(combo.Find("ComboMultiplierText"), ArcadeKitAssets.BodyBold, 19f, ArcadeUiTheme.GoldBright, false);

        Bind(combo.Find("Caption"), () => Lang.GetOr("hud.combo", "КОМБО"));
    }

    private static void StyleComboText(
        Transform textTransform,
        TMP_FontAsset font,
        float size,
        Color color,
        bool uppercase)
    {
        if (textTransform == null)
            return;

        TMP_Text text = textTransform.GetComponent<TMP_Text>();

        if (text == null)
            return;

        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = uppercase
            ? FontStyles.Bold | FontStyles.UpperCase
            : FontStyles.Bold;

        AddShadowText(text, new Vector2(0f, -3f), 0.6f);
    }

    // =========================================================
    // PAUSE — тёмный полупрозрачный оверлей + карточка меню
    // =========================================================

    private void StylePause()
    {
        Transform pause = FindDeep(transform, "PausePanel");

        if (pause == null)
            return;

        pauseRoot = pause.gameObject;

        Image overlay = pause.GetComponent<Image>();

        if (overlay != null)
        {
            overlay.sprite = null;
            overlay.type = Image.Type.Simple;
            overlay.color = OverlayColor;
            overlay.raycastTarget = true;
        }

        Transform mini = FindDeep(pause, "PauseMiniPanel");

        if (mini == null)
            return;

        Image card = mini.GetComponent<Image>();

        if (card != null)
        {
            card.sprite = ArcadeKitAssets.CutPanel;
            card.type = Image.Type.Sliced;
            card.pixelsPerUnitMultiplier = 1f;
            card.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f);
            AddShadow(card.rectTransform, new Vector2(0f, -8f), 0.5f);
        }

        if (mini is RectTransform miniRect)
            miniRect.sizeDelta = new Vector2(440f, 480f);

        TMP_Text title = ArcadeUiFactory.Label(
            mini,
            "SkinTitle",
            "",
            ArcadeKitAssets.Display,
            30f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -28f),
            new Vector2(360f, 42f),
            true
        );
        Bind(title, () => Lang.GetOr("pause.title", "ПАУЗА"));

        RectTransform line = ArcadeUiFactory.At(
            mini,
            "SkinTitleLine",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -78f),
            new Vector2(360f, 4f)
        );
        ArcadeUiFactory.Image(
            line,
            null,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Gold, 0.5f)
        );

        Transform layout = mini.Find("ButtonLayot");

        if (layout is RectTransform layoutRect)
        {
            layoutRect.anchorMin = Vector2.zero;
            layoutRect.anchorMax = Vector2.one;
            layoutRect.pivot = new Vector2(0.5f, 0.5f);
            layoutRect.offsetMin = new Vector2(36f, 32f);
            layoutRect.offsetMax = new Vector2(-36f, -104f);

            VerticalLayoutGroup group =
                layout.GetComponent<VerticalLayoutGroup>();

            if (group != null)
            {
                group.spacing = 12f;
                group.childForceExpandHeight = false;
                group.childControlHeight = true;
                group.childControlWidth = true;
                group.childForceExpandWidth = true;
                group.childAlignment = TextAnchor.UpperCenter;
            }
        }

        StylePauseButton(layout, "ResumeButton", "pause.resume", "ПРОДОЛЖИТЬ", true);
        StylePauseButton(layout, "RestartButton", "pause.restart", "РЕСТАРТ", false);
        StylePauseButton(layout, "SettingsButton", "pause.settings", "НАСТРОЙКИ", false);
        StylePauseButton(layout, "MenuButton", "pause.menu", "МЕНЮ", false);
        if (layout is RectTransform actions && mini is RectTransform paper)
        {
            var board = UiArtboard.Create((RectTransform)pause, new Vector2(646,460));
            paper.SetParent(board, false);
            UiArtboard.Place(paper, Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(600,410));
            var auto = actions.GetComponent<VerticalLayoutGroup>();
            if (auto != null) auto.enabled = false;
            UiArtboard.Stretch(actions);
            RuntimeWindow.Place(actions,"ResumeButton",actions,36,106,528,76);
            RuntimeWindow.Place(actions,"SettingsButton",actions,36,198,256,64);
            RuntimeWindow.Place(actions,"RestartButton",actions,308,198,256,64);
            RuntimeWindow.Place(actions,"MenuButton",actions,36,292,528,64);
            MenuSheet.Box(paper,"Rule",36,278,528,2,MenuSheet.Rule);
            pause.SetAsLastSibling();
        }
    }

    private void StylePauseButton(
        Transform layout,
        string name,
        string langKey,
        string fallback,
        bool primary)
    {
        if (layout == null)
            return;

        Transform button = layout.Find(name);

        if (button == null)
            return;

        Image image = button.GetComponent<Image>();

        if (image != null)
        {
            image.sprite = ArcadeKitAssets.CutPanel;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.color = primary
                ? ArcadeUiTheme.Gold
                : ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.95f);
            image.raycastTarget = true;
        }

        MakePressable(button.GetComponent<Button>());

        LayoutElement element = button.GetComponent<LayoutElement>();

        if (element == null)
            element = button.gameObject.AddComponent<LayoutElement>();

        element.minHeight = 60f;
        element.preferredHeight = 60f;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
        {
            label.font = primary
                ? ArcadeKitAssets.Display
                : ArcadeKitAssets.BodyBold;
            label.fontSize = primary ? 24f : 18f;
            label.alignment = TextAlignmentOptions.Center;
            label.fontStyle = FontStyles.Normal;
            label.color = primary
                ? ArcadeUiTheme.TextDark
                : ArcadeUiTheme.TextPrimary;
            label.text = Lang.GetOr(langKey, fallback);

            Bind(label, () => Lang.GetOr(langKey, fallback));
        }
    }

    // =========================================================
    // HUD — пропорции полос и кнопок как в меню
    // =========================================================

    private void StyleHud()
    {
        Transform hud = FindDeep(transform, "HUD");

        if (hud == null)
            return;

        var gameplay = hud.Find("GameplayUI") as RectTransform;
        if (gameplay == null) return;
        UiArtboard.Stretch((RectTransform)hud);
        ResponsiveUi.BindSafeArea((RectTransform)hud);
        UiArtboard.Stretch(gameplay);
        var right = Vector2.one;
        var left = new Vector2(0, 1);
        var middle = Vector2.one * .5f;
        var strip = ArcadeUiFactory.At(gameplay, "Vitals", right, right, new Vector2(-22, -20), new Vector2(466, 90));
        ArcadeUiFactory.Image(strip, ArcadeKitAssets.CutPanel, ArcadeUiTheme.Ink, true);
        strip.gameObject.AddComponent<ActiveBoostHud>().Build(strip);
        strip.gameObject.AddComponent<RunAmmoHud>().Build(strip);
        strip.gameObject.AddComponent<RunOxygenHud>().Build(strip);
        strip.SetAsFirstSibling();
        var health = gameplay.Find("HealthSlider") as RectTransform;
        if (health != null)
        {
            health.SetParent(strip, false);
            UiArtboard.Place(health, left, left, new Vector2(12,-12), new Vector2(188,34));
            var hp = ArcadeUiFactory.Label(ArcadeUiFactory.Stretch(health, "HealthValue"), "",
                ArcadeKitAssets.BodyBold, 21, MenuSheet.Paper, TextAlignmentOptions.Center);
            AddShadowText(hp, new Vector2(0,-1), .65f);
            var binding = FindAnyObjectByType<GameplayUI>(FindObjectsInactive.Include);
            if (binding != null) binding.BindHealthLabel(hp);
        }
        MoveHudText(gameplay, strip, "WaveCounter", new Vector2(213,-8), new Vector2(237,45), 20);
        if (strip.Find("WaveCounter")?.GetComponent<TMP_Text>() is TMP_Text waveStatus)
        {
            waveStatus.textWrappingMode = TextWrappingModes.NoWrap;
            waveStatus.richText = true;
        }
        MoveHudText(gameplay, strip, "ScoreText", new Vector2(14,-50), new Vector2(184,31), 18);
        MoveHudText(gameplay, strip, "CoinsText", new Vector2(243,-50), new Vector2(190,31), 18);
        ArcadeUiFactory.Icon(strip, "Coins", "coin", ArcadeUiTheme.Gold, left, left,
            new Vector2(214,-55), new Vector2(20,20));
        if (hud.Find("PersonalBestHUD") is RectTransform personal)
        {
            UiArtboard.Place(personal, left, left, new Vector2(20,-120), new Vector2(320,146));
            int row = 0;
            foreach (string key in new[] { "ProgressText", "WavePbText", "ApproachText", "NewRecordText" })
            {
                var best = personal.Find(key)?.GetComponent<TMP_Text>();
                if (best == null) continue;
                UiArtboard.Place(best.rectTransform, left, left, new Vector2(0, -row++ * 33), new Vector2(310,32));
                best.alignment = TextAlignmentOptions.Left;
                best.font = ArcadeKitAssets.BodyBold;
                best.fontSize = 19;
                best.color = MenuSheet.Paper;
                AddShadowText(best, new Vector2(1,-2), .85f);
            }
        }
        var combo = FindDeep(hud, "ComboPanel") as RectTransform;
        if (combo != null && combo.parent is RectTransform leaderboard)
        {
            UiArtboard.Place(leaderboard, new Vector2(0,0), new Vector2(0,0), new Vector2(22,26), new Vector2(190,146));
            UiArtboard.Place(combo, middle, middle, Vector2.zero, new Vector2(170,116));
        }

        Transform bossPanel = FindDeep(hud, "BossHpPanel");

        if (bossPanel is RectTransform bossRect)
        {
            bossRect.pivot = new Vector2(.5f, 1f);
            bossRect.sizeDelta = new Vector2(660f, 102f);
            bossRect.anchoredPosition = new Vector2(0f, -40f);
        }

        if (bossPanel != null)
        {
            Transform bossName = bossPanel.Find("BossName");

            if (bossName is RectTransform nameRect)
            {
                nameRect.anchorMin = new Vector2(0.5f, 1f);
                nameRect.anchorMax = new Vector2(0.5f, 1f);
                nameRect.pivot = new Vector2(0.5f, 1f);
                nameRect.anchoredPosition = new Vector2(0f, -16f);
                nameRect.sizeDelta = new Vector2(600f, 32f);
            }

            Transform bossBar = bossPanel.Find("BossHpSlider");

            if (bossBar is RectTransform barRect)
            {
                barRect.anchorMin = new Vector2(0.5f, 1f);
                barRect.anchorMax = new Vector2(0.5f, 1f);
                barRect.pivot = new Vector2(0.5f, 1f);
                barRect.anchoredPosition = new Vector2(0f, -56f);
                barRect.sizeDelta = new Vector2(580f, 24f);
            }
        }

        // Кнопка паузы — круглая пилюля-иконка, как шестерня в меню.
        Transform pauseButton = FindDeep(hud, "PauseButton");

        if (pauseButton != null)
        {
            if (pauseButton is RectTransform pauseRect)
                UiArtboard.Place(pauseRect, left, left, new Vector2(22,-20), new Vector2(76,76));
            Image image = pauseButton.GetComponent<Image>();

            if (image != null)
            {
                image.sprite = ArcadeKitAssets.SliderHandle;
                image.type = Image.Type.Sliced;
                image.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.94f);
            }

            TMP_Text label = pauseButton.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
                label.color = MenuSheet.Ink;
        }
    }

    private static void MoveHudText(Transform from, RectTransform strip, string key, Vector2 position, Vector2 size, float fontSize)
    {
        var label = from.Find(key)?.GetComponent<TMP_Text>();
        if (label == null) return;
        label.transform.SetParent(strip, false);
        var topLeft = new Vector2(0,1);
        UiArtboard.Place(label.rectTransform, topLeft, topLeft, position, size);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.font = ArcadeKitAssets.BodyBold;
        label.fontStyle = FontStyles.Normal;
        label.enableAutoSizing = true;
        label.fontSizeMin = 14;
        label.fontSizeMax = fontSize;
        label.margin = Vector4.zero;
        label.raycastTarget = false;
        label.color = MenuSheet.Paper;
    }

    // =========================================================
    // SETTINGS — копия панели настроек меню
    // =========================================================

    private void BuildSettings()
    {
        Transform setting = FindDeep(transform, "SettingPanel");

        if (setting == null)
            return;

        settingsRoot = setting.gameObject;

        Image overlay = setting.GetComponent<Image>();

        if (overlay != null)
        {
            overlay.sprite = null;
            overlay.type = Image.Type.Simple;
            overlay.color = OverlayColor;
            overlay.raycastTarget = true;
        }

        SettingsUI legacy = setting.GetComponent<SettingsUI>();

        if (legacy != null)
            Destroy(legacy);

        Transform old = setting.Find("SettingPanelMini");

        if (old != null)
            Destroy(old.gameObject);

        RectTransform board = UiArtboard.Create((RectTransform)setting, new Vector2(1040, 634));
        RectTransform card = BuildSettingsCard(board);

        BuildSettingsHeader(card);
        BuildSettingsSound(card);
        BuildSettingsEffects(card);
        BuildSettingsLanguage(card);
        BuildSettingsButtons(card);
        RuntimeWindow.Settings(card);
    }

    private RectTransform BuildSettingsCard(Transform parent)
    {
        RectTransform card = ArcadeUiFactory.At(
            parent,
            "SkinSettingsCard",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(980f, 740f)
        );

        Image image = ArcadeUiFactory.Image(
            card,
            ArcadeKitAssets.CutPanel,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.99f),
            true
        );
        image.raycastTarget = true;
        AddShadow(card, new Vector2(0f, -8f), 0.5f);

        return card;
    }

    private void BuildSettingsHeader(RectTransform card)
    {
        TMP_Text title = ArcadeUiFactory.Label(
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
        Bind(title, () => Lang.GetOr("settings.title", "НАСТРОЙКИ"));

        RectTransform line = ArcadeUiFactory.At(
            card,
            "TitleLine",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(40f, -84f),
            new Vector2(900f, 4f)
        );
        ArcadeUiFactory.Image(
            line,
            null,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Gold, 0.5f)
        );

        WindowCloseButton.Create(card, CloseSettings);
    }

    private void BuildSettingsSound(RectTransform card)
    {
        SettingsSection(
            card,
            "SoundTitle",
            "settings.sfx_section",
            "ЗВУК",
            -128f
        );

        sfxSlider = SettingsSliderRow(
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

        musicSlider = SettingsSliderRow(
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

        uiSlider = SettingsSliderRow(
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
    }

    private void BuildSettingsEffects(RectTransform card)
    {
        SettingsSection(
            card,
            "EffectsTitle",
            "settings.effects",
            "ЭФФЕКТЫ",
            -398f
        );

        SettingsToggleRow(
            card,
            "ShakeRow",
            () => Lang.GetOr("settings.shake_damage", "ТРЯСКА ЭКРАНА"),
            new Vector2(36f, -438f),
            out shakeImage,
            out shakeLabel,
            () =>
            {
                SettingsManager settings = SettingsManager.Instance;

                if (settings == null)
                    return;

                settings.SetScreenShake(!settings.ScreenShakeEnabled);
                RefreshSettingsValues();
            }
        );

        SettingsToggleRow(
            card,
            "FireShakeRow",
            () => Lang.GetOr("settings.shake_fire", "ТРЯСКА ОТ ВЫСТРЕЛОВ"),
            new Vector2(36f, -506f),
            out fireShakeImage,
            out fireShakeLabel,
            () =>
            {
                SettingsManager settings = SettingsManager.Instance;

                if (settings == null)
                    return;

                settings.SetFireShake(!settings.FireShakeEnabled);
                RefreshSettingsValues();
            }
        );
    }

    private void BuildSettingsLanguage(RectTransform card)
    {
        SettingsSection(
            card,
            "LanguageTitle",
            "settings.language",
            "ЯЗЫК",
            -580f
        );

        BuildLangPill(card, new Vector2(36f, -616f), LangCode.Ru, "RU");
        BuildLangPill(card, new Vector2(112f, -616f), LangCode.En, "EN");
        BuildLangPill(card, new Vector2(188f, -616f), LangCode.Tr, "TR");
    }

    private void BuildSettingsButtons(RectTransform card)
    {
        RectTransform reset = ArcadeUiFactory.At(
            card,
            "ResetButton",
            new Vector2(0f, 0f),
            new Vector2(0f, 0f),
            new Vector2(36f, 24f),
            new Vector2(300f, 56f)
        );
        MakePressable(ArcadeUiFactory.Button(
            reset,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.PanelRaised,
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextSecondary,
            ResetSettings
        ));

        TMP_Text resetLabel = reset.Find("Label").GetComponent<TMP_Text>();
        Bind(resetLabel, () => Lang.GetOr("settings.reset", "СБРОСИТЬ НАСТРОЙКИ"));


    }

    private TMP_Text SettingsSection(
        RectTransform card,
        string name,
        string langKey,
        string fallback,
        float y)
    {
        TMP_Text label = ArcadeUiFactory.Label(
            card,
            name,
            "",
            ArcadeKitAssets.BodyBold,
            17f,
            ArcadeUiTheme.TextPrimary,
            TextAlignmentOptions.Left,
            new Vector2(0f, 1f),
            new Vector2(0f, 0.5f),
            new Vector2(40f, y),
            new Vector2(400f, 28f),
            true
        );

        Bind(label, () => Lang.GetOr(langKey, fallback));

        return label;
    }

    private Slider SettingsSliderRow(
        RectTransform card,
        string name,
        Func<string> label,
        float y,
        float value,
        out TMP_Text valueText,
        UnityEngine.Events.UnityAction<float> onChange)
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
        Bind(caption, label);

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
            ArcadeUiTheme.TextPrimary,
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

    private void SettingsToggleRow(
        RectTransform card,
        string name,
        Func<string> label,
        Vector2 position,
        out Image image,
        out TMP_Text labelText,
        UnityEngine.Events.UnityAction onClick)
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
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(onClick);
        rect.gameObject.AddComponent<ArcadeHoverLift>().Bind(image, 1.02f, 0.06f);
        MakePressable(button);

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
        Bind(caption, label);

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

    private void BuildLangPill(
        RectTransform card,
        Vector2 position,
        LangCode code,
        string text)
    {
        RectTransform rect = ArcadeUiFactory.At(
            card,
            "Lang" + text,
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            position,
            new Vector2(68f, 38f)
        );
        Image background = ArcadeUiFactory.Image(
            rect,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 1f),
            true
        );
        background.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = background;
        button.navigation = new Navigation { mode = Navigation.Mode.None };
        button.onClick.AddListener(() => Lang.SetUserLanguage(code));
        rect.gameObject.AddComponent<ArcadeHoverLift>().Bind(background, 1.04f, 0.1f);
        MakePressable(button);

        TMP_Text label = ArcadeUiFactory.Label(
            rect,
            "Label",
            text,
            ArcadeKitAssets.BodyBold,
            15f,
            ArcadeUiTheme.TextSecondary,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(64f, 30f)
        );

        langPills.Add(new LangPill
        {
            Background = background,
            Label = label,
            Code = code
        });
    }

    // =========================================================
    // SETTINGS STATE
    // =========================================================

    private void RefreshSettingsValues()
    {
        SettingsManager settings = SettingsManager.Instance;

        if (settings == null)
            return;

        SetSliderValue(sfxSlider, sfxValueText, settings.SfxVolume);
        SetSliderValue(musicSlider, musicValueText, settings.MusicVolume);
        SetSliderValue(uiSlider, uiValueText, settings.UiVolume);

        RefreshToggleVisual(shakeImage, shakeLabel, settings.ScreenShakeEnabled);
        RefreshToggleVisual(fireShakeImage, fireShakeLabel, settings.FireShakeEnabled);
    }

    private static void SetSliderValue(Slider slider, TMP_Text label, float value)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));

        if (label != null)
            label.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
    }

    private static void RefreshToggleVisual(Image image, TMP_Text label, bool enabled)
    {
        if (image != null)
        {
            image.color = enabled
                ? ArcadeUiTheme.Green
                : ArcadeUiTheme.PanelRaised;
        }

        if (label != null)
        {
            label.text = enabled
                ? Lang.GetOr("menu.skin.on", "ВКЛ")
                : Lang.GetOr("menu.skin.off", "ВЫКЛ");
        }
    }

    private void RefreshLangPills()
    {
        for (int i = 0; i < langPills.Count; i++)
        {
            LangPill pill = langPills[i];

            if (pill == null || pill.Background == null)
                continue;

            bool active = pill.Code == Lang.Code;

            pill.Background.color = active
                ? ArcadeUiTheme.Gold
                : ArcadeUiTheme.PanelRaised;

            if (pill.Label != null)
                pill.Label.color = active
                    ? ArcadeUiTheme.TextDark
                    : ArcadeUiTheme.TextSecondary;
        }
    }

    private void ResetSettings()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.ResetToDefaults();

        RefreshSettingsValues();
    }

    private void CloseSettings()
    {
        if (settingsRoot != null)
            settingsRoot.SetActive(false);

        if (pauseRoot != null &&
            PauseManager.Instance != null &&
            PauseManager.Instance.IsPaused)
        {
            pauseRoot.SetActive(true);
        }
    }

    // =========================================================
    // HELPERS
    // =========================================================

    /// <summary>
    /// Видимая реакция на нажатие (в том числе на тач-устройствах,
    /// где нет ховера): кнопка темнеет при нажатии.
    /// </summary>
    private static void MakePressable(Button button)
    {
        if (button == null)
            return;

        button.transition = Selectable.Transition.ColorTint;

        ColorBlock colors = ColorBlock.defaultColorBlock;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.12f, 1.18f, 1.28f, 1f);
        colors.pressedColor = new Color(0.7f, 0.76f, 0.86f, 1f);
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
    }

    /// <summary>
    /// Страховка от чужих скинов, которые могли отключить рейкаст
    /// или интерактивность кнопок паузы и настроек.
    /// </summary>
    private static void EnsureButtonsClickable(GameObject root)
    {
        if (root == null)
            return;

        Button[] buttons = root.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null)
                continue;

            button.interactable = true;

            Image image = button.targetGraphic as Image;

            if (image == null)
                image = button.GetComponent<Image>();

            if (image != null)
                image.raycastTarget = true;
        }
    }

    private void Bind(TMP_Text label, Func<string> value)
    {
        bindings.Add(new TextBinding { Label = label, Value = value });

        if (label != null && value != null)
            label.text = value();
    }

    private void Bind(Transform labelTransform, Func<string> value)
    {
        if (labelTransform == null)
            return;

        TMP_Text text = labelTransform.GetComponent<TMP_Text>();

        if (text != null)
            Bind(text, value);
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);

            if (found != null)
                return found;
        }

        return null;
    }

    private static void AddShadow(RectTransform rect, Vector2 offset, float alpha)
    {
        Image image = rect != null ? rect.GetComponent<Image>() : null;

        if (image == null)
            return;

        Shadow shadow = rect.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
        shadow.effectDistance = offset;
        shadow.useGraphicAlpha = true;
    }

    private static void AddShadowText(TMP_Text text, Vector2 offset, float alpha)
    {
        if (text == null)
            return;

        if (text.GetComponent<Shadow>() != null)
            return;

        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, alpha);
        shadow.effectDistance = offset;
        shadow.useGraphicAlpha = true;
    }
}
