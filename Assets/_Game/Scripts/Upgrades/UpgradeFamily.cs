using System;

[Flags]
public enum UpgradeFamily
{
    None = 0,
    Core = 1 << 0,
    Fire = 1 << 1,
    Lightning = 1 << 2,
    Bleed = 1 << 3,
    Explosion = 1 << 4,
    Lifesteal = 1 << 5,
    Ricochet = 1 << 6,
    Defensive = 1 << 7,
    Ability = 1 << 8,
    Weapon = 1 << 9,
    Utility = 1 << 10
}

public static class UpgradeFamilyUtility
{
    public static readonly UpgradeFamily[] All =
    {
        UpgradeFamily.Core,
        UpgradeFamily.Fire,
        UpgradeFamily.Lightning,
        UpgradeFamily.Bleed,
        UpgradeFamily.Explosion,
        UpgradeFamily.Lifesteal,
        UpgradeFamily.Ricochet,
        UpgradeFamily.Defensive,
        UpgradeFamily.Ability,
        UpgradeFamily.Weapon,
        UpgradeFamily.Utility
    };

    public static UpgradeFamily Resolve(
        UpgradeFamily families,
        UpgradeType type)
    {
        return families != UpgradeFamily.None
            ? families
            : Infer(type);
    }

    public static UpgradeFamily Infer(UpgradeType type)
    {
        switch (type)
        {
            case UpgradeType.Damage:
            case UpgradeType.FireRate:
            case UpgradeType.ProjectileCount:
            case UpgradeType.ProjectileSpeed:
            case UpgradeType.CriticalChance:
            case UpgradeType.CriticalDamage:
            case UpgradeType.Pierce:
                return UpgradeFamily.Core;

            case UpgradeType.MoveSpeed:
            case UpgradeType.Special:
                return UpgradeFamily.Utility;

            case UpgradeType.MaxHealth:
            case UpgradeType.HealthRegen:
                return UpgradeFamily.Defensive;

            case UpgradeType.Bomb:
            case UpgradeType.Shield:
                return UpgradeFamily.Ability;

            case UpgradeType.Weapon:
                return UpgradeFamily.Weapon;

            case UpgradeType.BurningRounds:
                return UpgradeFamily.Fire;

            case UpgradeType.ExplosiveRounds:
                return UpgradeFamily.Explosion;

            case UpgradeType.Bleeding:
                return UpgradeFamily.Bleed;

            case UpgradeType.Lifesteal:
                return UpgradeFamily.Lifesteal;

            case UpgradeType.ChainLightning:
                return UpgradeFamily.Lightning;

            case UpgradeType.Ricochet:
                return UpgradeFamily.Ricochet;

            default:
                return UpgradeFamily.Core;
        }
    }

    public static bool Intersects(
        UpgradeFamily a,
        UpgradeFamily b)
    {
        return a != UpgradeFamily.None &&
               b != UpgradeFamily.None &&
               (a & b) != 0;
    }
}
