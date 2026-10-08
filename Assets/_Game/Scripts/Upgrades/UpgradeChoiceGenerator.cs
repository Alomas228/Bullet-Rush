using System.Collections.Generic;
using UnityEngine;

public enum ChoiceRole
{
    BuildContinuation,
    SynergyCrossBuild,
    WildCard
}

[System.Serializable]
public class SmartChoiceSettings
{
    [Min(1f)]
    public float continuationAffinity = 1.9f;

    [Min(1f)]
    public float continuationSynergyBoost = 1.8f;

    [Min(1f)]
    public float crossBuildAffinity = 1.7f;

    [Min(1f)]
    public float crossSynergyBoost = 2.0f;

    [Min(0f)]
    public float wildFamiliarFactor = 0.7f;

    [Min(0f)]
    public float wildFreshFactor = 1.7f;

    [Min(0f)]
    public float wildRareFactor = 2.0f;

    [Min(0f)]
    public float wildSupportFactor = 1.35f;

    [Min(1f)]
    public float synergyAlmostDoneWeight = 2.6f;

    [Min(1f)]
    public float synergyInProgressWeight = 1.7f;

    [Min(1f)]
    public float synergySeedWeight = 1.25f;

    [Min(0f)]
    public float recentPickPenalty = 0.35f;

    [Min(0f)]
    public float nearMaxStacksPenalty = 0.8f;

    [Min(0f)]
    public float familyOverCapDecay = 0.6f;

    [Min(0)]
    public int familySoftCap = 4;

    [Min(0f)]
    public float sameRoundFamilyPenalty = 0.5f;

    [Min(0f)]
    public float coreBonus = 1.12f;
}

public static class UpgradeChoiceGenerator
{
    public static void Fill(
        UpgradeManager manager,
        List<UpgradeData> pool,
        int slotCount,
        List<UpgradeData> results)
    {
        if (manager == null ||
            pool == null ||
            pool.Count == 0 ||
            slotCount <= 0 ||
            results == null)
        {
            return;
        }

        manager.EnsureState();

        SmartChoiceSettings settings =
            manager.SmartChoices ?? new SmartChoiceSettings();

        RunBuildState build = manager.BuildState;

        IReadOnlyList<SynergyProgress> synergies =
            manager.ActiveSynergies;

        int count = Mathf.Min(slotCount, pool.Count);

        var usedFamilies = new HashSet<UpgradeFamily>();

        for (int i = 0; i < count; i++)
        {
            ChoiceRole role = ResolveRole(i, count);

            bool onlyFresh =
                role == ChoiceRole.WildCard &&
                HasFresh(pool, manager);

            UpgradeData selected = Pick(
                manager,
                pool,
                role,
                usedFamilies,
                onlyFresh,
                settings,
                build,
                synergies
            );

            if (selected == null)
                break;

            results.Add(selected);
            pool.Remove(selected);

            MarkFamilies(usedFamilies, selected);
        }
    }

    private static ChoiceRole ResolveRole(int index, int count)
    {
        if (index >= count - 1)
            return ChoiceRole.WildCard;

        if (index == 0)
            return ChoiceRole.BuildContinuation;

        return ChoiceRole.SynergyCrossBuild;
    }

    private static UpgradeData Pick(
        UpgradeManager manager,
        List<UpgradeData> pool,
        ChoiceRole role,
        HashSet<UpgradeFamily> usedFamilies,
        bool onlyFresh,
        SmartChoiceSettings settings,
        RunBuildState build,
        IReadOnlyList<SynergyProgress> synergies)
    {
        if (pool.Count == 0)
            return null;

        var weights = new float[pool.Count];

        float totalWeight = 0f;

        for (int i = 0; i < pool.Count; i++)
        {
            weights[i] = GetWeight(
                manager,
                pool,
                pool[i],
                role,
                usedFamilies,
                onlyFresh,
                settings,
                build,
                synergies
            );

            totalWeight += weights[i];
        }

        if (totalWeight <= 0f)
            return pool[Random.Range(0, pool.Count)];

        float roll = Random.Range(0f, totalWeight);

        for (int i = 0; i < pool.Count; i++)
        {
            roll -= weights[i];

            if (roll < 0f)
                return pool[i];
        }

        return pool[pool.Count - 1];
    }

    private static float GetWeight(
        UpgradeManager manager,
        List<UpgradeData> pool,
        UpgradeData upgrade,
        ChoiceRole role,
        HashSet<UpgradeFamily> usedFamilies,
        bool onlyFresh,
        SmartChoiceSettings settings,
        RunBuildState build,
        IReadOnlyList<SynergyProgress> synergies)
    {
        if (upgrade == null)
            return 0f;

        int stacks = build.GetStacks(upgrade);

        if (onlyFresh && stacks > 0)
            return 0f;

        UpgradeFamily families = upgrade.Families;

        if (role != ChoiceRole.WildCard &&
            stacks == 0 &&
            IsLastFresh(pool, manager, upgrade))
        {
            return 0f;
        }

        float weight = manager.GetRarityWeight(upgrade.Rarity);

        if (stacks > 0)
            weight *= manager.TakenAffinity;

        if (upgrade == manager.LastTaken)
            weight *= manager.RepeatPenalty;

        if (build.IsRecent(upgrade))
            weight *= settings.recentPickPenalty;

        if (upgrade.MaxStacks > 1 &&
            stacks >= upgrade.MaxStacks - 1)
        {
            weight *= settings.nearMaxStacksPenalty;
        }

        if (build.AnyFamilyAbove(
                families,
                settings.familySoftCap))
        {
            weight *= settings.familyOverCapDecay;
        }

        if (UpgradeFamilyUtility.Intersects(
                families,
                UpgradeFamily.Core))
        {
            weight *= settings.coreBonus;
        }

        if (UsedAnyFamily(families, usedFamilies))
            weight *= settings.sameRoundFamilyPenalty;

        weight *= GetRoleMultiplier(
            role,
            upgrade,
            stacks,
            families,
            build,
            synergies,
            settings
        );

        weight *= GetSynergyPull(
            upgrade,
            families,
            build,
            synergies,
            settings
        );

        return weight;
    }

    private static float GetRoleMultiplier(
        ChoiceRole role,
        UpgradeData upgrade,
        int stacks,
        UpgradeFamily families,
        RunBuildState build,
        IReadOnlyList<SynergyProgress> synergies,
        SmartChoiceSettings settings)
    {
        bool advancesSynergy = AdvancesUnfinishedSynergy(
            upgrade,
            families,
            synergies
        );

        switch (role)
        {
            case ChoiceRole.BuildContinuation:
            {
                float multiplier = 1f;

                if (stacks > 0)
                    multiplier *= settings.continuationAffinity;

                if (advancesSynergy)
                {
                    multiplier *=
                        settings.continuationSynergyBoost;
                }

                return multiplier;
            }

            case ChoiceRole.SynergyCrossBuild:
            {
                float multiplier = 1f;

                if (build.HasAnyFamily(families))
                    multiplier *= settings.crossBuildAffinity;

                if (advancesSynergy)
                    multiplier *= settings.crossSynergyBoost;

                return multiplier;
            }

            case ChoiceRole.WildCard:
            {
                float multiplier = 1f;

                if (stacks == 0)
                    multiplier *= settings.wildFreshFactor;

                if (upgrade.Rarity >= Rarity.Rare)
                    multiplier *= settings.wildRareFactor;

                if (IsSupportFamily(families))
                    multiplier *= settings.wildSupportFactor;

                if (build.HasAnyFamily(families))
                    multiplier *= settings.wildFamiliarFactor;

                return multiplier;
            }

            default:
                return 1f;
        }
    }

    private static float GetSynergyPull(
        UpgradeData upgrade,
        UpgradeFamily families,
        RunBuildState build,
        IReadOnlyList<SynergyProgress> synergies,
        SmartChoiceSettings settings)
    {
        if (synergies == null || synergies.Count == 0)
            return 1f;

        RunBuildState hypo = null;

        float pull = 1f;

        for (int i = 0; i < synergies.Count; i++)
        {
            SynergyProgress progress = synergies[i];

            if (progress == null ||
                progress.Synergy == null ||
                progress.IsComplete)
            {
                continue;
            }

            if (!progress.Synergy.Involves(upgrade, families))
                continue;

            hypo ??= build.WithExtraStack(upgrade);

            float candidate;

            if (progress.Synergy.IsSatisfied(hypo))
                candidate = settings.synergyAlmostDoneWeight;
            else if (progress.Current > 0)
                candidate = settings.synergyInProgressWeight;
            else
                candidate = settings.synergySeedWeight;

            if (candidate > pull)
                pull = candidate;
        }

        return pull;
    }

    private static bool AdvancesUnfinishedSynergy(
        UpgradeData upgrade,
        UpgradeFamily families,
        IReadOnlyList<SynergyProgress> synergies)
    {
        if (synergies == null)
            return false;

        for (int i = 0; i < synergies.Count; i++)
        {
            SynergyProgress progress = synergies[i];

            if (progress == null ||
                progress.Synergy == null ||
                progress.IsComplete)
            {
                continue;
            }

            if (progress.Synergy.Involves(upgrade, families))
                return true;
        }

        return false;
    }

    private static bool HasFresh(
        List<UpgradeData> pool,
        UpgradeManager manager)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (manager.GetStacks(pool[i]) == 0)
                return true;
        }

        return false;
    }

    private static bool IsLastFresh(
        List<UpgradeData> pool,
        UpgradeManager manager,
        UpgradeData candidate)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            UpgradeData upgrade = pool[i];

            if (upgrade == candidate)
                continue;

            if (manager.GetStacks(upgrade) == 0)
                return false;
        }

        return true;
    }

    private static bool IsSupportFamily(UpgradeFamily families)
    {
        const UpgradeFamily support =
            UpgradeFamily.Defensive |
            UpgradeFamily.Ability |
            UpgradeFamily.Utility;

        return (families & support) != 0;
    }

    private static bool UsedAnyFamily(
        UpgradeFamily families,
        HashSet<UpgradeFamily> used)
    {
        if (used == null || used.Count == 0)
            return false;

        for (int i = 0; i < UpgradeFamilyUtility.All.Length; i++)
        {
            UpgradeFamily flag = UpgradeFamilyUtility.All[i];

            if ((families & flag) != 0 && used.Contains(flag))
                return true;
        }

        return false;
    }

    private static void MarkFamilies(
        HashSet<UpgradeFamily> used,
        UpgradeData upgrade)
    {
        if (upgrade == null)
            return;

        UpgradeFamily families = upgrade.Families;

        for (int i = 0; i < UpgradeFamilyUtility.All.Length; i++)
        {
            UpgradeFamily flag = UpgradeFamilyUtility.All[i];

            if ((families & flag) != 0)
                used.Add(flag);
        }
    }
}
