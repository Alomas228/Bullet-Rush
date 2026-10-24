using UnityEngine;

public static class MasteryBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        MasteryManager.EnsureExists();
    }
}
