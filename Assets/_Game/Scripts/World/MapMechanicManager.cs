using System.Collections;
using UnityEngine;

/// <summary>
/// Боевая механика текущей карты.
///
/// Зеркалит RunModifierManager: ленивый синглтон (EnsureExists),
/// сброс и ролл на старте забега. Разница в том, что тема забега
/// случайна, а механика карты детерминирована выбранной локацией —
/// поэтому её не «роллят», а берут из GameMap текущей карты.
///
/// Что даёт механика:
///   • множители врагов (скорость/здоровье/урон/число) — их читают
///     Enemy.CacheRunModifierScaling, EnemySpawner и WaveManager;
///   • уклон спавна (быстрые/дальние/танки) — как у темы забега;
///   • разовые баффы игрока на старте забега;
///   • периодические зоны урона (песчаная буря, метеориты космоса).
/// </summary>
public class MapMechanicManager : MonoBehaviour
{
    public static MapMechanicManager Instance { get; private set; }

    // =========================================================
    // TUNING
    // =========================================================

    [Header("Hazard timing")]
    [Tooltip("Пауза перед первым залпом зон, секунды.")]
    [SerializeField] private float hazardFirstDelay = 10f;

    [Tooltip("Интервал между залпами зон, секунды.")]
    [SerializeField] private float hazardInterval = 9f;

    [Tooltip("Сколько зон в одном залпе.")]
    [SerializeField] private int hazardBurst = 2;

    [Header("Hazard shape")]
    [SerializeField] private float hazardWarning = 1.6f;
    [SerializeField] private float hazardDuration = 4.5f;
    [SerializeField] private float hazardGrow = 1.8f;

    [Header("Arena")]
    [Tooltip("Полуширина арены: зоны сыпятся в этот квадрат вокруг центра.")]
    [SerializeField] private float arenaHalfSize = 30f;

    private MapMechanic mechanic = MapMechanic.None;
    private float strength = 1f;
    private Coroutine hazardRoutine;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// Находит менеджер в сцене или создаёт его. Нужен тому, кто
    /// начинает забег, чтобы не требовать ручной расстановки объекта.
    /// </summary>
    public static MapMechanicManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        MapMechanicManager existing =
            FindAnyObjectByType<MapMechanicManager>();

        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("MapMechanicManager");

        return host.AddComponent<MapMechanicManager>();
    }

    /// <summary>Сброс механики (выход в меню).</summary>
    public void ResetRun()
    {
        mechanic = MapMechanic.None;
        strength = 1f;

        StopHazards();
    }

    /// <summary>
    /// Применяет механику карты на старте забега: баффы игрока и
    /// фоновые зоны урона. Вызывается один раз в начале забега.
    /// </summary>
    public void BeginRun(GameMap map)
    {
        ResetRun();

        if (map == null)
            return;

        mechanic = map.mechanic;
        strength = Mathf.Max(map.mechanicStrength, 0f);

        ApplyPlayerBuffs();

        if (HasPeriodicHazard)
            StartHazards();

        Debug.Log($"MAP MECHANIC: {mechanic} (strength {strength:0.##})");
    }

    // =========================================================
    // PLAYER BUFFS
    // =========================================================

    private void ApplyPlayerBuffs()
    {
        float damageBonus = PlayerDamagePercent;
        float moveBonus = PlayerMoveSpeedPercent;

        if (damageBonus > 0f || moveBonus > 0f)
        {
            PlayerStats stats = FindAnyObjectByType<PlayerStats>();

            if (stats != null)
            {
                if (damageBonus > 0f)
                    stats.AddDamagePercent(damageBonus);

                if (moveBonus > 0f)
                    stats.AddMoveSpeedPercent(moveBonus);
            }
        }
    }

    // =========================================================
    // EFFECT QUERIES (instance, с учётом силы механики)
    // =========================================================

    public float EnemySpeedMultiplier =>
        BlendOf(EnemyBaseSpeed);

    public float EnemyHealthMultiplier =>
        BlendOf(EnemyBaseHealth);

    public float EnemyDamageMultiplier =>
        BlendOf(EnemyBaseDamage);

    public float EnemyCountMultiplier =>
        BlendOf(EnemyBaseCount);

    public float PlayerDamagePercent =>
        PlayerBaseDamage * strength;

    public float PlayerMoveSpeedPercent =>
        PlayerBaseMoveSpeed * strength;

    public RunSpawnBias SpawnBias
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.OpenField:
                    return RunSpawnBias.Ranged;

                case MapMechanic.Highland:
                    return RunSpawnBias.Tank;

                case MapMechanic.Urban:
                    return RunSpawnBias.Ranged;

                default:
                    return RunSpawnBias.None;
            }
        }
    }

    public float SpawnBiasChance
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.OpenField: return 0.30f * strength;
                case MapMechanic.Highland: return 0.30f * strength;
                case MapMechanic.Urban: return 0.35f * strength;
                default: return 0f;
            }
        }
    }

    // =========================================================
    // PER-MECHANIC BASE VALUES
    // =========================================================

    private float EnemyBaseSpeed
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.Highland: return 0.94f;
                case MapMechanic.Sandstorm: return 0.95f;
                case MapMechanic.LowGravity: return 1.18f;
                default: return 1f;
            }
        }
    }

    private float EnemyBaseHealth
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.OpenField: return 0.95f;
                case MapMechanic.Highland: return 1.16f;
                case MapMechanic.LowGravity: return 1.06f;
                default: return 1f;
            }
        }
    }

    private float EnemyBaseDamage
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.OpenField: return 1.08f;
                default: return 1f;
            }
        }
    }

    private float EnemyBaseCount
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.OpenField: return 0.92f;
                case MapMechanic.Urban: return 1.14f;
                default: return 1f;
            }
        }
    }

    private float PlayerBaseDamage
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.OpenField: return 0.06f;
                default: return 0f;
            }
        }
    }

    private float PlayerBaseMoveSpeed
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.OpenField: return 0.05f;
                case MapMechanic.Sandstorm: return -0.06f;
                case MapMechanic.LowGravity: return 0.12f;
                default: return 0f;
            }
        }
    }

    /// <summary>Нейтральное значение 1 → плавно к base по силе механики.</summary>
    private float BlendOf(float baseValue)
    {
        return 1f + (baseValue - 1f) * strength;
    }

    // =========================================================
    // PERIODIC HAZARDS
    // =========================================================

    private bool HasPeriodicHazard =>
        mechanic == MapMechanic.Sandstorm ||
        mechanic == MapMechanic.LowGravity;

    private void StartHazards()
    {
        StopHazards();

        hazardRoutine = StartCoroutine(HazardLoop());
    }

    private void StopHazards()
    {
        if (hazardRoutine != null)
        {
            StopCoroutine(hazardRoutine);
            hazardRoutine = null;
        }
    }

    private IEnumerator HazardLoop()
    {
        yield return new WaitForSeconds(hazardFirstDelay);

        while (true)
        {
            if (GameStateManager.Instance == null ||
                GameStateManager.Instance.CurrentState == GameState.Playing)
            {
                SpawnHazardBurst();
            }

            yield return new WaitForSeconds(hazardInterval);
        }
    }

    private void SpawnHazardBurst()
    {
        float radius = HazardRadius;
        float damage = HazardDamage;

        for (int i = 0; i < Mathf.Max(hazardBurst, 1); i++)
        {
            Vector2 offset =
                Random.insideUnitCircle * arenaHalfSize;

            GameObject host = new GameObject("MapHazardZone");

            host.transform.position =
                new Vector3(offset.x, 0f, offset.y);

            HazardZone zone = host.AddComponent<HazardZone>();

            zone.Initialize(
                hazardWarning,
                radius,
                damage,
                hazardDuration,
                hazardGrow
            );
        }
    }

    private float HazardRadius
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.Sandstorm: return 3.4f;
                case MapMechanic.LowGravity: return 2.6f;
                default: return 3f;
            }
        }
    }

    private float HazardDamage
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.Sandstorm: return 22f;
                case MapMechanic.LowGravity: return 30f;
                default: return 25f;
            }
        }
    }

    // =========================================================
    // STATIC QUERIES (для врагов/спавнера; безопасны без Instance)
    // =========================================================

    public static float EnemySpeedScale =>
        Instance != null ? Instance.EnemySpeedMultiplier : 1f;

    public static float EnemyHealthScale =>
        Instance != null ? Instance.EnemyHealthMultiplier : 1f;

    public static float EnemyDamageScale =>
        Instance != null ? Instance.EnemyDamageMultiplier : 1f;

    public static float EnemyCountScale =>
        Instance != null ? Instance.EnemyCountMultiplier : 1f;

    public static RunSpawnBias SpawnBiasType =>
        Instance != null ? Instance.SpawnBias : RunSpawnBias.None;

    public static float SpawnBiasProbability =>
        Instance != null ? Instance.SpawnBiasChance : 0f;
}
