using UnityEngine;

/// <summary>
/// Огонь на горящем мобе. Эффект не одноразовый, а состояние:
/// он живёт ровно пока моб горит, следует за ним и гаснет сам,
/// когда горение кончилось или моб умер. Проверяет это напрямую
/// по Enemy, поэтому в Enemy не нужно ни счётчика, ни ссылки на
/// эффект, ни Stop() при смерти - враг просто перестаёт гореть.
///
/// Языки пламени и угли собраны в ОДИН меш, поэтому всё горение
/// стоит 1 draw call на моба. Материал общий на все эффекты,
/// цвет идёт через vertex color, MaterialPropertyBlock не
/// используется - он ломает SRP Batcher.
///
/// Симуляция в локальных координатах эффекта, как у BloodEffect:
/// объект развёрнут билбордом к камере (FaceDirection с
/// Vector3.right даёт X = экран вправо, Y = экран вверх), поэтому
/// пламя всегда растёт вверх по экрану, а движение моба не
/// требует пересчёта вершин в мировых координатах.
///
/// Языки не проигрываются один раз: каждый догорает и
/// пересоздаётся у основания, пока горит. Иначе через полсекунды
/// на мобе не осталось бы ничего.
/// </summary>
public sealed class BurnFlameEffect : VfxEffect
{
    // =========================================================
    // РЕКОМЕНДУЕМЫЕ ЗНАЧЕНИЯ
    //
    // Камера стоит в ~22.5 мировых единиц при FOV 60, то есть
    // видит ~26 единиц по вертикали: на 1080p это ~42 пикселя на
    // единицу. Мобы высотой 1..2 единицы (normal 1, tank 1.3,
    // boss 2), поэтому доля от масштаба моба даёт язык пламени
    // примерно в пол-роста - огонь должен накрывать силуэт, а
    // не уезжать над ним.
    // =========================================================

    public const int DefaultFlameCount = 5;
    public const int DefaultEmberCount = 4;

    /// <summary>
    /// Доля от масштаба моба: высота языка пламени.
    /// </summary>
    public const float DefaultFlameSize = 0.55f;

    public const float DefaultFlameSpeedMin = 1.3f;
    public const float DefaultFlameSpeedMax = 2.4f;
    public const float DefaultFlameLifetimeMin = 0.28f;
    public const float DefaultFlameLifetimeMax = 0.5f;

    public const float DefaultEmberSize = 0.09f;
    public const float DefaultEmberSpeedMin = 1.8f;
    public const float DefaultEmberSpeedMax = 3.4f;
    public const float DefaultEmberLifetimeMin = 0.45f;
    public const float DefaultEmberLifetimeMax = 0.85f;

    /// <summary>
    /// Насколько пламя отрывается от земли: основание языка
    /// начинается не в ноль, а на уровне середины тела моба.
    /// </summary>
    public const float DefaultBaseHeight = 0.3f;

    /// <summary>
    /// Страховка на случай, если состояние горения залипнет:
    /// эффект не живёт вечно ни при каких условиях.
    /// </summary>
    public const float MaxLifetime = 15f;

    private const int MaxFlames = 6;
    private const int MaxEmbers = 5;
    private const int TotalQuads = MaxFlames + MaxEmbers;

    [Header("Flames")]
    [SerializeField, Tooltip("Сколько языков пламени одновременно")]
    private int flameCount = DefaultFlameCount;

    [SerializeField, Tooltip("Высота языка в долях масштаба моба")]
    private float flameSize = DefaultFlameSize;

    [SerializeField, Tooltip("Минимальная скорость подъёма языка")]
    private float flameSpeedMin = DefaultFlameSpeedMin;

    [SerializeField, Tooltip("Максимальная скорость подъёма языка")]
    private float flameSpeedMax = DefaultFlameSpeedMax;

    [SerializeField, Tooltip("Минимальное время жизни языка")]
    private float flameLifetimeMin = DefaultFlameLifetimeMin;

    [SerializeField, Tooltip("Максимальное время жизни языка")]
    private float flameLifetimeMax = DefaultFlameLifetimeMax;

    [Header("Embers")]
    [SerializeField, Tooltip("Сколько углей над пламенем")]
    private int emberCount = DefaultEmberCount;

    [SerializeField, Tooltip("Размер угля в долях масштаба моба")]
    private float emberSize = DefaultEmberSize;

    [SerializeField, Tooltip("Минимальная скорость угля")]
    private float emberSpeedMin = DefaultEmberSpeedMin;

    [SerializeField, Tooltip("Максимальная скорость угля")]
    private float emberSpeedMax = DefaultEmberSpeedMax;

    [SerializeField, Tooltip("Минимальное время жизни угля")]
    private float emberLifetimeMin = DefaultEmberLifetimeMin;

    [SerializeField, Tooltip("Максимальное время жизни угля")]
    private float emberLifetimeMax = DefaultEmberLifetimeMax;

    [Header("Placement")]
    [SerializeField, Tooltip("Высота основания пламени в долях масштаба моба")]
    private float baseHeight = DefaultBaseHeight;

    // Состояние языков и углей в локальных координатах.
    private Vector3[] flamePosition;
    private Vector3[] flameVelocity;
    private float[] flameTimeLeft;
    private float[] flameDuration;
    private float[] flameWidth;
    private float[] flameHeat;

    private Vector3[] emberPosition;
    private Vector3[] emberVelocity;
    private float[] emberTimeLeft;
    private float[] emberDuration;
    private float[] emberSizeNow;
    private float[] emberHeat;

    private Vector3[] vertexBuffer;
    private Color[] colorBuffer;

    private Mesh mesh;
    private Enemy target;
    private float bodyScale;
    private Vector3 localGravity;
    private int activeFlames;
    private int activeEmbers;

    /// <summary>
    /// Шаблон для пула, когда префаб не задан. Создаётся один раз.
    /// </summary>
    public static GameObject CreateTemplate()
    {
        GameObject template = new GameObject("BurnFlameTemplate");

        template.SetActive(false);
        template.AddComponent<BurnFlameEffect>();

        return template;
    }

    private void Awake()
    {
        gameObject.name = "BurnFlames";

        AllocateState();
        BuildMesh();
    }

    private void AllocateState()
    {
        flamePosition = new Vector3[MaxFlames];
        flameVelocity = new Vector3[MaxFlames];
        flameTimeLeft = new float[MaxFlames];
        flameDuration = new float[MaxFlames];
        flameWidth = new float[MaxFlames];
        flameHeat = new float[MaxFlames];

        emberPosition = new Vector3[MaxEmbers];
        emberVelocity = new Vector3[MaxEmbers];
        emberTimeLeft = new float[MaxEmbers];
        emberDuration = new float[MaxEmbers];
        emberSizeNow = new float[MaxEmbers];
        emberHeat = new float[MaxEmbers];

        vertexBuffer = new Vector3[TotalQuads * 4];
        colorBuffer = new Color[TotalQuads * 4];
    }

    private void BuildMesh()
    {
        mesh = new Mesh
        {
            name = "VfxBurnFlames",
            hideFlags = HideFlags.HideAndDontSave
        };

        // Меш перезаписывается каждый кадр.
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
        mesh.colors = colorBuffer;
        mesh.uv = uvs;
        mesh.triangles = triangles;

        // Эффект стоит на мобе и следует за ним, а пламя
        // поднимается вверх на пол-роста. Локального бокса 4
        // единиц хватает с запасом; без заданного бокса меш
        // вылетел бы из frustum culling, как только язык догорел.
        mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(4f, 4f, 4f)
        );

        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();

        if (renderer == null)
            renderer = gameObject.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupRenderer(
            renderer,
            mesh,
            VfxSharedAssets.BurnFlameMaterial
        );
    }

    /// <summary>
    /// Запуск горения на мобе.
    /// </summary>
    /// <param name="enemy">Моб, который горит. Эффект сам следит
    /// за его состоянием и заканчивается, когда горение кончилось.</param>
    public void Play(Enemy enemy)
    {
        target = enemy;

        // Масштаб моба вместо его коллайдера: у префабов
        // normal 1, tank 1.3, boss 2, и по нему сразу видно
        // пропорции пламени. Никаких GetComponent - только
        // свойство transform.
        bodyScale = Mathf.Max(
            enemy.transform.lossyScale.x,
            0.05f
        );

        activeFlames = Mathf.Clamp(flameCount, 1, MaxFlames);
        activeEmbers = Mathf.Clamp(emberCount, 0, MaxEmbers);

        // Билборд: X - вправо по экрану, Y - вверх по экрану.
        transform.rotation =
            VfxSharedAssets.FaceDirection(Vector3.right);

        Quaternion toLocal = Quaternion.Inverse(transform.rotation);

        // Угли чуть проседают: они тяжелее огня. Один вектор
        // на весь эффект вместо поворота Vector3.down в каждом
        // угле каждый кадр.
        localGravity = toLocal * (Vector3.down * 2.2f);

        transform.position = GetBasePosition();

        for (int i = 0; i < activeFlames; i++)
            SpawnFlame(i);

        for (int i = 0; i < activeEmbers; i++)
            SpawnEmber(i);

        // Буферы с прошлого моба нельзя оставлять: у погасших
        // языков в них лежат старые вершины с непрозрачным
        // цветом. Перезаписываем целиком.
        WriteBuffers();

        BeginPlay(MaxLifetime);
    }

    private Vector3 GetBasePosition()
    {
        return target.transform.position +
            Vector3.up * (bodyScale * baseHeight);
    }

    private void SpawnFlame(int index)
    {
        Vector2 circle = Random.insideUnitCircle;

        flamePosition[index] = new Vector3(
            circle.x * bodyScale * 0.18f,
            0f,
            circle.y * bodyScale * 0.18f
        );

        Vector3 drift = new Vector3(circle.x, 0f, circle.y);

        // Язык рвётся вбок слабее, чем вверх, иначе пламя
        // разлетается пятном, а не столбом.
        flameVelocity[index] =
            drift * Random.Range(0.1f, 0.3f) +
            Vector3.up * Random.Range(flameSpeedMin, flameSpeedMax);

        float duration = Random.Range(
            flameLifetimeMin,
            flameLifetimeMax
        );

        flameDuration[index] = duration;
        flameTimeLeft[index] = duration;

        flameWidth[index] = Random.Range(0.55f, 0.85f);
        flameHeat[index] = Random.Range(0.8f, 1.25f);
    }

    private void SpawnEmber(int index)
    {
        Vector2 circle = Random.insideUnitCircle;

        emberPosition[index] = new Vector3(
            circle.x * bodyScale * 0.3f,
            bodyScale * flameSize * 0.5f,
            circle.y * bodyScale * 0.3f
        );

        emberVelocity[index] =
            new Vector3(circle.x, 0f, circle.y) *
            Random.Range(0.3f, 0.8f) +
            Vector3.up * Random.Range(emberSpeedMin, emberSpeedMax);

        float duration = Random.Range(
            emberLifetimeMin,
            emberLifetimeMax
        );

        emberDuration[index] = duration;
        emberTimeLeft[index] = duration;

        emberSizeNow[index] =
            emberSize * bodyScale * Random.Range(0.7f, 1.3f);

        emberHeat[index] = Random.Range(0.7f, 1.3f);
    }

    protected override void Tick(float deltaTime)
    {
        // Моб мог умереть или перестать гореть. Ссылка на
        // уничтоженный Unity-объект даёт target == null, поэтому
        // и Destroy, и IsDead приводят к одной проверке.
        if (target == null || target.IsDead || !target.IsBurning)
        {
            Finish();
            return;
        }

        // Моб движется - пламя едет за ним. Симуляция языков
        // при этом остаётся локальной: сдвигается только корень.
        transform.position = GetBasePosition();

        for (int i = 0; i < activeFlames; i++)
        {
            flameTimeLeft[i] -= deltaTime;

            if (flameTimeLeft[i] <= 0f)
            {
                // Горение - состояние, а не разовый залп: догоревший
                // язык тут же пересоздаётся у основания.
                SpawnFlame(i);
                continue;
            }

            flamePosition[i] += flameVelocity[i] * deltaTime;
        }

        for (int i = 0; i < activeEmbers; i++)
        {
            emberTimeLeft[i] -= deltaTime;

            if (emberTimeLeft[i] <= 0f)
            {
                SpawnEmber(i);
                continue;
            }

            Vector3 velocity = emberVelocity[i];

            velocity += localGravity * deltaTime;
            emberVelocity[i] = velocity;

            emberPosition[i] += velocity * deltaTime;
        }

        WriteBuffers();

        TimeLeft -= deltaTime;

        if (TimeLeft <= 0f)
            Finish();
    }

    /// <summary>
    /// Сборка языков и углей в меш. Идём по всем TotalQuads: у
    /// погасшего элемента в буфере лежат старые вершины, их надо
    /// затирать, иначе они остались бы висеть от прошлого моба.
    /// </summary>
    private void WriteBuffers()
    {
        for (int i = 0; i < MaxFlames; i++)
        {
            int vertex = i * 4;

            if (i >= activeFlames)
            {
                HideQuad(vertex);
                continue;
            }

            float life = flameTimeLeft[i] / flameDuration[i];

            WriteFlame(i, vertex, life);
        }

        for (int i = 0; i < MaxEmbers; i++)
        {
            int vertex = (MaxFlames + i) * 4;

            if (i >= activeEmbers)
            {
                HideQuad(vertex);
                continue;
            }

            float life = emberTimeLeft[i] / emberDuration[i];

            WriteEmber(i, vertex, life);
        }

        mesh.vertices = vertexBuffer;
        mesh.colors = colorBuffer;
    }

    /// <summary>
    /// Язык пламени: узкий внизу (у основания он всегда шире,
    /// чем рисуется) и сходящий на нет к вершине. Сужение даёт
    /// градиент альфы по вершинам квада - именно он превращает
    /// мягкий круг шейдера в пламя, а не в пятно.
    /// </summary>
    private void WriteFlame(
        int index,
        int vertex,
        float life)
    {
        // life идёт от 1 к 0: язык растёт к концу жизни и гаснет.
        float fade = life * life;

        float height =
            bodyScale * flameSize * Mathf.Lerp(1.1f, 0.55f, 1f - life);

        float width = height * flameWidth[index] * 0.55f;

        Vector3 position = flamePosition[index];
        Vector3 right = Vector3.right * width * 0.5f;
        Vector3 up = Vector3.up * height * 0.5f;

        vertexBuffer[vertex + 0] = position - right - up;
        vertexBuffer[vertex + 1] = position - right + up;
        vertexBuffer[vertex + 2] = position + right - up;
        vertexBuffer[vertex + 3] = position + right + up;

        // Цвет: у основания жёлто-белый, к вершине оранжевый и
        // тусклый. HDR-значения выше единицы нужны, чтобы пламя
        // переваливало порог Bloom в первые кадры горения.
        Color baseColor = FlameColor(fade) * flameHeat[index];

        // Основание ярче и не прозрачное, вершина гаснет в ноль.
        colorBuffer[vertex + 0] = baseColor;
        colorBuffer[vertex + 1] = baseColor * 0.35f;
        colorBuffer[vertex + 2] = baseColor;
        colorBuffer[vertex + 3] = baseColor * 0.35f;
    }

    private void WriteEmber(
        int index,
        int vertex,
        float life)
    {
        float size = emberSizeNow[index] * Mathf.Lerp(0.3f, 1f, life);
        float half = size * 0.5f;

        Vector3 position = emberPosition[index];
        Vector3 right = Vector3.right * half;
        Vector3 up = Vector3.up * half;

        vertexBuffer[vertex + 0] = position - right - up;
        vertexBuffer[vertex + 1] = position - right + up;
        vertexBuffer[vertex + 2] = position + right - up;
        vertexBuffer[vertex + 3] = position + right + up;

        // Уголь тускнеет быстрее языка и уходит в тёмно-красный.
        Color color = Color.Lerp(
            EmberColor(life),
            new Color(0.35f, 0.05f, 0.01f, 1f),
            1f - life
        ) * emberHeat[index];

        colorBuffer[vertex + 0] = color;
        colorBuffer[vertex + 1] = color;
        colorBuffer[vertex + 2] = color;
        colorBuffer[vertex + 3] = color;
    }

    private static Color FlameColor(float fade)
    {
        if (fade > 0.55f)
        {
            return Color.Lerp(
                new Color(1.6f, 0.75f, 0.15f, 1f),
                new Color(2.4f, 1.5f, 0.6f, 1f),
                (fade - 0.55f) / 0.45f
            );
        }

        return Color.Lerp(
            new Color(1.1f, 0.3f, 0.04f, 1f),
            new Color(1.6f, 0.75f, 0.15f, 1f),
            fade / 0.55f
        );
    }

    private static Color EmberColor(float life)
    {
        return Color.Lerp(
            new Color(1.4f, 0.35f, 0.05f, 1f),
            new Color(2.2f, 1.3f, 0.5f, 1f),
            life
        );
    }

    private void HideQuad(int vertex)
    {
        vertexBuffer[vertex + 0] = Vector3.zero;
        vertexBuffer[vertex + 1] = Vector3.zero;
        vertexBuffer[vertex + 2] = Vector3.zero;
        vertexBuffer[vertex + 3] = Vector3.zero;

        Color hidden = new Color(0f, 0f, 0f, 0f);

        colorBuffer[vertex + 0] = hidden;
        colorBuffer[vertex + 1] = hidden;
        colorBuffer[vertex + 2] = hidden;
        colorBuffer[vertex + 3] = hidden;
    }

    protected override void ReturnToPool()
    {
        target = null;

        VfxPools.BurnFlames.Despawn(this);
    }
}
