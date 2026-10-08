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
        DistinctFamilies
    }

    [System.Serializable]
    public class SynergyRequirement
    {
        [SerializeField] private RequirementKind kind =
            RequirementKind.Upgrade;

        [SerializeField] private UpgradeData upgrade;

        [SerializeField]
        private UpgradeFamily family = UpgradeFamily.None;

        [SerializeField] private int count = 1;

        [SerializeField] private int target = 1;

        public RequirementKind Kind => kind;

        public UpgradeData Upgrade => upgrade;

        public UpgradeFamily Family => family;

        public int Count => count > 0 ? count : 1;

        public int Target => target > 0 ? target : 1;

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
                    return build.GetDistinctFamilyCount() >= Count;

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
                    return families != UpgradeFamily.None;

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

    [Header("Requirements")]
    [SerializeField]
    private List<SynergyRequirement> requirements =
        new List<SynergyRequirement>();

    [Header("Rewards")]
    [SerializeField]
    private List<UpgradeData> rewards =
        new List<UpgradeData>();

    public SynergyKind Kind => kind;

    public Rarity Rarity => rarity;

    public IReadOnlyList<SynergyRequirement> Requirements =>
        requirements;

    public IReadOnlyList<UpgradeData> Rewards => rewards;

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

    public bool Involves(
        UpgradeData upgrade,
        UpgradeFamily families)
    {
        if (requirements == null || upgrade == null)
            return false;

        for (int i = 0; i < requirements.Count; i++)
        {
            if (requirements[i] != null &&
                requirements[i].Involves(upgrade, families))
            {
                return true;
            }
        }

        return false;
    }
}
