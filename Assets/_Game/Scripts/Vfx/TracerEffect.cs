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
/// ПОЧЕМУ ХВОСТ И ЯДРО В ДВУХ ОТДЕЛЬНЫХ КВАДОВ. Раньше они были
/// одним мешем с двумя квадами, и всё держалось на двух вещах:
/// разные размеры были зашиты прямо в вершины, а форма выбиралась
/// в шейдере через UV1 (см. BulletTracerVfx.hlsl). С переходом на
/// GPU-инстансинг первый пункт стал невозможен - у всех инстансов
/// обязан быть один и тот же меш. Теперь каждый квад отдельным
/// мешем, а длина и ширина приходят из матрицы инстанса; рисовает
/// всё TracerBatcher. На видимый результат это не влияет: те же
/// UV, та же форма, тот же фрагментный шейдер.
///
/// Форма квада задаётся на самом меше (UV1.x): у хвоста -1, у
/// ядра +1. Раньше это держалось тем же каналом в том же меше,
/// теперь каналов два, но значения прежние.
///
/// Затухание тоже больше не пишет в вершины: это просто
/// масштаб матрицы. Побочный плюс - исчезли перезаписи
/// vertexBuffer каждый кадр на каждом трассере.
///
/// Объект в пуле больше не содержит рендерера вовсе: ни MeshRenderer,
/// ни MeshFilter. Рисует батчер.
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

    /// <summary>
    /// Насколько сейчас развёрнута форма: 1 - полная, 0 - схлопнута.
    /// Раньше это значение писалось прямо в вершины меша, теперь его
    /// читает TracerBatcher и раскладывает по масштабу матриц
    /// инстансов. Пока трассер летит с пулёй, значение равно 1 и
    /// матрица не меняется вовсе - меняется только позиция.
    /// </summary>
    internal float Retract = 1f;

    /// <summary>
    /// Стоит ли трассер в очереди на отрисовку. Ставится батчером
    /// при Play и снимается при возврате в пул; очередь чистится
    /// в LateUpdate батчера.
    /// </summary>
    internal bool Batched;

    internal bool EnemyStyle
    {
        get { return enemyStyle; }
    }

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
    }

    /// <summary>
    /// Матрицы инстансов для хвоста и ядра.
    ///
    /// Размеры, которые раньше писались в вершины, теперь идут в
    /// масштаб матрицы. Хвост тянется по X на всю длину и по Y на
    /// свою ширину, поэтому его масштаб неравномерный; ядро
    /// квадратное и масштабируется по обеим осям одинаково, чтобы
    /// после поворота к камере остаться круглым.
    ///
    /// Матрица собирается умножением базиса на Scale, а не через
    /// Matrix4x4.TRS: TRS с неравномерным масштабом и поворотом
    /// даёт матрицу, из которой нельзя корректно восстановить
    /// нормали, а инстансинг Unity считает ещё и worldToObject.
    /// Нам transforms нужны только позиции, но лишней зависимости от
    /// этого поведения в VFX не хочется.
    /// </summary>
    internal void BuildMatrices(
        Vector3 position,
        Quaternion rotation,
        out Matrix4x4 tail,
        out Matrix4x4 core)
    {
        float lengthNow = fullLength * Retract;

        float widthNow =
            fullWidth * Mathf.Lerp(MinWidthRatio, 1f, Retract);

        float sideNow = coreSize * Retract;

        Matrix4x4 basis = Matrix4x4.TRS(position, rotation, Vector3.one);

        // Хвост: меш от -1 до 0 по X и от -0.5 до 0.5 по Y, поэтому
        // масштаб идёт полными величинами - длиной и шириной.
        tail = basis * Matrix4x4.Scale(
            new Vector3(lengthNow, widthNow, 1f));

        // Ядро: меш - единичный квад (полная сторона 1), поэтому
        // масштабом идёт сторона, а не полусторона. Иначе ядро
        // получилось бы вдвое меньше прежнего.
        core = basis * Matrix4x4.Scale(
            new Vector3(sideNow, sideNow, 1f));
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

        followTarget = followBullet ? target : null;
        following = followTarget != null;

        Retract = 1f;

        // С пулей - едем до её смерти (с потолком maxFollowLifetime).
        // Без пули - сразу короткое затухание.
        BeginPlay(
            following
                ? maxFollowLifetime
                : fadeLifetime
        );

        // Последним: к этому моменту IsPlaying уже true, и батчер
        // сразу подхватит трассер в ближайшем LateUpdate.
        TracerBatcher.Register(this);
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
                Retract = 1f;
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

        Retract = life * life;
    }

    protected override void ReturnToPool()
    {
        followTarget = null;
        following = false;

        // Снимаем с отрисовки до возврата в пул: объект переиспользуют
        // следующим же выстрелом, и на кадр между возвратом и новым
        // Play трассер не должен остаться в очереди на отрисовку.
        TracerBatcher.Unregister(this);

        if (enemyStyle)
        {
            VfxPools.EnemyTracers.Despawn(this);
            return;
        }

        VfxPools.Tracers.Despawn(this);
    }
}