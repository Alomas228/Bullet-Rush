using System.Collections.Generic;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform player;

    [Header("Enemy Prefabs")]
    [SerializeField] private GameObject normalPrefab;
    [SerializeField] private GameObject fastPrefab;
    [SerializeField] private GameObject tankPrefab;
    [SerializeField] private GameObject rangedPrefab;
    [SerializeField] private GameObject elitePrefab;
    [SerializeField] private GameObject bossPrefab;

    [Header("Spawn Settings")]
    [SerializeField] private float spawnDistance = 10f;
    [SerializeField] private float minimumSpawnDistance = 1.5f;
    [SerializeField] private int spawnAttempts = 20;

    public int CurrentWave { get; set; } = 1;

    public void SpawnWave(
        int enemyCount,
        int wave)
    {
        if (player == null)
        {
            Debug.LogWarning(
                "EnemySpawner: Player is not assigned."
            );

            return;
        }

        for (int i = 0; i < enemyCount; i++)
        {
            SpawnEnemy(wave);
        }
    }

    private void SpawnEnemy(int wave)
    {
        GameObject prefab =
            GetEnemyPrefabForWave(wave);

        if (prefab == null)
        {
            Debug.LogWarning(
                $"EnemySpawner: No enemy prefab available for wave {wave}."
            );

            return;
        }

        Vector3 spawnPosition;

        if (!TryGetSpawnPosition(out spawnPosition))
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

        Enemy enemy = enemyObject.GetComponent<Enemy>();

        if (enemy != null)
            enemy.Initialize(wave);
    }

    private bool TryGetSpawnPosition(
        out Vector3 spawnPosition)
    {
        for (int attempt = 0;
             attempt < spawnAttempts;
             attempt++)
        {
            Vector2 randomDirection =
                Random.insideUnitCircle.normalized;

            spawnPosition =
                player.position +
                new Vector3(
                    randomDirection.x,
                    0f,
                    randomDirection.y
                ) *
                spawnDistance;

            if (IsSpawnPositionFree(spawnPosition))
            {
                return true;
            }
        }

        spawnPosition = Vector3.zero;
        return false;
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

        return true;
    }

    private GameObject GetEnemyPrefabForWave(
        int wave)
    {
        List<GameObject> pool =
            new List<GameObject>();

        // NORMAL
        if (normalPrefab != null)
        {
            AddMultiple(
                pool,
                normalPrefab,
                10
            );
        }

        // FAST - Wave 2+
        if (wave >= 2 &&
            fastPrefab != null)
        {
            AddMultiple(
                pool,
                fastPrefab,
                3
            );
        }

        // RANGED - Wave 3+
        if (wave >= 3 &&
            rangedPrefab != null)
        {
            AddMultiple(
                pool,
                rangedPrefab,
                2
            );
        }

        // TANK - Wave 4+
        if (wave >= 4 &&
            tankPrefab != null)
        {
            AddMultiple(
                pool,
                tankPrefab,
                1
            );
        }

        // ELITE - Wave 7+
        if (wave >= 7 &&
            elitePrefab != null)
        {
            AddMultiple(
                pool,
                elitePrefab,
                1
            );
        }

        if (pool.Count == 0)
            return null;

        int randomIndex =
            Random.Range(
                0,
                pool.Count
            );

        return pool[randomIndex];
    }

    private void AddMultiple(
        List<GameObject> pool,
        GameObject prefab,
        int amount)
    {
        for (int i = 0; i < amount; i++)
        {
            pool.Add(prefab);
        }
    }

    public void SpawnEnemyAtPosition(
        EnemyType enemyType,
        Vector3 position)
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
}