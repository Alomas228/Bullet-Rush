using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;

    [Header("Collision")]
    [Tooltip("Как часто пуля проверяет попадание в стену. Пуля летит по прямой, поэтому проверка на накопленном отрезке равносильна проверке каждый кадр.")]
    [SerializeField] private float structureCheckInterval = 1f / 60f;

    [Header("Pierce")]
    [SerializeField] private int pierceCount = 0;

    [Header("Knockback")]
    [Tooltip("Сила отдачи от попадания (ед/с). Насколько моб устойчив к ней — задаёт EnemyData: у босса, элиты и танка отдачи нет совсем.")]
    [SerializeField] private float knockbackForce = 2.5f;

    [Header("Burn")]
    [SerializeField] private float burnDamagePerSecond = 0f;
    [SerializeField] private float burnDuration = 0f;
    [SerializeField] private float burnTickInterval = 0.5f;

    [Header("Lightning")]
    [SerializeField] private float lightningChance = 0f;
    [SerializeField] private float lightningDamage = 0f;
    [SerializeField] private int lightningTargets = 1;
    [SerializeField] private float lightningRange = 0f;

    // =========================================================
    // RUN UPGRADE EFFECTS (задаются из RunUpgrades в Weapon)
    // =========================================================

    private float lifestealPercent;

    private float runBurnChance;
    private float runBurnDamage;
    private float runBurnDuration;
    private float runBurnTickInterval = 0.5f;

    private float bleedChance;
    private float bleedDamage;
    private float bleedDuration;
    private float bleedTickInterval = 0.5f;

    private float explosionRadius;
    private float explosionDamage;

    private float chainLightningChance;
    private float chainLightningDamage;
    private float chainLightningRadius;
    private int chainLightningMaxTargets;

    private float ricochetChance;
    private int ricochetBouncesRemaining;
    private float ricochetSearchRadius;
    private float ricochetDamageMultiplier = 1f;

    private PlayerHealth cachedPlayerHealth;
    private GameObject bulletPrefab;

    // Враг, в которого снаряд уже попал (нужен рикошету,
    // чтобы новая пуля не ударила в ту же цель повторно).
    private Enemy ignoredEnemy;

    private bool isCritical;
    private int enemiesHit;

    private float lifetimeRemaining;

    // Общий буфер рейкаста: попаданий вдоль отрезка пули единицы,
    // аллокаций на кадр быть не должно. Не readonly — перерастает,
    // если вдоль отрезка всё же оказалось больше попаданий, чем
    // влезло: иначе RaycastNonAlloc молча выбросил бы стену из
    // результата и пуль проскочил бы насквозь.
    private static RaycastHit[] structureHitBuffer =
        new RaycastHit[8];

    // Точка, откуда пойдёт следующий рейкаст по стенам. Обновляется
    // только после успешной проверки, иначе пропущенные кадры
    // оставят зазор, через который пуль проскочит.
    private Vector3 lastStructureCheckPosition;
    private float structureCheckTimer;

    // Ключ пула (по префабу): пустой = объект живёт вне пула.
    public EntityId? PoolKey { get; internal set; }

    private void Awake()
    {
        ApplyGlowMaterial();
    }

    /// <summary>
    /// Тело пули раньше было отдельным Sphere-префабом со своим
    /// MeshRenderer, и снаряд стоил 2 draw call: шар плюс хвост.
    /// Теперь круглое ядро нарисовано вторым квадом внутри меша
    /// трассера (см. TracerEffect), поэтому рендерер шара
    /// выключается: он остаётся в префабе, но не рисуется и
    /// не стоит ничего.
    ///
    /// Рендерер НЕ удаляется из префаба намеренно - гашение
    /// обратимо одной строкой, а править YAML префаба руками
    /// ради одной строчки рискованнее.
    ///
    /// Если понадобится вернуть шар обратно - достаточно
    /// вернуть enabled = true и убрать квад ядра из TracerEffect.
    /// </summary>
    private void ApplyGlowMaterial()
    {
        MeshRenderer meshRenderer =
            GetComponent<MeshRenderer>();

        if (meshRenderer == null)
            return;

        meshRenderer.enabled = false;
    }

    private void Update()
    {
        lifetimeRemaining -= Time.deltaTime;

        if (lifetimeRemaining <= 0f)
        {
            ReturnToPool();
            return;
        }

        transform.Translate(
            Vector3.forward *
            speed *
            Time.deltaTime
        );

        // Проверка стен идёт не каждый кадр, а по таймеру. Пуля летит
        // по прямой с постоянной скоростью, поэтому рейкаст от точки
        // прошлой проверки до текущей даёт тот же ответ, что и проверка
        // в каждом кадре, — а на 144 fps рейкастов выходит втрое меньше.
        if (structureCheckTimer > 0f)
        {
            structureCheckTimer -= Time.deltaTime;
            return;
        }

        structureCheckTimer =
            Mathf.Max(structureCheckInterval, 0.005f);

        if (HitStructure(
                lastStructureCheckPosition,
                transform.position))
        {
            ReturnToPool();
            return;
        }

        // Точка отсчёта двигается только после проверки: иначе
        // отрезок перестанет покрывать путь, и пуль проскочит стену
        // на пропущенных кадрах.
        lastStructureCheckPosition =
            transform.position;
    }

    public void ReturnToPool()
    {
        lifetimeRemaining = 0f;
        ignoredEnemy = null;
        enemiesHit = 0;

        BulletPool.Despawn(this);
    }

    // Отсюда пуля продолжит полёт в следующий шаг проверки.
    private void ResetStructureCheck()
    {
        structureCheckTimer = 0f;

        lastStructureCheckPosition =
            transform.position;
    }

    private bool HitStructure(
        Vector3 from,
        Vector3 to)
    {
        Vector3 delta =
            to - from;

        float distance =
            delta.magnitude;

        if (distance <= 0.0001f)
            return false;

        int hitCount =
            Physics.RaycastNonAlloc(
                from,
                delta / distance,
                structureHitBuffer,
                distance);

        while (hitCount == structureHitBuffer.Length)
        {
            structureHitBuffer =
                new RaycastHit[structureHitBuffer.Length * 2];

            hitCount =
                Physics.RaycastNonAlloc(
                    from,
                    delta / distance,
                    structureHitBuffer,
                    distance);
        }

        // Порядок попаданий в буфере не гарантирован, поэтому берём
        // ближайшую именно структуру, а не первую попавшуюся.
        // Заодно это чинит тоннелирование: раньше Physics.Raycast
        // возвращал лишь ближайший коллайдер, и если это был враг,
        // проверка стен на этом кадре просто не выполнялась.
        bool found = false;

        float nearestDistance =
            float.MaxValue;

        Vector3 nearestPoint =
            to;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = structureHitBuffer[i];

            if (!StructureQuery.IsWorldStructure(hit.collider))
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearestDistance = hit.distance;
            nearestPoint = hit.point;
            found = true;
        }

        if (!found)
            return false;

        transform.position = nearestPoint;

        return true;
    }

    public void Initialize(
        float newDamage,
        float newSpeed,
        bool critical,
        int newPierceCount,

        float newBurnDamagePerSecond = 0f,
        float newBurnDuration = 0f,
        float newBurnTickInterval = 0.5f,

        float newLightningChance = 0f,
        float newLightningDamage = 0f,
        int newLightningTargets = 1,
        float newLightningRange = 0f
    )
    {
        damage = newDamage;
        speed = newSpeed;
        isCritical = critical;
        pierceCount = newPierceCount;

        burnDamagePerSecond =
            newBurnDamagePerSecond;

        burnDuration =
            newBurnDuration;

        burnTickInterval =
            newBurnTickInterval;

        lightningChance =
            newLightningChance;

        lightningDamage =
            newLightningDamage;

        lightningTargets =
            newLightningTargets;

        lightningRange =
            newLightningRange;

        enemiesHit = 0;

        lifetimeRemaining = lifetime;

        // Пул переиспользует объект, поэтому отсчёт рейкаста по стенам
        // надо сбросить: иначе пуля начнёт проверку от точки, где
        // лежала прошлой стрельбой, и пролетит полкарты вслепую.
        ResetStructureCheck();

        // Пул переиспользует объект: сбрасываем цель, в которую
        // рикошетная пуля уже попала.
        ignoredEnemy = null;
    }

    /// <summary>
    /// Переносит параметры временных улучшений забега на снаряд.
    /// Вызывается Weapon сразу после Initialize.
    /// </summary>
    public void SetRunEffects(
        RunUpgrades run,
        PlayerHealth health,
        GameObject bulletPrefabRef,
        float damageMultiplier = 1f)
    {
        cachedPlayerHealth = health;
        bulletPrefab = bulletPrefabRef;

        if (run == null)
            return;

        lifestealPercent =
            run.LifestealPercent;

        runBurnChance =
            run.RunBurnChance;

        runBurnDamage =
            run.RunBurnDamage * damageMultiplier;

        runBurnDuration =
            run.RunBurnDuration;

        runBurnTickInterval =
            run.RunBurnTickInterval;

        bleedChance =
            run.BleedChance;

        bleedDamage =
            run.BleedDamage * damageMultiplier;

        bleedDuration =
            run.BleedDuration;

        bleedTickInterval =
            run.BleedTickInterval;

        explosionRadius =
            run.ExplosionRadius;

        explosionDamage =
            run.ExplosionDamage * damageMultiplier;

        chainLightningChance =
            run.LightningChance;

        chainLightningDamage =
            run.LightningDamage * damageMultiplier;

        chainLightningRadius =
            run.LightningRadius;

        chainLightningMaxTargets =
            run.LightningMaxTargets;

        ricochetChance =
            run.RicochetChance;

        ricochetBouncesRemaining =
            run.RicochetMaxBounces;

        ricochetSearchRadius =
            run.RicochetSearchRadius;

        ricochetDamageMultiplier =
            run.RicochetDamageMultiplier;
    }

    /// <summary>
    /// Переносит на рикошетную пулю только безвредные модификаторы:
    /// DOT и лайфстил. Каскадные эффекты (взрыв, цепная молния,
    /// повторные рикошеты) отключены, чтобы стак апгрейдов
    /// не давал экспоненциального урона.
    /// </summary>
    private void CopyRunEffectsFrom(
        Bullet source)
    {
        lifestealPercent =
            source.lifestealPercent;

        runBurnChance =
            source.runBurnChance;

        runBurnDamage =
            source.runBurnDamage;

        runBurnDuration =
            source.runBurnDuration;

        runBurnTickInterval =
            source.runBurnTickInterval;

        bleedChance =
            source.bleedChance;

        bleedDamage =
            source.bleedDamage;

        bleedDuration =
            source.bleedDuration;

        bleedTickInterval =
            source.bleedTickInterval;

        chainLightningChance = 0f;
        chainLightningDamage = 0f;
        chainLightningRadius = 0f;
        chainLightningMaxTargets = 0;

        ricochetChance = 0f;
        ricochetBouncesRemaining = 0;
        ricochetSearchRadius = 0f;
        ricochetDamageMultiplier = 1f;

        cachedPlayerHealth =
            source.cachedPlayerHealth;

        bulletPrefab =
            source.bulletPrefab;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (StructureQuery.IsWorldStructure(other))
        {
            ReturnToPool();
            return;
        }

        Enemy enemy =
            ColliderKindQuery.GetEnemy(other);

        if (enemy == null)
            return;

        if (enemy.IsDead)
            return;

        if (enemy == ignoredEnemy)
            return;

        // ============================================
        // NORMAL DAMAGE
        // ============================================

        enemy.TakeDamage(
            damage,
            isCritical
        );

        // Отдача: моб сбавляет разгон и чуть отходит назад.
        // Мёртвому она не нужна, а выбитый из толка моб уехал бы
        // уже без своей воли.
        if (knockbackForce > 0f &&
            !enemy.IsDead)
        {
            enemy.ApplyKnockback(
                transform.forward,
                knockbackForce
            );
        }

        // ============================================
        // IMPACT VFX
        // ============================================

        // Вспышка с осколками забирается из пула, бюджет на кадр
        // держит VfxFactory, чтобы залп в упор не превращался
        // в белое пятно.
        VfxFactory.SpawnImpact(
            transform.position,
            transform.forward
        );

        // Кровь: капли разлетаются от точки попадания по
        // направлению полёта пули. Отдельный бюджет на кадр.
        VfxFactory.SpawnBlood(
            transform.position,
            transform.forward
        );

        // ============================================
        // BURN
        // ============================================

        if (
            burnDamagePerSecond > 0f &&
            burnDuration > 0f
        )
        {
            enemy.ApplyBurn(
                burnDamagePerSecond,
                burnDuration,
                burnTickInterval
            );
        }

        // ============================================
        // LIGHTNING
        // ============================================

        if (
            lightningChance > 0f &&
            lightningDamage > 0f &&
            lightningRange > 0f
        )
        {
            if (!VfxFactory.IsLightningMerged(
                    transform.position))
            {
                VfxFactory.MarkLightningAttempt(
                    transform.position
                );

                if (
                    Random.value <
                    lightningChance
                )
                {
                    enemy.TriggerLightning(
                        lightningDamage,
                        lightningTargets,
                        lightningRange
                    );

                    PlayLightningSound();
                }
            }
        }

        // ============================================
        // RUN UPGRADES: LIFESTEAL
        // ============================================

        if (
            lifestealPercent > 0f &&
            cachedPlayerHealth != null
        )
        {
            cachedPlayerHealth.Heal(
                damage * lifestealPercent
            );
        }

        // ============================================
        // RUN UPGRADES: BURNING ROUNDS
        // ============================================

        if (
            runBurnChance > 0f &&
            runBurnDamage > 0f &&
            runBurnDuration > 0f
        )
        {
            if (
                Random.value <
                runBurnChance
            )
            {
                enemy.ApplyBurn(
                    runBurnDamage,
                    runBurnDuration,
                    runBurnTickInterval
                );
            }
        }

        // ============================================
        // RUN UPGRADES: BLEEDING
        // ============================================

        if (
            bleedChance > 0f &&
            bleedDamage > 0f &&
            bleedDuration > 0f
        )
        {
            if (
                Random.value <
                bleedChance
            )
            {
                enemy.ApplyBleeding(
                    bleedDamage,
                    bleedDuration,
                    bleedTickInterval
                );
            }
        }

        // ============================================
        // RUN UPGRADES: EXPLOSIVE ROUNDS
        // ============================================

        if (
            explosionRadius > 0f &&
            explosionDamage > 0f
        )
        {
            TriggerExplosion(enemy);
        }

        // ============================================
        // RUN UPGRADES: CHAIN LIGHTNING
        // ============================================

        if (
            chainLightningChance > 0f &&
            chainLightningDamage > 0f &&
            chainLightningRadius > 0f
        )
        {
            if (
                Random.value <
                chainLightningChance
            )
            {
                TriggerChainLightning(enemy);
            }
        }

        // ============================================
        // RUN UPGRADES: RICOCHET
        // ============================================

        if (
            ricochetChance > 0f &&
            ricochetBouncesRemaining > 0
        )
        {
            if (
                Random.value <
                ricochetChance
            )
            {
                CreateRicochetBullet(enemy);
            }
        }

        // ============================================
        // PIERCE
        // ============================================

        enemiesHit++;

        if (enemiesHit > pierceCount)
        {
            ReturnToPool();
        }
    }

    // =========================================================
    // EXPLOSION
    // =========================================================

    private void TriggerExplosion(
        Enemy hitEnemy)
    {
        if (!VfxFactory.TrySpawnExplosion(
                transform.position,
                explosionRadius,
                VfxFactory.DefaultExplosionColor))
        {
            return;
        }

        PlayExplosionSound();

        Collider[] colliders =
            StructureQuery.OverlapSphere(
                transform.position,
                explosionRadius,
                out int colliderCount
            );

        for (int i = 0; i < colliderCount; i++)
        {
            Enemy target =
                ColliderKindQuery.GetEnemy(colliders[i]);

            if (target == null ||
                target == hitEnemy ||
                target.IsDead)
            {
                continue;
            }

            target.TakeDamage(
                explosionDamage,
                false
            );

            ApplyAreaDotEffects(target);
            ApplyAreaLifesteal(
                explosionDamage
            );
        }
    }

    private void PlayExplosionSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXVariationAt(
                sfx.BulletExplosion,
                transform.position,
                priority: SfxPriority.High
            );
    }

    private void PlayLightningSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXVariationAt(
                sfx.Lightning,
                transform.position,
                priority: SfxPriority.High
            );
    }

    private void PlayRicochetSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXVariationAt(
                sfx.Ricochet,
                transform.position,
                priority: SfxPriority.Low
            );
    }

    // =========================================================
    // CHAIN LIGHTNING
    // =========================================================

    // Цели, уже пробитые текущей цепью. Без этого список
    // проверялся только на первый удар (target == hitEnemy),
    // и молния могла ударить одного и того же моба второй раз -
    // отрезок уходил обратно через всю карту.
    private static readonly Enemy[] visitedEnemies =
        new Enemy[16];

    private static int visitedCount;

    private static bool WasVisited(Enemy enemy)
    {
        for (int i = 0; i < visitedCount; i++)
        {
            if (visitedEnemies[i] == enemy)
                return true;
        }

        return false;
    }

    private void TriggerChainLightning(
        Enemy hitEnemy)
    {
        // Список пробитых целей общий на весь вызов: рекурсии
        // внутри нет (TriggerChainLightning зовётся только из
        // OnTriggerEnter и не вызывает сам себя).
        visitedCount = 0;

        Vector3 fromPosition =
            transform.position;

        // Молния «сходит» на цель сверху, чтобы отрезок был виден
        // даже при одиночном попадании (иначе пуля в коллизии
        // почти совпадает с целью и сегмент нулевой длины).
        fromPosition +=
            Vector3.up * 0.4f;

        // Первый «захват» визуально бьёт в цель попадания пули.
        VfxFactory.SpawnLightning(
            fromPosition,
            hitEnemy.transform.position
        );

        PlayLightningSound();

        // Голова цепи теперь на первом пробитом мобе.
        fromPosition =
            hitEnemy.transform.position;

        visitedEnemies[visitedCount++] =
            hitEnemy;

        // Поиск следующей цели идёт ВОКРУГ ГОЛОВЫ ЦЕПИ, а не
        // вокруг пули. Раньше центром был transform.position,
        // то есть точка попадания: все следующие цели выбирались
        // из одного места у пули, а рисовался отрезок от головы
        // цепи до них. Из-за этого молния ходила не наружу от
        // цепи, а постоянно возвращалась к месту выстрела -
        // в бою у центра карты это и читалось как «бьёт в центр».
        Collider[] colliders =
            StructureQuery.OverlapSphere(
                fromPosition,
                chainLightningRadius,
                out int colliderCount
            );

        int targetsHit = 0;

        for (int i = 0; i < colliderCount; i++)
        {
            if (targetsHit >= chainLightningMaxTargets)
                break;

            Enemy target =
                ColliderKindQuery.GetEnemy(colliders[i]);

            if (target == null ||
                target == hitEnemy ||
                target.IsDead ||
                WasVisited(target))
            {
                continue;
            }

            target.TakeDamage(
                chainLightningDamage,
                false
            );

            VfxFactory.SpawnLightning(
                fromPosition,
                target.transform.position
            );

            fromPosition =
                target.transform.position;

            if (visitedCount < visitedEnemies.Length)
                visitedEnemies[visitedCount++] =
                    target;

            ApplyAreaDotEffects(target);
            ApplyAreaLifesteal(
                chainLightningDamage
            );

            targetsHit++;
        }
    }

    // =========================================================
    // RICOCHET
    // =========================================================

    private void CreateRicochetBullet(
        Enemy hitEnemy)
    {
        if (bulletPrefab == null)
            return;

        Collider[] colliders =
            StructureQuery.OverlapSphere(
                transform.position,
                ricochetSearchRadius,
                out int colliderCount
            );

        Enemy closest =
            null;

        float closestDistance =
            float.MaxValue;

        for (int i = 0; i < colliderCount; i++)
        {
            Enemy target =
                ColliderKindQuery.GetEnemy(colliders[i]);

            if (target == null ||
                target == hitEnemy ||
                target.IsDead)
            {
                continue;
            }

            float distance =
                Vector3.Distance(
                    transform.position,
                    target.transform.position
                );

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = target;
            }
        }

        if (closest == null)
            return;

        VfxFactory.SpawnRicochet(
            transform.position,
            closest.transform.position
        );

        PlayRicochetSound();

        Vector3 direction =
            closest.transform.position -
            transform.position;

        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();

        Quaternion rotation =
            Quaternion.LookRotation(direction);

        Vector3 spawnPosition =
            transform.position +
            direction * 0.5f;

        Bullet bullet =
            BulletPool.Spawn(
                bulletPrefab,
                spawnPosition,
                rotation
            );

        if (bullet == null)
            return;

        bullet.Initialize(
            damage * ricochetDamageMultiplier,
            speed,
            isCritical,
            0,
            burnDamagePerSecond,
            burnDuration,
            burnTickInterval,
            lightningChance,
            lightningDamage,
            lightningTargets,
            lightningRange
        );

        bullet.CopyRunEffectsFrom(
            this
        );

        // Новая пуля стартует в точке удара, поэтому запрещаем
        // ей повторно попадать в только что задетую цель.
        bullet.ignoredEnemy = hitEnemy;
    }

    // =========================================================
    // AREA EFFECT HELPERS
    // =========================================================

    private void ApplyAreaDotEffects(
        Enemy target)
    {
        if (
            runBurnChance > 0f &&
            runBurnDamage > 0f &&
            runBurnDuration > 0f
        )
        {
            if (
                Random.value <
                runBurnChance
            )
            {
                target.ApplyBurn(
                    runBurnDamage,
                    runBurnDuration,
                    runBurnTickInterval
                );
            }
        }

        if (
            bleedChance > 0f &&
            bleedDamage > 0f &&
            bleedDuration > 0f
        )
        {
            if (
                Random.value <
                bleedChance
            )
            {
                target.ApplyBleeding(
                    bleedDamage,
                    bleedDuration,
                    bleedTickInterval
                );
            }
        }
    }

    private void ApplyAreaLifesteal(
        float sourceDamage)
    {
        if (
            lifestealPercent > 0f &&
            cachedPlayerHealth != null
        )
        {
            cachedPlayerHealth.Heal(
                sourceDamage *
                lifestealPercent
            );
        }
    }
}