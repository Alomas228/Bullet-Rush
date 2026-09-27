using UnityEngine;

/// <summary>
/// Применяет постоянные улучшения (PermanentUpgrades) на старте забега:
///  - множители урона, скорострельности, скорости бега, скорости снарядов
///    и критические удары -> PlayerStats;
///  - максимум здоровья и регенерация -> PlayerHealth;
///  - перезарядка рывка -> PlayerController;
///  - перезарядка бомбы и щита -> AbilityManager.
///
/// Бонусы применяются один раз за забег: компонент сам подписан на
/// GameStateManager и страхуется флагом applied.
///
/// Компонент добавляется на игрока панелью «Улучшения»
/// (Tools -> Bullet Rush -> Build Upgrades UI). PlayerEquipmentApplier
/// тоже вызывает ApplyPermanentUpgrades — чтобы перезарядки способностей
/// уменьшились уже после разблокировки бомбы/щита.
/// </summary>
public class PermanentUpgradeApplier : MonoBehaviour
{
    private bool applied;
    private bool subscribed;

    private PlayerStats playerStats;
    private PlayerHealth playerHealth;
    private PlayerController playerController;
    private AbilityManager abilityManager;

    private void Awake()
    {
        Resolve();
    }

    private void OnEnable()
    {
        EnsureSubscribed();

        if (IsPlaying())
            ApplyPermanentUpgrades();
    }

    private void Start()
    {
        EnsureSubscribed();

        if (IsPlaying())
            ApplyPermanentUpgrades();
    }

    private void OnDisable()
    {
        if (!subscribed || GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged -=
            HandleStateChanged;

        subscribed = false;
    }

    private static bool IsPlaying()
    {
        return
            GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing;
    }

    private void EnsureSubscribed()
    {
        if (subscribed || GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged +=
            HandleStateChanged;

        subscribed = true;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing)
            ApplyPermanentUpgrades();
    }

    // =========================================================
    // APPLY
    // =========================================================

    /// <summary>
    /// Навешивает все купленные бонусы на игрока. Повторные вызовы
    /// в том же забеге игнорируются.
    /// </summary>
    public void ApplyPermanentUpgrades()
    {
        if (applied)
            return;

        Resolve();

        if (playerStats == null)
            return;

        applied = true;

        ApplyStats(playerStats);
        ApplyHealth(playerHealth);
        ApplyDashCooldown(playerController);
        ApplyAbilityCooldown(abilityManager);
    }

    private void Resolve()
    {
        if (playerStats == null)
            playerStats = GetComponent<PlayerStats>();

        if (playerHealth == null)
            playerHealth = GetComponent<PlayerHealth>();

        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (abilityManager == null)
            abilityManager = GetComponent<AbilityManager>();
    }

    private static void ApplyStats(PlayerStats stats)
    {
        if (stats == null)
            return;

        int level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.Damage
        );

        if (level > 0)
            stats.AddDamagePercent(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.Damage,
                    level
                )
            );

        level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.FireRate
        );

        if (level > 0)
            stats.AddFireRatePercent(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.FireRate,
                    level
                )
            );

        level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.MoveSpeed
        );

        if (level > 0)
            stats.AddMoveSpeedPercent(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.MoveSpeed,
                    level
                )
            );

        level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.ProjectileSpeed
        );

        if (level > 0)
            stats.AddProjectileSpeedPercent(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.ProjectileSpeed,
                    level
                )
            );

        level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.CriticalChance
        );

        if (level > 0)
            stats.AddCriticalChance(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.CriticalChance,
                    level
                )
            );

        level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.CriticalDamage
        );

        if (level > 0)
            stats.AddCriticalDamageMultiplier(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.CriticalDamage,
                    level
                )
            );
    }

    private static void ApplyHealth(PlayerHealth health)
    {
        if (health == null)
            return;

        int level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.MaxHealth
        );

        if (level > 0)
            health.AddMaxHealthPercent(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.MaxHealth,
                    level
                )
            );

        level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.HealthRegen
        );

        if (level > 0)
            health.AddHealthRegen(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.HealthRegen,
                    level
                )
            );
    }

    private static void ApplyDashCooldown(PlayerController controller)
    {
        if (controller == null)
            return;

        int level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.DashCooldown
        );

        if (level <= 0)
            return;

        // GetApplyValue для перезарядок отрицательный (множитель
        // меньше единицы), а ReduceDashCooldown ждёт долю снятия.
        controller.ReduceDashCooldown(
            Mathf.Abs(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.DashCooldown,
                    level
                )
            )
        );
    }

    private static void ApplyAbilityCooldown(AbilityManager manager)
    {
        if (manager == null)
            return;

        int level = PermanentUpgrades.GetLevel(
            PermanentUpgradeStat.AbilityCooldown
        );

        if (level <= 0)
            return;

        float value =
            Mathf.Abs(
                PermanentUpgrades.GetApplyValue(
                    PermanentUpgradeStat.AbilityCooldown,
                    level
                )
            );

        if (manager.Bomb != null)
            manager.Bomb.UpgradeCooldown(value);

        if (manager.Shield != null)
            manager.Shield.UpgradeCooldown(value);
    }
}
