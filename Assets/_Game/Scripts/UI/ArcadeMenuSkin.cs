using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed partial class ArcadeMenuSkin : MonoBehaviour
{
    private const string BonusAdDateKey = "ArcadeSurvivor.Menu.BonusAdDate";
    private const string WeaponAdDateKey = "ArcadeSurvivor.Menu.WeaponAdDate";
    private const int BonusAdCoins = 75;
    private const int FallbackWeaponCoins = 300;

    public static ArcadeMenuSkin Instance { get; private set; }

    public bool IsBuilt { get; private set; }

    private MainMenuUI menu;
    private RectTransform menuRoot;
    private GameObject root;
    private RectTransform rootRect;
    private CanvasGroup rootGroup;

    private GameObject toastRoot;
    private CanvasGroup toastGroup;
    private TMP_Text toastText;
    private Coroutine toastRoutine;

    private Button legacyPlayButton;
    private TMP_Dropdown modifierDropdown;

    private TMP_Text levelValueText;
    private TMP_Text recordValueText;
    private TMP_Text coinsValueText;
    private TMP_Text xpValueText;
    private Slider topXpBar;

    private RailCard dailyCard;
    private RailCard upgradesCard;
    private RailCard equipmentCard;
    private RailCard mapsCard;

    private TMP_Text weaponTagText;
    private TMP_Text weaponTitleText;
    private TMP_Text weaponHintText;
    private TMP_Text weaponButtonLabel;
    private GameObject weaponBadge;
    private GameObject weaponCardRoot;
    private Image weaponIconImage;
    private Button weaponActionButton;

    private TMP_Text playerNameText;
    private TMP_Text playerLevelText;
    private TMP_Text playerXpText;
    private Slider playerXpBar;
    private GameObject playerCardRoot;

    private TMP_Text mapNameText;
    private TMP_Text modifierValueText;
    private Button modifierButton;
    private Button playButton;
    private Button pveModeButton;
    private Button pvpModeButton;
    private TMP_Text pveModeLabel;
    private TMP_Text pvpModeLabel;
    private Image playGlow;
    private Image playShine;

    private StatTile statsTile;
    private StatTile tipsTile;
    private StatTile missionsTile;
    private StatTile achievementsTile;
    private StatTile masteryTile;

    private readonly List<LangPill> langPills = new List<LangPill>();
    private readonly List<LangBinding> langBindings = new List<LangBinding>();
    private readonly List<ToastLine> toastLines = new List<ToastLine>();
    private readonly List<GameObject> legacyHidden = new List<GameObject>();

    private TMP_Text versionText;
    private Button exitButton;

    private GameObject settingsPanel;
    private GameObject statsPanel;
    private GameObject tipsPanel;

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

    private TMP_Text statsNameText;
    private TMP_Text statsLevelText;
    private TMP_Text statsXpText;
    private Slider statsXpBar;
    private TMP_Text statsAchievementsText;
    private TMP_Text statsBestWaveText;
    private TMP_Text statsBestScoreText;
    private TMP_Text statsRunsText;
    private TMP_Text statsCoinsText;
    private TMP_Text statsWeaponsText;
    private Button statsAuthButton;
    private TMP_Text statsAuthLabel;

    private TMP_Text tipsStatusText;
    private Button tipsReplayButton;

    private XpManager xpManager;
    private DailyRewardManager dailyRewardManager;
    private AchievementManager achievementManager;
    private DailyMissionManager dailyMissionManager;
    private YandexGameManager yandexManager;
    private GameStateManager gameStateManager;
    private EnvironmentController environment;

    private bool boundPersonalBest;
    private bool servicesBound;

    private WeaponData pendingWeaponReward;

    private sealed class RailCard
    {
        public GameObject Root;
        public Button Button;
        public Image Background;
        public TMP_Text Title;
        public TMP_Text Subtitle;
        public GameObject Badge;
        public TMP_Text BadgeLabel;
        public Image Icon;
    }

    private sealed class StatTile
    {
        public GameObject Root;
        public Button Button;
        public Image Background;
        public TMP_Text Title;
        public TMP_Text Value;
        public GameObject Badge;
        public TMP_Text BadgeLabel;
    }

    private sealed class LangPill
    {
        public Image Background;
        public TMP_Text Label;
        public LangCode Code;
    }

    private sealed class LangBinding
    {
        public TMP_Text Label;
        public Func<string> Value;
    }

    private sealed class ToastLine
    {
        public TMP_Text Label;
        public Func<string> Value;
    }

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
        MainMenuUI found = FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);

        if (found == null || found.GetComponent<ArcadeMenuSkin>() != null)
            return;

        MainMenuUI.ShowWithoutAnimation = true;
        found.gameObject.AddComponent<ArcadeMenuSkin>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        menu = GetComponent<MainMenuUI>();

        if (menu == null)
            menu = FindFirstObjectByType<MainMenuUI>(FindObjectsInactive.Include);

        if (menu == null)
        {
            enabled = false;
            return;
        }

        menuRoot = transform.Find("Menu") as RectTransform;

        if (menuRoot == null)
            menuRoot = transform as RectTransform;

        if (menuRoot == null)
        {
            enabled = false;
            return;
        }

        Build();
    }

    private void Update()
    {
        for (int i = 0; i < legacyHidden.Count; i++)
        {
            GameObject hidden = legacyHidden[i];

            if (hidden != null && hidden.activeSelf)
                hidden.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        UnbindServices();

        if (toastRoutine != null)
            StopCoroutine(toastRoutine);

        if (Instance == this)
            Instance = null;
    }

    private void Build()
    {
        if (IsBuilt)
            return;

        rootRect = ArcadeUiFactory.Stretch(menuRoot, "ArcadeMenuSkin");
        root = rootRect.gameObject;
        rootGroup = root.AddComponent<CanvasGroup>();

        BuildBackground(rootRect);
        BuildTopBar(rootRect);
        BuildLeftRail(rootRect);
        BuildRightRail(rootRect);
        BuildCenter(rootRect);
        BuildBottom(rootRect);
        HideLegacyMenu();
        BuildPanels();
        BuildToast();

        BindServices();
        RefreshLangPills();
        RefreshAll();

        IsBuilt = true;
        StartCoroutine(FadeIn());
    }

    private void HideLegacyMenu()
    {
        legacyPlayButton = FindLegacyButton("StartButton");
        modifierDropdown = FindLegacyComponent<TMP_Dropdown>("RunModifierDropdown");
        HideLegacyButton("MasteryButton");

        for (int i = menuRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = menuRoot.GetChild(i).gameObject;

            if (child == root)
                continue;

            if (!child.activeSelf)
                continue;

            legacyHidden.Add(child);
            child.SetActive(false);
        }
    }

    private void HideLegacyButton(string name)
    {
        Transform found = menuRoot.Find(name);
        if (found != null && found.gameObject.activeSelf)
        {
            legacyHidden.Add(found.gameObject);
            found.gameObject.SetActive(false);
        }
    }

    private Button FindLegacyButton(string name)
    {
        Transform found = menuRoot.Find(name);
        return found != null ? found.GetComponent<Button>() : null;
    }

    private T FindLegacyComponent<T>(string name) where T : Component
    {
        Transform found = menuRoot.Find(name);
        return found != null ? found.GetComponent<T>() : null;
    }

    private IEnumerator FadeIn()
    {
        rootGroup.alpha = 0f;

        float timer = 0f;

        while (timer < 0.28f)
        {
            timer += Time.unscaledDeltaTime;
            rootGroup.alpha = Mathf.Clamp01(timer / 0.28f);
            yield return null;
        }

        rootGroup.alpha = 1f;
    }

    private void BindServices()
    {
        UnbindServices();

        xpManager = XpManager.Instance;

        if (xpManager != null)
        {
            xpManager.OnCoinsChanged += HandleCoinsChanged;
            xpManager.OnPlayerXPChanged += HandlePlayerXpChanged;
            xpManager.OnPlayerLevelChanged += HandlePlayerLevelChanged;
        }

        dailyRewardManager = DailyRewardManager.Instance;

        if (dailyRewardManager != null)
            dailyRewardManager.OnRewardClaimed += HandleDailyRewardClaimed;

        achievementManager = AchievementManager.Instance;

        if (achievementManager != null)
            achievementManager.OnProgressChanged += HandleProgressChanged;

        dailyMissionManager = DailyMissionManager.Instance;

        if (dailyMissionManager != null)
            dailyMissionManager.OnProgressChanged += HandleProgressChanged;

        PersonalBestRecord.OnChanged += HandlePersonalBestChanged;
        boundPersonalBest = true;

        yandexManager = YandexGameManager.Instance;

        if (yandexManager != null)
        {
            yandexManager.OnReady += HandleServicesReady;
            yandexManager.OnBestScoreChanged += HandleBestScoreChanged;
            yandexManager.OnAuthorizedChanged += HandleAuthorizedChanged;
        }

        gameStateManager = GameStateManager.Instance;

        if (gameStateManager != null)
            gameStateManager.OnGameStateChanged += HandleGameStateChanged;

        environment = FindFirstObjectByType<EnvironmentController>(FindObjectsInactive.Include);

        if (environment != null)
            environment.OnMapChanged += HandleMapChanged;

        Lang.OnLanguageChanged += HandleLanguageChanged;

        servicesBound = true;
    }

    private void UnbindServices()
    {
        if (!servicesBound)
            return;

        if (xpManager != null)
        {
            xpManager.OnCoinsChanged -= HandleCoinsChanged;
            xpManager.OnPlayerXPChanged -= HandlePlayerXpChanged;
            xpManager.OnPlayerLevelChanged -= HandlePlayerLevelChanged;
        }

        if (dailyRewardManager != null)
            dailyRewardManager.OnRewardClaimed -= HandleDailyRewardClaimed;

        if (achievementManager != null)
            achievementManager.OnProgressChanged -= HandleProgressChanged;

        if (dailyMissionManager != null)
            dailyMissionManager.OnProgressChanged -= HandleProgressChanged;

        if (boundPersonalBest)
        {
            PersonalBestRecord.OnChanged -= HandlePersonalBestChanged;
            boundPersonalBest = false;
        }

        if (yandexManager != null)
        {
            yandexManager.OnReady -= HandleServicesReady;
            yandexManager.OnBestScoreChanged -= HandleBestScoreChanged;
            yandexManager.OnAuthorizedChanged -= HandleAuthorizedChanged;
        }

        if (gameStateManager != null)
            gameStateManager.OnGameStateChanged -= HandleGameStateChanged;

        if (environment != null)
            environment.OnMapChanged -= HandleMapChanged;

        Lang.OnLanguageChanged -= HandleLanguageChanged;

        xpManager = null;
        dailyRewardManager = null;
        achievementManager = null;
        dailyMissionManager = null;
        yandexManager = null;
        gameStateManager = null;
        environment = null;
        servicesBound = false;
    }

    private void HandleCoinsChanged(int value)
    {
        RefreshBalances();
        RefreshStats();
    }

    private void HandlePlayerXpChanged(int value)
    {
        RefreshBalances();
        RefreshStats();
    }

    private void HandlePlayerLevelChanged(int value)
    {
        RefreshBalances();
        RefreshStats();
    }

    private void HandleDailyRewardClaimed(int day)
    {
        RefreshDailyCard();
    }

    private void HandleProgressChanged()
    {
        RefreshTiles();
        RefreshStats();
    }

    private void HandlePersonalBestChanged()
    {
        RefreshRecord();
        RefreshTiles();
        RefreshStats();
    }

    private void HandleBestScoreChanged()
    {
        RefreshRecord();
        RefreshStats();
    }

    private void HandleAuthorizedChanged(bool authorized)
    {
        RefreshPlayer();
        RefreshStats();
    }

    private void HandleServicesReady()
    {
        RefreshAll();
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state != GameState.Menu)
            return;

        if (rootGroup != null)
            rootGroup.alpha = 1f;

        RefreshAll();
    }

    private void HandleMapChanged(GameMap map)
    {
        RefreshMapChip();
    }

    private void HandleLanguageChanged(LangCode code)
    {
        if (menu != null)
            menu.RefreshLang();

        RefreshLangBindings();
        RefreshLangPills();
        RefreshAll();
    }

    private void RefreshAll()
    {
        RefreshBalances();
        RefreshRecord();
        RefreshPlayer();
        RefreshDailyCard();
        RefreshWeaponCard();
        RefreshTiles();
        RefreshMapChip();
        RefreshModifier();
        RefreshGameMode();
        RefreshStats();
        RefreshSettingsValues();
    }

    private void RefreshBalances()
    {
        int coins = xpManager != null ? xpManager.GlobalCoins : 0;
        int level = xpManager != null ? xpManager.GlobalLevel : 1;
        int xpIn = xpManager != null ? xpManager.GlobalXPInCurrentLevel : 0;
        int xpNext = xpManager != null ? xpManager.GlobalXPNeededForNextLevel : 1;
        float progress = xpManager != null ? xpManager.GlobalLevelProgress : 0f;

        if (levelValueText != null)
            levelValueText.text = level.ToString();

        if (coinsValueText != null)
            coinsValueText.text = FormatAmount(coins);

        if (xpValueText != null)
            xpValueText.text = Lang.GetOr("menu.xp", "{0} / {1} XP")
                .Replace("{0}", xpIn.ToString())
                .Replace("{1}", xpNext.ToString());

        if (topXpBar != null)
            topXpBar.value = progress;

        if (playerLevelText != null)
            playerLevelText.text = Lang.GetOr("menu.level", "Уровень {0}")
                .Replace("{0}", level.ToString());

        if (playerXpText != null)
            playerXpText.text = Lang.GetOr("menu.xp", "{0} / {1} XP")
                .Replace("{0}", xpIn.ToString())
                .Replace("{1}", xpNext.ToString());

        if (playerXpBar != null)
            playerXpBar.value = progress;
    }

    private void RefreshRecord()
    {
        int bestWave = PersonalBestRecord.BestWave;

        if (recordValueText != null)
        {
            recordValueText.text = bestWave > 0
                ? Lang.GetOr("menu.wave_reached", "Волна: {0}")
                    .Replace("{0}", bestWave.ToString())
                : Lang.GetOr("menu.record_none", "НЕТ РЕКОРДА");
        }
    }

    private void RefreshPlayer()
    {
        bool ready = yandexManager != null && yandexManager.IsReady;
        bool authorized = yandexManager != null && yandexManager.IsAuthorized;

        if (playerNameText != null)
        {
            bool hasName = authorized &&
                !string.IsNullOrWhiteSpace(yandexManager.PlayerName);

            playerNameText.text = hasName
                ? yandexManager.PlayerName
                : Lang.GetOr("menu.skin.guest", "ГОСТЬ");
        }

        if (statsAuthButton != null)
            statsAuthButton.interactable = ready && !authorized;

        if (statsAuthLabel != null)
        {
            if (authorized)
                statsAuthLabel.text = Lang.GetOr("menu.skin.authorized", "ПОДКЛЮЧЕНО");
            else if (ready)
                statsAuthLabel.text = Lang.GetOr("menu.skin.auth", "ВОЙТИ");
            else
                statsAuthLabel.text = Lang.GetOr("menu.skin.connecting", "ПОДКЛЮЧЕНИЕ...");
        }
    }

    private void RefreshDailyCard()
    {
        if (dailyCard == null)
            return;

        bool available = dailyRewardManager != null &&
            dailyRewardManager.IsAvailableToday;

        if (dailyCard.Badge != null)
            dailyCard.Badge.SetActive(available);

        if (dailyCard.Subtitle != null)
        {
            dailyCard.Subtitle.text = available
                ? Lang.GetOr("menu.skin.daily_sub", "ЗАБРАТЬ НАГРАДУ")
                : Lang.GetOr("menu.skin.daily_done", "ПОЛУЧЕНО СЕГОДНЯ");
        }
    }

    private void RefreshWeaponCard()
    {
        bool available = IsAdAvailable(WeaponAdDateKey);
        pendingWeaponReward = PickWeaponReward();
        bool hasWeapon = pendingWeaponReward != null;

        if (weaponCardRoot != null)
            weaponCardRoot.SetActive(true);

        if (weaponTagText != null)
            weaponTagText.text = Lang.GetOr("menu.skin.reward_tag", "ЗА ПРОСМОТР РЕКЛАМЫ");

        if (weaponTitleText != null)
        {
            weaponTitleText.text = hasWeapon
                ? Lang.GetOr("menu.skin.weapon_reward", "ОРУЖИЕ ДНЯ")
                : Lang.GetOr("menu.skin.coin_reward", "МОНЕТЫ");
        }

        if (weaponIconImage != null)
            weaponIconImage.sprite = hasWeapon
                ? ArcadeKitAssets.Icon("sword")
                : ArcadeKitAssets.Coin;

        if (weaponHintText != null)
        {
            if (!available)
            {
                weaponHintText.text = Lang.GetOr(
                    "menu.skin.come_back",
                    "ВОЗВРАЩАЙСЯ ЗАВТРА"
                );
            }
            else if (hasWeapon)
            {
                weaponHintText.text = pendingWeaponReward.LocalizedName;
            }
            else
            {
                weaponHintText.text = Lang.GetOr("menu.skin.coins_amount", "{0} МОНЕТ")
                    .Replace("{0}", FallbackWeaponCoins.ToString());
            }
        }

        if (weaponButtonLabel != null)
        {
            weaponButtonLabel.text = available
                ? Lang.GetOr("menu.skin.free", "ЗАБРАТЬ")
                : Lang.GetOr("menu.skin.claimed", "ПОЛУЧЕНО");
        }

        if (weaponBadge != null)
            weaponBadge.SetActive(available);

        if (weaponActionButton != null)
            weaponActionButton.interactable = available;

        if (weaponIconImage != null)
            weaponIconImage.color = available
                ? Color.white
                : ArcadeUiTheme.Alpha(Color.white, 0.45f);
    }

    private void RefreshTiles()
    {
        if (missionsTile != null)
        {
            int completed = dailyMissionManager != null
                ? dailyMissionManager.CompletedCount
                : 0;
            int total = dailyMissionManager != null
                ? dailyMissionManager.TotalCount
                : 0;

            if (missionsTile.Value != null)
                missionsTile.Value.text = completed + " / " + total;

            bool claimable = dailyMissionManager != null &&
                dailyMissionManager.CompletedCount >
                dailyMissionManager.ClaimedCount;

            if (missionsTile.Badge != null)
                missionsTile.Badge.SetActive(claimable);
        }

        if (achievementsTile != null)
        {
            int unlocked = achievementManager != null
                ? achievementManager.UnlockedCount
                : 0;
            int total = achievementManager != null &&
                achievementManager.All != null
                ? achievementManager.All.Count
                : 0;

            if (achievementsTile.Value != null)
                achievementsTile.Value.text = unlocked + " / " + total;
        }

        if (tipsTile != null && tipsTile.Value != null)
        {
            tipsTile.Value.text = TutorialManager.IsCompleted
                ? Lang.GetOr("menu.skin.tips_done", "ПРОЙДЕНО")
                : Lang.GetOr("menu.skin.tips_new", "НОВОЕ");
        }

        if (statsTile != null && statsTile.Value != null)
        {
            statsTile.Value.text = Lang.GetOr("menu.wave_reached", "Волна: {0}")
                .Replace("{0}", PersonalBestRecord.BestWave.ToString());
        }
    }

    private void RefreshMapChip()
    {
        if (mapNameText == null)
            return;

        GameMap map = environment != null ? environment.CurrentMap : null;

        if (map != null)
        {
            mapNameText.text = map.LocalizedName;
            return;
        }

        mapNameText.text = Lang.GetOr("menu.maps", "КАРТЫ");
    }

    private void RefreshModifier()
    {
        if (modifierValueText == null)
            return;

        if (modifierDropdown == null || modifierDropdown.options == null ||
            modifierDropdown.options.Count == 0)
        {
            modifierValueText.text = Lang.GetOr("menu.run.standard", "СТАНДАРТ");
            return;
        }

        int index = Mathf.Clamp(
            modifierDropdown.value,
            0,
            modifierDropdown.options.Count - 1
        );

        modifierValueText.text = modifierDropdown.options[index].text;
    }

    private void RefreshGameMode()
    {
        bool pvp = GameModeManager.IsPvp;

        if (pveModeButton != null)
            pveModeButton.GetComponent<Image>().color = pvp
                ? ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.94f)
                : ArcadeUiTheme.Green;

        if (pvpModeButton != null)
            pvpModeButton.GetComponent<Image>().color = pvp
                ? ArcadeUiTheme.Orange
                : ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.94f);

        if (pveModeLabel != null)
            pveModeLabel.color = pvp
                ? ArcadeUiTheme.TextPrimary
                : ArcadeUiTheme.TextDark;

        if (pvpModeLabel != null)
            pvpModeLabel.color = pvp
                ? ArcadeUiTheme.TextDark
                : ArcadeUiTheme.TextPrimary;

        if (pveModeLabel != null)
            pveModeLabel.text = Lang.GetOr("menu.mode.pve", "PVE");

        if (pvpModeLabel != null)
            pvpModeLabel.text = Lang.GetOr("menu.mode.pvp", "PVP");
    }

    private void RefreshStats()
    {
        int level = xpManager != null ? xpManager.GlobalLevel : 1;
        int xpIn = xpManager != null ? xpManager.GlobalXPInCurrentLevel : 0;
        int xpNext = xpManager != null ? xpManager.GlobalXPNeededForNextLevel : 1;

        if (statsLevelText != null)
            statsLevelText.text = level.ToString();

        if (statsXpText != null)
            statsXpText.text = xpIn + " / " + xpNext;

        if (statsXpBar != null)
            statsXpBar.value = xpManager != null ? xpManager.GlobalLevelProgress : 0f;

        if (statsAchievementsText != null)
        {
            int unlocked = achievementManager != null
                ? achievementManager.UnlockedCount
                : 0;
            int total = achievementManager != null &&
                achievementManager.All != null
                ? achievementManager.All.Count
                : 0;

            statsAchievementsText.text = unlocked + " / " + total;
        }

        if (statsBestWaveText != null)
            statsBestWaveText.text = PersonalBestRecord.BestWave.ToString();

        if (statsBestScoreText != null)
            statsBestScoreText.text = yandexManager != null
                ? FormatAmount(yandexManager.BestScore)
                : "0";

        if (statsRunsText != null)
            statsRunsText.text = yandexManager != null
                ? FormatAmount(yandexManager.RunCount)
                : "0";

        if (statsCoinsText != null)
            statsCoinsText.text = FormatAmount(xpManager != null ? xpManager.GlobalCoins : 0);

        if (statsWeaponsText != null)
            statsWeaponsText.text = (EquipmentManager.OwnedWeapons != null
                ? EquipmentManager.OwnedWeapons.Count
                : 0).ToString();

        if (statsNameText != null)
        {
            bool authorized = yandexManager != null && yandexManager.IsAuthorized;
            bool hasName = authorized &&
                !string.IsNullOrWhiteSpace(yandexManager.PlayerName);

            statsNameText.text = hasName
                ? yandexManager.PlayerName
                : Lang.GetOr("menu.skin.guest", "ГОСТЬ");
        }
    }

    private void RefreshSettingsValues()
    {
        SettingsManager settings = SettingsManager.Instance;

        if (settings == null)
            return;

        SetSliderValue(sfxSlider, sfxValueText, settings.SfxVolume);
        SetSliderValue(musicSlider, musicValueText, settings.MusicVolume);
        SetSliderValue(uiSlider, uiValueText, settings.UiVolume);
        RefreshToggleVisual(
            shakeImage,
            shakeLabel,
            settings.ScreenShakeEnabled
        );
        RefreshToggleVisual(
            fireShakeImage,
            fireShakeLabel,
            settings.FireShakeEnabled
        );
    }

    private void SetSliderValue(Slider slider, TMP_Text label, float value)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(Mathf.Clamp01(value));

        if (label != null)
        {
            label.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }
    }

    private void RefreshToggleVisual(Image image, TMP_Text label, bool enabled)
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

    private void RefreshLangBindings()
    {
        for (int i = 0; i < langBindings.Count; i++)
        {
            LangBinding binding = langBindings[i];

            if (binding != null && binding.Label != null && binding.Value != null)
                binding.Label.text = binding.Value();
        }

        for (int i = 0; i < toastLines.Count; i++)
        {
            ToastLine line = toastLines[i];

            if (line != null && line.Label != null && line.Value != null)
                line.Label.text = line.Value();
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

    private void Bind(LangBinding binding, TMP_Text label, Func<string> value)
    {
        binding.Label = label;
        binding.Value = value;
        langBindings.Add(binding);

        if (label != null && value != null)
            label.text = value();
    }

    private void AddLangBinding(TMP_Text label, Func<string> value)
    {
        Bind(new LangBinding(), label, value);
    }

    private void Play()
    {
        if (GameModeManager.IsPvp)
        {
            PvpMatchManager.EnsureExists().BeginSearch();
            return;
        }

        if (legacyPlayButton != null)
        {
            legacyPlayButton.onClick.Invoke();
            return;
        }

        if (gameStateManager != null)
            gameStateManager.StartGame();
    }

    private void SelectPveMode()
    {
        GameModeManager.Select(GameMode.Pve);
        PvpMatchManager pvp = FindAnyObjectByType<PvpMatchManager>();
        if (pvp != null)
            pvp.CancelAndReturnToPve();

        WaveManager waves = FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (waves != null)
            waves.ActivateForPve();

        RefreshGameMode();
    }

    private void SelectPvpMode()
    {
        GameModeManager.Select(GameMode.Pvp);
        WaveManager waves = FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (waves != null)
            waves.DeactivateForPvp();

        PvpMatchManager.EnsureExists().PreviewArena();
        RefreshGameMode();
    }

    private void OpenDaily()
    {
        if (menu != null)
            menu.OpenDailyRewardPanel();
    }

    private void OpenUpgrades()
    {
        OpenLegacyPanel("UpgradesPanel");
    }

    private void OpenEquipment()
    {
        OpenLegacyPanel("EquipmentPanel");
    }

    private void OpenMaps()
    {
        OpenLegacyPanel("MapsPanel");
    }

    private void OpenAchievements()
    {
        if (menu != null)
            menu.OpenAchievementsPanel();
    }

    private void OpenMastery()
    {
        if (menu == null)
            return;

        menu.OpenSubPanel(menu.MasteryPanel);
        if (menu.MasteryPanel != null)
        {
            MasteryPanelUI ui = menu.MasteryPanel.GetComponent<MasteryPanelUI>();
            if (ui != null)
                ui.Refresh();
        }
    }

    private void OpenMissions()
    {
        if (menu != null)
            menu.OpenMissionsPanel();
    }

    private void OpenLegacyPanel(string name)
    {
        if (menu == null)
            return;

        Transform found = menuRoot.Find(name);

        if (found == null)
        {
            ShowToast(Lang.GetOr("menu.skin.missing", "РАЗДЕЛ НЕДОСТУПЕН"));
            return;
        }

        legacyHidden.Remove(found.gameObject);
        menu.OpenSubPanel(found.gameObject);
        StyleLegacyBackButtons(found.gameObject);
    }

    private void StyleLegacyBackButtons(GameObject panel)
    {
        if (panel == null)
            return;

        Button[] buttons = panel.GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null || !button.name.StartsWith("BackButton"))
                continue;

            Image background = button.targetGraphic as Image;

            if (background == null)
                background = button.GetComponent<Image>();

            if (background != null)
            {
                background.sprite = ArcadeKitAssets.RoundRect;
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 1f;
                background.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Ink, 0.78f);
            }

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.font = ArcadeKitAssets.BodyBold;
                label.fontSize = 18f;
                label.color = ArcadeUiTheme.TextPrimary;
                label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
            }
        }
    }

    private void OpenSettings()
    {
        OpenCustomPanel(settingsPanel);
    }

    private void OpenStats()
    {
        OpenCustomPanel(statsPanel);
    }

    private void OpenTips()
    {
        OpenCustomPanel(tipsPanel);
    }

    private void OpenCustomPanel(GameObject panel)
    {
        if (panel == null || menu == null)
            return;

        if (panel == statsPanel)
            RefreshStats();
        else if (panel == settingsPanel)
            RefreshSettingsValues();

        menu.OpenSubPanel(panel);
    }

    private void CloseCustomPanel(GameObject panel)
    {
        if (panel != null)
            panel.SetActive(false);

        if (menu != null)
            menu.CloseSubPanels();
    }

    private void CycleModifier()
    {
        if (modifierDropdown == null || modifierDropdown.options == null ||
            modifierDropdown.options.Count == 0)
            return;

        int next = (modifierDropdown.value + 1) % modifierDropdown.options.Count;
        modifierDropdown.value = next;
        modifierDropdown.RefreshShownValue();
        RefreshModifier();
    }

    private void ClaimBonusAd()
    {
        if (!IsAdAvailable(BonusAdDateKey))
        {
            ShowToast(Lang.GetOr("menu.skin.claimed", "ПОЛУЧЕНО"));
            return;
        }

        RunRewarded(YandexGameManager.RewardId_MenuBonus, GrantBonusAd);
    }

    private void GrantBonusAd()
    {
        if (!IsAdAvailable(BonusAdDateKey))
            return;

        if (xpManager != null)
            xpManager.AddGlobalCoins(BonusAdCoins);

        MarkAdClaimed(BonusAdDateKey);
        RefreshAll();

        ShowToast(
            Lang.GetOr("menu.skin.toast_bonus", "+{0} МОНЕТ")
                .Replace("{0}", BonusAdCoins.ToString())
        );
    }

    private void ClaimWeaponAd()
    {
        if (!IsAdAvailable(WeaponAdDateKey))
        {
            ShowToast(Lang.GetOr("menu.skin.claimed", "ПОЛУЧЕНО"));
            return;
        }

        WeaponData reward = PickWeaponReward();

        RunRewarded(
            YandexGameManager.RewardId_MenuWeapon,
            () => GrantWeaponAd(reward)
        );
    }

    private void GrantWeaponAd(WeaponData reward)
    {
        if (!IsAdAvailable(WeaponAdDateKey))
            return;

        bool granted = false;

        if (reward != null && !EquipmentManager.IsOwned(reward))
        {
            EquipmentManager.GrantWeapon(reward.WeaponName);
            granted = true;
        }
        else if (xpManager != null)
        {
            xpManager.AddGlobalCoins(FallbackWeaponCoins);
        }

        MarkAdClaimed(WeaponAdDateKey);
        RefreshAll();

        ShowToast(
            granted
                ? Lang.GetOr("menu.skin.toast_weapon", "ОРУЖИЕ ОТКРЫТО")
                : Lang.GetOr("menu.skin.toast_bonus", "+{0} МОНЕТ")
                    .Replace("{0}", FallbackWeaponCoins.ToString())
        );
    }

    private void RunRewarded(string id, Action onReward)
    {
        YandexGameManager yandex = YandexGameManager.Instance;

        if (yandex == null)
        {
            onReward?.Invoke();
            return;
        }

        yandex.ShowRewarded(id, onReward);
    }

    private void OpenAuthDialog()
    {
        if (yandexManager == null || !yandexManager.IsReady)
        {
            ShowToast(Lang.GetOr("menu.skin.connecting", "ПОДКЛЮЧЕНИЕ..."));
            return;
        }

        yandexManager.OpenAuthDialog();
    }

    private void ReplayTutorial()
    {
        TutorialManager.IsCompleted = false;
        PlayerPrefs.Save();

        if (tipsStatusText != null)
            tipsStatusText.text = Lang.GetOr("menu.tutorial_reset", "Обучение сброшено");

        ShowToast(Lang.GetOr("menu.tutorial_reset", "Обучение сброшено"));
    }

    private void ExitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private static WeaponData PickWeaponReward()
    {
        UpgradeManager upgrades = UpgradeManager.Instance;

        if (upgrades == null)
            return null;

        IReadOnlyList<WeaponData> weapons = upgrades.GetAvailableWeapons();

        if (weapons == null)
            return null;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponData weapon = weapons[i];

            if (weapon != null && weapon.Price > 0 && !EquipmentManager.IsOwned(weapon))
                return weapon;
        }

        return null;
    }

    private static bool IsAdAvailable(string key)
    {
        return PlayerPrefs.GetString(key, string.Empty) != TodayKey();
    }

    private static void MarkAdClaimed(string key)
    {
        PlayerPrefs.SetString(key, TodayKey());
        PlayerPrefs.Save();
    }

    private static string TodayKey()
    {
        return DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static string FormatAmount(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }

    private void ShowToast(string message)
    {
        if (toastRoot == null || toastText == null)
            return;

        if (toastRoutine != null)
            StopCoroutine(toastRoutine);

        toastText.text = message;
        toastRoot.SetActive(true);

        if (toastGroup != null)
            toastGroup.alpha = 1f;

        toastRoutine = StartCoroutine(HideToast());
    }

    private IEnumerator HideToast()
    {
        yield return new WaitForSecondsRealtime(1.9f);

        if (toastGroup != null)
        {
            float timer = 0f;

            while (timer < 0.22f)
            {
                timer += Time.unscaledDeltaTime;
                toastGroup.alpha = 1f - Mathf.Clamp01(timer / 0.22f);
                yield return null;
            }
        }

        if (toastRoot != null)
            toastRoot.SetActive(false);

        toastRoutine = null;
    }
}
