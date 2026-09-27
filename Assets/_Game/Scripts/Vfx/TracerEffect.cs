using UnityEngine;

/// <summary>
/// Пулевой трассер: тонкое белое ядро с оранжевым свечением,
/// короткий хвост, который гаснет вместе с пулей.
///
/// Тот же эффект используется для снарядов врага (enemyStyle):
/// отличается только красный материал и потолок фазы Follow -
/// форма, размер и поведение остаются игрокными. Вражеские пули
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
/// Форма (белое ядро + оранжевое свечение) считается в шейдере
/// из UV квада: ни текстур, ни сэмплов. Затухание сделано через
/// localScale, а не через цвет: одна запись в transform вместо
/// пересборки меха или MaterialPropertyBlock (тот ломает SRP
/// Batcher).
/// </summary>
public sealed class TracerEffect : VfxEffect
{
    // =========================================================
    // РЕКОМЕНДУЕМЫЕ ЗНАЧЕНИЯ
    //
    // Размеры заданы от высоты экрана. Игровая камера стоит
    // в ~22.5 мировых единицах от плоскости боя при FOV 60,
    // то есть видит ~26 единиц по вертикали: на 1080p это
    // ~42 пикселя на единицу.
    //
    //   ширина 0.26  -> ~1% высоты экрана (гало), ядро ~3-4 px
    //   длина 0.075 * скорость -> при 16 у/с = 1.2 ед. = ~5%
    //
    // Если камеру поднимут или уменьшишь FOV - пересчитай:
    //   size = доля_экрана * (2 * расстояние * tan(FOV / 2))
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
    /// Длина на единицу скорости для снарядов врага. Снаряды
    /// медленные (8 у/с у дальника, 7 у/с у взрыва танка) против
    /// 14-16 у/с у пуль игрока, поэтому при общем коэффициенте
    /// вражеский хвост упирался в MinLength и был вдвое короче
    /// игрового. 0.14 при 8 у/с даёт те же ~1.12 единицы, что и
    /// игровые 0.075 при 15 у/с.
    /// </summary>
    public const float EnemyLengthPerSpeed = 0.14f;

    private const float MinWidthRatio = 0.35f;

    [Header("Visual")]
    [SerializeField, Tooltip("Ширина трассера: доля высоты экрана (0.01 = 1%)")]
    private float width = DefaultWidth;

    [SerializeField, Tooltip("Длина хвоста, если скорость не задана")]
    private float length = DefaultLength;

    [SerializeField, Tooltip("Затухание хвоста после смерти пули, с")]
    private float fadeLifetime = DefaultFadeLifetime;

    [SerializeField, Tooltip("Длина хвоста на единицу скорости")]
    private float lengthPerSpeed = LengthPerSpeed;

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

        MeshRenderer meshRenderer =
            gameObject.AddComponent<MeshRenderer>();

        VfxSharedAssets.SetupAdditiveRenderer(
            meshRenderer,
            VfxSharedAssets.StreakMesh,
            enemyStyle
                ? VfxSharedAssets.EnemyTracerMaterial
                : VfxSharedAssets.TracerMaterial
        );
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

        transform.localScale = new Vector3(
            fullLength,
            fullWidth,
            1f
        );

        followTarget = followBullet ? target : null;
        following = followTarget != null;

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

        float retract = life * life;

        transform.localScale = new Vector3(
            fullLength * retract,
            fullWidth * Mathf.Lerp(MinWidthRatio, 1f, retract),
            1f
        );
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
