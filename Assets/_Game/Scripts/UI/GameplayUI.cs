using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayUI : MonoBehaviour, ILangRefreshable
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private WaveManager waveManager;

    [Header("UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text coinsText;

    [Header("Visibility")]
    [Tooltip("Корень HUD (HP/score/wave + кнопка паузы). Показывается только во время боя. Сам объект GameplayUI должен оставаться активным.")]
    [SerializeField] private GameObject hudRoot;

    private TMP_Text healthLabel;

    public void BindHealthLabel(TMP_Text label)
    {
        healthLabel = label;
        cachedHealth = cachedHealthMax = float.NaN;
    }

    private bool subscribed;
    private bool warnedAboutRoot;
    private bool pvpHidden;

    private float cachedHealthMax = float.NaN;
    private float cachedHealth = float.NaN;
    private int cachedScore = int.MinValue;
    private int cachedWave = int.MinValue;
    private int cachedWaveLeft = int.MinValue;
    private int cachedBossesDefeated = int.MinValue;
    private bool cachedEndless;
    private WaveGoal cachedWaveGoal;
    private bool cachedWaveComplete;
    private bool cachedWaveFinishing;
    private int cachedCoins = int.MinValue;

    /// <summary>
    /// Сбрасывает кэши, чтобы Update перерисовал подписи на новом
    /// языке. Значения не меняются, поэтому без сброса текст остался
    /// бы на старом языке до следующего изменения счёта или волны.
    /// </summary>
    public void RefreshLang()
    {
        cachedScore = int.MinValue;
        cachedWave = int.MinValue;
        cachedWaveLeft = int.MinValue;
        cachedCoins = int.MinValue;
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();

        ApplyVisibility(
            GameStateManager.Instance != null
                ? GameStateManager.Instance.CurrentState
                : GameState.Menu
        );
    }

    private void OnDisable()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
    }

    private void EnsureSubscribed()
    {
        if (subscribed)
            return;

        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
        subscribed = true;
    }

    private void HandleStateChanged(GameState state)
    {
        ApplyVisibility(state);
    }

    private void ApplyVisibility(GameState state)
    {
        if (hudRoot == null)
        {
            if (!warnedAboutRoot)
            {
                warnedAboutRoot = true;

                Debug.LogWarning(
                    "GameplayUI: hudRoot не назначен. " +
                    "Назначь контейнер HUD (квитанции + кнопка паузы), " +
                    "иначе он не будет скрываться в меню.",
                    this
                );
            }

            return;
        }

        bool visible = state == GameState.Playing && !GameModeManager.IsPvp && !pvpHidden;

        if (hudRoot.activeSelf != visible)
            hudRoot.SetActive(visible);
    }

    public void SetPvpHidden(bool hidden)
    {
        pvpHidden = hidden;
        ApplyVisibility(
            GameStateManager.Instance != null
                ? GameStateManager.Instance.CurrentState
                : GameState.Menu
        );
    }

    private void Update()
    {
        if (hudRoot != null && !hudRoot.activeSelf)
            return;

        if (playerHealth != null && healthSlider != null)
        {
            float maxHealth = playerHealth.MaxHealth;

            bool maxChanged = !Mathf.Approximately(maxHealth, cachedHealthMax);
            if (maxChanged)
            {
                cachedHealthMax = maxHealth;

                healthSlider.minValue = 0f;
                healthSlider.maxValue = maxHealth;
            }

            float health = playerHealth.CurrentHealth;

            if (maxChanged || !Mathf.Approximately(health, cachedHealth))
            {
                cachedHealth = health;

                healthSlider.value = health;
                if (healthLabel != null)
                    healthLabel.SetText("{0}/{1}", Mathf.Ceil(health), Mathf.Ceil(maxHealth));
            }
        }

        if (scoreManager != null && scoreText != null)
        {
            int score = scoreManager.Score;

            if (score != cachedScore)
            {
                cachedScore = score;

                scoreText.text = Lang.Get("hud.score", score);
            }
        }

        if (waveManager != null && waveText != null)
        {
            int wave = waveManager.CurrentWave;
            WaveGoal goal = waveManager.CurrentGoal;
            bool finishing = waveManager.IsFinishingWave;
            int left = finishing ? waveManager.CleanupEnemiesRemaining : waveManager.IsTimedWave
                ? Mathf.CeilToInt(waveManager.WaveTimeRemaining)
                : goal == WaveGoal.Hunt ? waveManager.HuntTargetsRemaining : 0;
            int defeated = waveManager.BossesDefeated;
            bool endless = waveManager.EndlessMode;
            bool complete = waveManager.IsWaveComplete;

            // Обновляем и по смене волны, и по числу оставшихся:
            // счётчик обязан реагировать на убийства, иначе он
            // показывает только стартовое число и быстро врёт.
            if (wave != cachedWave || left != cachedWaveLeft || defeated != cachedBossesDefeated ||
                endless != cachedEndless || goal != cachedWaveGoal || complete != cachedWaveComplete ||
                finishing != cachedWaveFinishing)
            {
                cachedWave = wave;
                cachedWaveLeft = left;
                cachedBossesDefeated = defeated;
                cachedEndless = endless;
                cachedWaveGoal = goal;
                cachedWaveComplete = complete;
                cachedWaveFinishing = finishing;

                waveText.text = wave > 0 ? BuildWaveStatus(wave, left, defeated, endless, goal, complete, finishing) : string.Empty;
            }
        }

        if (XpManager.Instance != null && coinsText != null)
        {
            int coins = XpManager.Instance.RunCoins;

            if (coins != cachedCoins)
            {
                cachedCoins = coins;

                coinsText.text = $"{coins}";
            }
        }
    }

    private static string BuildWaveStatus(int wave, int left, int defeated, bool endless, WaveGoal goal, bool complete, bool finishing)
    {
        string waveNumber = endless ? Lang.Get("wave.number", wave) :
            Lang.Get("hud.wave_campaign", wave, BossCampaign.FinalWave);
        string bossStatus;
        if (BossCampaign.IsBossWave(wave, endless) && (endless || defeated < wave / BossCampaign.Interval))
            bossStatus = Lang.Get("hud.boss_current");
        else if (!endless && wave >= BossCampaign.FinalWave)
            bossStatus = Lang.Get("hud.campaign_cleared");
        else
            bossStatus = Lang.Get("hud.boss_next_wave", (wave / BossCampaign.Interval + 1) * BossCampaign.Interval);

        string objective;
        if (complete)
            objective = Lang.Get("hud.wave_complete");
        else if (finishing)
            objective = left > 0
                ? GameplayJourney.T("Добивание", "Cleanup", "Temizlik") + $": {left}"
                : GameplayJourney.T("Враги отступают", "Enemies retreating", "Dusmanlar cekiliyor");
        else
        {
            switch (goal)
            {
                case WaveGoal.Coordinator:
                    objective = GameplayJourney.T("Уничтожь координатора", "Eliminate the coordinator", "Koordinatörü yok et");
                    break;
                case WaveGoal.Hunt:
                    objective = GameplayJourney.T("Элитных целей", "Elite targets", "Elit hedefler") + $": {left}";
                    break;
                case WaveGoal.Boss:
                    objective = GameplayJourney.T("Победи босса", "Defeat the boss", "Bossu yen");
                    break;
                default:
                    string label = goal == WaveGoal.Horde
                        ? GameplayJourney.T("Орда", "Horde", "Sürü")
                        : GameplayJourney.T("Выживание", "Survive", "Hayatta kal");
                    string color = left <= 5 ? "#FF8060" : "#FFFFFF";
                    objective = $"{label}: <color={color}>{left / 60:00}:{left % 60:00}</color>";
                    break;
            }
        }
        return $"{waveNumber} · <color=#{ColorUtility.ToHtmlStringRGB(ArcadeUiTheme.Gold)}>{bossStatus}</color>\n<size=85%>{objective}</size>";
    }
}
