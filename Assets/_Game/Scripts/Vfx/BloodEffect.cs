using UnityEngine;

/// <summary>
/// Кровь при попадании пули в моба: несколько капель разлетаются
/// от точки попадания по направлению полёта пули, летят по дуге
/// под гравитацией и тают на лету.
///
/// Почему не Particle System - как и в ImpactEffect: свой эмиттер
/// на каждое попадание дороже короткого эффекта. Зальёт ещё и то,
/// что у Particle System на каждый эмиттер свой набор шейдерных
/// вариантов и своя сортировка.
///
/// Главное отличие от ImpactEffect по стоимости: все капли
/// effect'а собраны в ОДИН меш на объекте, поэтому попадание
/// целиком стоит 1 draw call (у ImpactEffect вспышка + 3 осколка
/// = 4). Затухание идёт через vertex color, а не через
/// localScale: капли летят по разным направлениям, и у каждой
/// своя прозрачность.
///
/// Меш свой у каждого экземпляра пула, потому что вершины
/// пересобираются каждый кадр. Буферы вершин и цветов
/// выделяются один раз в Awake: в рантайме аллокаций нет.
///
/// Симуляция целиком в локальных координатах эффекта: объект
/// стоит в точке попадания и развёрнут билбордом к камере
/// (VfxSharedAssets.FaceDirection), поэтому капли летят в плоскости
/// билборда, а гравитация - один повёрнутый вектор. Вершины пишутся
/// в меш сразу, без обратных преобразований каждый кадр.
/// </summary>
public sealed class BloodEffect : VfxEffect
{
    // =========================================================
    // РЕКОМЕНДУЕМЫЕ ЗНАЧЕНИЯ
    //
    // Размеры заданы от высоты экрана. Камера стоит в ~22.5
    // мировых единицах при FOV 60, то есть видит ~26 единиц по
    // вертикали: на 1080p это ~42 пикселя на единицу.
    //
    //   капля 0.28 -> ~12 пикселей (с разбросом 0.6-1.4 это
    //                ~7-16 пикселей). 0.09 читалась как точка,
    //                0.16 - как брызги; дальше 0.4 - уже пятно,
    //                а не капля.
    //
    // Если камеру поднимут или уменьшишь FOV - пересчитай:
    //   size = доля * (2 * distance * tan(FOV/2))
    // =========================================================

    public const int DefaultDropletCount = 8;
    public const float DefaultDropletSize = 0.28f;
    public const float DefaultSpeedMin = 3f;
    public const float DefaultSpeedMax = 7.5f;
    public const float DefaultGravity = 16f;
    public const float DefaultLifetimeMin = 0.32f;
    public const float DefaultLifetimeMax = 0.68f;

    /// <summary>
    /// Насколько конус брызг размыкается от направления полёта
    /// пули: 0 - строго вперёд, 1 - во все стороны.
    /// </summary>
    public const float DefaultSpread = 0.8f;

    private const int MaxDroplets = 12;

    private static readonly Color32 InvisibleColor =
        new Color32(0, 0, 0, 0);

    [Header("Droplets")]
    [SerializeField, Tooltip("Сколько капель на попадание")]
    private int dropletCount = DefaultDropletCount;

    [SerializeField, Tooltip("Базовый размер капли в мировых единицах")]
    private float dropletSize = DefaultDropletSize;

    [SerializeField, Tooltip("Минимальная скорость капли")]
    private float speedMin = DefaultSpeedMin;

    [SerializeField, Tooltip("Максимальная скорость капли")]
    private float speedMax = DefaultSpeedMax;

    [SerializeField, Tooltip("Гравитация капель")]
    private float gravity = DefaultGravity;

    [SerializeField, Tooltip("Размыкание конуса брызг (0 - только вперёд)")]
    private float spread = DefaultSpread;

    [Header("Lifetime")]
    [SerializeField, Tooltip("Минимальное время жизни капли")]
    private float lifetimeMin = DefaultLifetimeMin;

    [SerializeField, Tooltip("Максимальное время жизни капли")]
    private float lifetimeMax = DefaultLifetimeMax;

    // Позиции и скорости капель в локальных координатах эффекта.
    private Vector3[] dropletPosition;
    private Vector3[] dropletVelocity;
    private float[] dropletTimeLeft;
    private float[] dropletDuration;
    private float[] dropletBaseSize;
    private float[] dropletShade;

    // Буферы меша: по 4 вершины и 4 цвета на каплю.
    private Vector3[] vertexBuffer;
    private Color32[] colorBuffer;

    private Mesh mesh;
    private Vector3 localGravity;
    private int activeDroplets;

    /// <summary>
    /// Шаблон для пула, когда префаб не задан. Создаётся один раз.
    /// </summary>
    public static GameObject CreateTemplate()
    {
        GameObject template = new GameObject("BloodTemplate");

        template.SetActive(false);
        template.AddComponent<BloodEffect>();

        return template;
    }

    private void Awake()
    {
        gameObject.name = "BloodBurst";

        AllocateState();
        BuildMesh();
    }

    private void AllocateState()
    {
        dropletPosition = new Vector3[MaxDroplets];
        dropletVelocity = new Vector3[MaxDroplets];
        dropletTimeLeft = new float[MaxDroplets];
        dropletDuration = new float[MaxDroplets];
        dropletBaseSize = new float[MaxDroplets];
        dropletShade = new float[MaxDroplets];

        vertexBuffer = new Vector3[MaxDroplets * 4];
        colorBuffer = new Color32[MaxDroplets * 4];
    }

    private void BuildMesh()
    {
        mesh = new Mesh
        {
            name = "VfxBloodDroplets",
            hideFlags = HideFlags.HideAndDontSave
        };

        // Меш перезаписывается каждый кадр: без MarkDynamic Unity
        // считает его статическим и перезаливает буфер как static.
        mesh.MarkDynamic();

        Vector2[] uvs = new Vector2[MaxDroplets * 4];
        int[] triangles = new int[MaxDroplets * 6];

        for (int i = 0; i < MaxDroplets; i++)
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
        mesh.colors32 = colorBuffer;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Капли улетают на 2-3 единицы от точки попадания, а сам
        // эффект стоит в этой точке: локального бокса 8 единиц
        // хватает с запасом. RecalculateBounds каждый кадр не
        // нужен, а без заданного бокса эффект вылетел бы из
        // frustum culling, как только капли разлетелись.
        mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(8f, 8f, 8f)
        );

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

        if (renderer == null)
            renderer = gameObject.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupRenderer(
            renderer,
            mesh,
            VfxSharedAssets.BloodMaterial
        );
    }

    /// <summary>
    /// Запуск брызг крови.
    /// </summary>
    /// <param name="position">Точка попадания.</param>
    /// <param name="normal">Направление полёта пули: брызги
    /// разлетаются вперёд по нему.</param>
    /// <param name="scale">Общий множитель размера брызг.
    /// Больше единицы - кровавее, меньше - мельче.</param>
    public void Play(
        Vector3 position,
        Vector3 normal,
        float scale = 1f)
    {
        transform.position = position;
        transform.rotation = VfxSharedAssets.FaceDirection(normal);

        Quaternion toLocal =
            Quaternion.Inverse(transform.rotation);

        // Гравитация в локальных координатах: один вектор на
        // весь эффект вместо поворота Vector3.down в каждой
        // капле каждый кадр.
        localGravity = toLocal * (Vector3.down * gravity);

        activeDroplets = Mathf.Clamp(
            Mathf.RoundToInt(dropletCount * scale),
            1,
            MaxDroplets
        );

        float longest = 0f;

        for (int i = 0; i < activeDroplets; i++)
        {
            Vector2 circle = Random.insideUnitCircle;

            Vector3 spray = new Vector3(circle.x, 0f, circle.y);

            Vector3 direction = Vector3.Slerp(
                normal,
                spray,
                spread * Random.Range(0.75f, 1f)
            );

            if (direction.sqrMagnitude < 0.0001f)
                direction = normal;

            direction.Normalize();

            dropletVelocity[i] = toLocal * (
                direction *
                Random.Range(speedMin, speedMax) *
                scale
            );

            dropletPosition[i] = Vector3.zero;

            float duration = Random.Range(
                lifetimeMin,
                lifetimeMax
            );

            dropletDuration[i] = duration;
            dropletTimeLeft[i] = duration;

            dropletBaseSize[i] = dropletSize * scale * Random.Range(0.6f, 1.4f);
            dropletShade[i] = Random.Range(0.7f, 1.2f);

            if (duration > longest)
                longest = duration;
        }

        // Буферы с прошлого попадания нельзя оставлять: у
        // погашенных капель в них лежат старые вершины с
        // непрозрачным цветом. Перезаписываем целиком.
        WriteBuffers();

        BeginPlay(longest);
    }

    protected override void Tick(float deltaTime)
    {
        int alive = 0;

        for (int i = 0; i < activeDroplets; i++)
        {
            dropletTimeLeft[i] -= deltaTime;

            if (dropletTimeLeft[i] <= 0f)
                continue;

            alive++;

            Vector3 velocity = dropletVelocity[i];

            velocity += localGravity * deltaTime;
            dropletVelocity[i] = velocity;

            dropletPosition[i] += velocity * deltaTime;
        }

        if (alive == 0)
        {
            // Последние капли погашены прошлым WriteBuffers,
            // повторно перезаписывать буферы незачем.
            TimeLeft = 0f;
            Finish();
            return;
        }

        WriteBuffers();

        TimeLeft -= deltaTime;

        if (TimeLeft <= 0f)
            Finish();
    }

    /// <summary>
    /// Сборка капель в меш. Идём по всем MaxDroplets, а не по
    /// activeDroplets: у капли, которая уже погасла, и у капли
    /// сверх activeDroplets в буфере лежат старые вершины, и их
    /// надо затереть нулями, иначе они останутся висеть на экране
    /// от прошлого попадания.
    /// </summary>
    private void WriteBuffers()
    {
        for (int i = 0; i < MaxDroplets; i++)
        {
            int vertex = i * 4;

            if (i >= activeDroplets ||
                dropletTimeLeft[i] <= 0f)
            {
                HideDroplet(vertex);
                continue;
            }

            float life =
                dropletTimeLeft[i] / dropletDuration[i];

            float half = dropletBaseSize[i] * life * 0.5f;

            Vector3 position = dropletPosition[i];
            Vector3 right = Vector3.right * half;
            Vector3 up = Vector3.up * half;

            vertexBuffer[vertex + 0] = position - right - up;
            vertexBuffer[vertex + 1] = position - right + up;
            vertexBuffer[vertex + 2] = position + right - up;
            vertexBuffer[vertex + 3] = position + right + up;

            // Затухание - в альфе, размер при этом почти не
            // меняется: капля выглядит выцветающей, а не
            // схлопывающейся в точку.
            byte alpha = (byte)(
                life * life * 255f
            );

            byte shade = (byte)(
                dropletShade[i] * 255f
            );

            Color32 color = new Color32(
                shade,
                shade,
                shade,
                alpha
            );

            colorBuffer[vertex + 0] = color;
            colorBuffer[vertex + 1] = color;
            colorBuffer[vertex + 2] = color;
            colorBuffer[vertex + 3] = color;
        }

        mesh.vertices = vertexBuffer;
        mesh.colors32 = colorBuffer;
    }

    private void HideDroplet(int vertex)
    {
        vertexBuffer[vertex + 0] = Vector3.zero;
        vertexBuffer[vertex + 1] = Vector3.zero;
        vertexBuffer[vertex + 2] = Vector3.zero;
        vertexBuffer[vertex + 3] = Vector3.zero;

        colorBuffer[vertex + 0] = InvisibleColor;
        colorBuffer[vertex + 1] = InvisibleColor;
        colorBuffer[vertex + 2] = InvisibleColor;
        colorBuffer[vertex + 3] = InvisibleColor;
    }

    protected override void ReturnToPool()
    {
        VfxPools.BloodBursts.Despawn(this);
    }
}
