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

    public int ScoreValue => scoreValue;

    public float WaveHealthPercent =>
        Mathf.Max(waveHealthPercent, 0f);

    public float WaveDamagePercent =>
        Mathf.Max(waveDamagePercent, 0f);

    public float WaveScorePercent =>
        Mathf.Max(waveScorePercent, 0f);
}