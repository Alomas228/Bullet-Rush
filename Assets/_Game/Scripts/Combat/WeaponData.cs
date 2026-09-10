using UnityEngine;

[CreateAssetMenu(
    fileName = "Weapon_",
    menuName = "Arcade Survivor/Weapon"
)]
public class WeaponData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string weaponName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Classification")]
    [SerializeField] private WeaponType weaponType;
    [SerializeField] private Rarity rarity = Rarity.Common;

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

    [Header("Lightning")]
    [Range(0f, 1f)]
    [SerializeField] private float lightningChance = 0.25f;

    [SerializeField] private float lightningDamage = 8f;

    [SerializeField] private int lightningTargets = 2;

    [SerializeField] private float lightningRange = 4f;


    public string WeaponName => weaponName;

    public string Description => description;

    public WeaponType WeaponType => weaponType;

    public Rarity Rarity => rarity;

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
}