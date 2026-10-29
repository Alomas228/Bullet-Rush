using UnityEngine;

public partial class Enemy
{
    private float wetUntil;
    private float iceHealth;
    private float initialIceHealth;
    private EnemyMapStatusVisual mapStatusVisual;

    // A lethal bullet may still trigger its lightning from this wet origin.
    public bool IsWet => Time.time < wetUntil;
    public float IceHealth => iceHealth;
    public float IceFraction => initialIceHealth > 0f ? iceHealth / initialIceHealth : 0f;

    private void ResetMapStatuses()
    {
        wetUntil = 0f;
        iceHealth = 0f;
        initialIceHealth = 0f;
    }

    private void OnDisable() => ResetMapStatuses();

    private void InitializeMapStatuses()
    {
        // All non-boss types participate, not just elites. Health is not added:
        // the existing total health includes this destructible outer portion.
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

    private void EnsureMapStatusVisual()
    {
        if (mapStatusVisual == null)
            mapStatusVisual = gameObject.AddComponent<EnemyMapStatusVisual>();
        mapStatusVisual.Bind(this);
    }
}
