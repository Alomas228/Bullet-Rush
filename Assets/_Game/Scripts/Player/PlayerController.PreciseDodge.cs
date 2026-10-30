using System.Collections.Generic;
using UnityEngine;

public partial class PlayerController
{
    private bool preciseDodgeAwarded;
    private readonly HashSet<int> dodgedAttacks = new HashSet<int>();
    private readonly Queue<int> recentDodges = new Queue<int>();

    // Only called by damage that actually intersected the player's dash.
    public void RegisterPreciseDodge(int attackId)
    {
        if (!IsDashing || attackId == 0 || preciseDodgeAwarded ||
            GameModeManager.IsPvp || !dodgedAttacks.Add(attackId)) return;
        preciseDodgeAwarded = true;
        recentDodges.Enqueue(attackId);
        if (recentDodges.Count > 128) dodgedAttacks.Remove(recentDodges.Dequeue());
        FindAnyObjectByType<RunMetrics>()?.RecordDashDodged();
        if (bonusSettings == null) bonusSettings = BonusSettings.Instance;
        if (bonusSettings != null)
            ScoreManager.Instance?.AddBonus(bonusSettings.dashDodgeBonus, Lang.Get("bonus.dash_dodge"));
        GameplayJourney.Instance?.RecordDodge();
        VfxFactory.TrySpawnExplosion(transform.position, 0.65f, new Color(0.2f, 0.85f, 1f));
    }
}
