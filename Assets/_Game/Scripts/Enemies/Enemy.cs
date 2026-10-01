using UnityEngine;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    [Header("Enemy Data")]
    [SerializeField] private EnemyData enemyData;

    [Header("Boss Data")]
    [SerializeField] private BossData bossData;

    [Header("References")]
    [SerializeField] private Transform player;
    [SerializeField] private GameObject enemyProjectilePrefab;
    [SerializeField] private Transform attackPoint;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private GameObject bossAttackZonePrefab;

    [Header("Damage Numbers")]
    [Tooltip("Префаб числа урона. Сам объект больше не инстанцируется на каждое попадание: DamageNumberSystem берёт его как образец вида и держит пул таких же под общим экранным канвасом.")]
    [SerializeField] private GameObject damageNumberPrefab;

    [Header("HP Bar")]
    [Tooltip("Вид полосы HP над мобом. Появляется после первого урона и держится до смерти моба. Настройки свои у каждого префаба.")]
    [SerializeField] private EnemyHealthBarSettings healthBar = new EnemyHealthBarSettings();

    [Header("Navigation")]
    [Tooltip("Дальность проверки препятствия перед движением.")]
    [SerializeField] private float obstacleProbeDistance = 1.2f;
    [Tooltip("Минимальный запас до стены при обходе.")]
    [SerializeField] private float obstaclePadding = 0.75f;
    [Tooltip("Подъём точки, из которой пускаются лучи (чтобы не цеплять собственный коллайдер).")]
    [SerializeField] private float rayHeight = 0.5f;
    [Tooltip("Скорость поворота (градусов в секунду).")]
    [SerializeField] private float rotationSpeed = 360f;
    [Tooltip("Как далеко смотрим по бокам, чтобы выбрать сторону обхода с большим зазором.")]
    [SerializeField] private float clearanceProbeDistance = 4f;
    [Tooltip("Насколько движение при обходе идёт вдоль стены (касательная), а не поперёк желаемого направления.")]
    [SerializeField] private float dodgeSteer = 0.85f;
    [Tooltip("Сколько секунд держим выбранную сторону обхода, чтобы не дёргаться от стены к стене.")]
    [SerializeField] private float sideMemoryTime = 0.4f;
    [Tooltip("Сколько кадров подряд путь должен быть свободен, чтобы выйти из обхода и довернуть за угол.")]
    [SerializeField] private int clearFramesToExit = 3;

    [Header("Knockback")]
    [Tooltip("Насколько попадание сбивает моба с разгона: 0 — не сбивает совсем, 1 — почти полная остановка. Действует в связке с сопротивлением из EnemyData.")]
    [Range(0f, 1f)]
    [SerializeField] private float knockbackSlowdown = 0.75f;
    [Tooltip("С какой скоростью моб возвращается к обычному бегу после попадания. Больше — разгоняется быстрее.")]
    [SerializeField] private float knockbackRecovery = 6f;
    [Tooltip("Как быстро гаснет сама отдача. Больше — короче откат назад, меньше — длиннее и мягче.")]
    [SerializeField] private float knockbackDamping = 9f;
    [Tooltip("Потолок скорости отдачи: залп из десятка пуль не должен уносить моба через полкарты.")]
    [SerializeField] private float knockbackMaxSpeed = 3f;

    [Header("Performance")]
    [Tooltip("Как часто обновлять дорогую проверку обхода препятствий.")]
    [SerializeField] private float navigationUpdateInterval = 0.05f;
    [Tooltip("Как часто проверять пересечение врага со структурами и игроком.")]
    [SerializeField] private float structureResolveInterval = 0.04f;
    [Tooltip("Скорость плавного досъезда к точке выталкивания — убирает скачки при редком разрешении пересечений.")]
    [SerializeField] private float resolveSlideSpeed = 6f;
    [Tooltip("Как часто проверять линию огня у дальних врагов.")]
    [SerializeField] private float lineOfSightCheckInterval = 0.10f;
    [Tooltip("Как часто дальник пересматривает, есть ли рядом танк, который его прикрывает.")]
    [SerializeField] private float supportCheckInterval = 0.25f;

    private float currentHealth;

    private int currentWave = 1;
    private float scaledMaxHealth;
    private float scaledDamageMultiplier = 1f;

    private float contactDamageTimer;
    private float attackTimer;
    private float abilityTimer;
    private float summonTimer;

    // Состояние рывка быстрых врагов.
    private bool isDashing;
    private float dashTimer;
    private float dashRemainingTime;
    private Vector3 dashDirection;

    // Замах перед рывком. Без него быстрый враг просто разгоняется
    // без предупреждения, и реакция выглядит как «моб сам по себе
    // ускорился». Здесь это честное окно, за которое игрок успевает
    // отойти или поставить между собой препятствие.
    private bool dashWindup;
    private float dashWindupRemainingTime;
    private float dashWindupTotalTime;
    private Vector3 dashWindupDirection;

    // Роль и редкий вариант. Сами роли не хранятся в ассете: это
    // честные множители поверх EnemyData, которые двигают поведение,
    // а не характеристики (никакой накрутки HP).
    private EnemyVariant variant = EnemyVariant.None;
    private float moveSpeedMultiplier = 1f;
    private float flankAngle;
    private float flankRetargetInterval = 1.6f;
    private float interceptLead;
    private float maxInterceptDistance;
    private float flankTimer;
    private int flankSide = 1;
    private float flankAmount = 1f;
    private float dashCooldownMultiplier = 1f;
    private float dashRange = 6f;
    private float dashWindupSpeedScale = 0.25f;
    private float dashTelegraphCrouch = 0.3f;

    // Дальник: держит дистанцию, отходит, переставляется, телеграфирует выстрел.
    private float preferredDistance;
    private float retreatDistance;
    private float retreatSpeedScale = 0.9f;
    private float repositionInterval = 2.2f;
    private float repositionArcDegrees = 60f;
    private float attackTelegraphCrouch = 0.18f;
    private float rangedAttackRateMultiplier = 1f;
    private float repositionTimer;
    private Vector3 repositionTarget;
    private bool hasRepositionTarget;
    private bool attackTelegraphActive;
    private float attackTelegraphRemainingTime;
    private float attackTelegraphTotalTime;
    private float supportCheckTimer;
    private bool hasTankSupport;
    private float tankSupportAttackRateMultiplier = 1f;

    // Танк: ищет ближайшего дальника и встаёт между ним и игроком.
    private float supportRadius;

    // Два разных расстояния, и их важно не путать:
    // braceRange — насколько далеко перед СОЮЗНИКОМ встать (экран);
    // braceStopRange — на каком расстоянии от ИГРОКА остановиться.
    private float braceRange;
    private float braceStopRange;
    private float allySearchInterval = 0.5f;
    private float allySearchTimer;
    private Enemy escortedAlly;

    // Приоритет цели. В игре с одним игроком выбирать «кого бить»
    // не из чего, поэтому приоритет выражен иначе: моб, у которого
    // есть роль в связке, на гибели союзника по этой роли бросает
    // свою позицию и идёт в ту точку. Это единственный выбор цели,
    // который тут вообще возможен, и он честно читается: убили
    // дальника — танк отошёл от экрана и пошёл туда.
    private float allyRevengeRadius;
    private float allyRevengeTime = 2.5f;
    private float revengeTimer;
    private Vector3 revengePosition;
    private EnemyType roleAllyType = EnemyType.Boss;
    private bool hasRoleAllyType;

    // Элита: базовая волна уходит от игрока, агрессивный вариант — под него.
    private float abilityCooldownMultiplier = 1f;
    private float abilityDpsMultiplier = 1f;
    private bool abilityTargetsPlayerPosition;

    private PlayerController playerController;

    // Живые враги одним списком. Нужен дальникам и танкам, чтобы
    // находить друг друга (экран и поддержка), и не делает лишних
    // FindObject каждый кадр. Обход идёт раз в 0.2-0.5 секунды.
    private static readonly List<Enemy> aliveEnemies =
        new List<Enemy>(128);

    // Павшие мобы. Живой реестр выше нужен, чтобы найти союзника,
    // а этот — чтобы узнать, что союзника только что убили. И то и
    // другое держится на тех же тиках, что уже были (2-4 Гц), без
    // нового поиска по сцене.
    private struct FallenAlly
    {
        public Vector3 position;
        public float time;
        public EnemyType type;
    }

        private static readonly List<FallenAlly> fallenEnemies =
        new List<FallenAlly>(32);

    // Сколько секунд павший союзник ещё «значим» для мобов, которые
    // на него реагируют. Дольше держать не нужно: за это время
    // волна всё равно дойдёт до следующей точки, и моб вернётся
    // в роль раньше, чем список успит заполниться.
    private const float fallenMemoryTime = 3f;

    // Подкраска варианта идёт через копию материала, а не через
    // MaterialPropertyBlock. Любой PropertyBlock выводит рендерер
    // из SRP Batcher, а у мобов он один на весь объект: 38 живых
    // мобов в поле — это 38 отдельных draw call вместо одного
    // батча на тип. Копий мало и они общие: по одной на пару
    // (материал префаба, вариант), всего 5 типов × 5 вариантов.
    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int LegacyColorId =
        Shader.PropertyToID("_Color");

    // material.GetInstanceID() + вариант. Вариант нужен в ключе,
    // иначе первая же подкраска закэширует цвет для всех.
    private static readonly Dictionary<int, Material>
        variantMaterialCache = new Dictionary<int, Material>(32);

    private int aliveRegistryIndex = -1;

    private Vector3 baseScale = Vector3.one;
    private Renderer variantRenderer;
    private Material baseVariantMaterial;

    // Отдача от попадания. Это не смещение и не импульс физики,
    // а скорость, которую каждый кадр добавляем к позиции и
    // экспоненциально гасим. Разгон при этом умножается на
    // knockbackSpeedScale, поэтому моб не улетает, а сначала
    // сбавляет, потом снова разгоняется к игроку.
    private Vector3 knockbackVelocity;
    private float knockbackSpeedScale = 1f;

    private Collider selfCollider;
    private Rigidbody cachedRigidbody;
    private PlayerHealth playerHealth;

    // Точка плавного выталкивания, к которой враг досъезжает каждый кадр.
    private Vector3 resolveSlideTarget;
    private bool hasResolveSlideTarget;
    private Collider playerCollider;

    private EnemyType cachedEnemyType;
    private bool isBoss;

    // Поиск игрока (страховка) идёт не чаще раза в это время.
    private float nextPlayerSearchTime;

    // Состояние обхода препятствий.
    private int avoidSide;
    private float avoidSideTimer;
    private int clearFrames;

    // Expensive physics checks are rate-limited; movement still runs every frame.
    private float navigationTimer;
    private float structureResolveTimer;
    private float lineOfSightTimer;
    private Vector3 cachedMoveDirection = Vector3.forward;
    private bool hasCachedMoveDirection;
    private bool cachedLineOfSight;

    // Состояние периодического урона (burn/bleed). Без корутин,
    // чтобы не аллоцировать WaitForSeconds и машины состояний.
    private bool burnActive;
    private float burnDamagePerTick;
    private float burnRemainingTime;
    private float burnTickInterval;
    private float burnTickTimer;

    // Предыдущее состояние горения: нужно, чтобы поймать
    // переход и зажечь огонь один раз, а не каждый кадр.
    private bool wasBurning;

    private bool bleedActive;
    private float bleedDamagePerTick;
    private float bleedRemainingTime;
    private float bleedTickInterval;
    private float bleedTickTimer;
    private int bleedStacks;

    /// <summary>
    /// Сколько раз кровотечение складывается на одном враге.
    /// Без потолка быстрая стрельба разгоняла бы DoT в
    /// произвольные значения.
    /// </summary>
    private const int MaxBleedStacks = 4;

    // Количество живых врагов в сцене. Позволяет волновому
    // менеджеру обходиться без FindGameObjectsWithTag каждый кадр.
    public static int AliveCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        AliveCount = 0;

        if (aliveEnemies != null)
            aliveEnemies.Clear();

        if (fallenEnemies != null)
            fallenEnemies.Clear();
    }

    private void OnDestroy()
    {
        if (AliveCount > 0)
            AliveCount--;

        UnregisterAlive();

        ColliderKindQuery.UnregisterEnemy(transform);
        EnemyHealthBarSystem.Unregister(this);
    }

    public bool IsDead { get; private set; }

    /// <summary>
    /// Горит ли моб сейчас. Читает BurnFlameEffect, чтобы огонь
    /// сам знал, когда погаснуть.
    /// </summary>
    public bool IsBurning => burnActive;

    public float CurrentHealth => currentHealth;

    public EnemyData GetEnemyData()
    {
        return enemyData;
    }

    public float MaxHealth => scaledMaxHealth;

    public int SpawnWave => currentWave;

    public int BossPhase { get; private set; } = 1;

    /// <summary>
    /// Имя босса из его ассета BossData. Пустое у обычных мобов.
    /// </summary>
    public string BossDisplayName =>
        bossData != null
            ? bossData.BossName
            : string.Empty;

    /// <summary>
    /// Редкий вариант этого моба. None — обычный моб без выкрутки.
    /// </summary>
    public EnemyVariant Variant => variant;

    /// <summary>
    /// Полоса HP этого моба. Вид задаётся на префабе, сама
    /// система полос читает настройки отсюда.
    /// </summary>
    public EnemyHealthBarSettings HealthBarSettings => healthBar;

    /// <summary>
    /// Полоса HP появляется после первого урона и не прячется до
    /// смерти моба.
    ///
    /// Именно флаг, а не сравнение CurrentHealth == MaxHealth:
    /// при частом уроне (миниган, горение) здоровье на кадр
    /// успевает вернуться к полному, и полоса мигала бы.
    /// </summary>
    public bool HasTakenDamage { get; private set; }

    // Служебное: связь с EnemyHealthBarSystem - индекс в реестре
    // мобов и привязанная полоса. Руками не трогать.
    internal int HealthBarRegistryIndex = -1;
    internal EnemyHealthBarView HealthBarView;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        FindPlayer();

        selfCollider = GetComponent<Collider>();
        cachedRigidbody = GetComponent<Rigidbody>();

        ColliderKindQuery.RegisterEnemy(transform);

        cachedEnemyType = enemyData != null ? enemyData.EnemyType : EnemyType.Normal;
        isBoss = enemyData != null && cachedEnemyType == EnemyType.Boss;

        scaledMaxHealth = GetBaseMaxHealth();
        currentHealth = scaledMaxHealth;

        if (attackPoint == null)
            attackPoint = transform;

        if (enemySpawner == null)
            enemySpawner = FindAnyObjectByType<EnemySpawner>();

        AliveCount++;

        RegisterAlive();

        EnemyHealthBarSystem.Register(this);

        LockVerticalRigidbody();

        CacheVisualSetup();
    }

    // Всё, что нужно поведению, но не характеристики: базовый
    // масштаб (телеграфы приседают и возвращают его назад), рендерер
    // для подкраски редкого варианта и контроллер игрока для перехвата.
    private void CacheVisualSetup()
    {
        baseScale = transform.localScale;

        variantRenderer = GetComponentInChildren<MeshRenderer>();

        if (player == null)
            return;

        playerController =
            player.GetComponentInParent<PlayerController>();
    }

    private void RegisterAlive()
    {
        if (aliveRegistryIndex >= 0)
            return;

        aliveRegistryIndex = aliveEnemies.Count;

        aliveEnemies.Add(this);
    }

    // Удаление свапом с последним элементом: список живых врагов
    // может перебирать кто угодно в этом же кадре, поэтому список
    // не должен «съезжать» и оставлять пустые элементы.
    private void UnregisterAlive()
    {
        int index = aliveRegistryIndex;

        if (index < 0)
            return;

        aliveRegistryIndex = -1;

        int lastIndex = aliveEnemies.Count - 1;

        if (index < lastIndex)
        {
            Enemy moved = aliveEnemies[lastIndex];

            aliveEnemies[index] = moved;
            moved.aliveRegistryIndex = index;
        }

        aliveEnemies.RemoveAt(lastIndex);
    }

    // Враги двигаются через transform (скрипт), а не через физику.
    // Некинематическое тело с нулевым damping подхватывает скорость от
    // толчков (игрок/соседний моб), она никогда не гаснет, и враг начинает
    // бесконечно скользить в одну сторону. Кинематическое тело убирает
    // это: солвер не пишет ему скорость, а триггеры (пули, KillZone) и
    // контактный урон с динамическим игроком продолжают работать.
    private void LockVerticalRigidbody()
    {
        if (cachedRigidbody == null)
            return;

        cachedRigidbody.isKinematic = true;

        cachedRigidbody.constraints |=
            RigidbodyConstraints.FreezePositionY;
    }

    // Страховка от остаточной скорости, записанной до перевода в
    // кинематический режим.
    private void FixedUpdate()
    {
        if (cachedRigidbody == null)
            return;

        if (cachedRigidbody.linearVelocity.sqrMagnitude > 0f)
            cachedRigidbody.linearVelocity = Vector3.zero;

        if (cachedRigidbody.angularVelocity.sqrMagnitude > 0f)
            cachedRigidbody.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// Спавн моба. Модификатор волны приходит сюда только как
    /// контекст: сама система волн не трогается, моб лишь усиливает
    /// уже существующую роль в рамках уже существующего модификатора.
    /// </summary>
    public void Initialize(
        int wave,
        WaveModifier modifier = WaveModifier.None)
    {
        currentWave = Mathf.Max(wave, 1);

        CacheRoleParameters();

        RollVariant();

        ApplyVariantTuning();

        ApplyModifierRoleContext(modifier);

        ApplyWaveScaling();

        CacheRoleAllyType();

        ApplyFirstCastGrace();
    }

    // Копия нужных ролевых чисел в поля моба. EnemyData — общий
    // ассет, поэтому множить и менять его нельзя: так вариант или
    // модификатор волны изменили бы всех мобов этого типа сразу.
    private void CacheRoleParameters()
    {
        moveSpeedMultiplier = 1f;
        flankAngle = 0f;
        interceptLead = 0f;
        maxInterceptDistance = 0f;
        dashCooldownMultiplier = 1f;
        dashRange = 6f;
        dashWindupSpeedScale = 0.25f;
        dashTelegraphCrouch = 0.3f;

        preferredDistance = 0f;
        retreatDistance = 0f;
        retreatSpeedScale = 0.9f;
        repositionInterval = 2.2f;
        repositionArcDegrees = 60f;
        attackTelegraphCrouch = 0.18f;
        rangedAttackRateMultiplier = 1f;

        supportRadius = 0f;
        braceRange = 0f;
        braceStopRange = 0f;
        allySearchInterval = 0.5f;
        allyRevengeRadius = 0f;
        allyRevengeTime = 2.5f;
        revengeTimer = 0f;

        abilityCooldownMultiplier = 1f;
        abilityDpsMultiplier = 1f;
        abilityTargetsPlayerPosition = false;

        supportCheckTimer = 0f;
        hasTankSupport = false;
        tankSupportAttackRateMultiplier = 1f;

        hasRepositionTarget = false;
        attackTelegraphActive = false;
        dashWindup = false;

        if (enemyData == null)
            return;

        flankAngle = enemyData.FlankAngle;
        flankRetargetInterval = enemyData.FlankRetargetInterval;
        interceptLead = enemyData.InterceptLead;
        maxInterceptDistance = enemyData.MaxInterceptDistance;

        dashRange = enemyData.DashRange;
        dashWindupSpeedScale = enemyData.DashWindupSpeedScale;
        dashTelegraphCrouch = enemyData.DashTelegraphCrouch;

        preferredDistance = enemyData.PreferredDistance;
        retreatDistance = enemyData.RetreatDistance;
        retreatSpeedScale = enemyData.RetreatSpeedScale;
        repositionInterval = enemyData.RepositionInterval;
        repositionArcDegrees = enemyData.RepositionArcDegrees;
        attackTelegraphCrouch = enemyData.AttackTelegraphCrouch;

        supportRadius = enemyData.SupportRadius;
        braceRange = enemyData.BraceRange;

        // Остановка выводится из собственной дальности урона танка,
        // а не задаётся в метрах. Раньше тут стояли 3.4 м, а контактный
        // урон танка достаёт до 2.06 м: танк останавливался дальше
        // собственного радиуса и становился безобидным, пока игрок
        // сам не подходил. Доля от GetContactRange() исключает такую
        // рассинхронизацию по построению.
        braceStopRange =
            GetContactRange() * enemyData.BraceStopScale;

        allySearchInterval = enemyData.AllySearchInterval;

        allyRevengeRadius = enemyData.AllyRevengeRadius;
        allyRevengeTime = enemyData.AllyRevengeTime;

        abilityDpsMultiplier = enemyData.AbilityDpsMultiplier;
    }

    // У кого есть «свой» союзник по роли, тот на его гибель
    // реагирует. Танк держит экран для дальника — значит и потеря
    // дальника его касается. Дальник прячется за танком — тоже.
    // Остальным типам (Normal, Fast, Elite, Boss) союзника по роли
    // нет, поэтому и приоритета цели у них нет.
    private void CacheRoleAllyType()
    {
        hasRoleAllyType = true;

        switch (cachedEnemyType)
        {
            case EnemyType.Tank:
                roleAllyType = EnemyType.Ranged;
                break;

            case EnemyType.Ranged:
                roleAllyType = EnemyType.Tank;
                break;

            default:
                hasRoleAllyType = false;
                break;
        }
    }

    // Вариант один раз за жизнь моба. Шанс и минимальную волну
    // берём из его же EnemyData, боссу варианты не выпадают.
    private void RollVariant()
    {
        variant = EnemyVariant.None;

        if (enemyData == null || isBoss)
            return;

        if (enemyData.VariantChance <= 0f)
            return;

        if (currentWave < enemyData.VariantMinWave)
            return;

        if (Random.value > enemyData.VariantChance)
            return;

        switch (cachedEnemyType)
        {
            case EnemyType.Fast:
                variant = EnemyVariant.Charger;
                break;

            case EnemyType.Ranged:
                variant = EnemyVariant.Mobile;
                break;

            case EnemyType.Tank:
                variant = EnemyVariant.Bulwark;
                break;

            case EnemyType.Elite:
                variant = EnemyVariant.Aggressive;
                break;

            default:
                return;
        }

        ApplyVariantTint();
    }

    // Вариант меняет поведение теми же ручками, что и обычный моб:
    // тот же рывок, та же дистанция, та же зона. Ни одна из них не
    // трогает HP, поэтому «страшный» враг остаётся честно убиваемым.
    private void ApplyVariantTuning()
    {
        switch (variant)
        {
            case EnemyVariant.Charger:
                // Рывок с более длинным разбегом, но и с более
                // коротким замахом: чаще, но всё ещё читаемо.
                dashRange *= 1.3f;
                dashCooldownMultiplier *= 0.55f;
                dashWindupSpeedScale *= 0.6f;
                interceptLead *= 1.25f;
                flankAngle = Mathf.Max(flankAngle, 18f);
                break;

            case EnemyVariant.Mobile:
                // Держится ближе и ходит по кругу шире.
                preferredDistance *= 0.8f;
                retreatDistance *= 0.8f;
                repositionInterval *= 0.55f;
                repositionArcDegrees *= 1.5f;
                rangedAttackRateMultiplier *= 0.85f;
                break;

            case EnemyVariant.Bulwark:
                // Шире экран и встаёт насмерть, а не продавливает.
                // Умножается именно экран (дистанция до игрока
                // остаётся собственной дальностью урона), иначе
                // вариант снова встал бы вне своего радиуса.
                supportRadius *= 1.5f;
                braceRange *= 1.3f;
                break;

            case EnemyVariant.Aggressive:
                // Зона уходит под игрока, каст чаще, сам быстрее.
                abilityTargetsPlayerPosition = true;
                abilityCooldownMultiplier *= 0.7f;
                abilityDpsMultiplier *= 1.2f;
                moveSpeedMultiplier *= 1.25f;
                break;
        }
    }

    // Редкий вариант должен читаться до того, как игрок поймёт по
    // поведению, что моб особенный. Подкраска идёт через общий
    // кэшированный материал варианта, а не через PropertyBlock:
    // PropertyBlock выводит рендерер из SRP Batcher, а у моба он
    // один на весь объект — 38 живых мобов стали бы 38 draw call.
    // Обычные мобы (без варианта) работают на материале префаба
    // и не создают вообще ничего.
    private void ApplyVariantTint()
    {
        if (variantRenderer == null)
            return;

        // Ключ и базовый цвет всегда берём у материала префаба,
        // а не у текущего sharedMaterial: иначе повторный вызов
        // красил бы уже подкрашенную копию и гонял свет в два раза.
        Material baseMaterial = baseVariantMaterial;

        if (baseMaterial == null)
        {
            baseMaterial = variantRenderer.sharedMaterial;

            if (baseMaterial == null)
                return;

            baseVariantMaterial = baseMaterial;
        }

        // Без варианта моб работает ровно на материале префаба:
        // ни своей копии, ни PropertyBlock, ни единой лишней работы.
        variantRenderer.sharedMaterial =
            variant == EnemyVariant.None
                ? baseMaterial
                : GetVariantMaterial(baseMaterial, variant);
    }

    // Копия материала на пару (материал, вариант), общая для всех
    // мобов этого сочетания. Копий получается 5 типов × 4 редких
    // варианта = 20 за забег, а не по одной на каждого моба.
    private static Material GetVariantMaterial(
        Material material,
        EnemyVariant target)
    {
        int key = material.GetInstanceID() * 31 + (int)target;

        if (variantMaterialCache.TryGetValue(key, out Material cached) &&
            cached != null)
        {
            return cached;
        }

        Color baseColor = Color.white;

        if (material.HasProperty(BaseColorId))
            baseColor = material.GetColor(BaseColorId);
        else if (material.HasProperty(LegacyColorId))
            baseColor = material.GetColor(LegacyColorId);

        Color tint = GetVariantTint(target);

        Color tinted = new Color(
            baseColor.r * tint.r,
            baseColor.g * tint.g,
            baseColor.b * tint.b,
            baseColor.a
        );

        Material tintedCopy = new Material(material)
        {
            name = material.name + " (" + target + ")"
        };

        if (tintedCopy.HasProperty(BaseColorId))
            tintedCopy.SetColor(BaseColorId, tinted);
        if (tintedCopy.HasProperty(LegacyColorId))
            tintedCopy.SetColor(LegacyColorId, tinted);

        // Копия обязана оставаться инстансируемой, иначе подкрашенный
        // вариант выпадает из общего батча не хуже, чем PropertyBlock.
        tintedCopy.enableInstancing = true;

        variantMaterialCache[key] = tintedCopy;

        return tintedCopy;
    }

    private static Color GetVariantTint(EnemyVariant target)
    {
        switch (target)
        {
            case EnemyVariant.Charger:
                return new Color(1.3f, 0.82f, 0.55f);

            case EnemyVariant.Mobile:
                return new Color(0.7f, 1.1f, 1.3f);

            case EnemyVariant.Bulwark:
                return new Color(0.78f, 0.88f, 1.35f);

            case EnemyVariant.Aggressive:
                return new Color(1.4f, 0.6f, 0.55f);

            default:
                return Color.white;
        }
    }

    // Модификаторы волн уже есть и уже выбирают, кого и сколько
    // ставить. Здесь они только усиливают роль, чтобы волна
    // «Рывок» реально читалась как быстрые, «Засада» — как заход
    // с флангов, а «Стальная стена» — как плотная связка танка и
    // дальника. Новых модификаторов и составов не добавляется.
    private void ApplyModifierRoleContext(WaveModifier modifier)
    {
        if (!modifier.Has(WaveModifier.FastAssault) &&
            !modifier.Has(WaveModifier.RangedAssault) &&
            !modifier.Has(WaveModifier.Ambush) &&
            !modifier.Has(WaveModifier.DangerZone) &&
            !modifier.Has(WaveModifier.EliteHunt) &&
            !modifier.Has(WaveModifier.LastStand))
        {
            return;
        }

        switch (cachedEnemyType)
        {
            case EnemyType.Fast:
                if (modifier.Has(WaveModifier.FastAssault))
                {
                    dashCooldownMultiplier *= 0.8f;
                    interceptLead *= 1.35f;
                    flankAngle *= 1.25f;
                }

                if (modifier.Has(WaveModifier.Ambush))
                    flankAngle *= 1.4f;

                break;

            case EnemyType.Ranged:
                if (modifier.Has(WaveModifier.RangedAssault))
                {
                    repositionInterval *= 0.6f;
                    repositionArcDegrees *= 1.2f;
                }

                if (modifier.Has(WaveModifier.DangerZone))
                    preferredDistance += 1.5f;

                break;

            case EnemyType.Tank:
                if (modifier.Has(WaveModifier.LastStand))
                {
                    supportRadius *= 1.3f;
                    braceRange *= 1.2f;
                }
                break;

            case EnemyType.Elite:
                if (modifier.Has(WaveModifier.EliteHunt))
                    abilityCooldownMultiplier *= 0.75f;

                break;
        }
    }

    // Элита без подготовки успевала кастовать зону ещё не дойдя до
    // игрока. Первый каст откладываем так, чтобы игрок сначала
    // увидел её и успело отойти — дальше ритм уже честный, с
    // предупреждением перед уроном.
    private void ApplyFirstCastGrace()
    {
        if (cachedEnemyType == EnemyType.Boss &&
            bossData != null)
        {
            float bossGrace =
                Mathf.Max(
                    bossData.Phase1AbilityWarningDuration + 0.8f,
                    bossData.Phase1AbilityCooldown * 0.5f
                );

            abilityTimer = bossGrace;
            summonTimer = bossGrace;
            attackTimer = bossGrace;

            return;
        }

        if (cachedEnemyType != EnemyType.Elite ||
            enemyData == null)
        {
            return;
        }

        float grace =
            Mathf.Max(
                enemyData.AbilityWarning + 0.6f,
                enemyData.AbilityCooldown * 0.5f
            );

        abilityTimer = grace * abilityCooldownMultiplier;
    }

    private void ApplyWaveScaling()
    {
        float healthMultiplier =
            1f + (currentWave - 1) * GetWaveHealthPercent();

        float damageMultiplier =
            1f + (currentWave - 1) * GetWaveDamagePercent();

        // Поверх плавного роста по номеру волны накладывается ритм
        // «лёгкая / сложная»: он не добавляет сложности в среднем, но
        // делает соседние волны заметно разными. Без него апгрейд,
        // взятый после сложной волны, не читался — следующая волна была
        // ровно такой же.
        float tierMultiplier =
            WaveDifficulty.GetHealthMultiplier(
                currentWave,
                isBoss
            );

        scaledMaxHealth =
            GetBaseMaxHealth() *
            healthMultiplier *
            tierMultiplier;

        // Тот же уровень волны, но для урона множитель свой: смерть от
        // снаряда читается хуже долгого боя, поэтому на сложной волне
        // урон уходит выше единицы сильнее, чем здоровье.
        damageMultiplier *=
            WaveDifficulty.GetDamageMultiplier(
                currentWave,
                isBoss
            );

        scaledDamageMultiplier = damageMultiplier;

        currentHealth = scaledMaxHealth;
    }

    private float GetBaseMaxHealth()
    {
        if (enemyData != null &&
            cachedEnemyType == EnemyType.Boss &&
            bossData != null)
        {
            return bossData.MaxHealth;
        }

        return enemyData != null
            ? enemyData.MaxHealth
            : 0f;
    }

    private float GetWaveHealthPercent()
    {
        if (enemyData != null &&
            cachedEnemyType == EnemyType.Boss &&
            bossData != null)
        {
            return bossData.WaveHealthPercent;
        }

        return enemyData != null
            ? enemyData.WaveHealthPercent
            : 0f;
    }

    private float GetWaveDamagePercent()
    {
        if (enemyData != null &&
            cachedEnemyType == EnemyType.Boss &&
            bossData != null)
        {
            return bossData.WaveDamagePercent;
        }

        return enemyData != null
            ? enemyData.WaveDamagePercent
            : 0f;
    }

    private float GetWaveScorePercent()
    {
        if (enemyData != null &&
            cachedEnemyType == EnemyType.Boss &&
            bossData != null)
        {
            return bossData.WaveScorePercent;
        }

        return enemyData != null
            ? enemyData.WaveScorePercent
            : 0f;
    }


    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (IsDead)
            return;

        if (player == null)
        {
            if (Time.time >= nextPlayerSearchTime)
            {
                nextPlayerSearchTime = Time.time + 0.25f;
                FindPlayer();
            }

            if (player == null)
                return;
        }

        Vector3 startPosition =
            transform.position;

        UpdateTimers();

        if (enemyData == null)
        {
            MoveTowardsPlayer();

            ApplyKnockbackMotion();

            ApplyResolveSlide();

            TryApplyContactDamage();

            // Превентивное скольжение по игроку (как стене).
            ApplyPreventivePlayerSlide(startPosition);

            return;
        }

        switch (cachedEnemyType)
        {
            case EnemyType.Ranged:
                HandleRangedBehaviour();
                break;

            case EnemyType.Fast:
                HandleFastBehaviour();
                break;

            case EnemyType.Tank:
                HandleTankBehaviour();
                break;

            case EnemyType.Elite:
                HandleEliteBehaviour();
                break;

            case EnemyType.Boss:

                if (bossData != null)
                {
                    UpdateBossPhase();
                    HandleBossBehaviour();
                }
                else
                {
                    MoveTowardsPlayer();
                }

                break;

            default:
                MoveTowardsPlayer();
                break;
        }

        ApplyKnockbackMotion();

        ApplyResolveSlide();

        TryApplyContactDamage();

        // Превентивное скольжение по игроку (как стене).
        ApplyPreventivePlayerSlide(startPosition);
    }


    // =========================================================
    // TIMERS
    // =========================================================

    private void UpdateTimers()
    {
        float deltaTime = Time.deltaTime;

        if (contactDamageTimer > 0f)
            contactDamageTimer -= deltaTime;

        if (attackTimer > 0f)
            attackTimer -= deltaTime;

        if (abilityTimer > 0f)
            abilityTimer -= deltaTime;

        if (summonTimer > 0f)
            summonTimer -= deltaTime;

        if (navigationTimer > 0f)
            navigationTimer -= deltaTime;

        if (structureResolveTimer > 0f)
            structureResolveTimer -= deltaTime;

        if (lineOfSightTimer > 0f)
            lineOfSightTimer -= deltaTime;

        if (avoidSideTimer > 0f)
            avoidSideTimer -= deltaTime;

        if (dashTimer > 0f)
            dashTimer -= deltaTime;

        if (flankTimer > 0f)
            flankTimer -= deltaTime;

        if (repositionTimer > 0f)
            repositionTimer -= deltaTime;

        if (supportCheckTimer > 0f)
            supportCheckTimer -= deltaTime;

        if (allySearchTimer > 0f)
            allySearchTimer -= deltaTime;

        if (revengeTimer > 0f)
            revengeTimer -= deltaTime;

        UpdateDoT(deltaTime);
    }

    // Периодический урон без корутин: тик каждые burn/bleedTickInterval,
    // пока не выйдет время действия.
    private void UpdateDoT(float deltaTime)
    {
        if (burnActive)
        {
            burnRemainingTime -= deltaTime;

            if (burnRemainingTime <= 0f)
            {
                burnActive = false;
            }
            else
            {
                burnTickTimer -= deltaTime;

                while (burnTickTimer <= 0f)
                {
                    burnTickTimer += burnTickInterval;
                    TakeDamage(burnDamagePerTick, false);

                    if (IsDead)
                        break;
                }
            }
        }

        UpdateBurnFlames();

        if (bleedActive)
        {
            bleedRemainingTime -= deltaTime;

            if (bleedRemainingTime <= 0f)
            {
                bleedActive = false;
                bleedStacks = 0;
            }
            else
            {
                bleedTickTimer -= deltaTime;

                while (bleedTickTimer <= 0f)
                {
                    bleedTickTimer += bleedTickInterval;
                    TakeDamage(bleedDamagePerTick, false);

                    if (IsDead)
                        break;
                }
            }
        }
    }

    /// <summary>
    /// Огонь на мобе появляется ровно один раз - на переходе
    /// «не горит → горит». Повторные поджоги просто продлевают
    /// горение (ApplyBurn перезаписывает таймер), а эффект сам
    /// следит за IsBurning и гаснет, когда горение кончилось
    /// или моб умер. Поэтому здесь не нужно ни хранить ссылку
    /// на эффект, ни останавливать его в Die().
    /// </summary>
    private void UpdateBurnFlames()
    {
        if (burnActive == wasBurning)
            return;

        wasBurning = burnActive;

        if (burnActive)
            VfxFactory.SpawnBurnFlames(this);
    }


    // =========================================================
    // PLAYER
    // =========================================================

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
            playerCollider =
                playerObject.GetComponentInChildren<Collider>();
            playerHealth =
                playerObject.GetComponentInChildren<PlayerHealth>();

            ColliderKindQuery.SetPlayerRoot(playerObject.transform);
        }
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void MoveTowardsPlayer()
    {
        if (player == null)
            return;

        Vector3 desired =
            player.position - transform.position;

        desired.y = 0f;

        if (desired.sqrMagnitude <= 0.01f)
            return;

        // Полная дистанция нужна только быстрым врагам для проверки
        // рывка — обычные не платят за лишний sqrt каждый кадр.
        float distanceToPlayer =
            cachedEnemyType == EnemyType.Fast
                ? Mathf.Sqrt(desired.sqrMagnitude)
                : 0f;

        // Перехват меняет только точку, куда бьёт рывок: он
        // целится туда, куда игрок доедет, а не туда, где стоит.
        Vector3 facing = desired;

        if (distanceToPlayer > 0f)
            facing = GetInterceptDirection(desired, distanceToPlayer);
        else
            facing.Normalize();

        TryStartDash(facing, distanceToPlayer);

        if (isDashing)
        {
            ApplyDash();
            return;
        }

        ApplyFlank(ref facing);

        MoveInDirection(facing, 1f);
    }

    // Единственное место, где моб реально двигается по своей
    // воле. Кэш направления и таймеры разрешения пересечений — те
    // же, что были раньше, просто вынесены сюда, потому что теперь
    // по направлению идут все роли, а не только преследование.
    private void MoveInDirection(
        Vector3 direction,
        float speedScale)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        float speed =
            (enemyData != null
                ? enemyData.MoveSpeed
                : 2f) *
            moveSpeedMultiplier *
            speedScale *
            knockbackSpeedScale;

        Vector3 moveDirection;

        if (!hasCachedMoveDirection || navigationTimer <= 0f)
        {
            moveDirection = AvoidObstacles(direction);
            cachedMoveDirection = moveDirection;
            hasCachedMoveDirection = true;
            navigationTimer = Mathf.Max(navigationUpdateInterval, 0.01f);
        }
        else
        {
            moveDirection = cachedMoveDirection;
        }

        transform.position +=
            moveDirection *
            speed *
            Time.deltaTime;

        if (structureResolveTimer <= 0f)
        {
            ResolveStructureOverlap();
            structureResolveTimer = Mathf.Max(structureResolveInterval, 0.02f);
        }

        RotateTowards(moveDirection);
    }

    // Подход не в лоб, а с небольшим уходом в сторону. Без этого
    // толпа слипается в одну линию и выглядит одним мобом: игрок
    // видит одну цель вместо нескольких. Угол и сторона выбираются
    // раз в flankRetargetInterval, иначе моб дёргается на месте.
    private void ApplyFlank(ref Vector3 direction)
    {
        if (flankAngle <= 0.5f)
            return;

        if (flankTimer <= 0f)
        {
            flankTimer = flankRetargetInterval;

            flankSide = Random.value < 0.5f ? -1 : 1;

            flankAmount = Random.Range(0.4f, 1f);

            // Смена стороны обязана сбросить кэш иначе до него ещё
            // доедут старым курсом.
            hasCachedMoveDirection = false;
        }

        Vector3 lateral = GetLateral(direction, flankSide);

        // Slerp между двумя единичными векторами идёт ровно по углу,
        // поэтому flankAngle градусов и даёт столько же градусов
        // отклонения от прямой линии.
        direction = Vector3.Slerp(
            direction,
            lateral,
            Mathf.Clamp01(
                flankAngle * flankAmount / 90f
            )
        );
    }

    // Перехват: быстрый враг целится не в текущую точку игрока,
    // а в точку впереди по его вектору бега. Из-за этого нельзя
    // просто бежать от него по прямой — выход вбок сбивает
    // предсказание. Смещение ограничено, чтобы моб не улетал за
    // угол и не «срезал» через стену.
    private Vector3 GetInterceptDirection(
        Vector3 toPlayer,
        float distance)
    {
        if (interceptLead <= 0.01f ||
            playerController == null)
        {
            return toPlayer.normalized;
        }

        Vector3 lead = playerController.PlanarVelocity * interceptLead;

        lead.y = 0f;

        float limit = Mathf.Min(
            Mathf.Max(maxInterceptDistance, 0f),
            distance
        );

        if (lead.sqrMagnitude > limit * limit)
            lead = lead.normalized * limit;

        Vector3 aimed = player.position + lead;

        Vector3 direction = aimed - transform.position;

        direction.y = 0f;

        return direction.sqrMagnitude > 0.0001f
            ? direction.normalized
            : toPlayer.normalized;
    }


    // =========================================================
    // FAST DASH
    // =========================================================

    // // Быстрые враги не просто гонятся — вблизи бросаются рывком.
    // Схема теперь трёхшаговая: замах (враг приседает, его видно,
    // линия огня по нему честная), рывок, перезарядка. Раньше рывок
    // начинался мгновенно, и реакция выглядела как «моб просто
    // ускорился», за которую игрок не отвечает.
    private void HandleFastBehaviour()
    {
        if (isDashing)
        {
            ApplyDash();
            return;
        }

        if (dashWindup)
        {
            HandleDashWindup();
            return;
        }

        MoveTowardsPlayer();
    }

    // Замах: продолжаем следить за игроком (рывок пойдёт туда, куда
    // он окажется), но двигаемся втрое медленнее и приседаем.
    // Это и есть честное окно реакции: моб опасен, но его видно и
    // от него можно уйти.
    private void HandleDashWindup()
    {
        if (player != null)
        {
            Vector3 toPlayer =
                player.position - transform.position;

            toPlayer.y = 0f;

            if (toPlayer.sqrMagnitude > 0.01f)
                dashWindupDirection = toPlayer.normalized;
        }

        dashWindupRemainingTime -= Time.deltaTime;

        ApplyCrouchPose(
            dashWindupRemainingTime,
            dashWindupTotalTime,
            dashTelegraphCrouch
        );

        if (dashWindupRemainingTime > 0f)
        {
            MoveInDirection(
                dashWindupDirection,
                dashWindupSpeedScale
            );

            return;
        }

        dashWindup = false;
        transform.localScale = baseScale;

        isDashing = true;
        dashDirection = dashWindupDirection;

        dashRemainingTime =
            enemyData != null
                ? enemyData.DashDuration
                : 0.25f;

        dashTimer =
            dashCooldownMultiplier *
            (enemyData != null
                ? enemyData.DashCooldown
                : 2.8f);
    }

    private void TryStartDash(
        Vector3 direction,
        float distanceToPlayer)
    {
        if (isDashing || dashWindup)
            return;

        if (cachedEnemyType != EnemyType.Fast)
            return;

        if (enemyData == null)
            return;

        if (dashTimer > 0f)
            return;

        if (distanceToPlayer > dashRange)
            return;

        // Вплотную рывок не нужен: там и так работает контактный
        // урон, а телеграф на таком расстоянии не читается.
        if (distanceToPlayer < enemyData.DashMinRange)
            return;

        dashWindup = true;

        dashWindupDirection = direction;

        dashWindupTotalTime = enemyData.DashWarningTime;

        dashWindupRemainingTime = dashWindupTotalTime;

        if (dashWindupTotalTime <= 0f)
        {
            dashWindup = false;
            isDashing = true;
            dashDirection = direction;
            dashRemainingTime = enemyData.DashDuration;
            dashTimer = dashCooldownMultiplier * enemyData.DashCooldown;
        }
    }

    private void ApplyDash()
    {
        dashRemainingTime -= Time.deltaTime;

        if (dashRemainingTime <= 0f)
        {
            isDashing = false;
            return;
        }


        float dashSpeed =
            (enemyData != null
                ? enemyData.MoveSpeed *
                  enemyData.DashSpeedMultiplier
                : 14f) *
            knockbackSpeedScale;

        transform.position +=
            dashDirection *
            dashSpeed *
            Time.deltaTime;

        if (structureResolveTimer <= 0f)
        {
            ResolveStructureOverlap();
            structureResolveTimer = Mathf.Max(structureResolveInterval, 0.02f);
        }

        RotateTowards(dashDirection);
    }

    // =========================================================
    // SMOOTH ROTATION
    // =========================================================

    private void RotateTowards(Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        Quaternion targetRotation =
            Quaternion.LookRotation(direction);

        transform.rotation =
            Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime
            );
    }

    // Телеграф через масштаб: замах виден даже в углу экрана и
    // даже если моб за стеной. Только transform, без VFX и без
    // аллокаций — масштаб возвращается к базовому, когда замах
    // закончился.
    private void ApplyCrouchPose(
        float remaining,
        float total,
        float amount)
    {
        if (total <= 0f || amount <= 0f)
            return;

        float progress =
            Mathf.Clamp01(remaining / total);

        transform.localScale =
            baseScale *
            (1f - amount * progress);
    }


    // =========================================================
    // KNOCKBACK
    // =========================================================

    /// <summary>
    /// Реакция на попадание. Моб не телепортируется: удар
    /// добавляет к его позиции скорость, которая каждый кадр
    /// экспоненциально гаснет, и разом сбивает разгон. Поэтому
    /// движение читается как «задел — сбавил — снова побежал»,
    /// а не как короткий рывок телепортом.
    ///
    /// Сила приходит от попадания (см. Bullet), а насколько моб
    /// устойчив к ней — из его EnemyData: у босса, элиты и
    /// танка сопротивление равно 1, и попадание не двигает их
    /// вообще.
    /// </summary>
    public void ApplyKnockback(
        Vector3 direction,
        float force)
    {
        if (IsDead)
            return;

        if (force <= 0f)
            return;

        float resistance =
            GetKnockbackResistance();

        // Иммунный моб не должен дёрнуться даже на пиксель:
        // заметное смещение выдало бы «неотталкиваемость».
        if (resistance >= 1f)
            return;

        direction.y = 0f;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        knockbackVelocity +=
            direction.normalized *
            force *
            (1f - resistance);

        // Потолок держит залп дробовика: десяток пуль за кадр
        // складывается в скорость, уносящую моб через полкарты.
        float maxSpeed =
            Mathf.Max(knockbackMaxSpeed, 0.01f);

        if (knockbackVelocity.sqrMagnitude > maxSpeed * maxSpeed)
            knockbackVelocity =
                knockbackVelocity.normalized * maxSpeed;

        // Минимум, а не присваивание: пока моб не разогнался
        // обратно, новое попадание не должно его разгонять.
        knockbackSpeedScale =
            Mathf.Min(
                knockbackSpeedScale,
                1f - (1f - resistance) * knockbackSlowdown
            );
    }

    // Погашение отдачи и возврат разгона. Идёт после поведения,
    // но до разрешения пересечений и скольжения по игроку —
    // тогда оба доезжают поверх отдачи, а не срезают её.
    private void ApplyKnockbackMotion()
    {
        float deltaTime = Time.deltaTime;

        if (knockbackSpeedScale < 1f)
        {
            // Экспонента, а не линейный возврат: разгон
            // набирается мягко, без рывка на последнем кадре.
            knockbackSpeedScale = Mathf.Lerp(
                knockbackSpeedScale,
                1f,
                1f - Mathf.Exp(-knockbackRecovery * deltaTime)
            );

            if (knockbackSpeedScale > 0.999f)
                knockbackSpeedScale = 1f;
        }

        if (knockbackVelocity.sqrMagnitude <= 0.000001f)
        {
            knockbackVelocity = Vector3.zero;
            return;
        }

        transform.position +=
            knockbackVelocity *
            deltaTime;

        knockbackVelocity = Vector3.Lerp(
            knockbackVelocity,
            Vector3.zero,
            1f - Mathf.Exp(-knockbackDamping * deltaTime)
        );
    }

    // Без данных считаем моба неотталкиваемым: дефолт должен
    // быть безопасным, а не «сдвинуть не на что».
    private float GetKnockbackResistance()
    {
        return enemyData != null
            ? enemyData.KnockbackResistance
            : 1f;
    }


    // =========================================================
    // OBSTACLE AVOIDANCE
    // =========================================================

    private Vector3 AvoidObstacles(Vector3 desired)
    {
        Vector3 origin =
            transform.position +
            Vector3.up * rayHeight;

        float probeDistance =
            obstacleProbeDistance +
            obstaclePadding;

        if (TryGetProbeHit(
            origin,
            desired,
            probeDistance,
            out RaycastHit forwardHit))
        {
            // Путь перекрыт: фиксируем сторону обхода и идём вдоль стены.
            clearFrames = 0;

            if (avoidSideTimer <= 0f)
                avoidSide =
                    ChooseAvoidSide(
                        origin,
                        desired,
                        probeDistance
                    );

            avoidSideTimer =
                sideMemoryTime;

            if (avoidSide != 0)
                return ComputeAvoidDirection(
                    desired,
                    forwardHit,
                    avoidSide
                );

            // Свободного места нет ни с одной стороны — отступаем.
            return Vector3.Slerp(
                desired,
                -desired,
                0.85f
            ).normalized;
        }

        // Путь свободен. Не выходим из обхода мгновенно,
        // чтобы моб плавно довернул за угол и не дёргался у кромки.
        if (avoidSide != 0)
        {
            if (clearFrames < clearFramesToExit)
            {
                clearFrames++;

                return Vector3.Slerp(
                    desired,
                    GetLateral(desired, avoidSide),
                    0.35f
                ).normalized;
            }

            clearFrames = 0;
            avoidSide = 0;
            avoidSideTimer = 0f;
        }

        return desired;
    }

    // Выбираем сторону обхода: по бокам меряем свободное
    // расстояние и идём туда, где больше места.
    private int ChooseAvoidSide(
        Vector3 origin,
        Vector3 desired,
        float probeDistance)
    {
        Vector3 left =
            GetLateral(desired, -1);

        Vector3 right =
            GetLateral(desired, 1);

        float leftClearance =
            GetClearance(origin, left);

        float rightClearance =
            GetClearance(origin, right);

        bool leftOpen =
            leftClearance >
            obstaclePadding;

        bool rightOpen =
            rightClearance >
            obstaclePadding;

        if (!leftOpen && !rightOpen)
            return 0;

        if (leftOpen && !rightOpen)
            return -1;

        if (rightOpen && !leftOpen)
            return 1;

        return rightClearance > leftClearance
            ? 1
            : -1;
    }

    // Ведём моба вдоль стены (по касательной к грани), а не просто
    // поперёк желаемого направления. Так он не врезается в кромку,
    // плавно обходит угол и не скользит вдоль блока до бесконечности.
    private Vector3 ComputeAvoidDirection(
        Vector3 desired,
        RaycastHit hit,
        int side)
    {
        Vector3 wallNormal =
            new Vector3(
                hit.normal.x,
                0f,
                hit.normal.z
            );

        Vector3 tangent;

        if (wallNormal.sqrMagnitude > 0.0001f)
        {
            tangent =
                Vector3.Cross(
                    wallNormal.normalized,
                    Vector3.up
                );
        }
        else
        {
            tangent =
                GetLateral(desired, side);
        }

        Vector3 lateral =
            GetLateral(desired, side);

        if (Vector3.Dot(tangent, lateral) < 0f)
            tangent = -tangent;

        tangent.y = 0f;
        tangent.Normalize();

        // Доворачиваем к игроку, чтобы после прохода угла
        // сразу вернуться на прямой курс.
        return Vector3.Slerp(
            tangent,
            desired,
            1f - dodgeSteer
        ).normalized;
    }

    private Vector3 GetLateral(
        Vector3 forward,
        int side)
    {
        Vector3 lateral =
            side < 0
                ? Vector3.Cross(
                    Vector3.up,
                    forward
                )
                : -Vector3.Cross(
                    Vector3.up,
                    forward
                );

        lateral.y = 0f;

        return lateral.normalized;
    }

    private float GetClearance(
        Vector3 origin,
        Vector3 direction)
    {
        if (Physics.Raycast(
            origin,
            direction.normalized,
            out RaycastHit hit,
            clearanceProbeDistance))
        {
            if (StructureQuery.IsWorldStructure(hit.collider))
                return hit.distance;
        }

        return clearanceProbeDistance;
    }

    // Выталкиваем врага из стен, игрока и других врагов.
    // Все тела kinematic — физика сама их не расталкивает,
    // поэтому пересечения решаются вручную.
    private void ResolveStructureOverlap()
    {
        if (selfCollider == null)
            return;

        Vector3 position =
            transform.position;

        Vector3 size =
            selfCollider.bounds.size;

        float checkRadius =
            Mathf.Max(
                size.x,
                Mathf.Max(size.y, size.z)
            ) *
            0.5f +
            obstaclePadding;

        Collider[] nearbyColliders =
            StructureQuery.OverlapSphere(
                selfCollider.bounds.center,
                checkRadius,
                out int nearbyCount
            );

        for (int i = 0; i < nearbyCount; i++)
        {
            Collider nearby = nearbyColliders[i];

            if (nearby == null ||
                nearby == selfCollider)
            {
                continue;
            }

            switch (ColliderKindQuery.GetKind(nearby))
            {
                case ColliderKind.WorldStructure:
                    ResolvePenetration(ref position, nearby, 1f);
                    break;

                // Игрок не должен толкаться врагами — выталкивается только враг.
                case ColliderKind.Player:
                    ResolvePenetration(ref position, nearby, 1f);
                    break;

                // Другой враг — расталкиваемся пополам:
                // сосед тоже выталкивает себя своей половиной.
                case ColliderKind.Enemy:
                    ResolvePenetration(ref position, nearby, 0.5f);
                    break;
            }
        }

        if ((position - transform.position).sqrMagnitude > 0.0001f)
        {
            resolveSlideTarget = position;
            hasResolveSlideTarget = true;
        }
    }

    private void ResolvePenetration(
        ref Vector3 position,
        Collider other,
        float pushFactor)
    {
        if (!Physics.ComputePenetration(
            selfCollider,
            position,
            transform.rotation,
            other,
            other.transform.position,
            other.transform.rotation,
            out Vector3 direction,
            out float distance))
        {
            return;
        }

        if (distance <= 0.01f)
            return;

        position +=
            direction *
            (distance * pushFactor + 0.005f);
    }

    // Плавный «досъезд» к точке выталкивания вместо жёсткого
    // телепорта — убирает скачки, когда разрешение пересечений
    // происходит раз в 0.02–0.10с.
    private void ApplyResolveSlide()
    {
        if (!hasResolveSlideTarget)
            return;

        transform.position = Vector3.MoveTowards(
            transform.position,
            resolveSlideTarget,
            resolveSlideSpeed * Time.deltaTime
        );

        if (Vector3.Distance(
                transform.position,
                resolveSlideTarget
            ) <= 0.01f)
        {
            transform.position = resolveSlideTarget;
            hasResolveSlideTarget = false;
        }
    }

    // Превентивное скольжение по игроку: составляющая движения,
    // ведущая внутрь игрока, гасится до того, как враг в него
    // зашёл. Игрок ведёт себя как стена — враг упирается и
    // скользит вокруг, не проседая внутрь и не отдёргиваясь.
    private void ApplyPreventivePlayerSlide(
        Vector3 startPosition)
    {
        if (selfCollider == null ||
            playerCollider == null ||
            player == null)
        {
            return;
        }

        Vector3 toPlayer =
            player.position - transform.position;

        toPlayer.y = 0f;

        // Быстрый ранний выход: большинство врагов далеко от игрока.
        if (toPlayer.sqrMagnitude > 64f)
            return;

        Vector3 delta =
            transform.position - startPosition;

        if (delta.sqrMagnitude <= 0.0001f)
            return;

        Vector3 result = delta;

        for (int i = 0; i < 3; i++)
        {
            if (!PenetratesPlayer(
                startPosition + result,
                out Vector3 normal))
            {
                break;
            }

            result -=
                normal *
                Vector3.Dot(result, normal);

            if (result.sqrMagnitude <= 0.0001f)
                break;
        }

        transform.position =
            startPosition + result;
    }

    private bool PenetratesPlayer(
        Vector3 position,
        out Vector3 normal)
    {
        normal = Vector3.zero;

        if (selfCollider == null ||
            playerCollider == null)
        {
            return false;
        }

        if (Physics.ComputePenetration(
            selfCollider,
            position,
            transform.rotation,
            playerCollider,
            playerCollider.transform.position,
            playerCollider.transform.rotation,
            out Vector3 direction,
            out float distance))
        {
            if (distance > 0.0005f)
            {
                normal = direction;
                return true;
            }
        }

        return false;
    }

    // Контактный урон по близости: kinematic-тела не шлют
    // OnCollisionStay, поэтому проверяем дистанцию напрямую.
    private void TryApplyContactDamage()
    {
        if (IsDead ||
            player == null ||
            contactDamageTimer > 0f)
        {
            return;
        }

        Vector3 toPlayer =
            player.position - transform.position;

        toPlayer.y = 0f;

        float contactRange =
            GetContactRange();

        if (toPlayer.sqrMagnitude >
            contactRange * contactRange)
        {
            return;
        }

        PlayerHealth health = playerHealth;

        if (health == null)
        {
            health =
                player.GetComponentInChildren<PlayerHealth>();

            if (health == null)
                return;

            playerHealth = health;
        }

        float damage = 1f;

        if (enemyData != null)
            damage =
                enemyData.ContactDamage;

        if (isBoss &&
            bossData != null)
        {
            damage =
                bossData.ContactDamage;

            if (BossPhase == 3)
            {
                damage *=
                    bossData.Phase3DamageMultiplier;
            }
        }

        damage *= scaledDamageMultiplier;

        health.TakeDamage(damage);

        float cooldown =
            enemyData != null
                ? enemyData.DamageCooldown
                : 1f;

        contactDamageTimer = cooldown;
    }

    private float GetContactRange()
    {
        float selfRadius =
            selfCollider != null
                ? selfCollider.bounds.extents.magnitude
                : 1f;

        float playerRadius =
            playerCollider != null
                ? playerCollider.bounds.extents.magnitude
                : 0.5f;

        return selfRadius + playerRadius + 0.15f;
    }

    private bool TryGetProbeHit(
        Vector3 origin,
        Vector3 direction,
        float distance,
        out RaycastHit hit)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            hit = default;
            return false;
        }

        if (Physics.Raycast(
            origin,
            direction.normalized,
            out hit,
            distance))
        {
            if (StructureQuery.IsWorldStructure(hit.collider))
                return true;
        }

        hit = default;
        return false;
    }

    private bool HasStructureBlocking(
        Vector3 origin,
        Vector3 direction,
        float distance)
    {
        return TryGetProbeHit(
            origin,
            direction,
            distance,
            out _
        );
    }

    private bool HasLineOfSightToPlayer()
    {
        if (player == null)
            return false;

        Vector3 origin =
            transform.position +
            Vector3.up * rayHeight;

        Vector3 target =
            player.position +
            Vector3.up * rayHeight;

        Vector3 direction =
            target - origin;

        float distance =
            direction.magnitude;

        if (distance <= 0.01f)
            return true;

        return !HasStructureBlocking(
            origin,
            direction,
            distance
        );
    }


    // =========================================================
    // TARGET PRIORITY
    // =========================================================

    // Павший моб запоминает точку своей смерти. Список короткий и
    // чистится по времени прямо во время обхода, поэтому он не
    // растёт и не требует отдельного прохода.
    private static void RegisterFallenAlly(
        Vector3 position,
        EnemyType type)
    {
        fallenEnemies.Add(
            new FallenAlly
            {
                position = position,
                time = Time.time,
                type = type
            }
        );
    }

    // Проверка идёт на тех же тиках, что и поиск живого союзника:
    // 2 Гц у танка, 4 Гц у дальника. Перебора по кадру нет.
    private void CheckFallenRoleAlly()
    {
        if (!hasRoleAllyType ||
            allyRevengeRadius <= 0.1f ||
            revengeTimer > 0f)
        {
            return;
        }

        if (!TryFindFallenRoleAlly(out Vector3 position))
            return;

        revengePosition = position;

        revengeTimer = allyRevengeTime;

        // Пока моб шёл на точку, кэш направления мог остаться от
        // прежней задачи (экран/дистанция).
        hasCachedMoveDirection = false;
    }

    private bool TryFindFallenRoleAlly(
        out Vector3 position)
    {
        position = transform.position;

        Vector3 origin = transform.position;

        float limit = allyRevengeRadius * allyRevengeRadius;

        float now = Time.time;

        bool found = false;

        for (int i = fallenEnemies.Count - 1; i >= 0; i--)
        {
            FallenAlly fallen = fallenEnemies[i];

            // Обход с конца: устаревшие записи сразу удаляются,
            // и индексы при удалении не «уезжают».
            if (now - fallen.time > fallenMemoryTime)
            {
                fallenEnemies.RemoveAt(i);
                continue;
            }

            if (fallen.type != roleAllyType)
                continue;

            Vector3 offset = fallen.position - origin;

            offset.y = 0f;

            float sqr = offset.sqrMagnitude;

            if (sqr > limit)
                continue;

            position = fallen.position;
            limit = sqr;

            found = true;
        }

        return found;
    }

    // Пока идёт «добивание», обычная роль молчит: упора нет,
    // дистанции нет, перестановки нет, выстрела нет. Это и есть
    // плата за потерянного союзника — позиция была дороже, чем
    // труп, и теперь игрок может её отыграть.
    private void HandleRoleRevenge()
    {
        Vector3 toPosition = revengePosition - transform.position;

        toPosition.y = 0f;

        if (toPosition.sqrMagnitude > 0.09f)
        {
            MoveInDirection(
                toPosition.normalized,
                1f
            );

            return;
        }

        // Дошли — и сразу обратно в роль.
        revengeTimer = 0f;

        hasCachedMoveDirection = false;
    }


    // =========================================================
    // RANGED
    // =========================================================

    // Дальник больше не «встал и стреляет». Он держит дистанцию,
    // отходит, если к нему подошли, переставляется по кругу, когда
    // ему удобно стоять, и телеграфирует каждый выстрел. Всё решает
    // позиция, а не урон: цифры у него прежние.
    private void HandleRangedBehaviour()
    {
        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        if (distance <= 0.01f)
            return;

        direction.Normalize();

        UpdateCachedLineOfSight();

        UpdateSupportContext();

        // Приоритет цели важнее роли: упал танк — дистанция
        // забыта, идём туда. Заодно это единственное окно, когда
        // дальник не стреляет.
        if (revengeTimer > 0f)
        {
            HandleRoleRevenge();
            return;
        }

        // Замах: стоим, целимся, не двигаемся. Это и есть окно,
        // в котором игрок успевает разорвать линию огня.
        if (attackTelegraphActive)
        {
            RotateTowards(direction);

            HandleAttackTelegraph();

            return;
        }

        if (attackTimer <= 0f &&
            cachedLineOfSight &&
            distance <= enemyData.AttackRange &&
            distance >= retreatDistance)
        {
            StartAttackTelegraph();

            return;
        }

        HandleRangedPositioning(direction, distance);
    }

    private void UpdateCachedLineOfSight()
    {
        if (lineOfSightTimer > 0f)
            return;

        cachedLineOfSight = HasLineOfSightToPlayer();

        lineOfSightTimer =
            Mathf.Max(lineOfSightCheckInterval, 0.05f);
    }

    private void StartAttackTelegraph()
    {
        float telegraph = enemyData.AttackTelegraph;

        attackTelegraphActive = true;

        attackTelegraphTotalTime = telegraph;

        attackTelegraphRemainingTime = telegraph;

        if (telegraph <= 0f)
            HandleAttackTelegraph();
    }

    private void HandleAttackTelegraph()
    {
        if (attackTelegraphTotalTime > 0f)
        {
            attackTelegraphRemainingTime -= Time.deltaTime;

            ApplyCrouchPose(
                attackTelegraphRemainingTime,
                attackTelegraphTotalTime,
                attackTelegraphCrouch
            );

            if (attackTelegraphRemainingTime > 0f)
                return;
        }

        attackTelegraphActive = false;

        transform.localScale = baseScale;

        ShootFanAtPlayer(
            enemyData.ProjectileDamage * scaledDamageMultiplier,
            enemyData.ProjectileSpeed
        );

        attackTimer = GetRangedAttackInterval();

        // После выстрела обязательно переставляемся: иначе
        // дальник превращается в стационарную мишень, в которую
        // просто вбивают издалека.
        repositionTimer = Mathf.Max(repositionInterval, 0.2f);

        hasRepositionTarget = false;
    }

    // Порядок именно такой: безопасность (не подпускать вплотную)
    // важнее перестановки, поэтому отход и сближение отменяют
    // начатую перестановку, а не ждут её конца.
    private void HandleRangedPositioning(
        Vector3 direction,
        float distance)
    {
        if (!cachedLineOfSight)
        {
            // Стена между врагом и игроком: подходим, пока не
            // откроется линия огня. Иначе он просто стоит за
            // препятствием и ждёт, когда игрок сам подойдёт.
            ClearReposition();
            MoveTowardsPlayer();

            return;
        }

        if (distance < retreatDistance)
        {
            ClearReposition();

            MoveInDirection(
                -direction,
                retreatSpeedScale
            );

            // Отступая, держим моб развёрнутым к игроку: иначе
            // он выглядит как убегающий, а не отступающий с
            // оружием наготове.
            RotateTowards(direction);

            return;
        }

        if (distance > preferredDistance)
        {
            ClearReposition();

            MoveInDirection(direction, 1f);

            return;
        }

        if (hasRepositionTarget)
        {
            Vector3 toTarget =
                repositionTarget - transform.position;

            toTarget.y = 0f;

            if (toTarget.sqrMagnitude > 0.25f)
            {
                MoveInDirection(
                    toTarget.normalized,
                    1f
                );

                return;
            }

            hasRepositionTarget = false;
        }

        if (repositionTimer <= 0f)
            StartReposition(direction);
    }


    private void ClearReposition()
    {
        if (!hasRepositionTarget &&
            repositionTimer <= 0f)
        {
            return;
        }

        hasRepositionTarget = false;

        repositionTimer = 0f;

        hasCachedMoveDirection = false;
    }

    // Перестановка = точка на кольце вокруг игрока. Сторона и дуга
    // случайны, поэтому два дальника не встают в одну точку. Одна
    // проверка луча отсекает вариант «упереться в стену».
    private void StartReposition(Vector3 toPlayer)
    {
        hasRepositionTarget = false;
        hasCachedMoveDirection = false;

        if (player == null)
            return;

        int side = Random.value < 0.5f ? -1 : 1;

        Vector3 target = GetRepositionTarget(
            toPlayer,
            side
        );

        if (IsRepositionBlocked(target))
        {
            target = GetRepositionTarget(
                toPlayer,
                -side
            );
        }

        if (IsRepositionBlocked(target))
            return;

        repositionTarget = target;
        hasRepositionTarget = true;

        repositionTimer = Mathf.Max(repositionInterval, 0.2f);
    }

    private Vector3 GetRepositionTarget(
        Vector3 toPlayer,
        int side)
    {
        Vector3 lateral = GetLateral(toPlayer, side);

        // Дуга между «назад от игрока» и «вбок»: чем больше угол,
        // тем дальше уход в сторону, тем меньше моб стоит спиной.
        Vector3 arcDirection = Vector3.Slerp(
            -toPlayer,
            lateral,
            Mathf.Clamp01(
                repositionArcDegrees / 90f
            )
        );

        Vector3 target =
            player.position +
            arcDirection * preferredDistance;

        target.y = transform.position.y;

        return target;
    }

    private bool IsRepositionBlocked(Vector3 target)
    {
        Vector3 toTarget =
            target - transform.position;

        toTarget.y = 0f;

        float distance = toTarget.magnitude;

        if (distance <= 0.01f)
            return false;

        Vector3 origin =
            transform.position +
            Vector3.up * rayHeight;

        return HasStructureBlocking(
            origin,
            toTarget / distance,
            distance
        );
    }

    // Танк рядом = дальник опаснее: тот же урон, но окно между
    // выстрелами меньше. Проверка кэшируется, обход списка живых
    // врагов идёт 4 раза в секунду, а не каждый кадр.
    private float GetRangedAttackInterval()
    {
        float interval =
            enemyData.AttackRate *
            rangedAttackRateMultiplier;

        if (HasNearbyTankSupport())
        {
            interval *= tankSupportAttackRateMultiplier;
        }

        return Mathf.Max(interval, 0.4f);
    }

    // Контекст связки дальника: есть ли рядом танк и не упал ли
    // тот, кто его прикрывал. Общий тик 4 Гц на оба вопроса, и
    // он не зависит от того, успел ли дальник выстрелить.
    private void UpdateSupportContext()
    {
        if (supportCheckTimer > 0f)
            return;

        supportCheckTimer =
            Mathf.Max(supportCheckInterval, 0.05f);

        hasTankSupport = false;
        tankSupportAttackRateMultiplier = 1f;

        CheckFallenRoleAlly();

        if (player == null)
            return;

        Vector3 origin = transform.position;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            Enemy ally = aliveEnemies[i];

            if (ally == null ||
                ally.IsDead ||
                ally == this)
            {
                continue;
            }

            if (ally.cachedEnemyType != EnemyType.Tank)
                continue;

            float radius = ally.EffectiveSupportRadius;

            if (radius <= 0.05f)
                continue;

            Vector3 offset =
                ally.transform.position - origin;

            offset.y = 0f;

            if (offset.sqrMagnitude > radius * radius)
                continue;

            hasTankSupport = true;

            tankSupportAttackRateMultiplier =
                ally.enemyData != null
                    ? ally.enemyData.SupportAttackRateMultiplier
                    : 1f;

            break;
        }
    }

    private bool HasNearbyTankSupport()
    {
        return hasTankSupport;
    }


    // =========================================================
    // TANK
    // =========================================================

    // Танк больше не идёт «просто в лоб». Он выбирает, кого
    // экранировать, и встаёт между игроком и ближайшим дальником.
    // Там, где экранировать некого и игрок подошёл вплотную, он
    // упирается на месте и давит контактом. Это и есть тактическая
    // задача: либо сначала убрать танка, либо пробиться к дальнику
    // через его корпус.
    private void HandleTankBehaviour()
    {
        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        if (distance <= 0.01f)
            return;

        direction.Normalize();

        // Приоритет цели важнее роли: упал дальник — экран забыт.
        if (revengeTimer > 0f)
        {
            HandleRoleRevenge();
            return;
        }

        // Упёрся: не протискивается вплотную, но и не отступает.
        // Останавливается на braceStopRange, который по построению
        // меньше его собственной дальности урона, поэтому бьёт сам,
        // без всякой подсказки игроку «подойди поближе».
        if (braceStopRange > 0.01f && distance <= braceStopRange)
        {
            hasCachedMoveDirection = false;

            RotateTowards(direction);

            return;
        }

        Vector3 target = GetTankTarget();

        Vector3 toTarget = target - transform.position;

        toTarget.y = 0f;

        if (toTarget.sqrMagnitude <= 0.04f)
        {
            hasCachedMoveDirection = false;

            RotateTowards(direction);

            return;
        }

        MoveInDirection(
            toTarget.normalized,
            1f
        );
    }

    private Vector3 GetTankTarget()
    {
        if (player == null)
            return transform.position;

        if (braceRange <= 0.1f)
            return player.position;
        if (allySearchTimer <= 0f)
        {
            allySearchTimer =
                Mathf.Max(allySearchInterval, 0.05f);

            escortedAlly = FindAllyToScreen();

            CheckFallenRoleAlly();
        }

        if (escortedAlly == null ||
            escortedAlly.IsDead)
        {
            escortedAlly = null;

            return player.position;
        }

        // Точка перед союзником на линии «союзник — игрок».
        // Танк встаёт туда и закрывает дальника корпусом.
        Vector3 toPlayer =
            player.position -
            escortedAlly.transform.position;

        toPlayer.y = 0f;

        if (toPlayer.sqrMagnitude <= 0.01f)
            return player.position;

        return escortedAlly.transform.position +
            toPlayer.normalized * braceRange;
    }

    private Enemy FindAllyToScreen()
    {
        if (supportRadius <= 0.1f)
            return null;

        Vector3 origin = transform.position;

        float bestSqr = supportRadius * supportRadius;

        Enemy best = null;

        for (int i = 0; i < aliveEnemies.Count; i++)
        {
            Enemy ally = aliveEnemies[i];

            if (ally == null ||
                ally == this ||
                ally.IsDead)
            {
                continue;
            }

            if (ally.cachedEnemyType != EnemyType.Ranged)
                continue;

            Vector3 offset =
                ally.transform.position - origin;

            offset.y = 0f;

            float sqr = offset.sqrMagnitude;

            if (sqr >= bestSqr)
                continue;

            bestSqr = sqr;
            best = ally;
        }

        return best;
    }

    // Радиус, который дальник видит снаружи: уже с учётом варианта.
    internal float EffectiveSupportRadius =>
        supportRadius;


    // =========================================================
    // ELITE
    // =========================================================

    private void HandleEliteBehaviour()
    {
        MoveTowardsPlayer();

        if (abilityTimer > 0f)
            return;

        EliteAbility();

        abilityTimer =
            enemyData.AbilityCooldown *
            abilityCooldownMultiplier;
    }

    // Базовая элита бьёт зоной вокруг себя: это честно для
    // ближнего боя и заставляет держать дистанцию. Агрессивный
    // вариант ставит зону туда, где игрок был в момент каста —
    // с тем же предупреждением, но теперь от неё нужно уходить
    // каждый каст, а не только в первый.
    private void EliteAbility()
    {
        if (enemyData == null)
            return;

        Vector3 center =
            abilityTargetsPlayerPosition && player != null
                ? player.position
                : transform.position;

        GameObject zoneObject =
            new GameObject("Elite_Shockwave");

        zoneObject.transform.position = center;

        zoneObject.transform.rotation =
            Quaternion.identity;

        HazardZone zone =
            zoneObject.AddComponent<HazardZone>();

        zone.Initialize(
            enemyData.AbilityWarning,
            enemyData.AbilityRadius,
            enemyData.AbilityDps * abilityDpsMultiplier,
            enemyData.AbilityDuration,
            1.35f
        );

        PlayEliteAbilitySound();

        Debug.Log(
            $"Elite '{name}' cast a shockwave zone."
        );
    }

    private void PlayEliteAbilitySound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.BossAbility,
                priority: SfxPriority.High
            );
    }


    // =========================================================
    // BOSS PHASES
    // =========================================================

    private void UpdateBossPhase()
    {
        if (bossData == null)
            return;

        float healthPercent =
            currentHealth /
            scaledMaxHealth;

        int newPhase;

        if (healthPercent <= bossData.Phase3HealthPercent)
        {
            newPhase = 3;
        }
        else if (healthPercent <= bossData.Phase2HealthPercent)
        {
            newPhase = 2;
        }
        else
        {
            newPhase = 1;
        }

        if (newPhase == BossPhase)
            return;

        BossPhase = newPhase;

        // Смена фазы сама по себе не опасна: новые кулдауны не должны
        // срабатывать в тот же кадр, в который фаза сменилась, иначе
        // ускорение читается как удар из ниоткуда. Даём короткую
        // паузу, равную новому кулдауну способности фазы.
        abilityTimer = Mathf.Max(
            abilityTimer,
            GetBossAbilityCooldown()
        );

        summonTimer = Mathf.Max(
            summonTimer,
            GetBossSummonCooldown()
        );

        PlayBossPhaseChangeSound();

        Debug.Log(
            $"BOSS '{name}' entered PHASE {BossPhase}!"
        );
    }

    private void PlayBossPhaseChangeSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.BossPhaseChange,
                priority: SfxPriority.High
            );
    }

    private void PlayBossAbilitySound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.BossAbility,
                priority: SfxPriority.High
            );
    }


    // Есть ли у игрока хотя бы одно направление, в котором можно
    // отойти от центра опасной зоны на её радиус. Зона всегда
    // ставится под игрока, поэтому проверяем именно геометрию
    // вокруг игрока: луч в сторону не должен упираться в стену
    // раньше, чем игрок успеет выйти из круга.
    private bool HasEscapeRouteFromPlayer(float radius)
    {
        if (player == null)
            return false;

        const int directions = 12;

        float checkDistance =
            Mathf.Max(radius, 0.5f) + 0.5f;

        Vector3 origin =
            player.position +
            Vector3.up * rayHeight;

        for (int i = 0; i < directions; i++)
        {
            float angle =
                (float)i / directions * 360f;

            Vector3 direction =
                new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

            if (!HasStructureBlocking(
                    origin,
                    direction,
                    checkDistance))
            {
                return true;
            }
        }

        return false;
    }


    // =========================================================
    // BOSS BEHAVIOUR
    // =========================================================

    private void HandleBossBehaviour()
    {
        if (bossData == null)
            return;

        Vector3 direction =
            player.position - transform.position;

        direction.y = 0f;

        float distance =
            direction.magnitude;

        if (lineOfSightTimer <= 0f)
        {
            cachedLineOfSight =
                HasLineOfSightToPlayer();
            lineOfSightTimer =
                Mathf.Max(lineOfSightCheckInterval, 0.05f);
        }

        if (distance > bossData.AttackRange ||
            !cachedLineOfSight)
        {
            MoveBossTowardsPlayer();
        }
        else
        {
            if (direction.sqrMagnitude > 0.01f)
            {
                direction.Normalize();

                RotateTowards(direction);
            }

            if (attackTimer <= 0f)
            {
                ShootAtPlayer(
                    GetBossProjectileDamage(),
                    bossData.ProjectileSpeed
                );

                attackTimer =
                    GetBossAttackInterval();
            }
        }

        if (abilityTimer <= 0f)
        {
            BossAbility();

            abilityTimer =
                GetBossAbilityCooldown();
        }

        if (summonTimer <= 0f)
        {
            SummonEnemies();

            summonTimer =
                GetBossSummonCooldown();
        }
    }


    // =========================================================
    // BOSS MOVEMENT
    // =========================================================

    private void MoveBossTowardsPlayer()
    {
        if (player == null ||
            bossData == null)
            return;

        Vector3 desired =
            player.position - transform.position;

        desired.y = 0f;

        if (desired.sqrMagnitude <= 0.01f)
            return;

        desired.Normalize();

        float speed =
            GetBossMoveSpeed() *
            knockbackSpeedScale;

        Vector3 moveDirection;

        if (!hasCachedMoveDirection || navigationTimer <= 0f)
        {
            moveDirection = AvoidObstacles(desired);
            cachedMoveDirection = moveDirection;
            hasCachedMoveDirection = true;
            navigationTimer = Mathf.Max(navigationUpdateInterval, 0.01f);
        }
        else
        {
            moveDirection = cachedMoveDirection;
        }

        transform.position +=
            moveDirection *
            speed *
            Time.deltaTime;

        if (structureResolveTimer <= 0f)
        {
            ResolveStructureOverlap();
            structureResolveTimer = Mathf.Max(structureResolveInterval, 0.02f);
        }

        RotateTowards(moveDirection);
    }

    private float GetBossMoveSpeed()
    {
        switch (BossPhase)
        {
            case 2:
                return bossData.MoveSpeed *
                       bossData.Phase2MoveSpeedMultiplier;

            case 3:
                return bossData.MoveSpeed *
                       bossData.Phase3MoveSpeedMultiplier;

            default:
                return bossData.MoveSpeed;
        }
    }


    // =========================================================
    // BOSS RANGED ATTACK
    // =========================================================

    private float GetBossAttackInterval()
    {
        float multiplier = 1f;

        switch (BossPhase)
        {
            case 2:
                multiplier =
                    bossData.Phase2AttackIntervalMultiplier;
                break;

            case 3:
                multiplier =
                    bossData.Phase3AttackIntervalMultiplier;
                break;
        }

        return Mathf.Max(
            bossData.AttackInterval * multiplier,
            0.1f
        );
    }

    private float GetBossProjectileDamage()
    {
        float damage =
            bossData.ProjectileDamage;

        if (BossPhase == 3)
        {
            damage *=
                bossData.Phase3DamageMultiplier;
        }

        return damage * scaledDamageMultiplier;
    }


    // =========================================================
    // BOSS ABILITY PARAMETERS
    // =========================================================

    private float GetBossAbilityCooldown()
    {
        switch (BossPhase)
        {
            case 2:
                return bossData.Phase2AbilityCooldown;

            case 3:
                return bossData.Phase3AbilityCooldown;

            default:
                return bossData.Phase1AbilityCooldown;
        }
    }

    private float GetBossAbilityDamage()
    {
        float damage;

        switch (BossPhase)
        {
            case 2:
                damage =
                    bossData.Phase2AbilityDamage;

                break;

            case 3:
                damage =
                    bossData.Phase3AbilityDamage;

                break;

            default:
                damage =
                    bossData.Phase1AbilityDamage;

                break;
        }

        // Способность босса по номеру волны не масштабируется — так
        // было и раньше, и менять это здесь нельзя, волны бы поехали.
        // А вот уровень «лёгкая / сложная» к ней применяется: иначе на
        // лёгкой волне ударная зона осталась бы ровно такой же дорогой,
        // как контактный урон, и половина ритма развалилась бы — игрок
        // получил бы передышку по мобам и мгновенную смерть от способности.
        return damage *
            WaveDifficulty.GetDamageMultiplier(
                currentWave,
                true
            );
    }

    private float GetBossAbilityRadius()
    {
        switch (BossPhase)
        {
            case 2:
                return bossData.Phase2AbilityRadius;

            case 3:
                return bossData.Phase3AbilityRadius;

            default:
                return bossData.Phase1AbilityRadius;
        }
    }

    private float GetBossAbilityWarningDuration()
    {
        switch (BossPhase)
        {
            case 2:
                return bossData.Phase2AbilityWarningDuration;

            case 3:
                return bossData.Phase3AbilityWarningDuration;

            default:
                return bossData.Phase1AbilityWarningDuration;
        }
    }


    // =========================================================
    // BOSS ABILITY
    // =========================================================

    private void BossAbility()
    {
        if (bossData == null)
            return;

        if (bossAttackZonePrefab == null)
        {
            Debug.LogWarning(
                "Boss: Boss Attack Zone Prefab is not assigned."
            );

            return;
        }

        float zoneRadius = GetBossAbilityRadius();

        // Зона ставится ровно под игрока, поэтому она обязана быть
        // покидаемой. Если ни в одну сторону от игрока нельзя отойти
        // на радиус зоны (узкий проход, угол карты), каст не читается
        // как уклонение — это просто смерть с телеграфом. В таком
        // случае способность не применяется вовсе.
        if (!HasEscapeRouteFromPlayer(zoneRadius))
        {
            Debug.Log(
                $"BOSS '{name}' skipped AoE: player has " +
                $"no escape route within {zoneRadius:F1}."
            );

            return;
        }

        Vector3 zonePosition =
            player.position;

        zonePosition.y = 0.005f;

        GameObject zoneObject =
            Instantiate(
                bossAttackZonePrefab,
                zonePosition,
                Quaternion.identity
            );

        BossAttackZone attackZone =
            zoneObject.GetComponent<BossAttackZone>();

        if (attackZone != null)
        {
            attackZone.Initialize(
                GetBossAbilityDamage(),
                zoneRadius,
                GetBossAbilityWarningDuration()
            );
        }

        PlayBossAbilitySound();

        Debug.Log(
            $"BOSS '{name}' used AoE ability! " +
            $"Phase: {BossPhase}"
        );
    }


    // =========================================================
    // BOSS SUMMON PARAMETERS
    // =========================================================

    private float GetBossSummonCooldown()
    {
        switch (BossPhase)
        {
            case 2:
                return bossData.Phase2SummonCooldown;

            case 3:
                return bossData.Phase3SummonCooldown;

            default:
                return bossData.Phase1SummonCooldown;
        }
    }

    private int GetBossSummonCount()
    {
        switch (BossPhase)
        {
            case 2:
                return bossData.Phase2SummonCount;

            case 3:
                return bossData.Phase3SummonCount;

            default:
                return bossData.Phase1SummonCount;
        }
    }


    // =========================================================
    // BOSS SUMMON
    // =========================================================

    private void SummonEnemies()
    {
        if (enemySpawner == null)
        {
            Debug.LogWarning(
                "Boss: EnemySpawner not found."
            );

            return;
        }

        if (bossData == null)
            return;

        int wantedCount =
            GetBossSummonCount();

        // Призыв идёт мимо очереди спавнера, поэтому потолок живых
        // врагов его не касается. Без этой проверки третья фаза
        // добавляет врагов быстрее, чем игрок успевает их убивать, и
        // бой превращается в очередь, которая не заканчивается.
        int room =
            enemySpawner.MaxAliveEnemies -
            Enemy.AliveCount;

        int summonCount =
            Mathf.Min(
                Mathf.Max(wantedCount, 0),
                Mathf.Max(room, 0)
            );

        if (summonCount <= 0)
        {
            Debug.Log(
                $"Boss summon skipped: field is full " +
                $"({Enemy.AliveCount}/" +
                $"{enemySpawner.MaxAliveEnemies}). " +
                $"Phase: {BossPhase}"
            );

            return;
        }

        for (int i = 0;
             i < summonCount;
             i++)
        {
            Vector2 randomOffset =
                Random.insideUnitCircle * 3f;

            Vector3 spawnPosition =
                transform.position +
                new Vector3(
                    randomOffset.x,
                    0f,
                    randomOffset.y
                );

            enemySpawner.SpawnEnemyAtPosition(
                EnemyType.Normal,
                spawnPosition
            );
        }

        Debug.Log(
            $"Boss summoned {summonCount} of {wantedCount} " +
            $"enemies. Phase: {BossPhase}"
        );
    }


    // =========================================================
    // SHOOTING
    // =========================================================

    private void ShootAtPlayer(
        float damage,
        float projectileSpeed)
    {
        if (enemyProjectilePrefab == null)
        {
            Debug.LogWarning(
                $"Enemy '{name}' has no projectile prefab."
            );

            return;
        }

        Vector3 direction =
            player.position -
            attackPoint.position;

        if (direction.sqrMagnitude <= 0.01f)
            return;

        direction.Normalize();

        SpawnEnemyProjectile(
            direction,
            damage,
            projectileSpeed
        );
    }

    // Веер дальника: несколько снарядов с равномерным разлётом
    // вокруг прицела. Одиночная пуля стала слишком легко уворачиваемой.
    private void ShootFanAtPlayer(
        float damage,
        float projectileSpeed)
    {
        if (enemyProjectilePrefab == null)
        {
            Debug.LogWarning(
                $"Enemy '{name}' has no projectile prefab."
            );

            return;
        }

        Vector3 baseDirection =
            player.position -
            attackPoint.position;

        if (baseDirection.sqrMagnitude <= 0.01f)
            return;

        baseDirection.Normalize();

        int projectileCount =
            enemyData != null
                ? enemyData.FanProjectileCount
                : 1;

        float spread =
            enemyData != null
                ? enemyData.FanSpreadDegrees
                : 0f;

        for (int i = 0; i < projectileCount; i++)
        {
            float t =
                projectileCount <= 1
                    ? 0f
                    : (float)i / (projectileCount - 1f);

            float angleOffset =
                Mathf.Lerp(
                    -spread * 0.5f,
                    spread * 0.5f,
                    t
                );

            Vector3 direction =
                Quaternion.Euler(
                    0f,
                    angleOffset,
                    0f
                ) *
                baseDirection;

            SpawnEnemyProjectile(
                direction,
                damage,
                projectileSpeed
            );
        }
    }

    private void SpawnEnemyProjectile(
        Vector3 direction,
        float damage,
        float projectileSpeed)
    {
        if (enemyProjectilePrefab == null)
            return;

        EnemyProjectile projectile =
            EnemyProjectilePool.Spawn(
                enemyProjectilePrefab,
                attackPoint.position,
                Quaternion.LookRotation(direction)
            );

        if (projectile != null)
        {
            projectile.Initialize(
                direction,
                damage,
                projectileSpeed
            );

            PlayEnemyShotSound();
        }
    }

    private void PlayEnemyShotSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXVariationAt(
                sfx.EnemyShot,
                transform.position,
                priority: SfxPriority.Low
            );
    }


    // =========================================================
    // DAMAGE
    // =========================================================

    public void TakeDamage(
        float damage,
        bool isCritical = false)
    {
        if (IsDead)
            return;

        if (damage <= 0f)
            return;

        HasTakenDamage = true;

        currentHealth -= damage;

        SpawnDamageNumber(
            damage,
            isCritical
        );

        PlayEnemyHitSound();

        if (isBoss &&
            bossData != null)
        {
            UpdateBossPhase();
        }

        if (currentHealth <= 0f)
        {
            currentHealth = 0f;
            Die();
        }
    }

    private void PlayEnemyHitSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXAt(
                sfx.EnemyHit,
                transform.position,
                priority: SfxPriority.Low
            );
    }


    // =========================================================
    // BURN
    // =========================================================

    public void ApplyBurn(
        float damagePerSecond,
        float duration,
        float tickInterval)
    {
        if (IsDead)
            return;

        if (damagePerSecond <= 0f ||
            duration <= 0f)
            return;

        tickInterval =
            Mathf.Max(
                tickInterval,
                0.05f
            );

        burnDamagePerTick =
            damagePerSecond * tickInterval;
        burnRemainingTime = duration;
        burnTickInterval = tickInterval;

        // Таймер тика сбрасывается только при первом поджоге.
        // Раньше он обнулялся на каждом попадании, и оружие с
        // частой стрельбой (выстрел каждые 0.18 с против тика в
        // 0.5 с) перезаписывало таймер быстрее, чем тот успевал
        // дойти до нуля: горение висело, а урона не наносило.
        if (!burnActive)
            burnTickTimer = tickInterval;

        burnActive = true;
    }


    // =========================================================
    // BLEEDING
    // =========================================================

    public void ApplyBleeding(
        float damagePerSecond,
        float duration,
        float tickInterval)
    {
        if (IsDead)
            return;

        if (damagePerSecond <= 0f ||
            duration <= 0f)
            return;

        tickInterval =
            Mathf.Max(
                tickInterval,
                0.05f
            );

        // Кровотечение копится, а не перезаписывается: серия
        // попаданий складывает урон, иначе оно было бы обычным
        // вторым горением. Потолок не даёт стаку DoT уйти в
        // бесконечность на дробнозарядном оружии.
        if (bleedStacks < MaxBleedStacks)
            bleedStacks++;

        bleedDamagePerTick =
            damagePerSecond * tickInterval * bleedStacks;
        bleedRemainingTime = duration;
        bleedTickInterval = tickInterval;

        // Как и с горением, таймер тика сбрасывается только при
        // первом наложении, иначе быстрая стрельба гасит урон.
        if (!bleedActive)
            bleedTickTimer = tickInterval;

        bleedActive = true;
    }


    // =========================================================
    // LIGHTNING
    // =========================================================

    public void TriggerLightning(
        float damage,
        int targetCount,
        float range)
    {
        if (IsDead)
            return;

        if (damage <= 0f ||
            range <= 0f)
            return;

        targetCount =
            Mathf.Max(
                targetCount,
                1
            );

        Collider[] nearbyColliders =
            StructureQuery.OverlapSphere(
                transform.position,
                range,
                out int colliderCount
            );

        int targetsHit = 0;

        for (int i = 0; i < colliderCount; i++)
        {
            if (targetsHit >= targetCount)
                break;

            Enemy target =
                ColliderKindQuery.GetEnemy(nearbyColliders[i]);

            if (target == null ||
                target == this ||
                target.IsDead)
            {
                continue;
            }

            target.TakeDamage(
                damage,
                false
            );

            targetsHit++;
        }
    }


    // =========================================================
    // CONTACT DAMAGE
    // =========================================================

    private void OnCollisionStay(
        Collision collision)
    {
        if (IsDead)
            return;

        if (!collision.gameObject.CompareTag("Player"))
            return;

        if (contactDamageTimer > 0f)
            return;

        PlayerHealth health =
            collision.gameObject.GetComponent<PlayerHealth>();

        if (health == null)
            return;

        float damage = 1f;

        if (enemyData != null)
        {
            damage =
                enemyData.ContactDamage;
        }

        if (isBoss &&
            bossData != null)
        {
            damage =
                bossData.ContactDamage;

            if (BossPhase == 3)
            {
                damage *=
                    bossData.Phase3DamageMultiplier;
            }
        }

        damage *= scaledDamageMultiplier;

        health.TakeDamage(damage);

        float cooldown =
            enemyData != null
                ? enemyData.DamageCooldown
                : 1f;

        contactDamageTimer =
            cooldown;
    }


    // =========================================================
    // DAMAGE NUMBER
    // =========================================================

    private void SpawnDamageNumber(
        float damage,
        bool isCritical)
    {
        // Разброс остаётся здесь: это "где именно попало", то есть
        // правила боя. Всё остальное (общий канвас, пул, подъём и
        // размер) делает DamageNumberSystem.
        Vector3 randomOffset =
            new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(0.8f, 1.4f),
                Random.Range(-0.5f, 0.5f)
            );

        DamageNumberSystem.Spawn(
            transform.position + randomOffset,
            damage,
            isCritical,
            damageNumberPrefab);
    }


    // =========================================================
    // DEATH
    // =========================================================

    public void Kill()
    {
        Die();
    }

    private void Die()
    {
        if (IsDead)
            return;

        IsDead = true;

        // Босс в список павших не идёт: его смерть и так
        // заканчивает волну, и трогать его поведение не нужно.
        if (!isBoss)
            RegisterFallenAlly(transform.position, cachedEnemyType);

        SpawnDeathExplosion();
        SpawnBloodPool();
        SpawnLoot();

        // Горение переносится на соседей до сброса флагов: это
        // единственная награда за то, что врага убили огнём, и она
        // отличает горение от кровотечения, которое просто тикает.
        if (burnActive)
            RunUpgrades.Instance?.SpreadBurn(transform.position);

        PlayEnemyDeathSound();

        burnActive = false;
        bleedActive = false;
        bleedStacks = 0;

        // ============================================
        // LEADERBOARD METRICS
        // ============================================

        float scoreValue = 0f;

        if (enemyData != null &&
            isBoss &&
            bossData != null)
        {
            scoreValue = bossData.ScoreValue;
        }
        else if (enemyData != null)
        {
            scoreValue = enemyData.ScoreValue;
        }

        float scoreMultiplier =
            1f + (currentWave - 1) * GetWaveScorePercent();

        float baseScore = scoreValue * scoreMultiplier;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.IncrementKills();
            ScoreManager.Instance.AddScore(
                Mathf.RoundToInt(baseScore), Lang.Get("bonus.enemy_kill")
            );
        }

        // Отправляем метрику в RunMetrics
        RunMetrics metrics =
            FindAnyObjectByType<RunMetrics>();

        if (metrics != null)
        {
            metrics.OnEnemyDied(
                this,
                baseScore,
                false
            );

            ComboSystem combo =
                FindAnyObjectByType<ComboSystem>();
            if (combo != null)
                combo.OnEnemyKilled();
        }

        if (enemyData != null &&
            cachedEnemyType == EnemyType.Boss)
        {
            Debug.Log(
                $"========== BOSS '{name}' DEFEATED =========="
            );
        }

        Destroy(gameObject);
    }

    private void PlayEnemyDeathSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx == null)
            return;

        if (enemyData != null &&
            cachedEnemyType == EnemyType.Boss)
        {
            AudioClip bossDie = sfx.BossDie;

            AudioManager.Instance.PlaySFXAt(
                bossDie != null
                    ? bossDie
                    : sfx.EnemyDie,
                transform.position,
                priority: SfxPriority.High
            );
        }
        else
        {
            AudioManager.Instance.PlaySFXAt(
                sfx.EnemyDie,
                transform.position,
                priority: SfxPriority.Medium
            );
        }
    }


    // =========================================================
    // LOOT
    // =========================================================

    private void SpawnLoot()
    {
        if (enemyData == null)
            return;

        Vector3 position =
            transform.position;

        if (isBoss)
        {
            LootPickup.SpawnHealth(position, 0.35f);
            LootPickup.SpawnCoin(position, 5);

            return;
        }

        if (Random.value < 0.10f)
        {
            LootPickup.SpawnHealth(
                position,
                0.20f
            );
        }

        if (Random.value < 0.15f)
        {
            int coins = Random.value < 0.35f ? 2 : 1;

            LootPickup.SpawnCoin(
                position,
                coins
            );
        }
    }

    // =========================================================
    // BLOOD POOL
    // =========================================================

    private void SpawnBloodPool()
    {
        float poolSize = 1.2f;
        int splatters = 4;
        float maxOffset = 1.1f;

        if (enemyData != null)
        {
            switch (cachedEnemyType)
            {
                case EnemyType.Tank:
                case EnemyType.Elite:
                    poolSize = 1.9f;
                    splatters = 7;
                    maxOffset = 1.6f;
                    break;

                case EnemyType.Boss:
                    poolSize = 4f;
                    splatters = 10;
                    maxOffset = 3f;
                    break;
            }
        }

        BloodPool.SpawnAt(
            transform.position,
            poolSize,
            splatters,
            maxOffset
        );
    }

    // =========================================================
    // DEATH EXPLOSION
    // =========================================================

    // Танк при смерти разлетается веером снарядов — «не стой рядом,
    // чтобы зачистить жирную тушу». Урон снарядов едет от масштаба волны.
    private void SpawnDeathExplosion()
    {
        if (cachedEnemyType != EnemyType.Tank)
            return;

        if (enemyData == null ||
            enemyProjectilePrefab == null)
        {
            return;
        }

        int projectileCount =
            enemyData.DeathExplosionProjectileCount;

        if (projectileCount <= 0)
            return;

        float damage =
            enemyData.DeathExplosionDamage *
            scaledDamageMultiplier;

        float projectileSpeed =
            enemyData.DeathExplosionProjectileSpeed;

        Vector3 origin =
            transform.position +
            Vector3.up * 0.6f;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle =
                (float)i / projectileCount *
                360f +
                Random.Range(-6f, 6f);

            Vector3 direction =
                new Vector3(
                    Mathf.Cos(angle * Mathf.Deg2Rad),
                    0f,
                    Mathf.Sin(angle * Mathf.Deg2Rad)
                );

            direction.Normalize();

            EnemyProjectile projectile =
                EnemyProjectilePool.Spawn(
                    enemyProjectilePrefab,
                    origin,
                    Quaternion.LookRotation(direction)
                );

            if (projectile != null)
            {
                projectile.Initialize(
                    direction,
                    damage,
                    projectileSpeed
                );
            }
        }

        PlayTankExplosionSound();
    }

    private void PlayTankExplosionSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXAt(
                sfx.BossAoeExplode,
                transform.position,
                priority: SfxPriority.High
            );
    }


}
