using UnityEngine;

/// <summary>
/// Предмет одежды (внешний вид игрока), который можно купить и снарядить
/// в панели «Снаряжение». Тот же принцип, что у WeaponData: создаётся
/// ассет (Assets -> Create -> Arcade Survivor -> Clothing), кладётся в
/// список availableClothing на UpgradeManager — и появляется во вкладке
/// «Одежда» магазина.
///
/// Снаряжённая одежда меняет внешний вид тела игрока на старте
/// забега (PlayerEquipmentApplier): натягивает текстуру скина, а
/// если её нет — красит куб цветом.
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
    [SerializeField] private bool defaultOutfit;

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
    [Tooltip(
        "Текстура скина, которая натягивается на куб игрока. " +
        "Если задана — заменяет цвет тела (bodyColor игнорируется). " +
        "Пусто — куб красится bodyColor."
    )]
    [SerializeField] private Texture2D skinTexture;

    [SerializeField] private Mesh bodyMesh;
    [SerializeField] private Sprite preview;

    public Mesh BodyMesh => bodyMesh;
    public Sprite Preview => preview;

    [Tooltip(
        "Цвет тела игрока при выбранной одежде. " +
        "Используется только когда текстура не задана."
    )]
    [SerializeField] private Color bodyColor = Color.white;

    public string ClothingName => clothingName;
    public string MapPerkId => langKey;
    public bool IsDefaultOutfit => defaultOutfit;

    public string Description => description;

    /// <summary>Имя на языке игрока; без перевода — имя из ассета.</summary>
    public string LocalizedName =>
        Lang.GetOr("clo." + langKey + ".name", clothingName);

    /// <summary>Описание на языке игрока; без перевода — текст ассета.</summary>
    public string LocalizedDescription =>
        Lang.GetOr("clo." + langKey + ".desc", description) + MapPerkDescription;

    private string MapPerkDescription => langKey switch
    {
        "astronaut" => "\n" + Lang.GetOr("map_perk.astronaut", "Space: oxygen capacity 36s instead of 10s."),
        "cowboy" => "\n" + Lang.GetOr("map_perk.cowboy", "Desert: 55% less storm darkening."),
        "police" => "\n" + Lang.GetOr("map_perk.police", "City: 45% less traffic damage."),
        "winter" => "\n" + Lang.GetOr("map_perk.winter", "Mountains: 35% less boulder damage."),
        _ => string.Empty
    };

    public int UnlockLevel =>
        Mathf.Max(unlockLevel, 1);

    public int Price =>
        Mathf.Max(price, 0);

    public Color BodyColor => bodyColor;

    /// <summary>Текстура скина; null, если одежда только красит куб.</summary>
    public Texture2D SkinTexture => skinTexture;

    /// <summary>Задана ли текстура скина (тогда цвет тела не используется).</summary>
    public bool HasSkinTexture => skinTexture != null;
}
