using UnityEngine;

public class PlayerHealth : MonoBehaviour
{
    [SerializeField] private float maxHealth = 10f;
    [SerializeField] private float healTickInterval = 0.5f;

    [Header("Screen Shake")]
    [Tooltip("Сила тряски экрана при получении урона (0 = без тряски).")]
    [SerializeField] private float hitShakeAmount = 0.35f;

    private float healthRegenPerSecond;
    private float healTimer;

    private PlayerController playerController;

    public float CurrentHealth { get; private set; }

    public float MaxHealth => maxHealth;

    public float HealthRegenPerSecond => healthRegenPerSecond;

    private void Start()
    {
        CurrentHealth = maxHealth;

        playerController =
            GetComponent<PlayerController>();

        Debug.Log(
            $"Player HP: {CurrentHealth}/{maxHealth}"
        );
    }

    private void Update()
    {
        if (healthRegenPerSecond <= 0f)
            return;

        if (CurrentHealth >= maxHealth)
            return;

        healTimer += Time.deltaTime;

        while (healTimer >= healTickInterval)
        {
            healTimer -= healTickInterval;

            float healAmount =
                healthRegenPerSecond * healTickInterval;

            CurrentHealth = Mathf.Min(
                CurrentHealth + healAmount,
                maxHealth
            );
        }
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || CurrentHealth <= 0f)
            return;

        if (playerController != null &&
            playerController.IsDashing)
            return;

        CurrentHealth -= damage;
        CurrentHealth = Mathf.Max(CurrentHealth, 0f);

        PlayPlayerHitSound();
        TriggerScreenShake();

        Debug.Log(
            $"Player HP: {CurrentHealth}/{maxHealth}"
        );

        if (CurrentHealth <= 0f)
            Die();
    }

    public void Kill()
    {
        if (CurrentHealth <= 0f)
            return;

        CurrentHealth = 0f;

        Die();
    }

    public void Heal(float amount)
    {
        if (amount <= 0f)
            return;

        if (CurrentHealth <= 0f)
            return;

        CurrentHealth =
            Mathf.Min(
                CurrentHealth + amount,
                maxHealth
            );

        Debug.Log(
            $"Player healed +{amount:0.##} HP " +
            $"(Now: {CurrentHealth}/{maxHealth})"
        );
    }

    private void PlayPlayerHitSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.PlayerHit);
    }

    private void TriggerScreenShake()
    {
        CameraFollow camera =
            FindAnyObjectByType<CameraFollow>();

        if (camera != null)
            camera.AddShake(hitShakeAmount);
    }

    public void AddMaxHealthPercent(float percent)
    {
        if (percent <= 0f)
            return;

        float oldMaxHealth = maxHealth;

        maxHealth *= 1f + percent;

        float healthIncrease =
            maxHealth - oldMaxHealth;

        CurrentHealth = Mathf.Min(
            CurrentHealth + healthIncrease,
            maxHealth
        );

        Debug.Log(
            $"Max HP increased: {oldMaxHealth:0.##} -> {maxHealth:0.##}"
        );
    }

    public void AddHealthRegen(float regenPerSecond)
    {
        healthRegenPerSecond += regenPerSecond;

        Debug.Log(
            $"Health regen: +{regenPerSecond:0.##}/s " +
            $"(Total: {healthRegenPerSecond:0.##}/s)"
        );

        // Мгновенное подлечивание при взятии апа
        if (regenPerSecond > 0f)
        {
            CurrentHealth = Mathf.Min(
                CurrentHealth + regenPerSecond,
                maxHealth
            );
        }
    }

    private void Die()
    {
        Debug.Log("PLAYER DIED");

        PlayPlayerDeathSound();

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.GameOver();

        gameObject.SetActive(false);
    }

    private void PlayPlayerDeathSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.PlayerDie);
    }
}