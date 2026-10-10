using UnityEngine;
using System.Collections;
using System.Collections.Generic;

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
    [Tooltip("Общий множитель количества противников во всех волнах. Умножается последним, поверх сжатия, высоты слота и надбавки, поэтому кривая остаётся той же — растёт только количество. 1 — без изменений, 2 — вдвое больше.")]
    [SerializeField] private float enemyCountMultiplier = 2f;
    [SerializeField] private int startingEnemies = 5;
    [SerializeField] private int enemiesAddedPerWave = 3;
    [Tooltip("Каждая N-я волна заменяется боссом.")]
    [SerializeField] private int bossWaveInterval = 10;

    [Header("Intensity Curve")]
    [Tooltip("Мягкий потолок размера волны. Сырой рост «+N за волну» сжимается к этому потолку: короткие волны почти не меняются, длинные упираются в него и растут всё медленнее. Без этого волна 30 — это девяносто мобов разом.")]
    [SerializeField] private float enemyCountCeiling = 85f;
    [Tooltip("Жёсткий потолок: больше этого врагов в одну волну не выпускается никогда. Страховка от бесконечной волны, если потолок сжатия выставлен неверно.")]
    [SerializeField] private int maxEnemiesPerWave = 110;
    [Tooltip("Дополнительный линейный множитель размера с номером волны. Нужен потому, что здоровье врагов растёт на фиксированный процент за волну и никогда не останавливается, а количество упирается в потолок сжатия. Без этого множителя после ~20 волны сложность держалась бы только на HP, и волны переставали бы отличаться друг от друга.")]
    [SerializeField] private float countRampPerWave = 0.008f;

    [Header("Wave Modifiers")]
    [Tooltip("С какой волны включаются модификаторы. Раньше игрок ещё не знает базовые типы врагов — акценты только мешают учиться.")]
    [SerializeField] private int minModifierWave = 5;
    [Tooltip("Шанс, что «спокойный» слот цикла получит модификатор. Ниже 0.5 — чтобы больше половины волн оставались обычными.")]
    [Range(0f, 1f)]
    [SerializeField] private float extraModifierChance = 0.35f;
    [Tooltip("Сколько последних акцентов помнить, чтобы не ставить тот же дважды подряд и не повторять слишком часто. 2-4 — компромисс: узнаваемый шаблон ломается, но игрок всё ещё ждёт акцент на «тяжёлых» слотах.")]
    [Range(1, 6)]
    [SerializeField] private int modifierMemoryLength = 3;
    [Tooltip("Минимальный зазор (в волнах) между тяжёлыми акцентами — «охота на элиту», «мины» и «последний рубеж». Не даёт двум самым дорогим ситуациям идти одна за другой.")]
    [Range(1, 5)]
    [SerializeField] private int heavyModifierCooldown = 2;

    [Header("Boss Spawning")]
    [Tooltip("Пауза между выходом босса и его прислугой.")]
    [SerializeField] private float bossMinionDelay = 1.2f;
    [Tooltip("Сколько прислуги выходит вместе с боссом.")]
    [SerializeField] private int bossMinionCount = 5;
    [Tooltip("Длительность «материализации» босса (окно неуязвимости и интро).")]
    [SerializeField] private float bossSpawnInDuration = 1.2f;

    [Header("Timing")]
    [Tooltip("Баннер «WAVE N» обычной волны. Короткий: за ним сразу идёт бой, без «подготовки» и отсчёта.")]
    [SerializeField] private float waveDisplayTime = 0.75f;
    [Tooltip("Заставка первой волны — вступление в забег, поэтому чуть дольше обычного.")]
    [SerializeField] private float firstWaveDisplayTime = 1f;
    [Tooltip("Заставка перед боссом. Должна ощущаться как отдельный момент.")]
    [SerializeField] private float bossWaveDisplayTime = 1.6f;
    [Tooltip("Экран «ПОДГОТОВКА». Показывается только на первой волне и перед боссом.")]
    [SerializeField] private float prepareTime = 0.5f;
    [Tooltip("Длительность одной цифры отсчёта. Тоже только первая волна и босс.")]
    [SerializeField] private float countdownStepTime = 0.6f;
    [Tooltip("Пауза между «WAVE COMPLETE» и окном выбора улучшения.")]
    [SerializeField] private float waveCompleteDisplayTime = 0.6f;
    [Tooltip("Та же пауза после победы над боссом — кульминация забега, её можно подержать дольше.")]
    [SerializeField] private float bossWaveCompleteDisplayTime = 1.4f;
    [Tooltip("Пауза после выбора улучшения перед началом следующей волны.")]
    [SerializeField] private float postUpgradeDelay = 0.35f;
    [Tooltip("Сколько ждать перестройки арены между волнами, прежде чем начать спавн врагов. Время уходит на исчезновение старых блоков и вырастание новых — без него игрок увидел бы пустую площадку.")]
    [SerializeField] private float structureTransitionWait = 1.2f;
    [Tooltip("Потолок ожидания перестройки. Страховка: если генерация зависнет, волна всё равно начнётся.")]
    [SerializeField] private float structureTransitionTimeout = 4f;

    public int CurrentWave { get; private set; }

    /// <summary>
    /// Сколько врагов ещё предстоит увидеть: живые на поле плюс те,
    /// кого спавнер ещё выпустит (включая отложенную элиту и
    /// финальную пачку «последнего рубежа»). Нужен игроку, чтобы
    /// понимать, когда волна действительно закончится, а не когда
    /// на экране временно опустело.
    /// </summary>
    public int EnemiesLeft
    {
        get
        {
            int pending =
                enemySpawner != null
                    ? enemySpawner.PendingSpawnCount
                    : 0;

            return pending + Enemy.AliveCount;
        }
    }

    // Последние выданные акценты. Нужны, чтобы не повторять один и
    // тот же акцент и не ставить тяжёлые ситуации впритык.
    private readonly List<WaveModifier> recentModifiers =
        new List<WaveModifier>();

    // Номер волны, на которой последний раз выдался тяжёлый акцент.
    // Память акцентов хранит только сами акценты, а для передышки
    // между тяжёлыми нужен ещё и порядковый номер волны.
    private int lastHeavyModifierWave = int.MinValue;

    private WaveArchetype currentArchetype =
        WaveArchetype.Standard;

    private WaveModifier currentModifier =
        WaveModifier.None;

    private bool waveActive;
    private bool waitingForNextWave;
    private bool gameStarted;
    private bool waveCompleteShown;

    // Для какой волны арена уже собрана. Нужна, чтобы не перестраивать
    // мир повторно на первой волне после анимированной подготовки
    // обучения (та же раскладка, только уже выросшая).
    private int generatedWorldWave = -1;

    private EnvironmentController cachedEnvironment;

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

        cachedEnvironment =
            FindAnyObjectByType<EnvironmentController>();

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
            currentModifier = WaveModifier.None;

            // Память акцентов обнуляется вместе с забегом: иначе
            // второй забег начинался бы с запретов, оставшихся от
            // первого, и первые волны шли без акцентов.
            recentModifiers.Clear();
            lastHeavyModifierWave = int.MinValue;

            StopAllCoroutines();

            if (eventDirector != null)
                eventDirector.ResetRun();

            if (RunModifierManager.Instance != null)
                RunModifierManager.Instance.ResetRun();

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

            // ============================================
            // LEADERBOARD METRICS
            // ============================================
            RunMetrics metrics =
                FindAnyObjectByType<RunMetrics>();
            if (metrics != null)
            {
                PlayerHealth ph = FindAnyObjectByType<PlayerHealth>();
                bool wasPerfect = ph != null && ph.CurrentHealth >= ph.MaxHealth;
                metrics.OnWaveCleared(wasPerfect);
            }

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
        yield return new WaitForSeconds(
            IsBossWave(CurrentWave)
                ? bossWaveCompleteDisplayTime
                : waveCompleteDisplayTime
        );

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
        {
            ScoreManager.Instance.ResetRunStats();
        }

        // Тема забега разыгрывается на его старте, до первой волны и
        // до спавна врагов, чтобы все успели прочитать множители.
        RunModifierManager.EnsureExists().RollForRun();
            
        // LEADERBOARD: сброс и старт метрик
        RunMetrics metrics = FindAnyObjectByType<RunMetrics>();
        if (metrics != null)
        {
            metrics.ResetAll();
            metrics.StartRun();
        }

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
        if (ScoreManager.Instance != null){
            ScoreManager.Instance.ResetRunStats();
        }

        // LEADERBOARD: старт метрик после обучения
        RunMetrics metrics = FindAnyObjectByType<RunMetrics>();
        if (metrics != null)
        {
            metrics.StartRun();
        }

        StartCoroutine(StartWaveSequence());
    }

    /// <summary>
    /// Строит арену для первой волны заранее — во время обучения,
    /// чтобы игрок сражался не на пустом поле. Geометрия привязана
    /// к текущей карте и номеру первой волны, поэтому пересборка
    /// на старте забега даёт ту же раскладку.
    /// </summary>
    public void PrepareTutorialWorld()
    {
        if (worldGenerator == null)
            return;

        int mapSeed = GetCurrentMapSeed();

        // Здесь анимация нужна: игрок ещё в обучении, блоки
        // появляются цепочкой и показывают, что арена живая.
        // Seed совпадает с формулой RebuildForWave, поэтому на
        // первой волне раскладка не перестроится.
        worldGenerator.GenerateAnimated(mapSeed, 1);
        generatedWorldWave = 1;
    }

    // Seed геометрии арены берётся из текущей карты: у каждого
    // биома своя конфигурация блоков. Номер волны добавляется
    // поверх, поэтому внутри карты раскладка меняется.
    private int GetCurrentMapSeed()
    {
        if (cachedEnvironment == null)
            cachedEnvironment =
                FindAnyObjectByType<EnvironmentController>();

        if (cachedEnvironment != null &&
            cachedEnvironment.CurrentMap != null)
        {
            return cachedEnvironment.CurrentMap.layoutSeed;
        }

        return worldGenerator != null
            ? worldGenerator.BaseSeed
            : -1;
    }

    private IEnumerator StartWaveSequence()
    {
        CurrentWave++;
        waveActive = false;
        waitingForNextWave = false;
        waveCompleteShown = false;

        currentArchetype =
            GetArchetypeForWave(CurrentWave);

        // Босс-волна играет по своим правилам (прислуга, способности),
        // поэтому акцент слота на ней не назначается: подпись вроде
        // «НАЛЁТ» над боссом обещала бы волну быстрых врагов, которой
        // на босс-волне нет.
        currentModifier =
            IsBossWave(CurrentWave)
                ? WaveModifier.None
                : GetModifierForWave(CurrentWave);

        // Акцент попадает в память и на босс-волне — как «пустой»:
        // иначе после босса слот сразу выдаст тот же акцент, который
        // шёл до него, и у босса появится дубль.
        if (IsBossWave(CurrentWave))
            recentModifiers.Add(WaveModifier.None);
        else
            RememberModifier(currentModifier, CurrentWave);

        SwitchToMainMusic();

        // Арена перестраивается каждую волну, но мгновенно: новая
        // раскладка блоков готова до спавна врагов, поэтому лишней
        // паузы между волнами не появляется. На первой волне мир
        // уже собран обучением — перестраивать в ту же раскладку
        // незачем.
        if (worldGenerator != null &&
            generatedWorldWave != CurrentWave)
        {
            worldGenerator.RebuildForWave(
                GetCurrentMapSeed(),
                CurrentWave
            );

            generatedWorldWave = CurrentWave;
        }

        // Отсчёт 3-2-1 и «подготовка» нужны только там, где игрок
        // ждёт начала забега или готовится к боссу. Обычная волна
        // получает короткий баннер и стартует сразу.
        bool isFirstWave = CurrentWave <= 1;
        bool isBossWave = IsBossWave(CurrentWave);

        // Заставка первой волны заодно объявляет тему забега: игрок
        // должен понимать, чем этот забег отличается от прошлого, до
        // первого выстрела, а не догадываться по поведению врагов.
        string subtitle =
            GetWaveSubtitle(
                currentArchetype,
                currentModifier
            );

        if (isFirstWave &&
            RunModifierManager.Instance != null &&
            RunModifierManager.Instance.HasAny)
        {
            subtitle = RunModifierManager.Instance.ShortName;
        }

        if (waveUI != null)
            waveUI.ShowWave(CurrentWave, subtitle);

        if (!isFirstWave && !isBossWave)
        {
            yield return new WaitForSeconds(
                waveDisplayTime
            );

            if (waveUI != null)
                waveUI.Hide();

            yield return WaitForWorldReady();

            SpawnCurrentWave();

            waveActive = true;

            yield break;
        }

        yield return new WaitForSeconds(
            isBossWave
                ? bossWaveDisplayTime
                : firstWaveDisplayTime
        );

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

        yield return WaitForWorldReady();

        SpawnCurrentWave();

        waveActive = true;
    }

    // Ждать нужно только анимированную генерацию (смена карты,
    // ждём и сам переход: пока блоки исчезают и вырастают, на поле
    // нет ни старой, ни новой раскладки, и враги спавнились бы в
    // пустое место. Лимит нужен, чтобы зависшая генерация не
    // заблокировала начало волны навсегда.
    private IEnumerator WaitForWorldReady()
    {
        if (worldGenerator == null)
            yield break;

        float timeout = structureTransitionTimeout;

        while (worldGenerator.IsGenerating && timeout > 0f)
        {
            timeout -= Time.unscaledDeltaTime;

            yield return null;
        }

        // Минимальная пауза нужна даже когда перестройка не запускалась
        // (первая волна после обучения): баннер волны и доска улучшений
        // должны успеть уйти, прежде чем на поле придут враги.
        yield return new WaitForSeconds(
            Mathf.Max(structureTransitionWait, 0f)
        );
    }

    private bool IsBossWave(int wave)
    {
        return bossWaveInterval > 0 &&
               wave % bossWaveInterval == 0;
    }

    private IEnumerator StartNextWaveAfterDelay()
    {
        yield return new WaitForSeconds(postUpgradeDelay);

        StartCoroutine(StartWaveSequence());
    }

    private void SpawnCurrentWave()
    {
        int enemyCount =
            GetEnemyCountForWave(CurrentWave);

        Debug.Log(
            $"WAVE {CurrentWave} START " +
            $"({currentArchetype}" +
            $"{DescribeModifier(currentModifier)}" +
            $", enemies: {enemyCount})"
        );

        PlayWaveStartSound();

        if (enemySpawner == null)
            return;

        enemySpawner.CurrentWave = CurrentWave;

        // Босс-волна начинает «материализацию» сразу после каунтдауна.
        // События посреди босс-волны не запускаем — у босса и так
        // есть прислуга и способности.
        if (IsBossWave(CurrentWave))
        {
            SpawnBossWave();

            if (eventDirector != null)
                eventDirector.OnWaveStarted(
                    CurrentWave,
                    false,
                    currentModifier
                );

            return;
        }

        // Обычная волна «вытекает» приёмами — эмиттер сам сообщит
        // через IsSpawning, когда очередь спавна исчерпана.
        enemySpawner.SpawnWave(
            enemyCount,
            CurrentWave,
            currentArchetype,
            currentModifier
        );

        if (eventDirector != null)
            eventDirector.OnWaveStarted(
                CurrentWave,
                AllowsMidWaveEvent(currentModifier),
                currentModifier
            );
    }

    // Акцент волны больше не запрещает событие целиком. Раньше волны
    // с акцентом просто не получали событий, а волны без акцента
    // получали их всегда — и «опасные зоны» выпадали из забега
    // целиком, хотя ничто не мешало им сочетаться с обычным
    // событием. Теперь несовместимость решается по типу события
    // внутри WaveEventDirector, а не запретом волны.
    private bool AllowsMidWaveEvent(
        WaveModifier modifier)
    {
        return true;
    }

    private string DescribeModifier(
        WaveModifier modifier)
    {
        return modifier == WaveModifier.None
            ? string.Empty
            : $" + {modifier}";
    }

    // Размер волны. Сырой рост «+N за волну» задаёт длину забега,
    // но сам по себе превращает волну 30 в девяносто мобов и волну
    // 50 в полторы сотни. Поэтому линейный рост сжимается к
    // потолку: короткие волны почти не меняются, длинные упираются
    // в потолок и растут всё медленнее. Поверх сжатия идёт высота
    // слота — так появляются спады и пики, а не ровный поток.
    private int GetEnemyCountForWave(int wave)
    {
        // Множитель поднимает и потолок сжатия, и сам линейный рост.
        // Если поднять только потолок, волны с малым raw почти не
        // изменятся, а после потолка количество упрётся в него вдвое
        // раньше. Равномерное масштабирование обоих даёт ровно ×N
        // на каждой волне, не ломая форму кривой.
        float multiplier =
            Mathf.Max(enemyCountMultiplier, 0f);

        float ceiling =
            Mathf.Max(enemyCountCeiling, 1f) * multiplier;

        float raw =
            (startingEnemies +
            (wave - 1) * enemiesAddedPerWave) * multiplier;

        float compressed =
            ceiling *
            (1f - Mathf.Exp(-raw / ceiling));

        // Линейная надбавка поверх сжатия. Нужна потому, что сжатие
        // почти останавливает рост к потолку, а здоровье врагов
        // продолжает расти на фиксированный процент каждую волну.
        // Без этой надбавки после потолка волны отличались бы только
        // множителем HP, а не размером.
        float ramp =
            1f +
            Mathf.Max(countRampPerWave, 0f) *
            (wave - 1);

        float scaled =
            compressed *
            GetWaveIntensity(wave) *
            WaveDifficulty.GetCountMultiplier(wave) *
            ramp;

        return Mathf.Clamp(
            Mathf.RoundToInt(scaled),
            1,
            Mathf.Max(Mathf.RoundToInt(maxEnemiesPerWave * multiplier), 1)
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
            enemySpawner.GetPlayerRingSpawnPosition();

        enemySpawner.SpawnEnemyAtPosition(
            EnemyType.Boss,
            bossPosition,
            bossSpawnInDuration
        );

        // Пауза: игрок видит «ритуал» появления босса, потом выходит прислуга.
        yield return new WaitForSeconds(bossMinionDelay);

        int minionCount =
            Mathf.RoundToInt(
                bossMinionCount *
                Mathf.Max(enemyCountMultiplier, 0f)
            );

        for (int i = 0; i < minionCount; i++)
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

    // Волны чередуются по фиксированному циклу из восьми слотов,
    // чтобы у каждой был свой характер: рой → осада → вылазка →
    // снова. Слот определяет и архетип, и высоту волны, и её акцент,
    // поэтому подъёмы и спады совпадают по смыслу, а не случайно.
    private WaveArchetype GetArchetypeForWave(
        int wave)
    {
        switch (GetWaveSlot(wave))
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

// Высота волны внутри восьмиволнового цикла. Смысл слотов:
//   0 — отдых после босса
//   1 — рой с напором
//   2 — передышка, первая дальняя угроза
//   3 — осада
//   4 — отдых
//   5 — рой с другой стороны
//   6 — вылазка за элитой
//   7 — пик цикла
//
// Раньше разброс здесь был основным источником «горки» сложности
// (≈ -16% … +12%). Теперь главный ритм задаёт уровень волны
// (лёгкая / сложная, см. WaveDifficulty), поэтому таблица сжата
// почти до единицы и работает как фактура поверх него: слот по-прежнему
// чуть выше или ниже соседей, но уже не перебивает чередование, иначе
// «лёгкая» волна в слоте пика оказывалась бы тяжелее «сложной» в слоте
// отдыха — а это прямое противоречие тому, что игрок видит на экране.
private static readonly float[] SlotIntensity =
    {
        0.97f,
        1.00f,
        0.98f,
        1.02f,
        0.96f,
        1.00f,
        0.99f,
        1.06f
    };

    // Модификатор — не украшение каждой волны, а акцент. Слоты с
    // характером (1, 3, 5, 6, 7) несут свой акцент всегда, а слоты-
    // передышки (0, 2, 4) могут получить случайный и только с шансом
    // меньше половины. Итог: около трети волн остаются полностью
    // обычными, иначе «особенность» перестаёт быть особенной.
    private WaveModifier GetModifierForWave(
        int wave)
    {
        if (wave < minModifierWave)
            return WaveModifier.None;

        switch (GetWaveSlot(wave))
        {
            case 1:
                return PickSlotModifier(
                    FastSlotPool,
                    wave);

            case 3:
                return PickSlotModifier(
                    RangedSlotPool,
                    wave);

            case 5:
                return PickSlotModifier(
                    AmbushSlotPool,
                    wave);

            case 6:
                return PickSlotModifier(
                    EliteSlotPool,
                    wave);

            case 7:
                return PickSlotModifier(
                    PeakSlotPool,
                    wave);
        }

        // Акцент на отдыхе — редкость, а не правило.
        if (wave >= minModifierWave + 2 &&
            Random.value < extraModifierChance)
        {
            return PickSlotModifier(
                CalmSlotPool,
                wave);
        }

        return WaveModifier.None;
    }

    // Слот «навала». «Заход с флангов» здесь почти всегда, но не
    // всегда: иначе вторая волна каждого цикла была бы гарантированным
    // заходом, и цикл читался бы наизусть.
    private static readonly WaveModifier[] FastSlotPool =
    {
        WaveModifier.FastAssault,
        WaveModifier.FastAssault,
        WaveModifier.Ambush
    };

    // Кандидаты для «спокойных» слотов: только лёгкие акценты.
    // Тяжёлые ситуации (охота, мины, последний рубеж) на отдыхе
    // не ставятся — иначе тихая волна оказывалась бы тяжелее
    // предыдущей громкой, и ритм пульса ломался бы.
    private static readonly WaveModifier[] CalmSlotPool =
    {
        WaveModifier.FastAssault,
        WaveModifier.RangedAssault
    };

    // Слот с дальниками по умолчанию, но «мины» и «заход с
    // флангов» здесь равновероятны: иначе игрок выучивает
    // «четвёртая волна — всегда обстрел».
    private static readonly WaveModifier[] RangedSlotPool =
    {
        WaveModifier.RangedAssault,
        WaveModifier.DangerZone,
        WaveModifier.Ambush
    };

    // Слот захода с флангов по умолчанию, но с равными шансами
    // «мины» и «навал».
    private static readonly WaveModifier[] AmbushSlotPool =
    {
        WaveModifier.Ambush,
        WaveModifier.DangerZone,
        WaveModifier.FastAssault
    };

    // Слот охоты на элиту почти всегда остаётся охотой: это
    // единственный слот, где она уместна, и без элиты слот
    // просто дублирует соседние. Охота требует волну 7+,
    // где элита и так появляется в общей смеси.
    private static readonly WaveModifier[] EliteSlotPool =
    {
        WaveModifier.EliteHunt,
        WaveModifier.EliteHunt,
        WaveModifier.RangedAssault
    };

    // Пик цикла: самый широкий набор. Здесь и «последний рубеж»,
    // и всё остальное, что обычно придерживается для отдыха.
    private static readonly WaveModifier[] PeakSlotPool =
    {
        WaveModifier.LastStand,
        WaveModifier.DangerZone,
        WaveModifier.Ambush,
        WaveModifier.EliteHunt,
        WaveModifier.FastAssault
    };

    // Выбор акцента из слота с двумя ограничениями: не повторять
    // слишком недавно и не ставить тяжёлый акцент впритык к
    // предыдущему тяжёлому. Оба условия убирают предсказуемость,
    // но выбор остаётся внутри фиксированного пула слота —
    // структура «лёгкая → тяжёлая» восьмиволнового цикла
    // сохраняется.
    private WaveModifier PickSlotModifier(
        WaveModifier[] pool,
        int wave)
    {
        int memory =
            Mathf.Clamp(modifierMemoryLength, 1, 6);

        // Тяжёлые акценты: дорогие по вниманию и по урону.
        bool previousWasHeavy =
            recentModifiers.Count > 0 &&
            IsHeavyModifier(recentModifiers[recentModifiers.Count - 1]);

        int allowed = pool.Length;
        WaveModifier fallback = WaveModifier.None;

        // Считаем веса: подходящие кандидаты получают вес 1,
        // повторённые — 0. Дальше взвешенный выбор, поэтому
        // дубли в пуле (EliteSlotPool) работают как вес.
        float totalWeight = 0f;
        int[] weights = new int[pool.Length];

        for (int i = 0; i < pool.Length; i++)
        {
            WaveModifier candidate = pool[i];

            weights[i] = 1;

            if (WasUsedRecently(candidate, memory))
            {
                weights[i] = 0;
                continue;
            }

            // Два тяжёлых акцента подряд — это не «разнообразие»,
            // а две тяжёлые волны в ряд без передышки.
            if (previousWasHeavy &&
                IsHeavyModifier(candidate) &&
                recentModifiers.Count > 0)
            {
                int wavesSinceHeavy =
                    wave - lastHeavyModifierWave;

                if (wavesSinceHeavy <
                    Mathf.Max(heavyModifierCooldown, 1))
                {
                    weights[i] = 0;
                    continue;
                }
            }

            totalWeight += weights[i];

            if (fallback == WaveModifier.None)
                fallback = candidate;
        }

        // Все кандидаты в памяти: не превращаем волну в обычную,
        // просто берём самый далёкий из использованных.
        if (totalWeight <= 0f)
            return PickLeastRecent(pool, memory);

        float roll = Random.Range(0f, totalWeight);

        for (int i = 0; i < pool.Length; i++)
        {
            if (weights[i] <= 0)
                continue;

            roll -= weights[i];

            if (roll < 0f)
                return pool[i];
        }

        return fallback;
    }

    private bool WasUsedRecently(
        WaveModifier modifier,
        int memory)
    {
        int count = recentModifiers.Count;

        for (int i = 0; i < count && i < memory; i++)
        {
            if (recentModifiers[count - 1 - i] == modifier)
                return true;
        }

        return false;
    }

    // Самый далёкий по времени: волна всё равно получает акцент,
    // просто не тот, что только что был.
    private WaveModifier PickLeastRecent(
        WaveModifier[] pool,
        int memory)
    {
        WaveModifier best = pool[0];
        int bestDistance = -1;

        for (int i = 0; i < pool.Length; i++)
        {
            int distance = 0;

            for (int j = 0; j < recentModifiers.Count && j < memory; j++)
            {
                if (recentModifiers[recentModifiers.Count - 1 - j] ==
                    pool[i])
                {
                    distance++;
                }
            }

            if (distance > bestDistance)
            {
                bestDistance = distance;
                best = pool[i];
            }
        }

        return best;
    }

    private static bool IsHeavyModifier(
        WaveModifier modifier)
    {
        return
            modifier.Has(WaveModifier.EliteHunt) ||
            modifier.Has(WaveModifier.DangerZone) ||
            modifier.Has(WaveModifier.LastStand);
    }

    private void RememberModifier(
        WaveModifier modifier,
        int wave)
    {
        if (modifier == WaveModifier.None)
            return;

        recentModifiers.Add(modifier);

        if (IsHeavyModifier(modifier))
            lastHeavyModifierWave = wave;

        int limit =
            Mathf.Max(modifierMemoryLength, 1) + 2;

        while (recentModifiers.Count > limit)
            recentModifiers.RemoveAt(0);
    }

    // Номер слота восьмиволнового цикла. Остаток берётся
    // неотрицательным, чтобы смена забега (CurrentWave = 0)
    // не уводила индекс в минус.
    private static int GetWaveSlot(int wave)
    {
        int slot = (wave - 1) % SlotIntensity.Length;

        return slot < 0
            ? slot + SlotIntensity.Length
            : slot;
    }

    private float GetWaveIntensity(int wave)
    {
        return SlotIntensity[GetWaveSlot(wave)];
    }

    // Подпись под номером волны: архетип важнее модификатора, но
    // у обычной волны архетип безымянный, и тогда подписью
    // становится акцент — игрок хотя бы видит, что волна не «просто
    // ещё одна».
    private string GetWaveSubtitle(
        WaveArchetype archetype,
        WaveModifier modifier)
    {
        // Уровень волны (лёгкая / сложная) намеренно не показывается:
        // это внутренний ритм, и подпись выдала бы его игроку. Разница
        // должна читаться по тому, что враги стали живучее или слабее, а
        // не по названию под колонтитулом.
        switch (archetype)
        {
            case WaveArchetype.Swarm:
                return Lang.Get("wave.archetype_swarm");

            case WaveArchetype.Siege:
                return Lang.Get("wave.archetype_siege");

            case WaveArchetype.Hunt:
                return Lang.Get("wave.archetype_hunt");
        }

        return GetModifierSubtitle(modifier);
    }

    private string GetModifierSubtitle(
        WaveModifier modifier)
    {
        if (modifier.Has(WaveModifier.FastAssault))
            return Lang.Get("wave.mod_fast_assault");

        if (modifier.Has(WaveModifier.RangedAssault))
            return Lang.Get("wave.mod_ranged_assault");

        if (modifier.Has(WaveModifier.EliteHunt))
            return Lang.Get("wave.mod_elite_hunt");

        if (modifier.Has(WaveModifier.Ambush))
            return Lang.Get("wave.mod_ambush");

        if (modifier.Has(WaveModifier.DangerZone))
            return Lang.Get("wave.mod_danger_zone");

        if (modifier.Has(WaveModifier.LastStand))
            return Lang.Get("wave.mod_last_stand");

        return null;
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
            AudioManager.Instance.PlaySFX(
                sfx.CountdownTick,
                priority: SfxPriority.High
            );
    }

    private void PlayWaveStartSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.WaveStart,
                priority: SfxPriority.High
            );
    }

    private void PlayBossSpawnSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.BossSpawn,
                priority: SfxPriority.Critical
            );
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
