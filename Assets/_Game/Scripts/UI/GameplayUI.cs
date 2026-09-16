using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameplayUI : MonoBehaviour
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
    private int cachedCoins = int.MinValue;

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

                scoreText.text = $"SCORE: {score}";
            }
        }

        if (waveManager != null && waveText != null)
        {
            int wave = waveManager.CurrentWave;

            if (wave != cachedWave)
            {
                cachedWave = wave;

                waveText.text = $"WAVE: {wave}";
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