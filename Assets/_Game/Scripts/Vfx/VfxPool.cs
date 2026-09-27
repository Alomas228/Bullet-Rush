using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Пул VFX-эффектов. Убирает Instantiate/Destroy на каждом
/// выстреле и на каждом попадании (на WebGL это главный
/// источник GC-пауз).
///
/// Ключ пула - EntityId источника: заданного префаба
/// или внутреннего шаблона эффекта. Поэтому эффекты можно
/// пустить и без префаба вообще: пул сам создаст шаблон один
/// раз и дальше будет его переиспользовать.
///
/// Прогрев (PrewarmCount) снимает главный риск - первый выстрел
/// в бою не должен ни аллоцировать, ни компилировать шейдер.
/// </summary>
public class VfxPool<T> where T : VfxEffect
{
    private class Bucket
    {
        public readonly Queue<T> queue = new Queue<T>();
        public bool warmed;
    }

    private readonly Func<GameObject> templateFactory;
    private readonly Dictionary<EntityId, Bucket> buckets =
        new Dictionary<EntityId, Bucket>();

    // Добирать пул по одному объекту - значит делать Instantiate
    // на каждом выстреле, ровно то, чего пул и должен избегать.
    private const int RefillBatch = 8;

    // Источники, у которых нет нашего компонента. Проверяются
    // один раз, дальше просто пропускаются.
    private readonly HashSet<EntityId> invalidSources =
        new HashSet<EntityId>();

    private GameObject cachedTemplate;

    public VfxPool(Func<GameObject> templateFactory)
    {
        this.templateFactory = templateFactory;
    }

    /// <summary>
    /// Сколько объектов заготовить заранее на один ключ пула.
    /// Трассер живёт до конца жизни пули, поэтому считается от
    /// темпа стрельбы: fireRate * lifetime.
    /// </summary>
    public int PrewarmCount = 64;

    private GameObject Template
    {
        get
        {
            if (cachedTemplate == null)
                cachedTemplate = templateFactory();

            return cachedTemplate;
        }
    }

    /// <summary>
    /// Достаёт объект из пула (или создаёт) и ставит его в мир.
    /// Запуск эффекта делает сам эффект через Play() - так
    /// длительность живёт в одном месте, рядом с визуалом.
    /// </summary>
    public T Spawn(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation)
    {
        GameObject source = ResolveSource(prefab);

        if (source == null)
            return null;

        EntityId key = source.GetEntityId();

        Bucket bucket = GetBucket(key);

        if (!bucket.warmed)
        {
            bucket.warmed = true;
            Prewarm(source, key, bucket, PrewarmCount);
        }

        T effect = Take(bucket);

        if (effect == null)
            effect = Refill(source, key, bucket);

        if (effect == null)
            return null;

        Place(effect, position, rotation);
        return effect;
    }

    /// <summary>
    /// Очередь пуста - добираем пачкой, а не по одному.
    /// Иначе пул, переросший прогрев, рос бы Instantiate'ом на
    /// каждом выстреле, и эффект вернул бы себе ту аллокацию,
    /// ради которой он и выделен в пул.
    /// </summary>
    private T Refill(
        GameObject source,
        EntityId key,
        Bucket bucket)
    {
        T first = Create(source, key);

        if (first == null)
            return null;

        for (int i = 1; i < RefillBatch; i++)
        {
            T extra = Create(source, key);

            if (extra == null)
                break;

            extra.gameObject.SetActive(false);
            bucket.queue.Enqueue(extra);
        }

        return first;
    }

    /// <summary>
    /// Источник, который реально можно клонировать.
    ///
    /// Префаб без нашего компонента клонировать нельзя:
    /// Instantiate такого источника сыпет "The referenced script
    /// on this Behaviour is missing" на каждом выстреле. Проверка
    /// стоит один раз на источник, дальше он помечен битым и
    /// молча заменяется процедурным шаблоном.
    /// </summary>
    private GameObject ResolveSource(GameObject prefab)
    {
        if (prefab == null)
            return Template;

        EntityId key = prefab.GetEntityId();

        if (invalidSources.Contains(key))
            return Template;

        if (prefab.GetComponent<T>() == null)
        {
            invalidSources.Add(key);
            return Template;
        }

        return prefab;
    }

    public void Despawn(T effect)
    {
        if (effect == null)
            return;

        GameObject effectObject = effect.gameObject;

        if (effectObject == null)
            return;

        // Если эффект ещё играет, Stop() сам снимет его с тикера
        // и вернёт в пул. Продолжать нельзя: объект уже в очереди,
        // иначе он попадёт туда дважды.
        if (effect.IsPlaying)
        {
            effect.Stop();
            return;
        }

        effectObject.SetActive(false);

        EntityId? key = effect.PoolKey;

        // PoolKey == null - объект создан вручную и пулу не
        // принадлежит.
        if (!key.HasValue)
        {
            DestroyObject(effectObject);
            return;
        }

        Bucket bucket;

        if (!buckets.TryGetValue(key.Value, out bucket))
        {
            DestroyObject(effectObject);
            return;
        }

        bucket.queue.Enqueue(effect);
    }

    /// <summary>
    /// Полная очистка пула. Объекты не просто забываются, а
    /// уничтожаются: они живут в DontDestroyOnLoad, и если
    /// просто выкинуть ссылки, они останутся в сцене навсегда
    /// невидимыми сиротами (HideFlags их не прячет от GC).
    /// </summary>
    public void Clear()
    {
        foreach (Bucket bucket in buckets.Values)
        {
            while (bucket.queue.Count > 0)
            {
                T effect = bucket.queue.Dequeue();

                if (effect == null ||
                    effect.gameObject == null)
                {
                    continue;
                }

                DestroyObject(effect.gameObject);
            }
        }

        buckets.Clear();
        invalidSources.Clear();
    }

    private static void DestroyObject(GameObject target)
    {
        // В редакторе вне play mode Destroy ругается и не
        // уничтожает - нужен DestroyImmediate.
        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(target);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(target);
        }
    }

    private Bucket GetBucket(EntityId key)
    {
        Bucket bucket;

        if (!buckets.TryGetValue(key, out bucket))
        {
            bucket = new Bucket();
            buckets[key] = bucket;
        }

        return bucket;
    }

    private T Take(Bucket bucket)
    {
        while (bucket.queue.Count > 0)
        {
            // Объект мог быть уничтожен сменой сцены -
            // выкидываем битые ссылки.
            T candidate = bucket.queue.Dequeue();

            if (candidate != null &&
                candidate.gameObject != null)
            {
                return candidate;
            }
        }

        return null;
    }

    private T Create(GameObject source, EntityId key)
    {
        GameObject instance =
            UnityEngine.Object.Instantiate(source);

        T effect = instance.GetComponent<T>();

        if (effect == null)
        {
            UnityEngine.Object.Destroy(instance);
            return null;
        }

        // Пул переживает смену сцены: без этого объекты из пула
        // умирали бы вместе со сценой, и пул на каждом входе в бой
        // прогревался бы заново в кадре первого выстрела.
        // Важно сделать это ДО установки позиции.
        UnityEngine.Object.DontDestroyOnLoad(instance);

        // Сотни выключенных "Tracer" в иерархии - мусор. Их нельзя
        // выбрать в инспекторе и незачем видеть: место в пуле
        // видно по счётчику VfxUpdater.ActiveCount.
        instance.hideFlags = HideFlags.HideInHierarchy;

        effect.PoolKey = key;
        return effect;
    }

    private static void Place(
        T effect,
        Vector3 position,
        Quaternion rotation)
    {
        Transform effectTransform = effect.transform;

        effectTransform.SetParent(null, true);
        effectTransform.SetPositionAndRotation(
            position,
            rotation
        );

        effect.gameObject.SetActive(true);
    }

    private void Prewarm(
        GameObject source,
        EntityId key,
        Bucket bucket,
        int count)
    {
        for (int i = 0; i < count; i++)
        {
            T effect = Create(source, key);

            if (effect == null)
                return;

            // Один раз активируем: так отрабатывает Awake и
            // компилируется вариант шейдера. Потом гасим.
            Place(
                effect,
                Vector3.zero,
                Quaternion.identity
            );

            effect.gameObject.SetActive(false);

            bucket.queue.Enqueue(effect);
        }
    }
}

/// <summary>
/// Реестр пулов VFX. Три пула на игру, новых в рантайме не появляется.
/// </summary>
public static class VfxPools
{
    public static readonly VfxPool<TracerEffect> Tracers =
        new VfxPool<TracerEffect>(
            TracerEffect.CreateTemplate);

    public static readonly VfxPool<TracerEffect> EnemyTracers =
        new VfxPool<TracerEffect>(
            TracerEffect.CreateEnemyTemplate);

    public static readonly VfxPool<ImpactEffect> Impacts =
        new VfxPool<ImpactEffect>(
            ImpactEffect.CreateTemplate);

    public static readonly VfxPool<BloodEffect> BloodBursts =
        new VfxPool<BloodEffect>(
            BloodEffect.CreateTemplate);

    static VfxPools()
    {
        // Трассер живёт всю жизнь пули (lifetime в Bullet = 3 с),
        // а у минигана fireRate = 28 выстрелов в секунду. Значит
        // одновременно в воздухе до 28 * 3 = 84 пуль, и трассеров
        // столько же. 160 - с запасом на дробовик и на то, что
        // пули не сразу умирают о стены.
        Tracers.PrewarmCount = 160;

        // Снарядов врага на порядок меньше: веер дальника даёт 3,
        // взрыв танка при смерти - 8, стрелков на экране единицы.
        // 32 - с запасом на несколько таких залпов сразу.
        EnemyTracers.PrewarmCount = 32;

        // Попадание = объект с 5 рендерерами (вспышка, 3 осколка
        // и один запасной), поэтому запас меньше.
        Impacts.PrewarmCount = 16;

        // Капля крови живёт до 0.7 с, бюджет на кадр у SpawnBlood
        // - 6 брызг, то есть теоретический потолок выше, чем у
        // вспышек. На практике столько попаданий в кадр не
        // бывает, а 32 переживают и залп дробовика в упор.
        BloodBursts.PrewarmCount = 32;
    }

    public static void ClearAll()
    {
        Tracers.Clear();
        EnemyTracers.Clear();
        Impacts.Clear();
        BloodBursts.Clear();
    }
}
