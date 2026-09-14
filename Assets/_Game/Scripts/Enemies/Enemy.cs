using System.Collections;
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

    private float currentHealth;

    private int currentWave = 1;
    private float scaledMaxHealth;
    private float scaledDamageMultiplier = 1f;

    private float contactDamageTimer;
    private float attackTimer;
    private float abilityTimer;
    private float summonTimer;

    private Coroutine burnCoroutine;

    private Collider selfCollider;
    private Rigidbody cachedRigidbody;

    // Состояние обхода препятствий.
    private int avoidSide;
    private float avoidSideTimer;
    private int clearFrames;

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

        scaledMaxHealth = GetBaseMaxHealth();
        currentHealth = scaledMaxHealth;

        if (attackPoint == null)
            attackPoint = transform;

        if (enemySpawner == null)
            enemySpawner = FindAnyObjectByType<EnemySpawner>();

        LockVerticalRigidbody();
    }

    // Враги двигаются через transform, физика нужна только для
    // контактного урона. Замораживаем вертикаль, чтобы рывок игрока
    // не выбивал моба в воздух.
    private void LockVerticalRigidbody()
    {
        if (cachedRigidbody == null)
            return;

        cachedRigidbody.constraints |=
            RigidbodyConstraints.FreezePositionY;
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
            enemyData.EnemyType == EnemyType.Boss &&
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
            enemyData.EnemyType == EnemyType.Boss &&
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
            enemyData.EnemyType == EnemyType.Boss &&
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
            enemyData.EnemyType == EnemyType.Boss &&
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
            FindPlayer();

            if (player == null)
                return;
        }

        UpdateTimers();

        if (enemyData == null)
        {
            MoveTowardsPlayer();
            return;
        }

        switch (enemyData.EnemyType)
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
        if (contactDamageTimer > 0f)
            contactDamageTimer -= Time.deltaTime;

        if (attackTimer > 0f)
            attackTimer -= Time.deltaTime;

        if (abilityTimer > 0f)
            abilityTimer -= Time.deltaTime;

        if (summonTimer > 0f)
            summonTimer -= Time.deltaTime;
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

        Vector3 moveDirection =
            AvoidObstacles(desired);

        transform.position +=
            moveDirection *
            speed *
            Time.deltaTime;

        ResolveStructureOverlap();

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

        if (avoidSideTimer > 0f)
            avoidSideTimer -= Time.deltaTime;

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
            if (hit.collider.GetComponentInParent<WorldStructure>() != null)
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

        Collider[] nearby =
            Physics.OverlapSphere(
                selfCollider.bounds.center,
                checkRadius
            );

        foreach (Collider structure in nearby)
        {
            if (structure.GetComponentInParent<WorldStructure>() == null)
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
            if (hit.collider.GetComponentInParent<WorldStructure>() != null)
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

        if (!HasLineOfSightToPlayer())
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

        if (distance > bossData.AttackRange ||
            !HasLineOfSightToPlayer())
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

        Vector3 moveDirection =
            AvoidObstacles(desired);

        transform.position +=
            moveDirection *
            speed *
            Time.deltaTime;

        ResolveStructureOverlap();

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

        if (attackPoint == null)
            attackPoint = transform;

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

        if (enemyData != null &&
            enemyData.EnemyType == EnemyType.Boss &&
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

        if (burnCoroutine != null)
        {
            StopCoroutine(
                burnCoroutine
            );
        }

        burnCoroutine =
            StartCoroutine(
                BurnRoutine(
                    damagePerSecond,
                    duration,
                    tickInterval
                )
            );
    }

    private IEnumerator BurnRoutine(
        float damagePerSecond,
        float duration,
        float tickInterval)
    {
        float elapsed = 0f;

        while (
            elapsed < duration &&
            !IsDead)
        {
            yield return new WaitForSeconds(
                tickInterval
            );

            if (IsDead)
                yield break;

            float burnDamage =
                damagePerSecond *
                tickInterval;

            TakeDamage(
                burnDamage,
                false
            );

            elapsed += tickInterval;
        }

        burnCoroutine = null;
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

        Collider[] colliders =
            Physics.OverlapSphere(
                transform.position,
                range
            );

        int targetsHit = 0;

        foreach (Collider collider in colliders)
        {
            if (targetsHit >= targetCount)
                break;

            Enemy target =
                collider.GetComponent<Enemy>();

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

        if (enemyData != null &&
            enemyData.EnemyType == EnemyType.Boss &&
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

        if (burnCoroutine != null)
        {
            StopCoroutine(
                burnCoroutine
            );

            burnCoroutine = null;
        }

        if (ScoreManager.Instance != null)
        {
            ScoreManager.Instance.IncrementKills();

            float scoreMultiplier =
                1f + (currentWave - 1) * GetWaveScorePercent();

            if (enemyData != null &&
                enemyData.EnemyType == EnemyType.Boss &&
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
            enemyData.EnemyType == EnemyType.Boss)
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
            enemyData.EnemyType == EnemyType.Boss)
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

        bool isBoss =
            enemyData.EnemyType == EnemyType.Boss;

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
            enemyData.EnemyType == EnemyType.Boss &&
            bossData != null)
        {
            return 100;
        }

        if (enemyData == null)
            return 5;

        switch (enemyData.EnemyType)
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
            switch (enemyData.EnemyType)
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