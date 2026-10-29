using UnityEngine;

public partial class Enemy
{
    private int signatureSequence;
    private float signatureWindupUntil;

    private bool UseCampaignBossAbility()
    {
        float warning = Mathf.Max(GetBossAbilityWarningDuration(), 1f);
        float damage = GetBossAbilityDamage();
        if ((int)bossData.AbilityPattern >= (int)BossAbilityPattern.MortarBarrage)
            signatureWindupUntil = Time.time + warning;
        switch (bossData.AbilityPattern)
        {
            case BossAbilityPattern.MortarBarrage:
                // Three separated impacts along a locked line, leaving space
                // to dodge sideways rather than covering every exit at once.
                Vector3 target = player.position;
                Vector3 side = Vector3.Cross(Vector3.up, target - transform.position).normalized;
                if (side.sqrMagnitude < 0.01f) side = Vector3.right;
                for (int i = -1; i <= 1; i++)
                    CampaignStrike(target + side * i * 5f, 2f, warning + Mathf.Abs(i) * 0.45f, damage);
                PlayBossAbilitySound();
                return true;

            case BossAbilityPattern.RadialBarrage:
                int count = 8 + BossPhase * 2;
                Vector3 origin = attackPoint.position;
                float offset = signatureSequence++ * 17f;
                for (int i = 0; i < count; i++)
                {
                    Vector3 direction = Quaternion.Euler(0f, offset + i * 360f / count, 0f) * Vector3.forward;
                    BossLineTelegraph.Spawn(origin, origin + direction * 18f, warning,
                        bossData.AbilityColor, () =>
                        {
                            if (!CanCompleteCampaignAttack() || enemyProjectilePrefab == null) return;
                            EnemyProjectile projectile = EnemyProjectilePool.Spawn(enemyProjectilePrefab,
                                origin, Quaternion.LookRotation(direction));
                            if (projectile != null)
                                projectile.Initialize(direction, damage, bossData.ProjectileSpeed);
                        });
                }
                PlayBossAbilitySound();
                return true;

            case BossAbilityPattern.SummoningRitual:
                SummonEnemies();
                // The matriarch's own space is dangerous, while the player
                // chooses whether to clear minions or keep attacking the boss.
                CampaignStrike(transform.position, 3f + BossPhase * 0.4f, warning, damage);
                PlayBossAbilitySound();
                return true;

            case BossAbilityPattern.Singularity:
                Vector3 center = transform.position;
                float rotation = signatureSequence++ % 2 == 0 ? 0f : 45f;
                bool beamHitPlayer = false;
                for (int i = 0; i < 4; i++)
                {
                    Vector3 direction = Quaternion.Euler(0f, rotation + i * 90f, 0f) * Vector3.forward;
                    Vector3 end = center + direction * 24f;
                    BossLineTelegraph.Spawn(center, end, warning, bossData.AbilityColor, () =>
                    {
                        if (!CanCompleteCampaignAttack() || PlayerHealth.Instance == null) return;
                        Vector3 delta = PlayerHealth.Instance.transform.position - center;
                        delta.y = 0f;
                        float along = Vector3.Dot(delta, direction);
                        Vector3 lateral = delta - direction * along;
                        if (!beamHitPlayer && along >= 0f && along <= 24f && lateral.sqrMagnitude <= 0.9f * 0.9f)
                        {
                            beamHitPlayer = true;
                            PlayerHealth.Instance.TakeDamage(damage);
                        }
                        VfxFactory.SpawnLightning(center, end);
                    });
                }
                if (BossPhase >= 2)
                {
                    Vector3 locked = player.position;
                    if (HasEscapeRouteFromPlayer(locked, 2.5f))
                        CampaignStrike(locked, 2.5f, warning + 0.7f, damage * 0.6f, true);
                }
                PlayBossAbilitySound();
                return true;
        }
        return false;
    }

    private bool CanCompleteCampaignAttack() => this != null && !IsDead &&
        (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState == GameState.Playing);

    private void CampaignStrike(Vector3 position, float radius, float warning, float damage, bool gravity = false)
    {
        if (!HasEscapeRouteFromPlayer(position, radius)) return;
        // A callback owned by a telegraph cancels damage if its boss dies.
        BossLineTelegraph.Spawn(position + Vector3.left * radius,
            position + Vector3.right * radius, warning, bossData.AbilityColor, () =>
            {
                if (!CanCompleteCampaignAttack()) return;
                MapStrikeZone.Strike(position, radius, damage, false, 0f, 0.15f,
                    bossData.AbilityColor, 0f, gravity, 4f, 4f, 3f);
            });
        var preview = new GameObject("BossImpactWarning").AddComponent<BossAttackZone>();
        // Visual warning only; the owner-checked callback applies actual damage.
        preview.Initialize(0f, radius, warning, bossData.AbilityColor);
        preview.transform.position = position;
    }
}
