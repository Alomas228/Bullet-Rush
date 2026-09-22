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
    [Tooltip("Минимальная дистанция до игрока, ближе которой враги не появляются.")]
    [SerializeField] private float minPlayerSpawnDistance = 15f;
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

    [Header("Arena Edge Spawning")]
    [Tooltip("Ближняя граница кольца спауна у края арены (доля радиуса).")]
    [SerializeField] private float arenaEdgeMin = 0.85f;
    [Tooltip("Дальняя граница кольца спауна у края арены (доля радиуса).")]
    [SerializeField] private float arenaEdgeMax = 0.95f;
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

    // Пока true — волна считается «идущей»: очередь спавна ещё не исчерпана.
    // Нужно менеджеру волн, чтобы не завершать волну, пока враги только едут.
    public bool IsSpawning { get; private set; }

    // Архетип, под который спавнятся враги текущей волны.
    private WaveArchetype currentWaveArchetype =
        WaveArchetype.Standard;

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

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject != null)
                player = playerObject.transform;
        }

        if (worldGenerator == null)
            worldGenerator =
                FindAnyObjectByType<WorldStructureGenerator>();
    }

    // =========================================================
    // WAVE EMISSION
    // =========================================================

    public void SpawnWave(
        int enemyCount,
        int wave,
        WaveArchetype archetype =
            WaveArchetype.Standard)
    {
        StopSpawnQueue();

        CurrentArchetype = archetype;
        currentWaveArchetype = archetype;
        spawnedPositions.Clear();

        IsSpawning = enemyCount > 0;
        spawnQueueCoroutine = StartCoroutine(
            SpawnWaveRoutine(enemyCount, wave, archetype)
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
        IsSpawning = false;
    }

    public void SetManualSpawning(bool active)
    {
        IsSpawning = active;
    }

    // Волна не вываливается за один кадр, а «вытекает» приёмами:
    // щуп → регулярные порции → паузы. Так у игрока есть время
    // среагировать на каждый выход, а не паника от 20 врагов разом.
    // Архетип правит и размером порций, и частотой приёмов.
    private IEnumerator SpawnWaveRoutine(
        int enemyCount,
        int wave,
        WaveArchetype archetype)
    {
        int remaining = Mathf.Max(enemyCount, 0);
        bool firstBatch = true;

        currentWaveArchetype = archetype;

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

        // «Вылазка» гарантированно выводит одну элиту первой порцией.
        bool elitePending =
            archetype == WaveArchetype.Hunt &&
            wave >= 7;

        while (remaining > 0)
        {
            int batchSize = firstBatch
                ? Mathf.Min(firstBatchSize, remaining)
                : Mathf.Min(effectiveBatchSize, remaining);
            firstBatch = false;

            batchAngles.Clear();

            for (int i = 0; i < batchSize; i++)
            {
                if (elitePending)
                {
                    SpawnEnemy(wave, elitePrefab);
                    elitePending = false;
                }
                else
                {
                    SpawnEnemy(wave);
                }

                remaining--;

                if (remaining <= 0)
                    break;

                if (spawnStagger > 0f)
                    yield return new WaitForSeconds(spawnStagger);
            }

            if (remaining > 0 && effectiveInterval > 0f)
                yield return new WaitForSeconds(effectiveInterval);
        }

        spawnQueueCoroutine = null;
        IsSpawning = false;
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
        GameObject forcedPrefab = null)
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

        if (!TryGetWaveSpawnPosition(out Vector3 spawnPosition))
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
            enemy.Initialize(wave);
    }

    // =========================================================
    // SPAWN POSITIONS
    // =========================================================

    // Кольцо спауна лежит у края арены, а не вокруг игрока.
    // Три прохода по строгости, чтобы враги гарантированно заспавнились
    // даже если игрок встал у стены: 0 — вне экрана + дистанция до игрока,
    // 1 — без проверки экрана, 2 — только кольцо арены и соседство.
    private bool TryGetWaveSpawnPosition(
        out Vector3 spawnPosition)
    {
        Vector3 arenaCenter = GetArenaCenter();
        float minRadius =
            GetArenaRadius() * arenaEdgeMin;
        float maxRadius =
            GetArenaRadius() * arenaEdgeMax;

        for (int pass = 0; pass < 3; pass++)
        {
            for (int attempt = 0;
                 attempt < spawnAttempts;
                 attempt++)
            {
                float angle =
                    Random.Range(0f, 360f);

                if (!IsAngleSeparated(angle))
                    continue;

                float radius =
                    Random.Range(minRadius, maxRadius);

                // Высоту берём у игрока: пол арены приподнят относительно
                // нуля, а коллайдер врага центрирован на его transform.
                // Спавн на y = arenaCenter.y утапливал врага в KillZone.
                float spawnHeight =
                    player != null
                        ? player.position.y
                        : arenaCenter.y;

                Vector3 candidate =
                    arenaCenter +
                    new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                        spawnHeight,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                    );

                if (pass <= 1)
                {
                    float distanceToPlayer =
                        Vector3.Distance(
                            player.position,
                            candidate
                        );

                    if (distanceToPlayer < minPlayerSpawnDistance)
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

    // Точка у противоположного края арены — для боссов.
    public Vector3 GetArenaEdgeSpawnPosition()
    {
        Vector3 arenaCenter = GetArenaCenter();
        float minRadius =
            GetArenaRadius() * arenaEdgeMin;
        float maxRadius =
            GetArenaRadius() * arenaEdgeMax;

        for (int attempt = 0;
             attempt < spawnAttempts;
             attempt++)
        {
            float angle =
                Random.Range(0f, 360f);

            float radius =
                Random.Range(minRadius, maxRadius);

            float spawnHeight =
                player != null
                    ? player.position.y
                    : arenaCenter.y;

            Vector3 candidate =
                arenaCenter +
                new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    spawnHeight,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                );

            if (IsSpawnPositionFree(candidate))
                return candidate;
        }

        return arenaCenter + Vector3.forward * minRadius;
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

            if (collider.GetComponentInParent<Enemy>() != null)
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
                center +
                new Vector3(
                    randomOffset.x,
                    0f,
                    randomOffset.y
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

    private float GetArenaRadius()
    {
        if (worldGenerator != null)
            return worldGenerator.ArenaRadius;

        return 30f;
    }

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

        Vector3 spawnPosition;

        if (!TryGetFreePositionAround(
                position,
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
            enemy.Initialize(CurrentWave);

        return enemy;
    }

    // =========================================================
    // MID-WAVE EVENTS (Ambush / Rush)
    // =========================================================

    // Внезапная «осада» с близкой дистанции: кольцо быстрых врагов
    // вокруг игрока. Работает через общего спасвнера, чтобы враги
    // корректно инициализировались и уважали структуры.
    public void SpawnAmbush(
        int count,
        float minDistance,
        float maxDistance)
    {
        if (player == null)
            return;

        for (int i = 0; i < count; i++)
        {
            float angle =
                Random.Range(0f, 360f);

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

            if (!IsSpawnPositionFree(desired) &&
                !TryGetFreePositionAround(desired, out desired))
            {
                continue;
            }

            EnemyType type =
                CurrentWave >= 2
                    ? EnemyType.Fast
                    : EnemyType.Normal;

            SpawnEnemyAtPosition(type, desired);
        }
    }

    // Стенда с одного фланга: куча быстрых врагов из узкой дуги у края
    // арены. Игрок вынужден сместиться, а не стоять на месте.
    public void SpawnRushPack(
        int count,
        float arcDegrees,
        int wave)
    {
        if (player == null)
            return;

        Vector3 arenaCenter = GetArenaCenter();

        float minRadius =
            GetArenaRadius() * arenaEdgeMin;

        float maxRadius =
            GetArenaRadius() * arenaEdgeMax;

        float centerAngle =
            Random.Range(0f, 360f);

        float halfArc =
            Mathf.Clamp(arcDegrees, 10f, 180f) *
            0.5f;

        for (int i = 0; i < count; i++)
        {
            float angle =
                centerAngle +
                Random.Range(-halfArc, halfArc);

            float radius =
                Random.Range(minRadius, maxRadius);

            Vector3 candidate =
                arenaCenter +
                new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad) * radius,
                    player.position.y,
                    Mathf.Sin(angle * Mathf.Deg2Rad) * radius
                );

            if (!IsSpawnPositionFree(candidate) &&
                !TryGetFreePositionAround(candidate, out candidate))
            {
                continue;
            }

            EnemyType type =
                wave >= 2
                    ? EnemyType.Fast
                    : EnemyType.Normal;

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
    // Гарантированная элита заводится отдельно в SpawnWaveRoutine.
    private GameObject GetHuntPrefabForWave(
        int wave)
    {
        int totalWeight = 0;

        if (normalPrefab != null)
            totalWeight += 6;

        if (wave >= 2 && fastPrefab != null)
            totalWeight += 2;

        if (wave >= 3 && rangedPrefab != null)
            totalWeight += 2;

        if (wave >= 4 && tankPrefab != null)
            totalWeight += 1;

        if (wave >= 7 && elitePrefab != null)
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

        if (wave >= 7 && elitePrefab != null)
            return elitePrefab;

        return null;
    }
}