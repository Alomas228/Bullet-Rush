using UnityEngine;

public sealed class PvpCombatant : MonoBehaviour
{
    public static float PlayerDamageMultiplier { get; private set; } = 1f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPlayerDamageMultiplier()
    {
        PlayerDamageMultiplier = 1f;
    }

    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool isPlayer;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0f;
    public bool IsPlayer => isPlayer;

    private float damageTakenMultiplier = 1f;

    private void Awake()
    {
        if (CurrentHealth <= 0f)
            CurrentHealth = maxHealth;
    }

    public void Configure(float health, bool player)
    {
        maxHealth = Mathf.Max(health, 1f);
        isPlayer = player;
        CurrentHealth = maxHealth;
        damageTakenMultiplier = 1f;
        if (player)
            PlayerDamageMultiplier = 1f;
    }

    public static void SetPlayerDamageMultiplier(float multiplier)
    {
        PlayerDamageMultiplier = Mathf.Clamp(multiplier, 0.1f, 3f);
    }

    public void SetDamageTakenMultiplier(float multiplier)
    {
        damageTakenMultiplier = Mathf.Clamp(multiplier, 0.1f, 2f);
    }

    public void RestoreHealth(float amount)
    {
        CurrentHealth = Mathf.Min(CurrentHealth + Mathf.Max(amount, 0f), maxHealth);
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || IsDead)
            return;

        CurrentHealth = Mathf.Max(
            CurrentHealth - damage * damageTakenMultiplier,
            0f
        );

        if (AudioManager.Instance != null &&
            AudioManager.Instance.SFXLibrary != null)
        {
            AudioManager.Instance.PlaySFXVariation(
                isPlayer
                    ? AudioManager.Instance.SFXLibrary.PlayerHit
                    : AudioManager.Instance.SFXLibrary.EnemyHit,
                volumeScale: 0.85f,
                priority: SfxPriority.High
            );
        }

        if (IsDead)
        {
            PvpMatchManager match = FindAnyObjectByType<PvpMatchManager>();
            if (match != null)
                match.HandleCombatantDefeated(this);
        }
    }
}
