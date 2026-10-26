using UnityEngine;

public enum GameMode
{
    Pve,
    Pvp
}

public static class GameModeManager
{
    public static GameMode SelectedMode { get; private set; } = GameMode.Pve;

    public static bool IsPvp => SelectedMode == GameMode.Pvp;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        SelectedMode = GameMode.Pve;
    }

    public static void Select(GameMode mode)
    {
        SelectedMode = mode;
    }
}
