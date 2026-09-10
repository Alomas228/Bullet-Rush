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
    [SerializeField] private int bonusProjectiles;

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

    public int BonusProjectiles =>
        Mathf.Max(bonusProjectiles, 0);

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

    public void AddProjectileCount(float amount)
    {
        bonusProjectiles += Mathf.RoundToInt(amount);

        bonusProjectiles =
            Mathf.Max(bonusProjectiles, 0);
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