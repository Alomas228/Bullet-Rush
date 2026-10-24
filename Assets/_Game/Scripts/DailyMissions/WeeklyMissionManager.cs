using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

/// <summary>
/// Weekly mission track. Uses the same gameplay event bus as daily missions,
/// but stores an independent period and claim state.
/// </summary>
public sealed class WeeklyMissionManager : MonoBehaviour
{
    public static WeeklyMissionManager Instance { get; private set; }

    private const string PrefsKey = "ArcadeSurvivor.WeeklyMissions";
    private const int MissionsPerWeek = 4;
    private const float RewardMultiplier = 2f;

    private readonly DailyMissionSaveData save = new DailyMissionSaveData();
    private readonly Dictionary<int, int> eventKillCounts = new Dictionary<int, int>();
    private bool runActive;
    private bool runTookDamage;
    private int runNoDamageKills;

    public event Action OnProgressChanged;
    public event Action OnMissionsRefreshed;
    public event Action<DailyMissionSaveData.Entry> OnMissionCompleted;

    public IReadOnlyList<DailyMissionSaveData.Entry> Missions => save.missions;
    public int TotalCount => save.missions.Count;
    public int CompletedCount => CountCompleted();
    public int ClaimedCount => CountClaimed();
    public string CurrentPeriodKey => save.date;

    public static WeeklyMissionManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        WeeklyMissionManager existing = FindAnyObjectByType<WeeklyMissionManager>();
        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("WeeklyMissionManager");
        return host.AddComponent<WeeklyMissionManager>();
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
        EnsureCurrentWeek();
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

    public void EnsureCurrentWeek()
    {
        string key = WeekStart(DateTime.Now).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (save.date == key && HasCurrentWeeklyPool())
            return;

        DateTime start = DateTime.ParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        save.date = key;
        save.missions = DailyMissionGenerator.GenerateWeekly(start, MissionsPerWeek);
        for (int i = 0; i < save.missions.Count; i++)
        {
            DailyMissionSaveData.Entry entry = save.missions[i];
            entry.coinReward = Mathf.RoundToInt(entry.coinReward * RewardMultiplier);
            entry.xpReward = Mathf.RoundToInt(entry.xpReward * RewardMultiplier);
        }

        ResetRunState();
        SaveState();
        OnMissionsRefreshed?.Invoke();
        OnProgressChanged?.Invoke();
    }

    private bool HasCurrentWeeklyPool()
    {
        if (save.missions == null || save.missions.Count == 0)
            return false;

        for (int i = 0; i < save.missions.Count; i++)
        {
            DailyMissionSaveData.Entry entry = save.missions[i];
            if (entry == null || !WeeklyMissionPool.ContainsId(entry.id))
                return false;
        }

        return true;
    }

    public bool IsCompleted(DailyMissionSaveData.Entry entry) =>
        entry != null && entry.progress >= entry.target;

    public bool CanClaim(DailyMissionSaveData.Entry entry) =>
        entry != null && IsCompleted(entry) && !entry.claimed;

    public bool Claim(DailyMissionSaveData.Entry entry)
    {
        EnsureCurrentWeek();
        if (!CanClaim(entry))
            return false;

        XpManager xp = XpManager.Instance;
        if (xp == null)
        {
            Debug.LogWarning("[WeeklyMissions] XpManager.Instance is null — reward not granted.");
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

    private void HandleEnemyKilled(EnemyKillInfo info)
    {
        if (!runActive) return;
        bool changed = false;
        for (int i = 0; i < save.missions.Count; i++)
        {
            var entry = save.missions[i];
            if (entry == null || entry.claimed || entry.completed) continue;
            switch (entry.Objective)
            {
                case DailyMissionObjective.KillEnemies:
                    AddProgress(entry, 1f); changed = true; break;
                case DailyMissionObjective.KillByDamageSource:
                    if (info.source == entry.Source) { AddProgress(entry, 1f); changed = true; } break;
                case DailyMissionObjective.KillBosses:
                    if (info.isBoss) { AddProgress(entry, 1f); changed = true; } break;
                case DailyMissionObjective.KillsInSingleEvent:
                    if (info.sourceEventId != 0)
                    {
                        int count = IncrementEventCount(info.sourceEventId);
                        if (count > entry.progress) { entry.progress = count; changed = true; }
                    }
                    break;
                case DailyMissionObjective.KillsWithoutDamage:
                    if (!runTookDamage)
                    {
                        runNoDamageKills++;
                        if (runNoDamageKills > entry.progress) { entry.progress = runNoDamageKills; changed = true; }
                    }
                    break;
            }
            EvaluateCompletion(entry);
        }
        if (changed) { SaveState(); OnProgressChanged?.Invoke(); }
    }

    private void HandlePlayerDamaged()
    {
        if (!runActive) return;
        runTookDamage = true;
        runNoDamageKills = 0;
    }

    private void HandleWaveCleared(int wave, bool perfect)
    {
        if (!runActive) return;
        bool changed = false;
        for (int i = 0; i < save.missions.Count; i++)
        {
            var entry = save.missions[i];
            if (entry == null || entry.claimed || entry.completed) continue;
            if (entry.Objective == DailyMissionObjective.PerfectWaves && perfect) { AddProgress(entry, 1f); changed = true; }
            if (entry.Objective == DailyMissionObjective.ReachWave && wave > entry.progress) { entry.progress = wave; changed = true; }
            EvaluateCompletion(entry);
        }
        if (changed) { SaveState(); OnProgressChanged?.Invoke(); }
    }

    private void HandleRunStarted() { EnsureCurrentWeek(); ResetRunState(); runActive = true; OnProgressChanged?.Invoke(); }

    private void HandleRunEnded(RunEndInfo info)
    {
        if (!runActive) return;
        bool changed = false;
        for (int i = 0; i < save.missions.Count; i++)
        {
            var entry = save.missions[i];
            if (entry == null || entry.claimed || entry.completed) continue;
            if (entry.Objective == DailyMissionObjective.CompleteRuns && info.won) { AddProgress(entry, 1f); changed = true; }
            if (entry.Objective == DailyMissionObjective.ReachWave && info.waveReached > entry.progress) { entry.progress = info.waveReached; changed = true; }
            EvaluateCompletion(entry);
        }
        runActive = false;
        SaveState();
        if (changed) OnProgressChanged?.Invoke();
    }

    private void EvaluateCompletion(DailyMissionSaveData.Entry entry)
    {
        if (entry == null || entry.completed || entry.progress < entry.target) return;
        entry.completed = true;
        OnMissionCompleted?.Invoke(entry);
        SaveState();
    }

    private int IncrementEventCount(int id)
    {
        eventKillCounts.TryGetValue(id, out int count);
        eventKillCounts[id] = ++count;
        return count;
    }

    private void ResetRunState()
    {
        runActive = false;
        runTookDamage = false;
        runNoDamageKills = 0;
        eventKillCounts.Clear();
    }

    private int CountCompleted()
    {
        int count = 0;
        for (int i = 0; i < save.missions.Count; i++) if (IsCompleted(save.missions[i])) count++;
        return count;
    }

    private int CountClaimed()
    {
        int count = 0;
        for (int i = 0; i < save.missions.Count; i++) if (save.missions[i] != null && save.missions[i].claimed) count++;
        return count;
    }

    private static void AddProgress(DailyMissionSaveData.Entry entry, float amount) => entry.progress += amount;

    private static DateTime WeekStart(DateTime date)
    {
        int daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }

    private void LoadState()
    {
        string json = PlayerPrefs.GetString(PrefsKey, string.Empty);
        if (string.IsNullOrEmpty(json)) return;
        try
        {
            DailyMissionSaveData loaded = JsonUtility.FromJson<DailyMissionSaveData>(json);
            if (loaded != null && loaded.missions != null)
            {
                save.date = loaded.date ?? string.Empty;
                save.missions = loaded.missions;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("[WeeklyMissions] Could not load state: " + exception.Message);
        }
    }

    public void SaveState()
    {
        PlayerPrefs.SetString(PrefsKey, JsonUtility.ToJson(save));
        PlayerPrefs.Save();
    }

    public void ResetAll()
    {
        save.date = string.Empty;
        save.missions.Clear();
        ResetRunState();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
        EnsureCurrentWeek();
    }
}
