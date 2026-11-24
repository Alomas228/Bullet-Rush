using UnityEngine;

// Achievement and mission systems may finish on the same kill.
internal static class RunNoticeSlot
{
    private static MonoBehaviour owner;

    public static bool TryTake(MonoBehaviour requester)
    {
        if (owner != null && owner != requester && owner.isActiveAndEnabled) return false;
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Playing &&
            Time.timeScale == 0f) return false;
        owner = requester;
        return true;
    }

    public static void Release(MonoBehaviour requester)
    {
        if (owner == requester) owner = null;
    }
}
