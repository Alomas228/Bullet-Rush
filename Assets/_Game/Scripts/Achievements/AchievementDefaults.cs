using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Набор достижений по умолчанию. Используется только когда список на
/// AchievementManager пуст (например, менеджер создан в рантайме
/// бутстрапом, а ассеты не заведены). Как только в инспекторе назначены
/// ассеты, этот код не выполняется — всё настраивается данными.
/// </summary>
public static class AchievementDefaults
{
    public static List<AchievementData> Create()
    {
        var list = new List<AchievementData>
        {
            Make(
                "nomercy",
                "ach.nomercy.title", "NO MERCY",
                "ach.nomercy.desc", "Убей 100 врагов, не получив урона, за один забег.",
                AchievementCategory.Combat,
                AchievementConditionType.KillsWithoutDamageInRun,
                100f,
                DamageSource.Unknown,
                false,
                Currency(300)
            ),

            Make(
                "pyromaniac",
                "ach.pyromaniac.title", "PYROMANIAC",
                "ach.pyromaniac.desc", "Убей 50 врагов именно уроном от Burning.",
                AchievementCategory.Mastery,
                AchievementConditionType.KillsByDamageSource,
                50f,
                DamageSource.Burn,
                false,
                Clothing("Камуфляж")
            ),

            Make(
                "chainreaction",
                "ach.chainreaction.title", "CHAIN REACTION",
                "ach.chainreaction.desc", "Убей 10 врагов одной цепной реакцией или одним взрывом.",
                AchievementCategory.Combat,
                AchievementConditionType.KillsInSingleEvent,
                10f,
                DamageSource.Unknown,
                false,
                Modifier(RunModifier.Overcharge)
            ),

            Make(
                "untouchable",
                "ach.untouchable.title", "UNTOUCHABLE",
                "ach.untouchable.desc", "Достигни 10-й волны, не получив урона за весь забег.",
                AchievementCategory.Survival,
                AchievementConditionType.ReachWaveWithoutDamage,
                10f,
                DamageSource.Unknown,
                false,
                Currency(500)
            ),

            Make(
                "arsenal",
                "ach.arsenal.title", "ARSENAL",
                "ach.arsenal.desc", "Победи в забеге с каждым доступным оружием.",
                AchievementCategory.Weapons,
                AchievementConditionType.WinWithUniqueWeapons,
                0f,
                DamageSource.Unknown,
                false,
                Clothing("Неон")
            ),

            Make(
                "bosslayer",
                "ach.bosslayer.title", "BOSS SLAYER",
                "ach.bosslayer.desc", "Победи всех уникальных боссов игры.",
                AchievementCategory.Bosses,
                AchievementConditionType.UniqueBosses,
                3f,
                DamageSource.Unknown,
                false,
                Weapon("Plasma Rifle")
            ),

            Make(
                "perfectionist",
                "ach.perfectionist.title", "PERFECTIONIST",
                "ach.perfectionist.desc", "Получи 10 Perfect Waves за один забег.",
                AchievementCategory.Mastery,
                AchievementConditionType.PerfectWavesInRun,
                10f,
                DamageSource.Unknown,
                false,
                Modifier(RunModifier.GlassCannon)
            ),

            Make(
                "lifetime_kills_10000",
                "ach.milestone.kills.title", "EXTERMINATOR",
                "ach.milestone.kills.desc", "Убей 10 000 врагов в общем.",
                AchievementCategory.Combat,
                AchievementConditionType.CumulativeKills,
                10000f,
                DamageSource.Unknown,
                false,
                Currency(1000)
            ),

            Make(
                "lifetime_bosses_50",
                "ach.milestone.bosses.title", "BOSS HUNTER",
                "ach.milestone.bosses.desc", "Убей 50 боссов в общем.",
                AchievementCategory.Bosses,
                AchievementConditionType.CumulativeBossKills,
                50f,
                DamageSource.Unknown,
                false,
                Currency(1500)
            ),

            Make(
                "lifetime_waves_1000",
                "ach.milestone.waves.title", "ENDLESS SURVIVOR",
                "ach.milestone.waves.desc", "Достигни суммарно 1000 волн.",
                AchievementCategory.Survival,
                AchievementConditionType.CumulativeWaves,
                1000f,
                DamageSource.Unknown,
                false,
                Currency(1500)
            ),

            Make(
                "lifetime_runs_100",
                "ach.milestone.runs.title", "VETERAN",
                "ach.milestone.runs.desc", "Заверши 100 забегов.",
                AchievementCategory.Survival,
                AchievementConditionType.CumulativeRuns,
                100f,
                DamageSource.Unknown,
                false,
                Currency(2000)
            )
        };

        return list;
    }

    private static AchievementData Make(
        string id,
        string titleKey,
        string titleFallback,
        string descriptionKey,
        string descriptionFallback,
        AchievementCategory category,
        AchievementConditionType conditionType,
        float target,
        DamageSource source,
        bool hidden,
        params AchievementReward[] rewards)
    {
        AchievementData data = ScriptableObject.CreateInstance<AchievementData>();

        data.ConfigureRuntime(
            id,
            titleKey,
            titleFallback,
            descriptionKey,
            descriptionFallback,
            category,
            conditionType,
            target,
            source,
            hidden,
            null,
            new List<AchievementReward>(rewards)
        );

        data.name = "Achievement_" + id;

        return data;
    }

    private static AchievementReward Currency(int amount) =>
        new AchievementReward
        {
            type = AchievementRewardType.Currency,
            currencyAmount = amount
        };

    private static AchievementReward Clothing(string name) =>
        new AchievementReward
        {
            type = AchievementRewardType.ClothingUnlock,
            customId = name
        };

    private static AchievementReward Weapon(string name) =>
        new AchievementReward
        {
            type = AchievementRewardType.WeaponUnlock,
            customId = name
        };

    private static AchievementReward Modifier(RunModifier modifier) =>
        new AchievementReward
        {
            type = AchievementRewardType.RunModifier,
            modifier = modifier
        };
}
