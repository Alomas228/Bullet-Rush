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
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private TMP_Text coinsText;

    [Header("Visibility")]
    [Tooltip("Корень HUD (HP/score/wave + кнопка паузы). Показывается только во время боя. Сам объект GameplayUI должен оставаться активным.")]
    [SerializeField] private GameObject hudRoot;

    private bool subscribed;
    private bool warnedAboutRoot;

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
            healthSlider.minValue = 0f;
            healthSlider.maxValue = playerHealth.MaxHealth;
            healthSlider.value = playerHealth.CurrentHealth;
        }

        if (scoreManager != null && scoreText != null)
        {
            scoreText.text = $"SCORE: {scoreManager.Score}";
        }

        if (waveManager != null && waveText != null)
        {
            waveText.text = $"WAVE: {waveManager.CurrentWave}";
        }

        if (XpManager.Instance != null && xpText != null)
        {
            xpText.text =
                $"LVL {XpManager.Instance.RunLevel} | " +
                $"XP {XpManager.Instance.RunXP}/{XpManager.Instance.RunXPToNextLevel}";
        }

        if (XpManager.Instance != null && coinsText != null)
        {
            coinsText.text =
                $"{XpManager.Instance.RunCoins}";
        }
    }
}