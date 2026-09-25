using System.Collections.Generic;
using UnityEngine;

// Пул пуль игрока: пуля при окончании жизни, попадании в структуру
// или исчерпании пробития не уничтожается, а прячется и переиспользуется.
// Убирает Instantiate/Destroy на каждый выстрел и рикошет (GC на WebGL).
public static class BulletPool
{
    private static readonly Dictionary<int, Queue<Bullet>> Pools =
        new Dictionary<int, Queue<Bullet>>();

    public static Bullet Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
            return null;

        int key = prefab.GetInstanceID();

        Queue<Bullet> queue;

        if (!Pools.TryGetValue(key, out queue))
        {
            queue = new Queue<Bullet>();
            Pools[key] = queue;
        }

        Bullet bullet = null;

        while (queue.Count > 0)
        {
            // Объект мог быть уничтожен сменой сцены — выкидываем битые ссылки.
            Bullet candidate = queue.Dequeue();

            if (candidate != null &&
                candidate.gameObject != null)
            {
                bullet = candidate;
                break;
            }
        }

        if (bullet == null)
        {
            GameObject newObject =
                Object.Instantiate(prefab, position, rotation);

            if (newObject == null)
                return null;

            bullet = newObject.GetComponent<Bullet>();

            if (bullet == null)
                return null;

            bullet.PoolKey = key;
        }
        else
        {
            bullet.transform.SetParent(null);
            bullet.transform.position = position;
            bullet.transform.rotation = rotation;
            bullet.gameObject.SetActive(true);
        }

        return bullet;
    }

    public static void Despawn(Bullet bullet)
    {
        if (bullet == null)
            return;

        GameObject bulletObject =
            bullet.gameObject;

        if (bulletObject == null)
            return;

        bulletObject.SetActive(false);

        int key = bullet.PoolKey;

        if (key == 0)
        {
            Object.Destroy(bulletObject);
            return;
        }

        Queue<Bullet> queue;

        if (!Pools.TryGetValue(key, out queue))
        {
            Object.Destroy(bulletObject);
            return;
        }

        queue.Enqueue(bullet);
    }
}