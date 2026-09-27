using UnityEngine;

/// <summary>
/// Взрыв из трёх слоёв, как в «боевике», а не как плоский диск:
///
///   1. Огненный шар - настоящая сфера радиуса поражения, а не
///      билборд. Стоит с любого ракурса и заполняет всю зону
///      урона: видно, где опасно. Объём даёт сам шейдер -
///      задние грани рисуются аддитивно, поэтому в центре диска
///      два слоя, у силуэта один, плюс горячая кромка. Всё
///      остывание приходит из vertex color: материал общий на
///      все взрывы, а стадия у каждого своя.
///   2. Угли - горящие точки, летят по конусу, гаснут и падают.
///   3. Дым - мягкие тёмные клубы, шире и позже огня.
///
/// Почему не Particle System - как и во всех остальных VFX этого
/// проекта: свой эмиттер на каждый взрыв дороже трёх мешей,
/// которые уже лежат в пуле. Так что взрыв стоит 3 draw call.
///
/// Меши свои у каждого экземпляра пула: цвет пишется каждый кадр
/// (стадия остывания, прозрачность), а общий меш на всех означал
/// бы, что остывают все взрывы на сцене разом.
///
/// Меши углей и дыма лежат в мировых координатах относительно
/// центра взрыва: корень не повёрнут, и не нужно ни одной
/// обратной матрицы в кадре. У сферы поворот не нужен вовсе -
/// она симметрична, поэтому билборд-логики здесь больше нет.
/// </summary>
public sealed class ExplosionEffect : VfxEffect
{
    // =========================================================
    // РЕКОМЕНДУЕМЫЕ ЗНАЧЕНИЯ
    //
    // Камера стоит в ~22.5 мировых единиц от плоскости боя при
    // FOV 60: видит ~26 единиц по вертикали, на 1080p это ~42
    // пикселя на единицу. Радиус взрыва в бою - 2..5 единиц,
    // то есть 84..210 пикселей: сфера радиуса поражения
    // перекрывает зону урона и читается как «здесь небезопасно».
    // =========================================================

    private const int MaxEmbers = 10;
    private const int MaxSmokePuffs = 5;

    // Сфера строится в коде. 14x9 - это 150 вершин: для мягкого
    // аддитивного шара хватает с большим запасом, а на кадр
    // приходится только запись цвета.
    private const int SphereSegments = 14;
    private const int SphereRings = 9;

    [Header("Fireball")]
    [SerializeField, Tooltip("Время жизни огненного шара, с")]
    private float fireballDuration = 0.32f;

    [SerializeField, Tooltip("Пиковый радиус шара в радиусах взрыва")]
    private float fireballPeakScale = 1.1f;

    [Header("Embers")]
    [SerializeField, Tooltip("Сколько углей на взрыв")]
    private int emberCount = 8;

    [SerializeField, Tooltip("Время жизни углей, с")]
    private float emberDuration = 0.45f;

    [SerializeField, Tooltip("Гравитация углей")]
    private float emberGravity = 11f;

    [Header("Smoke")]
    [SerializeField, Tooltip("Сколько клубов дыма")]
    private int smokeCount = 4;

    [SerializeField, Tooltip("Задержка дыма после взрыва, с")]
    private float smokeDelay = 0.04f;

    [SerializeField, Tooltip("Время жизни дыма, с")]
    private float smokeDuration = 0.8f;

    [SerializeField, Tooltip("Насколько дым шире зоны поражения")]
    private float smokeSpread = 0.35f;

    // Шар: сфера, размер через localScale, цвет через
    // vertex color.
    private Transform fireballTransform;
    private Mesh fireballMesh;
    private Color[] fireballColors;

    // Угли и дым: по несколько кватов в одном меше.
    private Mesh emberMesh;
    private Mesh smokeMesh;
    private Vector3[] emberVertices;
    private Color[] emberColors;
    private Vector3[] smokeVertices;
    private Color[] smokeColors;

    private Vector3[] emberPosition;
    private Vector3[] emberVelocity;
    private float[] emberTimeLeft;
    private float[] emberSize;
    private float[] emberHeat;

    private Vector3[] smokePosition;
    private float[] smokeTimeLeft;
    private float[] smokeSize;
    private float[] smokeRise;

    private Color blastColor;
    private float blastRadius;
    private float elapsed;
    private int activeEmbers;
    private int activeSmoke;

    /// <summary>
    /// Шаблон для пула, когда префаб не задан. Создаётся один раз.
    /// </summary>
    public static GameObject CreateTemplate()
    {
        GameObject template = new GameObject("ExplosionTemplate");

        template.SetActive(false);
        template.AddComponent<ExplosionEffect>();

        return template;
    }

    private void Awake()
    {
        gameObject.name = "Explosion";

        AllocateState();
        BuildLayers();
    }

    private void AllocateState()
    {
        fireballColors =
            new Color[(SphereSegments + 1) * (SphereRings + 1)];

        emberVertices = new Vector3[MaxEmbers * 4];
        emberColors = new Color[MaxEmbers * 4];

        smokeVertices = new Vector3[MaxSmokePuffs * 4];
        smokeColors = new Color[MaxSmokePuffs * 4];

        emberPosition = new Vector3[MaxEmbers];
        emberVelocity = new Vector3[MaxEmbers];
        emberTimeLeft = new float[MaxEmbers];
        emberSize = new float[MaxEmbers];
        emberHeat = new float[MaxEmbers];

        smokePosition = new Vector3[MaxSmokePuffs];
        smokeTimeLeft = new float[MaxSmokePuffs];
        smokeSize = new float[MaxSmokePuffs];
        smokeRise = new float[MaxSmokePuffs];
    }

    private void BuildLayers()
    {
        // Угли рисуются прямо на корне: их меш и так двигается
        // каждый кадр, отдельный объект не нужен.
        emberMesh = BuildLayerMesh("VfxBlastEmbers", MaxEmbers);

        SetupRenderer(
            gameObject,
            emberMesh,
            VfxSharedAssets.BlastEmberMaterial
        );

        // Дым - отдельный объект: у него свой меш.
        smokeMesh = BuildLayerMesh("VfxBlastSmoke", MaxSmokePuffs);

        SetupRenderer(
            CreateLayer("Smoke"),
            smokeMesh,
            VfxSharedAssets.BlastSmokeMaterial
        );

        // Огненный шар - единственный объёмный слой: сфера.
        fireballMesh = BuildSphereMesh(
            "VfxBlastFireball",
            SphereSegments,
            SphereRings
        );

        fireballTransform = CreateLayer("Fireball").transform;

        SetupRenderer(
            fireballTransform.gameObject,
            fireballMesh,
            VfxSharedAssets.ExplosionSphereMaterial
        );
    }

    private GameObject CreateLayer(string name)
    {
        GameObject layer = new GameObject(name);

        layer.transform.SetParent(transform, false);

        return layer;
    }

    private static void SetupRenderer(
        GameObject target,
        Mesh mesh,
        Material material)
    {
        MeshRenderer renderer =
            target.GetComponent<MeshRenderer>();

        if (renderer == null)
            renderer = target.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupRenderer(
            renderer,
            mesh,
            material
        );
    }

    /// <summary>
    /// Меш из N кватов с центром в pivot. Цвета и вершины
    /// заполняются эффектом каждый кадр, поэтому меш динамический.
    /// </summary>
    private static Mesh BuildLayerMesh(string name, int quads)
    {
        Vector3[] vertices = new Vector3[quads * 4];
        Vector2[] uvs = new Vector2[quads * 4];
        int[] triangles = new int[quads * 6];

        for (int i = 0; i < quads; i++)
        {
            int vertex = i * 4;

            vertices[vertex + 0] = new Vector3(-0.5f, -0.5f, 0f);
            vertices[vertex + 1] = new Vector3(-0.5f, 0.5f, 0f);
            vertices[vertex + 2] = new Vector3(0.5f, -0.5f, 0f);
            vertices[vertex + 3] = new Vector3(0.5f, 0.5f, 0f);

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

        return CreateLayerMesh(
            name,
            vertices,
            uvs,
            triangles
        );
    }

    /// <summary>
    /// Единичная сфера радиуса 0.5, чтобы localScale равнялся
    /// диаметру. Нормали в шейдере берутся из самой позиции
    /// вершины, поэтому атрибут normal не нужен: на сфере
    /// радиус-вектор и есть нормаль.
    /// Порядок треугольников не важен - Cull Off рисует обе
    /// стороны, а именно они и создают объём.
    /// </summary>
    private static Mesh BuildSphereMesh(
        string name,
        int segments,
        int rings)
    {
        Vector3[] vertices = new Vector3[(segments + 1) * (rings + 1)];
        Vector2[] uvs = new Vector2[(segments + 1) * (rings + 1)];
        int[] triangles = new int[segments * rings * 6];

        for (int ring = 0; ring <= rings; ring++)
        {
            float v = ring / (float)rings;
            float phi = v * Mathf.PI;

            float sin = Mathf.Sin(phi);
            float cos = Mathf.Cos(phi);

            for (int segment = 0; segment <= segments; segment++)
            {
                float u = segment / (float)segments;
                float theta = u * Mathf.PI * 2f;

                int index = ring * (segments + 1) + segment;

                vertices[index] = new Vector3(
                    sin * Mathf.Cos(theta),
                    cos,
                    sin * Mathf.Sin(theta)
                ) * 0.5f;

                uvs[index] = new Vector2(u, 1f - v);
            }
        }

        int cursor = 0;

        for (int ring = 0; ring < rings; ring++)
        {
            for (int segment = 0; segment < segments; segment++)
            {
                int bottomLeft = ring * (segments + 1) + segment;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + segments + 1;
                int topRight = topLeft + 1;

                triangles[cursor++] = bottomLeft;
                triangles[cursor++] = topLeft;
                triangles[cursor++] = bottomRight;

                triangles[cursor++] = topLeft;
                triangles[cursor++] = topRight;
                triangles[cursor++] = bottomRight;
            }
        }

        return CreateLayerMesh(
            name,
            vertices,
            uvs,
            triangles
        );
    }

    private static Mesh CreateLayerMesh(
        string name,
        Vector3[] vertices,
        Vector2[] uvs,
        int[] triangles)
    {
        Mesh mesh = new Mesh
        {
            name = name,
            hideFlags = HideFlags.HideAndDontSave
        };

        mesh.MarkDynamic();

        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.colors = new Color[vertices.Length];
        mesh.triangles = triangles;

        // Взрыв шире зоны поражения (дым вылезает за неё), и он
        // остаётся на месте, пока эффект жив. Локальный бокс с
        // запасом, иначе эффект вылетал бы из frustum culling на
        // первой секунде.
        mesh.bounds = new Bounds(
            Vector3.zero,
            new Vector3(16f, 16f, 16f)
        );

        return mesh;
    }

    /// <summary>
    /// Запуск взрыва.
    /// </summary>
    /// <param name="position">Центр взрыва.</param>
    /// <param name="radius">Радиус поражения: от него считается
    /// шар и от него же разлетаются угли с дымом.</param>
    /// <param name="color">Цвет пламени. Белое ядро, угли и дым
    /// выводятся из него.</param>
    public void Play(
        Vector3 position,
        float radius,
        Color color)
    {
        transform.position = position;
        transform.rotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        blastColor = new Color(color.r, color.g, color.b, 1f);
        blastRadius = Mathf.Max(radius, 0.2f);
        elapsed = 0f;

        SpawnEmbers();
        SpawnSmoke();

        // Слои, которые ещё не стартовали, могут лежать с
        // прошлого взрыва на этом же объекте пула: гасим всё
        // сразу, иначе остатки прошлого взрыва мигнут на экране.
        WriteFireball();
        WriteEmbers();
        WriteSmoke();

        float total = Mathf.Max(
            fireballDuration,
            Mathf.Max(
                emberDuration,
                smokeDelay + smokeDuration
            )
        );

        BeginPlay(total);
    }

    private void SpawnEmbers()
    {
        activeEmbers = Mathf.Clamp(emberCount, 0, MaxEmbers);

        for (int i = 0; i < activeEmbers; i++)
        {
            Vector2 circle = Random.insideUnitCircle;

            Vector3 direction = new Vector3(
                circle.x,
                Random.Range(0.15f, 1f),
                circle.y
            );

            if (direction.sqrMagnitude < 0.0001f)
                direction = Vector3.up;

            direction.Normalize();

            // Скорость в мировых единицах в секунду: чем больше
            // радиус взрыва, тем дальше разлетаются угли.
            emberVelocity[i] = direction * (
                blastRadius * Random.Range(1.1f, 2.4f)
            );

            emberPosition[i] = Vector3.up * blastRadius * 0.15f;

            emberTimeLeft[i] =
                emberDuration * Random.Range(0.6f, 1f);

            emberSize[i] =
                blastRadius * Random.Range(0.07f, 0.16f);

            emberHeat[i] = Random.Range(0.75f, 1f);
        }
    }

    private void SpawnSmoke()
    {
        activeSmoke = Mathf.Clamp(smokeCount, 0, MaxSmokePuffs);

        for (int i = 0; i < activeSmoke; i++)
        {
            Vector2 circle = Random.insideUnitCircle;

            smokePosition[i] = new Vector3(
                circle.x * blastRadius * 0.3f,
                blastRadius * Random.Range(0.1f, 0.45f),
                circle.y * blastRadius * 0.3f
            );

            // Отсчёт назад идёт от полной жизни, задержка
            // отсчитывается общим elapsed: так клуб не щёлкает
            // первым кадром и не живёт дольше своей жизни.
            smokeTimeLeft[i] =
                smokeDuration * Random.Range(0.7f, 1f);

            smokeSize[i] =
                blastRadius * Random.Range(0.5f, 0.8f);

            smokeRise[i] =
                blastRadius * Random.Range(0.3f, 0.7f);
        }
    }

    protected override void Tick(float deltaTime)
    {
        elapsed += deltaTime;

        WriteFireball();
        TickEmbers(deltaTime);
        TickSmoke(deltaTime);

        if (elapsed >= Duration)
            Finish();
    }

    /// <summary>
    /// Огненный шар: сфера радиуса поражения. За первые 30%
    /// жизни раздувается с перелётом, потом остывает и гаснет.
    /// Цвет идёт от белого ядра через жёлтый к тёмно-красным
    /// углям, яркость падает в разы - именно это даёт вспышку в
    /// первые кадры и спокойное затухание дальше.
    ///
    /// Меш единичный (радиус 0.5), поэтому scale - это диаметр.
    /// </summary>
    private void WriteFireball()
    {
        float progress =
            Mathf.Clamp01(elapsed / fireballDuration);

        float scale;

        if (progress < 0.3f)
        {
            float t = progress / 0.3f;

            scale = Mathf.Lerp(
                0.3f,
                fireballPeakScale,
                1f - Mathf.Pow(1f - t, 3f)
            );
        }
        else
        {
            float t = (progress - 0.3f) / 0.7f;

            scale = Mathf.Lerp(
                fireballPeakScale,
                0.7f,
                t * t
            );
        }

        float size = blastRadius * scale * 2f;

        fireballTransform.localScale =
            new Vector3(size, size, size);

        // Старт не белый: 85% белого в первом кадре давали
        // белый шар, а не вспышку огня. Белёется только
        // самый центр кадра.
        Color hot = Color.Lerp(blastColor, Color.white, 0.35f);

        Color color = progress < 0.25f
            ? Color.Lerp(hot, blastColor, progress / 0.25f)
            : Color.Lerp(
                blastColor,
                blastColor * 0.2f,
                (progress - 0.25f) / 0.75f
            );

        // HDR: в первые кадры вдвое выше порога Bloom (0.9),
        // к концу почти до нуля. Значение ниже, чем было у
        // билборда, потому что аддитивная сфера складывает две
        // грани - яркость и так удваивается в центре.
        float intensity = Mathf.Lerp(1.8f, 0f, progress * progress);

        SetLayerColor(fireballColors, Scale(color, intensity));

        fireballMesh.colors = fireballColors;
    }

    private void TickEmbers(float deltaTime)
    {
        int alive = 0;

        for (int i = 0; i < activeEmbers; i++)
        {
            emberTimeLeft[i] -= deltaTime;

            if (emberTimeLeft[i] > 0f)
                alive++;

            Vector3 velocity = emberVelocity[i];

            velocity.y -= emberGravity * deltaTime;
            emberVelocity[i] = velocity;

            emberPosition[i] += velocity * deltaTime;
        }

        WriteEmbers();
    }

    private void WriteEmbers()
    {
        for (int i = 0; i < MaxEmbers; i++)
        {
            int vertex = i * 4;

            if (i >= activeEmbers ||
                emberTimeLeft[i] <= 0f)
            {
                HideQuad(emberVertices, emberColors, vertex);
                continue;
            }

            float life =
                emberTimeLeft[i] / emberDuration;

            // Искра вытянута по направлению движения: круглые
            // точки читаются как «брызги», а полоса - как
            // летящий горящий кусок. Длина зависит от скорости,
            // поэтому быстрые искры длиннее медленных.
            SetStreak(
                emberVertices,
                vertex,
                emberPosition[i],
                emberVelocity[i],
                emberSize[i] * Mathf.Lerp(0.4f, 1f, life)
            );

            SetQuadColor(
                emberColors,
                vertex,
                EmberColor(life, emberHeat[i])
            );
        }

        emberMesh.vertices = emberVertices;
        emberMesh.colors = emberColors;
    }

    /// <summary>
    /// Уголь остывает от жёлто-белого к тёмно-красному и при
    /// этом тускнеет: в начале он ярче порога Bloom и виден как
    /// искра, в конце - тёмная точка.
    /// </summary>
    private Color EmberColor(float life, float heat)
    {
        Color hot = Color.Lerp(blastColor, Color.white, 0.7f);
        Color cold = blastColor * 0.25f;

        Color color = life > 0.45f
            ? Color.Lerp(hot, blastColor, (life - 0.45f) / 0.55f)
            : Color.Lerp(blastColor, cold, 1f - life / 0.45f);

        return Scale(color, Mathf.Lerp(0.2f, 3.2f, life) * heat);
    }

    private void TickSmoke(float deltaTime)
    {
        for (int i = 0; i < activeSmoke; i++)
        {
            smokeTimeLeft[i] -= deltaTime;

            // Дым всплывает: под пятном на земле это единственное,
            // что показывает, что клуб уходит вверх, а не
            // расползается по полу.
            smokePosition[i] +=
                Vector3.up * smokeRise[i] * deltaTime;
        }

        WriteSmoke();
    }

    private void WriteSmoke()
    {
        // До задержки дыма нет вообще: иначе он появлялся бы
        // первым кадром и потом только таял.
        if (elapsed < smokeDelay)
        {
            HideLayer(smokeMesh, smokeVertices, smokeColors);
            return;
        }

        for (int i = 0; i < MaxSmokePuffs; i++)
        {
            int vertex = i * 4;

            if (i >= activeSmoke ||
                smokeTimeLeft[i] <= 0f)
            {
                HideQuad(smokeVertices, smokeColors, vertex);
                continue;
            }

            float life = Mathf.Clamp01(
                smokeTimeLeft[i] / smokeDuration
            );

            // Клуб раскрывается по мере подъёма.
            float size = smokeSize[i] * (
                1f + smokeSpread + (1f - life) * 0.6f
            );

            SetQuad(
                smokeVertices,
                vertex,
                smokePosition[i],
                size
            );

            // Альфа в квадрате: клуб не только тает, но и
            // мягко появляется, а не щёлкает на первом кадре.
            SetQuadColor(
                smokeColors,
                vertex,
                new Color(1f, 1f, 1f, 0.42f * life * life)
            );
        }

        smokeMesh.vertices = smokeVertices;
        smokeMesh.colors = smokeColors;
    }

    private static void SetQuad(
        Vector3[] vertices,
        int vertex,
        Vector3 position,
        float size)
    {
        float half = size * 0.5f;

        Vector3 right = Vector3.right * half;
        Vector3 up = Vector3.up * half;

        vertices[vertex + 0] = position - right - up;
        vertices[vertex + 1] = position - right + up;
        vertices[vertex + 2] = position + right - up;
        vertices[vertex + 3] = position + right + up;
    }

    /// <summary>
    /// Квад, вытянутый вдоль движения. Ось полосы лежит в плоскости
    /// боя: с камеры сверху видно и длину, и направление, поэтому
    /// искра читается как летящая, а не как точка. Длина
    /// пропорциональна скорости, поэтому разлёт выглядит
    /// правдоподобно, а не одинаковыми штрихами.
    /// </summary>
    private static void SetStreak(
        Vector3[] vertices,
        int vertex,
        Vector3 position,
        Vector3 velocity,
        float size)
    {
        Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);

        float speed = flat.magnitude;

        if (speed < 0.0001f)
        {
            flat = Vector3.forward;
            speed = 1f;
        }
        else
        {
            flat /= speed;
        }

        // Вторую ось берём крест-накрест в плоскости боя: квад
        // остаётся тонким по ширине и длинным по ходу.
        Vector3 side = new Vector3(-flat.z, 0f, flat.x);

        // Хвост тем длиннее, чем быстрее летит искра. Верхняя
        // граница нужна, иначе на первых кадрах у самого центра
        // взрыва получаются длинные полосы через весь шар.
        float length = Mathf.Min(
            size,
            speed * 0.055f
        ) * 0.5f;

        float width = size * 0.22f * 0.5f;

        Vector3 along = flat * length;
        Vector3 across = side * width;

        vertices[vertex + 0] = position - along - across;
        vertices[vertex + 1] = position - along + across;
        vertices[vertex + 2] = position + along - across;
        vertices[vertex + 3] = position + along + across;
    }

    private static void SetQuadColor(
        Color[] colors,
        int vertex,
        Color color)
    {
        colors[vertex + 0] = color;
        colors[vertex + 1] = color;
        colors[vertex + 2] = color;
        colors[vertex + 3] = color;
    }

    private static void SetLayerColor(
        Color[] colors,
        Color color)
    {
        for (int i = 0; i < colors.Length; i++)
            colors[i] = color;
    }

    /// <summary>
    /// Вырожденный квад с нулевой альфой: ни пикселя на экране.
    /// Так гаснут и умершие капли, и слои, которые не стартовали
    /// в этом запуске.
    /// </summary>
    private static void HideQuad(
        Vector3[] vertices,
        Color[] colors,
        int vertex)
    {
        vertices[vertex + 0] = Vector3.zero;
        vertices[vertex + 1] = Vector3.zero;
        vertices[vertex + 2] = Vector3.zero;
        vertices[vertex + 3] = Vector3.zero;

        SetQuadColor(colors, vertex, Color.clear);
    }

    /// <summary>
    /// Слой целиком вне экрана: и геометрия, и цвет.
    /// </summary>
    private static void HideLayer(
        Mesh mesh,
        Vector3[] vertices,
        Color[] colors)
    {
        for (int i = 0; i < colors.Length; i += 4)
            HideQuad(vertices, colors, i);

        mesh.vertices = vertices;
        mesh.colors = colors;
    }

    private static Color Scale(Color color, float intensity)
    {
        return new Color(
            color.r * intensity,
            color.g * intensity,
            color.b * intensity,
            1f
        );
    }

    protected override void ReturnToPool()
    {
        VfxPools.Explosions.Despawn(this);
    }
}
