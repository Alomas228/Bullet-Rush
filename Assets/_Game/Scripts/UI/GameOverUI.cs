using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

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

    [Header("XP Bar Animation")]
    [Tooltip("Сколько секунд занимает анимация роста XP на экране Game Over.")]
    [SerializeField] private float xpAnimationDuration = 3f;

    [Header("Rewarded Ads")]
    [Tooltip("Если кнопки не назначены в сцене, создавать их автоматически при показе.")]
    [SerializeField] private bool createRewardedButtonsAtRuntime = true;
    [Tooltip("Кнопка «x2 монеты» (rewarded). Необязательно: создаётся в рантайме.")]
    [SerializeField] private Button doubleCoinsButton;
    [Tooltip("Кнопка «Возродиться» (rewarded). Необязательно: создаётся в рантайме.")]
    [SerializeField] private Button reviveButton;

    private Coroutine xpAnimCoroutine;
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
        SetupRewardedButtons();
    }

    private void ShowPlayerProgression()
    {
        XpManager xp = XpManager.Instance;

        if (xp == null)
            return;

        if (playerXpText != null)
            playerXpText.text = $"Player XP +{xp.LastRunReward}";

        if (levelUpText != null)
        {
            levelUpText.gameObject.SetActive(
                xp.LevelsGainedLastRun > 0
            );

            if (xp.LevelsGainedLastRun > 0)
            {
                levelUpText.text =
                    $"LEVEL {xp.LastLevelBeforeGrant} -> " +
                    $"LEVEL {xp.GetPlayerLevel()}";
            }
        }

        if (levelText != null || xpProgressText != null || xpProgressBar != null)
        {
            if (xpAnimCoroutine != null)
                StopCoroutine(xpAnimCoroutine);

            xpAnimCoroutine =
                StartCoroutine(AnimateXpSlider(xp));
        }
    }

    private IEnumerator AnimateXpSlider(XpManager xp)
    {
        int totalBefore =
            Mathf.Max(xp.GlobalXP - xp.LastRunReward, 0);

        int totalAfter = xp.GlobalXP;

        float duration = Mathf.Max(xpAnimationDuration, 0.01f);
        float elapsed = 0f;

        DisplayXpAt(xp, totalBefore);

        while (elapsed < duration && totalAfter > totalBefore)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            int currentTotal =
                Mathf.RoundToInt(
                    Mathf.Lerp(totalBefore, totalAfter, t)
                );

            DisplayXpAt(xp, currentTotal);

            yield return null;
        }

        DisplayXpAt(xp, totalAfter);

        xpAnimCoroutine = null;
    }

    private void DisplayXpAt(XpManager xp, int totalXp)
    {
        int level = Mathf.Max(xp.GetLevelForXP(totalXp), 1);

        int levelStartXp = xp.GetXPForLevel(level);
        int levelEndXp = xp.GetXPForLevel(level + 1);

        int xpInLevel =
            Mathf.Max(totalXp - levelStartXp, 0);

        int neededInLevel =
            Mathf.Max(levelEndXp - levelStartXp, 1);

        if (levelText != null)
            levelText.text = $"LEVEL {level}";

        if (xpProgressText != null)
            xpProgressText.text =
                $"{xpInLevel} / {neededInLevel} XP";

        if (xpProgressBar != null)
            xpProgressBar.value =
                Mathf.Clamp01((float)xpInLevel / neededInLevel);
    }

    private void SetupRewardedButtons()
    {
        if (createRewardedButtonsAtRuntime && doubleCoinsButton == null)
        {
            doubleCoinsButton =
                CreateRewardedButton(
                    "x2 COINS",
                    OnDoubleCoinsClicked,
                    1
                );
        }

        if (createRewardedButtonsAtRuntime && reviveButton == null)
        {
            reviveButton =
                CreateRewardedButton(
                    "REVIVE",
                    OnReviveClicked,
                    2
                );
        }

        if (doubleCoinsButton != null)
        {
            doubleCoinsButton.onClick.RemoveListener(OnDoubleCoinsClicked);
            doubleCoinsButton.onClick.AddListener(OnDoubleCoinsClicked);
        }

        if (reviveButton != null)
        {
            reviveButton.onClick.RemoveListener(OnReviveClicked);
            reviveButton.onClick.AddListener(OnReviveClicked);
        }
    }

    private Button CreateRewardedButton(
        string label,
        UnityEngine.Events.UnityAction onClick,
        int positionIndex)
    {
        Button source =
            restartButton != null
                ? restartButton
                : menuButton;

        if (source == null)
            return null;

        Button clone =
            Instantiate(source, source.transform.parent);

        clone.name = "Btn_" + label.Replace(" ", "_");
        clone.onClick.RemoveAllListeners();

        TMP_Text tmpText =
            clone.GetComponentInChildren<TMP_Text>(true);

        if (tmpText != null)
            tmpText.text = label;

        RectTransform cloneRect =
            clone.GetComponent<RectTransform>();

        RectTransform sourceRect =
            source.GetComponent<RectTransform>();

        if (cloneRect != null && sourceRect != null)
        {
            cloneRect.anchorMin = sourceRect.anchorMin;
            cloneRect.anchorMax = sourceRect.anchorMax;
            cloneRect.pivot = sourceRect.pivot;

            float step =
                Mathf.Max(sourceRect.sizeDelta.y, 40f) +
                12f;

            Vector2 offset =
                cloneRect.anchoredPosition;

            cloneRect.anchoredPosition =
                new Vector2(
                    offset.x,
                    offset.y - step * positionIndex
                );
        }

        return clone;
    }

    private void OnDoubleCoinsClicked()
    {
        if (doubleCoinsButton != null)
            doubleCoinsButton.interactable = false;

        YandexGameManager yg =
            YandexGameManager.Instance;

        if (yg == null)
            return;

        yg.ShowRewarded(
            YandexGameManager.RewardId_DoubleCoins,
            ApplyDoubleCoinsReward
        );
    }

    private void ApplyDoubleCoinsReward()
    {
        XpManager xp = XpManager.Instance;

        if (xp == null)
            return;

        xp.AddCoins(xp.RunCoins);

        if (coinsText != null)
            coinsText.text = $"Coins Earned: {xp.RunCoins}";
    }

    private void OnReviveClicked()
    {
        if (reviveButton != null)
            reviveButton.interactable = false;

        YandexGameManager yg =
            YandexGameManager.Instance;

        if (yg == null)
            return;

        yg.ShowRewarded(
            YandexGameManager.RewardId_Revive,
            ApplyReviveReward
        );
    }

    private void ApplyReviveReward()
    {
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.RevivePlayer();
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