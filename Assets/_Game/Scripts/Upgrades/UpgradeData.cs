using UnityEngine;

[CreateAssetMenu(
    fileName = "Upgrade_",
    menuName = "Arcade Survivor/Upgrade"
)]
public class UpgradeData : ScriptableObject
{
    /// <summary>
    /// Сколько раз за забег можно взять улучшение, если ассет
    /// не задал своё значение. Три уровня — достаточно, чтобы
    /// собрать узнаваемый билд, и мало, чтобы не сломать кривую.
    /// </summary>
    public const int DefaultMaxStacks = 3;

    [Header("Identity")]
    [Tooltip(
        "Ключ перевода без префикса .name/.desc — например " +
        "\"burning\" для ключей runup.burning.name. " +
        "Пусто — используется текст из полей ниже."
    )]
    [SerializeField] private string langKey;

    [SerializeField] private string upgradeName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Classification")]
    [SerializeField] private UpgradeType type;
    [SerializeField] private Rarity rarity = Rarity.Common;

    [Tooltip(
        "Семейства билда для синергий. None — вывести " +
        "автоматически из типа улучшения."
    )]
    [SerializeField]
    private UpgradeFamily families = UpgradeFamily.None;

    [Header("Value")]
    [SerializeField] private float percentValue = 0.10f;

    [Header("Stacking")]
    [Tooltip(
        "Сколько раз за забег можно взять это улучшение. " +
        "Достигнут лимит — карточка перестаёт выпадать. " +
        "0 = " + "DefaultMaxStacks."
    )]
    [SerializeField] private int maxStacks = DefaultMaxStacks;

    [Header("Weapon")]
    [SerializeField] private WeaponData weaponData;

    [Header("Effect")]
    [SerializeField] private UpgradeEffectData effectData;

    public string UpgradeName => upgradeName;
    public string Description => description;
    public RunPerk Perk { get; private set; }
    public UpgradeType CardIcon => Perk != RunPerk.None ? RunBuildMechanics.Icon(Perk) : type == UpgradeType.CharacterPower ? UpgradeType.Damage : type == UpgradeType.CharacterHaste ? UpgradeType.FireRate : type == UpgradeType.CharacterRange ? UpgradeType.ExplosiveRounds : type == UpgradeType.AbilityPower ? UpgradeType.Bomb :
        type == UpgradeType.AbilityHaste ? UpgradeType.FireRate : type == UpgradeType.AbilityRange ? UpgradeType.ExplosiveRounds : type;

    public UpgradeType Type => type;
    public Rarity Rarity => rarity;

    public UpgradeFamily Families =>
        UpgradeFamilyUtility.Resolve(families, type);

    public float PercentValue => percentValue;

    /// <summary>
    /// Лимит стаков за забег. Раньше его не существовало: любое
    /// улучшение можно было брать на каждой волне, и Damage или
    /// Projectile Count разгонялись до произвольных значений.
    /// </summary>
    public int MaxStacks =>
        maxStacks > 0
            ? maxStacks
            : DefaultMaxStacks;

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
            if (Perk != RunPerk.None) return RunBuildMechanics.Name(Perk);
            if(type==UpgradeType.CharacterPower) return GameplayJourney.T("Мастерство героя","Hero Power","Kahraman Gucu");
            if(type==UpgradeType.CharacterHaste) return GameplayJourney.T("Ритм героя","Hero Haste","Kahraman Hizi");
            if(type==UpgradeType.CharacterRange) return GameplayJourney.T("Размах героя","Hero Reach","Kahraman Menzili");
            if (type == UpgradeType.AbilityPower) return GameplayJourney.T("Сила умений", "Ability Power", "Yetenek Gucu");
            if (type == UpgradeType.AbilityHaste) return GameplayJourney.T("Поток энергии", "Ability Haste", "Enerji Akisi");
            if (type == UpgradeType.AbilityRange) return GameplayJourney.T("Размах", "Ability Reach", "Yetenek Menzili");
            if (weaponData != null && type == UpgradeType.Weapon)
                return weaponData.LocalizedName;

            if (string.IsNullOrEmpty(langKey))
                return upgradeName;

            return Lang.GetOr(
                "runup." + langKey + ".name",
                upgradeName
            );
        }
    }

    /// <summary>
    /// Прирост за один уровень строкой: «+10%», «+1»,
    /// «+0.1 HP/s». Это единственное число в карточке, которое
    /// не устаревает — оно берётся из самого ассета, а суммы
    /// по стакам в текст намеренно не попадают.
    /// </summary>
    public string GainText
    {
        get
        {
            switch (type)
            {
                case UpgradeType.Pierce:
                    return "+" + Mathf.RoundToInt(percentValue);

                case UpgradeType.HealthRegen:
                    return "+" + FormatDecimal(percentValue) + " HP/s";

                case UpgradeType.CriticalDamage:
                    return "+" + FormatDecimal(percentValue);

                case UpgradeType.Weapon:
                case UpgradeType.Special:
                case UpgradeType.Bomb:
                case UpgradeType.Shield:
                case UpgradeType.BurningRounds:
                case UpgradeType.ExplosiveRounds:
                case UpgradeType.Bleeding:
                case UpgradeType.Lifesteal:
                case UpgradeType.ChainLightning:
                case UpgradeType.Ricochet:
                    return string.Empty;

                default:
                    return "+" + Mathf.RoundToInt(percentValue * 100f) + "%";
            }
        }
    }

    private static string FormatDecimal(float value) =>
        value
            .ToString(
                "0.##",
                System.Globalization.CultureInfo.InvariantCulture
            )
            .Replace(',', '.');

    /// <summary>Описание на языке игрока.</summary>
    public string LocalizedDescription
    {
        get
        {
            if (Perk != RunPerk.None) return RunBuildMechanics.Description(Perk);
            if(type==UpgradeType.CharacterPower) return GameplayJourney.T("Сила двух врождённых умений ","Power of both innate skills ","Dogustan yetenek gucu ")+GainText;
            if(type==UpgradeType.CharacterHaste) return GameplayJourney.T("Кулдаун врождённых умений −","Innate cooldown -","Dogustan bekleme -")+Mathf.RoundToInt(percentValue*100f)+"%";
            if(type==UpgradeType.CharacterRange) return GameplayJourney.T("Радиус врождённых умений ","Innate skill range ","Dogustan menzil ")+GainText;
            if (type == UpgradeType.AbilityPower) return GameplayJourney.T("Урон умений ", "Ability damage ", "Yetenek hasari ") + GainText + GameplayJourney.T("; усиливает щит.", "; extends shield.", "; kalkani guclendirir.");
            if (type == UpgradeType.AbilityHaste) return GameplayJourney.T("Время перезарядки умений −", "Ability cooldown -", "Yetenek bekleme suresi -") + Mathf.RoundToInt(percentValue*100f) + "%";
            if (type == UpgradeType.AbilityRange) return GameplayJourney.T("Радиус и дальность умений ", "Ability radius and range ", "Yetenek alani ve menzili ") + GainText;
            if (weaponData != null && type == UpgradeType.Weapon)
                return weaponData.LocalizedDescription;

            if (string.IsNullOrEmpty(langKey))
                return description;

            if (!Lang.Has("runup." + langKey + ".desc"))
                return description;

            return Lang.Get(
                "runup." + langKey + ".desc",
                GainText
            );
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

        // Оружие не стекуется: его и так можно взять только один раз,
        // повторный выбор просто заменяет текущий ствол на тот же.
        // Перевод берётся из WeaponData, поэтому свой ключ не нужен.
        langKey = null;
        maxStacks = 1;
    }

    public void InitializeRunPerk(RunPerk perk, UpgradeFamily family, Rarity grade)
    {
        Perk = perk;
        type = UpgradeType.RunPerk;
        rarity = grade;
        families = family;
        maxStacks = 1;
        percentValue = 0;
        upgradeName = RunBuildMechanics.Name(perk);
        description = RunBuildMechanics.Description(perk);
    }

    public void InitializeAbilityUpgrade(UpgradeType upgradeType, float value)
    {
        type=upgradeType; rarity=Rarity.Uncommon; families=UpgradeFamily.Ability;
        maxStacks=3; percentValue=value; upgradeName=LocalizedName; description=LocalizedDescription;
    }
}
