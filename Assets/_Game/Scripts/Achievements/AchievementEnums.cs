/// <summary>Категории достижений для группировки в UI.</summary>
public enum AchievementCategory
{
    Combat,
    Survival,
    Weapons,
    Bosses,
    Mastery,
    Secrets
}

/// <summary>
/// Тип условия достижения. Определяет и способ накопления, и то,
/// сбрасывается ли прогресс на новом забеге.
/// </summary>
public enum AchievementConditionType
{
    /// <summary>Суммарно убить N врагов за всё время (между забегами).</summary>
    CumulativeKills,

    /// <summary>Убить N врагов подряд, не получив урона, за один забег.</summary>
    KillsWithoutDamageInRun,

    /// <summary>Суммарно убить N врагов конкретным источником урона (например, огнём).</summary>
    KillsByDamageSource,

    /// <summary>Убить N врагов одним событием (взрывом или цепной молнией).</summary>
    KillsInSingleEvent,

    /// <summary>Дойти до волны N, не получив урона за весь забег.</summary>
    ReachWaveWithoutDamage,

    /// <summary>Победить в забеге с N уникальными оружиями (коллекция имён).</summary>
    WinWithUniqueWeapons,

    /// <summary>Победить N уникальных боссов за всё время (коллекция ID).</summary>
    UniqueBosses,

    /// <summary>Получить N Perfect Wave за один забег.</summary>
    PerfectWavesInRun
}

/// <summary>Тип награды за достижение.</summary>
public enum AchievementRewardType
{
    Currency,
    WeaponUnlock,
    ClothingUnlock,
    AbilityUnlock,
    RunModifier
}
