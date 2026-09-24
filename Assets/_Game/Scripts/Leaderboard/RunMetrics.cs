using UnityEngine;

/// <summary>
/// Сборщик метрик забега в реальном времени.
/// Отслеживает все ключевые показатели для лидерборда.
/// </summary>
public class RunMetrics : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private ComboSystem comboSystem;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private AbilityManager abilityManager;

    // =========================================================
    // METRICS
    // =========================================================

    private float runStartTime;
    private bool runStarted;

    private int killsThisRun;
    private float totalKillTime;
    private float fastestKillTime;
    private float timeToFirstKill;
    private bool firstKillReached;

    private float timeToFirstBoss;
    private bool firstBossReached;

    private float currentWaveStartTime;
    private float bestWaveClearTime;
    private int perfectWaves;

    private float totalDamageDealt;
    private float totalDamageTaken;
    private int criticalHits;
    private int maxCombo;
    private int noHitKillStreak;
    private int abilitiesUsed;
    private int dashDodges;
    private int multiKillStreaks;

    // =========================================================
    // MOVEMENT METRICS
    // =========================================================

    private float totalDistanceTraveled;
    private float lastPosition;
    private int totalDashDistance;
    private int movementBurstCount;

    // =========================================================
    // PROPERTIES
    // =========================================================

    public float RunTime => runStarted ? Time.time - runStartTime : 0f;
    public int Kills => killsThisRun;
    public float AverageKillTime => killsThisRun > 0 ? totalKillTime / killsThisRun : 0f;
    public float FastestKillTime => fastestKillTime < float.MaxValue ? fastestKillTime : 0f;
    public float TimeToFirstKill => timeToFirstKill < float.MaxValue ? timeToFirstKill : 0f;
    public float TimeToFirstBoss => timeToFirstBoss < float.MaxValue ? timeToFirstBoss : 0f;
    public float BestWaveClearTime => bestWaveClearTime < float.MaxValue ? bestWaveClearTime : 0f;
    public int PerfectWaves => perfectWaves;
    public float TotalDamageDealt => totalDamageDealt;
    public float TotalDamageTaken => totalDamageTaken;
    public int CriticalHits => criticalHits;
    public int MaxCombo => maxCombo;
    public int NoHitKillStreak => noHitKillStreak;
    public int AbilitiesUsed => abilitiesUsed;
    public int DashDodges => dashDodges;
    public int MultiKillStreaks => multiKillStreaks;
    public float TotalDistance => totalDistanceTraveled;
    public int TotalDashDistance => totalDashDistance;

    public event System.Action<float> OnDamageDealt;
    public event System.Action<float> OnDamageTaken;
    public event System.Action<int> OnDashDodged;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        if (comboSystem == null)
            comboSystem = GetComponent<ComboSystem>();

        ResetAll();
    }

    private void OnEnable()
    {
        if (scoreManager != null)
            scoreManager.OnScoreAdded += OnScoreAdded;

        if (comboSystem != null)
            comboSystem.OnComboChanged += OnComboChanged;
    }

    private void OnDisable()
    {
        if (scoreManager != null)
            scoreManager.OnScoreAdded -= OnScoreAdded;

        if (comboSystem != null)
            comboSystem.OnComboChanged -= OnComboChanged;
    }

    /// <summary>
    /// Вызывается при начале забега.
    /// </summary>
    public void StartRun()
    {
        runStarted = true;
        runStartTime = Time.time;
        currentWaveStartTime = Time.time;
        lastPosition = transform != null ? transform.position.x : 0f;
    }

    /// <summary>
    /// Обновление метрик движений (вызывать каждый кадр).
    /// </summary>
    private void Update()
    {
        if (!runStarted) return;

        // Отслеживаем расстояние
        if (transform != null)
        {
            float currentX = transform.position.x;
            float delta = Mathf.Abs(currentX - lastPosition);
            totalDistanceTraveled += delta;
            lastPosition = currentX;

            // Бонус за рывки
            if (delta > 5f) // Большой рывок
            {
                totalDashDistance += Mathf.RoundToInt(delta);
            }
        }
    }

    /// <summary>
    /// Вызывается при смерти врага.
    /// </summary>
    public void OnEnemyDied(Enemy enemy, float damageDealt, bool isCritical)
    {
        if (!runStarted) return;

        killsThisRun++;

        float killTime = Time.time - runStartTime;
        totalKillTime += killTime;

        if (fastestKillTime == float.MaxValue || killTime < fastestKillTime)
            fastestKillTime = killTime;

        if (!firstKillReached)
        {
            timeToFirstKill = killTime;
            firstKillReached = true;
        }

        if (enemy != null && enemy.GetEnemyData() != null)
        {
            if (enemy.GetEnemyData().EnemyType == EnemyType.Boss && !firstBossReached)
            {
                timeToFirstBoss = killTime;
                firstBossReached = true;
            }
        }

        if (isCritical)
            criticalHits++;

        totalDamageDealt += damageDealt;
        OnDamageDealt?.Invoke(damageDealt);
    }

    /// <summary>
    /// Вызывается при получении урона игроком.
    /// </summary>
    public void OnPlayerDamaged(float damage)
    {
        if (!runStarted) return;

        totalDamageTaken += damage;
        OnDamageTaken?.Invoke(damage);
    }

    /// <summary>
    /// Вызывается при использовании способности.
    /// </summary>
    public void OnAbilityUsed()
    {
        if (!runStarted) return;
        abilitiesUsed++;
    }

    /// <summary>
    /// Вызывается при увороте через dash.
    /// </summary>
    public void RecordDashDodged()
    {
        if (!runStarted) return;

        dashDodges++;
        OnDashDodged?.Invoke(dashDodges);
    }

    /// <summary>
    /// Вызывается при изменении комбо.
    /// </summary>
    private void OnComboChanged(int combo)
    {
        if (combo > maxCombo)
            maxCombo = combo;

        if (combo > noHitKillStreak)
            noHitKillStreak = combo;
    }

    /// <summary>
    /// Вызывается при добавлении очков.
    /// </summary>
    private void OnScoreAdded(int amount, string bonusName)
    {
        // Можно использовать для дополнительных метрик
    }

    /// <summary>
    /// Вызывается при завершении волны.
    /// </summary>
    public void OnWaveCleared(bool wasPerfect)
    {
        if (!runStarted) return;

        float waveClearTime = Time.time - currentWaveStartTime;

        if (bestWaveClearTime == float.MaxValue || waveClearTime < bestWaveClearTime)
            bestWaveClearTime = waveClearTime;

        if (wasPerfect)
        {
            perfectWaves++;
            if (comboSystem != null)
                comboSystem.OnPerfectWave();
        }

        currentWaveStartTime = Time.time;
    }

    /// <summary>
    /// Вызывается при мульти-килле.
    /// </summary>
    public void OnMultiKill(int count)
    {
        if (!runStarted) return;
        multiKillStreaks++;
    }

    /// <summary>
    /// Собирает все метрики в RunResult.
    /// </summary>
    public RunResult CollectResult()
    {
        RunResult result = new RunResult
        {
            score = scoreManager != null ? scoreManager.Score : 0,
            kills = killsThisRun,
            wavesCleared = waveManager != null ? waveManager.CurrentWave : 0,
            runTime = RunTime,
            maxCombo = maxCombo,
            totalCriticalHits = criticalHits,
            averageKillTime = AverageKillTime,
            fastestKillTime = FastestKillTime,
            timeToFirstKill = TimeToFirstKill,
            timeToFirstBoss = TimeToFirstBoss,
            bestWaveClearTime = BestWaveClearTime,
            perfectWaves = perfectWaves,
            noHitKillStreak = noHitKillStreak,
            totalDamageDealt = totalDamageDealt,
            totalDamageTaken = totalDamageTaken,
            abilitiesUsed = abilitiesUsed,
            dashDodges = dashDodges,
            multiKillStreaks = multiKillStreaks,
            totalDistance = totalDistanceTraveled,
            totalDashDistance = totalDashDistance,
        };

        if (totalDamageTaken > 0f)
            result.damageEfficiency = totalDamageDealt / totalDamageTaken;

        result.CalculateRank();
        result.CalculateStyleBonus();

        return result;
    }

    /// <summary>
    /// Полный сброс метрик.
    /// </summary>
    public void ResetAll()
    {
        runStarted = false;
        runStartTime = 0f;

        killsThisRun = 0;
        totalKillTime = 0f;
        fastestKillTime = float.MaxValue;
        timeToFirstKill = float.MaxValue;
        firstKillReached = false;

        timeToFirstBoss = float.MaxValue;
        firstBossReached = false;

        currentWaveStartTime = 0f;
        bestWaveClearTime = float.MaxValue;
        perfectWaves = 0;

        totalDamageDealt = 0f;
        totalDamageTaken = 0f;
        criticalHits = 0;
        maxCombo = 0;
        noHitKillStreak = 0;
        abilitiesUsed = 0;
        dashDodges = 0;
        multiKillStreaks = 0;

        totalDistanceTraveled = 0f;
        lastPosition = 0f;
        totalDashDistance = 0;
    }
}
