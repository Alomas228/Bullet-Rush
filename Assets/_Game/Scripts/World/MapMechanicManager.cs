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
///   • мировые механики, заметно меняющие восприятие карты:
///     пляж — волны прибоя, горы — камнепады, город — машины,
///     пустыня — зоны урона с затемнением бури, космос —
///     метеоры с гравитационными ямами;
///   • тинт местных врагов (EnemyTintColor) и баннер входа на карту.
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

    [Header("Surf (Пляж) — волна прибоя")]
    [SerializeField] private float surfInterval = 12f;
    [SerializeField] private float surfTravelTime = 1.9f;
    [SerializeField] private float surfHalfThickness = 3f;
    [SerializeField] private float surfKnockback = 15f;

    [Header("Boulder (Горы) — камнепад")]
    [SerializeField] private float boulderInterval = 9f;
    [SerializeField] private int boulderLanes = 2;
    [SerializeField] private float boulderWarning = 1.4f;
    [SerializeField] private float boulderHalfThickness = 2.5f;
    [SerializeField] private float boulderDamage = 26f;
    [SerializeField] private float boulderKnockback = 16f;

    [Header("Traffic (Город) — машины")]
    [SerializeField] private float trafficInterval = 13f;
    [SerializeField] private float trafficWarning = 1.5f;
    [SerializeField] private float trafficSpeed = 42f;
    [SerializeField] private float trafficDamage = 70f;
    [SerializeField] private float trafficKnockback = 22f;

    [Header("Meteor (Космос) — метеоры и гравитация")]
    [SerializeField] private float meteorInterval = 10f;
    [SerializeField] private int meteorStrikes = 1;
    [SerializeField] private float meteorWarning = 1.5f;
    [SerializeField] private float meteorRadius = 3.2f;
    [SerializeField] private float meteorDamage = 40f;
    [SerializeField] private float meteorShake = 0.5f;
    [SerializeField] private bool meteorGravity = true;
    [SerializeField] private float gravityStrength = 4f;
    [SerializeField] private float gravityDuration = 4f;
    [SerializeField] private float gravityRadiusGrow = 1.25f;
    [SerializeField] private float dashReductionPercent = 0.2f;

    [Header("Storm (Пустыня) — затемнение")]
    [SerializeField] private float stormOverlayIntensity = 0.38f;
    [SerializeField] private float stormOverlayFadeIn = 0.8f;
    [SerializeField] private float stormOverlayHold = 0.9f;
    [SerializeField] private float stormOverlayFadeOut = 1.4f;

    private MapMechanic mechanic = MapMechanic.None;
    private float strength = 1f;
    private Coroutine hazardRoutine;
    private WaveEventDirector waveEvents;

    // Событие волны и залп механики — два независимых источника
    // давления. Их наложение превращает читаемую угрозу в хаос, где
    // неясно, от чего уходить. Поэтому залп ждёт, пока событие волны
    // закончится, и не пропадает — сработает на следующем круге.
    private WaveEventDirector WaveEvents
    {
        get
        {
            if (waveEvents == null)
                waveEvents =
                    FindAnyObjectByType<WaveEventDirector>();

            return waveEvents;
        }
    }

    // =========================================================
    // STATIC QUERIES (для врагов/спавнера; безопасны без Instance)
    // =========================================================

    /// <summary>Механика текущей карты (None — лес без правил).</summary>
    public static MapMechanic CurrentMechanic =>
        Instance != null ? Instance.mechanic : MapMechanic.None;

    /// <summary>
    /// Тинт врагов местности: мультипликативный оттенок, которым карта
    /// красит всех своих мобов. Враги читаются «здешними», а редкие
    /// варианты остаются узнаваемыми поверх местного окраса.
    /// </summary>
    public static Color EnemyTintColor =>
        TintOf(CurrentMechanic);

    /// <summary>Цвет подрыва фугаса-бомбера.</summary>
    public static readonly Color BomberBlastColor =
        new Color(1f, 0.5f, 0.12f, 1f);

    private static Color TintOf(MapMechanic target)
    {
        switch (target)
        {
            case MapMechanic.OpenField:
                return new Color(1.08f, 1f, 0.82f);

            case MapMechanic.Highland:
                return new Color(0.96f, 0.98f, 1.06f);

            case MapMechanic.Urban:
                return new Color(0.96f, 0.94f, 1.06f);

            case MapMechanic.Sandstorm:
                return new Color(1.12f, 0.96f, 0.7f);

            case MapMechanic.LowGravity:
                return new Color(0.9f, 0.85f, 1.15f);

            default:
                return new Color(0.92f, 1.04f, 0.94f);
        }
    }

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

        AnnounceMap(map);

        Debug.Log($"MAP MECHANIC: {mechanic} (strength {strength:0.##})");
    }

    /// <summary>
    /// Баннер входа на карту: название локации и подпись её механики.
    /// HUD может ещё не существовать в сцене — тогда просто молчим.
    /// </summary>
    private void AnnounceMap(GameMap map)
    {
        GameplayHUD hud = FindAnyObjectByType<GameplayHUD>();

        if (hud == null)
            return;

        string mechanicName =
            MapMechanicText.Description(mechanic);

        hud.ShowMapAnnouncement(
            map.LocalizedName,
            string.IsNullOrEmpty(mechanicName)
                ? null
                : mechanicName
        );
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

        // Космос: метеоры заставляют много рыскать — рывок остывает
        // быстрее. Это игровой ответ на частые взрывы, а не набор цифр.
        if (mechanic == MapMechanic.LowGravity &&
            dashReductionPercent > 0f)
        {
            PlayerController controller =
                FindAnyObjectByType<PlayerController>();

            if (controller != null)
            {
                controller.ReduceDashCooldown(
                    dashReductionPercent *
                    Mathf.Clamp01(strength)
                );
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
        mechanic == MapMechanic.OpenField ||
        mechanic == MapMechanic.Highland ||
        mechanic == MapMechanic.Urban ||
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
            bool playing =
                GameStateManager.Instance == null ||
                GameStateManager.Instance.CurrentState == GameState.Playing;

            WaveEventDirector events = WaveEvents;
            bool eventBusy = events != null && events.IsEventActive;

            if (playing && !eventBusy)
                SpawnHazardBurst();

            yield return new WaitForSeconds(LoopInterval);
        }
    }

    /// <summary>
    /// Свой период залпа у каждой механики: волна реже, камнепады
    /// чаще — арена не должна превращаться в бесконечную чехарду.
    /// </summary>
    private float LoopInterval
    {
        get
        {
            switch (mechanic)
            {
                case MapMechanic.OpenField: return surfInterval;
                case MapMechanic.Highland: return boulderInterval;
                case MapMechanic.Urban: return trafficInterval;
                case MapMechanic.LowGravity: return meteorInterval;
                default: return hazardInterval;
            }
        }
    }

    private void SpawnHazardBurst()
    {
        switch (mechanic)
        {
            case MapMechanic.OpenField:
                SpawnSurfWave();
                break;

            case MapMechanic.Highland:
                SpawnBoulderLanes();
                break;

            case MapMechanic.Urban:
                SpawnTraffic();
                break;

            case MapMechanic.Sandstorm:
                SpawnStormBurst();
                MapStormOverlay.Pulse(
                    stormOverlayIntensity,
                    stormOverlayFadeIn,
                    stormOverlayHold,
                    stormOverlayFadeOut
                );
                break;

            case MapMechanic.LowGravity:
                SpawnMeteorBurst();
                break;
        }
    }

    // ---------------------------------------------------------
    // ПЛЯЖ: волна прибоя раскатывает мобов с края до края
    // ---------------------------------------------------------

    private void SpawnSurfWave()
    {
        bool alongZ =
            Random.value < 0.5f;

        MapLaneSweep.LaneAxis axis =
            alongZ
                ? MapLaneSweep.LaneAxis.Horizontal
                : MapLaneSweep.LaneAxis.Vertical;

        Vector3 center = new Vector3(
            alongZ ? 0f : Random.Range(-arenaHalfSize, arenaHalfSize),
            0f,
            alongZ ? Random.Range(-arenaHalfSize, arenaHalfSize) : 0f
        );

        GameObject host = new GameObject("MapSurfWave");

        host.transform.position = center;

        MapLaneSweep wave = host.AddComponent<MapLaneSweep>();

        wave.Initialize(
            center,
            axis,
            arenaHalfSize * 1.15f,
            surfHalfThickness,
            arenaHalfSize * 1.8f,
            surfTravelTime,
            0f,
            surfKnockback,
            0f,
            // HDR-цвет: шейдер аддитивный, поэтому >1 rgb даёт
            // заметный светлый гребень даже на ярком песке.
            new Color(1.1f, 1.9f, 2.6f, 1f)
        );
    }

    // ---------------------------------------------------------
    // ГОРЫ: валуны падают по случайным полосам после телеграфа
    // ---------------------------------------------------------

    private void SpawnBoulderLanes()
    {
        int lanes = Mathf.Max(boulderLanes, 1);

        for (int i = 0; i < lanes; i++)
        {
            bool alongZ =
                Random.value < 0.5f;

            MapLaneSweep.LaneAxis axis =
                alongZ
                    ? MapLaneSweep.LaneAxis.Horizontal
                    : MapLaneSweep.LaneAxis.Vertical;

            Vector3 center = new Vector3(
                Random.Range(-arenaHalfSize, arenaHalfSize),
                0f,
                Random.Range(-arenaHalfSize, arenaHalfSize)
            );

            GameObject host = new GameObject("MapBoulderLane");

            host.transform.position = center;

            MapLaneSweep lane = host.AddComponent<MapLaneSweep>();

            lane.Initialize(
                center,
                axis,
                arenaHalfSize * 1.1f,
                boulderHalfThickness,
                0f,
                0f,
                boulderDamage,
                boulderKnockback,
                boulderWarning,
                new Color(0.72f, 0.7f, 0.66f, 1f)
            );
        }
    }

    // ---------------------------------------------------------
    // ГОРОД: машины проезжают полосы после предупреждения
    // ---------------------------------------------------------

    private void SpawnTraffic()
    {
        bool alongZ =
            Random.value < 0.5f;

        Vector3 center = new Vector3(
            alongZ
                ? Random.Range(-arenaHalfSize, arenaHalfSize)
                : 0f,
            0f,
            alongZ
                ? 0f
                : Random.Range(-arenaHalfSize, arenaHalfSize)
        );

        MapTrafficRush.Launch(
            center,
            alongZ
                ? MapLaneSweep.LaneAxis.Horizontal
                : MapLaneSweep.LaneAxis.Vertical,
            arenaHalfSize * 0.85f,
            arenaHalfSize * 2f,
            trafficSpeed,
            trafficWarning,
            trafficDamage,
            trafficKnockback,
            new Color(1f, 0.55f, 0.2f, 1f)
        );
    }

    // ---------------------------------------------------------
    // ПУСТЫНЯ: классические зоны урона (песочные круги)
    // ---------------------------------------------------------

    private void SpawnStormBurst()
    {
        float radius = HazardRadius;
        float damage = HazardDamage;

        int count = Mathf.Max(hazardBurst, 1);

        // Итоговый радиус зоны после роста: по нему считаем зазор,
        // иначе зона, поставленная «рядом» с игроком, дотягивается
        // до него при расширении — это уже не задача «обойди», а
        // ловушка без выхода.
        float finalRadius =
            radius * Mathf.Max(hazardGrow, 1f);

        float minFromPlayer = finalRadius * 1.2f;
        float minBetween = finalRadius * 1.1f;

        Vector2 playerXZ = PlayerPositionXZ();

        Vector2[] placed = new Vector2[count];
        int placedCount = 0;

        for (int i = 0; i < count; i++)
        {
            if (!TryFindStormCenter(
                    playerXZ,
                    minFromPlayer,
                    minBetween,
                    placed,
                    placedCount,
                    out Vector2 center))
            {
                continue;
            }

            placed[placedCount++] = center;

            GameObject host = new GameObject("MapHazardZone");

            host.transform.position =
                new Vector3(center.x, 0f, center.y);

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

    // Центр песчаной зоны: внутри арены, не ближе minFromPlayer к
    // игроку и не ближе minBetween к уже поставленным зонам. Две
    // зоны в одной точке читаются как одна — теряется смысл залпа.
    private bool TryFindStormCenter(
        Vector2 playerXZ,
        float minFromPlayer,
        float minBetween,
        Vector2[] placed,
        int placedCount,
        out Vector2 center)
    {
        const int attempts = 24;

        bool hasPlayer =
            PlayerHealth.Instance != null;

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Vector2 candidate =
                Random.insideUnitCircle * arenaHalfSize;

            if (hasPlayer &&
                (candidate - playerXZ).sqrMagnitude <
                minFromPlayer * minFromPlayer)
            {
                continue;
            }

            bool tooClose = false;

            for (int i = 0; i < placedCount; i++)
            {
                if ((candidate - placed[i]).sqrMagnitude <
                    minBetween * minBetween)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose)
                continue;

            center = candidate;

            return true;
        }

        center = Vector2.zero;

        return false;
    }

    private static Vector2 PlayerPositionXZ()
    {
        PlayerHealth player = PlayerHealth.Instance;

        if (player == null)
            return Vector2.zero;

        Vector3 position = player.transform.position;

        return new Vector2(position.x, position.z);
    }

    // ---------------------------------------------------------
    // КОСМОС: метеоры с телеграфом, взрывом и гравитационной ямой
    // ---------------------------------------------------------

    private void SpawnMeteorBurst()
    {
        int strikes = Mathf.Max(meteorStrikes, 1);

        for (int i = 0; i < strikes; i++)
        {
            Vector2 offset =
                Random.insideUnitCircle * (arenaHalfSize - 2f);

            Vector3 center = new Vector3(
                offset.x,
                0f,
                offset.y
            );

            MapStrikeZone.Strike(
                center,
                meteorRadius,
                meteorDamage,
                damagesEnemies: true,
                knockback: 12f,
                screenShake: meteorShake,
                color: new Color(1f, 0.42f, 0.15f, 1f),
                warning: meteorWarning,
                gravityWell: meteorGravity,
                gravityRadius: meteorRadius * gravityRadiusGrow,
                gravityStrength: gravityStrength,
                gravityDuration: gravityDuration
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
