using UnityEngine;
using System.Collections;

public class WaveManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private WaveUI waveUI;
    [SerializeField] private UpgradeUI upgradeUI;
    [SerializeField] private WorldStructureGenerator worldGenerator;
    [Tooltip("События посреди волны. Если не назначен — создаётся на этом объекте.")]
    [SerializeField] private WaveEventDirector eventDirector;

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

    private WaveArchetype currentArchetype =
        WaveArchetype.Standard;

    private bool waveActive;
    private bool waitingForNextWave;
    private bool gameStarted;
    private bool waveCompleteShown;

    // Для какой волны мир уже сгенерирован — чтобы не перестраивать
    // арену заново, если она была подготовлена во время обучения.
    private int generatedWorldWave = -1;

    private void Start()
    {
        if (waveUI != null)
            waveUI.Hide();

        if (upgradeUI == null)
            upgradeUI = FindAnyObjectByType<UpgradeUI>();

        if (eventDirector == null)
            eventDirector =
                gameObject.AddComponent<WaveEventDirector>();

        eventDirector.Initialize(enemySpawner, waveUI);

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;

            if (GameStateManager.Instance.CurrentState == GameState.Playing && !gameStarted)
            {
                gameStarted = true;
                BeginRun();
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

            BeginRun();
        }
        else if (state == GameState.Menu)
        {
            gameStarted = false;
            waveActive = false;
            waitingForNextWave = false;
            waveCompleteShown = false;
            generatedWorldWave = -1;
            StopAllCoroutines();

            if (eventDirector != null)
                eventDirector.Stop();

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

            if (eventDirector != null)
                eventDirector.OnWaveEnded();

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

    // =========================================================
    // RUN / TUTORIAL ENTRY POINTS
    // =========================================================

    // Первый запуск забега: сброс статистики и старт первой волны —
    // либо сразу, либо через обучение, если оно ещё не пройдено.
    private void BeginRun()
    {
        CurrentWave = 0;

        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetRunStats();

        // Обучение перехватывает запуск: волна стартует сама,
        // когда игрок дошёл до конца туториала (StartFirstWave).
        if (TutorialManager.Instance != null &&
            TutorialManager.Instance.BeginTutorial())
        {
            return;
        }

        StartCoroutine(StartWaveSequence());
    }

    /// <summary>
    /// Запускает первую волну после завершения обучения.
    /// </summary>
    public void StartFirstWave()
    {
        if (CurrentWave != 0)
            return;

        // Статистика обнуляется заново: убийства во время
        // обучения не должны идти в счёт забега.
        if (ScoreManager.Instance != null)
            ScoreManager.Instance.ResetRunStats();

        StartCoroutine(StartWaveSequence());
    }

    /// <summary>
    /// Строит арену для первой волны заранее — во время обучения,
    /// чтобы игрок сражался не на пустом поле.
    /// </summary>
    public void PrepareTutorialWorld()
    {
        if (worldGenerator == null)
            return;

        worldGenerator.GenerateForWave(1);
        generatedWorldWave = 1;
    }

    private IEnumerator StartWaveSequence()
    {
        CurrentWave++;
        waveActive = false;
        waitingForNextWave = false;
        waveCompleteShown = false;

        currentArchetype =
            GetArchetypeForWave(CurrentWave);

        SwitchToMainMusic();

        // Мир может быть уже построен во время обучения —
        // тогда волну 1 не перестраиваем заново.
        if (worldGenerator != null &&
            generatedWorldWave != CurrentWave)
        {
            worldGenerator.GenerateForWave(CurrentWave);
            generatedWorldWave = CurrentWave;
        }

        if (waveUI != null)
            waveUI.ShowWave(
                CurrentWave,
                GetArchetypeSubtitle(currentArchetype)
            );

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
            $"WAVE {CurrentWave} START " +
            $"({currentArchetype})"
        );

        PlayWaveStartSound();

        if (enemySpawner == null)
            return;

        enemySpawner.CurrentWave = CurrentWave;

        // Босс-волна начинает «материализацию» сразу после каунтдауна.
        // События посреди босс-волны не запускаем — у босса и так
        // есть прислуга и способности.
        if (CurrentWave % bossWaveInterval == 0)
        {
            SpawnBossWave();

            if (eventDirector != null)
                eventDirector.OnWaveStarted(CurrentWave, false);

            return;
        }

        // Обычная волна «вытекает» приёмами — эмиттер сам сообщит
        // через IsSpawning, когда очередь спавна исчерпана.
        enemySpawner.SpawnWave(
            enemyCount,
            CurrentWave,
            currentArchetype
        );

        if (eventDirector != null)
            eventDirector.OnWaveStarted(CurrentWave, true);
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
    // WAVE ARCHETYPES
    // =========================================================

    // Волны чередуются по фиксированному циклу, чтобы у каждой
    // был свой характер: рой → осада → вылазка → снова.
    private WaveArchetype GetArchetypeForWave(
        int wave)
    {
        switch ((wave - 1) % 8)
        {
            case 1:
            case 5:
                return WaveArchetype.Swarm;

            case 3:
            case 7:
                return WaveArchetype.Siege;

            case 6:
                return WaveArchetype.Hunt;

            default:
                return WaveArchetype.Standard;
        }
    }

    private string GetArchetypeSubtitle(
        WaveArchetype archetype)
    {
        switch (archetype)
        {
            case WaveArchetype.Swarm:
                return "РОЙ";

            case WaveArchetype.Siege:
                return "ОСАДА";

            case WaveArchetype.Hunt:
                return "ВЫЛАЗКА";

            default:
                return null;
        }
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
