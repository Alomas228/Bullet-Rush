using System;
using System.Collections.Generic;

/// <summary>
/// Собирает набор ежедневных миссий на конкретную дату.
///
/// Выбор детерминирован датой: у всех запусков игры в один день миссии
/// одинаковые, а на следующий день — другие. Случайность привязана к
/// номеру дня (year*10000+month*100+day), поэтому не зависит от порядка
/// запуска и не требует сети.
/// </summary>
public static class DailyMissionGenerator
{
    public static List<DailyMissionSaveData.Entry> Generate(
        DateTime date,
        int count)
    {
        return GenerateFromPool(
            date,
            count,
            DailyMissionPool.Templates
        );
    }

    public static List<DailyMissionSaveData.Entry> GenerateWeekly(
        DateTime weekStart,
        int count)
    {
        return GenerateFromPool(
            weekStart,
            count,
            WeeklyMissionPool.Templates
        );
    }

    private static List<DailyMissionSaveData.Entry> GenerateFromPool(
        DateTime date,
        int count,
        IReadOnlyList<DailyMissionTemplate> sourcePool)
    {
        count = Math.Max(count, 1);

        List<DailyMissionTemplate> pool =
            new List<DailyMissionTemplate>(sourcePool);

        if (pool.Count == 0)
            return new List<DailyMissionSaveData.Entry>();

        System.Random rng = new System.Random(SeedFor(date));

        Shuffle(pool, rng);

        List<DailyMissionSaveData.Entry> result =
            new List<DailyMissionSaveData.Entry>(count);

        for (int i = 0; i < count; i++)
        {
            // Если пул меньше запрошенного числа, начинаем по кругу —
            // минимум миссий гарантирован при любом размере пула.
            DailyMissionTemplate template = pool[i % pool.Count];

            result.Add(new DailyMissionSaveData.Entry
            {
                id = template.id,
                objective = (int)template.objective,
                source = (int)template.source,
                target = template.target,
                progress = 0f,
                completed = false,
                claimed = false,
                coinReward = template.coinReward,
                xpReward = template.xpReward
            });
        }

        return result;
    }

    private static int SeedFor(DateTime date)
    {
        return date.Year * 10000 + date.Month * 100 + date.Day;
    }

    /// <summary>Перемешивание Фишера — Йетса на переданном ГПСЧ.</summary>
    private static void Shuffle(List<DailyMissionTemplate> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);

            DailyMissionTemplate tmp = list[i];
            list[i] = list[j];
            list[j] = tmp;
        }
    }
}
