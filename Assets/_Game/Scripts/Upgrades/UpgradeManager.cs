using System.Collections.Generic;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("Player References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private Weapon weapon;
    [SerializeField] private AbilityManager abilityManager;

    [Header("Run Upgrades")]
    [SerializeField] private RunUpgrades runUpgrades;

    [Header("Normal Upgrades")]
    [SerializeField]
    private List<UpgradeData> availableUpgrades =
        new List<UpgradeData>();

    [Header("Weapons")]
    [SerializeField]
    private List<WeaponData> availableWeapons =
        new List<WeaponData>();

    [Header("Abilities (Shop / Equipment)")]
    [Tooltip(
        "Способности для вкладки «Способности» панели «Снаряжение». " +
        "Заполняется автоматически при сборке UI (Tools -> Bullet Rush)."
    )]
    [SerializeField]
    private List<AbilityData> availableAbilities =
        new List<AbilityData>();

    [Header("Clothing (Shop / Equipment)")]
    [Tooltip(
        "Одежда для вкладки «Одежда» панели «Снаряжение». " +
        "Заполняется автоматически при сборке UI (Tools -> Bullet Rush)."
    )]
    [SerializeField]
    private List<ClothingData> availableClothing =
        new List<ClothingData>();

    [Header("Selection")]
    [SerializeField] private int choicesCount = 3;

    [Header("Weapon Drop")]
    [Range(0f, 1f)]
    [SerializeField] private float weaponDropChance = 0.20f;

    private List<UpgradeData> currentChoices =
        new List<UpgradeData>();

    private List<UpgradeData> runtimeWeaponUpgrades =
        new List<UpgradeData>();

    public IReadOnlyList<UpgradeData> CurrentChoices =>
        currentChoices;

    /// <summary>
    /// Все оружия, доступные в забеге (используется панелью снаряжения
    /// и применением выбранного стартового оружия).
    /// </summary>
    public IReadOnlyList<WeaponData> GetAvailableWeapons() =>
        availableWeapons;

    /// <summary>
    /// Все способности магазина (вкладка «Способности» панели снаряжения).
    /// </summary>
    public IReadOnlyList<AbilityData> GetAvailableAbilities() =>
        availableAbilities;

    /// <summary>
    /// Вся одежда магазина (вкладка «Одежда» панели снаряжения).
    /// </summary>
    public IReadOnlyList<ClothingData> GetAvailableClothing() =>
        availableClothing;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void GenerateChoices()
    {
        ClearPreviousChoices();

        if (availableUpgrades.Count == 0)
        {
            Debug.LogWarning(
                "UpgradeManager: no normal upgrades available."
            );

            return;
        }

        bool weaponDrop =
            availableWeapons.Count > 0 &&
            Random.value < weaponDropChance;

        if (weaponDrop)
        {
            GenerateChoicesWithWeapon();

            Debug.Log(
                "WEAPON DROP! A weapon appeared in upgrade choices."
            );
        }
        else
        {
            GenerateNormalChoices();

            Debug.Log(
                "No weapon this wave. Normal upgrades generated."
            );
        }

        Debug.Log("Generated upgrade choices:");

        foreach (UpgradeData upgrade in currentChoices)
        {
            Debug.Log(
                $"{upgrade.UpgradeName} " +
                $"[{upgrade.Rarity}]"
            );
        }
    }

    private void GenerateNormalChoices()
    {
        List<UpgradeData> pool =
            new List<UpgradeData>(availableUpgrades);

        int count =
            Mathf.Min(
                choicesCount,
                pool.Count
            );

        for (int i = 0; i < count; i++)
        {
            UpgradeData selected =
                PickWeighted(pool);

            currentChoices.Add(selected);

            pool.Remove(selected);
        }
    }

    private void GenerateChoicesWithWeapon()
    {
        WeaponData selectedWeapon =
            GetRandomWeapon();

        if (selectedWeapon != null)
        {
            UpgradeData weaponUpgrade =
                ScriptableObject.CreateInstance<UpgradeData>();

            weaponUpgrade.InitializeWeapon(
                selectedWeapon
            );

            runtimeWeaponUpgrades.Add(
                weaponUpgrade
            );

            currentChoices.Add(
                weaponUpgrade
            );
        }

        List<UpgradeData> pool =
            new List<UpgradeData>(availableUpgrades);

        int normalChoices =
            choicesCount -
            currentChoices.Count;

        int count =
            Mathf.Min(
                normalChoices,
                pool.Count
            );

        for (int i = 0; i < count; i++)
        {
            UpgradeData selected =
                PickWeighted(pool);

            currentChoices.Add(selected);

            pool.Remove(selected);
        }
    }

    private WeaponData GetRandomWeapon()
    {
        if (availableWeapons.Count == 0)
            return null;

        int totalWeight = 0;

        foreach (WeaponData weapon in availableWeapons)
        {
            if (weapon == null)
                continue;

            totalWeight += GetRarityWeight(weapon.Rarity);
        }

        if (totalWeight <= 0)
        {
            return availableWeapons[
                Random.Range(0, availableWeapons.Count)
            ];
        }

        int roll =
            Random.Range(0, totalWeight);

        foreach (WeaponData weapon in availableWeapons)
        {
            if (weapon == null)
                continue;

            roll -= GetRarityWeight(weapon.Rarity);

            if (roll < 0)
                return weapon;
        }

        return availableWeapons[availableWeapons.Count - 1];
    }

    /// <summary>
    /// Случайный элемент пула, взвешенный по редкости:
    /// чем выше редкость, тем реже выпадает.
    /// </summary>
    private UpgradeData PickWeighted(
        List<UpgradeData> pool)
    {
        if (pool == null || pool.Count == 0)
            return null;

        int totalWeight = 0;

        for (int i = 0; i < pool.Count; i++)
        {
            UpgradeData candidate = pool[i];

            if (candidate == null)
                continue;

            totalWeight += GetRarityWeight(candidate.Rarity);
        }

        if (totalWeight <= 0)
        {
            return pool[
                Random.Range(0, pool.Count)
            ];
        }

        int roll =
            Random.Range(0, totalWeight);

        for (int i = 0; i < pool.Count; i++)
        {
            UpgradeData candidate = pool[i];

            if (candidate == null)
                continue;

            roll -= GetRarityWeight(candidate.Rarity);

            if (roll < 0)
                return candidate;
        }

        return pool[pool.Count - 1];
    }

    private int GetRarityWeight(Rarity rarity)
    {
        return Mathf.Max(5 - (int)rarity, 1);
    }

    public void ChooseUpgrade(int index)
    {
        if (index < 0 ||
            index >= currentChoices.Count)
        {
            Debug.LogWarning(
                $"Invalid upgrade index: {index}"
            );

            return;
        }

        UpgradeData selected =
            currentChoices[index];

        ApplyUpgrade(selected);

        PlayUpgradePickSound();

        Debug.Log(
            $"Player chose: " +
            $"{selected.UpgradeName} " +
            $"[{selected.Rarity}]"
        );
    }

    public void ApplyUpgrade(UpgradeData upgrade)
    {
        if (upgrade == null)
            return;

        EnsureAbilityManager();

        switch (upgrade.Type)
        {
            case UpgradeType.Damage:

                if (playerStats != null)
                {
                    playerStats.AddDamagePercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.FireRate:

                if (playerStats != null)
                {
                    playerStats.AddFireRatePercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.MoveSpeed:

                if (playerStats != null)
                {
                    playerStats.AddMoveSpeedPercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.ProjectileSpeed:

                if (playerStats != null)
                {
                    playerStats.AddProjectileSpeedPercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.MaxHealth:

                if (playerHealth != null)
                {
                    playerHealth.AddMaxHealthPercent(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.CriticalChance:

                if (playerStats != null)
                {
                    playerStats.AddCriticalChance(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.CriticalDamage:

                if (playerStats != null)
                {
                    playerStats.AddCriticalDamageMultiplier(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.HealthRegen:

                if (playerHealth != null)
                {
                    playerHealth.AddHealthRegen(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.ProjectileCount:

                if (playerStats != null)
                {
                    playerStats.AddProjectileCount(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.Pierce:

                if (playerStats != null)
                {
                    playerStats.AddPierce(
                        upgrade.PercentValue
                    );
                }

                break;

            case UpgradeType.Weapon:

                if (weapon != null &&
                    upgrade.WeaponData != null)
                {
                    weapon.SetWeapon(
                        upgrade.WeaponData
                    );
                }

                break;

            case UpgradeType.Special:

                Debug.Log(
                    "Special upgrade will be connected later."
                );

                break;

            case UpgradeType.Bomb:

                if (abilityManager != null)
                {
                    if (!abilityManager.HasBomb)
                    {
                        abilityManager.UnlockBomb();

                        Debug.Log(
                            "Bomb ability unlocked! Press E to blast nearby enemies."
                        );
                    }
                    else
                    {
                        BombAbility bomb =
                            abilityManager.Bomb;

                        if (bomb != null)
                        {
                            bomb.UpgradeDamage(
                                upgrade.PercentValue
                            );

                            bomb.UpgradeCooldown(0.10f);
                        }
                    }
                }

                break;

            case UpgradeType.Shield:

                if (abilityManager != null)
                {
                    if (!abilityManager.HasShield)
                    {
                        abilityManager.UnlockShield();

                        Debug.Log(
                            "Shield ability unlocked! Press Q to block damage."
                        );
                    }
                    else
                    {
                        ShieldAbility shield =
                            abilityManager.Shield;

                        if (shield != null)
                        {
                            shield.UpgradeDuration(
                                upgrade.PercentValue
                            );

                            shield.UpgradeCooldown(0.10f);
                        }
                    }
                }

                break;

            case UpgradeType.BurningRounds:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddBurn(
                        upgrade.EffectData as BurnEffectData
                    );
                }

                break;

            case UpgradeType.ExplosiveRounds:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddExplosion(
                        upgrade.EffectData as ExplosionEffectData
                    );
                }

                break;

            case UpgradeType.Bleeding:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddBleed(
                        upgrade.EffectData as BleedingEffectData
                    );
                }

                break;

            case UpgradeType.Lifesteal:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddLifesteal(
                        upgrade.EffectData as LifestealEffectData
                    );
                }

                break;

            case UpgradeType.ChainLightning:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddChainLightning(
                        upgrade.EffectData as ChainLightningEffectData
                    );
                }

                break;

            case UpgradeType.Ricochet:

                EnsureRunUpgrades();

                if (runUpgrades != null)
                {
                    runUpgrades.AddRicochet(
                        upgrade.EffectData as RicochetEffectData
                    );
                }

                break;
        }

        Debug.Log(
            $"Upgrade applied: " +
            $"{upgrade.UpgradeName} " +
            $"[{upgrade.Rarity}]"
        );
    }

    private void EnsureAbilityManager()
    {
        if (abilityManager != null)
            return;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            abilityManager =
                playerObject.GetComponent<AbilityManager>();

            if (abilityManager == null)
                abilityManager =
                    playerObject.AddComponent<AbilityManager>();
        }
    }

    private void EnsureRunUpgrades()
    {
        if (runUpgrades != null)
            return;

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            runUpgrades =
                playerObject.GetComponent<RunUpgrades>();

            if (runUpgrades == null)
                runUpgrades =
                    playerObject.AddComponent<RunUpgrades>();
        }
    }

    private void ClearPreviousChoices()
    {
        currentChoices.Clear();

        foreach (UpgradeData upgrade in runtimeWeaponUpgrades)
        {
            if (upgrade != null)
                Destroy(upgrade);
        }

        runtimeWeaponUpgrades.Clear();
    }

    private void PlayUpgradePickSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.UpgradePick);
    }

    private void OnDestroy()
    {
        foreach (UpgradeData upgrade in runtimeWeaponUpgrades)
        {
            if (upgrade != null)
                Destroy(upgrade);
        }

        runtimeWeaponUpgrades.Clear();
    }
}