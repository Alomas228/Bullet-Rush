using UnityEngine;

/// <summary>
/// Текст ежедневной миссии: заголовок, описание и подпись награды.
/// Собирается из локализации по типу цели — так переводы живут в
/// Tables.cs, а не в сохранении, и миссия остаётся маленькой записью.
/// </summary>
public static class DailyMissionText
{
    public static string GetTitle(DailyMissionSaveData.Entry entry)
    {
        if (entry == null)
            return string.Empty;

        return Lang.GetOr(TitleKey(entry.Objective), FallbackTitle(entry.Objective));
    }

    public static string GetDescription(DailyMissionSaveData.Entry entry)
    {
        return GetDescription(entry, MissionTrack.Daily);
    }

    public static string GetDescription(
        DailyMissionSaveData.Entry entry,
        MissionTrack track)
    {
        if (entry == null)
            return string.Empty;

        int target = Mathf.RoundToInt(entry.target);

        if (entry.Objective == DailyMissionObjective.KillEnemies &&
            track == MissionTrack.Weekly)
        {
            string weeklyKey = "mission.weekly.kill_enemies.desc";
            return Lang.GetOr(weeklyKey, "Убей {0} врагов.")
                .Replace("{0}", target.ToString());
        }

        if (entry.Objective == DailyMissionObjective.KillByDamageSource)
        {
            string source = SourceName(entry.Source);
            string key = "mission.kill_by_source.desc";

            return Lang.Has(key)
                ? Lang.Get(key, target, source)
                : FallbackDescription(entry.Objective, target, source);
        }

        string descKey = DescKey(entry.Objective);

        return Lang.Has(descKey)
            ? Lang.Get(descKey, target)
            : FallbackDescription(entry.Objective, target, string.Empty);
    }

    /// <summary>Подпись награды: «Монеты xN · XP xM».</summary>
    public static string GetRewardLabel(DailyMissionSaveData.Entry entry)
    {
        if (entry == null)
            return string.Empty;

        string coins = Lang.GetOr("mission.reward.coins", "Монеты");
        string xp = Lang.GetOr("mission.reward.xp", "XP");

        if (entry.coinReward > 0 && entry.xpReward > 0)
            return $"{coins} x{entry.coinReward}   {xp} x{entry.xpReward}";

        if (entry.coinReward > 0)
            return $"{coins} x{entry.coinReward}";

        return $"{xp} x{entry.xpReward}";
    }

    public static string SourceName(DamageSource source)
    {
        switch (source)
        {
            case DamageSource.Burn:
                return Lang.GetOr("mission.source.burn", "огнём");

            case DamageSource.Explosion:
                return Lang.GetOr("mission.source.explosion", "взрывом");

            case DamageSource.ChainLightning:
                return Lang.GetOr("mission.source.chain_lightning", "цепной молнией");

            case DamageSource.Bleed:
                return Lang.GetOr("mission.source.bleed", "кровотечением");

            case DamageSource.Ability:
                return Lang.GetOr("mission.source.ability", "способностью");

            case DamageSource.Weapon:
                return Lang.GetOr("mission.source.weapon", "оружием");

            default:
                return Lang.GetOr("mission.source.unknown", "уроном");
        }
    }

    private static string TitleKey(DailyMissionObjective objective)
    {
        switch (objective)
        {
            case DailyMissionObjective.KillEnemies:
                return "mission.kill_enemies.title";

            case DailyMissionObjective.KillByDamageSource:
                return "mission.kill_by_source.title";

            case DailyMissionObjective.KillsInSingleEvent:
                return "mission.single_event.title";

            case DailyMissionObjective.KillsWithoutDamage:
                return "mission.kills_without_damage.title";

            case DailyMissionObjective.PerfectWaves:
                return "mission.perfect_waves.title";

            case DailyMissionObjective.ReachWave:
                return "mission.reach_wave.title";

            case DailyMissionObjective.KillBosses:
                return "mission.kill_bosses.title";

            case DailyMissionObjective.CompleteRuns:
                return "mission.complete_runs.title";
        }

        return "mission.title";
    }

    private static string DescKey(DailyMissionObjective objective)
    {
        switch (objective)
        {
            case DailyMissionObjective.KillEnemies:
                return "mission.kill_enemies.desc";

            case DailyMissionObjective.KillsInSingleEvent:
                return "mission.single_event.desc";

            case DailyMissionObjective.KillsWithoutDamage:
                return "mission.kills_without_damage.desc";

            case DailyMissionObjective.PerfectWaves:
                return "mission.perfect_waves.desc";

            case DailyMissionObjective.ReachWave:
                return "mission.reach_wave.desc";

            case DailyMissionObjective.KillBosses:
                return "mission.kill_bosses.desc";

            case DailyMissionObjective.CompleteRuns:
                return "mission.complete_runs.desc";
        }

        return "mission.kill_enemies.desc";
    }

    private static string FallbackTitle(DailyMissionObjective objective)
    {
        switch (objective)
        {
            case DailyMissionObjective.KillEnemies:
                return "Охотник";

            case DailyMissionObjective.KillByDamageSource:
                return "Стихийный урон";

            case DailyMissionObjective.KillsInSingleEvent:
                return "Цепная реакция";

            case DailyMissionObjective.KillsWithoutDamage:
                return "Неуязвимый";

            case DailyMissionObjective.PerfectWaves:
                return "Безупречно";

            case DailyMissionObjective.ReachWave:
                return "Прорыв";

            case DailyMissionObjective.KillBosses:
                return "Убийца боссов";

            case DailyMissionObjective.CompleteRuns:
                return "Победитель";
        }

        return "Миссия";
    }

    private static string FallbackDescription(
        DailyMissionObjective objective,
        int target,
        string source)
    {
        switch (objective)
        {
            case DailyMissionObjective.KillEnemies:
                return $"Убей {target} врагов за сегодня.";

            case DailyMissionObjective.KillByDamageSource:
                return $"Убей {target} врагов ({source}).";

            case DailyMissionObjective.KillsInSingleEvent:
                return $"Убей {target} врагов одним взрывом или цепной молнией.";

            case DailyMissionObjective.KillsWithoutDamage:
                return $"Убей {target} врагов подряд, не получив урона.";

            case DailyMissionObjective.PerfectWaves:
                return $"Заверши {target} волн без урона.";

            case DailyMissionObjective.ReachWave:
                return $"Дойди до {target}-й волны.";

            case DailyMissionObjective.KillBosses:
                return $"Убей {target} босса.";

            case DailyMissionObjective.CompleteRuns:
                return "Заверши забег победой.";
        }

        return string.Empty;
    }
}
