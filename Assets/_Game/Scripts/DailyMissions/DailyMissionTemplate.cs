/// <summary>
/// Шаблон (заготовка) ежедневной миссии. Из пула таких шаблонов
/// генератор каждый день выбирает набор на день. Логики здесь нет —
/// только данные, как и у AchievementData.
/// </summary>
public class DailyMissionTemplate
{
    /// <summary>Уникальный строковый ID. Указывается в сохранении дня, не менять после релиза.</summary>
    public readonly string id;

    public readonly DailyMissionObjective objective;

    /// <summary>Сколько нужно накопить/достичь.</summary>
    public readonly float target;

    /// <summary>Источник урона для условия KillByDamageSource.</summary>
    public readonly DamageSource source;

    public readonly int coinReward;

    public readonly int xpReward;

    public DailyMissionTemplate(
        string id,
        DailyMissionObjective objective,
        float target,
        int coinReward,
        int xpReward,
        DamageSource source = DamageSource.Unknown)
    {
        this.id = id;
        this.objective = objective;
        this.target = target;
        this.coinReward = coinReward;
        this.xpReward = xpReward;
        this.source = source;
    }
}
