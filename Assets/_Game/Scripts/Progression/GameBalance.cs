using System;

/// <summary>Shared economy rules. All values refer to active PvE play, without ads.</summary>
public static class GameBalance
{
    public const int MaxPlayerLevel = 50;
    public const int BossInterval = 10;
    public const int XpPerClearedWave = 28;
    public const int XpPerClearedBoss = 75;
    public const int BossCoinBonus = 30;
    public const int PerfectCoinBonus = 3;
    public const float CoinDropChance = 0.24f;
    public const float LargeCoinChance = 0.35f;
    public const int BossLootCoins = 12;
    public const float HealthDropChance = 0.09f;
    public const float HealthDropFraction = 0.16f;
    public const float MaxLifestealFraction = 0.04f;
    public const float LifestealHealthPerSecondFraction = 0.05f;

    private static readonly int[] UpgradeCosts = { 25, 35, 50, 70, 95, 125, 160, 205, 255, 320 };

    public static int LevelXp(int level)
    {
        int n = Math.Max(0, Math.Min(level, MaxPlayerLevel) - 1);
        return 120 * n + 12 * n * n;
    }

    public static int UpgradeCost(int purchasedLevels) =>
        purchasedLevels >= UpgradeCosts.Length ? 0 : UpgradeCosts[Math.Max(0, purchasedLevels)];

    public static int RunXp(int clearedWaves)
    {
        int waves = Math.Max(0, clearedWaves);
        return waves * XpPerClearedWave + waves / BossInterval * XpPerClearedBoss;
    }

    public static int WaveCoins(int wave, bool perfect)
    {
        if (wave <= 0) return 0;
        return 7 + Math.Min(12, (wave - 1) / 3) +
            (wave % BossInterval == 0 ? BossCoinBonus : 0) +
            (perfect ? PerfectCoinBonus : 0);
    }
}
