using UnityEngine;

public class BombAbility : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float baseDamage = 5f;
    [SerializeField] private float radius = 5f;
    [SerializeField] private float cooldown = 8f;

    [Header("Visual")]
    [Tooltip("Если не назначен — эффект создастся автоматически кодом.")]
    [SerializeField] private GameObject bombEffectPrefab;

    private float cooldownTimer;
    private PlayerStats playerStats;

    public bool IsUnlocked { get; set; }

    public float Cooldown => cooldown;
    public float CooldownRemaining => cooldownTimer;
    public bool IsReady => cooldownTimer <= 0f;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
    }

    private void Update()
    {
        if (cooldownTimer > 0f)
            cooldownTimer -= Time.deltaTime;
    }

    public bool TryActivate()
    {
        if (!IsUnlocked)
            return false;

        if (!IsReady)
            return false;

        cooldownTimer = cooldown;

        Explode();

        return true;
    }

    private void Explode()
    {
        Vector3 center = transform.position;

        if (bombEffectPrefab != null)
        {
            GameObject effect =
                Instantiate(
                    bombEffectPrefab,
                    center,
                    Quaternion.identity
                );

            Destroy(effect, 2f);
        }
        else
        {
            SpawnProceduralEffect(center);
        }

        Collider[] hits =
            Physics.OverlapSphere(center, radius);

        float damage = CalculateDamage();

        foreach (Collider hit in hits)
        {
            Enemy enemy =
                hit.GetComponentInParent<Enemy>();

            if (enemy == null)
                continue;

            enemy.TakeDamage(damage);
        }

        Debug.Log(
            $"Bomb exploded! Damage: {damage:0.#}, " +
            $"Radius: {radius}"
        );
    }

    private float CalculateDamage()
    {
        float multiplier = 1f;

        if (playerStats != null)
            multiplier = playerStats.DamageMultiplier;

        return baseDamage * multiplier;
    }

    private void SpawnProceduralEffect(Vector3 center)
    {
        GameObject effect =
            new GameObject("BombExplosion");

        effect.transform.position = center;

        BombExplosionEffect visual =
            effect.AddComponent<BombExplosionEffect>();

        if (visual != null)
        {
            visual.Initialize(
                radius,
                new Color(1f, 0.55f, 0.1f, 0.8f)
            );
        }
    }

    public void UpgradeCooldown(float percent)
    {
        cooldown *= 1f - percent;
        cooldown = Mathf.Max(cooldown, 1f);
    }

    public void UpgradeDamage(float percent)
    {
        baseDamage *= 1f + percent;
    }

    public void UpgradeRadius(float percent)
    {
        radius *= 1f + percent;
    }
}
