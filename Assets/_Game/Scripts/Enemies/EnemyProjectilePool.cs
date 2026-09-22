using System.Collections.Generic;
using UnityEngine;

// Пул снарядов врагов: снаряд при «смерти» не уничтожается,
// а прячется и переиспользуется. Трейл (LineRenderer) строится
// один раз за жизнь слота пула, а не на каждый залп.
public static class EnemyProjectilePool
{
    private static readonly Dictionary<int, Queue<EnemyProjectile>> Pools =
        new Dictionary<int, Queue<EnemyProjectile>>();

    public static EnemyProjectile Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
            return null;

        int key = prefab.GetInstanceID();

        Queue<EnemyProjectile> queue;

        if (!Pools.TryGetValue(key, out queue))
        {
            queue = new Queue<EnemyProjectile>();
            Pools[key] = queue;
        }

        EnemyProjectile projectile = null;

        while (queue.Count > 0)
        {
            // Объект мог быть уничтожен сменой сцены — выкидываем битые ссылки.
            EnemyProjectile candidate = queue.Dequeue();

            if (candidate != null &&
                candidate.gameObject != null)
            {
                projectile = candidate;
                break;
            }
        }

        if (projectile == null)
        {
            GameObject newObject =
                Object.Instantiate(prefab, position, rotation);

            if (newObject == null)
                return null;

            projectile = newObject.GetComponent<EnemyProjectile>();

            if (projectile == null)
                return null;

            projectile.PoolKey = key;
        }
        else
        {
            projectile.transform.SetParent(null);
            projectile.transform.position = position;
            projectile.transform.rotation = rotation;
            projectile.gameObject.SetActive(true);
        }

        return projectile;
    }

    public static void Despawn(EnemyProjectile projectile)
    {
        if (projectile == null)
            return;

        GameObject projectileObject =
            projectile.gameObject;

        if (projectileObject == null)
            return;

        projectileObject.SetActive(false);

        int key = projectile.PoolKey;

        if (key == 0)
        {
            Object.Destroy(projectileObject);
            return;
        }

        Queue<EnemyProjectile> queue;

        if (!Pools.TryGetValue(key, out queue))
        {
            Object.Destroy(projectileObject);
            return;
        }

        queue.Enqueue(projectile);
    }
}