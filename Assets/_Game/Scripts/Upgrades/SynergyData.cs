using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "Synergy_",
    menuName = "Arcade Survivor/Synergy"
)]
public class SynergyData : ScriptableObject
{
    public enum SynergyKind
    {
        Specialist,
        Hybrid,
        Chaos,
        Weapon
    }

    public enum RequirementKind
    {
        Upgrade,
        Family,
        DistinctFamilies,
        WeaponTaken
    }

    [System.Serializable]
    public class SynergyRequirement
    {
        [SerializeField] private RequirementKind kind =
            RequirementKind.Upgrade;

        [SerializeField] private UpgradeData upgrade;

        [SerializeField] private WeaponData weapon;

        [SerializeField]
        private UpgradeFamily family = UpgradeFamily.None;

        [SerializeField] private int count = 1;

        [SerializeField] private int target = 1;

        public RequirementKind Kind => kind;

        public UpgradeData Upgrade => upgrade;

        public WeaponData Weapon => weapon;

        public UpgradeFamily Family => family;

        public int Count => count > 0 ? count : 1;

        public int Target => target > 0 ? target : 1;

public int GetCurrent(RunBuildState build)
        {
            if (build == null)
                return 0;

            switch (kind)
            {
                case RequirementKind.Upgrade:
                    return upgrade != null
                        ? UnityEngine.Mathf.Min(
                            build.GetStacks(upgrade),
                            Target
                        )
                        : 0;

                case RequirementKind.Family:
                    return family != UpgradeFamily.None
                        ? UnityEngine.Mathf.Min(
                            build.GetFamilyStacks(family),
                            Count
                        )
                        : 0;

                case RequirementKind.DistinctFamilies:
                    return UnityEngine.Mathf.Min(
                        build.GetDistinctFamilyCount(family),
                        Count
                    );

                case RequirementKind.WeaponTaken:

                    if (weapon != null)
                        return build.HasTakenWeapon(weapon) ? 1 : 0;

                    return UnityEngine.Mathf.Min(
                        build.GetDistinctWeaponCount(),
                        Count
                    );

                default:
                    return 0;
            }
        }

        public int GetRequired()
        {
            switch (kind)
            {
                case RequirementKind.Upgrade:
                    return upgrade != null ? Target : 0;

                case RequirementKind.Family:
                    return family != UpgradeFamily.None ? Count : 0;

                case RequirementKind.DistinctFamilies:
                    return Count;

                case RequirementKind.WeaponTaken:
                    return weapon != null ? 1 : Count;

                default:
                    return 0;
            }
        }

        public bool IsSatisfied(RunBuildState build)
        {
            if (build == null)
                return false;

            switch (kind)
            {
                case RequirementKind.Upgrade:
                    return upgrade != null &&
                           build.GetStacks(upgrade) >= Target;

                case RequirementKind.Family:
                    return family != UpgradeFamily.None &&
                           build.GetFamilyStacks(family) >= Count;

                case RequirementKind.DistinctFamilies:
                    return build.GetDistinctFamilyCount(family) >= Count;

                case RequirementKind.WeaponTaken:
                    return weapon != null
                        ? build.HasTakenWeapon(weapon)
                        : build.GetDistinctWeaponCount() >= Count;

                default:
                    return false;
            }
        }

        public bool Involves(
            UpgradeData candidate,
            UpgradeFamily families)
        {
            switch (kind)
            {
                case RequirementKind.Upgrade:
                    return upgrade != null &&
                           candidate == upgrade;

                case RequirementKind.Family:
                    return UpgradeFamilyUtility.Intersects(
                        families,
                        family
                    );

                case RequirementKind.DistinctFamilies:
                    return families != UpgradeFamily.None &&
                           (family == UpgradeFamily.None ||
                            UpgradeFamilyUtility.Intersects(
                                families,
                                family
                            ));

                case RequirementKind.WeaponTaken:
                    return false;

                default:
                    return false;
            }
        }
    }

    [Header("Identity")]
    [Tooltip(
        "Ключ перевода без префикса — например \"firestorm\" " +
        "для ключей synergy.firestorm.name"
    )]
    [SerializeField] private string langKey;

    [SerializeField] private string displayName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Classification")]
    [SerializeField] private SynergyKind kind =
        SynergyKind.Specialist;

    [SerializeField] private Rarity rarity = Rarity.Rare;

    [Header("Availability")]
    [Tooltip(
        "Условие доступности: маска семейств, хотя бы одно из " +
        "которых должно быть в билде. None — доступна всегда."
    )]
    [SerializeField]
    private UpgradeFamily unlockFamily = UpgradeFamily.None;

    [Header("Requirements")]
    [Tooltip("Обязательные условия активации.")]
    [SerializeField]
    private List<SynergyRequirement> requirements =
        new List<SynergyRequirement>();

    [Tooltip(
        "Необязательные условия: не блокируют активацию, " +
        "но выполняются — дают бонусные награды."
    )]
    [SerializeField]
    private List<SynergyRequirement> optionalRequirements =
        new List<SynergyRequirement>();

    [Header("Rewards")]
    [SerializeField]
    private List<UpgradeData> rewards =
        new List<UpgradeData>();

    [SerializeField]
    private List<UpgradeData> optionalRewards =
        new List<UpgradeData>();

    public SynergyKind Kind => kind;

    public Rarity Rarity => rarity;

    public UpgradeFamily UnlockFamily => unlockFamily;

    public IReadOnlyList<SynergyRequirement> Requirements =>
        requirements;

    public IReadOnlyList<SynergyRequirement> OptionalRequirements =>
        optionalRequirements;

    public IReadOnlyList<UpgradeData> Rewards => rewards;

    public IReadOnlyList<UpgradeData> OptionalRewards =>
        optionalRewards;

    public string LocalizedName
    {
        get
        {
            if (string.IsNullOrEmpty(langKey))
                return displayName;

            return Lang.GetOr(
                "synergy." + langKey + ".name",
                displayName
            );
        }
    }

    public string LocalizedDescription
    {
        get
        {
            if (string.IsNullOrEmpty(langKey))
                return description;

            return Lang.GetOr(
                "synergy." + langKey + ".desc",
                description
            );
        }
    }

    public string ShortName
    {
        get
        {
            if (string.IsNullOrEmpty(langKey))
                return displayName;

            return Lang.GetOr(
                "synergy." + langKey + ".short",
                displayName
            );
        }
    }

    public bool IsAvailable(RunBuildState build)
    {
        if (unlockFamily == UpgradeFamily.None)
            return true;

        return build != null && build.HasAnyFlag(unlockFamily);
    }

    public bool IsSatisfied(RunBuildState build)
    {
        if (requirements == null || requirements.Count == 0)
            return false;

        return CountSatisfied(build) == requirements.Count;
    }

    public int CountSatisfied(RunBuildState build)
    {
        if (requirements == null)
            return 0;

        int satisfied = 0;

        for (int i = 0; i < requirements.Count; i++)
        {
            if (requirements[i] != null &&
                requirements[i].IsSatisfied(build))
            {
                satisfied++;
            }
        }

        return satisfied;
    }

    public void GetProgress(
        RunBuildState build,
        out int current,
        out int required)
    {
        current = 0;
        required = 0;

        if (requirements == null)
            return;

        for (int i = 0; i < requirements.Count; i++)
        {
            SynergyRequirement req = requirements[i];

            if (req == null)
                continue;

            current += req.GetCurrent(build);
            required += req.GetRequired();
        }
    }

    public bool HasAllOptional(RunBuildState build)
    {
        if (optionalRequirements == null ||
            optionalRequirements.Count == 0)
        {
            return true;
        }

        for (int i = 0; i < optionalRequirements.Count; i++)
        {
            if (optionalRequirements[i] == null)
                continue;

            if (!optionalRequirements[i].IsSatisfied(build))
                return false;
        }

        return true;
    }

    public bool Involves(
        UpgradeData upgrade,
        UpgradeFamily families)
    {
        if (upgrade == null)
            return false;

        if (InvolvesList(requirements, upgrade, families))
            return true;

        return InvolvesList(
            optionalRequirements,
            upgrade,
            families
        );
    }

    private static bool InvolvesList(
        List<SynergyRequirement> list,
        UpgradeData upgrade,
        UpgradeFamily families)
    {
        if (list == null)
            return false;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] != null &&
                list[i].Involves(upgrade, families))
            {
                return true;
            }
        }

        return false;
    }
}
