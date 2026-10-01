using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private WorldStructureGenerator worldGenerator;

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject normalPrefab;
    [SerializeField] private GameObject fastPrefab;
    [SerializeField] private GameObject tankPrefab;
    [SerializeField] private GameObject rangedPrefab;
    [SerializeField] private GameObject elitePrefab;
    [SerializeField] private GameObject bossPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float minimumSpawnDistance = 1.5f;
    [SerializeField] private int spawnAttempts = 24;

    [Header("Wave Emission")]
    [Tooltip("Сколько врагов выходит за один приём волны.")]
    [SerializeField] private int spawnBatchSize = 4;
    [Tooltip("Первый приём волны — «щуп», меньше остальных.")]
    [SerializeField] private int firstBatchSize = 3;
    [Tooltip("Пауза между приёмами волны.")]
    [SerializeField] private float spawnBatchInterval = 1.2f;
    [Tooltip("Пауза между врагами внутри одного приёма.")]
    [SerializeField] private float spawnStagger = 0.15f;

    [Header("Wave Archetype Pressure")]
    [Tooltip("Делитель роста батчей от номера волны. Чем меньше — тем быстрее нарастает давление.")]
    [SerializeField] private float batchSizeGrowthDivisor = 5f;
    [Tooltip("Затухание интервала между приёмами за волну (перемножается каждую волну).")]
    [SerializeField] private float spawnIntervalDecay = 0.95f;
    [Tooltip("Минимальный интервал между приёмами волны (до множителя архетипа).")]
    [SerializeField] private float minSpawnInterval = 0.30f;
    [Tooltip("Потолок размера приёма после масштабирования.")]
    [SerializeField] private int maxScaledBatchSize = 12;

    [Tooltip("Множитель размера приёма для «Роя».")]
    [SerializeField] private float swarmBatchSizeMultiplier = 1.5f;
    [Tooltip("Множитель интервала для «Роя» (меньше = чаще).")]
    [SerializeField] private float swarmIntervalMultiplier = 0.6f;

    [Tooltip("Множитель размера приёма для «Осады».")]
    [SerializeField] private float siegeBatchSizeMultiplier = 0.7f;
    [Tooltip("Множитель интервала для «Осады».")]
    [SerializeField] private float siegeIntervalMultiplier = 1.35f;

    [Tooltip("Множитель размера приёма для «Вылазки».")]
    [SerializeField] private float huntBatchSizeMultiplier = 0.8f;
    [Tooltip("Множитель интервала для «Вылазки».")]
    [SerializeField] private float huntIntervalMultiplier = 0.9f;

    [Header("Pacing Guards")]
    [Tooltip("Сколько врагов может одновременно находиться на арене. Очередь волны ждёт, пока игрок расчистит поле: без этого предел задаёт только количество в очереди, и на длинных волнах мобы копятся быстрее, чем игрок их убивает.")]
    [SerializeField] private int maxAliveEnemies = 38;
    [Tooltip("Сколько секунд очередь готова ждать, прежде чем выпустить приём всё равно. Страховка от бесконечной волны, если с поля никто не уходит.")]
    [SerializeField] private float aliveCapWaitLimit = 4f;

    [Header("Elite Reveal (Hunt)")]
    [Tooltip("С какой доли волны выпущенного элита выходит на сцену. Пока охота не началась, игрок жерёт обычных врагов и не понимает, куда бежать.")]
    [Range(0f, 0.9f)]
    [SerializeField] private float eliteRevealFraction = 0.4f;
    [Tooltip("Минимум секунд до появления элиты, даже если доля уже набрана.")]
    [SerializeField] private float eliteRevealMinDelay = 4f;
    [Tooltip("Сколько элит выходит за волну на первой охоте и дальше по одной за каждые четыре волны.")]
    [SerializeField] private int baseEliteCount = 1;
    [SerializeField] private int eliteCountPerWaves = 4;
    [SerializeField] private int maxEliteCount = 3;

    [Header("Last Stand")]
    [Tooltip("Размер финальной пачки «последнего рубежа». Маленькая: её задача — последняя мини-решающая задача, а не вторая волна.")]
    [SerializeField] private int lastStandBaseCount = 3;
    [Tooltip("Сколько волн на одну дополнительного сильного врага в финальной пачке.")]
    [SerializeField] private int lastStandCountPerWaves = 3;
    [SerializeField] private int lastStandMaxCount = 8;
    [Tooltip("Пауза перед финальной пачкой — игрок успевает выдохнуть и увидеть, что бой почти закончен.")]
    [SerializeField] private float lastStandDelay = 2.2f;

    [Header("Ambush Spawn Lanes")]
    [Tooltip("Сколько направлений выхода чередуется между приёмами волны.")]
    [SerializeField] private int ambushLaneCount = 3;
    [Tooltip("Разброс внутри направления (градусы). Больше — волна приходит «широкой полосой», меньше — плотным клином.")]
    [SerializeField] private float ambushLaneJitter = 22f;
    [Tooltip("Шаг между направлениями, если направлений больше трёх (градусы). При трёх — ровно 120°.")]
    [SerializeField] private float ambushLaneStep = 120f;

    [Header("Modifier Weights")]
    [Tooltip("Шанс, что «Навал бегунов» заставит конкретного врага быть быстрым.")]
    [Range(0f, 1f)]
    [SerializeField] private float fastAssaultWeight = 0.45f;
    [Tooltip("Шанс, что «Залп дальников» заставит конкретного врага быть дальником.")]
    [Range(0f, 1f)]
    [SerializeField] private float rangedAssaultWeight = 0.45f;
    [Tooltip("Шанс, что «Заход с флангов» добавит бегуна к приёму — волна идёт плотнее и с рывками.")]
    [Range(0f, 1f)]
    [SerializeField] private float ambushFastWeight = 0.3f;
    [Tooltip("Шанс дальника на волне с опасными зонами: зона давит на позицию, снаряд — на внимание.")]
    [Range(0f, 1f)]
    [SerializeField] private float dangerZoneRangedWeight = 0.2f;

    [Header("Player Ring Spawning")]
    [Tooltip("Ближняя граница кольца спауна вокруг игрока.")]
    [SerializeField] private float playerRingMinDistance = 15f;
    [Tooltip("Дальняя граница кольца спауна вокруг игрока.")]
    [SerializeField] private float playerRingMaxDistance = 22f;
    [Tooltip("Минимальная дистанция до игрока, ближе которой враги не появляются никогда, даже если арена не даёт места.")]
    [SerializeField] private float minPlayerSpawnDistance = 8f;

    [Header("Per-Type Ring Start")]
    [Tooltip("Ближняя граница кольца для обычных. Ниже playerRingMinDistance смысла не имеет.")]
    [SerializeField] private float normalMinSpawnDistance = 15f;

    [Tooltip("Быстрые начинают с внешнего края кольца: до игрока ещё надо добежать, поэтому у него остаётся время на реакцию, а не мгновенный навал.")]
    [SerializeField] private float fastMinSpawnDistance = 20f;

    [Tooltip("Танки идут ближе: медленный разгон делает их дистанцию менее опасной.")]
    [SerializeField] private float tankMinSpawnDistance = 16f;

    [Tooltip("Дальники держат среднюю дистанцию: ближе они бьют точнее, но выходят у игрока из-за спины.")]
    [SerializeField] private float rangedMinSpawnDistance = 18f;

    [Tooltip("Элиты выходят с внешнего края кольца.")]
    [SerializeField] private float eliteMinSpawnDistance = 20f;

    [Tooltip("Боссы выходят с внешнего края кольца.")]
    [SerializeField] private float bossMinSpawnDistance = 22f;

    [Header("Arena Bounds")]
    [Tooltip("Полуширина квадратной арены: враги не спавнятся за её пределами (0 — без ограничения). Стены арены на ±50.")]
    [SerializeField] private float arenaHalfSize = 50f;

    [Tooltip("Запас от стен арены при квадратном ограничении спауна.")]
    [SerializeField] private float arenaWallMargin = 1f;

    [Tooltip("Минимальный угол между врагами одного приёма, чтобы они не слипались (градусы).")]
    [SerializeField] private float minAngularSeparation = 18f;

    [Header("Spawn-In Effect")]
    [Tooltip("Длительность «материализации» врага при появлении.")]
    [SerializeField] private float spawnInDuration = 0.35f;

    public int CurrentWave { get; set; } = 1;

    // Архетип текущей волны — влияет на состав и давление. Выставляется
    // менеджером волн до начала спавна.
    public WaveArchetype CurrentArchetype { get; set; } =
        WaveArchetype.Standard;

    // Сколько врагов очередь волны ещё выпустит. Считается вместе с
    // отложенной элитой и финальной пачкой «последнего рубежа», иначе
    // счётчик «осталось» на HUD врал бы в обе стороны: показывал бы
    // ноль при ещё не вышедшей элите и зависал на пачке, которую
    // игрок уже не видит.
    public int PendingSpawnCount { get; private set; }

    // Пока true — волна считается «идущей»: очередь спавна ещё не исчерпана.
    // Нужно менеджеру волн, чтобы не завершать волну, пока враги только едут.
    public bool IsSpawning { get; private set; }

    // Архетип, под который спавнятся враги текущей волны.
    private WaveArchetype currentWaveArchetype =
        WaveArchetype.Standard;

    private WaveModifier currentWaveModifier =
        WaveModifier.None;

    // Индекс текущего «направления выхода» для волны с заходом
    // с флангов: каждый приём смещается на следующее направление,
    // поэтому волна физически «обходит» игрока по кольцу.
    private int currentLaneIndex;

    // Стартовое направление волны с заходом с флангов — одно на
    // всю волну, чтобы чередование полос читалось как система, а не
    // как шум.
    private float waveLaneBaseAngle;

    private Coroutine spawnQueueCoroutine;

    private Camera cachedCamera;

    // Позиции уже заспавненных врагов волны: пока враг материализуется,
    // у него нет коллайдеров, поэтому только по ним можно отсечь соседство.
    private readonly List<Vector3> spawnedPositions =
        new List<Vector3>();

    // Углы текущего приёма волны — для равномерного распределения по кольцу.
    private readonly List<float> batchAngles =
        new List<float>();

    private void Awake()
    {
        cachedCamera = Camera.main;

        ResolvePlayer();

        if (worldGenerator == null)
            worldGenerator =
                FindAnyObjectByType<WorldStructureGenerator>();
    }

    // Ссылка на игрока в инспекторе может указывать на ассет
    // префаба, а не на инстанс в сцене: такой Transform живёт вне
    // иерархии сцены, и его position вечно равен нулю. Проверка
    // player == null такое не ловит — объект валиден, просто не
    // тот. Из-за этого кольцо спауна и все проверки дистанции
    // молча считали опорной точкой центр арены вместо игрока.
    private void ResolvePlayer()
    {
        if (IsUsablePlayer(player))
            return;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    // Годится только объект, живущий в загруженной сцене.
    private static bool IsUsablePlayer(Transform candidate)
    {
        return
            candidate != null &&
            candidate.gameObject.scene.IsValid();
    }

    // =========================================================
    // WAVE EMISSION
    // =========================================================

    public void SpawnWave(
        int enemyCount,
        int wave,
        WaveArchetype archetype =
            WaveArchetype.Standard,
        WaveModifier modifier =
            WaveModifier.None)
    {
        StopSpawnQueue();

        CurrentArchetype = archetype;
        currentWaveArchetype = archetype;
        currentWaveModifier = modifier;
        currentLaneIndex = 0;
        waveLaneBaseAngle = Random.Range(0f, 360f);
        spawnedPositions.Clear();

        IsSpawning = enemyCount > 0;
        spawnQueueCoroutine = StartCoroutine(
            SpawnWaveRoutine(enemyCount, wave, archetype, modifier)
        );
    }

    public void StopSpawnQueue()
    {
        if (spawnQueueCoroutine != null)
        {
            StopCoroutine(spawnQueueCoroutine);
            spawnQueueCoroutine = null;
        }

        spawnedPositions.Clear();
        batchAngles.Clear();
        PendingSpawnCount = 0;
        IsSpawning = false;
    }

    public void SetManualSpawning(bool active)
    {
        IsSpawning = active;

        // Ручной спавн (босс и его прислуга) не ведёт очередь:
        // сколько там будет врагов, решает сценарий, а не спавнер.
        if (active)
            PendingSpawnCount = 0;
    }

    public int MaxAliveEnemies =>
        Mathf.Max(maxAliveEnemies, 1);

    // События посреди волны идут мимо очереди, поэтому потолок живых
    // врагов сам по себе их не касается: без этой проверки засада
    // поверх полной арены добавляет ещё десяток поверх и так
    // предельной нагрузки, и волна становится нечестной, а не сложной.
    // Опасные зоны живых врагов не добавляют, но накрывают арену
    // вокруг игрока, поэтому для них считается та же величина.
    public bool HasRoomForEvent(int plannedCount)
    {
        return
            Enemy.AliveCount +
            Mathf.Max(plannedCount, 0) <=
            MaxAliveEnemies;
    }

    // Волна не вываливается за один кадр, а «вытекает» приёмами:
    // щуп → регулярные порции → паузы. Так у игрока есть время
    // среагировать на каждый выход, а не паника от 20 врагов разом.
    // Архетип правит и размером порций, и частотой приёмов,
    // модификатор — темпом и порядком появления врагов.
    private IEnumerator SpawnWaveRoutine(
        int enemyCount,
        int wave,
        WaveArchetype archetype,
        WaveModifier modifier)
    {
        int remaining = Mathf.Max(enemyCount, 0);
        bool firstBatch = true;

        currentWaveArchetype = archetype;
        currentWaveModifier = modifier;

        // Давление волны растёт с её номером: порции крупнее,
        // паузы между ними короче. Скорость роста настраивается
        // делителем — чем он меньше, тем раньше волны «жарят».
        int batchSizeGrowth =
            Mathf.FloorToInt(
                Mathf.Max(wave - 1, 0) /
                Mathf.Max(batchSizeGrowthDivisor, 1f)
            );

        int effectiveBatchSize =
            Mathf.RoundToInt(
                Mathf.Max(spawnBatchSize, 1) *
                GetBatchSizeMultiplier(archetype)
            ) +
            batchSizeGrowth;

        effectiveBatchSize =
            Mathf.Min(
                effectiveBatchSize,
                Mathf.Max(maxScaledBatchSize, spawnBatchSize)
            );

        float effectiveInterval =
            spawnBatchInterval *
            GetIntervalMultiplier(archetype) *
            Mathf.Pow(
                spawnIntervalDecay,
                Mathf.Max(wave - 1, 0)
            );

        effectiveInterval =
            Mathf.Max(
                effectiveInterval,
                minSpawnInterval *
                GetIntervalMultiplier(archetype)
            );

        int lastStandCount =
            GetLastStandCount(modifier, wave);

        int elitePending =
            GetEliteCountForWave(modifier, wave);

        int lastStandPending = lastStandCount;

        PendingSpawnCount = remaining + elitePending + lastStandPending;

        float elapsed = 0f;

        while (remaining > 0)
        {
            yield return WaitForFieldRoom();

            if (ShouldRevealElites(
                    modifier,
                    elitePending,
                    elapsed,
                    remaining,
                    enemyCount))
            {
                int eliteCount = elitePending;
                elitePending = 0;

                PendingSpawnCount -= eliteCount;

                SpawnEliteWave(wave, eliteCount);

                yield return new WaitForSeconds(
                    effectiveInterval
                );

                elapsed += effectiveInterval;
            }

            int batchSize = firstBatch
                ? Mathf.Min(firstBatchSize, remaining)
                : Mathf.Min(effectiveBatchSize, remaining);
            firstBatch = false;

            // С заходом с фланга приём не раздувается: широкая пачка
            // перестаёт читаться как один фронт и просто
            // разбрасывается по кольцу. Узкий приём зато и сменяет
            // направление чаще — волна обходит игрока сторонами.
            if (modifier.Has(WaveModifier.Ambush))
            {
                batchSize = Mathf.Min(
                    batchSize,
                    Mathf.Max(ambushLaneCount, 1) * 2
                );
            }

            batchAngles.Clear();

            for (int i = 0; i < batchSize; i++)
            {
                SpawnEnemy(
                    wave,
                    null,
                    GetLaneAngleForSlot(i, batchSize)
                );

                remaining--;
                PendingSpawnCount--;

                if (remaining <= 0)
                    break;

                if (spawnStagger > 0f)
                {
                    elapsed += spawnStagger;
                    yield return new WaitForSeconds(spawnStagger);
                }
            }

            // Смена «направления выхода» — только между приёмами,
            // иначе приём расползётся по кольцу и перестанет читаться
            // как одна волна с одного фланга.
            currentLaneIndex++;

            if (remaining > 0 && effectiveInterval > 0f)
            {
                elapsed += effectiveInterval;
                yield return new WaitForSeconds(effectiveInterval);
            }
        }

        // Охота не должна закончиться, пока элита ещё не вышла:
        // иначе игрок убьёт обычных, увидит «конец волны» и получит
        // элиту в следующей — уже без нужды идти к ней.
        if (elitePending > 0)
        {
            yield return WaitForEliteRevealWindow(elapsed);

            PendingSpawnCount -= elitePending;

            SpawnEliteWave(wave, elitePending);
            elitePending = 0;
        }

        // Финальная пачка идёт строго внутри очереди спавна. Если
        // выпустить её отдельным сценарием, IsSpawning уже снялся бы,
        // волна объявилась бы законченной, и игрок увидел бы врагов,
        // которых бой уже не ждёт.
        if (lastStandPending > 0)
        {
            if (lastStandDelay > 0f)
                yield return new WaitForSeconds(lastStandDelay);

            yield return WaitForFieldRoom();

            PendingSpawnCount -= lastStandPending;

            SpawnLastStand(wave, lastStandPending);

            lastStandPending = 0;
        }

        spawnQueueCoroutine = null;
        IsSpawning = false;
    }

    // Очередь ждёт, пока на поле появится место. Ожидание конечное:
    // если с арены никто не уходит, волна всё равно должна
    // продвинуться, иначе она не закончится никогда.
    private IEnumerator WaitForFieldRoom()
    {
        int cap = Mathf.Max(maxAliveEnemies, 1);

        if (Enemy.AliveCount < cap)
            yield break;

        float waited = 0f;
        float limit = Mathf.Max(aliveCapWaitLimit, 0.1f);

        while (Enemy.AliveCount >= cap && waited < limit)
        {
            waited += Time.deltaTime;

            yield return null;
        }
    }

    // =========================================================
    // ELITE REVEAL (HUNT)
    // =========================================================

    private int GetEliteCountForWave(
        WaveModifier modifier,
        int wave)
    {
        // Без акцента «охота на элиту» обычная «Вылазка» оставляет
        // элиту в общей взвешенной смеси — там она часть фона, а не
        // цель. Охота делает её отдельной задачей.
        bool huntActive =
            modifier.Has(WaveModifier.EliteHunt);

        if (!huntActive)
            return 0;

        if (elitePrefab == null || wave < 7)
            return 0;

        int extra =
            Mathf.Max(wave - 7, 0) /
            Mathf.Max(eliteCountPerWaves, 1);

        return Mathf.Clamp(
            baseEliteCount + extra,
            1,
            Mathf.Max(maxEliteCount, 1)
        );
    }

    // Элита выходит не сразу: сначала игрок съедает обычных врагов,
    // привыкает к темпу волны, и только потом получает цель, которую
    // надо найти и решить, когда подходить.
    private bool ShouldRevealElites(
        WaveModifier modifier,
        int elitePending,
        float elapsed,
        int remaining,
        int total)
    {
        if (elitePending <= 0)
            return false;

        if (elapsed < eliteRevealMinDelay)
            return false;

        int totalForFraction =
            Mathf.Max(total, 1);

        float released =
            1f -
            (remaining / (float)totalForFraction);

        return released >= eliteRevealFraction;
    }

    private IEnumerator WaitForEliteRevealWindow(float elapsed)
    {
        // Даже на короткой волне элита не должна выскакивать в тот же
        // кадр, что и последний обычный враг: нужен хоть минимальный
        // шанс её заметить.
        float missing =
            eliteRevealMinDelay -
            elapsed;

        if (missing > 0f)
            yield return new WaitForSeconds(missing);
    }

    // Элиты выходят одной группой, но через обычный поиск свободной
    // точки у края арены — поэтому не слипаются в одну точку и
    // появляются с тем же «материализованием», что и остальные враги.
    private void SpawnEliteWave(
        int wave,
        int count)
    {
        batchAngles.Clear();

        for (int i = 0; i < count; i++)
            SpawnEnemy(wave, elitePrefab);
    }

    // =========================================================
    // LAST STAND
    // =========================================================

    private int GetLastStandCount(
        WaveModifier modifier,
        int wave)
    {
        if (!modifier.Has(WaveModifier.LastStand))
            return 0;

        // Финальная пачка — только когда в игре уже появились типы,
        // из которых есть что выбрать. Иначе она была бы шайкой
        // обычных врагов без следа.
        if (wave < 4 || tankPrefab == null && rangedPrefab == null)
            return 0;

        int extra =
            Mathf.Max(wave - 4, 0) /
            Mathf.Max(lastStandCountPerWaves, 1);

        return Mathf.Clamp(
            lastStandBaseCount + extra,
            1,
            Mathf.Max(lastStandMaxCount, 1)
        );
    }

    // Пачка из сильных типов: танк, дальник и обычный. Роль не в том,
    // чтобы «страшнее», а в том, чтобы игрок последние секунды волны
    // решал, кого бить первым.
    private void SpawnLastStand(
        int wave,
        int count)
    {
        batchAngles.Clear();

        for (int i = 0; i < count; i++)
        {
            SpawnEnemyAtPosition(
                PickLastStandType(wave),
                GetPlayerRingSpawnPosition()
            );
        }
    }

    private EnemyType PickLastStandType(int wave)
    {
        int totalWeight = 0;

        if (normalPrefab != null)
            totalWeight += 2;

        if (wave >= 4 && tankPrefab != null)
            totalWeight += 3;

        if (wave >= 3 && rangedPrefab != null)
            totalWeight += 3;

        if (totalWeight <= 0)
            return EnemyType.Normal;

        int roll = Random.Range(0, totalWeight);

        if (normalPrefab != null)
        {
            roll -= 2;
            if (roll < 0)
                return EnemyType.Normal;
        }

        if (wave >= 4 && tankPrefab != null)
        {
            roll -= 3;
            if (roll < 0)
                return EnemyType.Tank;
        }

        return EnemyType.Ranged;
    }

    private float GetBatchSizeMultiplier(
        WaveArchetype archetype)
    {
        switch (archetype)
        {
            case WaveArchetype.Swarm:
                return Mathf.Max(swarmBatchSizeMultiplier, 0.1f);

            case WaveArchetype.Siege:
                return Mathf.Max(siegeBatchSizeMultiplier, 0.1f);

            case WaveArchetype.Hunt:
                return Mathf.Max(huntBatchSizeMultiplier, 0.1f);

            default:
                return 1f;
        }
    }

    private float GetIntervalMultiplier(
        WaveArchetype archetype)
    {
        switch (archetype)
        {
            case WaveArchetype.Swarm:
                return Mathf.Max(swarmIntervalMultiplier, 0.1f);

            case WaveArchetype.Siege:
                return Mathf.Max(siegeIntervalMultiplier, 0.1f);

            case WaveArchetype.Hunt:
                return Mathf.Max(huntIntervalMultiplier, 0.1f);

            default:
                return 1f;
        }
    }

    private void SpawnEnemy(
        int wave,
        GameObject forcedPrefab = null,
        float preferredAngle = -1f)
    {
        if (player == null)
        {
            Debug.LogWarning(
                "EnemySpawner: Player is not assigned."
            );

            return;
        }

        GameObject prefab =
            forcedPrefab != null
                ? forcedPrefab
                : GetEnemyPrefabForWave(wave);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"EnemySpawner: No enemy prefab available for wave {wave}."
            );

            return;
        }

        if (!TryGetWaveSpawnPosition(
                preferredAngle,
                GetMinSpawnDistanceFor(GetTypeForPrefab(prefab)),
                out Vector3 spawnPosition))
        {
            Debug.LogWarning(
                "EnemySpawner: Could not find a free spawn position."
            );

            return;
        }

        GameObject enemyObject =
            Instantiate(
                prefab,
                spawnPosition,
                Quaternion.identity
            );

        spawnedPositions.Add(spawnPosition);

        ApplySpawnInEffect(enemyObject, spawnInDuration);

        Enemy enemy = enemyObject.GetComponent<Enemy>();

        if (enemy != null)
            enemy.Initialize(wave, currentWaveModifier);
    }

    // =========================================================
    // SPAWN POSITIONS
    // =========================================================

    // Обычный приём берёт случайный угол по всему кольцу. Приём волны с
    // акцентом «заход с флангов» вместо этого раскладывается вдоль
    // полосы выхода, а полоса меняется от приёма к приёму: волна
    // приходит попеременно с разных сторон, и «переждать» её в одном
    // углу больше нельзя.
    private float GetLaneAngleForSlot(
        int slotInBatch,
        int batchSize)
    {
        if (!currentWaveModifier.Has(WaveModifier.Ambush))
            return -1f;

        int lanes =
            Mathf.Max(ambushLaneCount, 1);

        if (lanes <= 1)
            return -1f;

        float step =
            lanes == 3
                ? ambushLaneStep
                : 360f / lanes;

        int lane =
            PositiveMod(currentLaneIndex, lanes);

        // Стартовое направление выбирается один раз на волну. Если
        // брать случайный базовый угол для каждого приёма, полосы
        // разъезжаются и «заход с флангов» превращается в обычный
        // случайный спавн, то есть в обычную волну.
        float center =
            waveLaneBaseAngle +
            lane * step +
            Random.Range(
                -ambushLaneJitter,
                ambushLaneJitter
            );

        // Враги одного приёма раскладываются линией вдоль полосы.
        // Случайный угол здесь не годится: проверка минимального
        // угла между соседями отбрасывала бы большую часть попыток,
        // и приём вырождался бы в одного-двух мобов за раз.
        //
        // Шаг обязан быть не меньше minAngularSeparation, а не
        // «чуть меньше». Проверка отбрасывает угол, если он ближе
        // этого порога к уже занятому, и повторные попытки берут тот
        // же угол: при шаге меньше порога приём не проходит целиком
        // и не спавнится ни одного врага.
        float spacing =
            Mathf.Max(minAngularSeparation, 12f);

        float offset =
            (slotInBatch - (batchSize - 1) * 0.5f) *
            spacing;

        return Mathf.Repeat(center + offset, 360f);
    }

    private static int PositiveMod(int value, int modulus)
    {
        int result = value % modulus;

        return result < 0
            ? result + modulus
            : result;
    }

    // Кольцо спауна строится вокруг игрока, а не вокруг центра арены:
    // иначе игрок может отойти к стене и стоять там, пока все подходят
    // через всю карту. Чтобы враги при этом не появлялись за стенами,
    // луч от игрока обрезается границами арены: если в выбранном
    // направлении до стены меньше, чем кольцо, радиус подрезается до
    // доступного, а если и его не хватает — направление отбрасывается.
    //
    // Три прохода по строгости, чтобы враги гарантированно
    // заспавнились даже если игрок зажат в углу: 0 — вне экрана,
    // 1 — без проверки экрана, 2 — с гарантированным зазором.
    //
    // preferredAngle >= 0 — враг обязан выйти из этого направления:
    // повторные попытки поиска свободной точки не имеют права
    // разбрасывать приём по всему кольцу.
    // Ближняя граница кольца для конкретного типа. Значение не может
    // оказаться выше playerRingMaxDistance: иначе кольцо схлопнется в
    // точку и спавн прижмётся к стене. В этом случае берётся дальняя
    // граница — «максимально издалека», а не «невозможно».
    private float GetMinSpawnDistanceFor(
        EnemyType enemyType)
    {
        float value;

        switch (enemyType)
        {
            case EnemyType.Normal:
                value = normalMinSpawnDistance;
                break;

            case EnemyType.Fast:
                value = fastMinSpawnDistance;
                break;

            case EnemyType.Tank:
                value = tankMinSpawnDistance;
                break;

            case EnemyType.Ranged:
                value = rangedMinSpawnDistance;
                break;

            case EnemyType.Elite:
                value = eliteMinSpawnDistance;
                break;

            case EnemyType.Boss:
                value = bossMinSpawnDistance;
                break;

            default:
                value = playerRingMinDistance;
                break;
        }

        float ringMax =
            Mathf.Max(playerRingMaxDistance, playerRingMinDistance);

        return Mathf.Clamp(
            value,
            0f,
            ringMax
        );
    }

    // Обратная к GetPrefabByType: приём уже выбрал префаб, но
    // дистанция настраивается по типу, а не по ассету.
    private EnemyType GetTypeForPrefab(
        GameObject prefab)
    {
        if (prefab == null)
            return EnemyType.Normal;

        if (prefab == elitePrefab)
            return EnemyType.Elite;

        if (prefab == bossPrefab)
            return EnemyType.Boss;

        if (prefab == fastPrefab)
            return EnemyType.Fast;

        if (prefab == tankPrefab)
            return EnemyType.Tank;

        if (prefab == rangedPrefab)
            return EnemyType.Ranged;

        return EnemyType.Normal;
    }

    private bool TryGetWaveSpawnPosition(
        float preferredAngle,
        out Vector3 spawnPosition)
    {
        return TryGetWaveSpawnPosition(
            preferredAngle,
            playerRingMinDistance,
            out spawnPosition
        );
    }

    // preferredAngle >= 0 — враг обязан выйти из этого направления:
    // повторные попытки поиска свободной точки не имеют права
    // разбрасывать приём по всему кольцу.
    private bool TryGetWaveSpawnPosition(
        float preferredAngle,
        float typeMinDistance,
        out Vector3 spawnPosition)
    {
        Vector3 ringCenter = GetRingCenter();

        // Ближняя граница — это максимум из общей и типовой: обычные
        // берут playerRingMinDistance, быстрые отталкиваются дальше.
        float ringMin =
            Mathf.Max(typeMinDistance, 0f);

        float ringMax =
            Mathf.Max(playerRingMaxDistance, ringMin);

        for (int pass = 0; pass < 3; pass++)
        {
            // Даже последний, «а лишь бы где-нибудь» проход держит
            // минимальный зазор. Иначе на зажатой арене волна
            // материализуется прямо на игроке.
            float requiredDistance =
                pass <= 1
                    ? minPlayerSpawnDistance
                    : Mathf.Max(minimumSpawnDistance * 3f, 4f);

            for (int attempt = 0;
                 attempt < spawnAttempts;
                 attempt++)
            {
                float angle =
                    preferredAngle >= 0f
                        ? preferredAngle
                        : Random.Range(0f, 360f);

                if (!IsAngleSeparated(angle))
                    continue;

                // Дальняя граница — это ringMax, подрезанная лучом до стены.
                // Без Mathf.Min кольцо растягивалось от ringMin до самой
                // стены, и враги выходили у самого края арены вместо
                // того, чтобы подойти к игроку.
                float maxRadius =
                    Mathf.Min(
                        ringMax,
                        GetRayLimitInsideArena(ringCenter, angle)
                    );

                // В этом направлении арена не вмещает даже минимальный
                // зазор: брать точку у стены под ногами игрока хуже,
                // чем поискать другое направление.
                if (maxRadius < requiredDistance)
                    continue;

                // Ближняя граница поджимается к обрезанному лучу: иначе
                // Random.Range получил бы min > max.
                float minRadius =
                    Mathf.Min(ringMin, maxRadius);

                float radius =
                    Random.Range(minRadius, maxRadius);

                // Высоту берём у игрока: пол арены приподнят относительно
                // нуля, а коллайдер врага центрирован на его transform.
                // Спавн на y = arenaCenter.y утапливал врага в KillZone.
                Vector3 candidate =
                    ringCenter +
                    new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                        0f,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                    );

                candidate.y =
                    IsUsablePlayer(player)
                        ? player.position.y
                        : GetArenaCenter().y;

                // Не выходить за квадратные границы игровой арены
                // (стены на ±50): луч уже обрезан, но после подрезки
                // радиуса точка всё равно прижимается к стене.
                candidate = ClampToArena(candidate);

                // Проверка зазора идёт от настоящего игрока. С ассетом
                // префаба в поле distance считалась от нуля, и гарантия
                // «не ближе minPlayerSpawnDistance» не работала вовсе.
                if (IsUsablePlayer(player) &&
                    FlatDistance(player.position, candidate) <
                    requiredDistance)
                {
                    continue;
                }

                if (pass == 0 && IsPositionVisible(candidate))
                    continue;

                if (IsSpawnPositionFree(candidate))
                {
                    batchAngles.Add(angle);
                    spawnPosition = candidate;
                    return true;
                }
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    // Центр кольца спауна — позиция игрока. Без пригодной ссылки на
    // игрока (загрузка, отладка) откатываемся на центр арены, чтобы
    // волна не собралась в точке у нуля.
    private Vector3 GetRingCenter()
    {
        if (!IsUsablePlayer(player))
            return GetArenaCenter();

        return new Vector3(
            player.position.x,
            GetArenaCenter().y,
            player.position.z
        );
    }

    // Насколько далеко от точки можно уйти в этом направлении, не
    // выйдя за квадратные стены арены. Возвращает бесконечность,
    // если ограничение выключено.
    private float GetRayLimitInsideArena(
        Vector3 origin,
        float angle)
    {
        if (arenaHalfSize <= 0f)
            return float.MaxValue;

        float limit =
            Mathf.Max(
                arenaHalfSize - arenaWallMargin,
                0f
            );

        float directionX =
            Mathf.Cos(angle * Mathf.Deg2Rad);
        float directionZ =
            Mathf.Sin(angle * Mathf.Deg2Rad);

        float distance = float.MaxValue;

        if (directionX > 0.0001f)
        {
            distance =
                Mathf.Min(
                    distance,
                    (limit - origin.x) / directionX
                );
        }
        else if (directionX < -0.0001f)
        {
            distance =
                Mathf.Min(
                    distance,
                    (-limit - origin.x) / directionX
                );
        }

        if (directionZ > 0.0001f)
        {
            distance =
                Mathf.Min(
                    distance,
                    (limit - origin.z) / directionZ
                );
        }
        else if (directionZ < -0.0001f)
        {
            distance =
                Mathf.Min(
                    distance,
                    (-limit - origin.z) / directionZ
                );
        }

        return distance;
    }

    // Точка с кольца вокруг игрока — для боссов, прислуги и
    // финальной пачки «последнего рубежа».
    //
    // Раньше кольцо бралось вслепую, без единой проверки: игрок,
    // стоящий на той же окружности, получал босса на голову, а
    // финальная пачка выходила вплотную к тем, кого должна была
    // оставить на последние секунды волны. Теперь та же
    // трёхпроходная проверка, что и у обычного приёма: вне экрана
    // и не ближе проёма, затем без экрана, затем с гарантированным
    // зазором.
    public Vector3 GetPlayerRingSpawnPosition()
    {
        // Угол не задан — приём сам раскидывается по кольцу, иначе
        // босс с прислугой вышли бы одной точкой.
        if (TryGetWaveSpawnPosition(-1f, out Vector3 spawnPosition))
            return spawnPosition;

        // Арена настолько забита, что свободной точки вне экрана не
        // нашлось. Отдаём ближний край кольца — хоть и близко, но
        // точно не в стене и не под ногами у игрока.
        Vector3 ringCenter = GetRingCenter();

        float radius =
            Mathf.Max(minPlayerSpawnDistance, 0.1f);

        Vector3 fallback =
            ringCenter +
            new Vector3(0f, 0f, radius);

        fallback.y = ringCenter.y;

        return ClampToArena(fallback);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;

        return Mathf.Sqrt(dx * dx + dz * dz);
    }

    // Враги не должны появляться за пределами игровой арены:
    // точка прижимается к квадратным стенам ±(arenaHalfSize - запас).
    private Vector3 ClampToArena(Vector3 position)
    {
        if (arenaHalfSize <= 0f)
            return position;

        float limit =
            Mathf.Max(
                arenaHalfSize - arenaWallMargin,
                0f
            );

        position.x = Mathf.Clamp(position.x, -limit, limit);
        position.z = Mathf.Clamp(position.z, -limit, limit);

        return position;
    }

    // Углы одного приёма держим на равном удалении, чтобы враги
    // не выходили группой в одну точку, а распределялись по кольцу.
    private bool IsAngleSeparated(float angle)
    {
        if (batchAngles.Count == 0)
            return true;

        for (int i = 0; i < batchAngles.Count; i++)
        {
            float difference =
                Mathf.Repeat(
                    Mathf.Abs(angle - batchAngles[i]),
                    360f
                );

            difference =
                Mathf.Min(
                    difference,
                    360f - difference
                );

            if (difference < minAngularSeparation)
                return false;
        }

        return true;
    }

    // True, если точка попала в видимую область камеры (с запасом).
    // Запас чуть шире экрана, чтобы враг не «вспыхивал» на кромке кадра.
    private bool IsPositionVisible(Vector3 worldPosition)
    {
        if (cachedCamera == null)
            cachedCamera = Camera.main;

        Camera cameraComponent = cachedCamera;

        if (cameraComponent == null)
            return false;

        Vector3 viewport =
            cameraComponent.WorldToViewportPoint(worldPosition);

        if (viewport.z < 0f)
            return false;

        const float margin = 0.03f;

        return
            viewport.x > -margin &&
            viewport.x < 1f + margin &&
            viewport.y > -margin &&
            viewport.y < 1f + margin;
    }

    private bool IsSpawnPositionFree(
        Vector3 position)
    {
        Collider[] colliders =
            StructureQuery.OverlapSphere(
                position,
                minimumSpawnDistance,
                out int colliderCount
            );

        for (int i = 0; i < colliderCount; i++)
        {
            Collider collider = colliders[i];

            if (ColliderKindQuery.GetEnemy(collider) != null)
                return false;

            if (StructureQuery.IsWorldStructure(collider))
                return false;
        }

        // Враги, которые сейчас «материализуются», не имеют коллайдеров,
        // поэтому их позиции отслеживаем списком.
        for (int i = 0; i < spawnedPositions.Count; i++)
        {
            if (Vector3.Distance(
                    spawnedPositions[i],
                    position
                ) < minimumSpawnDistance * 2f)
            {
                return false;
            }
        }

        return true;
    }

    private bool TryGetFreePositionAround(
        Vector3 center,
        out Vector3 spawnPosition)
    {
        // Сначала проверяем саму заданную точку.
        if (IsSpawnPositionFree(center))
        {
            spawnPosition = center;
            return true;
        }

        // Если занято — ищем свободное место рядом.
        for (int attempt = 0;
             attempt < spawnAttempts;
             attempt++)
        {
            Vector2 randomOffset =
                Random.insideUnitCircle *
                minimumSpawnDistance * 2f;

            spawnPosition =
                ClampToArena(
                    center +
                    new Vector3(
                        randomOffset.x,
                        0f,
                        randomOffset.y
                    )
                );

            if (IsSpawnPositionFree(spawnPosition))
            {
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
    }

    private Vector3 GetArenaCenter()
    {
        if (worldGenerator != null)
            return worldGenerator.ArenaCenter;

        return Vector3.zero;
    }

    // Границы квадратной арены для внешних систем. Опасная зона,
    // поставленная за стеной, не опасна, а только тратит событие:
    // такую точку должен уметь отбраковать тот, кто ставит.
    public float ArenaHalfSize =>
        arenaHalfSize;

    public Vector3 ArenaCenter =>
        GetArenaCenter();

    // =========================================================
    // SPAWN-IN EFFECT
    // =========================================================

    private void ApplySpawnInEffect(
        GameObject enemyObject,
        float duration)
    {
        if (duration <= 0f)
            return;

        SpawnInEffect effect =
            enemyObject.AddComponent<SpawnInEffect>();

        effect.Initialize(duration);
    }

    // =========================================================
    // SINGLE ENEMY SPAWN (Boss / Summons)
    // =========================================================

    public Enemy SpawnEnemyAtPosition(
        EnemyType enemyType,
        Vector3 position,
        float spawnDuration = -1f)
    {
        GameObject prefab =
            GetPrefabByType(enemyType);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"EnemySpawner: Prefab for {enemyType} is not assigned."
            );

            return null;
        }

        // Заявленная точка прижимается к арене: босс у стены зовёт
        // прислугу, и без зажима часть призыва уходила за стену.
        Vector3 spawnPosition;

        if (!TryGetFreePositionAround(
                ClampToArena(position),
                out spawnPosition))
        {
            Debug.LogWarning(
                $"EnemySpawner: Could not find a free position for {enemyType}."
            );

            return null;
        }

        GameObject enemyObject =
            Instantiate(
                prefab,
                spawnPosition,
                Quaternion.identity
            );

        float effectDuration =
            spawnDuration >= 0f
                ? spawnDuration
                : spawnInDuration;

        ApplySpawnInEffect(enemyObject, effectDuration);

        Enemy enemy = enemyObject.GetComponent<Enemy>();

        if (enemy != null)
            enemy.Initialize(CurrentWave, currentWaveModifier);

        return enemy;
    }

    // =========================================================
    // MID-WAVE EVENTS (Ambush / Rush)
    // =========================================================

    // Засада: волна уже на игроке и приходит сразу со всех сторон.
    // Отличие от «Рывка» — в дистанции и составе: враги появляются
    // вплотную (игрок уже в бою) и вперемешку, а не одним типом с
    // дальнего фланга. Задача события — заставить отойти, а не
    // выбирать, кого бить первым.
    // Пошаговая версия. Пачка, материализующаяся в один кадр, не
    // читается: игрок не успевает ни уклониться, ни выбрать цель,
    // а урон прилетает сразу из восьми сторон. Шаг в 0.1с при
    // длительности появления 0.35с даёт узнаваемый фронт.
    public void SpawnAmbush(
        int count,
        float minDistance,
        float maxDistance,
        bool allowRanged = true,
        float stagger = 0f)
    {
        if (player == null || count <= 0)
            return;

        StartCoroutine(
            SpawnAmbushRoutine(
                count,
                minDistance,
                maxDistance,
                allowRanged,
                stagger
            )
        );
    }

    private IEnumerator SpawnAmbushRoutine(
        int count,
        float minDistance,
        float maxDistance,
        bool allowRanged,
        float stagger)
    {
        // Кольцо строится до старта, иначе каждый враг брал бы
        // новый случайный старт и половина кольца выходила бы
        // с одного бока.
        float startAngle = Random.Range(0f, 360f);

        for (int i = 0; i < count; i++)
        {
            if (stagger > 0f &&
                i > 0)
            {
                yield return new WaitForSeconds(stagger);
            }

            // Если за время шага игрок вышел из боя или начался
            // откат — не доспавниваем остаток: кольцо вокруг уже
            // не имеет смысла.
            if (player == null)
                yield break;

            float angle =
                Mathf.Repeat(
                    startAngle +
                    (float)i / count * 360f +
                    Random.Range(-12f, 12f),
                    360f
                );

            float radius =
                Random.Range(minDistance, maxDistance);

            Vector3 desired =
                player.position +
                new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                );

            desired.y = player.position.y;

            desired = ClampToArena(desired);

            desired = RestorePlayerDistance(desired, minDistance);

            if (!IsSpawnPositionFree(desired) &&
                !TryGetFreePositionAround(desired, out desired))
            {
                continue;
            }

            desired = ClampToArena(desired);

            SpawnEnemyAtPosition(
                PickAmbushType(allowRanged),
                desired
            );
        }
    }

    // Зажим по квадрату арены срезает дистанцию: игрок у самой стены
    // получал засаду в упор, потому что точка «в 13-17 метрах» от
    // него оказывалась в метре от стены, то есть в полутора метрах
    // от игрока.
    //
    // Лечится вдоль того же луча: берём максимум, который арена
    // позволяет в этом направлении. Если арена помещается — враг
    // выходит на запрошенную дистанцию, если нет — на максимально
    // возможную, но всё равно не вплотную.
    private Vector3 RestorePlayerDistance(
        Vector3 position,
        float minDistance)
    {
        if (player == null || minDistance <= 0f)
            return position;

        Vector3 origin = player.position;

        Vector3 offset = position - origin;
        offset.y = 0f;

        if (offset.sqrMagnitude < 0.0001f)
            return position;

        Vector3 restored =
            origin +
            offset.normalized *
            minDistance;

        restored.y = position.y;

        return ClampToArena(restored);
    }

    // Смесь внутри засады: быстрые давят, обычные держат кольцо,
    // дальник бьёт с закрытого фланга. Если дальник ещё не открыт
    // по номеру волны, роли просто делятся между двумя типами.
    private EnemyType PickAmbushType(
        bool allowRanged)
    {
        bool canDash = CurrentWave >= 2 && fastPrefab != null;

        bool canShoot =
            allowRanged &&
            CurrentWave >= 3 &&
            rangedPrefab != null;

        if (!canDash)
            return EnemyType.Normal;

        if (canShoot && Random.value < 0.25f)
            return EnemyType.Ranged;

        return Random.value < 0.7f
            ? EnemyType.Fast
            : EnemyType.Normal;
    }

    // Стенда с одного фланга: только быстрые, с самого края арены и
    // из узкой дуги. Отличие от «Засады» — в дистанции и ритме: пачка
    // приходит волнами с одного направления, и игрок успевает занять
    // позицию напротив фланга, а не убегать от кольца.
    // centerAngle >= 0 держит направление между волнами пачки —
    // иначе две волны пришли бы с разных сторон и событие
    // превратилось бы в засаду.
    public void SpawnRushPack(
        int count,
        float arcDegrees,
        int wave,
        float centerAngle = -1f)
    {
        if (player == null || count <= 0)
            return;

        Vector3 ringCenter = GetRingCenter();

        // Стенда идёт с одного фланга и почти всегда быстрая, поэтому
        // ближняя граница берётся типовой: иначе пачка вылезала бы
        // из-под ног на максимальной скорости, ровно тот навал,
        // от которого стенда и должна была отличаться.
        EnemyType type =
            wave >= 2
                ? EnemyType.Fast
                : EnemyType.Normal;

        float ringMin =
            GetMinSpawnDistanceFor(type);
        float ringMax =
            Mathf.Max(playerRingMaxDistance, ringMin);

        if (centerAngle < 0f)
            centerAngle = Random.Range(0f, 360f);

        float halfArc =
            Mathf.Clamp(arcDegrees, 10f, 180f) *
            0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle =
                centerAngle +
                Random.Range(-halfArc, halfArc);

            // Стенда идёт с одного фланга, поэтому луч обрезается
            // ареной: если у игрока за спиной стена, пачка выходит
            // с доступной стороны. Дальняя граница — ringMax, иначе
            // кольцо растянулось бы до самой стены.
            float maxRadius =
                Mathf.Min(
                    ringMax,
                    GetRayLimitInsideArena(ringCenter, angle)
                );

            if (maxRadius < minPlayerSpawnDistance)
                continue;

            float minRadius =
                Mathf.Min(ringMin, maxRadius);

            float radius =
                Random.Range(minRadius, maxRadius);

            Vector3 candidate =
                ringCenter +
                new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                );

            candidate.y = ringCenter.y;

            candidate = ClampToArena(candidate);

            if (!IsSpawnPositionFree(candidate) &&
                !TryGetFreePositionAround(candidate, out candidate))
            {
                continue;
            }

            // Немного разного времени материализации, чтобы пачка
            // выходила «волной», а не одним кадром.
            float materialize =
                Random.Range(
                    spawnInDuration * 0.6f,
                    spawnInDuration * 1.3f
                );

            SpawnEnemyAtPosition(type, candidate, materialize);
        }
    }

    private GameObject GetPrefabByType(
        EnemyType enemyType)
    {
        switch (enemyType)
        {
            case EnemyType.Normal:
                return normalPrefab;

            case EnemyType.Fast:
                return fastPrefab;

            case EnemyType.Tank:
                return tankPrefab;

            case EnemyType.Ranged:
                return rangedPrefab;

            case EnemyType.Elite:
                return elitePrefab;

            case EnemyType.Boss:
                return bossPrefab;
        }

        return null;
    }

    // =========================================================
    // WAVE COMPOSITION
    // =========================================================

    private GameObject GetEnemyPrefabForWave(
        int wave)
    {
        // Модификатор не добавляет новых типов — он перекачивает
        // часть существующих ролей в конкретный тип. Поэтому волна
        // выглядит и ощущается иначе, а пул врагов остаётся прежним.
        if (TryGetModifierPrefab(wave, out GameObject forced))
            return forced;

        switch (currentWaveArchetype)
        {
            case WaveArchetype.Swarm:
                return GetSwarmPrefabForWave(wave);

            case WaveArchetype.Siege:
                return GetSiegePrefabForWave(wave);

            case WaveArchetype.Hunt:
                return GetHuntPrefabForWave(wave);

            default:
                return GetStandardPrefabForWave(wave);
        }
    }

    // Акцент волны решает, кого выбить из роли. Шансы небольшие:
    // модификатор должен менять характер волны, а не превращать её
    // в «восемьдесят процентов дальников».
    private bool TryGetModifierPrefab(
        int wave,
        out GameObject prefab)
    {
        prefab = null;

        if (currentWaveModifier.Has(WaveModifier.FastAssault) &&
            wave >= 2 &&
            fastPrefab != null &&
            Random.value < fastAssaultWeight)
        {
            prefab = fastPrefab;
            return true;
        }

        if (currentWaveModifier.Has(WaveModifier.RangedAssault) &&
            wave >= 3 &&
            rangedPrefab != null &&
            Random.value < rangedAssaultWeight)
        {
            prefab = rangedPrefab;
            return true;
        }

        if (currentWaveModifier.Has(WaveModifier.Ambush) &&
            wave >= 2 &&
            fastPrefab != null &&
            Random.value < ambushFastWeight)
        {
            prefab = fastPrefab;
            return true;
        }

        if (currentWaveModifier.Has(WaveModifier.DangerZone) &&
            wave >= 3 &&
            rangedPrefab != null &&
            Random.value < dangerZoneRangedWeight)
        {
            prefab = rangedPrefab;
            return true;
        }

        return false;
    }

    // Сбалансированная смесь всех типов.
    private GameObject GetStandardPrefabForWave(
        int wave)
    {
        int totalWeight = 0;

        if (normalPrefab != null)
            totalWeight += 10;

        if (wave >= 2 && fastPrefab != null)
            totalWeight += 3;

        if (wave >= 3 && rangedPrefab != null)
            totalWeight += 2;

        if (wave >= 4 && tankPrefab != null)
            totalWeight += 1;

        if (wave >= 7 && elitePrefab != null)
            totalWeight += 1 + Mathf.FloorToInt((wave - 7) / 12f);

        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);

        if (normalPrefab != null)
        {
            roll -= 10;
            if (roll < 0)
                return normalPrefab;
        }

        if (wave >= 2 && fastPrefab != null)
        {
            roll -= 3;
            if (roll < 0)
                return fastPrefab;
        }

        if (wave >= 3 && rangedPrefab != null)
        {
            roll -= 2;
            if (roll < 0)
                return rangedPrefab;
        }

        if (wave >= 4 && tankPrefab != null)
        {
            roll -= 1;
            if (roll < 0)
                return tankPrefab;
        }

        if (wave >= 7 && elitePrefab != null)
            return elitePrefab;

        return null;
    }

    // «Рой»: давление быстрых бегунов.
    private GameObject GetSwarmPrefabForWave(
        int wave)
    {
        int totalWeight = 0;

        if (normalPrefab != null)
            totalWeight += 4;

        if (wave >= 2 && fastPrefab != null)
            totalWeight += 9;

        if (wave >= 4 && rangedPrefab != null)
            totalWeight += 1;

        if (wave >= 8 && elitePrefab != null)
            totalWeight += 1 + Mathf.FloorToInt((wave - 8) / 8f);

        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);

        if (normalPrefab != null)
        {
            roll -= 4;
            if (roll < 0)
                return normalPrefab;
        }

        if (wave >= 2 && fastPrefab != null)
        {
            roll -= 9;
            if (roll < 0)
                return fastPrefab;
        }

        if (wave >= 4 && rangedPrefab != null)
        {
            roll -= 1;
            if (roll < 0)
                return rangedPrefab;
        }

        if (wave >= 8 && elitePrefab != null)
            return elitePrefab;

        return null;
    }

    // «Осада»: тяжёлые впереди, дальники стреляют из-за блоков.
    private GameObject GetSiegePrefabForWave(
        int wave)
    {
        int totalWeight = 0;

        if (normalPrefab != null)
            totalWeight += 4;

        if (wave >= 2 && fastPrefab != null)
            totalWeight += 1;

        if (wave >= 3 && rangedPrefab != null)
            totalWeight += 6;

        if (wave >= 4 && tankPrefab != null)
            totalWeight += 3;

        if (wave >= 9 && elitePrefab != null)
            totalWeight += 1;

        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);

        if (normalPrefab != null)
        {
            roll -= 4;
            if (roll < 0)
                return normalPrefab;
        }

        if (wave >= 2 && fastPrefab != null)
        {
            roll -= 1;
            if (roll < 0)
                return fastPrefab;
        }

        if (wave >= 3 && rangedPrefab != null)
        {
            roll -= 6;
            if (roll < 0)
                return rangedPrefab;
        }

        if (wave >= 4 && tankPrefab != null)
        {
            roll -= 3;
            if (roll < 0)
                return tankPrefab;
        }

        if (wave >= 9 && elitePrefab != null)
            return elitePrefab;

        return null;
    }

    // «Вылазка»: элита впереди под прикрытием обычных.
    // При акценте «охота на элиту» элита выводится отдельной
    // задачей, поэтому из общей смеси она убирается — иначе охоту
    // портит случайная элита в общей толпе, и цель теряется.
    private GameObject GetHuntPrefabForWave(
        int wave)
    {
        bool huntActive =
            currentWaveModifier.Has(WaveModifier.EliteHunt);

        int totalWeight = 0;

        if (normalPrefab != null)
            totalWeight += 6;

        if (wave >= 2 && fastPrefab != null)
            totalWeight += 2;

        if (wave >= 3 && rangedPrefab != null)
            totalWeight += 2;

        if (wave >= 4 && tankPrefab != null)
            totalWeight += 1;

        if (!huntActive && wave >= 7 && elitePrefab != null)
            totalWeight += 2;

        if (totalWeight <= 0)
            return null;

        int roll = Random.Range(0, totalWeight);

        if (normalPrefab != null)
        {
            roll -= 6;
            if (roll < 0)
                return normalPrefab;
        }

        if (wave >= 2 && fastPrefab != null)
        {
            roll -= 2;
            if (roll < 0)
                return fastPrefab;
        }

        if (wave >= 3 && rangedPrefab != null)
        {
            roll -= 2;
            if (roll < 0)
                return rangedPrefab;
        }

        if (wave >= 4 && tankPrefab != null)
        {
            roll -= 1;
            if (roll < 0)
                return tankPrefab;
        }

        if (!huntActive && wave >= 7 && elitePrefab != null)
            return elitePrefab;

        return normalPrefab;
    }
}