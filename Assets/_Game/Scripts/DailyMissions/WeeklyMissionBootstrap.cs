using UnityEngine;

public static class WeeklyMissionBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        WeeklyMissionManager.EnsureExists();
    }
}
