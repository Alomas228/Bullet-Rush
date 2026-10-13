using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOverManager : MonoBehaviour
{
    public static GameOverManager Instance { get; private set; }

    public bool IsGameOver { get; private set; }

    /// <summary>
    /// Итог последнего завершения забега. Считается один раз и
    /// используется и лидербордом, и экраном итогов, чтобы оба
    /// показывали одинаковый счёт.
    /// </summary>
    public RunResult LastResult { get; private set; }

    [Header("Achievements")]
    [Tooltip("Сколько волн нужно пройти, чтобы забег считался победным (для ARSENAL). " +
             "10 соответствует первой босс-волне.")]
    [SerializeField] private int runWinWave = 10;

    private bool pendingRestart;
    private bool menuReturnPending;

    // Итоги забега фиксируются один раз на его завершение. При
    // возрождении флаг снимается: забег продолжается, и следующий
    // его конец должен снова зафиксировать (уже улучшенный) результат.
    private bool runFinalized;

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
            runFinalized = false;
            LastResult = null;

            if (GameStateManager.Instance != null)
                GameStateManager.Instance.SetState(GameState.Playing);
        }
    }

    public void GameOver()
    {
        if (IsGameOver)
            return;

        IsGameOver = true;

        // Результат собирается ровно один раз: и лидерборд, и экран
        // итогов читают один и тот же объект, поэтому расхождений
        // между показанным счётом и отправленным больше нет.
        RunMetrics metrics =
            FindAnyObjectByType<RunMetrics>();

        LastResult = metrics != null
            ? metrics.CollectResult()
            : null;

        // Фиксируем завершение забега (достижения, рекорд, лидерборд,
        // награда) один раз. При возрождении флаг снимется.
        FinalizeRun();

        GameOverLeaderboardUI lbUI =
            FindAnyObjectByType<GameOverLeaderboardUI>();

        if (lbUI != null)
            lbUI.ShowResults(LastResult);

        Debug.Log("[GameOver] entered; XpManager.Instance present: " + (XpManager.Instance != null) + ", IsRunActive: " + (XpManager.Instance != null ? XpManager.Instance.IsRunActive.ToString() : "n/a"));

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.GameOver);

        PlayGameOverSound();

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMusic();

        Time.timeScale = 0f;
    }

    /// <summary>
    /// Фиксирует завершение забега один раз: достижения, личный рекорд,
    /// лидерборд и награда игрока. Повторные вызовы до возрождения
    /// или нового забега игнорируются.
    /// </summary>
    private void FinalizeRun()
    {
        if (runFinalized)
            return;

        runFinalized = true;

        if (LastResult == null)
        {
            RunMetrics metrics =
                FindAnyObjectByType<RunMetrics>();

            LastResult = metrics != null
                ? metrics.CollectResult()
                : null;
        }

        // Сообщаем о конце забега достижениям: по числу пройденных
        // волн определяется победа (для ARSENAL и подобных).
        ReportRunEnd();

        // Рекорд фиксируется в момент смерти: волна, на которой игрок
        // погиб, и есть его достижение («добраться хотя бы до 18»).
        SubmitPersonalBest();

        if (LastResult != null && LeaderboardService.Instance != null)
            LeaderboardService.Instance.SubmitResult(LastResult);

        if (XpManager.Instance != null)
            XpManager.Instance.ProcessRunEnd();
    }

    /// <summary>
    /// Сохраняет волну текущего забега как личный рекорд, если она
    /// лучше сохранённой. Вызывается на всех выходах из забега:
    /// смерть, рестарт, выход в меню.
    /// </summary>
    /// <summary>
    /// Сообщает о завершении забега достижениям. Передаём имя
    /// снаряжённого оружия, чтобы ARSENAL мог засчитать коллекцию.
    /// </summary>
    private void ReportRunEnd()
    {
        string weaponName = EquipmentManager.EquippedWeaponName;

        int waveReached = 0;
        WaveManager waves = FindAnyObjectByType<WaveManager>();

        if (waves != null)
            waveReached = waves.CurrentWave;

        // Забег считается победным, если игрок дошёл до пороговой волны,
        // даже если потом погиб. Порог настраивается в инспекторе.
        bool won = waveReached >= runWinWave;

        AchievementEvents.RaiseRunEnded(new RunEndInfo
        {
            won = won,
            weaponName = weaponName,
            waveReached = waveReached
        });
    }

    private void SubmitPersonalBest()
    {
        WaveManager waves = FindAnyObjectByType<WaveManager>();

        if (waves != null)
            PersonalBestRecord.Submit(waves.CurrentWave);
    }

    public void RestartGame()
    {
        FinalizeRun();

        runFinalized = false;
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

        // Забег продолжается: снимаем фиксацию, чтобы следующий конец
        // снова зафиксировал уже улучшенный результат.
        runFinalized = false;

        IsGameOver = false;

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.Playing);

        Time.timeScale = 1f;

        if (PlayerHealth.Instance != null)
            PlayerHealth.Instance.Revive();
    }

    public void BackToMenu()
    {
        // Забег прерван (выход из паузы или с экрана итогов) — фиксируем
        // результат один раз: рекорд, лидерборд, достижения и награда.
        FinalizeRun();

        runFinalized = false;
        IsGameOver = false;

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