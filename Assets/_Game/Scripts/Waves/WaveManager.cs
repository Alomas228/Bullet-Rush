using UnityEngine;
using System.Collections;

public class WaveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private WaveUI waveUI;
    [SerializeField] private UpgradeUI upgradeUI;
    [SerializeField] private WorldStructureGenerator worldGenerator;

    [Header("Wave Settings")]
    [SerializeField] private int startingEnemies = 5;
    [SerializeField] private int enemiesAddedPerWave = 3;
    [Tooltip("Каждая N-я волна заменяется боссом.")]
    [SerializeField] private int bossWaveInterval = 10;

    [Header("Boss Spawning")]
    [Tooltip("Пауза между выходом босса и его прислугой.")]
    [SerializeField] private float bossMinionDelay = 1.2f;
    [Tooltip("Сколько прислуги выходит вместе с боссом.")]
    [SerializeField] private int bossMinionCount = 5;
    [Tooltip("Длительность «материализации» босса (окно неуязвимости и интро).")]
    [SerializeField] private float bossSpawnInDuration = 1.2f;

    [Header("Timing")]
    [SerializeField] private float waveDisplayTime = 1.5f;
    [SerializeField] private float prepareTime = 1.5f;
    [SerializeField] private float countdownStepTime = 1f;
    [Tooltip("Пауза между «WAVE COMPLETE» и окном выбора улучшения.")]
    [SerializeField] private float waveCompleteDisplayTime = 1.2f;
    [Tooltip("Пауза после выбора улучшения перед началом следующей волны.")]
    [SerializeField] private float postUpgradeDelay = 0.8f;

    [Header("Structure Sync")]
    [Tooltip("Пауза после завершения генерации структур перед спавном врагов. Даёт структурам секунду-другую до конца вырасти.")]
    [SerializeField] private float structureSpawnBuffer = 1f;

    public int CurrentWave { get; private set; }

    private bool waveActive;
    private bool waitingForNextWave;
    private bool gameStarted;
    private bool waveCompleteShown;

    private void Start()
    {
        if (waveUI != null)
            waveUI.Hide();

        if (upgradeUI == null)
            upgradeUI = FindAnyObjectByType<UpgradeUI>();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;

            if (GameStateManager.Instance.CurrentState == GameState.Playing && !gameStarted)
            {
                gameStarted = true;
                CurrentWave = 0;
                StartCoroutine(StartWaveSequence());
            }
        }
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing && !gameStarted)
        {
            gameStarted = true;
            CurrentWave = 0;

            if (ScoreManager.Instance != null)
                ScoreManager.Instance.ResetRunStats();

            StartCoroutine(StartWaveSequence());
        }
        else if (state == GameState.Menu)
        {
            gameStarted = false;
            waveActive = false;
            waitingForNextWave = false;
            waveCompleteShown = false;
            StopAllCoroutines();

            if (enemySpawner != null)
                enemySpawner.StopSpawnQueue();

            if (waveUI != null)
                waveUI.Hide();
        }
    }

    private void Update()
    {
        if (!waveActive || waitingForNextWave)
            return;

        if (GameStateManager.Instance != null
            && GameStateManager.Instance.CurrentState != GameState.Playing)
            return;

        // Волна считается пройденной только когда исчерпана очередь спавна
        // И все заспавненные враги мертвы. При растянутом спавне нельзя
        // полагаться только на счётчик живых.
        bool spawnerBusy =
            enemySpawner != null &&
            enemySpawner.IsSpawning;

        if (spawnerBusy || Enemy.AliveCount > 0)
            return;

        {
            waitingForNextWave = true;
            waveActive = false;

            if (waveCompleteShown)
                return;

            waveCompleteShown = true;

            if (waveUI != null)
            {
                waveUI.ShowWaveComplete();

                StartCoroutine(ShowUpgradeAfterWaveComplete());
            }
            else if (upgradeUI != null)
            {
                upgradeUI.Show();
            }
            else
            {
                Debug.LogWarning(
                    "WaveManager: UpgradeUI is not assigned."
                );

                ContinueAfterUpgrade();
            }
        }
    }

    private IEnumerator ShowUpgradeAfterWaveComplete()
    {
        yield return new WaitForSeconds(waveCompleteDisplayTime);

        if (upgradeUI != null)
        {
            upgradeUI.Show();
        }
        else
        {
            Debug.LogWarning(
                "WaveManager: UpgradeUI is not assigned."
            );

            ContinueAfterUpgrade();
        }
    }

    private IEnumerator StartWaveSequence()
    {
        CurrentWave++;
        waveActive = false;
        waitingForNextWave = false;
        waveCompleteShown = false;

        SwitchToMainMusic();

        if (worldGenerator != null)
            worldGenerator.GenerateForWave(CurrentWave);

        if (waveUI != null)
            waveUI.ShowWave(CurrentWave);

        yield return new WaitForSeconds(waveDisplayTime);

        if (waveUI != null)
            waveUI.ShowPrepare();

        yield return new WaitForSeconds(prepareTime);

        if (waveUI != null)
            waveUI.ShowCountdown(3);

        PlayCountdownSound();

        yield return new WaitForSeconds(countdownStepTime);

        if (waveUI != null)
            waveUI.ShowCountdown(2);

        PlayCountdownSound();

        yield return new WaitForSeconds(countdownStepTime);

        if (waveUI != null)
            waveUI.ShowCountdown(1);

        PlayCountdownSound();

        yield return new WaitForSeconds(countdownStepTime);

        if (waveUI != null)
            waveUI.Hide();

        yield return new WaitForSeconds(
            structureSpawnBuffer
        );

        if (worldGenerator != null)
        {
            while (worldGenerator.IsGenerating)
            {
                yield return null;
            }
        }

        SpawnCurrentWave();

        waveActive = true;
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        yield return new WaitForSeconds(postUpgradeDelay);

        StartCoroutine(StartWaveSequence());
    }

    private void SpawnCurrentWave()
    {
        int enemyCount =
            startingEnemies +
            (CurrentWave - 1) *
            enemiesAddedPerWave;

        Debug.Log(
            $"WAVE {CurrentWave} START"
        );

        PlayWaveStartSound();

        if (enemySpawner == null)
            return;

        enemySpawner.CurrentWave = CurrentWave;

        // Босс-волна начинает «материализацию» сразу после каунтдауна.
        if (CurrentWave % bossWaveInterval == 0)
        {
            SpawnBossWave();
            return;
        }

        // Обычная волна «вытекает» приёмами — эмиттер сам сообщит
        // через IsSpawning, когда очередь спавна исчерпана.
        enemySpawner.SpawnWave(
            enemyCount,
            CurrentWave
        );
    }

    private void SpawnBossWave()
    {
        Debug.Log(
            $"========== BOSS WAVE {CurrentWave} =========="
        );

        PlayBossSpawnSound();

        SwitchToBossMusic();

        if (enemySpawner == null)
            return;

        // Волна активна, пока босс материализуется и подтягивается прислуга.
        enemySpawner.SetManualSpawning(true);
        StartCoroutine(BossWaveRoutine());
    }

    private IEnumerator BossWaveRoutine()
    {
        Vector3 bossPosition =
            enemySpawner.GetArenaEdgeSpawnPosition();

        enemySpawner.SpawnEnemyAtPosition(
            EnemyType.Boss,
            bossPosition,
            bossSpawnInDuration
        );

        // Пауза: игрок видит «ритуал» появления босса, потом выходит прислуга.
        yield return new WaitForSeconds(bossMinionDelay);

        for (int i = 0; i < bossMinionCount; i++)
        {
            enemySpawner.SpawnEnemyAtPosition(
                EnemyType.Normal,
                GetMinionPositionAround(bossPosition)
            );
        }

        enemySpawner.SetManualSpawning(false);
    }

    private Vector3 GetMinionPositionAround(
        Vector3 center)
    {
        Vector2 randomOffset =
            Random.insideUnitCircle * 2.5f;

        return
            center +
            new Vector3(
                randomOffset.x,
                0f,
                randomOffset.y
            );
    }

    public void ContinueAfterUpgrade()
    {
        if (!waitingForNextWave)
            return;

        Time.timeScale = 1f;

        if (upgradeUI != null)
            upgradeUI.Hide();

        waitingForNextWave = false;
        waveCompleteShown = false;

        StartCoroutine(StartNextWaveAfterDelay());
    }

    // =========================================================
    // SFX / MUSIC
    // =========================================================

    private void PlayCountdownSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.CountdownTick);
    }

    private void PlayWaveStartSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.WaveStart);
    }

    private void PlayBossSpawnSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.BossSpawn);
    }

    private void SwitchToBossMusic()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToBossMusic();
    }

    private void SwitchToMainMusic()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToMainMusic();
    }
}
