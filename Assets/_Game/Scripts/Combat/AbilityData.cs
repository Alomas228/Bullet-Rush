using UnityEngine;

/// <summary>
/// Способность, которую можно купить и снарядить в панели «Снаряжение».
/// Работает по тому же принципу, что и WeaponData: создаётся ассет
/// (Assets -> Create -> Arcade Survivor -> Ability), кладётся в список
/// availableAbilities на UpgradeManager — и появляется во вкладке
/// «Способности» магазина.
///
/// Снаряжённые способности применяются на старте забега компонентом
/// PlayerEquipmentApplier на игроке.
/// </summary>
public enum AbilityKind
{
    Bomb,
    Shield,
    CrescentSlash,
    ThunderSlam,
    FrostNova,
    GravityWell,
    OrbitBlades,
    ShadowFamiliar,
    LightningStorm,
    FlameRing,
    IceLance,
    FallingStar
}

[CreateAssetMenu(
    fileName = "Ability_",
    menuName = "Arcade Survivor/Ability"
)]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip(
        "Ключ перевода без префиксов .name/.desc/.stats — например " +
        "\"bomb\" для ключей abi.bomb.name. " +
        "Пусто — используется текст из полей ниже."
    )]
    [SerializeField] private string langKey;

    [SerializeField] private string abilityName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Classification")]
    [Tooltip("К какой внутриигровой способности относится предмет.")]
    [SerializeField] private AbilityKind kind;

    [Header("Meta (Shop / Equipment)")]
    [Tooltip(
        "Минимальный уровень игрока (XpManager), с которого способность " +
        "можно купить в магазине."
    )]
    [SerializeField] private int unlockLevel = 1;

    [Tooltip("Цена в монетах. 0 = бесплатно и всегда доступно.")]
    [SerializeField] private int price = 0;

    [Header("Store display")]
    [Tooltip(
        "Строка характеристик для карточки магазина, например " +
        "\"Урон: 5 • Радиус: 5 • Кулдаун: 8 с\"."
    )]
    [TextArea]
    [SerializeField] private string stats;

    [Header("Runtime (new abilities)")]
    [SerializeField, Min(0)] private float damage = 150f;
    [SerializeField, Min(.1f)] private float radius = 4f;
    [SerializeField, Min(.25f)] private float cooldown = 5f;
    [SerializeField, Range(1, 16)] private int maxTargets = 8;

    public string AbilityName => abilityName;

    public string Description => description;

    /// <summary>Имя на языке игрока; без перевода — имя из ассета.</summary>
    public string LocalizedName =>
        IsLegacy ? Lang.GetOr("abi." + langKey + ".name", abilityName) : AbilityPresentation.Name(kind);

    /// <summary>Описание на языке игрока; без перевода — текст ассета.</summary>
    public string LocalizedDescription =>
        IsLegacy ? Lang.GetOr("abi." + langKey + ".desc", description) : AbilityPresentation.Description(kind);

    /// <summary>Строка характеристик на языке игрока.</summary>
    public string LocalizedStats =>
        IsLegacy ? Lang.GetOr("abi." + langKey + ".stats", stats) : AbilityPresentation.Stats(this);

    public AbilityKind Kind => kind;
    public bool IsLegacy => kind == AbilityKind.Bomb || kind == AbilityKind.Shield;
    public bool IsAutomatic => (int)kind >= (int)AbilityKind.OrbitBlades;
    public float Damage => Mathf.Max(0f, damage);
    public float Radius => Mathf.Clamp(radius, .1f, 9f);
    public float Cooldown => Mathf.Max(.25f, cooldown);
    public int MaxTargets => Mathf.Clamp(maxTargets, 1, 16);

    public int UnlockLevel =>
        Mathf.Max(unlockLevel, 1);

    public int Price =>
        Mathf.Max(price, 0);

    public string Stats => stats;
}
