using UnityEngine;

public partial class Enemy
{
    public enum TacticalRole { None, Coordinator, Blocker }
    public TacticalRole Tactic { get; private set; }
    private Enemy coordinator;
    private float commandUntil, nextCommand, nextTacticalAttack;
    internal bool IsObjectiveCoordinator { get; private set; }
    internal bool IsCacheGuard { get; private set; }
    internal bool IsUnderCommand => coordinator != null && !coordinator.IsDead && Time.time < commandUntil;
    internal bool ShowsDirectionalShield => HasDirectionalShield;
    private const int MaxCommandedAllies = 4;
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
        IsObjectiveCoordinator = false;
        IsCacheGuard = false;
        if (HasDirectionalShield) EnsureMapStatusVisual();
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
        if (Tactic == TacticalRole.Blocker) EnsureMapStatusVisual();
        nextTacticalAttack = Time.time + 4f;
    }
    internal void AssignCoordinator(bool objective = false)
    {
        Tactic = TacticalRole.Coordinator;
        IsObjectiveCoordinator |= objective;
        EnsureMapStatusVisual();
    }

    internal void MarkCacheGuard()
    {
        IsCacheGuard = true;
        EnsureMapStatusVisual();
    }

    internal void Retreat(bool animate = false)
    {
        if (IsDead) return;
        IsDead = true;
        UnregisterAlive();
        if (animate && isActiveAndEnabled)
        {
            foreach (Collider collider in GetComponentsInChildren<Collider>())
                collider.enabled = false;
            StartCoroutine(RetreatAnimation());
            return;
        }
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    internal bool HasDirectCleanupPath(Vector3 target, float maxDistance)
    {
        Vector3 delta = target - transform.position;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance > maxDistance || !isActiveAndEnabled) return false;
        if (distance < 0.1f) return true;
        float radius = selfCollider != null
            ? Mathf.Clamp(Mathf.Min(selfCollider.bounds.extents.x, selfCollider.bounds.extents.z), 0.15f, 0.7f)
            : 0.3f;
        foreach (RaycastHit hit in Physics.SphereCastAll(transform.position + Vector3.up * rayHeight,
            radius, delta / distance, distance, ~0, QueryTriggerInteraction.Ignore))
            if (StructureQuery.IsWorldStructure(hit.collider)) return false;
        return true;
    }

    private System.Collections.IEnumerator RetreatAnimation()
    {
        Vector3 start = transform.position;
        Vector3 startScale = transform.localScale;
        Vector3 direction = player != null ? start - player.position : transform.forward;
        direction.y = 0f;
        if (direction.sqrMagnitude < 0.01f) direction = transform.forward;
        direction.Normalize();
        if (direction.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        const float duration = 1.65f;
        float distance = 3.4f;
        foreach (RaycastHit hit in Physics.SphereCastAll(start + Vector3.up * rayHeight,
            0.25f, direction, distance, ~0, QueryTriggerInteraction.Ignore))
            if (StructureQuery.IsWorldStructure(hit.collider))
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.15f));
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = elapsed / duration;
            transform.position = start + direction * (distance * t);
            float fade = Mathf.InverseLerp(0.45f, 1f, t);
            transform.localScale = startScale * (1f - Mathf.SmoothStep(0f, 1f, fade));
            yield return null;
            elapsed += Time.unscaledDeltaTime;
        }
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private float CommandSpeedScale => IsUnderCommand ? 1.18f : 1f;
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
            nextCommand = Time.time + 0.25f; int commanded = 0;
            foreach (var ally in aliveEnemies)
            {
                if (commanded >= MaxCommandedAllies) break;
                if (ally == null || ally == this || ally.IsDead || ally.isBoss || ally.IsElite || ally.Tactic != TacticalRole.None ||
                    (ally.transform.position - transform.position).sqrMagnitude > 49) continue;
                if (ally.coordinator != null && ally.coordinator != this && !ally.coordinator.IsDead && Time.time < ally.commandUntil) continue;
                ally.coordinator = this; ally.commandUntil = Time.time + 0.4f;
                ally.EnsureMapStatusVisual();
                commanded++;
            }
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
