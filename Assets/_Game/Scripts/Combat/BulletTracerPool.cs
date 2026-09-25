using System.Collections.Generic;
using UnityEngine;

// Пул трассеров: трассер после отрисовки не уничтожается,
// а прячется и переиспользуется. Убирает Instantiate/Destroy
// на каждый выстрел вместе с пулями.
public static class BulletTracerPool
{
    private static readonly Dictionary<int, Queue<BulletTracer>> Pools =
        new Dictionary<int, Queue<BulletTracer>>();

    public static BulletTracer Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        if (prefab == null)
            return null;

        int key = prefab.GetInstanceID();

        Queue<BulletTracer> queue;

        if (!Pools.TryGetValue(key, out queue))
        {
            queue = new Queue<BulletTracer>();
            Pools[key] = queue;
        }

        BulletTracer tracer = null;

        while (queue.Count > 0)
        {
            // Объект мог быть уничтожен сменой сцены — выкидываем битые ссылки.
            BulletTracer candidate = queue.Dequeue();

            if (candidate != null &&
                candidate.gameObject != null)
            {
                tracer = candidate;
                break;
            }
        }

        if (tracer == null)
        {
            GameObject newObject =
                Object.Instantiate(prefab, position, rotation);

            if (newObject == null)
                return null;

            tracer = newObject.GetComponent<BulletTracer>();

            if (tracer == null)
                return null;

            tracer.PoolKey = key;
        }
        else
        {
            tracer.transform.SetParent(null);
            tracer.transform.position = position;
            tracer.transform.rotation = rotation;
            tracer.gameObject.SetActive(true);
        }

        return tracer;
    }

    public static void Despawn(BulletTracer tracer)
    {
        if (tracer == null)
            return;

        GameObject tracerObject =
            tracer.gameObject;

        if (tracerObject == null)
            return;

        tracerObject.SetActive(false);

        int key = tracer.PoolKey;

        if (key == 0)
        {
            Object.Destroy(tracerObject);
            return;
        }

        Queue<BulletTracer> queue;

        if (!Pools.TryGetValue(key, out queue))
        {
            Object.Destroy(tracerObject);
            return;
        }

        queue.Enqueue(tracer);
    }
}