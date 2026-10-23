using System;
using System.Collections;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuChromeUI : MonoBehaviour, ILangRefreshable
{
    private const string BonusAdDateKey =
        "ArcadeSurvivor.Menu.BonusAdDate";
    private const string WeaponAdDateKey =
        "ArcadeSurvivor.Menu.WeaponAdDate";

    private const int BonusAdCoins = 75;
    private const int FallbackWeaponCoins = 300;

    [Header("Top Bar")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private Button coinsPlusButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button statsButton;

    [Header("Reward Cards")]
    [SerializeField] private Button bonusAdButton;
    [SerializeField] private TMP_Text bonusAdValueText;
    [SerializeField] private GameObject bonusAdBadge;
    [SerializeField] private GameObject bonusesBadge;
    [SerializeField] private Button weaponAdButton;
    [SerializeField] private TMP_Text weaponAdTitleText;
    [SerializeField] private TMP_Text weaponAdHintText;
    [SerializeField] private TMP_Text weaponAdButtonLabel;
    [SerializeField] private GameObject weaponAdBadge;

    [Header("Panels")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private Button settingsCloseButton;
    [SerializeField] private Button statsCloseButton;
    [SerializeField] private Button profileCloseButton;
    [SerializeField] private Button tipsCloseButton;

    [Header("Stats")]
    [SerializeField] private TMP_Text statsLevelValue;
    [SerializeField] private TMP_Text statsXpValue;
    [SerializeField] private Slider statsLevelBar;
    [SerializeField] private TMP_Text statsAchievementsValue;
    [SerializeField] private TMP_Text statsBestWaveValue;
    [SerializeField] private TMP_Text statsBestScoreValue;
    [SerializeField] private TMP_Text statsRunsValue;

    [Header("Profile")]
    [SerializeField] private TMP_Text profileNameText;
    [SerializeField] private TMP_Text profileLevelText;
    [SerializeField] private TMP_Text profileCoinsText;
    [SerializeField] private TMP_Text profileBestWaveText;
    [SerializeField] private TMP_Text profileBestScoreText;
    [SerializeField] private Button profileAuthButton;
    [SerializeField] private TMP_Text profileAuthLabel;

    [Header("Tips")]
    [SerializeField] private Button tipsReplayButton;
    [SerializeField] private TMP_Text tipsStatusText;

    [Header("Misc")]
    [SerializeField] private Button exitButton;
    [SerializeField] private GameObject toastRoot;
    [SerializeField] private TMP_Text toastText;
    [SerializeField] private CanvasGroup toastGroup;

    private MainMenuUI mainMenu;
    private XpManager boundXp;
    private DailyRewardManager boundDailyRewards;
    private AchievementManager boundAchievements;
    private YandexGameManager boundYandex;
    private GameStateManager boundGameState;
    private Coroutine toastRoutine;
    private bool boundPersonalBest;

    private void Awake()
    {
        mainMenu = GetComponentInParent<MainMenuUI>();

        if (coinsPlusButton != null)
            coinsPlusButton.onClick.AddListener(ClaimBonusAd);

        if (bonusAdButton != null)
            bonusAdButton.onClick.AddListener(ClaimBonusAd);

        if (weaponAdButton != null)
            weaponAdButton.onClick.AddListener(ClaimWeaponAd);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (statsButton != null)
            statsButton.onClick.AddListener(OpenStats);

        if (settingsCloseButton != null)
            settingsCloseButton.onClick.AddListener(CloseSettings);

        if (statsCloseButton != null)
            statsCloseButton.onClick.AddListener(CloseSubPanels);

        if (profileCloseButton != null)
            profileCloseButton.onClick.AddListener(CloseSubPanels);

        if (tipsCloseButton != null)
            tipsCloseButton.onClick.AddListener(CloseSubPanels);

        if (profileAuthButton != null)
            profileAuthButton.onClick.AddListener(OpenAuthDialog);

        if (tipsReplayButton != null)
            tipsReplayButton.onClick.AddListener(ReplayTutorial);

        if (exitButton != null)
            exitButton.onClick.AddListener(QuitGame);
    }

    private void OnEnable()
    {
        BindServices();
        RefreshAll();
    }

    private void Start()
    {
        BindServices();
        RefreshAll();
    }

    private void OnDisable()
    {
        UnbindServices();

        if (toastRoutine != null)
        {
            StopCoroutine(toastRoutine);
            toastRoutine = null;
        }
    }

    private void OnDestroy()
    {
        UnbindServices();

        if (coinsPlusButton != null)
            coinsPlusButton.onClick.RemoveListener(ClaimBonusAd);

        if (bonusAdButton != null)
            bonusAdButton.onClick.RemoveListener(ClaimBonusAd);

        if (weaponAdButton != null)
            weaponAdButton.onClick.RemoveListener(ClaimWeaponAd);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);

        if (statsButton != null)
            statsButton.onClick.RemoveListener(OpenStats);

        if (settingsCloseButton != null)
            settingsCloseButton.onClick.RemoveListener(CloseSettings);

        if (statsCloseButton != null)
            statsCloseButton.onClick.RemoveListener(CloseSubPanels);

        if (profileCloseButton != null)
            profileCloseButton.onClick.RemoveListener(CloseSubPanels);

        if (tipsCloseButton != null)
            tipsCloseButton.onClick.RemoveListener(CloseSubPanels);

        if (profileAuthButton != null)
            profileAuthButton.onClick.RemoveListener(OpenAuthDialog);

        if (tipsReplayButton != null)
            tipsReplayButton.onClick.RemoveListener(ReplayTutorial);

        if (exitButton != null)
            exitButton.onClick.RemoveListener(QuitGame);
    }

    public void RefreshLang()
    {
        RefreshAll();
    }

    public void RefreshAll()
    {
        RefreshBalances();
        RefreshRewardCards();
        RefreshStats();
        RefreshProfile();
    }

    private void BindServices()
    {
        UnbindServices();

        boundXp = XpManager.Instance;

        if (boundXp != null)
        {
            boundXp.OnCoinsChanged += HandleCoinsChanged;
            boundXp.OnPlayerXPChanged += HandlePlayerXpChanged;
            boundXp.OnPlayerLevelChanged += HandlePlayerLevelChanged;
        }

        boundDailyRewards = DailyRewardManager.Instance;

        if (boundDailyRewards != null)
            boundDailyRewards.OnRewardClaimed += HandleDailyRewardClaimed;

        boundAchievements = AchievementManager.Instance;

        if (boundAchievements != null)
            boundAchievements.OnProgressChanged += HandleProgressChanged;

        PersonalBestRecord.OnChanged += HandlePersonalBestChanged;
        boundPersonalBest = true;

        boundYandex = YandexGameManager.Instance;

        if (boundYandex != null)
        {
            boundYandex.OnReady += HandleServicesReady;
            boundYandex.OnBestScoreChanged += HandleBestScoreChanged;
            boundYandex.OnAuthorizedChanged += HandleAuthorizedChanged;
        }

        boundGameState = GameStateManager.Instance;

        if (boundGameState != null)
            boundGameState.OnGameStateChanged += HandleGameStateChanged;
    }

    private void UnbindServices()
    {
        if (boundXp != null)
        {
            boundXp.OnCoinsChanged -= HandleCoinsChanged;
            boundXp.OnPlayerXPChanged -= HandlePlayerXpChanged;
            boundXp.OnPlayerLevelChanged -= HandlePlayerLevelChanged;
            boundXp = null;
        }

        if (boundDailyRewards != null)
        {
            boundDailyRewards.OnRewardClaimed -= HandleDailyRewardClaimed;
            boundDailyRewards = null;
        }

        if (boundAchievements != null)
        {
            boundAchievements.OnProgressChanged -= HandleProgressChanged;
            boundAchievements = null;
        }

        if (boundPersonalBest)
        {
            PersonalBestRecord.OnChanged -= HandlePersonalBestChanged;
            boundPersonalBest = false;
        }

        if (boundYandex != null)
        {
            boundYandex.OnReady -= HandleServicesReady;
            boundYandex.OnBestScoreChanged -= HandleBestScoreChanged;
            boundYandex.OnAuthorizedChanged -= HandleAuthorizedChanged;
            boundYandex = null;
        }

        if (boundGameState != null)
        {
            boundGameState.OnGameStateChanged -= HandleGameStateChanged;
            boundGameState = null;
        }
    }

    private void HandleCoinsChanged(int value)
    {
        RefreshBalances();
        RefreshProfile();
    }

    private void HandlePlayerXpChanged(int value)
    {
        RefreshBalances();
        RefreshStats();
        RefreshProfile();
    }

    private void HandlePlayerLevelChanged(int value)
    {
        RefreshBalances();
        RefreshStats();
        RefreshProfile();
    }

    private void HandleDailyRewardClaimed(int day)
    {
        RefreshRewardCards();
    }

    private void HandleProgressChanged()
    {
        RefreshStats();
    }

    private void HandlePersonalBestChanged()
    {
        RefreshStats();
        RefreshProfile();
    }

    private void HandleBestScoreChanged()
    {
        RefreshStats();
        RefreshProfile();
    }

    private void HandleAuthorizedChanged(bool authorized)
    {
        RefreshProfile();
    }

    private void HandleServicesReady()
    {
        RefreshAll();
    }

    private void HandleGameStateChanged(GameState state)
    {
        if (state == GameState.Menu)
            RefreshAll();
    }

    private void RefreshBalances()
    {
        XpManager xp = XpManager.Instance;

        if (coinsText != null)
            coinsText.text = FormatAmount(xp != null ? xp.GlobalCoins : 0);

        if (xpText != null)
        {
            xpText.text = xp != null
                ? xp.GlobalXPInCurrentLevel + " / " +
                  xp.GlobalXPNeededForNextLevel + " XP"
                : "0 / 0 XP";
        }
    }

    private void RefreshRewardCards()
    {
        bool bonusAvailable = IsBonusAdAvailable();
        bool dailyAvailable =
            DailyRewardManager.Instance != null &&
            DailyRewardManager.Instance.IsAvailableToday;

        if (bonusesBadge != null)
            bonusesBadge.SetActive(dailyAvailable);

        if (bonusesBadge != null && dailyAvailable)
            bonusesBadge.transform.SetAsLastSibling();

        if (bonusAdButton != null)
            bonusAdButton.interactable = bonusAvailable;

        if (bonusAdBadge != null)
            bonusAdBadge.SetActive(bonusAvailable);

        if (bonusAdValueText != null)
        {
            bonusAdValueText.text = bonusAvailable
                ? Lang.GetOr("menu.bonus_value", "+{0} coins")
                    .Replace("{0}", BonusAdCoins.ToString())
                : Lang.GetOr("menu.bonus_claimed", "Claimed today");
        }

        bool weaponAvailable = IsWeaponAdAvailable();
        WeaponData weapon = PickWeaponReward();

        if (weaponAdButton != null)
            weaponAdButton.interactable = weaponAvailable;

        if (weaponAdBadge != null)
            weaponAdBadge.SetActive(weaponAvailable);

        if (weaponAdButtonLabel != null)
        {
            weaponAdButtonLabel.text = weaponAvailable
                ? Lang.GetOr("menu.free", "FREE")
                : Lang.GetOr("menu.reward_claimed", "Claimed");
        }

        if (weaponAdTitleText != null)
        {
            weaponAdTitleText.text = weapon != null
                ? Lang.GetOr("menu.reward", "Weapon reward")
                : Lang.GetOr("menu.reward_coins", "Coin reward");
        }

        if (weaponAdHintText != null)
        {
            if (!weaponAvailable)
            {
                weaponAdHintText.text = Lang.GetOr(
                    "menu.reward_claimed",
                    "Come back tomorrow"
                );
            }
            else if (weapon != null)
            {
                weaponAdHintText.text = weapon.LocalizedName;
            }
            else
            {
                weaponAdHintText.text = Lang.GetOr(
                    "menu.reward_coins_desc",
                    "+{0} coins"
                ).Replace("{0}", FallbackWeaponCoins.ToString());
            }
        }
    }

    private void RefreshStats()
    {
        XpManager xp = XpManager.Instance;
        AchievementManager achievements = AchievementManager.Instance;
        YandexGameManager yandex = YandexGameManager.Instance;

        if (statsLevelValue != null)
        {
            statsLevelValue.text = xp != null
                ? xp.GlobalLevel.ToString()
                : "1";
        }

        if (statsXpValue != null)
        {
            statsXpValue.text = xp != null
                ? xp.GlobalXPInCurrentLevel + " / " +
                  xp.GlobalXPNeededForNextLevel
                : "0 / 0";
        }

        if (statsLevelBar != null)
            statsLevelBar.value = xp != null ? xp.GlobalLevelProgress : 0f;

        if (statsAchievementsValue != null)
        {
            statsAchievementsValue.text = achievements != null
                ? achievements.UnlockedCount + " / " +
                  achievements.All.Count
                : "0 / 0";
        }

        if (statsBestWaveValue != null)
        {
            statsBestWaveValue.text =
                PersonalBestRecord.BestWave.ToString();
        }

        if (statsBestScoreValue != null)
        {
            statsBestScoreValue.text = yandex != null
                ? FormatAmount(yandex.BestScore)
                : "0";
        }

        if (statsRunsValue != null)
        {
            statsRunsValue.text = yandex != null
                ? FormatAmount(yandex.RunCount)
                : "0";
        }
    }

    private void RefreshProfile()
    {
        XpManager xp = XpManager.Instance;
        YandexGameManager yandex = YandexGameManager.Instance;

        bool ready = yandex != null && yandex.IsReady;
        bool authorized = yandex != null && yandex.IsAuthorized;

        if (profileNameText != null)
        {
            bool hasName =
                authorized &&
                !string.IsNullOrWhiteSpace(yandex.PlayerName);

            profileNameText.text = hasName
                ? yandex.PlayerName
                : Lang.GetOr("menu.profile.guest", "Guest");
        }

        if (profileLevelText != null)
        {
            profileLevelText.text = xp != null
                ? Lang.Get("menu.level", xp.GlobalLevel)
                : Lang.Get("menu.level", 1);
        }

        if (profileCoinsText != null)
        {
            profileCoinsText.text = FormatAmount(
                xp != null ? xp.GlobalCoins : 0
            );
        }

        if (profileBestWaveText != null)
        {
            profileBestWaveText.text =
                PersonalBestRecord.BestWave.ToString();
        }

        if (profileBestScoreText != null)
        {
            profileBestScoreText.text = yandex != null
                ? FormatAmount(yandex.BestScore)
                : "0";
        }

        if (profileAuthButton != null)
            profileAuthButton.interactable = ready && !authorized;

        if (profileAuthLabel != null)
        {
            if (authorized)
            {
                profileAuthLabel.text = Lang.GetOr(
                    "menu.profile.authorized",
                    "Signed in"
                );
            }
            else if (ready)
            {
                profileAuthLabel.text = Lang.GetOr(
                    "menu.profile.auth",
                    "Sign in"
                );
            }
            else
            {
                profileAuthLabel.text = Lang.GetOr(
                    "menu.profile.not_ready",
                    "Connecting..."
                );
            }
        }
    }

    private void ClaimBonusAd()
    {
        if (!IsBonusAdAvailable())
        {
            ShowToast(Lang.GetOr("menu.bonus_claimed", "Claimed today"));
            return;
        }

        RunRewarded(YandexGameManager.RewardId_MenuBonus, GrantBonusAd);
    }

    private void GrantBonusAd()
    {
        if (!IsBonusAdAvailable())
            return;

        XpManager xp = XpManager.Instance;

        if (xp != null)
            xp.AddGlobalCoins(BonusAdCoins);

        MarkAdClaimed(BonusAdDateKey);

        RefreshRewardCards();
        RefreshBalances();
        RefreshProfile();

        ShowToast(
            Lang.GetOr("menu.bonus_toast", "+{0} coins")
                .Replace("{0}", BonusAdCoins.ToString())
        );
    }

    private void ClaimWeaponAd()
    {
        if (!IsWeaponAdAvailable())
        {
            ShowToast(
                Lang.GetOr("menu.reward_claimed", "Claimed today")
            );
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
        if (!IsWeaponAdAvailable())
            return;

        bool grantedWeapon = false;

        if (reward != null && !EquipmentManager.IsOwned(reward))
        {
            EquipmentManager.GrantWeapon(reward.WeaponName);
            grantedWeapon = true;
        }
        else
        {
            XpManager xp = XpManager.Instance;

            if (xp != null)
                xp.AddGlobalCoins(FallbackWeaponCoins);
        }

        MarkAdClaimed(WeaponAdDateKey);

        RefreshRewardCards();
        RefreshBalances();
        RefreshProfile();

        if (grantedWeapon)
        {
            ShowToast(
                Lang.GetOr("menu.reward_weapon_toast", "Weapon unlocked")
            );
        }
        else
        {
            ShowToast(
                Lang.GetOr("menu.reward_coins_toast", "+{0} coins")
                    .Replace("{0}", FallbackWeaponCoins.ToString())
            );
        }
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

    private static bool IsBonusAdAvailable()
    {
        return IsAdAvailable(BonusAdDateKey);
    }

    private static bool IsWeaponAdAvailable()
    {
        return IsAdAvailable(WeaponAdDateKey);
    }

    private static bool IsAdAvailable(string key)
    {
        string saved = PlayerPrefs.GetString(key, string.Empty);

        return saved != TodayKey();
    }

    private static void MarkAdClaimed(string key)
    {
        PlayerPrefs.SetString(key, TodayKey());
        PlayerPrefs.Save();
    }

    private static string TodayKey()
    {
        return DateTime.Today.ToString(
            "yyyy-MM-dd",
            CultureInfo.InvariantCulture
        );
    }

    private static WeaponData PickWeaponReward()
    {
        UpgradeManager upgrades = UpgradeManager.Instance;

        if (upgrades == null)
            return null;

        System.Collections.Generic.IReadOnlyList<WeaponData> weapons =
            upgrades.GetAvailableWeapons();

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponData weapon = weapons[i];

            if (weapon != null &&
                weapon.Price > 0 &&
                !EquipmentManager.IsOwned(weapon))
            {
                return weapon;
            }
        }

        return null;
    }

    private void OpenSettings()
    {
        if (settingsPanel == null)
        {
            ShowToast(Lang.GetOr("menu.settings_missing", "Settings unavailable"));
            return;
        }

        if (mainMenu == null)
            mainMenu = GetComponentInParent<MainMenuUI>();

        if (mainMenu != null)
            mainMenu.OpenSubPanel(settingsPanel);
        else
            settingsPanel.SetActive(true);
    }

    private void OpenStats()
    {
        if (statsPanel == null)
        {
            ShowToast(Lang.GetOr("menu.settings_missing", "Stats unavailable"));
            return;
        }

        if (mainMenu == null)
            mainMenu = GetComponentInParent<MainMenuUI>();

        if (mainMenu != null)
            mainMenu.OpenSubPanel(statsPanel);
        else
            statsPanel.SetActive(true);
    }

    private void CloseSettings()
    {
        CloseSubPanels();
    }

    private void CloseSubPanels()
    {
        if (mainMenu == null)
            mainMenu = GetComponentInParent<MainMenuUI>();

        if (mainMenu != null)
        {
            mainMenu.CloseSubPanels();
            return;
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (statsPanel != null)
            statsPanel.SetActive(false);
    }

    private void OpenAuthDialog()
    {
        YandexGameManager yandex = YandexGameManager.Instance;

        if (yandex == null || !yandex.IsReady)
        {
            ShowToast(Lang.GetOr("menu.profile.not_ready", "Connecting..."));
            return;
        }

        yandex.OpenAuthDialog();
    }

    private void ReplayTutorial()
    {
        TutorialManager.IsCompleted = false;
        PlayerPrefs.Save();

        if (tipsStatusText != null)
            tipsStatusText.text = Lang.Get("menu.tutorial_reset");

        ShowToast(Lang.Get("menu.tutorial_reset"));
    }

    private void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
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

        toastRoutine = StartCoroutine(HideToastRoutine());
    }

    private IEnumerator HideToastRoutine()
    {
        yield return new WaitForSecondsRealtime(1.8f);

        if (toastGroup != null)
        {
            float timer = 0f;

            while (timer < 0.25f)
            {
                timer += Time.unscaledDeltaTime;
                toastGroup.alpha = 1f - Mathf.Clamp01(timer / 0.25f);
                yield return null;
            }
        }

        if (toastRoot != null)
            toastRoot.SetActive(false);

        toastRoutine = null;
    }

    private static string FormatAmount(int value)
    {
        return value.ToString("N0", CultureInfo.InvariantCulture);
    }
}
