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
            new DailyMissionTemplate("weekly_kill_1000", DailyMissionObjective.KillEnemies, 1000f, 1400, 700),
            new DailyMissionTemplate("weekly_kill_3000", DailyMissionObjective.KillEnemies, 3000f, 2600, 1300),
            new DailyMissionTemplate("weekly_bosses_8", DailyMissionObjective.KillBosses, 8f, 2200, 1100),
            new DailyMissionTemplate("weekly_perfect_20", DailyMissionObjective.PerfectWaves, 20f, 2400, 1200),
            new DailyMissionTemplate("weekly_reach_wave_30", DailyMissionObjective.ReachWave, 30f, 2000, 1000),
            new DailyMissionTemplate("weekly_reach_wave_50", DailyMissionObjective.ReachWave, 50f, 3500, 1750),
            new DailyMissionTemplate("weekly_complete_runs_5", DailyMissionObjective.CompleteRuns, 5f, 3000, 1500),
            new DailyMissionTemplate("weekly_chain_kills_150", DailyMissionObjective.KillByDamageSource, 150f, 2400, 1200, DamageSource.ChainLightning),
            new DailyMissionTemplate("weekly_explosion_kills_200", DailyMissionObjective.KillByDamageSource, 200f, 2400, 1200, DamageSource.Explosion),
            new DailyMissionTemplate("weekly_event_kills_15", DailyMissionObjective.KillsInSingleEvent, 15f, 1800, 900),
            new DailyMissionTemplate("weekly_no_damage_streak_75", DailyMissionObjective.KillsWithoutDamage, 75f, 2800, 1400),
            new DailyMissionTemplate("weekly_kill_1500", DailyMissionObjective.KillEnemies, 1500f, 1900, 950),
            new DailyMissionTemplate("weekly_kill_5000", DailyMissionObjective.KillEnemies, 5000f, 4200, 2100),
            new DailyMissionTemplate("weekly_bosses_12", DailyMissionObjective.KillBosses, 12f, 3200, 1600),
            new DailyMissionTemplate("weekly_bosses_20", DailyMissionObjective.KillBosses, 20f, 4800, 2400),
            new DailyMissionTemplate("weekly_perfect_35", DailyMissionObjective.PerfectWaves, 35f, 3400, 1700),
            new DailyMissionTemplate("weekly_perfect_50", DailyMissionObjective.PerfectWaves, 50f, 4600, 2300),
            new DailyMissionTemplate("weekly_reach_wave_40", DailyMissionObjective.ReachWave, 40f, 2600, 1300),
            new DailyMissionTemplate("weekly_reach_wave_60", DailyMissionObjective.ReachWave, 60f, 4400, 2200),
            new DailyMissionTemplate("weekly_complete_runs_8", DailyMissionObjective.CompleteRuns, 8f, 4000, 2000),
            new DailyMissionTemplate("weekly_complete_runs_12", DailyMissionObjective.CompleteRuns, 12f, 5200, 2600),
            new DailyMissionTemplate("weekly_chain_kills_300", DailyMissionObjective.KillByDamageSource, 300f, 3600, 1800, DamageSource.ChainLightning),
            new DailyMissionTemplate("weekly_explosion_kills_350", DailyMissionObjective.KillByDamageSource, 350f, 3800, 1900, DamageSource.Explosion),
            new DailyMissionTemplate("weekly_burn_kills_300", DailyMissionObjective.KillByDamageSource, 300f, 3600, 1800, DamageSource.Burn),
            new DailyMissionTemplate("weekly_event_kills_20", DailyMissionObjective.KillsInSingleEvent, 20f, 2600, 1300),
            new DailyMissionTemplate("weekly_event_kills_25", DailyMissionObjective.KillsInSingleEvent, 25f, 3400, 1700),
            new DailyMissionTemplate("weekly_no_damage_streak_100", DailyMissionObjective.KillsWithoutDamage, 100f, 3800, 1900),
            new DailyMissionTemplate("weekly_no_damage_streak_150", DailyMissionObjective.KillsWithoutDamage, 150f, 5000, 2500)
        };
    }
}
