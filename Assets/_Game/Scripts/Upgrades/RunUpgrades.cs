using UnityEngine;

/// <summary>
/// Накопленные временные улучшения текущего забега.
/// Живёт на игроке и сбрасывается вместе с ним при перезапуске/выходе.
/// </summary>
public class RunUpgrades : MonoBehaviour
{
    // =========================================================
    // BURNING ROUNDS
    // =========================================================

    [Header("Burning Rounds")]
    [Range(0f, 1f)]
    [SerializeField] private float runBurnChance;
    [SerializeField] private float runBurnDamage;
    [SerializeField] private float runBurnDuration;
    [SerializeField] private float runBurnTickInterval = 0.5f;

    // =========================================================
    // EXPLOSIVE ROUNDS
    // =========================================================

    [Header("Explosive Rounds")]
    [SerializeField] private float explosionRadius;
    [SerializeField] private float explosionDamage;

    // =========================================================
    // BLEEDING
    // =========================================================

    [Header("Bleeding")]
    [Range(0f, 1f)]
    [SerializeField] private float bleedChance;
    [SerializeField] private float bleedDamage;
    [SerializeField] private float bleedDuration;
    [SerializeField] private float bleedTickInterval = 0.5f;

    // =========================================================
    // LIFESTEAL
    // =========================================================

    [Header("Lifesteal")]
    [Range(0f, 1f)]
    [SerializeField] private float lifestealPercent;

    // =========================================================
    // CHAIN LIGHTNING
    // =========================================================

    [Header("Chain Lightning")]
    [Range(0f, 1f)]
    [SerializeField] private float lightningChance;
    [SerializeField] private float lightningDamage;
    [SerializeField] private float lightningRadius;
    [SerializeField] private int lightningMaxTargets;

    // =========================================================
    // RICOCHET
    // =========================================================

    [Header("Ricochet")]
    [Range(0f, 1f)]
    [SerializeField] private float ricochetChance;
    [SerializeField] private int ricochetMaxBounces;
    [SerializeField] private float ricochetSearchRadius;
    [SerializeField] private float ricochetDamageMultiplier = 1f;

    // =========================================================
    // PROPERTIES
    // =========================================================

    public float RunBurnChance => Mathf.Clamp01(runBurnChance);
    public float RunBurnDamage => runBurnDamage;
    public float RunBurnDuration => runBurnDuration;
    public float RunBurnTickInterval => Mathf.Max(runBurnTickInterval, 0.05f);

    public float ExplosionRadius => explosionRadius;
    public float ExplosionDamage => explosionDamage;

    public float BleedChance => Mathf.Clamp01(bleedChance);
    public float BleedDamage => bleedDamage;
    public float BleedDuration => bleedDuration;
    public float BleedTickInterval => Mathf.Max(bleedTickInterval, 0.05f);

    public float LifestealPercent => Mathf.Clamp01(lifestealPercent);

    public float LightningChance => Mathf.Clamp01(lightningChance);
    public float LightningDamage => lightningDamage;
    public float LightningRadius => lightningRadius;
    public int LightningMaxTargets => Mathf.Max(lightningMaxTargets, 0);

    public float RicochetChance => Mathf.Clamp01(ricochetChance);
    public int RicochetMaxBounces => ricochetMaxBounces;
    public float RicochetSearchRadius => ricochetSearchRadius;
    public float RicochetDamageMultiplier => ricochetDamageMultiplier;

    public bool HasBurn => runBurnChance > 0f;
    public bool HasExplosion => explosionRadius > 0f && explosionDamage > 0f;
    public bool HasBleed => bleedChance > 0f;
    public bool HasLifesteal => lifestealPercent > 0f;
    public bool HasLightning => lightningChance > 0f;
    public bool HasRicochet => ricochetChance > 0f;

    // =========================================================
    // ADD METHODS
    // =========================================================

    public void AddBurn(BurnEffectData data)
    {
        if (data == null)
            return;

        runBurnChance = Mathf.Clamp01(runBurnChance + data.Chance);
        runBurnDamage += data.Damage;
        runBurnDuration += data.Duration;
        runBurnTickInterval = Mathf.Max(runBurnTickInterval, data.TickInterval);
    }

    public void AddExplosion(ExplosionEffectData data)
    {
        if (data == null)
            return;

        explosionRadius += data.Radius;
        explosionDamage += data.Damage;
    }

    public void AddBleed(BleedingEffectData data)
    {
        if (data == null)
            return;

        bleedChance = Mathf.Clamp01(bleedChance + data.Chance);
        bleedDamage += data.Damage;
        bleedDuration += data.Duration;
        bleedTickInterval = Mathf.Max(bleedTickInterval, data.TickInterval);
    }

    public void AddLifesteal(LifestealEffectData data)
    {
        if (data == null)
            return;

        lifestealPercent = Mathf.Min(lifestealPercent + data.Percentage, 1f);
    }

    public void AddChainLightning(ChainLightningEffectData data)
    {
        if (data == null)
            return;

        lightningChance = Mathf.Clamp01(lightningChance + data.Chance);
        lightningDamage += data.Damage;
        lightningRadius += data.Radius;
        lightningMaxTargets += data.MaxTargets;
    }

    public void AddRicochet(RicochetEffectData data)
    {
        if (data == null)
            return;

        ricochetChance = Mathf.Clamp01(ricochetChance + data.Chance);
        ricochetMaxBounces += data.MaxBounces;
        ricochetSearchRadius += data.SearchRadius;
        ricochetDamageMultiplier = Mathf.Min(ricochetDamageMultiplier, data.DamageMultiplier);
    }

    // =========================================================
    // RESET
    // =========================================================

    /// <summary>
    /// Гарантирует чистый старт забега. Вызывается при создании
    /// игрока, поэтому пережить прошлый забег эффекты не могут.
    /// </summary>
    private void Start()
    {
        ResetAll();
    }

    public void ResetAll()
    {
        runBurnChance = 0f;
        runBurnDamage = 0f;
        runBurnDuration = 0f;
        runBurnTickInterval = 0.5f;

        explosionRadius = 0f;
        explosionDamage = 0f;

        bleedChance = 0f;
        bleedDamage = 0f;
        bleedDuration = 0f;
        bleedTickInterval = 0.5f;

        lifestealPercent = 0f;

        lightningChance = 0f;
        lightningDamage = 0f;
        lightningRadius = 0f;
        lightningMaxTargets = 0;

        ricochetChance = 0f;
        ricochetMaxBounces = 0;
        ricochetSearchRadius = 0f;
        ricochetDamageMultiplier = 1f;
    }
}