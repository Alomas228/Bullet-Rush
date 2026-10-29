using UnityEngine;

public static class BossCampaign
{
    public const int BossCount = 5;
    public const int Interval = 10;
    public const int FinalWave = BossCount * Interval;
    public static bool IsBossWave(int wave, bool endless = false) =>
        wave > 0 && (endless || wave <= FinalWave) && wave % Interval == 0;
    public static int IndexForWave(int wave, bool endless = false) =>
        endless && wave > FinalWave ? (wave / Interval - 1) % BossCount
            : Mathf.Clamp(wave / Interval - 1, 0, BossCount - 1);
    public static bool CanAdvance(int wave, bool endless) => wave < FinalWave || endless;
    public static bool IsComplete(int wave, int defeated) => wave >= FinalWave && defeated >= BossCount;
    public static bool ShouldOfferContinuation(int wave, int defeated, bool alreadyCompleted) =>
        !alreadyCompleted && IsComplete(wave, defeated);

    public static float Progress(int wave, int defeated, float bossHealthFraction = 1f)
    {
        float value = Mathf.Max(0f, wave - 1f);
        if (IsBossWave(wave))
            value = defeated > IndexForWave(wave) ? wave : value + 1f - Mathf.Clamp01(bossHealthFraction);
        return Mathf.Clamp(value, 0f, FinalWave);
    }
}
