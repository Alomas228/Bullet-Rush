using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Bullet")]
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 10f;

    [Header("Pierce")]
    [SerializeField] private int pierceCount = 0;

    [Header("Burn")]
    [SerializeField] private float burnDamagePerSecond = 0f;
    [SerializeField] private float burnDuration = 0f;
    [SerializeField] private float burnTickInterval = 0.5f;

    [Header("Lightning")]
    [SerializeField] private float lightningChance = 0f;
    [SerializeField] private float lightningDamage = 0f;
    [SerializeField] private int lightningTargets = 1;
    [SerializeField] private float lightningRange = 0f;

    private bool isCritical;
    private int enemiesHit;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        Vector3 previousPosition =
            transform.position;

        transform.Translate(
            Vector3.forward *
            speed *
            Time.deltaTime
        );

        if (HitStructure(
            previousPosition,
            transform.position))
        {
            Destroy(gameObject);
        }
    }

    private bool HitStructure(
        Vector3 from,
        Vector3 to)
    {
        Vector3 direction =
            to - from;

        float distance =
            direction.magnitude;

        if (distance <= 0.0001f)
            return false;

        if (Physics.Raycast(
            from,
            direction.normalized,
            out RaycastHit hit,
            distance))
        {
            if (hit.collider.GetComponentInParent<WorldStructure>() != null)
            {
                transform.position = hit.point;
                return true;
            }
        }

        return false;
    }

    public void Initialize(
        float newDamage,
        float newSpeed,
        bool critical,
        int newPierceCount,

        float newBurnDamagePerSecond = 0f,
        float newBurnDuration = 0f,
        float newBurnTickInterval = 0.5f,

        float newLightningChance = 0f,
        float newLightningDamage = 0f,
        int newLightningTargets = 1,
        float newLightningRange = 0f
    )
    {
        damage = newDamage;
        speed = newSpeed;
        isCritical = critical;
        pierceCount = newPierceCount;

        burnDamagePerSecond =
            newBurnDamagePerSecond;

        burnDuration =
            newBurnDuration;

        burnTickInterval =
            newBurnTickInterval;

        lightningChance =
            newLightningChance;

        lightningDamage =
            newLightningDamage;

        lightningTargets =
            newLightningTargets;

        lightningRange =
            newLightningRange;

        enemiesHit = 0;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<WorldStructure>() != null)
        {
            Destroy(gameObject);
            return;
        }

        Enemy enemy =
            other.GetComponent<Enemy>();

        if (enemy == null)
            return;

        if (enemy.IsDead)
            return;

        // ============================================
        // NORMAL DAMAGE
        // ============================================

        enemy.TakeDamage(
            damage,
            isCritical
        );

        // ============================================
        // BURN
        // ============================================

        if (
            burnDamagePerSecond > 0f &&
            burnDuration > 0f
        )
        {
            enemy.ApplyBurn(
                burnDamagePerSecond,
                burnDuration,
                burnTickInterval
            );
        }

        // ============================================
        // LIGHTNING
        // ============================================

        if (
            lightningChance > 0f &&
            lightningDamage > 0f &&
            lightningRange > 0f
        )
        {
            if (
                Random.value <
                lightningChance
            )
            {
                enemy.TriggerLightning(
                    lightningDamage,
                    lightningTargets,
                    lightningRange
                );
            }
        }

        // ============================================
        // PIERCE
        // ============================================

        enemiesHit++;

        if (enemiesHit > pierceCount)
        {
            Destroy(gameObject);
        }
    }
}