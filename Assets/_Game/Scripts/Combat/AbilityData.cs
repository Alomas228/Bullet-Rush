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
    Shield
}

[CreateAssetMenu(
    fileName = "Ability_",
    menuName = "Arcade Survivor/Ability"
)]
public class AbilityData : ScriptableObject
{
    [Header("Identity")]
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

    public string AbilityName => abilityName;

    public string Description => description;

    public AbilityKind Kind => kind;

    public int UnlockLevel =>
        Mathf.Max(unlockLevel, 1);

    public int Price =>
        Mathf.Max(price, 0);

    public string Stats => stats;
}