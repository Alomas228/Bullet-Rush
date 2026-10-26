using UnityEngine;

public sealed class PvpCombatant : MonoBehaviour
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool isPlayer;

    public float CurrentHealth { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsDead => CurrentHealth <= 0f;
    public bool IsPlayer => isPlayer;

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
    }

    public void TakeDamage(float damage)
    {
        if (damage <= 0f || IsDead)
            return;

        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0f);

        if (IsDead)
        {
            PvpMatchManager match = FindAnyObjectByType<PvpMatchManager>();
            if (match != null)
                match.HandleCombatantDefeated(this);
        }
    }
}
