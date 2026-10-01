using UnityEngine;

/// <summary>
/// Попадание пули: короткая яркая вспышка + несколько мелких
/// светящихся осколков.
///
/// Почему не Particle System:
///   Particle System на каждое попадание = свой эмиттер, своя
///   система частиц в сцене, сортировка, свой набор шейдерных
///   вариантов. Для эффекта длиной 0.2 с это заметно дороже,
///   чем квады, которые уже лежат в пуле.
///
/// Стоимость в draw call'ах - главный аргумент этой реализации.
/// Вспышка и осколки собраны в ОДИН меш на объекте, поэтому всё
/// попадание стоит 1 draw call. Раньше вспышка была отдельным
/// MeshRenderer и каждый осколок - тоже отдельным, то есть до
/// 5 draw call на одно попадание; при 8 попаданиях за кадр
/// (пулемёт) это 40 draw call только на вспышки, плюс те же
/// осколки висят в кадре ещё кадр после попадания.
///
/// Про SRP Batcher: он НЕ снижает число draw call, а только
/// удешевляет их подготовку (bind состояния). Каждый Renderer -
/// это отдельная пара bind+draw, поэтому количество Renderer'ов
/// здесь равно количеству draw call'ов один в один. Свести их
/// к одному - единственный способ урезать это число.
///
/// Форма считается в шейдере из UV квада (см. BulletTracerVfx.hlsl,
/// радиальный режим), ни текстур, ни сэмплов. Затухание идёт
/// через размер вершин, а не через цвет: шейдер не читает vertex
/// color, а пересборка меша не нужна - вершины и так пишутся
/// каждый кадр. MaterialPropertyBlock не используется: он ломает
/// SRP Batcher.
///
/// Меш свой у каждого экземпляра пула, потому что вершины
/// перезаписываются каждый кадр. Буферы выделяются один раз в
/// Awake: в рантайме аллокаций нет.
///
/// Симуляция целиком в локальных координатах эффекта: объект
/// стоит в точке попадания и развёрнут билбордом к камере
/// (VfxSharedAssets.FaceDirection), поэтому осколки лежат в
/// плоскости билборда, а гравитация - один повёрнутый вектор.
/// </summary>
public sealed class ImpactEffect : VfxEffect
{
    // =========================================================
    // РЕКОМЕНДУЕМЫЕ ЗНАЧЕНИЯ
    //
    // Размеры заданы от высоты экрана. Игровая камера стоит
    // в ~22.5 мировых единиц от плоскости боя при FOV 60,
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

    // Квадов в меше: вспышка + по одному на каждый возможный осколок.
    private const int TotalQuads = MaxShards + 1;

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

    // Позиции и скорости осколков в локальных координатах эффекта.
    private Vector3[] shardPosition;
    private Vector3[] shardVelocity;
    private float[] shardTimeLeft;
    private float[] shardDuration;
    private float[] shardBaseSize;

    // Буфер меша: по 4 вершины на квад.
    private Vector3[] vertexBuffer;

    private Mesh mesh;
    private Vector3 localGravity;
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

        AllocateState();
        BuildMesh();
    }

    private void AllocateState()
    {
        shardPosition = new Vector3[MaxShards];
        shardVelocity = new Vector3[MaxShards];
        shardTimeLeft = new float[MaxShards];
        shardDuration = new float[MaxShards];
        shardBaseSize = new float[MaxShards];

        vertexBuffer = new Vector3[TotalQuads * 4];
    }

    private void BuildMesh()
    {
        mesh = new Mesh
        {
            name = "VfxImpactFlashAndShards",
            hideFlags = HideFlags.HideAndDontSave
        };

        // Меш перезаписывается каждый кадр: без MarkDynamic Unity
        // считает его статическим и перезаливает буфер как static.
        mesh.MarkDynamic();

        Vector2[] uvs = new Vector2[TotalQuads * 4];
        int[] triangles = new int[TotalQuads * 6];

        for (int i = 0; i < TotalQuads; i++)
        {
            int vertex = i * 4;

            uvs[vertex + 0] = new Vector2(0f, 0f);
            uvs[vertex + 1] = new Vector2(0f, 1f);
            uvs[vertex + 2] = new Vector2(1f, 0f);
            uvs[vertex + 3] = new Vector2(1f, 1f);

            int triangle = i * 6;

            triangles[triangle + 0] = vertex + 0;
            triangles[triangle + 1] = vertex + 2;
            triangles[triangle + 2] = vertex + 1;
            triangles[triangle + 3] = vertex + 2;
            triangles[triangle + 4] = vertex + 3;
            triangles[triangle + 5] = vertex + 1;
        }

        mesh.vertices = vertexBuffer;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Осколки улетают на несколько единиц от точки попадания,
        // а сам эффект стоит в этой точке: локального бокса 8
        // единиц хватает с запасом. Без заданного бокса эффект
        // вылетел бы из frustum culling, как только осколки
        // разлетелись.
        mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(8f, 8f, 8f)
        );

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

        if (renderer == null)
            renderer = gameObject.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupAdditiveRenderer(
            renderer,
            mesh,
            VfxSharedAssets.ImpactMaterial
        );
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

        Quaternion toLocal =
            Quaternion.Inverse(transform.rotation);

        // Гравитация в локальных координатах: один вектор на
        // весь эффект вместо поворота Vector3.down в каждом
        // осколке каждый кадр.
        localGravity = toLocal * (Vector3.down * shardGravity);

        SpawnShards(normal, toLocal);

        // Меш перезаписывается целиком уже на первом кадре, но
        // показывать до Tick нечего: старые вершины от прошлого
        // попадания лежали бы в кадре один кадр.
        WriteFlash(0f);
        WriteShards();

        mesh.vertices = vertexBuffer;

        BeginPlay(flashLifetime);
    }

    private void SpawnShards(Vector3 normal, Quaternion toLocal)
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

            shardVelocity[i] = toLocal * (direction * speed);

            float size = shardSize * Random.Range(0.7f, 1.3f);

            shardBaseSize[i] = size;

            // Стартовая точка - сразу перед точкой попадания,
            // как и раньше, когда осколок был отдельным объектом.
            shardPosition[i] =
                toLocal * (normal * (size * 0.5f));

            float duration = Random.Range(
                shardLifetimeMin,
                shardLifetimeMax
            );

            shardDuration[i] = duration;
            shardTimeLeft[i] = duration;
        }

        // Неиспользуемые осколки гасим, иначе они останутся
        // висеть на экране от прошлого попадания.
        for (int i = activeShards; i < MaxShards; i++)
        {
            shardTimeLeft[i] = 0f;
            shardPosition[i] = Vector3.zero;
            shardVelocity[i] = Vector3.zero;
            shardBaseSize[i] = 0f;
        }
    }

    protected override void Tick(float deltaTime)
    {
        TimeLeft -= deltaTime;

        // Вспышка гаснет квадратично: полный размер в первый
        // кадр, схлопывание в ноль к концу жизни.
        float flashLife =
            Mathf.Clamp01(TimeLeft / Duration);

        WriteFlash(flashSize * flashLife * flashLife);

        for (int i = 0; i < activeShards; i++)
        {
            shardTimeLeft[i] -= deltaTime;

            if (shardTimeLeft[i] > 0f)
            {
                Vector3 velocity = shardVelocity[i];

                velocity += localGravity * deltaTime;
                shardVelocity[i] = velocity;

                shardPosition[i] += velocity * deltaTime;
            }

            WriteShard(i);
        }

        // Квады, которые не пишет WriteShard (сверх activeShards),
        // затираем: в буфере от прошлого попадания лежали бы
        // старые вершины.
        for (int i = activeShards; i < MaxShards; i++)
            HideQuad(i + 1);

        mesh.vertices = vertexBuffer;

        if (TimeLeft <= 0f)
            Finish();
    }

    private void WriteFlash(float size)
    {
        WriteQuad(0, Vector3.zero, size);
    }

    private void WriteShard(int index)
    {
        float life =
            shardDuration[index] > 0f
                ? shardTimeLeft[index] / shardDuration[index]
                : 0f;

        if (life <= 0f)
        {
            HideQuad(index + 1);
            return;
        }

        WriteQuad(
            index + 1,
            shardPosition[index],
            shardBaseSize[index] * life
        );
    }

    private void WriteShards()
    {
        for (int i = 0; i < MaxShards; i++)
            WriteShard(i);
    }

    /// <summary>
    /// Квад размером size с центром в точке position. size - это
    /// полная сторона, как и раньше у масштабируемого квада.
    /// </summary>
    private void WriteQuad(int index, Vector3 position, float size)
    {
        int vertex = index * 4;

        if (size <= 0.0001f)
        {
            HideQuad(index);
            return;
        }

        float half = size * 0.5f;

        Vector3 right = Vector3.right * half;
        Vector3 up = Vector3.up * half;

        vertexBuffer[vertex + 0] = position - right - up;
        vertexBuffer[vertex + 1] = position - right + up;
        vertexBuffer[vertex + 2] = position + right - up;
        vertexBuffer[vertex + 3] = position + right + up;
    }

    private void HideQuad(int index)
    {
        int vertex = index * 4;

        vertexBuffer[vertex + 0] = Vector3.zero;
        vertexBuffer[vertex + 1] = Vector3.zero;
        vertexBuffer[vertex + 2] = Vector3.zero;
        vertexBuffer[vertex + 3] = Vector3.zero;
    }

    protected override void ReturnToPool()
    {
        activeShards = 0;

        VfxPools.Impacts.Despawn(this);
    }
}