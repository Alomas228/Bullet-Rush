using System.Collections.Generic;

/// <summary>
/// Long-horizon weekly goals. These are intentionally different from the
/// small daily tasks and are designed to span multiple runs.
/// </summary>
public static class WeeklyMissionPool
{
    private static List<DailyMissionTemplate> templates;

    public static IReadOnlyList<DailyMissionTemplate> Templates
    {
        get
        {
            if (templates == null)
                templates = Build();
            return templates;
        }
    }

    public static bool ContainsId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        for (int i = 0; i < Templates.Count; i++)
        {
            if (Templates[i] != null && Templates[i].id == id)
                return true;
        }

        return false;
    }

    private static List<DailyMissionTemplate> Build()
    {
        return new List<DailyMissionTemplate>
        {
            new DailyMissionTemplate("weekly_kill_1000", DailyMissionObjective.KillEnemies, 1000f, 550, 375),
            new DailyMissionTemplate("weekly_kill_3000", DailyMissionObjective.KillEnemies, 3000f, 1000, 725),
            new DailyMissionTemplate("weekly_bosses_8", DailyMissionObjective.KillBosses, 8f, 850, 600),
            new DailyMissionTemplate("weekly_perfect_20", DailyMissionObjective.PerfectWaves, 20f, 900, 650),
            new DailyMissionTemplate("weekly_reach_wave_30", DailyMissionObjective.ReachWave, 30f, 750, 550),
            new DailyMissionTemplate("weekly_reach_wave_50", DailyMissionObjective.ReachWave, 50f, 1350, 975),
            new DailyMissionTemplate("weekly_complete_runs_5", DailyMissionObjective.CompleteRuns, 5f, 1150, 825),
            new DailyMissionTemplate("weekly_chain_kills_150", DailyMissionObjective.KillByDamageSource, 150f, 900, 650, DamageSource.ChainLightning),
            new DailyMissionTemplate("weekly_explosion_kills_200", DailyMissionObjective.KillByDamageSource, 200f, 900, 650, DamageSource.Explosion),
            new DailyMissionTemplate("weekly_event_kills_15", DailyMissionObjective.KillsInSingleEvent, 15f, 700, 500),
            new DailyMissionTemplate("weekly_no_damage_streak_75", DailyMissionObjective.KillsWithoutDamage, 75f, 1050, 775),
            new DailyMissionTemplate("weekly_kill_1500", DailyMissionObjective.KillEnemies, 1500f, 700, 525),
            new DailyMissionTemplate("weekly_kill_5000", DailyMissionObjective.KillEnemies, 5000f, 1600, 1150),
            new DailyMissionTemplate("weekly_bosses_12", DailyMissionObjective.KillBosses, 12f, 1200, 875),
            new DailyMissionTemplate("weekly_bosses_20", DailyMissionObjective.KillBosses, 20f, 1800, 1325),
            new DailyMissionTemplate("weekly_perfect_35", DailyMissionObjective.PerfectWaves, 35f, 1300, 925),
            new DailyMissionTemplate("weekly_perfect_50", DailyMissionObjective.PerfectWaves, 50f, 1750, 1275),
            new DailyMissionTemplate("weekly_reach_wave_40", DailyMissionObjective.ReachWave, 40f, 1000, 725),
            new DailyMissionTemplate("weekly_reach_wave_60", DailyMissionObjective.ReachWave, 60f, 1650, 1200),
            new DailyMissionTemplate("weekly_complete_runs_8", DailyMissionObjective.CompleteRuns, 8f, 1500, 1100),
            new DailyMissionTemplate("weekly_complete_runs_12", DailyMissionObjective.CompleteRuns, 12f, 2000, 1425),
            new DailyMissionTemplate("weekly_chain_kills_300", DailyMissionObjective.KillByDamageSource, 300f, 1350, 1000, DamageSource.ChainLightning),
            new DailyMissionTemplate("weekly_explosion_kills_350", DailyMissionObjective.KillByDamageSource, 350f, 1450, 1050, DamageSource.Explosion),
            new DailyMissionTemplate("weekly_burn_kills_300", DailyMissionObjective.KillByDamageSource, 300f, 1350, 1000, DamageSource.Burn),
            new DailyMissionTemplate("weekly_event_kills_20", DailyMissionObjective.KillsInSingleEvent, 20f, 1000, 725),
            new DailyMissionTemplate("weekly_event_kills_25", DailyMissionObjective.KillsInSingleEvent, 25f, 1300, 925),
            new DailyMissionTemplate("weekly_no_damage_streak_100", DailyMissionObjective.KillsWithoutDamage, 100f, 1450, 1050),
            new DailyMissionTemplate("weekly_no_damage_streak_150", DailyMissionObjective.KillsWithoutDamage, 150f, 1900, 1375)
        };
    }
}
