using UnityEngine;

public partial class Enemy
{
    private float wetUntil;
    private float arenaSlowUntil;
    private float iceHealth;
    private float initialIceHealth;
    private EnemyMapStatusVisual mapStatusVisual;

    // A lethal bullet may still trigger its lightning from this wet origin.
    public bool IsWet => Time.time < wetUntil;
    public float IceHealth => iceHealth;
    public float IceFraction => initialIceHealth > 0f ? iceHealth / initialIceHealth : 0f;
    private float ArenaSlowScale => Time.time < arenaSlowUntil ? .62f : 1f;

    private void ResetMapStatuses()
    {
        wetUntil = 0f;
        arenaSlowUntil = 0f;
        iceHealth = 0f;
        initialIceHealth = 0f;
    }

    private void OnDisable() => ResetMapStatuses();

    private void InitializeMapStatuses()
    {
        // All non-boss types participate. Ice is a separate shield on top of health.
        if (MapMechanicManager.IceCoatingEnabled &&
            MapCombatRules.ShouldHaveIce(MapMechanicManager.CurrentMechanic,
                cachedEnemyType == EnemyType.Boss, Random.value))
        {
            iceHealth = scaledMaxHealth * MapCombatRules.IceHealthFraction;
            initialIceHealth = iceHealth;
            EnsureMapStatusVisual();
        }
    }

    public void ApplyWet()
    {
        if (IsDead) return;
        wetUntil = Time.time + MapCombatRules.WetDuration;
        EnsureMapStatusVisual();
    }

    public void ApplyArenaSlow(float seconds)
    {
        if (IsDead || isBoss || seconds <= 0f) return;
        arenaSlowUntil = Mathf.Max(arenaSlowUntil, Time.time + seconds);
        ApplyWet();
    }

    private void EnsureMapStatusVisual()
    {
        if (mapStatusVisual == null)
            mapStatusVisual = gameObject.AddComponent<EnemyMapStatusVisual>();
        mapStatusVisual.Bind(this);
    }
}
