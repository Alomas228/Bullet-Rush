using UnityEngine;

[CreateAssetMenu(
    fileName = "Boss_",
    menuName = "Arcade Survivor/Boss"
)]
public class BossData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string bossName = "Boss";

    [Header("Base Stats")]
    [SerializeField] private float maxHealth = 1000f;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float contactDamage = 10f;

    [Header("Ranged Attack")]
    [SerializeField] private float attackRange = 8f;
    [SerializeField] private float attackInterval = 1.5f;
    [SerializeField] private float projectileDamage = 15f;
    [SerializeField] private float projectileSpeed = 8f;


    // =========================================================
    // PHASE 1
    // =========================================================

    [Header("Phase 1")]
    [Range(0f, 1f)]
    [SerializeField] private float phase1HealthPercent = 1f;

    [Header("Phase 1 Ability")]
    [SerializeField] private float phase1AbilityCooldown = 8f;
    [SerializeField] private float phase1AbilityDamage = 25f;
    [SerializeField] private float phase1AbilityRadius = 5f;
    [SerializeField] private float phase1AbilityWarningDuration = 2f;

    [Header("Phase 1 Summon")]
    [SerializeField] private float phase1SummonCooldown = 10f;
    [SerializeField] private int phase1SummonCount = 3;


    // =========================================================
    // PHASE 2
    // =========================================================

    [Header("Phase 2")]
    [Range(0f, 1f)]
    [SerializeField] private float phase2HealthPercent = 0.6f;

    [SerializeField] private float phase2MoveSpeedMultiplier = 1.2f;
    [SerializeField] private float phase2AttackIntervalMultiplier = 0.8f;

    [Header("Phase 2 Ability")]
    [SerializeField] private float phase2AbilityCooldown = 6f;
    [SerializeField] private float phase2AbilityDamage = 35f;
    [SerializeField] private float phase2AbilityRadius = 5f;
    [SerializeField] private float phase2AbilityWarningDuration = 2f;

    [Header("Phase 2 Summon")]
    [SerializeField] private float phase2SummonCooldown = 8f;
    [SerializeField] private int phase2SummonCount = 4;


    // =========================================================
    // PHASE 3
    // =========================================================

    [Header("Phase 3")]
    [Range(0f, 1f)]
    [SerializeField] private float phase3HealthPercent = 0.3f;

    [SerializeField] private float phase3MoveSpeedMultiplier = 1.5f;
    [SerializeField] private float phase3AttackIntervalMultiplier = 0.65f;
    [SerializeField] private float phase3DamageMultiplier = 1.5f;

    [Header("Phase 3 Ability")]
    [SerializeField] private float phase3AbilityCooldown = 4f;
    [SerializeField] private float phase3AbilityDamage = 50f;
    [SerializeField] private float phase3AbilityRadius = 6f;
    [SerializeField] private float phase3AbilityWarningDuration = 1.5f;

    [Header("Phase 3 Summon")]
    [SerializeField] private float phase3SummonCooldown = 6f;
    [SerializeField] private int phase3SummonCount = 6;


    // =========================================================
    // WAVE SCALING
    // =========================================================

    [Header("Wave Scaling")]
    [Tooltip("Percent of max health added per wave. Wave 1 = no bonus.")]
    [SerializeField] private float waveHealthPercent = 0.20f;
    [Tooltip("Percent of contact/projectile/ability damage added per wave.")]
    [SerializeField] private float waveDamagePercent = 0.10f;
    [Tooltip("Percent of score value added per wave.")]
    [SerializeField] private float waveScorePercent = 0.10f;


    // =========================================================
    // SCORE
    // =========================================================

    [Header("Score")]
    [SerializeField] private int scoreValue = 500;


    // =========================================================
    // PROPERTIES
    // =========================================================

    public string BossName =>
        bossName;

    public float MaxHealth =>
        Mathf.Max(maxHealth, 1f);

    public float MoveSpeed =>
        Mathf.Max(moveSpeed, 0f);

    public float ContactDamage =>
        Mathf.Max(contactDamage, 0f);

    public float AttackRange =>
        Mathf.Max(attackRange, 0f);

    public float AttackInterval =>
        Mathf.Max(attackInterval, 0.1f);

    public float ProjectileDamage =>
        Mathf.Max(projectileDamage, 0f);

    public float ProjectileSpeed =>
        Mathf.Max(projectileSpeed, 0f);


    // =========================================================
    // WAVE SCALING PROPERTIES
    // =========================================================

    public float WaveHealthPercent =>
        Mathf.Max(waveHealthPercent, 0f);

    public float WaveDamagePercent =>
        Mathf.Max(waveDamagePercent, 0f);

    public float WaveScorePercent =>
        Mathf.Max(waveScorePercent, 0f);


    // =========================================================
    // PHASE 1 PROPERTIES
    // =========================================================

    public float Phase1HealthPercent =>
        Mathf.Clamp01(phase1HealthPercent);

    public float Phase1AbilityCooldown =>
        Mathf.Max(phase1AbilityCooldown, 0.1f);

    public float Phase1AbilityDamage =>
        Mathf.Max(phase1AbilityDamage, 0f);

    public float Phase1AbilityRadius =>
        Mathf.Max(phase1AbilityRadius, 0f);

    public float Phase1AbilityWarningDuration =>
        Mathf.Max(phase1AbilityWarningDuration, 0f);

    public float Phase1SummonCooldown =>
        Mathf.Max(phase1SummonCooldown, 0.1f);

    public int Phase1SummonCount =>
        Mathf.Max(phase1SummonCount, 1);


    // =========================================================
    // PHASE 2 PROPERTIES
    // =========================================================

    public float Phase2HealthPercent =>
        Mathf.Clamp01(phase2HealthPercent);

    public float Phase2MoveSpeedMultiplier =>
        Mathf.Max(phase2MoveSpeedMultiplier, 0f);

    public float Phase2AttackIntervalMultiplier =>
        Mathf.Max(phase2AttackIntervalMultiplier, 0.1f);

    public float Phase2AbilityCooldown =>
        Mathf.Max(phase2AbilityCooldown, 0.1f);

    public float Phase2AbilityDamage =>
        Mathf.Max(phase2AbilityDamage, 0f);

    public float Phase2AbilityRadius =>
        Mathf.Max(phase2AbilityRadius, 0f);

    public float Phase2AbilityWarningDuration =>
        Mathf.Max(phase2AbilityWarningDuration, 0f);

    public float Phase2SummonCooldown =>
        Mathf.Max(phase2SummonCooldown, 0.1f);

    public int Phase2SummonCount =>
        Mathf.Max(phase2SummonCount, 1);


    // =========================================================
    // PHASE 3 PROPERTIES
    // =========================================================

    public float Phase3HealthPercent =>
        Mathf.Clamp01(phase3HealthPercent);

    public float Phase3MoveSpeedMultiplier =>
        Mathf.Max(phase3MoveSpeedMultiplier, 0f);

    public float Phase3AttackIntervalMultiplier =>
        Mathf.Max(phase3AttackIntervalMultiplier, 0.1f);

    public float Phase3DamageMultiplier =>
        Mathf.Max(phase3DamageMultiplier, 0f);

    public float Phase3AbilityCooldown =>
        Mathf.Max(phase3AbilityCooldown, 0.1f);

    public float Phase3AbilityDamage =>
        Mathf.Max(phase3AbilityDamage, 0f);

    public float Phase3AbilityRadius =>
        Mathf.Max(phase3AbilityRadius, 0f);

    public float Phase3AbilityWarningDuration =>
        Mathf.Max(phase3AbilityWarningDuration, 0f);

    public float Phase3SummonCooldown =>
        Mathf.Max(phase3SummonCooldown, 0.1f);

    public int Phase3SummonCount =>
        Mathf.Max(phase3SummonCount, 1);


    // =========================================================
    // SCORE
    // =========================================================

    public int ScoreValue =>
        Mathf.Max(scoreValue, 0);
}