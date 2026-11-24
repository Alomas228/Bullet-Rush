using System.Collections.Generic;
using UnityEngine;

public partial class Enemy
{
    private Transform enemyWeaponVisual;
    private Transform enemyWeaponFallbackMuzzle;
    private List<Transform> enemyVisualMuzzles;
    private int nextEnemyMuzzle;
    private float enemyWeaponFirePoseUntil;

    private static uint rangedVisualSequence;
    private static uint mobileVisualSequence;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetWeaponVisualSequence()
    {
        rangedVisualSequence = 0;
        mobileVisualSequence = 0;
    }

    /// <summary>Equip an art-only prop without touching the enemy's attack data or random gameplay stream.</summary>
    private void InitializeWeaponVisual()
    {
        if (enemyWeaponVisual != null)
        {
            enemyWeaponVisual.gameObject.SetActive(false);
            Destroy(enemyWeaponVisual.gameObject);
        }
        enemyWeaponVisual = null;
        enemyWeaponFallbackMuzzle = null;
        enemyVisualMuzzles?.Clear();
        nextEnemyMuzzle = 0;
        enemyWeaponFirePoseUntil = 0f;

        GameObject prefab = null;
        if (cachedEnemyType == EnemyType.Ranged && enemyData != null)
        {
            bool mobile = variant == EnemyVariant.Mobile;
            GameObject[] options = mobile ? enemyData.MobileWeaponVisuals : enemyData.RangedWeaponVisuals;
            if (options == null || options.Length == 0) options = enemyData.RangedWeaponVisuals;
            if (options != null && options.Length > 0)
            {
                uint sequence = mobile ? mobileVisualSequence++ : rangedVisualSequence++;
                int start = (int)(sequence % (uint)options.Length);
                for (int i = 0; i < options.Length; i++)
                {
                    prefab = options[(start + i) % options.Length];
                    if (prefab != null) break;
                }
            }
        }
        else if (isBoss && bossData != null &&
                 (bossData.UsesBasicRangedAttack || bossData.AbilityPattern == BossAbilityPattern.ProjectileFan))
        {
            prefab = bossData.RangedWeaponVisual;
        }

        if (prefab == null || enemyProjectilePrefab == null) return;

        // Match the player's front-right attachment, with a slightly shorter
        // mount on larger mobs so it doesn't dominate the body silhouette.
        GameObject visual = Instantiate(prefab, transform, false);
        visual.name = "EnemyWeapon_" + prefab.name;
        enemyWeaponVisual = visual.transform;
        enemyWeaponVisual.localPosition = isBoss
            ? new Vector3(0.48f, 0.22f, 0.48f)
            : new Vector3(0.53f, 0.18f, 0.53f);
        enemyWeaponVisual.localRotation = Quaternion.identity;
        enemyWeaponVisual.localScale = Vector3.one * (isBoss ? 0.85f : 0.92f);

        // A single MeshRenderer, shared weapon palette, no colliders or
        // weapon gameplay scripts are needed to present an enemy's model.
        enemyVisualMuzzles ??= new List<Transform>(5);
        foreach (Transform child in visual.GetComponentsInChildren<Transform>(false))
        {
            if (child.name == "FirePoint") enemyWeaponFallbackMuzzle = child;
            else if (child.name.StartsWith("VisualMuzzle_", System.StringComparison.Ordinal))
                enemyVisualMuzzles.Add(child);
        }
        if (enemyWeaponFallbackMuzzle == null) enemyWeaponFallbackMuzzle = enemyWeaponVisual;
        if (!isBoss) BattleMeshBatcher.RegisterTree(visual);
    }

    private void UpdateWeaponVisualPose()
    {
        if (enemyWeaponVisual == null || Time.time < enemyWeaponFirePoseUntil) return;

        Vector3 direction = transform.forward;
        if (bossFanWindup) direction = bossFanDirection;
        else if (attackTelegraphActive && player != null)
            direction = player.position - enemyWeaponVisual.position;

        direction.y = 0f;
        if (direction.sqrMagnitude < 0.0001f) return;
        enemyWeaponVisual.rotation = Quaternion.RotateTowards(enemyWeaponVisual.rotation,
            Quaternion.LookRotation(direction), 720f * Time.deltaTime);
    }

    // Called once for a volley so all fan particles come out of the same
    // decorative outlet. Twin/cluster props advance once for each new volley.
    private Vector3? GetEnemyWeaponMuzzle(Vector3 direction)
    {
        if (enemyWeaponVisual == null) return null;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.0001f)
            enemyWeaponVisual.rotation = Quaternion.LookRotation(direction);
        enemyWeaponFirePoseUntil = Time.time + 0.08f;

        if (enemyVisualMuzzles.Count > 0)
        {
            Transform muzzle = enemyVisualMuzzles[nextEnemyMuzzle];
            nextEnemyMuzzle = (nextEnemyMuzzle + 1) % enemyVisualMuzzles.Count;
            if (muzzle != null) return muzzle.position;
        }
        return enemyWeaponFallbackMuzzle != null ? enemyWeaponFallbackMuzzle.position : enemyWeaponVisual.position;
    }
}
