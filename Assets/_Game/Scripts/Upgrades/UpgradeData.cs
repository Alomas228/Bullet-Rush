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

    public string UpgradeName => upgradeName;
    public string Description => description;

    public UpgradeType Type => type;
    public Rarity Rarity => rarity;

    public float PercentValue => percentValue;

    public WeaponData WeaponData => weaponData;

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