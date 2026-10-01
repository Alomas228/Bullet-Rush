using UnityEngine;

/// <summary>
/// Пулевой трассер: тонкое белое ядро с оранжевым свечением,
/// короткий хвост, который гаснет вместе с пулей.
///
/// Тот же эффект используется для снарядов врага (enemyStyle):
/// отличается только красный материал и потолок фазы Follow -
/// форма, размер и поведение остаются игровыми. Вражеские пули
/// и свои должны читаться на лету, поэтому цвет - единственное,
/// чем они различаются.
///
/// ГЛАВНОЕ: трассер живёт ВСЮ жизнь пули, а не фиксированные
/// 0.09 секунды. Пуля летит до 3 секунд (lifetime в Bullet), и
/// импульс на 0.09 с ловится только на первых выстрелах -
/// дальше глаз ничего не видит. Поэтому у эффекта две фазы:
///
///   Follow - едет вместе с пулей на полной яркости, пока пуля
///            жива (пока её GameObject активен в иерархии);
///   Fade   - пуля умерла, хвост схлопывается за fadeLifetime.
///
/// Проверка смерти пули - один null и одно чтение bool на кадр,
/// это дешевле, чем знать точное время жизни заранее и гадать
/// с темпом стрельбы. Когда трассер теряет цель, он больше её
/// не ищет (лапч): пуля возвращается в пул и может тут же быть
/// выдана под новый выстрел - иначе хвост прыгнул бы на чужую
/// пулю.
///
/// ПОЧЕМУ ХВОСТ И ЯДРО В ОДНОМ МЕШЕ. Снаряд раньше стоил
/// 2 draw call: MeshRenderer трассера плюс MeshRenderer шара на
/// самом снаряде (у Sphere-префаба он был отдельным объектом).
/// SRP Batcher эти два рендерера не складывает в один вызов, а
/// инстансинг не применим: у шара и у хвоста разные меши и
/// разные шейдеры. Единственный способ получить один вызов -
/// держать обе части в одном меше на одном шейдере, поэтому
/// круглое ядро въехало в меш трассера как второй квад.
///
/// Форма квада выбирается в шейдере через UV1 (см.
/// BulletTracerVfx.hlsl): 0 - хвост, 1 - круглое свечение.
/// Раньше форма задавалась одним _RadialMode на материал, и в
/// одном материале жить двум формам было нельзя.
///
/// Размеры пишутся прямо в вершины, а не в localScale: хвост
/// растягивается по X, а ядро обязано остаться круглым, и
/// localScale с двумя разными коэффициентами растянул бы ядро в
/// овал. Меш перезаписывается каждый кадр (едет за пулей и
/// гаснет), поэтому он динамический и свой у каждого экземпляра
/// пула. Буферы выделяются один раз в Awake.
///
/// Форма (белое ядро + оранжевое свечение) считается в шейдере
/// из UV квада: ни текстур, ни сэмплов. Затухание сделано через
/// размер вершин, а не через цвет: одна запись в буфер меша
/// вместо пересборки геометрии или MaterialPropertyBlock (тот
/// ломает SRP Batcher).
/// </summary>
public sealed class TracerEffect : VfxEffect
{
    // =========================================================
    // РЕКОМЕНДУЕМЫЕ ЗНАЧЕНИЯ
    //
    // Размеры заданы от высоты экрана. Игровая камера стоит
    // в ~22.5 мировых единиц от плоскости боя при FOV 60,
    // то есть видит ~26 единиц по вертикали: на 1080p это
    // ~42 пикселя на единицу.
    //
    //   ширина 0.26  -> ~1% высоты экрана (гало), ядро ~3-4 px
    //   длина 0.075 * скорость -> при 16 у/с = 1.2 ед. = ~5%
    //
    // Если камеру поднимут или уменьшишь FOV - пересчитай:
    //   size = доля_экрана * (2 * расстояния * tan(FOV / 2))
    // =========================================================

    /// <summary>
    /// Ширина трассера: ~1% высоты экрана. Внутри квада ядро
    /// занимает примерно треть ширины, то есть 3-4 пикселя -
    /// тонко, но глазом ловится.
    /// </summary>
    public const float DefaultWidth = 0.26f;

    /// <summary>
    /// Длина хвоста, если скорость неизвестна.
    /// </summary>
    public const float DefaultLength = 1.1f;

    /// <summary>
    /// Сколько хвост живёт ПОСЛЕ смерти пули: 0.09 с = ~5
    /// кадров на 60 fps. Дольше - хвост заметно отрывается от
    /// точки попадания, короче - исчезает слишком резко.
    /// </summary>
    public const float DefaultFadeLifetime = 0.09f;

    /// <summary>
    /// Страховочный потолок фазы Follow. Время жизни пули в
    /// проекте 3 с, поэтому 3.5 с - с запасом. Нужен на случай,
    /// если пуля почему-то не деактивировалась: трассер не
    /// должен висеть вечно и держать объект в пуле.
    /// </summary>
    public const float MaxFollowLifetime = 3.5f;

    /// <summary>
    /// Потолок Follow для трассера врага. Снаряд EnemyProjectile
    /// живёт 5 с, поэтому 3.5 с ему мало: хвост погас бы на лету,
    /// а снаряд ещё летел бы. 5.5 с - с запасом.
    /// </summary>
    public const float EnemyMaxFollowLifetime = 5.5f;

    /// <summary>
    /// Длина хвоста пропорциональна скорости пули: быстрая пуля
    /// оставляет более длинный импульс.
    /// </summary>
    public const float LengthPerSpeed = 0.075f;

    public const float MinLength = 0.6f;
    public const float MaxLength = 1.8f;

    /// <summary>
    /// Длина хвоста на единицу скорости для снарядов врага. Снаряды
    /// медленные (8 у/с у дальника, 7 у/с у взрыва танка) против
    /// 14-16 у/с у пуль игрока, поэтому при общем коэффициенте
    /// вражеский хвост упирался в MinLength и был вдвое короче
    /// игрового. 0.14 при 8 у/с даёт те же ~1.12 единицы, что и
    /// игровые 0.075 при 15 у/с.
    /// </summary>
    public const float EnemyLengthPerSpeed = 0.14f;

    private const float MinWidthRatio = 0.35f;

    /// <summary>
    /// Диаметр круглого ядра. Раньше на снаряде висел Sphere с
    /// диаметром 0.1 (localScale префаба 0.1 на юните-примитиве),
    /// теперь это второй квад того же размера.
    /// </summary>
    public const float DefaultCoreSize = 0.1f;

    private const int Quads = 2;

    [Header("Visual")]
    [SerializeField, Tooltip("Ширина трассера: доля высоты экрана (0.01 = 1%)")]
    private float width = DefaultWidth;

    [SerializeField, Tooltip("Длина хвоста, если скорость не задана")]
    private float length = DefaultLength;

    [SerializeField, Tooltip("Затухание хвоста после смерти пули, с")]
    private float fadeLifetime = DefaultFadeLifetime;

    [SerializeField, Tooltip("Длина хвоста на единицу скорости")]
    private float lengthPerSpeed = LengthPerSpeed;

    [SerializeField, Tooltip("Диаметр круглого ядра снаряда")]
    private float coreSize = DefaultCoreSize;

    [Header("Follow")]
    [SerializeField, Tooltip("Жить вместе с пулёй до её смерти")]
    private bool followBullet = true;

    [SerializeField, Tooltip("Потолок фазы Follow, с")]
    private float maxFollowLifetime = MaxFollowLifetime;

    [Header("Enemy")]
    [Tooltip("Красный вариант: пули врагов. Отличается " +
             "только материалом и пулом, из которого трассер " +
             "возвращается.")]
    [SerializeField] private bool enemyStyle;

    private Transform followTarget;
    private bool following;

    private float fullLength;
    private float fullWidth;

    private Vector3[] vertexBuffer;
    private Mesh mesh;

    /// <summary>
    /// Шаблон для пула, когда префаб не задан. Создаётся один раз.
    /// </summary>
    public static GameObject CreateTemplate()
    {
        return BuildTemplate("TracerTemplate", false);
    }

    /// <summary>
    /// Шаблон красного трассера для пула снарядов врага.
    /// Создаётся один раз.
    /// </summary>
    public static GameObject CreateEnemyTemplate()
    {
        return BuildTemplate("EnemyTracerTemplate", true);
    }

    private static GameObject BuildTemplate(string name, bool enemy)
    {
        GameObject template = new GameObject(name);

        // Гасим объект ДО добавления компонента: Awake не должен
        // отработать на самом шаблоне. Рендерер и материал ставятся
        // на инстансах пула, а шаблон - только источник для
        // Instantiate. enemyStyle и maxFollowLifetime копируются
        // из шаблона вместе с остальными сериализованными полями.
        template.SetActive(false);

        TracerEffect tracer =
            template.AddComponent<TracerEffect>();

        tracer.enemyStyle = enemy;
        tracer.maxFollowLifetime =
            enemy
                ? EnemyMaxFollowLifetime
                : MaxFollowLifetime;
        tracer.lengthPerSpeed =
            enemy
                ? EnemyLengthPerSpeed
                : LengthPerSpeed;

        return template;
    }

    private void Awake()
    {
        gameObject.name = enemyStyle ? "EnemyTracer" : "Tracer";

        AllocateState();
        BuildMesh();

        VfxSharedAssets.SetupRenderer(
            gameObject.GetComponent<MeshRenderer>(),
            mesh,
            enemyStyle
                ? VfxSharedAssets.EnemyTracerMaterial
                : VfxSharedAssets.TracerMaterial
        );
    }

    private void AllocateState()
    {
        vertexBuffer = new Vector3[Quads * 4];
    }

    private void BuildMesh()
    {
        mesh = new Mesh
        {
            name = enemyStyle
                ? "VfxEnemyTracerWithCore"
                : "VfxTracerWithCore",
            hideFlags = HideFlags.HideAndDontSave
        };

        // Меш перезаписывается каждый кадр: он едет за пулей и
        // гаснет. Без MarkDynamic Unity считает меш статическим и
        // перезаливает буфер как static.
        mesh.MarkDynamic();

        Vector2[] uvs = new Vector2[Quads * 4];
        Vector2[] shapeOverrides = new Vector2[Quads * 4];
        int[] triangles = new int[Quads * 6];

        // Квад 0 - хвост. Ось X от -1 (хвост) до 0 (голова), так
        // же, как в VfxSharedAssets.StreakMesh: U = 1 у головы.
        uvs[0] = new Vector2(0f, 0f);
        uvs[1] = new Vector2(0f, 1f);
        uvs[2] = new Vector2(1f, 0f);
        uvs[3] = new Vector2(1f, 1f);

        // Квад 1 - круглое ядро по центру квадрата.
        uvs[4] = new Vector2(0f, 0f);
        uvs[5] = new Vector2(0f, 1f);
        uvs[6] = new Vector2(1f, 0f);
        uvs[7] = new Vector2(1f, 1f);

        // -1 = хвост, 1 = круглое свечение. Раньше форма была
        // одна на материал (_RadialMode), и в одном материале
        // хвост и ядро ужиться не могли.
        for (int i = 0; i < 4; i++)
        {
            shapeOverrides[i] = new Vector2(-1f, 0f);
            shapeOverrides[i + 4] = new Vector2(1f, 0f);
        }

        triangles[0] = 0;
        triangles[1] = 2;
        triangles[2] = 1;
        triangles[3] = 2;
        triangles[4] = 3;
        triangles[5] = 1;

        triangles[6] = 4;
        triangles[7] = 6;
        triangles[8] = 5;
        triangles[9] = 6;
        triangles[10] = 7;
        triangles[11] = 5;

        mesh.vertices = vertexBuffer;
        mesh.uv = uvs;
        mesh.SetUVs(1, shapeOverrides);
        mesh.triangles = triangles;

        // Хвост уходит назад по X, поэтому бокс должен покрывать
        // и MaxLength, и CoreSize. Задаётся руками: с нулевым
        // боксом трассер вылетел бы из frustum culling.
        float extent = Mathf.Max(MaxLength, DefaultCoreSize) + 1f;

        mesh.bounds = new Bounds(
            new Vector3(-MaxLength * 0.5f, 0f, 0f),
            new Vector3(extent * 2f, extent * 2f, extent * 2f)
        );

        MeshRenderer meshRenderer =
            gameObject.GetComponent<MeshRenderer>();

        if (meshRenderer == null)
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
    }

    /// <summary>
    /// Запуск трассера. Вызывается пулом сразу после активации.
    /// </summary>
    /// <param name="direction">Направление полёта пули.</param>
    /// <param name="speed">Скорость пули: от неё зависит длина.</param>
    /// <param name="target">Пуля, за которой идёт трассер (может быть null).</param>
    public void Play(
        Vector3 direction,
        float speed,
        Transform target)
    {
        fullLength = speed > 0f
            ? Mathf.Clamp(
                speed * lengthPerSpeed,
                MinLength,
                MaxLength)
            : length;

        fullWidth = width;

        // Поворот считается один раз: пуля летит по Vector3.forward
        // и не поворачивается, так что ось X (длина хвоста)
        // остаётся верной до конца полёта.
        transform.rotation =
            VfxSharedAssets.FaceDirection(direction);

        // Масштаб объекта всегда единичный: длина и ширина пишутся
        // в вершины, иначе ядро, у которого своя длина, растянулось
        // бы тем же localScale.
        transform.localScale = Vector3.one;

        followTarget = followBullet ? target : null;
        following = followTarget != null;

        // Меш перезаписывается целиком на первом же кадре, но
        // показывать до этого нечего: в буфере лежали бы нули от
        // Awake и квад прорисовался бы точкой в точке попадания.
        WriteMesh(1f);

        // С пулей - едем до её смерти (с потолком maxFollowLifetime).
        // Без пули - сразу короткое затухание.
        BeginPlay(
            following
                ? maxFollowLifetime
                : fadeLifetime
        );
    }

    protected override void Tick(float deltaTime)
    {
        TimeLeft -= deltaTime;

        if (TimeLeft <= 0f)
        {
            if (following)
            {
                // Потолок Follow reached: пуля не умерла, но ждать
                // больше нельзя - переходим в затухание.
                StartFade();
                return;
            }

            Finish();
            return;
        }

        if (following)
        {
            if (followTarget != null &&
                followTarget.gameObject.activeInHierarchy)
            {
                transform.position = followTarget.position;

                // Пока пуля жива, хвост и ядро полной формы.
                WriteMesh(1f);
                return;
            }

            // Пуля умерла: хвост остаётся в точке попадания
            // и гаснет. Лапча: обратно за целью уже не идём.
            followTarget = null;
            StartFade();
            return;
        }

        ApplyFade();
    }

    private void StartFade()
    {
        following = false;

        Duration = Mathf.Max(0.01f, fadeLifetime);
        TimeLeft = Duration;

        ApplyFade();
    }

    private void ApplyFade()
    {
        // Квадратичное схлопывание: почти вся жизнь хвост держит
        // форму, последние кадры быстро уходит в ноль.
        float life = Mathf.Clamp01(TimeLeft / Duration);

        WriteMesh(life * life);
    }

    /// <summary>
    /// Сборка хвоста и ядра в меш. retract = 1 - полная форма,
    /// 0 - всё схлопнуто.
    /// </summary>
    private void WriteMesh(float retract)
    {
        float lengthNow = fullLength * retract;

        float widthNow =
            fullWidth * Mathf.Lerp(MinWidthRatio, 1f, retract);

        float halfWidth = widthNow * 0.5f;

        // Хвост: от головы (X = 0) назад по -X.
        vertexBuffer[0] = new Vector3(-lengthNow, -halfWidth, 0f);
        vertexBuffer[1] = new Vector3(-lengthNow, halfWidth, 0f);
        vertexBuffer[2] = new Vector3(0f, -halfWidth, 0f);
        vertexBuffer[3] = new Vector3(0f, halfWidth, 0f);

        // Ядро: квад со стороной coreSize по центру головы.
        // Сторона равна в обеих осях, поэтому после поворота
        // объекта к камере ядро остаётся круглым.
        float halfCore = coreSize * retract * 0.5f;

        vertexBuffer[4] = new Vector3(-halfCore, -halfCore, 0f);
        vertexBuffer[5] = new Vector3(-halfCore, halfCore, 0f);
        vertexBuffer[6] = new Vector3(halfCore, -halfCore, 0f);
        vertexBuffer[7] = new Vector3(halfCore, halfCore, 0f);

        mesh.vertices = vertexBuffer;
    }

    protected override void ReturnToPool()
    {
        followTarget = null;
        following = false;

        if (enemyStyle)
        {
            VfxPools.EnemyTracers.Despawn(this);
            return;
        }

        VfxPools.Tracers.Despawn(this);
    }
}