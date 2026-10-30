using System;

// Pure rules shared by runtime and regression checks. Existing mastery saves are untouched.
public static class JourneyRules
{
    public static int Rank(int mapsWon, int trials, int cleanBosses, int bestCleared)
    {
        if (mapsWon >= 3 && trials >= 8 && cleanBosses >= 5 && bestCleared >= 70) return 4;
        if (mapsWon >= 2 && trials >= 5 && cleanBosses >= 3) return 3;
        if (mapsWon >= 1 && trials >= 3) return 2;
        return bestCleared >= 10 && trials >= 1 ? 1 : 0;
    }

    public static int TrialTarget(string id)
    {
        if (id == "Hunter Rifle" || id == "Plasma Rifle" || id == "Void Shotgun") return 3;
        if (id == "Double Shotgun" || id == "Twin SMG") return 10;
        if (id == "Lightning SMG") return 25;
        if (id == "Inferno Shotgun") return 30;
        if (id == "Golden Rifle" || id == "Assassin SMG") return 40;
        if (id == "Minigun") return 250;
        return 100;
    }

    public static bool UsesPiercingTrial(string id) => id == "Hunter Rifle" || id == "Plasma Rifle" || id == "Void Shotgun";
    public static bool UsesPairTrial(string id) => id == "Double Shotgun" || id == "Twin SMG";
    public static bool NeedsEndlessChoice(int nextWave, int selectedThrough) =>
        nextWave > 50 && (nextWave - 1) % 10 == 0 && selectedThrough < nextWave;
    public static int SegmentEnd(int firstWave) => ((Math.Max(firstWave, 1) - 1) / 10 + 1) * 10;
}
