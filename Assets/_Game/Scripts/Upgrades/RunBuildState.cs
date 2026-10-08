using System.Collections.Generic;

public class RunBuildState
{
    private readonly Dictionary<UpgradeData, int> stacks;

    private readonly Dictionary<UpgradeFamily, int> familyStacks;

    private readonly Dictionary<UpgradeFamily, int> familyUpgrades;

    private readonly List<UpgradeData> recentPicks;

    private RunBuildState(
        Dictionary<UpgradeData, int> stacks,
        List<UpgradeData> recentPicks)
    {
        this.stacks = stacks;
        this.recentPicks = recentPicks;

        familyStacks = new Dictionary<UpgradeFamily, int>();
        familyUpgrades = new Dictionary<UpgradeFamily, int>();

        foreach (KeyValuePair<UpgradeData, int> pair in stacks)
        {
            UpgradeData upgrade = pair.Key;
            int count = pair.Value;

            if (upgrade == null || count <= 0)
                continue;

            UpgradeFamily families = upgrade.Families;

            for (int i = 0; i < UpgradeFamilyUtility.All.Length; i++)
            {
                UpgradeFamily flag = UpgradeFamilyUtility.All[i];

                if ((families & flag) == 0)
                    continue;

                familyStacks.TryGetValue(flag, out int familyCount);
                familyStacks[flag] = familyCount + count;

                familyUpgrades.TryGetValue(flag, out int upgradeCount);
                familyUpgrades[flag] = upgradeCount + 1;
            }
        }
    }

    public static RunBuildState Build(
        IReadOnlyDictionary<UpgradeData, int> stacks,
        IReadOnlyList<UpgradeData> recentPicks)
    {
        var stackCopy = new Dictionary<UpgradeData, int>();

        if (stacks != null)
        {
            foreach (KeyValuePair<UpgradeData, int> pair in stacks)
            {
                if (pair.Key != null && pair.Value > 0)
                    stackCopy[pair.Key] = pair.Value;
            }
        }

        return new RunBuildState(
            stackCopy,
            CopyRecent(recentPicks)
        );
    }

    public RunBuildState WithExtraStack(UpgradeData upgrade)
    {
        if (upgrade == null)
            return this;

        var next = new Dictionary<UpgradeData, int>(stacks);

        next.TryGetValue(upgrade, out int current);
        next[upgrade] = current + 1;

        return new RunBuildState(
            next,
            new List<UpgradeData>(recentPicks)
        );
    }

    public int GetStacks(UpgradeData upgrade)
    {
        if (upgrade == null)
            return 0;

        return stacks.TryGetValue(upgrade, out int count)
            ? count
            : 0;
    }

    public int GetFamilyStacks(UpgradeFamily family)
    {
        return familyStacks.TryGetValue(family, out int count)
            ? count
            : 0;
    }

    public int GetDistinctUpgrades(UpgradeFamily family)
    {
        return familyUpgrades.TryGetValue(family, out int count)
            ? count
            : 0;
    }

    public bool HasFamily(UpgradeFamily family) =>
        GetFamilyStacks(family) > 0;

    public bool HasAnyFamily(UpgradeFamily families)
    {
        if (families == UpgradeFamily.None)
            return false;

        for (int i = 0; i < UpgradeFamilyUtility.All.Length; i++)
        {
            UpgradeFamily flag = UpgradeFamilyUtility.All[i];

            if ((families & flag) == 0)
                continue;

            if (flag != UpgradeFamily.Core && HasFamily(flag))
                return true;
        }

        return false;
    }

    public bool AnyFamilyAbove(
        UpgradeFamily families,
        int threshold)
    {
        if (families == UpgradeFamily.None)
            return false;

        for (int i = 0; i < UpgradeFamilyUtility.All.Length; i++)
        {
            UpgradeFamily flag = UpgradeFamilyUtility.All[i];

            if ((families & flag) == 0)
                continue;

            if (GetFamilyStacks(flag) > threshold)
                return true;
        }

        return false;
    }

    public int GetDistinctFamilyCount()
    {
        int count = 0;

        for (int i = 0; i < UpgradeFamilyUtility.All.Length; i++)
        {
            if (HasFamily(UpgradeFamilyUtility.All[i]))
                count++;
        }

        return count;
    }

    public bool IsRecent(UpgradeData upgrade)
    {
        if (upgrade == null)
            return false;

        return recentPicks.Contains(upgrade);
    }

    public IReadOnlyList<UpgradeData> RecentPicks =>
        recentPicks;

    private static List<UpgradeData> CopyRecent(
        IReadOnlyList<UpgradeData> recentPicks)
    {
        var copy = new List<UpgradeData>();

        if (recentPicks == null)
            return copy;

        for (int i = 0; i < recentPicks.Count; i++)
        {
            if (recentPicks[i] != null)
                copy.Add(recentPicks[i]);
        }

        return copy;
    }
}
