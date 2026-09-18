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
    [SerializeField] private GameObject damageNumberPrefab;

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

    [Header("Performance")]
    [Tooltip("Как часто обновлять дорогую проверку обхода препятствий.")]
    [SerializeField] private float navigationUpdateInterval = 0.05f;
    [Tooltip("Как часто проверять пересечение врага со структурами.")]
    [SerializeField] private float structureResolveInterval = 0.10f;
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

    private Collider selfCollider;
    private Rigidbody cachedRigidbody;

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
    }

    public bool IsDead { get; private set; }

    public float CurrentHealth => currentHealth;

    public EnemyData GetEnemyData()
    {
        return enemyData;
    }

    public float MaxHealth => scaledMaxHealth;

    public int SpawnWave => currentWave;

    public int BossPhase { get; private set; } = 1;


    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        FindPlayer();

        selfCollider = GetComponent<Collider>();
        cachedRigidbody = GetComponent<Rigidbody>();

        cachedEnemyType = enemyData != null ? enemyData.EnemyType : EnemyType.Normal;
        isBoss = enemyData != null && cachedEnemyType == EnemyType.Boss;

        scaledMaxHealth = GetBaseMaxHealth();
        currentHealth = scaledMaxHealth;

        if (attackPoint == null)
            attackPoint = transform;

        if (enemySpawner == null)
            enemySpawner = FindAnyObjectByType<EnemySpawner>();

        AliveCount++;

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

        UpdateTimers();

        if (enemyData == null)
        {
            MoveTowardsPlayer();
            return;
        }

        switch (cachedEnemyType)
        {
            case EnemyType.Ranged:
                HandleRangedBehaviour();
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


    // =========================================================
    // PLAYER
    // =========================================================

    private void FindPlayer()
    {
        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
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

        desired.Normalize();

        float speed =
            enemyData != null
                ? enemyData.MoveSpeed
                : 2f;

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

    // Выталкиваем врага из стен, если движение всё же занесло
    // его на кромку блока, чтобы он не «цеплялся» краем коллайдера.
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
            Collider structure = nearbyColliders[i];

            if (!StructureQuery.IsWorldStructure(structure))
                continue;

            if (!Physics.ComputePenetration(
                selfCollider,
                position,
                transform.rotation,
                structure,
                structure.transform.position,
                structure.transform.rotation,
                out Vector3 direction,
                out float distance))
            {
                continue;
            }

            if (distance > 0.05f)
                position += direction * (distance + 0.02f);
        }

        if ((position - transform.position).sqrMagnitude > 0.0001f)
            transform.position = position;
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
            ShootAtPlayer(
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
        if (currentHealth >= scaledMaxHealth * 0.8f)
            return;

        float healAmount =
            scaledMaxHealth * 0.05f;

        currentHealth =
            Mathf.Min(
                currentHealth + healAmount,
                scaledMaxHealth
            );

        Debug.Log(
            $"Elite '{name}' healed {healAmount:0} HP."
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

        float speed = GetBossMoveSpeed();

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

        GameObject projectileObject =
            Instantiate(
                enemyProjectilePrefab,
                attackPoint.position,
                Quaternion.LookRotation(direction)
            );

        EnemyProjectile projectile =
            projectileObject.GetComponent<EnemyProjectile>();

        if (projectile != null)
        {
            projectile.Initialize(
                direction,
                damage,
                projectileSpeed
            );
        }
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
                nearbyColliders[i].GetComponent<Enemy>();

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

        PlayerHealth playerHealth =
            collision.gameObject.GetComponent<PlayerHealth>();

        if (playerHealth == null)
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

        playerHealth.TakeDamage(damage);

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
        if (damageNumberPrefab == null)
            return;

        Vector3 randomOffset =
            new Vector3(
                Random.Range(-0.5f, 0.5f),
                Random.Range(0.8f, 1.4f),
                Random.Range(-0.5f, 0.5f)
            );

        GameObject numberObject =
            Instantiate(
                damageNumberPrefab,
                transform.position +
                randomOffset,
                Quaternion.identity
            );

        DamageNumber damageNumber =
            numberObject.GetComponent<DamageNumber>();

        if (damageNumber != null)
        {
            damageNumber.Initialize(
                damage,
                isCritical
            );
        }
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

        SpawnBloodPool();
        SpawnLoot();

        PlayEnemyDeathSound();

        burnActive = false;
        bleedActive = false;

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.IncrementKills();

            float scoreMultiplier =
                1f + (currentWave - 1) * GetWaveScorePercent();

            if (enemyData != null &&
                isBoss &&
                bossData != null)
            {
                ScoreManager.Instance.AddScore(
                    Mathf.RoundToInt(
                        bossData.ScoreValue * scoreMultiplier
                    )
                );
            }
            else if (enemyData != null)
            {
                ScoreManager.Instance.AddScore(
                    Mathf.RoundToInt(
                        enemyData.ScoreValue * scoreMultiplier
                    )
                );
            }
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

        LootPickup.SpawnXP(
            position,
            GetXpValue()
        );

        if (isBoss)
        {
            LootPickup.SpawnHealth(position, 0.35f);
            LootPickup.SpawnCoin(position, 3);

            return;
        }

        if (Random.value < 0.08f)
        {
            LootPickup.SpawnHealth(
                position,
                0.20f
            );
        }

        if (Random.value < 0.12f)
        {
            LootPickup.SpawnCoin(
                position,
                1
            );
        }
    }

    private int GetXpValue()
    {
        if (enemyData != null &&
            cachedEnemyType == EnemyType.Boss &&
            bossData != null)
        {
            return 100;
        }

        if (enemyData == null)
            return 5;

        switch (cachedEnemyType)
        {
            case EnemyType.Normal:
                return 5;

            case EnemyType.Fast:
                return 7;

            case EnemyType.Tank:
                return 12;

            case EnemyType.Ranged:
                return 8;

            case EnemyType.Elite:
                return 25;

            default:
                return Mathf.Max(enemyData.ScoreValue / 2, 5);
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


}