using UnityEngine;

/// <summary>
/// A run is disposable gameplay state, not a disposable scene. Only combat
/// values and transient entities are reset here; progression/save managers,
/// the menu, equipment selection and scene objects are deliberately retained.
/// </summary>
internal static class RunSessionResetter
{
    public static void ReturnToMenu()
    {
        WorldFadeOutManager.ClearRunVisualsForMenu();
        PlayerController player = FindPlayer();
        if (player != null)
        {
            ResetPlayer(player, false);
            player.PrepareMenuPreview();
        }
        ResetRunCounters();
        PvpCombatant.SetPlayerDamageMultiplier(1f);
    }

    public static bool PrepareForGameplay()
    {
        PlayerController player = FindPlayer();
        if (player == null)
        {
            Debug.LogError("[RunSession] Cannot start without a player in the scene.");
            return false;
        }

        if (!player.gameObject.activeSelf) player.PrepareMenuPreview();
        ResetRunCounters();
        PvpCombatant.SetPlayerDamageMultiplier(1f);
        // Repeat the base reset on Play so purchases and equipment chosen while
        // the arena was rebuilding are reflected in this run, exactly once.
        ResetPlayer(player, true);
        return true;
    }

    private static PlayerController FindPlayer() =>
        Object.FindAnyObjectByType<PlayerController>(FindObjectsInactive.Include);

    private static void ResetPlayer(PlayerController player, bool startingRun)
    {
        player.GetComponent<PlayerStats>()?.ResetForSession();
        player.GetComponent<PlayerHealth>()?.ResetForSession();
        player.ResetForSession();
        player.GetComponentInChildren<RunUpgrades>(true)?.ResetAll();
        player.GetComponent<AbilityManager>()?.ResetForSession();
        player.GetComponent<PermanentUpgradeApplier>()?.ResetForSession();
        player.GetComponentInChildren<Weapon>(true)?.ResetForSession(startingRun);
        player.GetComponent<PlayerEquipmentApplier>()?.ResetForSession(startingRun);
    }

    private static void ResetRunCounters()
    {
        RunBuildMechanics.Reset();
        ScoreManager.Instance?.ResetRunStats();
        Object.FindAnyObjectByType<ComboSystem>(FindObjectsInactive.Include)?.ResetForSession();
        Object.FindAnyObjectByType<RunMetrics>(FindObjectsInactive.Include)?.ResetAll();
    }
}
