using UnityEngine;
using System.Collections.Generic;

public sealed class PvpProjectile : MonoBehaviour
{
    private static readonly List<PvpProjectile> activeProjectiles =
        new List<PvpProjectile>(16);

    private Vector3 direction;
    private float speed;
    private float damage;
    private float lifetime;
    private bool fromPlayer;

    public bool IsPlayerOwned => fromPlayer;
    public Vector3 Direction => direction;
    public float Speed => speed;

    public static int ActiveCount => activeProjectiles.Count;

    public static PvpProjectile GetActive(int index) => activeProjectiles[index];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveProjectiles()
    {
        activeProjectiles.Clear();
    }

    private void OnEnable()
    {
        if (!activeProjectiles.Contains(this))
            activeProjectiles.Add(this);
    }

    private void OnDisable()
    {
        activeProjectiles.Remove(this);
    }

    public void Initialize(Vector3 shotDirection, float shotSpeed, float shotDamage, bool playerOwned)
    {
        direction = shotDirection.normalized;
        speed = Mathf.Max(shotSpeed, 1f);
        damage = Mathf.Max(shotDamage, 0f);
        fromPlayer = playerOwned;
        lifetime = 3f;
        transform.forward = direction;
    }

    private void Update()
    {
        float distance = speed * Time.deltaTime;
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance))
        {
            if (StructureQuery.IsWorldStructure(hit.collider))
            {
                Destroy(gameObject);
                return;
            }

            PvpCombatant target = hit.collider.GetComponentInParent<PvpCombatant>();
            if (target != null && target.IsPlayer != fromPlayer)
            {
                target.TakeDamage(damage);
                Destroy(gameObject);
                return;
            }
        }

        transform.position += direction * distance;
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
            Destroy(gameObject);
    }
}
