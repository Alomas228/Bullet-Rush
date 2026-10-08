using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    public bool IsGameOver { get; private set; }

    private bool pendingRestart;
    private bool menuReturnPending;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (pendingRestart)
        {
            pendingRestart = false;

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetState(GameState.Playing);
        }
    }

    public void GameOver()
    {
        if (IsGameOver)
            return;

        IsGameOver = true;

        // Рекорд фиксируется в момент смерти: волна, на которой игрок
        // погиб, и есть его достижение («добраться хотя бы до 18»).
        SubmitPersonalBest();

        // ============================================
        // LEADERBOARD: Собираем метрики
        // ============================================
        RunMetrics metrics =
            FindAnyObjectByType<RunMetrics>();
        
        if (metrics != null)
        {
            RunResult result = metrics.CollectResult();
            
            // Отправляем на сервер
            LeaderboardService.Instance?.SubmitResult(result);
            
            // Показываем UI
            GameOverLeaderboardUI lbUI =
                FindAnyObjectByType<GameOverLeaderboardUI>();
            if (lbUI != null)
                lbUI.ShowResults(metrics);
        }

        Debug.Log("[GameOver] entered; XpManager.Instance present: " + (XpManager.Instance != null) + ", IsRunActive: " + (XpManager.Instance != null ? XpManager.Instance.IsRunActive.ToString() : "n/a"));

        if (XpManager.Instance != null)
            XpManager.Instance.ProcessRunEnd();

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.GameOver);

        PlayGameOverSound();

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMusic();

        Time.timeScale = 0f;
    }

    /// <summary>
    /// Сохраняет волну текущего забега как личный рекорд, если она
    /// лучше сохранённой. Вызывается на всех выходах из забега:
    /// смерть, рестарт, выход в меню.
    /// </summary>
    private void SubmitPersonalBest()
    {
        WaveManager waves = FindAnyObjectByType<WaveManager>();

        if (waves != null)
            PersonalBestRecord.Submit(waves.CurrentWave);
    }

    public void RestartGame()
    {
        SubmitPersonalBest();

        IsGameOver = false;
        pendingRestart = true;
        Time.timeScale = 1f;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    /// <summary>
    /// Возрождение игрока через rewarded-рекламу: отменяет Game Over и
    /// продолжает забег в текущей волне.
    /// </summary>
    public void RevivePlayer()
    {
        if (!IsGameOver)
            return;

        IsGameOver = false;

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Playing);

        Time.timeScale = 1f;

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Revive();
    }

    public void BackToMenu()
    {
        SubmitPersonalBest();

        IsGameOver = false;

        // Если забег не был завершён через GameOver() (выход из паузы),
        // начисляем награду сейчас. Повторная выдача заблокирована внутри XpManager.
        if (XpManager.Instance != null)
            XpManager.Instance.ProcessRunEnd();

        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToMainMusic();

        Time.timeScale = 1f;

        MainMenuUI.MenuReloadPending = true;

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Menu);

        WorldFadeOutManager fadeOut =
            FindAnyObjectByType<WorldFadeOutManager>();

        if (fadeOut != null)
            fadeOut.FadeOutEverything();

        if (!menuReturnPending)
            StartCoroutine(ReloadAfterCameraReachesMenu(fadeOut));
    }

    private IEnumerator ReloadAfterCameraReachesMenu(
        WorldFadeOutManager fadeOut)
    {
        menuReturnPending = true;

        const float maxWait = 5f;
        float timer = 0f;

        CameraFollow camera = FindAnyObjectByType<CameraFollow>();

        if (fadeOut == null)
            fadeOut = FindAnyObjectByType<WorldFadeOutManager>();

        while (
            timer < maxWait &&
                ((camera != null && camera.IsTransitioning) ||
                 (fadeOut != null && fadeOut.IsFading))
        )
        {
            timer += Time.unscaledDeltaTime;

            if (GameStateManager.Instance == null ||
                GameStateManager.Instance.CurrentState != GameState.Menu)
            {
                CancelMenuReturn();
                yield break;
            }

            yield return null;
        }

        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Menu)
        {
            CancelMenuReturn();
            yield break;
        }

        menuReturnPending = false;
        MainMenuUI.MenuReloadPending = false;
        MainMenuUI.ShowWithoutAnimation = true;

        SceneManager.LoadScene(
            SceneManager.GetActiveScene().buildIndex
        );
    }

    private void CancelMenuReturn()
    {
        menuReturnPending = false;
        MainMenuUI.MenuReloadPending = false;
        MainMenuUI.ShowWithoutAnimation = false;
    }

    private void PlayGameOverSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.GameOver,
                priority: SfxPriority.Critical
            );
    }
}