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

/// <summary>
/// Редкая индивидуальная выкрутка внутри уже существующего типа врага.
/// Нового типа моба не создаёт: меняет только поведение и читаемый вид.
/// Заполняется в EnemyData.VariantChance, катится один раз при спавне.
/// </summary>
public enum EnemyVariant
{
    None,

    /// <summary>Fast: рывок с телеграфом, чаще и с более широкого расстояния.</summary>
    Charger,

    /// <summary>Ranged: держит более близкую дистанцию и постоянно переставляется по кругу.</summary>
    Mobile,

    /// <summary>Tank: шире экран для дальника и встаёт насмерть вместо давления.</summary>
    Bulwark,

    /// <summary>Elite: ударная зона ставится под игрока, способность чаще, сам быстрее.</summary>
    Aggressive
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
    [Tooltip("Замах перед рывком (секунды). Время, за которое игрок успевает увидеть подготовку и отойти.")]
    [SerializeField] private float dashWarningTime = 0.4f;
    [Tooltip("Ближняя граница рывка: вплотную быстрый враг не дёргается, а продолжает давить контактным уроном.")]
    [SerializeField] private float dashMinRange = 2.5f;
    [Tooltip("Насколько моб приседает на замахе (0.1 = почти не заметно, 0.5 = отчётливый замах).")]
    [Range(0.1f, 0.5f)]
    [SerializeField] private float dashTelegraphCrouch = 0.3f;
    [Tooltip("Насколько скорость гасится на замахе, чтобы рывок не выглядел внезапным ускорением.")]
    [Range(0f, 1f)]
    [SerializeField] private float dashWindupSpeedScale = 0.25f;

    [Header("Approach Steering")]
    [Tooltip("Насколько подход к игроку идёт вбок от прямой линии (градусы, 90 = чисто вбок). Снимает слипание толпы в одну линию.")]
    [Range(0f, 90f)]
    [SerializeField] private float flankAngle = 0f;
    [Tooltip("Как часто быстрый/обычный моб решает, с какой стороны зайти. Слишком часто — дёргается.")]
    [SerializeField] private float flankRetargetInterval = 1.6f;
    [Tooltip("Перехват: насколько моб целится не в текущую позицию игрока, а в точку впереди по его вектору бега (0 = прямо в игрока, 1 = чистый перехват).")]
    [Range(0f, 1f)]
    [SerializeField] private float interceptLead = 0f;
    [Tooltip("Потолок смещения при перехвате, чтобы быстрый не улетал за угол.")]
    [SerializeField] private float maxInterceptDistance = 2.5f;

    [Header("Ranged Positioning")]
    [Tooltip("Дистанция, которую дальник старается держать. Ближе — подходит, дальше — отходит.")]
    [SerializeField] private float preferredDistance = 6f;
    [Tooltip("Ближе этой дистанции дальник отступает и не лезет вплотную.")]
    [SerializeField] private float retreatDistance = 3.5f;
    [Tooltip("С какой скоростью он отходит от игрока (доля от своей обычной скорости).")]
    [Range(0.3f, 1f)]
    [SerializeField] private float retreatSpeedScale = 0.9f;
    [Tooltip("Как часто дальник меняет позицию, если ему удобно стоять на дистанции.")]
    [SerializeField] private float repositionInterval = 2.2f;
    [Tooltip("Куда именно уходит перестановка (градусы от линии «игрок — моб», 90 = полностью вбок, 180 = за спину к игроку).")]
    [Range(0f, 180f)]
    [SerializeField] private float repositionArcDegrees = 60f;
    [Tooltip("Замах перед выстрелом (секунды). Стрелок стоит и целится, игрок успевает закрыться щитом или уйти с линии.")]
    [SerializeField] private float attackTelegraph = 0.45f;
    [Tooltip("Насколько стрелок приседает на замахе — визуальный читаемый предупреждение выстрела.")]
    [Range(0f, 0.4f)]
    [SerializeField] private float attackTelegraphCrouch = 0.18f;

    [Header("Support (Tank)")]
    [Tooltip("Радиус, в котором танк считает дальника «своим» и встаёт между ним и игроком. 0 = танк не экранирует союзников.")]
    [SerializeField] private float supportRadius = 0f;
    [Tooltip("Насколько далеко перед экранируемым дальником танк встаёт (в метрах). Это экран, а не дистанция боя.")]
    [SerializeField] private float braceRange = 0f;
    [Tooltip("Доля от СОБСТВЕННОЙ дальности контактного урона, на которой танк встаёт и держит позицию. 0 = не встаёт, идёт в лоб. Держим долей, а не метрами: иначе можно выставить остановку дальше собственного урона и танк станет безобидным.")]
    [Range(0f, 1.2f)]
    [SerializeField] private float braceStopScale = 0f;
    [Tooltip("Как часто танк пересматривает, кого именно он сейчас экранирует.")]
    [SerializeField] private float allySearchInterval = 0.5f;
    [Tooltip("Радиус, в котором моб замечает гибель союзника по своей роли (0 = не реагирует).")]
    [SerializeField] private float allyRevengeRadius = 0f;
    [Tooltip("Сколько секунд моб бросает свою позицию и идёт в точку гибели союзника.")]
    [SerializeField] private float allyRevengeTime = 2.5f;
    [Tooltip("Пока рядом стоит танк, дальник стреляет чаще. Ниже 1 = опаснее, 1 = без эффекта.")]
    [Range(0.4f, 1.5f)]
    [SerializeField] private float supportAttackRateMultiplier = 1f;

    [Header("Elite Shockwave Aiming")]
    [Tooltip("Множитель урона в секунду ударной зоны для агрессивного варианта элиты.")]
    [Range(0.5f, 2f)]
    [SerializeField] private float abilityDpsMultiplier = 1f;

    [Header("Variant")]
    [Tooltip("Шанс, что у этого типа врага выпадет редкий вариант. 0 = вариантов у типа нет.")]
    [Range(0f, 1f)]
    [SerializeField] private float variantChance = 0f;
    [Tooltip("С какой волны вариант вообще может выпасть.")]
    [SerializeField] private int variantMinWave = 99;

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

    [Header("Knockback")]
    [Tooltip("Устойчивость к отталкиванию: 0 — отлетает от попадания полностью, 1 — не двигается совсем (босс, элита, танк).")]
    [Range(0f, 1f)]
    [SerializeField] private float knockbackResistance = 0.5f;

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

    public float DashWarningTime =>
        Mathf.Max(dashWarningTime, 0f);

    public float DashMinRange =>
        Mathf.Max(dashMinRange, 0f);

    public float DashTelegraphCrouch =>
        Mathf.Clamp(dashTelegraphCrouch, 0.05f, 0.5f);

    public float DashWindupSpeedScale =>
        Mathf.Clamp01(dashWindupSpeedScale);

    public float FlankAngle =>
        Mathf.Clamp(flankAngle, 0f, 90f);

    public float FlankRetargetInterval =>
        Mathf.Max(flankRetargetInterval, 0.2f);

    public float InterceptLead =>
        Mathf.Clamp01(interceptLead);

    public float MaxInterceptDistance =>
        Mathf.Max(maxInterceptDistance, 0f);

    public float PreferredDistance =>
        Mathf.Max(preferredDistance, 0f);

    public float RetreatDistance =>
        Mathf.Max(retreatDistance, 0f);

    public float RetreatSpeedScale =>
        Mathf.Clamp(retreatSpeedScale, 0.3f, 1f);

    public float RepositionInterval =>
        Mathf.Max(repositionInterval, 0.2f);

    public float RepositionArcDegrees =>
        Mathf.Clamp(repositionArcDegrees, 0f, 180f);

    public float AttackTelegraph =>
        Mathf.Max(attackTelegraph, 0f);

    public float AttackTelegraphCrouch =>
        Mathf.Clamp(attackTelegraphCrouch, 0f, 0.4f);

    public float SupportRadius =>
        Mathf.Max(supportRadius, 0f);

    public float BraceRange =>
        Mathf.Max(braceRange, 0f);

    public float BraceStopScale =>
        Mathf.Max(braceStopScale, 0f);

    public float AllySearchInterval =>
        Mathf.Max(allySearchInterval, 0.05f);

    public float AllyRevengeRadius =>
        Mathf.Max(allyRevengeRadius, 0f);

    public float AllyRevengeTime =>
        Mathf.Max(allyRevengeTime, 0f);

    public float SupportAttackRateMultiplier =>
        Mathf.Clamp(supportAttackRateMultiplier, 0.4f, 1.5f);

    public float AbilityDpsMultiplier =>
        Mathf.Max(abilityDpsMultiplier, 0.1f);

    public float VariantChance =>
        Mathf.Clamp01(variantChance);

    public int VariantMinWave =>
        Mathf.Max(variantMinWave, 1);

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

    public float KnockbackResistance =>
        Mathf.Clamp01(knockbackResistance);

    public float WaveHealthPercent =>
        Mathf.Max(waveHealthPercent, 0f);

    public float WaveDamagePercent =>
        Mathf.Max(waveDamagePercent, 0f);

    public float WaveScorePercent =>
        Mathf.Max(waveScorePercent, 0f);
}