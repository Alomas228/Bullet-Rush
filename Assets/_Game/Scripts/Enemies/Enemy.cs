using UnityEngine;

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

    // Количество живых врагов в сцене. Позволяет волновому
    // менеджеру обходиться без FindGameObjectsWithTag каждый кадр.
    public static int AliveCount { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        AliveCount = 0;
    }

    private void OnDestroy()
    {
        if (AliveCount > 0)
            AliveCount--;

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

        EnemyHealthBarSystem.Register(this);

        LockVerticalRigidbody();
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

    public void Initialize(int wave)
    {
        currentWave = Mathf.Max(wave, 1);

        ApplyWaveScaling();
    }

    private void ApplyWaveScaling()
    {
        float healthMultiplier =
            1f + (currentWave - 1) * GetWaveHealthPercent();

        float damageMultiplier =
            1f + (currentWave - 1) * GetWaveDamagePercent();

        scaledMaxHealth =
            GetBaseMaxHealth() * healthMultiplier;
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

        desired.Normalize();

        TryStartDash(desired, distanceToPlayer);

        if (isDashing)
        {
            ApplyDash();
            return;
        }

        float speed =
            (enemyData != null
                ? enemyData.MoveSpeed
                : 2f) *
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


    // =========================================================
    // FAST DASH
    // =========================================================

    // Быстрые враги не просто гонятся — вблизи бросаются рывком.
    // Преследование ведёт MoveTowardsPlayer, рывок берёт на себя
    // движение до конца длительности.
    private void HandleFastBehaviour()
    {
        if (isDashing)
        {
            ApplyDash();
            return;
        }

        MoveTowardsPlayer();
    }

    private void TryStartDash(
        Vector3 direction,
        float distanceToPlayer)
    {
        if (isDashing)
            return;

        if (cachedEnemyType != EnemyType.Fast)
            return;

        if (enemyData == null)
            return;

        if (dashTimer > 0f)
            return;

        if (distanceToPlayer > enemyData.DashRange)
            return;

        isDashing = true;
        dashDirection = direction;
        dashRemainingTime = enemyData.DashDuration;
        dashTimer = enemyData.DashCooldown;
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
    // RANGED
    // =========================================================

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

        RotateTowards(direction);

        if (distance > enemyData.AttackRange)
        {
            MoveTowardsPlayer();
            return;
        }

        if (lineOfSightTimer <= 0f)
        {
            cachedLineOfSight = HasLineOfSightToPlayer();
            lineOfSightTimer = Mathf.Max(lineOfSightCheckInterval, 0.05f);
        }

        if (!cachedLineOfSight)
        {
            // Стена между врагом и игроком: подходим, пока не откроется линия огня
            MoveTowardsPlayer();
            return;
        }

        if (attackTimer <= 0f)
        {
            ShootFanAtPlayer(
                enemyData.ProjectileDamage * scaledDamageMultiplier,
                enemyData.ProjectileSpeed
            );

            attackTimer =
                enemyData.AttackRate;
        }
    }


    // =========================================================
    // ELITE
    // =========================================================

    private void HandleEliteBehaviour()
    {
        MoveTowardsPlayer();

        if (abilityTimer <= 0f)
        {
            EliteAbility();

            if (enemyData != null)
            {
                abilityTimer =
                    enemyData.AbilityCooldown;
            }
        }
    }

    private void EliteAbility()
    {
        if (enemyData == null)
            return;

        // Вместо пассивного хилинга — зона шока вокруг элиты:
        // нельзя стоять рядом и дожигать её с руки.
        GameObject zoneObject =
            new GameObject("Elite_Shockwave");

        zoneObject.transform.position =
            transform.position;

        zoneObject.transform.rotation =
            Quaternion.identity;

        HazardZone zone =
            zoneObject.AddComponent<HazardZone>();

        zone.Initialize(
            enemyData.AbilityWarning,
            enemyData.AbilityRadius,
            enemyData.AbilityDps,
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
            AudioManager.Instance.PlaySFX(sfx.BossAbility);
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
            AudioManager.Instance.PlaySFX(sfx.BossPhaseChange);
    }

    private void PlayBossAbilitySound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.BossAbility);
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
        switch (BossPhase)
        {
            case 2:
                return bossData.Phase2AbilityDamage;

            case 3:
                return bossData.Phase3AbilityDamage;

            default:
                return bossData.Phase1AbilityDamage;
        }
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
                GetBossAbilityRadius(),
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

        int summonCount =
            GetBossSummonCount();

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
            $"Boss summoned {summonCount} enemies. " +
            $"Phase: {BossPhase}"
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
            AudioManager.Instance.PlaySFXVariation(
                sfx.EnemyShot
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
            AudioManager.Instance.PlaySFX(sfx.EnemyHit);
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

        bleedDamagePerTick =
            damagePerSecond * tickInterval;
        bleedRemainingTime = duration;
        bleedTickInterval = tickInterval;
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

        SpawnDeathExplosion();
        SpawnBloodPool();
        SpawnLoot();

        PlayEnemyDeathSound();

        burnActive = false;
        bleedActive = false;

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
                Mathf.RoundToInt(baseScore), "Убийство врага"
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
            AudioManager.Instance.PlaySFX(sfx.BossSpawn);
        }
        else
        {
            AudioManager.Instance.PlaySFX(sfx.EnemyDie);
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
            AudioManager.Instance.PlaySFX(sfx.BossAoeExplode);
    }


}