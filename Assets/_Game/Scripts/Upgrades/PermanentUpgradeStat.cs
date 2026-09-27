/// <summary>
/// Параметры игрока, которые можно постоянно прокачивать за монеты
/// в главном меню (панель «Улучшения»).
///
/// У каждого параметра ровно PermanentUpgrades.MaxLevel уровней,
/// прогресс не сбрасывается между забегами.
/// Порядок элементов = порядок карточек в панели «Улучшения».
/// </summary>
public enum PermanentUpgradeStat
{
    Damage,
    FireRate,
    MoveSpeed,
    MaxHealth,
    HealthRegen,
    CriticalChance,
    CriticalDamage,
    ProjectileSpeed,
    DashCooldown,
    AbilityCooldown
}
