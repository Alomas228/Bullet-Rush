using UnityEngine;

public static class MapCombatRules
{
    public const float WetDuration = 8f;
    public const float WetRangeBonus = 1.5f;
    public const float WetLightningMultiplier = 1.5f;
    public const float IceChance = 0.45f;
    public const float IceHealthFraction = 0.8f;

    public static bool ShouldHaveIce(MapMechanic mechanic, bool boss, float roll) =>
        mechanic == MapMechanic.Highland && !boss && roll < IceChance;

    public static float LightningRange(float baseRange, bool wet)
    {
        if (baseRange <= 0f) return 0f;
        return Mathf.Min(baseRange + (wet ? WetRangeBonus : 0f), 6f);
    }

    // Ice is a separate shield. Only the unused, unmultiplied part of the
    // hit reaches enemy health after the shield breaks.
    public static float ResolveIceDamage(float damage, bool burning, ref float ice)
    {
        if (damage <= 0f) return 0f;
        if (ice <= 0f) return damage;
        float multiplier = burning ? 3f : 0.4f;
        float removed = Mathf.Min(ice, damage * multiplier);
        ice = Mathf.Max(0f, ice - removed);
        return Mathf.Max(0f, damage - removed / multiplier);
    }
}
