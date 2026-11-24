using UnityEngine;

/// <summary>Fictional arcade scenery: converts player hits into a local enemy-only area effect.</summary>
public sealed class ArenaBreakable : MonoBehaviour
{
    public enum Kind { Explosive, Coolant, Supply }
    private float durability = 80f;
    private bool broken;
    private Kind kind;
    private bool healthReward;
    private int coinReward;

    public void Initialize(Kind type, bool heal, int coins)
    {
        kind = type;
        healthReward = heal;
        coinReward = coins;
        durability = kind == Kind.Supply ? 45f : kind == Kind.Coolant ? 60f : 80f;
    }

    public void Hit(float amount)
    {
        if (broken || amount <= 0f || GameModeManager.IsPvp ||
            GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing) return;
        durability -= amount;
        if (durability > 0f) return;
        broken = true;
        Vector3 center = transform.position + Vector3.up * .55f;
        gameObject.SetActive(false);
        if (kind == Kind.Supply)
        {
            VfxFactory.TrySpawnExplosion(center, .65f, new Color(1f, .77f, .35f));
            if (healthReward) LootPickup.SpawnHealth(center, .16f);
            else LootPickup.SpawnCoin(center, Mathf.Max(coinReward, 1));
            Destroy(gameObject);
            return;
        }

        float radius = kind == Kind.Coolant ? 4.2f : 3.2f;
        VfxFactory.TrySpawnExplosion(center, radius, kind == Kind.Coolant ? new Color(.36f, .78f, .93f) : new Color(1f,.52f,.18f));
        Enemy[] enemies = Enemy.SnapshotAlive();
        int eventId = DamageEventId.Next();
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.IsDead) continue;
            Vector3 delta = enemy.transform.position - center; delta.y = 0f;
            if (delta.sqrMagnitude > radius * radius) continue;
            if (kind == Kind.Coolant) enemy.ApplyArenaSlow(3.5f);
            else enemy.TakeDamage(enemy.MaxHealth * .4f + 65f, false, DamageSource.Explosion, eventId, secondary: true);
        }
        Destroy(gameObject);
    }
}
