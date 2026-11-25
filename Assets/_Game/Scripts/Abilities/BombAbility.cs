using UnityEngine;

public class BombAbility : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Урон бомбы. Масштаб ×10 к прежнему — как и весь остальной урон в игре, игрок живёт 100 HP.")]
    [SerializeField] private float baseDamage = 300f;
    [SerializeField] private float radius = 5f;
    [SerializeField] private float cooldown = 12f;

    [Header("Visual")]
    [Tooltip("Если не назначен — эффект создастся автоматически кодом.")]
    [SerializeField] private GameObject bombEffectPrefab;

    private float cooldownTimer;
    private float initialDamage, initialRadius, initialCooldown;
    private readonly System.Collections.Generic.List<GameObject> spawnedEffects = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<Enemy> targets = new(64);
    private PlayerStats playerStats;

    public bool IsUnlocked { get; set; }

    public float Cooldown => cooldown;
    public float CooldownRemaining => cooldownTimer;
    public bool IsReady => cooldownTimer <= 0f;

    private void Awake()
    {
        initialDamage = baseDamage;
        initialRadius = radius;
        initialCooldown = cooldown;
        playerStats = GetComponent<PlayerStats>();
    }

    internal void ResetForSession()
    {
        baseDamage = initialDamage;
        radius = initialRadius;
        cooldown = initialCooldown;
        cooldownTimer = 0f;
        IsUnlocked = false;
        foreach (GameObject effect in spawnedEffects)
            if (effect != null) { effect.SetActive(false); Destroy(effect); }
        spawnedEffects.Clear();
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
        NotifyAbilityUsed();

        return true;
    }

    private void NotifyAbilityUsed()
    {
        RunMetrics metrics =
            FindAnyObjectByType<RunMetrics>();

        if (metrics != null)
            metrics.OnAbilityUsed();
    }

    private Vector3 GetOwnerPosition()
    {
        Transform self = transform;

        if (self.CompareTag("Player"))
            return self.position;

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        return player != null
            ? player.transform.position
            : self.position;
    }

    private void Explode()
    {
        Vector3 center = GetOwnerPosition();

        PlayPlayerExplosionSound();

        if (bombEffectPrefab != null)
        {
            GameObject effect =
                Instantiate(
                    bombEffectPrefab,
                    center,
                    Quaternion.identity
                );

            spawnedEffects.RemoveAll(item => item == null);
            spawnedEffects.Add(effect);

            Destroy(effect, 2f);
        }
        else
        {
            SpawnProceduralEffect(center);
        }

        float damage = CalculateDamage();
        Enemy.CopyAliveTo(targets);
        int count=0, eventId=DamageEventId.Next();
        foreach (Enemy enemy in targets)
        {
            if(count>=16) break;
            if (enemy == null || enemy.IsDead) continue;
            Vector3 delta=enemy.transform.position-center; delta.y=0;
            if(delta.sqrMagnitude>radius*radius) continue;
            count++; enemy.TakeDamage(damage,false,DamageSource.Ability,eventId);
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

        return baseDamage * multiplier * RunBuildMechanics.EffectFactor;
    }

    private void PlayPlayerExplosionSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx =
            AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.PlayerAbilityExplosion,
                volumeScale: 0.9f,
                priority: SfxPriority.High
            );
    }

    private void SpawnProceduralEffect(Vector3 center)
    {
        VfxFactory.TrySpawnExplosion(
            center,
            radius,
            new Color(1f, 0.55f, 0.1f, 0.8f), playSound: false
        );
    }

    public void UpgradeCooldown(float percent)
    {
        cooldown *= 1f - percent;
        cooldown = Mathf.Max(cooldown, 5f);
    }

    public void UpgradeDamage(float percent)
    {
        baseDamage *= 1f + percent;
    }

    public void UpgradeRadius(float percent)
    {
        radius = Mathf.Min(radius * (1f + percent), 7f);
    }
}
