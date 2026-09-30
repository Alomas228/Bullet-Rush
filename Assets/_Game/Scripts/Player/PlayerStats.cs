using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Damage")]
    [SerializeField] private float damageMultiplier = 1f;

    [Header("Fire Rate")]
    [SerializeField] private float fireRateMultiplier = 1f;

    [Header("Move Speed")]
    [SerializeField] private float moveSpeedMultiplier = 1f;

    [Header("Projectile Speed")]
    [SerializeField] private float projectileSpeedMultiplier = 1f;

    [Header("Critical")]
    [SerializeField] private float criticalChance = 0f;
    [SerializeField] private float criticalDamageMultiplier = 2f;

    [Header("Projectile")]
    // Доля, на которую умножается число снарядов оружия.
    // Раньше здесь была плоская прибавка: "+1 снаряд" на однозарядном
    // оружии — это ровно +100% урона за выстрел, и четыре стака
    // давали +400% к урону, а к нему прибавлялись crit и статусы.
    // Процент ограничен потолком снарядов в Weapon.FireVolley.
    [SerializeField] private float projectileCountPercent;

    [SerializeField] private int bonusPierce;

    [Header("Score")]
    [SerializeField] private float scoreMultiplier = 1f;

    // =========================================================
    // PROPERTIES
    // =========================================================

    public float DamageMultiplier =>
        Mathf.Max(damageMultiplier, 0f);

    public float FireRateMultiplier =>
        Mathf.Max(fireRateMultiplier, 0f);

    public float MoveSpeed =>
        5f * Mathf.Max(moveSpeedMultiplier, 0f);

    public float ProjectileSpeedMultiplier =>
        Mathf.Max(projectileSpeedMultiplier, 0f);

    public float CriticalChance =>
        Mathf.Clamp01(criticalChance);

    public float CriticalDamageMultiplier =>
        Mathf.Max(criticalDamageMultiplier, 1f);

    public float ScoreMultiplier =>
        Mathf.Max(scoreMultiplier, 0f);

    public float ProjectileCountPercent =>
        Mathf.Max(projectileCountPercent, 0f);

    public int BonusPierce =>
        Mathf.Max(bonusPierce, 0);

    // =========================================================
    // DAMAGE
    // =========================================================

    public void AddDamagePercent(float percent)
    {
        damageMultiplier *= 1f + percent;
    }

    // =========================================================
    // FIRE RATE
    // =========================================================

    public void AddFireRatePercent(float percent)
    {
        fireRateMultiplier *= 1f + percent;
    }

    // =========================================================
    // MOVE SPEED
    // =========================================================

    public void AddMoveSpeedPercent(float percent)
    {
        moveSpeedMultiplier *= 1f + percent;
    }

    // =========================================================
    // PROJECTILE SPEED
    // =========================================================

    public void AddProjectileSpeedPercent(float percent)
    {
        projectileSpeedMultiplier *= 1f + percent;
    }

    // =========================================================
    // CRITICAL CHANCE
    // =========================================================

    public void AddCriticalChance(float percent)
    {
        criticalChance += percent;

        criticalChance =
            Mathf.Clamp01(criticalChance);
    }

    // =========================================================
    // CRITICAL DAMAGE
    // =========================================================

    public void AddCriticalDamageMultiplier(float multiplier)
    {
        criticalDamageMultiplier += multiplier;
    }

    // =========================================================
    // SCORE
    // =========================================================

    public void AddScorePercent(float percent)
    {
        scoreMultiplier *= 1f + percent;
    }

    public void RemoveScorePercent(float percent)
    {
        float divisor = 1f + percent;

        if (divisor <= 0f)
            return;

        scoreMultiplier /= divisor;

        scoreMultiplier =
            Mathf.Max(scoreMultiplier, 1f);
    }

    // =========================================================
    // PROJECTILE COUNT
    // =========================================================

    /// <summary>
    /// Прибавляет процент к числу снарядов. Само округление и потолок
    /// применяются в Weapon, потому что зависят от базового числа
    /// снарядов конкретного оружия.
    /// </summary>
    public void AddProjectileCountPercent(float percent)
    {
        projectileCountPercent += percent;

        projectileCountPercent =
            Mathf.Max(projectileCountPercent, 0f);
    }

    // =========================================================
    // PIERCE
    // =========================================================

    public void AddPierce(float amount)
    {
        bonusPierce += Mathf.RoundToInt(amount);

        bonusPierce =
            Mathf.Max(bonusPierce, 0);
    }
}