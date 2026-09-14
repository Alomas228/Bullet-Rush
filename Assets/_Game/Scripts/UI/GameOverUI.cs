using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameOverUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;

    [Header("Player XP (run reward)")]
    [SerializeField] private TMP_Text playerXpText;
    [SerializeField] private TMP_Text levelText;
    [Tooltip("Показывается при повышении уровня: «LEVEL 9 → LEVEL 10».")]
    [SerializeField] private TMP_Text levelUpText;
    [SerializeField] private TMP_Text xpProgressText;
    [Tooltip("Заполненная полоса прогресса текущего уровня.")]
    [SerializeField] private Slider xpProgressBar;

    private bool subscribed;

    private void Awake()
    {
        if (restartButton != null)
            restartButton.onClick.AddListener(OnRestartClicked);

        if (menuButton != null)
            menuButton.onClick.AddListener(OnMenuClicked);
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        if (restartButton != null)
            restartButton.onClick.RemoveListener(OnRestartClicked);

        if (menuButton != null)
            menuButton.onClick.RemoveListener(OnMenuClicked);
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
        if (state == GameState.GameOver)
            return;

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void Update()
    {
        if (GameOverManager.Instance == null)
            return;

        if (GameOverManager.Instance.IsGameOver &&
            gameOverPanel != null &&
            !gameOverPanel.activeSelf)
        {
            ShowGameOver();
        }
    }

    private void ShowGameOver()
    {
        gameOverPanel.SetActive(true);

        if (scoreText != null && ScoreManager.Instance != null)
            scoreText.text = $"Score: {ScoreManager.Instance.Score}";

        if (waveText != null)
        {
            WaveManager waveManager =
                FindAnyObjectByType<WaveManager>();

            if (waveManager != null)
                waveText.text = $"Wave Reached: {waveManager.CurrentWave}";
        }

        if (killsText != null && ScoreManager.Instance != null)
            killsText.text = $"Kills: {ScoreManager.Instance.Kills}";

        if (coinsText != null && XpManager.Instance != null)
            coinsText.text = $"Coins Earned: {XpManager.Instance.RunCoins}";

        ShowPlayerProgression();
    }

    private void ShowPlayerProgression()
    {
        XpManager xp = XpManager.Instance;

        if (xp == null)
            return;

        if (playerXpText != null)
            playerXpText.text = $"Player XP +{xp.LastRunReward}";

        int level = xp.GetPlayerLevel();

        if (levelText != null)
        {
            levelText.text =
                xp.LevelsGainedLastRun > 0
                    ? "LEVEL UP!"
                    : $"LEVEL {level}";
        }

        if (levelUpText != null)
        {
            levelUpText.gameObject.SetActive(
                xp.LevelsGainedLastRun > 0
            );

            if (xp.LevelsGainedLastRun > 0)
            {
                levelUpText.text =
                    $"LEVEL {xp.LastLevelBeforeGrant} -> LEVEL {level}";
            }
        }

        if (xpProgressText != null)
        {
            xpProgressText.text =
                $"{xp.GlobalXPInCurrentLevel} / {xp.GlobalXPNeededForNextLevel} XP";
        }

        if (xpProgressBar != null)
        {
            xpProgressBar.value = xp.GlobalLevelProgress;
        }
    }

    private void OnRestartClicked()
    {
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.RestartGame();
    }

    private void OnMenuClicked()
    {
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.BackToMenu();
    }
}