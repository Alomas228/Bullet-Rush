/// <summary>
/// Группа параметра на экране «Улучшения».
///
/// Нужна только для оформления карточки: у каждой группы свой
/// спокойный акцентный цвет и подпись. На сами бонусы группа не
/// влияет — см. PermanentUpgrades.
/// </summary>
public enum PermanentUpgradeCategory
{
    Damage,
    Health,
    Speed,
    Critical
}

public static class PermanentUpgradeCategories
{
    public static PermanentUpgradeCategory Get(PermanentUpgradeStat stat)
    {
        switch (stat)
        {
            case PermanentUpgradeStat.Damage:
            case PermanentUpgradeStat.FireRate:
                return PermanentUpgradeCategory.Damage;

            case PermanentUpgradeStat.MaxHealth:
            case PermanentUpgradeStat.HealthRegen:
                return PermanentUpgradeCategory.Health;

            case PermanentUpgradeStat.MoveSpeed:
            case PermanentUpgradeStat.ProjectileSpeed:
            case PermanentUpgradeStat.DashCooldown:
            case PermanentUpgradeStat.AbilityCooldown:
                return PermanentUpgradeCategory.Speed;

            default:
                return PermanentUpgradeCategory.Critical;
        }
    }

    public static string GetLabel(PermanentUpgradeCategory category)
    {
        switch (category)
        {
            case PermanentUpgradeCategory.Damage:
                return "УРОН";

            case PermanentUpgradeCategory.Health:
                return "ЗДОРОВЬЕ";

            case PermanentUpgradeCategory.Speed:
                return "СКОРОСТЬ";

            default:
                return "КРИТ. ШАНС";
        }
    }
}
