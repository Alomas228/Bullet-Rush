using UnityEngine;

/// <summary>
/// Попадание пули: короткая яркая вспышка + несколько мелких
/// светящихся осколков.
///
/// Почему не Particle System:
///   Particle System на каждое попадание = свой эмиттер, своя
///   система частиц в сцене, сортировка, свой набор шейдерных
///   вариантов. Для эффекта длиной 0.2 с это заметно дороже,
///   чем несколько квадов, которые уже лежат в пуле.
///
///   Здесь на попадание приходится 1 draw call вспышки и до
///   3 draw call осколков, без сортировки (аддитивное смешивание
///   не зависит от порядка) и без единой аллокации в рантайме.
///
/// Всё держится на двух материалах на весь забег:
/// VfxSharedAssets.ImpactMaterial. Вспышка и осколки - это
/// MeshRenderer на квадах без нормалей, теней и пробросов света.
///
/// Затухание сделано через localScale, а не через цвет: это
/// одна запись в transform вместо пересборки меша или
/// MaterialPropertyBlock (тот ломает SRP Batcher).
/// </summary>
public sealed class ImpactEffect : VfxEffect
{
    // =========================================================
    // РЕКОМЕНДУЕМЫЕ ЗНАЧЕНИЯ
    //
    // Размеры заданы от высоты экрана. Игровая камера стоит
    // в ~22.5 мировых единицах от плоскости боя при FOV 60,
    // то есть видит ~26 единиц по вертикали: на 1080p это
    // ~42 пикселя на единицу.
    //
    //   вспышка 0.6  -> ~2.3% высоты экрана (~25 пикселей)
    //   осколок 0.16 -> ~0.6% высоты экрана (~7 пикселей)
    //
    // Если камеру поднимут или уменьшишь FOV - пересчитай:
    //   size = доля * (2 * distance * tan(FOV/2))
    // =========================================================

    /// <summary>
    /// Диаметр вспышки. Больше 0.7 при такой камере уже читается
    /// как пятно, а не как удар.
    /// </summary>
    public const float DefaultFlashSize = 0.6f;

    /// <summary>
    /// Время жизни вспышки. Короче 0.08 - не видно, длиннее
    /// 0.15 - уже "лампочка", а не вспышка попадания.
    /// </summary>
    public const float DefaultFlashLifetime = 0.1f;

    public const int DefaultShardCount = 3;

    public const float DefaultShardSize = 0.16f;
    public const float DefaultShardLifetimeMin = 0.13f;
    public const float DefaultShardLifetimeMax = 0.24f;
    public const float DefaultShardSpeedMin = 4f;
    public const float DefaultShardSpeedMax = 8.5f;
    public const float DefaultShardGravity = 14f;

    private const int MaxShards = 4;

    [Header("Flash")]
    [SerializeField, Tooltip("Диаметр вспышки в мировых единицах")]
    private float flashSize = DefaultFlashSize;

    [SerializeField, Tooltip("Время жизни вспышки в секундах")]
    private float flashLifetime = DefaultFlashLifetime;

    [Header("Shards")]
    [SerializeField, Tooltip("Сколько осколков на попадание (0 = только вспышка)")]
    private int shardCount = DefaultShardCount;

    [SerializeField, Tooltip("Базовый размер осколка")]
    private float shardSize = DefaultShardSize;

    [SerializeField, Tooltip("Минимальная скорость осколка")]
    private float shardSpeedMin = DefaultShardSpeedMin;

    [SerializeField, Tooltip("Максимальная скорость осколка")]
    private float shardSpeedMax = DefaultShardSpeedMax;

    [SerializeField, Tooltip("Гравитация осколков")]
    private float shardGravity = DefaultShardGravity;

    [Header("Lifetime")]
    [SerializeField, Tooltip("Минимальное время жизни осколка")]
    private float shardLifetimeMin = DefaultShardLifetimeMin;

    [SerializeField, Tooltip("Максимальное время жизни осколка")]
    private float shardLifetimeMax = DefaultShardLifetimeMax;

    private Transform flashTransform;

    private Transform[] shards;
    private Vector3[] shardVelocity;
    private float[] shardTimeLeft;
    private float[] shardDuration;
    private float[] shardBaseSize;
    private int activeShards;

    /// <summary>
    /// Шаблон для пула, когда префаб не задан. Создаётся один раз.
    /// </summary>
    public static GameObject CreateTemplate()
    {
        GameObject template = new GameObject("ImpactTemplate");

        template.SetActive(false);
        template.AddComponent<ImpactEffect>();

        return template;
    }

    private void Awake()
    {
        gameObject.name = "Impact";

        BuildFlash();
        BuildShards();
    }

    private void BuildFlash()
    {
        // Вспышка - отдельный дочерний объект: масштабировать
        // самого эффекта нельзя, иначе масштаб уедет и осколкам
        // (они тоже дети).
        GameObject flashObject = new GameObject("Flash");

        flashObject.transform.SetParent(transform, false);

        MeshRenderer flashRenderer =
            flashObject.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupAdditiveRenderer(
            flashRenderer,
            VfxSharedAssets.CenteredMesh,
            VfxSharedAssets.ImpactMaterial
        );

        flashTransform = flashObject.transform;
    }

    private void BuildShards()
    {
        shards = new Transform[MaxShards];
        shardVelocity = new Vector3[MaxShards];
        shardTimeLeft = new float[MaxShards];
        shardDuration = new float[MaxShards];
        shardBaseSize = new float[MaxShards];

        for (int i = 0; i < MaxShards; i++)
        {
            GameObject shardObject = new GameObject("Shard");

            shardObject.transform.SetParent(transform, false);

            MeshRenderer shardRenderer =
                shardObject.AddComponent<MeshRenderer>();

            VfxSharedAssets.SetupAdditiveRenderer(
                shardRenderer,
                VfxSharedAssets.CenteredMesh,
                VfxSharedAssets.ImpactMaterial
            );

            shardObject.SetActive(false);
            shards[i] = shardObject.transform;
        }
    }

    /// <summary>
    /// Запуск эффекта попадания.
    /// </summary>
    /// <param name="position">Точка попадания.</param>
    /// <param name="normal">Направление полёта пули: осколки
    /// разлетаются вперёд по нему.</param>
    public void Play(Vector3 position, Vector3 normal)
    {
        transform.position = position;

        // Вспышка всегда развёрнута лицом к камере.
        transform.rotation =
            VfxSharedAssets.FaceDirection(normal);

        // Масштаб вспышки задаём сразу: первый кадр эффекта
        // не должен показывать квад размером во всю арену.
        flashTransform.localScale = new Vector3(
            flashSize,
            flashSize,
            1f
        );

        SpawnShards(position, normal);

        BeginPlay(flashLifetime);
    }

    private void SpawnShards(
        Vector3 position,
        Vector3 normal)
    {
        activeShards = Mathf.Clamp(shardCount, 0, MaxShards);

        for (int i = 0; i < activeShards; i++)
        {
            Vector2 circle = Random.insideUnitCircle;

            // Осколки летят по направлению полёта, разлетаясь
            // в стороны.
            Vector3 direction =
                Vector3.Slerp(
                    normal,
                    new Vector3(circle.x, 0f, circle.y),
                    0.85f
                );

            if (direction.sqrMagnitude < 0.0001f)
                direction = normal;

            float speed = Random.Range(
                shardSpeedMin,
                shardSpeedMax
            );

            shardVelocity[i] = direction * speed;
            shardDuration[i] = Random.Range(
                shardLifetimeMin,
                shardLifetimeMax
            );
            shardTimeLeft[i] = shardDuration[i];

            float size = shardSize * Random.Range(0.7f, 1.3f);

            shardBaseSize[i] = size;

            Transform shard = shards[i];

            shard.position =
                position + normal * (size * 0.5f);

            // Поворот не считаем: осколок наследует билборд
            // родителя и остаётся в плоскости камеры.
            shard.localRotation = Quaternion.identity;

            shard.localScale = new Vector3(size, size, 1f);

            if (!shard.gameObject.activeSelf)
                shard.gameObject.SetActive(true);
        }

        // Неиспользуемые осколки гасим, иначе они останутся
        // висеть на экране от прошлого попадания.
        for (int i = activeShards; i < MaxShards; i++)
        {
            if (shards[i].gameObject.activeSelf)
                shards[i].gameObject.SetActive(false);
        }
    }

    protected override void Tick(float deltaTime)
    {
        TimeLeft -= deltaTime;

        // Вспышка гаснет квадратично: полный размер в первый
        // кадр, схлопывание в ноль к концу жизни.
        float flashLife =
            Mathf.Clamp01(TimeLeft / Duration);

        float flashScale =
            flashSize * flashLife * flashLife;

        if (flashScale > 0.0001f)
        {
            flashTransform.localScale = new Vector3(
                flashScale,
                flashScale,
                1f
            );
        }

        float gravity = shardGravity * deltaTime;

        for (int i = 0; i < activeShards; i++)
        {
            shardTimeLeft[i] -= deltaTime;

            float life = shardTimeLeft[i] / shardDuration[i];

            if (life <= 0f)
            {
                shards[i].localScale = Vector3.zero;

                if (shards[i].gameObject.activeSelf)
                    shards[i].gameObject.SetActive(false);

                continue;
            }

            Vector3 velocity = shardVelocity[i];

            velocity.y -= gravity;
            shardVelocity[i] = velocity;

            Transform shard = shards[i];

            shard.position += velocity * deltaTime;

            float scale = shardBaseSize[i] * life;

            shard.localScale = new Vector3(
                scale,
                scale,
                1f
            );
        }

        if (TimeLeft <= 0f)
            Finish();
    }

    protected override void ReturnToPool()
    {
        if (shards != null)
        {
            for (int i = 0; i < MaxShards; i++)
            {
                if (shards[i] != null &&
                    shards[i].gameObject.activeSelf)
                {
                    shards[i].gameObject.SetActive(false);
                }
            }
        }

        activeShards = 0;
        VfxPools.Impacts.Despawn(this);
    }
}
