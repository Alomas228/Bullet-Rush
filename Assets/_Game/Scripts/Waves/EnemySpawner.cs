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

    // Пока true — волна считается «идущей»: очередь спавна ещё не исчерпана.
    // Нужно менеджеру волн, чтобы не завершать волну, пока враги только едут.
    public bool IsSpawning { get; private set; }

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
        int wave)
    {
        StopSpawnQueue();
        spawnedPositions.Clear();

        IsSpawning = enemyCount > 0;
        spawnQueueCoroutine = StartCoroutine(
            SpawnWaveRoutine(enemyCount, wave)
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
    private IEnumerator SpawnWaveRoutine(
        int enemyCount,
        int wave)
    {
        int remaining = Mathf.Max(enemyCount, 0);
        bool firstBatch = true;

        // Давление волны растёт с её номером: порции крупнее,
        // паузы между ними короче.
        int batchSizeGrowth =
            Mathf.FloorToInt(
                Mathf.Max(wave - 1, 0) / 8f
            );

        int effectiveBatchSize =
            Mathf.Max(spawnBatchSize, 1) +
            batchSizeGrowth;

        const int maxScaledBatchSize = 12;

        effectiveBatchSize =
            Mathf.Min(
                effectiveBatchSize,
                Mathf.Max(maxScaledBatchSize, spawnBatchSize)
            );

        float effectiveInterval =
            spawnBatchInterval *
            Mathf.Pow(0.95f, Mathf.Max(wave - 1, 0));

        effectiveInterval =
            Mathf.Max(effectiveInterval, 0.45f);

        while (remaining > 0)
        {
            int batchSize = firstBatch
                ? Mathf.Min(firstBatchSize, remaining)
                : Mathf.Min(effectiveBatchSize, remaining);
            firstBatch = false;

            batchAngles.Clear();

            for (int i = 0; i < batchSize; i++)
            {
                SpawnEnemy(wave);
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

    private void SpawnEnemy(int wave)
    {
        if (player == null)
        {
            Debug.LogWarning(
                "EnemySpawner: Player is not assigned."
            );

            return;
        }

        GameObject prefab =
            GetEnemyPrefabForWave(wave);

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

    public void SpawnEnemyAtPosition(
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

            return;
        }

        Vector3 spawnPosition;

        if (!TryGetFreePositionAround(
                position,
                out spawnPosition))
        {
            Debug.LogWarning(
                $"EnemySpawner: Could not find a free position for {enemyType}."
            );

            return;
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
        // Weighted selection without allocating a temporary List on every spawn.
        // The old implementation rebuilt a 17-item list for every enemy.
        int totalWeight = 0;

        if (normalPrefab != null)
            totalWeight += 10;

        if (wave >= 2 && fastPrefab != null)
            totalWeight += 3;

        if (wave >= 3 && rangedPrefab != null)
            totalWeight += 2;

        if (wave >= 4 && tankPrefab != null)
            totalWeight += 1 + (wave >= 16 ? 1 : 0);

        if (wave >= 7 && elitePrefab != null)
            totalWeight += 1 + Mathf.FloorToInt((wave - 7) / 5f);

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
            roll -= 1 + (wave >= 16 ? 1 : 0);
            if (roll < 0)
                return tankPrefab;
        }

        if (wave >= 7 && elitePrefab != null)
            return elitePrefab;

        return null;
    }
}