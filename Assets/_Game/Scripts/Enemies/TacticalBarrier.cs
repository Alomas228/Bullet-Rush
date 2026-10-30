using UnityEngine;

public sealed class TacticalBarrier : MonoBehaviour
{
    private static TacticalBarrier current;
    public static bool Exists => current != null;
    private Enemy owner;
    private Vector3 direction;
    private LineRenderer line;
    private float elapsed, damage;
    private int attackId;
    private bool hit;
    public static void Spawn(Enemy owner, Vector3 center, Vector3 direction, float damage)
    {
        if (Exists) return;
        var barrier = new GameObject("TacticalBarrier").AddComponent<TacticalBarrier>(); current = barrier;
        barrier.owner = owner; barrier.transform.position = center; barrier.direction = direction;
        barrier.damage = damage; barrier.attackId = DamageEventId.Next();
        barrier.line = JourneyVisuals.Line(barrier.transform, "Warning", new Color(1, 0.55f, 0.1f), 0.1f);
        barrier.line.positionCount = 2; barrier.line.SetPosition(0, -direction * 3.5f + Vector3.up * 0.1f);
        barrier.line.SetPosition(1, direction * 3.5f + Vector3.up * 0.1f);
    }
    private void Update()
    {
        if (owner == null || owner.IsDead || GameStateManager.Instance == null || GameStateManager.Instance.CurrentState != GameState.Playing)
        { Destroy(gameObject); return; }
        elapsed += Time.deltaTime;
        if (elapsed < 1.6f) { line.widthMultiplier = 0.08f + Mathf.Abs(Mathf.Sin(elapsed * 12)) * 0.08f; return; }
        line.widthMultiplier = 0.8f;
        var health = PlayerHealth.Instance;
        if (!hit && health != null)
        {
            var delta = health.transform.position - transform.position; delta.y = 0;
            float along = Vector3.Dot(delta, direction);
            if (Mathf.Abs(along) <= 3.5f && (delta - direction * along).sqrMagnitude <= 0.65f * 0.65f)
            { hit = true; health.TakeDamage(damage, attackId); }
        }
        if (elapsed >= 3.2f) Destroy(gameObject);
    }
    private void OnDestroy() { if (current == this) current = null; }
}
