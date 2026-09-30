using UnityEngine;

/// <summary>
/// Предмет одежды (внешний вид игрока), который можно купить и снарядить
/// в панели «Снаряжение». Тот же принцип, что у WeaponData: создаётся
/// ассет (Assets -> Create -> Arcade Survivor -> Clothing), кладётся в
/// список availableClothing на UpgradeManager — и появляется во вкладке
/// «Одежда» магазина.
///
/// Снаряжённая одежда меняет цвет тела игрока на старте забега
/// (PlayerEquipmentApplier).
/// </summary>
[CreateAssetMenu(
    fileName = "Clothing_",
    menuName = "Arcade Survivor/Clothing"
)]
public class ClothingData : ScriptableObject
{
    [Header("Identity")]
    [Tooltip(
        "Ключ перевода без префиксов .name/.desc — например " +
        "\"camo\" для ключей clo.camo.name. " +
        "Пусто — используется текст из полей ниже."
    )]
    [SerializeField] private string langKey;

    [SerializeField] private string clothingName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Meta (Shop / Equipment)")]
    [Tooltip(
        "Минимальный уровень игрока (XpManager), с которого одежду " +
        "можно купить в магазине."
    )]
    [SerializeField] private int unlockLevel = 1;

    [Tooltip("Цена в монетах. 0 = бесплатно и всегда доступно.")]
    [SerializeField] private int price = 0;

    [Header("Visual")]
    [Tooltip("Цвет тела игрока при выбранной одежде.")]
    [SerializeField] private Color bodyColor = Color.white;

    public string ClothingName => clothingName;

    public string Description => description;

    /// <summary>Имя на языке игрока; без перевода — имя из ассета.</summary>
    public string LocalizedName =>
        Lang.GetOr("clo." + langKey + ".name", clothingName);

    /// <summary>Описание на языке игрока; без перевода — текст ассета.</summary>
    public string LocalizedDescription =>
        Lang.GetOr("clo." + langKey + ".desc", description);

    public int UnlockLevel =>
        Mathf.Max(unlockLevel, 1);

    public int Price =>
        Mathf.Max(price, 0);

    public Color BodyColor => bodyColor;
}