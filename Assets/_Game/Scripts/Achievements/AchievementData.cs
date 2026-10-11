using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Описание одного достижения. Создаётся как ассет
/// (Assets -> Create -> Arcade Survivor -> Achievement) и кладётся
/// в список AchievementManager. Никакой логики разблокировки здесь нет:
/// ассет — это только данные, а вся механика живёт в AchievementManager.
///
/// Благодаря этому новое достижение добавляется без правок кода:
/// создать ассет, выбрать тип условия, цель и награды.
/// </summary>
[CreateAssetMenu(
    fileName = "Achievement_",
    menuName = "Arcade Survivor/Achievement"
)]
public class AchievementData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip("Уникальный строковый ID. Хранится в сохранении — не меняй после релиза.")]
    [SerializeField] private string achievementId = "achievement_id";

    [Tooltip("Ключ перевода заголовка (например ach.nomercy.title). Пусто — берётся titleFallback.")]
    [SerializeField] private string titleKey;

    [SerializeField] private string titleFallback = "Достижение";

    [Tooltip("Ключ перевода описания. Пусто — берётся descriptionFallback.")]
    [SerializeField] private string descriptionKey;

    [TextArea]
    [SerializeField] private string descriptionFallback = string.Empty;

    [SerializeField] private Sprite icon;

    [Header("Classification")]
    [SerializeField] private AchievementCategory category = AchievementCategory.Combat;

    [Tooltip("Скрытое достижение: до разблокировки условие и описание скрыты.")]
    [SerializeField] private bool hidden;

    [Header("Condition")]
    [SerializeField] private AchievementConditionType conditionType = AchievementConditionType.CumulativeKills;

    [Tooltip("Цель. Для коллекционных (оружие/боссы) 0 = вычислить автоматически.")]
    [SerializeField] private float targetValue = 1f;

    [Tooltip("Источник урона для условия KillsByDamageSource.")]
    [SerializeField] private DamageSource damageSource = DamageSource.Burn;

    [Header("Rewards")]
    [SerializeField] private List<AchievementReward> rewards = new List<AchievementReward>();

    // =========================================================
    // PROPERTIES
    // =========================================================

    public string Id => achievementId;

    public string TitleKey => titleKey;

    public string TitleFallback => titleFallback;

    public string DescriptionKey => descriptionKey;

    public string DescriptionFallback => descriptionFallback;

    public Sprite Icon => icon;

    public AchievementCategory Category => category;

    public bool IsHidden => hidden;

    public AchievementConditionType ConditionType => conditionType;

    public float TargetValue => targetValue;

    public DamageSource DamageSource => damageSource;

    public IReadOnlyList<AchievementReward> Rewards => rewards;

    public bool HasProgressBar =>
        conditionType == AchievementConditionType.CumulativeKills ||
        conditionType == AchievementConditionType.KillsWithoutDamageInRun ||
        conditionType == AchievementConditionType.KillsByDamageSource ||
        conditionType == AchievementConditionType.KillsInSingleEvent ||
        conditionType == AchievementConditionType.ReachWaveWithoutDamage ||
        conditionType == AchievementConditionType.WinWithUniqueWeapons ||
        conditionType == AchievementConditionType.UniqueBosses ||
        conditionType == AchievementConditionType.PerfectWavesInRun;

    // =========================================================
    // LOCALIZED TEXT
    // =========================================================

    public string GetTitle()
    {
        if (!string.IsNullOrEmpty(titleKey))
            return Lang.GetOr(titleKey, titleFallback);

        return titleFallback;
    }

    /// <summary>
    /// Описание с учётом скрытости: пока достижение не разблокировано
    /// и помечено hidden, текст условия не раскрывается.
    /// </summary>
    public string GetDescription(bool unlocked)
    {
        if (hidden && !unlocked)
            return Lang.GetOr("ach.hidden_hint", "Секретное достижение. Условие откроется после получения.");

        if (!string.IsNullOrEmpty(descriptionKey))
            return Lang.GetOr(descriptionKey, descriptionFallback);

        return descriptionFallback;
    }

    // =========================================================
    // RUNTIME CONFIGURATION
    // =========================================================

    /// <summary>
    /// Заполняет ассет из кода. Нужно только для достижений по умолчанию,
    /// которые создаются в памяти, если список менеджера пуст. Ассеты из
    /// инспектора идут обычным путём и сюда не попадают.
    /// </summary>
    public void ConfigureRuntime(
        string id,
        string titleKey,
        string titleFallback,
        string descriptionKey,
        string descriptionFallback,
        AchievementCategory category,
        AchievementConditionType conditionType,
        float targetValue,
        DamageSource damageSource,
        bool hidden,
        Sprite icon,
        List<AchievementReward> rewards)
    {
        achievementId = id;
        this.titleKey = titleKey;
        this.titleFallback = titleFallback;
        this.descriptionKey = descriptionKey;
        this.descriptionFallback = descriptionFallback;
        this.category = category;
        this.conditionType = conditionType;
        this.targetValue = targetValue;
        this.damageSource = damageSource;
        this.hidden = hidden;
        this.icon = icon;
        this.rewards = rewards ?? new List<AchievementReward>();
    }
}
