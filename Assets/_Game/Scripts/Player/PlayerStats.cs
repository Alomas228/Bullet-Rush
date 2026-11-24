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

    // Preserve the inspector's base stats, not a hard-coded set of defaults.
    private float baseDamage, baseFireRate, baseMoveSpeed, baseProjectileSpeed;
    private float baseCriticalChance, baseCriticalDamage, baseProjectileCount, baseScore;
    private int basePierce;
    private float damageBoostUntil, fireBoostUntil, speedBoostUntil;
    private readonly float[] boostDurations = new float[3];

    public enum BattleBoost { Damage, FireRate, MoveSpeed }

    public void GrantBattleBoost(BattleBoost boost, float seconds)
    {
        if (seconds <= 0f || (int)boost < 0 || (int)boost >= boostDurations.Length) return;
        if (seconds >= BattleBoostRemaining(boost)) boostDurations[(int)boost] = seconds;
        float end = Time.time + seconds;
        switch (boost)
        {
            case BattleBoost.Damage: damageBoostUntil = Mathf.Max(damageBoostUntil, end); break;
            case BattleBoost.FireRate: fireBoostUntil = Mathf.Max(fireBoostUntil, end); break;
            case BattleBoost.MoveSpeed: speedBoostUntil = Mathf.Max(speedBoostUntil, end); break;
        }
    }

    public float BattleBoostRemaining(BattleBoost boost)
    {
        float end = boost == BattleBoost.Damage ? damageBoostUntil : boost == BattleBoost.FireRate ? fireBoostUntil : speedBoostUntil;
        return Mathf.Max(0f, end - Time.time);
    }

    public float BattleBoostFraction(BattleBoost boost) => Mathf.Clamp01(BattleBoostRemaining(boost) /
        Mathf.Max(.01f, boostDurations[(int)boost]));

    private void Awake()
    {
        baseDamage = damageMultiplier;
        baseFireRate = fireRateMultiplier;
        baseMoveSpeed = moveSpeedMultiplier;
        baseProjectileSpeed = projectileSpeedMultiplier;
        baseCriticalChance = criticalChance;
        baseCriticalDamage = criticalDamageMultiplier;
        baseProjectileCount = projectileCountPercent;
        basePierce = bonusPierce;
        baseScore = scoreMultiplier;
    }

    internal void ResetForSession()
    {
        damageBoostUntil = fireBoostUntil = speedBoostUntil = 0f;
        System.Array.Clear(boostDurations, 0, boostDurations.Length);
        damageMultiplier = baseDamage;
        fireRateMultiplier = baseFireRate;
        moveSpeedMultiplier = baseMoveSpeed;
        projectileSpeedMultiplier = baseProjectileSpeed;
        criticalChance = baseCriticalChance;
        criticalDamageMultiplier = baseCriticalDamage;
        projectileCountPercent = baseProjectileCount;
        bonusPierce = basePierce;
        scoreMultiplier = baseScore;
    }

    // =========================================================
    // PROPERTIES
    // =========================================================

    public float DamageMultiplier =>
        Mathf.Max(damageMultiplier, 0f) * (Time.time < damageBoostUntil ? 1.35f : 1f);

    public float FireRateMultiplier =>
        Mathf.Max(fireRateMultiplier, 0f) * Mathf.Max(Time.time < fireBoostUntil ? 1.4f : 1f,
            ComboSystem.Instance != null ? ComboSystem.Instance.TempoMultiplier : 1f);

    public float MoveSpeed =>
        5f * Mathf.Max(moveSpeedMultiplier, 0f) * (Time.time < speedBoostUntil ? 1.25f : 1f);

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
