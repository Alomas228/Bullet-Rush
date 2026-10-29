using UnityEngine;

public static class MapCombatRules
{
    public const float WetDuration = 4f;
    public const float WetRangeBonus = 1.5f;
    public const float IceChance = 1f / 3f;
    public const float IceHealthFraction = 0.3f;

    public static bool ShouldHaveIce(MapMechanic mechanic, bool boss, float roll) =>
        mechanic == MapMechanic.Highland && !boss && roll < IceChance;

    public static float LightningRange(float baseRange, bool wet)
    {
        if (baseRange <= 0f) return 0f;
        return Mathf.Min(baseRange + (wet ? WetRangeBonus : 0f), 6f);
    }

    // Health already includes the coating. Spend part of this hit on ice,
    // then carry the unused (unmultiplied) part into flesh. Never double the
    // entire hit when a burn tick breaks a nearly depleted coating.
    public static float ResolveIceDamage(float damage, bool burning, ref float ice)
    {
        if (damage <= 0f) return 0f;
        if (ice <= 0f) return damage;
        float multiplier = burning ? 2f : 1f;
        float removed = Mathf.Min(ice, damage * multiplier);
        ice = Mathf.Max(0f, ice - removed);
        return removed + Mathf.Max(0f, damage - removed / multiplier);
    }
}
