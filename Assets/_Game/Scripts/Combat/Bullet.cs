using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;

    [Header("Pierce")]
    [SerializeField] private int pierceCount = 0;

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

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        Vector3 previousPosition =
            transform.position;

        transform.Translate(
            Vector3.forward *
            speed *
            Time.deltaTime
        );

        if (HitStructure(
            previousPosition,
            transform.position))
        {
            Destroy(gameObject);
        }
    }

    private bool HitStructure(
        Vector3 from,
        Vector3 to)
    {
        Vector3 direction =
            to - from;

        float distance =
            direction.magnitude;

        if (distance <= 0.0001f)
            return false;

        if (Physics.Raycast(
            from,
            direction.normalized,
            out RaycastHit hit,
            distance))
        {
            if (hit.collider.GetComponentInParent<WorldStructure>() != null)
            {
                transform.position = hit.point;
                return true;
            }
        }

        return false;
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
    }

    /// <summary>
    /// Переносит параметры временных улучшений забега на снаряд.
    /// Вызывается Weapon сразу после Initialize.
    /// </summary>
    public void SetRunEffects(
        RunUpgrades run,
        PlayerHealth health,
        GameObject bulletPrefabRef)
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
            run.RunBurnDamage;

        runBurnDuration =
            run.RunBurnDuration;

        runBurnTickInterval =
            run.RunBurnTickInterval;

        bleedChance =
            run.BleedChance;

        bleedDamage =
            run.BleedDamage;

        bleedDuration =
            run.BleedDuration;

        bleedTickInterval =
            run.BleedTickInterval;

        explosionRadius =
            run.ExplosionRadius;

        explosionDamage =
            run.ExplosionDamage;

        chainLightningChance =
            run.LightningChance;

        chainLightningDamage =
            run.LightningDamage;

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

    private void CopyRunEffectsFrom(
        Bullet source,
        bool isRicochet)
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

        if (!isRicochet)
        {
            explosionRadius =
                source.explosionRadius;

            explosionDamage =
                source.explosionDamage;
        }

        chainLightningChance =
            source.chainLightningChance;

        chainLightningDamage =
            source.chainLightningDamage;

        chainLightningRadius =
            source.chainLightningRadius;

        chainLightningMaxTargets =
            source.chainLightningMaxTargets;

        ricochetChance =
            source.ricochetChance;

        ricochetBouncesRemaining = isRicochet
            ? source.ricochetBouncesRemaining - 1
            : source.ricochetBouncesRemaining;

        ricochetSearchRadius =
            source.ricochetSearchRadius;

        ricochetDamageMultiplier =
            source.ricochetDamageMultiplier;

        cachedPlayerHealth =
            source.cachedPlayerHealth;

        bulletPrefab =
            source.bulletPrefab;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<WorldStructure>() != null)
        {
            Destroy(gameObject);
            return;
        }

        Enemy enemy =
            other.GetComponent<Enemy>();

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
            Destroy(gameObject);
        }
    }

    // =========================================================
    // EXPLOSION
    // =========================================================

    private void TriggerExplosion(
        Enemy hitEnemy)
    {
        Collider[] colliders =
            Physics.OverlapSphere(
                transform.position,
                explosionRadius
            );

        foreach (Collider collider in colliders)
        {
            Enemy target =
                collider.GetComponent<Enemy>();

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

    // =========================================================
    // CHAIN LIGHTNING
    // =========================================================

    private void TriggerChainLightning(
        Enemy hitEnemy)
    {
        Collider[] colliders =
            Physics.OverlapSphere(
                transform.position,
                chainLightningRadius
            );

        int targetsHit = 0;

        foreach (Collider collider in colliders)
        {
            if (targetsHit >= chainLightningMaxTargets)
                break;

            Enemy target =
                collider.GetComponent<Enemy>();

            if (target == null ||
                target == hitEnemy ||
                target.IsDead)
            {
                continue;
            }

            target.TakeDamage(
                chainLightningDamage,
                false
            );

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
            Physics.OverlapSphere(
                transform.position,
                ricochetSearchRadius
            );

        Enemy closest =
            null;

        float closestDistance =
            float.MaxValue;

        foreach (Collider collider in colliders)
        {
            Enemy target =
                collider.GetComponent<Enemy>();

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

        GameObject bulletObject =
            Instantiate(
                bulletPrefab,
                spawnPosition,
                rotation
            );

        Bullet bullet =
            bulletObject.GetComponent<Bullet>();

        if (bullet == null)
        {
            Destroy(bulletObject);
            return;
        }

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
            this,
            true
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