/// <summary>
/// Тип цели ежедневной миссии. Определяет, как начисляется прогресс:
/// накопительно за день или как «лучший результат за день».
///
/// Миссии сбрасываются раз в сутки, поэтому весь прогресс живёт в
/// пределах календарного дня (по локальной дате устройства).
/// </summary>
public enum DailyMissionObjective
{
    /// <summary>Суммарно убить N врагов за день.</summary>
    KillEnemies = 0,

    /// <summary>Убить N врагов конкретным источником урона (огонь, взрыв, молния).</summary>
    KillByDamageSource = 1,

    /// <summary>Убить N врагов одним событием (взрыв/цепная молния) — лучший результат дня.</summary>
    KillsInSingleEvent = 2,

    /// <summary>Убить N врагов подряд без урона за один забег — лучший результат дня.</summary>
    KillsWithoutDamage = 3,

    /// <summary>Собрать N Perfect Wave за день.</summary>
    PerfectWaves = 4,

    /// <summary>Дойти до волны N — лучший результат дня.</summary>
    ReachWave = 5,

    /// <summary>Убить N боссов за день.</summary>
    KillBosses = 6,

    /// <summary>Завершить N забегов (дойти до победной волны) за день.</summary>
    CompleteRuns = 7
}
