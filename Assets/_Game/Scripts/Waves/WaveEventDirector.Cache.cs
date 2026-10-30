using System.Collections;
using UnityEngine;

public partial class WaveEventDirector
{
    private int cacheSegment = -1;
    private IEnumerator OfferProtectedCache(int wave)
    {
        // Reserve the event slot before this delay, allowing existing map attacks to expire.
        yield return new WaitForSeconds(4f);
        var player = PlayerHealth.Instance;
        if (!waveRunning || player == null || !TryFindZoneCenter(player.transform.position, Random.Range(0, 360), 5, 8, out Vector3 position))
        { IsEventActive = false; yield break; }
        cacheSegment = (wave - 1) / 10;
        eventFiredThisWave = true; lastEventTime = Time.time;
        activeObjective = CreateObjectiveMarker("ProtectedCache", position, new Color(1, 0.75f, 0.2f), 0.8f);
        activeObjective.AddComponent<ProtectedCache>().Initialize(enemySpawner, () =>
        { activeObjective = null; IsEventActive = eliteHuntActive; });
    }
}
