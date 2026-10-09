using System.Collections.Generic;
using UnityEngine;

// Лужа крови на земле.
//
// Раньше каждая лужа держала два MeshRenderer (центральное пятно и
// брызги), поэтому 10 луж на поле стоили 20 draw call'ов. Теперь
// геометрия лужи - это просто вершины в статическом массиве, которые
// собирает BloodPoolBatcher: все пятна идут одним мешем, все брызги -
// вторым. То есть вся кровь на карте - 2 draw call'а, сколько бы луж
// ни лежало.
//
// Здесь остаётся только жизненный цикл и рискование формы пятна:
// объект лужи берётся из пула, не уничтожается и хранит локальные
// (относительно точки смерти) вершины, а рендерит их батчер.
//
// Затухание при уходе в меню тоже общее: WorldFadeOutManager зовёт
// BloodPoolBatcher.FadeOut, и все лужи сжимаются разом.
public sealed class BloodPool : MonoBehaviour
{
    [Header("Main Pool")]
    [SerializeField] private float mainPoolSize = 1.2f;
    [SerializeField, Range(0f, 1f)] private float mainSizeRandomness = 0.2f;

    [Header("Splatters")]
    [SerializeField] private int splatterCount = 4;
    [SerializeField] private float minSplatterSize = 0.12f;
    [SerializeField] private float maxSplatterSize = 0.38f;
    [SerializeField] private float maxSplatterOffset = 1.1f;

    [Header("Lifetime")]
    [SerializeField] private float lifetime = 10f;

    // Больше 10 брызг не запрашивает ни один тип моба, но запас
    // оставлен на случай добавления нового.
    internal const int MaxSplatters = 12;

    // Больше 16 луж одновременно на карте не лежит: каждая живёт
    // lifetime секунд, а мобы за это время успевают закончиться.
    private const int MaxPooled = 16;

    private const float GroundY = 0.006f;

    // Все живые лужи в одном списке: их обслуживает один Update,
    // а геометрию собирает BloodPoolBatcher.
    private static readonly List<BloodPool> active =
        new List<BloodPool>(32);

    private static readonly Queue<BloodPool> pool =
        new Queue<BloodPool>(MaxPooled);

    private static BloodPoolTicker ticker;

    // Форма пятна в локальных координатах лужи. Трансформ ставится
    // в точку смерти, поэтому вершины можно держать локальными и не
    // пересобирать при переиспользовании объекта из пула.
    private readonly Vector3[] mainQuad = new Vector3[4];

    private readonly Vector3[] splatterQuads =
        new Vector3[MaxSplatters * 4];

    private int activeSplatters;
    private float remaining;

    // Геометрию читает BloodPoolBatcher.
    internal Vector3[] MainQuad => mainQuad;

    internal Vector3[] SplatterQuads => splatterQuads;

    internal int ActiveSplatters => activeSplatters;

    /// <summary>
    /// Все живые лужи. Батчер обходит этот список разом.
    /// </summary>
    internal static List<BloodPool> ActivePools => active;

    public static void SpawnAt(
        Vector3 position,
        float poolSize = 1.2f,
        int splatters = 4,
        float maxOffset = 1.1f)
    {
        BloodPool blood = Rent();

        if (blood == null)
            return;

        blood.Play(position, poolSize, splatters, maxOffset);
    }

    private static BloodPool Rent()
    {
        EnsureTicker();

        while (pool.Count > 0)
        {
            // Объект мог быть уничтожен сменой сцены.
            BloodPool candidate = pool.Dequeue();

            if (candidate != null)
            {
                candidate.transform.SetParent(null);
                candidate.transform.rotation = Quaternion.identity;
                candidate.gameObject.SetActive(true);

                if (!active.Contains(candidate))
                    active.Add(candidate);

                return candidate;
            }
        }

        GameObject created = new GameObject("BloodPool");

        BloodPool instance = created.AddComponent<BloodPool>();

        active.Add(instance);

        return instance;
    }

    private void Play(
        Vector3 position,
        float poolSize,
        int splatters,
        float maxOffset)
    {
        if (poolSize > 0f)
            mainPoolSize = poolSize;

        if (splatters >= 0)
            splatterCount = splatters;

        if (maxOffset > 0f)
            maxSplatterOffset = maxOffset;

        remaining = lifetime;

        transform.localPosition = new Vector3(
            position.x,
            GroundY,
            position.z
        );

        transform.localScale = Vector3.one;

        WriteMainQuad();
        WriteSplatterQuads();

        // Форма изменилась - батчер пересоберёт общий меш.
        BloodPoolBatcher.NotifyChanged();
    }

    // Пятно рисуется в плоскости земли: четыре вершины квадрата
    // лежат в XZ, нормаль смотрит вверх.
    private void WriteMainQuad()
    {
        float size =
            mainPoolSize *
            (1f - Random.value * mainSizeRandomness);

        Vector3 center = new Vector3(
            Random.Range(-0.15f, 0.15f),
            0f,
            Random.Range(-0.15f, 0.15f)
        );

        WriteQuad(
            mainQuad,
            0,
            center,
            size,
            Random.Range(0f, 360f)
        );
    }

    private void WriteSplatterQuads()
    {
        activeSplatters = Mathf.Clamp(splatterCount, 0, MaxSplatters);

        for (int i = 0; i < activeSplatters; i++)
        {
            float size =
                Random.Range(minSplatterSize, maxSplatterSize);

            Vector2 offset =
                Random.insideUnitCircle * maxSplatterOffset;

            WriteQuad(
                splatterQuads,
                i * 4,
                new Vector3(offset.x, 0f, offset.y),
                size,
                Random.Range(0f, 360f)
            );
        }
    }

    private static void WriteQuad(
        Vector3[] vertices,
        int vertexBase,
        Vector3 center,
        float size,
        float yawDegrees)
    {
        float half = size * 0.5f;
        float yaw = yawDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(yaw);
        float sin = Mathf.Sin(yaw);

        // Поворот квадрата в плоскости земли: тот же Euler(90, yaw, 0),
        // что и раньше на кваде, только посчитанный сразу в вершины.
        WriteCorner(vertices, vertexBase + 0, center, -half, -half, cos, sin);
        WriteCorner(vertices, vertexBase + 1, center, half, -half, cos, sin);
        WriteCorner(vertices, vertexBase + 2, center, half, half, cos, sin);
        WriteCorner(vertices, vertexBase + 3, center, -half, half, cos, sin);
    }

    private static void WriteCorner(
        Vector3[] vertices,
        int index,
        Vector3 center,
        float x,
        float z,
        float cos,
        float sin)
    {
        vertices[index] = new Vector3(
            center.x + x * cos - z * sin,
            0f,
            center.z + x * sin + z * cos
        );
    }

    private static void EnsureTicker()
    {
        if (ticker != null)
            return;

        GameObject host = new GameObject("BloodPoolTicker");

        Object.DontDestroyOnLoad(host);

        ticker = host.AddComponent<BloodPoolTicker>();
    }

    // Обслуживает срок жизни всех луж разом.
    private sealed class BloodPoolTicker : MonoBehaviour
    {
        private void Update()
        {
            float delta = Time.deltaTime;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                BloodPool blood = active[i];

                if (blood == null)
                {
                    active.RemoveAt(i);
                    continue;
                }

                blood.remaining -= delta;

                if (blood.remaining > 0f)
                    continue;

                blood.gameObject.SetActive(false);

                if (pool.Count < MaxPooled)
                    pool.Enqueue(blood);

                active.RemoveAt(i);

                BloodPoolBatcher.NotifyChanged();
            }

            // Тикер нужен только пока есть живые лужи. Когда их не
            // осталось, объект снимает себя, чтобы не болтаться в сцене
            // пустым GameObject между волнами.
            if (active.Count == 0)
                Destroy(gameObject);
        }
    }
}
