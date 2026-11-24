using UnityEngine;

// Body effects, tactical headpieces and side pins occupy different parts of the silhouette.
public sealed class EnemyMapStatusVisual : CombatMarkSource
{
    private Enemy enemy;
    public void Bind(Enemy owner)
    {
        enemy = owner;
        enabled = true;
    }

    internal override void Draw(CombatMarkBatch batch)
    {
        if (enemy == null || enemy.IsDead) { enabled = false; return; }
        bool commands = enemy.IsUnderCommand;
        if (!enemy.IsWet && enemy.IceHealth <= 0 && enemy.Tactic == Enemy.TacticalRole.None &&
            !enemy.ShowsDirectionalShield && !commands && !enemy.IsCacheGuard)
        {
            enabled = false;
            return;
        }
        // Use exactly the transform that steers and squashes the enemy body.
        // Turning, charging and being knocked back move the equipment with the cube.
        Matrix4x4 body = transform.localToWorldMatrix;
        if (enemy.IsWet)
            batch.Stamp(CombatMarkShape.Wet, body);
        if (enemy.IceHealth > 0)
        {
            var stage = enemy.IceFraction > .66f ? CombatMarkShape.IceIntact :
                enemy.IceFraction > .33f ? CombatMarkShape.IceCracked : CombatMarkShape.IceChipped;
            batch.Stamp(stage, body);
        }
        if (enemy.ShowsDirectionalShield)
            batch.Stamp(CombatMarkShape.Shield, body);

        float iceLift = .16f * Mathf.Clamp01(enemy.IceFraction * 2f);
        Matrix4x4 head = iceLift > 0 ? body * Matrix4x4.Translate(Vector3.up * iceLift) : body;
        if (enemy.Tactic == Enemy.TacticalRole.Coordinator)
        {
            var crown = enemy.IsObjectiveCoordinator ? CombatMarkShape.ObjectiveCoordinator : CombatMarkShape.Coordinator;
            batch.Stamp(crown, head);
        }
        else if (enemy.Tactic == Enemy.TacticalRole.Blocker)
            batch.Stamp(CombatMarkShape.Blocker, head);

        // Pins lie on the shoulder of the cube, with their upper surface visible from the game camera.
        if (commands) Shoulder(batch, head, CombatMarkShape.Commanded, -.31f);
        if (enemy.IsCacheGuard) Shoulder(batch, head, CombatMarkShape.Guard, .32f);
    }

    private static void Shoulder(CombatMarkBatch batch, Matrix4x4 head, CombatMarkShape shape, float x)
    {
        var pin = Matrix4x4.TRS(new Vector3(x,.54f,-.22f), Quaternion.Euler(85,0,0), Vector3.one * .90f);
        batch.Stamp(shape, head * pin);
    }
}
