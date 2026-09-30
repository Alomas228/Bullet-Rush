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

    private bool subscribed;
    private bool warnedAboutRoot;

    private float cachedHealthMax = float.NaN;
    private float cachedHealth = float.NaN;
    private int cachedScore = int.MinValue;
    private int cachedWave = int.MinValue;
    private int cachedWaveLeft = int.MinValue;
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

        bool visible = state == GameState.Playing;

        if (hudRoot.activeSelf != visible)
            hudRoot.SetActive(visible);
    }

    private void Update()
    {
        if (hudRoot != null && !hudRoot.activeSelf)
            return;

        if (playerHealth != null && healthSlider != null)
        {
            float maxHealth = playerHealth.MaxHealth;

            if (!Mathf.Approximately(maxHealth, cachedHealthMax))
            {
                cachedHealthMax = maxHealth;

                healthSlider.minValue = 0f;
                healthSlider.maxValue = maxHealth;
            }

            float health = playerHealth.CurrentHealth;

            if (!Mathf.Approximately(health, cachedHealth))
            {
                cachedHealth = health;

                healthSlider.value = health;
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
            int left = waveManager.EnemiesLeft;

            // Обновляем и по смене волны, и по числу оставшихся:
            // счётчик обязан реагировать на убийства, иначе он
            // показывает только стартовое число и быстро врёт.
            if (wave != cachedWave || left != cachedWaveLeft)
            {
                cachedWave = wave;
                cachedWaveLeft = left;

                // WAVE: 0 выглядит багом — во время обучения волны ещё нет.
                waveText.text =
                    wave > 0
                        ? Lang.Get("hud.wave_left", wave, left)
                        : string.Empty;
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
}