using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Ежедневные миссии: раз в сутки игроку выдаётся набор заданий
/// (минимум три), прогресс копится в течение дня и обнуляется на
/// следующий день.
///
/// Менеджер живёт один на всю игру (DontDestroyOnLoad) и подписан на ту
/// же шину событий, что и достижения (AchievementEvents): убийства,
/// урон игроку, зачистка волн, начало и конец забега. Отдельного канала
/// событий не заводим — сигналы уже собираются в одном месте.
///
/// Состояние дня хранится в PlayerPrefs (JsonUtility), поэтому прогресс
/// переживает перезапуск, а смена даты автоматически пересобирает набор.
/// </summary>
public class DailyMissionManager : MonoBehaviour
{
    public static DailyMissionManager Instance { get; private set; }

    private const string PrefsKey = "ArcadeSurvivor.DailyMissions";

    /// <summary>Минимум миссий в день, требуемый по условию задания.</summary>
    public const int MinimumPerDay = 3;

    [Header("Daily Missions")]
    [Tooltip("Сколько миссий выдаётся в день. Значения меньше 3 поднимаются до 3.")]
    [SerializeField] private int missionsPerDay = MinimumPerDay;

    /// <summary>Прогресс любой миссии изменился (для открытой панели).</summary>
    public event Action OnProgressChanged;

    /// <summary>Набор дня пересобран (сменилась дата) — панель должна перечитать список.</summary>
    public event Action OnMissionsRefreshed;

    /// <summary>Миссия только что выполнена (для всплывающего уведомления).</summary>
    public event Action<DailyMissionSaveData.Entry> OnMissionCompleted;

    private readonly DailyMissionSaveData save = new DailyMissionSaveData();

    // Транзиентное состояние текущего забега — не сохраняется.
    private readonly Dictionary<int, int> eventKillCounts = new Dictionary<int, int>();
    private bool runActive;
    private bool runTookDamage;
    private int runNoDamageKills;

    private bool loaded;

    public int PerDay => Mathf.Max(missionsPerDay, MinimumPerDay);

    // =========================================================
    // LIFECYCLE
    // =========================================================

    public static DailyMissionManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        DailyMissionManager existing = FindAnyObjectByType<DailyMissionManager>();

        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("DailyMissionManager");
        return host.AddComponent<DailyMissionManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadState();
        EnsureToday();

        DailyMissionNotificationUI.EnsureExists();
    }

    private void OnEnable()
    {
        AchievementEvents.EnemyKilled += HandleEnemyKilled;
        AchievementEvents.PlayerDamaged += HandlePlayerDamaged;
        AchievementEvents.WaveCleared += HandleWaveCleared;
        AchievementEvents.RunStarted += HandleRunStarted;
        AchievementEvents.RunEnded += HandleRunEnded;
    }

    private void OnDisable()
    {
        AchievementEvents.EnemyKilled -= HandleEnemyKilled;
        AchievementEvents.PlayerDamaged -= HandlePlayerDamaged;
        AchievementEvents.WaveCleared -= HandleWaveCleared;
        AchievementEvents.RunStarted -= HandleRunStarted;
        AchievementEvents.RunEnded -= HandleRunEnded;
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause)
            SaveState();
    }

    private void OnApplicationQuit()
    {
        SaveState();
    }

    // =========================================================
    // QUERIES
    // =========================================================

    public IReadOnlyList<DailyMissionSaveData.Entry> Missions => save.missions;

    public int TotalCount => save.missions.Count;

    public int CompletedCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < save.missions.Count; i++)
            {
                if (IsCompleted(save.missions[i]))
                    count++;
            }

            return count;
        }
    }

    public int ClaimedCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < save.missions.Count; i++)
            {
                if (save.missions[i] != null && save.missions[i].claimed)
                    count++;
            }

            return count;
        }
    }

    public bool IsCompleted(DailyMissionSaveData.Entry entry)
    {
        return entry != null && entry.progress >= entry.target;
    }

    public bool CanClaim(DailyMissionSaveData.Entry entry)
    {
        return entry != null && IsCompleted(entry) && !entry.claimed;
    }

    public float GetProgress(DailyMissionSaveData.Entry entry)
    {
        return entry != null ? entry.progress : 0f;
    }

    public float GetTarget(DailyMissionSaveData.Entry entry)
    {
        return entry != null ? entry.target : 1f;
    }

    // =========================================================
    // DAILY ROLLOVER
    // =========================================================

    /// <summary>
    /// Гарантирует, что активен набор именно на сегодняшнюю дату.
    /// Вызывается на старте, при запуске забега и при открытии панели,
    /// чтобы миссии сменились, даже если игра не перезапускалась сутки.
    /// </summary>
    public void EnsureToday()
    {
        string today = TodayString();

        if (save.date == today && save.missions.Count > 0)
            return;

        save.date = today;
        save.missions = DailyMissionGenerator.Generate(DateTime.Now, PerDay);

        runActive = false;
        runTookDamage = false;
        runNoDamageKills = 0;
        eventKillCounts.Clear();

        SaveState();

        Debug.Log($"[DailyMissions] Новый набор на {today}: {save.missions.Count} миссий.");

        OnMissionsRefreshed?.Invoke();
        OnProgressChanged?.Invoke();
    }

    // =========================================================
    // EVENT HANDLERS
    // =========================================================

    private void HandleEnemyKilled(EnemyKillInfo info)
    {
        if (!runActive)
            return;

        bool changed = false;

        for (int i = 0; i < save.missions.Count; i++)
        {
            DailyMissionSaveData.Entry entry = save.missions[i];

            if (entry == null || entry.claimed || entry.completed)
                continue;

            switch (entry.Objective)
            {
                case DailyMissionObjective.KillEnemies:
                    AddProgress(entry, 1f);
                    changed = true;
                    break;

                case DailyMissionObjective.KillByDamageSource:
                    if (info.source == entry.Source)
                    {
                        AddProgress(entry, 1f);
                        changed = true;
                    }
                    break;

                case DailyMissionObjective.KillBosses:
                    if (info.isBoss)
                    {
                        AddProgress(entry, 1f);
                        changed = true;
                    }
                    break;

                case DailyMissionObjective.KillsInSingleEvent:
                    if (info.sourceEventId != 0)
                    {
                        int count = IncrementEventCount(info.sourceEventId);

                        if (count > entry.progress)
                        {
                            entry.progress = count;
                            changed = true;
                        }
                    }
                    break;

                case DailyMissionObjective.KillsWithoutDamage:
                    if (!runTookDamage)
                    {
                        runNoDamageKills++;

                        if (runNoDamageKills > entry.progress)
                        {
                            entry.progress = runNoDamageKills;
                            changed = true;
                        }
                    }
                    break;
            }

            EvaluateCompletion(entry);
        }

        if (changed)
            OnProgressChanged?.Invoke();
    }

    private void HandlePlayerDamaged()
    {
        if (!runActive)
            return;

        runTookDamage = true;
        runNoDamageKills = 0;
    }

    private void HandleWaveCleared(int wave, bool wasPerfect)
    {
        if (!runActive)
            return;

        bool changed = false;

        for (int i = 0; i < save.missions.Count; i++)
        {
            DailyMissionSaveData.Entry entry = save.missions[i];

            if (entry == null || entry.claimed || entry.completed)
                continue;

            switch (entry.Objective)
            {
                case DailyMissionObjective.PerfectWaves:
                    if (wasPerfect)
                    {
                        AddProgress(entry, 1f);
                        changed = true;
                    }
                    break;

                case DailyMissionObjective.ReachWave:
                    if (wave > entry.progress)
                    {
                        entry.progress = wave;
                        changed = true;
                    }
                    break;
            }

            EvaluateCompletion(entry);
        }

        if (changed)
            OnProgressChanged?.Invoke();
    }

    private void HandleRunStarted()
    {
        EnsureToday();

        runActive = true;
        runTookDamage = false;
        runNoDamageKills = 0;
        eventKillCounts.Clear();

        OnProgressChanged?.Invoke();
    }

    private void HandleRunEnded(RunEndInfo info)
    {
        if (!runActive)
            return;

        bool changed = false;

        for (int i = 0; i < save.missions.Count; i++)
        {
            DailyMissionSaveData.Entry entry = save.missions[i];

            if (entry == null || entry.claimed || entry.completed)
                continue;

            if (entry.Objective == DailyMissionObjective.CompleteRuns && info.won)
            {
                AddProgress(entry, 1f);
                changed = true;
            }
            else if (entry.Objective == DailyMissionObjective.ReachWave &&
                     info.waveReached > entry.progress)
            {
                entry.progress = info.waveReached;
                changed = true;
            }

            EvaluateCompletion(entry);
        }

        runActive = false;

        SaveState();

        if (changed)
            OnProgressChanged?.Invoke();
    }

    // =========================================================
    // PROGRESS MUTATION
    // =========================================================

    private static void AddProgress(DailyMissionSaveData.Entry entry, float amount)
    {
        entry.progress += amount;
    }

    private void EvaluateCompletion(DailyMissionSaveData.Entry entry)
    {
        if (entry == null || entry.completed)
            return;

        if (entry.progress < entry.target)
            return;

        entry.completed = true;

        Debug.Log($"[DailyMissions] Выполнено: {entry.id}");

        OnMissionCompleted?.Invoke(entry);

        // Фиксируем результат сразу: награду ещё могут не забрать,
        // а выполнение не должно потеряться при перезапуске.
        SaveState();
    }

    private int IncrementEventCount(int eventId)
    {
        int count;
        eventKillCounts.TryGetValue(eventId, out count);
        count++;
        eventKillCounts[eventId] = count;

        return count;
    }

    // =========================================================
    // CLAIM
    // =========================================================

    /// <summary>
    /// Забирает награду за выполненную миссию. Идемпотентно: повторный
    /// вызов для уже полученной миссии ничего не делает.
    /// </summary>
    public bool Claim(DailyMissionSaveData.Entry entry)
    {
        if (!CanClaim(entry))
            return false;

        XpManager xp = XpManager.Instance;
        if (xp == null)
        {
            Debug.LogWarning("[DailyMissions] XpManager.Instance is null — награда не выдана.");
            return false;
        }

        if (entry.coinReward > 0)
            xp.AddGlobalCoins(entry.coinReward);

        if (entry.xpReward > 0)
            xp.AddPlayerXP(entry.xpReward);

        entry.claimed = true;
        SaveState();

        OnProgressChanged?.Invoke();

        return true;
    }

    // =========================================================
    // PERSISTENCE
    // =========================================================

    private void LoadState()
    {
        if (loaded)
            return;

        loaded = true;

        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);

        if (string.IsNullOrEmpty(json))
            return;

        try
        {
            DailyMissionSaveData parsed =
                JsonUtility.FromJson<DailyMissionSaveData>(json);

            if (parsed == null)
                return;

            save.date = parsed.date ?? string.Empty;
            save.missions = parsed.missions ?? new List<DailyMissionSaveData.Entry>();

            for (int i = 0; i < save.missions.Count; i++)
            {
                if (save.missions[i] == null)
                {
                    save.missions.RemoveAt(i);
                    i--;
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DailyMissions] Не удалось прочитать сохранение: " + e.Message);
            save.date = string.Empty;
            save.missions = new List<DailyMissionSaveData.Entry>();
        }
    }

    public void SaveState()
    {
        try
        {
            PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(save));
            PlayerPrefs.Save();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[DailyMissions] Не удалось сохранить: " + e.Message);
        }
    }

    // =========================================================
    // DEBUG / EDITOR
    // =========================================================

    /// <summary>Полный сброс ежедневных миссий (для отладки/меню).</summary>
    public void ResetAll()
    {
        save.date = string.Empty;
        save.missions.Clear();

        eventKillCounts.Clear();

        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();

        EnsureToday();
    }

    private static string TodayString()
    {
        return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
