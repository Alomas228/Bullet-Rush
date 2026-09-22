using UnityEngine;

public enum EnemyType
{
    Normal,
    Fast,
    Tank,
    Ranged,
    Elite,
    Boss
}

[CreateAssetMenu(
    fileName = "Enemy_",
    menuName = "Arcade Survivor/Enemy"
)]
public class EnemyData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string enemyName;
    [SerializeField] private EnemyType enemyType;

    [Header("Stats")]
    [SerializeField] private float maxHealth = 40f;
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float contactDamage = 1f;
    [SerializeField] private float damageCooldown = 1f;

    [Header("Ranged Attack")]
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float attackRate = 1.5f;
    [SerializeField] private float projectileDamage = 5f;
    [SerializeField] private float projectileSpeed = 8f;

    [Header("Elite / Boss")]
    [SerializeField] private float damageMultiplier = 1f;
    [SerializeField] private float healthMultiplier = 1f;

    [Header("Boss Abilities")]
    [SerializeField] private float abilityCooldown = 5f;
    [SerializeField] private float summonCooldown = 8f;
    [SerializeField] private int summonCount = 3;

    [Header("Fast Dash")]
    [Tooltip("Дистанция, с которой быстрый враг делает рывок.")]
    [SerializeField] private float dashRange = 6f;
    [Tooltip("Перезарядка рывка.")]
    [SerializeField] private float dashCooldown = 2.8f;
    [Tooltip("Длительность рывка.")]
    [SerializeField] private float dashDuration = 0.25f;
    [Tooltip("Множитель скорости во время рывка.")]
    [SerializeField] private float dashSpeedMultiplier = 4.5f;

    [Header("Ranged Fan")]
    [Tooltip("Сколько снарядов в залпе дальника (1 = одиночный).")]
    [SerializeField] private int fanProjectileCount = 3;
    [Tooltip("Разлёт залпа (градусы).")]
    [SerializeField] private float fanSpreadDegrees = 24f;

    [Header("Elite Shockwave")]
    [Tooltip("Радиус зоны шока вокруг элиты.")]
    [SerializeField] private float abilityRadius = 4f;
    [Tooltip("Время предупреждения зоны шока.")]
    [SerializeField] private float abilityWarning = 1.1f;
    [Tooltip("Длительность зоны шока.")]
    [SerializeField] private float abilityDuration = 3f;
    [Tooltip("Урон в секунду зоны шока.")]
    [SerializeField] private float abilityDps = 3f;

    [Header("Death Explosion")]
    [Tooltip("Сколько снарядов разлетается при смерти (0 = без взрыва).")]
    [SerializeField] private int deathExplosionProjectileCount = 8;
    [Tooltip("Скорость снарядов взрыва при смерти.")]
    [SerializeField] private float deathExplosionProjectileSpeed = 7f;
    [Tooltip("Урон каждого снаряда взрыва при смерти.")]
    [SerializeField] private float deathExplosionDamage = 6f;

    [Header("Wave Scaling")]
    [Tooltip("Percent of max health added per wave. Wave 1 = no bonus.")]
    [SerializeField] private float waveHealthPercent = 0.15f;
    [Tooltip("Percent of contact/projectile damage added per wave.")]
    [SerializeField] private float waveDamagePercent = 0.05f;
    [Tooltip("Percent of score value added per wave.")]
    [SerializeField] private float waveScorePercent = 0.10f;

    [Header("Score")]
    [SerializeField] private int scoreValue = 10;

    public string EnemyName => enemyName;

    public EnemyType EnemyType => enemyType;

    public float MaxHealth =>
        maxHealth * Mathf.Max(healthMultiplier, 0f);

    public float MoveSpeed => moveSpeed;

    public float ContactDamage =>
        contactDamage * Mathf.Max(damageMultiplier, 0f);

    public float DamageCooldown => damageCooldown;

    public float AttackRange => attackRange;

    public float AttackRate => attackRate;

    public float ProjectileDamage =>
        projectileDamage * Mathf.Max(damageMultiplier, 0f);

    public float ProjectileSpeed => projectileSpeed;

    public float DamageMultiplier =>
        Mathf.Max(damageMultiplier, 0f);

    public float HealthMultiplier =>
        Mathf.Max(healthMultiplier, 0f);

    public float AbilityCooldown =>
        Mathf.Max(abilityCooldown, 0.1f);

    public float SummonCooldown =>
        Mathf.Max(summonCooldown, 0.1f);

    public int SummonCount =>
        Mathf.Max(summonCount, 1);

    public float DashRange =>
        Mathf.Max(dashRange, 0f);

    public float DashCooldown =>
        Mathf.Max(dashCooldown, 0.1f);

    public float DashDuration =>
        Mathf.Max(dashDuration, 0.05f);

    public float DashSpeedMultiplier =>
        Mathf.Max(dashSpeedMultiplier, 1f);

    public int FanProjectileCount =>
        Mathf.Max(fanProjectileCount, 1);

    public float FanSpreadDegrees =>
        Mathf.Max(fanSpreadDegrees, 0f);

    public float AbilityRadius =>
        Mathf.Max(abilityRadius, 0.5f);

    public float AbilityWarning =>
        Mathf.Max(abilityWarning, 0.1f);

    public float AbilityDuration =>
        Mathf.Max(abilityDuration, 0.1f);

    public float AbilityDps =>
        Mathf.Max(abilityDps, 0f);

    public int DeathExplosionProjectileCount =>
        Mathf.Max(deathExplosionProjectileCount, 0);

    public float DeathExplosionProjectileSpeed =>
        Mathf.Max(deathExplosionProjectileSpeed, 1f);

    public float DeathExplosionDamage =>
        Mathf.Max(deathExplosionDamage, 0f);

    public int ScoreValue => scoreValue;

    public float WaveHealthPercent =>
        Mathf.Max(waveHealthPercent, 0f);

    public float WaveDamagePercent =>
        Mathf.Max(waveDamagePercent, 0f);

    public float WaveScorePercent =>
        Mathf.Max(waveScorePercent, 0f);
}