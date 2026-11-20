using UnityEngine;

public partial class Enemy
{
    public enum TacticalRole { None, Coordinator, Blocker }
    public TacticalRole Tactic { get; private set; }
    private Enemy coordinator;
    private float commandUntil, nextCommand, nextTacticalAttack;
    private LineRenderer[] commandLinks;
    private int lightningCharges;
    private float lightningReadyAt, feedbackReadyAt;
    private bool inheritedBleeding;
    private bool suppressDeathSynergy;
    private int pairedCycle, pairedVolley;
    private bool pairAwarded;
    public void RecordPairedHit(string id, int cycle, int volley)
    {
        if (cycle == 0 || !JourneyRules.UsesPairTrial(id)) return;
        if (pairedCycle != cycle) { pairedCycle = cycle; pairedVolley = volley; pairAwarded = false; return; }
        if (pairAwarded || pairedVolley == volley) return;
        pairAwarded = true; GameplayJourney.Instance?.ProgressTrial(id);
    }
    public bool IsBurningAtDeath { get; private set; }
    private bool HasDirectionalShield => variant == EnemyVariant.Bulwark && cachedEnemyType == EnemyType.Tank;

    private void InitializeTactics()
    {
        Tactic = TacticalRole.None; coordinator = null; commandUntil = 0;
        lightningCharges = 0; lightningReadyAt = feedbackReadyAt = 0; inheritedBleeding = false;
        IsBurningAtDeath = false;
        if (GameModeManager.IsPvp || currentWave < 6 || currentWave % 10 == 0) return;
        int commanders = 0, blockers = 0;
        foreach (var enemy in aliveEnemies)
        {
            if (enemy == this || enemy == null || enemy.IsDead) continue;
            if (enemy.Tactic == TacticalRole.Coordinator) commanders++;
            if (enemy.Tactic == TacticalRole.Blocker) blockers++;
        }
        if (cachedEnemyType == EnemyType.Normal && commanders < 2 && Random.value < 0.12f)
            Tactic = TacticalRole.Coordinator;
        else if (cachedEnemyType == EnemyType.Ranged && currentWave >= 13 && blockers < 1 && Random.value < 0.22f)
            Tactic = TacticalRole.Blocker;
        if (Tactic == TacticalRole.Coordinator)
            AssignCoordinator();
        if (Tactic == TacticalRole.Blocker)
        {
            var marker = JourneyVisuals.Line(transform, "BlockerFork", new Color(1, 0.6f, 0.1f), 0.12f);
            marker.positionCount = 4; marker.SetPositions(new[] { new Vector3(-0.6f, 1.6f, 0), new Vector3(-0.6f, 2, 0), new Vector3(0.6f, 2, 0), new Vector3(0.6f, 1.6f, 0) });
        }
        if (HasDirectionalShield)
        {
            var shield = JourneyVisuals.Line(transform, "DirectionalShield", new Color(0.3f, 0.85f, 1), 0.14f);
            shield.loop = true; shield.positionCount = 4;
            shield.SetPositions(new[] { new Vector3(-0.6f, 0.15f, 0.65f), new Vector3(-0.6f, 1.4f, 0.65f), new Vector3(0.6f, 1.4f, 0.65f), new Vector3(0.6f, 0.15f, 0.65f) });
        }
        nextTacticalAttack = Time.time + 4f;
    }
    internal void AssignCoordinator()
    {
        Tactic = TacticalRole.Coordinator;
        var color = new Color(0.65f, 0.4f, 1);
        JourneyVisuals.Ring(transform, "CoordinatorCrown", color, 0.65f, 1.7f, 6);
        commandLinks = new LineRenderer[4];
        for (int i = 0; i < commandLinks.Length; i++)
        {
            var link = JourneyVisuals.Line(transform, "CommandLink", color, 0.055f);
            link.useWorldSpace = true;
            link.positionCount = 2;
            link.enabled = false;
            commandLinks[i] = link;
        }
    }

    internal void Retreat()
    {
        if (IsDead) return;
        IsDead = true;
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private float CommandSpeedScale => coordinator != null && !coordinator.IsDead && Time.time < commandUntil ? 1.18f : 1f;
    private bool HandleTactics()
    {
        if (Tactic == TacticalRole.None) return false;
        Vector3 delta = player.position - transform.position; delta.y = 0;
        float distance = delta.magnitude;
        if (distance > 9) MoveInDirection(delta.normalized, 0.8f);
        else if (distance < 5) MoveInDirection(-delta.normalized, 0.7f);
        else RotateTowards(delta);
        if (Tactic == TacticalRole.Coordinator && Time.time >= nextCommand)
        {
            nextCommand = Time.time + 0.25f; int link = 0;
            foreach (var ally in aliveEnemies)
            {
                if (link >= commandLinks.Length) break;
                if (ally == null || ally == this || ally.IsDead || ally.isBoss || ally.IsElite || ally.Tactic != TacticalRole.None ||
                    (ally.transform.position - transform.position).sqrMagnitude > 49) continue;
                if (ally.coordinator != null && ally.coordinator != this && !ally.coordinator.IsDead && Time.time < ally.commandUntil) continue;
                ally.coordinator = this; ally.commandUntil = Time.time + 0.4f;
                commandLinks[link].enabled = true;
                commandLinks[link].SetPosition(0, transform.position + Vector3.up);
                commandLinks[link++].SetPosition(1, ally.transform.position + Vector3.up * 0.6f);
            }
            for (; link < commandLinks.Length; link++) commandLinks[link].enabled = false;
        }
        if (Tactic == TacticalRole.Blocker && Time.time >= nextTacticalAttack && distance < 15 && !TacticalBarrier.Exists)
        {
            nextTacticalAttack = Time.time + 7;
            var director = GameplayJourney.Instance != null ? GameplayJourney.Instance.GetComponent<WaveEventDirector>() : null;
            if (director != null && director.IsEventActive) return true;
            Vector3 center = player.position + delta.normalized * 1.2f;
            if (HasEscapeRouteFromPlayer(center, 3.5f))
                TacticalBarrier.Spawn(this, center, Vector3.Cross(Vector3.up, delta).normalized, enemyData.ProjectileDamage * scaledDamageMultiplier * 0.7f);
        }
        return true;
    }
    public float ResolveDirectionalDamage(float amount, Vector3 incoming)
    {
        if (!HasDirectionalShield) return amount;
        incoming.y = 0;
        return Vector3.Dot(transform.forward, -incoming.normalized) > 0.35f ? amount * 0.45f : amount;
    }
    private void JourneyHit(float amount, bool critical, DamageSource source, string weaponId, bool secondary)
    {
        var journey = GameplayJourney.Instance;
        if (journey == null) return;
        if (source == DamageSource.Weapon)
        {
            if (weaponId == "Minigun" || (critical && (weaponId == "Golden Rifle" || weaponId == "Assassin SMG"))) journey.ProgressTrial(weaponId);
            if (Time.time >= feedbackReadyAt && (critical || amount >= 160))
            {
                feedbackReadyAt = Time.time + 0.15f;
                VfxFactory.TrySpawnExplosion(transform.position + Vector3.up * 0.5f, critical ? 0.32f : 0.22f,
                    critical ? new Color(1, 0.8f, 0.2f) : new Color(0.7f, 0.8f, 1));
            }
        }
        if (source != DamageSource.ChainLightning || secondary) return;
        journey.RecordLightning(IsWet, weaponId);
        if (IsDead || !journey.HasSynergy("Synergy_ChainReaction") || Time.time < lightningReadyAt) return;
        if (++lightningCharges < 3) return;
        lightningCharges = 0; lightningReadyAt = Time.time + 2;
        SynergyCombat.Discharge(this, amount * 1.25f, weaponId);
    }
    private void JourneyDeath()
    {
        IsBurningAtDeath = burnActive;
        var journey = GameplayJourney.Instance;
        if (journey == null || suppressDeathSynergy) return;
        if (burnActive && journey.HasSynergy("Synergy_Firestorm"))
            SynergyCombat.FirePool(transform.position, burnDamagePerTick / Mathf.Max(burnTickInterval, 0.05f), burnWeaponId);
        if (bleedActive && bleedStacks >= 3 && !inheritedBleeding && journey.HasSynergy("Synergy_BloodBomb"))
        {
            int count = 0;
            foreach (var enemy in SnapshotAlive())
            {
                if (count >= 3) break;
                if (enemy == null || enemy == this || enemy.IsDead || (enemy.transform.position - transform.position).sqrMagnitude > 16) continue;
                enemy.ApplyBleeding(bleedDamagePerTick / Mathf.Max(bleedTickInterval, 0.05f) * 0.5f, 3, 0.5f, bleedWeaponId);
                enemy.inheritedBleeding = true;
                VfxFactory.SpawnLightning(transform.position, enemy.transform.position); count++;
            }
        }
    }
}
