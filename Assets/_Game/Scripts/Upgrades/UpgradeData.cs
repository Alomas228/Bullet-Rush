using UnityEngine;

[CreateAssetMenu(
    fileName = "Upgrade_",
    menuName = "Arcade Survivor/Upgrade"
)]
public class UpgradeData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string upgradeName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Classification")]
    [SerializeField] private UpgradeType type;
    [SerializeField] private Rarity rarity = Rarity.Common;

    [Header("Value")]
    [SerializeField] private float percentValue = 0.10f;

    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;

    [Header("Effect")]
    [SerializeField] private UpgradeEffectData effectData;

    public string UpgradeName => upgradeName;
    public string Description => description;

    public UpgradeType Type => type;
    public Rarity Rarity => rarity;

    public float PercentValue => percentValue;

    public WeaponData WeaponData => weaponData;

    public UpgradeEffectData EffectData => effectData;

    /// <summary>
    /// Имя на языке игрока. Улучшение-оружие наследует перевод
    /// описания из WeaponData, остальные используют своё поле.
    /// </summary>
    public string LocalizedName
    {
        get
        {
            if (weaponData != null && type == UpgradeType.Weapon)
                return weaponData.LocalizedName;

            return upgradeName;
        }
    }

    /// <summary>Описание на языке игрока.</summary>
    public string LocalizedDescription
    {
        get
        {
            if (weaponData != null && type == UpgradeType.Weapon)
                return weaponData.LocalizedDescription;

            return description;
        }
    }

    /// <summary>
    /// Заполняет поля из ассета оружия. Текст сохраняется в ассете,
    /// но показывается игроку через локализованные свойства выше —
    /// иначе смена языка не подхватилась бы без пересоздания карточки.
    /// </summary>
    public void InitializeWeapon(WeaponData data)
    {
        if (data == null)
            return;

        upgradeName = data.WeaponName;
        description = data.Description;

        type = UpgradeType.Weapon;
        rarity = data.Rarity;

        percentValue = 0f;
        weaponData = data;
    }
}