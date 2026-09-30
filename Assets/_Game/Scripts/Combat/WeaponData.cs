using UnityEngine;

[CreateAssetMenu(
    fileName = "Weapon_",
    menuName = "Arcade Survivor/Weapon"
)]
public class WeaponData : ScriptableObject
{
    /// <summary>
    /// Потолок снарядов за выстрел, который даёт улучшение Projectile
    /// Count. Нужен потому, что процентный бонус сам по себе
    /// умножается: без потолка четыре стака превращали однозарядное
    /// оружие в залп из четырёх пуль, а каждое попадание ещё и
    /// множило горение, кровотечение, взрыв и рикошет.
    /// Потолок равен максимуму уровней Projectile Count, поэтому
    /// однозарядный ствол растёт как 2 → 3 → 4, а дробовик берёт
    /// один-два уровня и карточка у него потом исчезает.
    /// Базовая стрельба потолком не урезается — потолок всегда не
    /// меньше базового числа снарядов.
    /// </summary>
    public const int MaxBonusProjectilesPerShot = 4;

    /// <summary>
    /// Сколько снарядов выстрелит оружие при заданном процентном
    /// бонусе. Формула одна и для выстрела, и для фильтра выдачи
    /// карточек, иначе «+снаряды» продолжит предлагаться там, где
    /// число уже упирается в потолок.
    ///
    /// Процент округляется вверх: иначе бонус однозарядному стволу
    /// не дал бы вообще ничего. Потолок не опускается ниже базового
    /// значения, поэтому дробовик не теряет свои штатные снаряды.
    /// </summary>
    public static int ResolveProjectileCount(
        int baseCount,
        float percentBonus)
    {
        int baseValue = Mathf.Max(baseCount, 1);

        if (percentBonus <= 0f)
            return baseValue;

        int ceiling = Mathf.Max(
            baseValue,
            MaxBonusProjectilesPerShot
        );

        return Mathf.Clamp(
            Mathf.CeilToInt(
                baseValue * (1f + percentBonus)
            ),
            baseValue,
            ceiling
        );
    }

    [Header("Identity")]
    [Tooltip(
        "Ключ перевода без префикса .name/.desc — например " +
        "\"basic_rifle\" для ключей wpn.basic_rifle.desc. " +
        "Пусто — используется текст из полей ниже."
    )]
    [SerializeField] private string langKey;

    [SerializeField] private string weaponName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Classification")]
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private Rarity rarity = Rarity.Common;

    [Header("Meta (Shop / Equipment)")]
    [Tooltip("Минимальный уровень игрока (XpManager), с которого оружие можно купить в магазине.")]
    [SerializeField] private int unlockLevel = 1;
    [Tooltip("Цена в монетах. 0 = бесплатно и всегда доступно после достижения уровня.")]
    [SerializeField] private int price = 0;

    [Header("Visual")]
    [SerializeField] private GameObject weaponPrefab;

    [Header("Stats")]
    [SerializeField] private float damage = 1f;
    [SerializeField] private float fireRate = 5f;
    [SerializeField] private float projectileSpeed = 15f;

    [Header("Projectile")]
    [SerializeField] private int projectileCount = 1;
    [SerializeField] private float spreadAngle = 0f;
    [SerializeField] private int pierceCount = 0;

    [Header("Special")]
    [SerializeField]
    private WeaponSpecialType specialType =
        WeaponSpecialType.None;

    [Header("Burst")]
    [SerializeField] private int shotsPerBurst = 3;
    [SerializeField] private float burstInterval = 0.08f;

    [Header("Critical Chance")]
    [Range(0f, 1f)]
    [SerializeField] private float criticalChanceBonus = 0f;

    [Header("Critical Damage")]
    [SerializeField] private float criticalDamageBonus = 0f;

    [Header("Score Bonus")]
    [SerializeField] private float scoreBonusPercent = 0f;

    [Header("Double Shot")]
    [SerializeField] private float doubleShotInterval = 0.12f;

    [Header("Burn")]
    [SerializeField] private float burnDamagePerSecond = 3f;
    [SerializeField] private float burnDuration = 2f;
    [SerializeField] private float burnTickInterval = 0.5f;

    [Header("Recoil")]
    [SerializeField] private float recoilForce = 0f;

    [Header("Screen Shake")]
    [Tooltip("Тряска экрана при выстреле этой пушки.")]
    [SerializeField] private float shakeOnFire = 0.06f;

    [Header("Lightning")]
    [Range(0f, 1f)]
    [SerializeField] private float lightningChance = 0.25f;

    [SerializeField] private float lightningDamage = 8f;

    [SerializeField] private int lightningTargets = 2;

    [SerializeField] private float lightningRange = 4f;


    public string WeaponName => weaponName;

    public string Description => description;

    /// <summary>Имя на языке игрока; без перевода — имя из ассета.</summary>
    public string LocalizedName =>
        Lang.GetOr("wpn." + langKey + ".name", weaponName);

    /// <summary>Описание на языке игрока; без перевода — текст ассета.</summary>
    public string LocalizedDescription =>
        Lang.GetOr("wpn." + langKey + ".desc", description);

    public WeaponType WeaponType => weaponType;

    public Rarity Rarity => rarity;

    public int UnlockLevel =>
        Mathf.Max(unlockLevel, 1);

    public int Price =>
        Mathf.Max(price, 0);

    public GameObject WeaponPrefab => weaponPrefab;


    public float Damage => damage;

    public float FireRate => fireRate;

    public float ProjectileSpeed =>
        projectileSpeed;


    public int ProjectileCount =>
        projectileCount;

    public float SpreadAngle =>
        spreadAngle;

    public int PierceCount =>
        pierceCount;


    public WeaponSpecialType SpecialType =>
        specialType;


    public bool IsBurstWeapon =>
        specialType ==
        WeaponSpecialType.Burst;

    public int ShotsPerBurst =>
        Mathf.Max(
            shotsPerBurst,
            1
        );

    public float BurstInterval =>
        Mathf.Max(
            burstInterval,
            0f
        );


    public float CriticalChanceBonus =>
        criticalChanceBonus;


    public float CriticalDamageBonus =>
        criticalDamageBonus;


    public float ScoreBonusPercent =>
        scoreBonusPercent;


    public float DoubleShotInterval =>
        Mathf.Max(
            doubleShotInterval,
            0f
        );


    public float BurnDamagePerSecond =>
        burnDamagePerSecond;

    public float BurnDuration =>
        burnDuration;

    public float BurnTickInterval =>
        Mathf.Max(
            burnTickInterval,
            0.05f
        );


    public float LightningChance =>
        Mathf.Clamp01(
            lightningChance
        );

    public float LightningDamage =>
        lightningDamage;

    public int LightningTargets =>
        Mathf.Max(
            lightningTargets,
            1
        );

    public float LightningRange =>
        Mathf.Max(
            lightningRange,
            0f
        );


    public float RecoilForce =>
        recoilForce;

    public float ShakeOnFire =>
        Mathf.Max(
            shakeOnFire,
            0f
        );
}