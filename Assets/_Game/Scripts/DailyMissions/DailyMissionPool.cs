using System.Collections.Generic;

/// <summary>
/// Пул возможных ежедневных миссий. Генератор каждый день выбирает из
/// него набор, поэтому одно и то же задание не выпадает изо дня в день.
///
/// Пул набран так, чтобы миссии подходили и новичку, и опытному игроку:
/// встречаются простые (25 убийств) и требовательные (2 босса за день).
/// Новое задание добавляется одной строкой здесь, без правок логики.
/// </summary>
public static class DailyMissionPool
{
    private static List<DailyMissionTemplate> _templates;

    public static IReadOnlyList<DailyMissionTemplate> Templates
    {
        get
        {
            if (_templates == null)
                _templates = Build();

            return _templates;
        }
    }

    private static List<DailyMissionTemplate> Build()
    {
        return new List<DailyMissionTemplate>
        {
            new DailyMissionTemplate(
                "kill_25",
                DailyMissionObjective.KillEnemies,
                25f,
                120,
                60),

            new DailyMissionTemplate(
                "kill_75",
                DailyMissionObjective.KillEnemies,
                75f,
                250,
                120),

            new DailyMissionTemplate(
                "kill_150",
                DailyMissionObjective.KillEnemies,
                150f,
                400,
                200),

            new DailyMissionTemplate(
                "burn_kills",
                DailyMissionObjective.KillByDamageSource,
                20f,
                200,
                100,
                DamageSource.Burn),

            new DailyMissionTemplate(
                "explosion_kills",
                DailyMissionObjective.KillByDamageSource,
                20f,
                200,
                100,
                DamageSource.Explosion),

            new DailyMissionTemplate(
                "chain_kills",
                DailyMissionObjective.KillByDamageSource,
                15f,
                220,
                110,
                DamageSource.ChainLightning),

            new DailyMissionTemplate(
                "perfect_waves",
                DailyMissionObjective.PerfectWaves,
                3f,
                250,
                120),

            new DailyMissionTemplate(
                "reach_wave",
                DailyMissionObjective.ReachWave,
                8f,
                300,
                150),

            new DailyMissionTemplate(
                "boss_kill",
                DailyMissionObjective.KillBosses,
                1f,
                400,
                200),

            new DailyMissionTemplate(
                "event_kills",
                DailyMissionObjective.KillsInSingleEvent,
                8f,
                250,
                130),

            new DailyMissionTemplate(
                "no_damage_streak",
                DailyMissionObjective.KillsWithoutDamage,
                30f,
                300,
                150),

            new DailyMissionTemplate(
                "win_run",
                DailyMissionObjective.CompleteRuns,
                1f,
                500,
                250)
        };
    }
}
