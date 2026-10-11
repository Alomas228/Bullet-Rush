using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Единый обработчик прогресса достижений.
///
/// Слушает игровые события (убийства, урон игроку, завершение волны,
/// конец забега), накапливает прогресс и выдаёт награды. Живёт один на
/// всю игру (DontDestroyOnLoad), потому что состояние достижений общее
/// для меню и забега, а сцена перезагружается при рестарте.
///
/// Разделение прогресса:
///  - накопительный (между забегами) — пишется в сохранение;
///  - за один забег — живёт только в рантайме и сбрасывается на старте
///    нового забега, поэтому добить его «задним числом» нельзя.
/// </summary>
public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    private const string PrefsKey = "ArcadeSurvivor.Achievements";

    [Header("Achievements")]
    [Tooltip("Все достижения игры. Пусто — создаётся набор по умолчанию в рантайме.")]
    [SerializeField] private List<AchievementData> achievements = new List<AchievementData>();

    /// <summary>Достижение разблокировано. Слушает UI-уведомление.</summary>
    public event Action<AchievementData> OnAchievementUnlocked;

    /// <summary>Прогресс достижения изменился (для обновления UI, если он открыт).</summary>
    public event Action OnProgressChanged;

    private readonly AchievementSaveData save = new AchievementSaveData();

    // Транзиентный (за забег) прогресс — не сохраняется.
    private readonly Dictionary<string, float> runProgress = new Dictionary<string, float>();
    private readonly Dictionary<int, int> eventKillCounts = new Dictionary<int, int>();

    private bool runActive;
    private bool runTookDamage;
    private int runKillsSinceDamage;
    private int runPerfectWaves;

    private bool loaded;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    public static AchievementManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        AchievementManager existing = FindAnyObjectByType<AchievementManager>();

        if (existing != null)
        {
            Instance = existing;
            return existing;
        }

        GameObject host = new GameObject("AchievementManager");
        return host.AddComponent<AchievementManager>();
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

        SanitizeAchievements();
        LoadState();

        // Готовим слой уведомлений заранее, чтобы показать всплывашку
        // в момент разблокировки без задержки на создание UI.
        AchievementNotificationUI.EnsureExists();
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

    /// <summary>
    /// Убирает null-элементы и дубликаты ID, а если список пуст — берёт
    /// ассеты из Resources/Achievements. Если и там пусто, подставляет
    /// встроенный набор. Так работают оба пути: заведённые ассеты и
    /// запуск без них (п. 11 задания).
    /// </summary>
    private void SanitizeAchievements()
    {
        if (achievements == null)
            achievements = new List<AchievementData>();

        achievements.RemoveAll(a => a == null);

        if (achievements.Count == 0)
        {
            AchievementData[] fromResources =
                Resources.LoadAll<AchievementData>("Achievements");

            if (fromResources != null && fromResources.Length > 0)
                achievements = new List<AchievementData>(fromResources);
        }

        HashSet<string> seen = new HashSet<string>();
        List<AchievementData> unique = new List<AchievementData>();

        foreach (AchievementData achievement in achievements)
        {
            if (string.IsNullOrEmpty(achievement.Id))
            {
                Debug.LogWarning($"[Achievements] Ассет '{achievement.name}' без ID — пропущен.");
                continue;
            }

            if (!seen.Add(achievement.Id))
            {
                Debug.LogWarning($"[Achievements] Дубликат ID '{achievement.Id}' — пропущен.");
                continue;
            }

            unique.Add(achievement);
        }

        achievements = unique;

        if (achievements.Count == 0)
        {
            Debug.Log("[Achievements] Список пуст — используется набор по умолчанию.");
            achievements = AchievementDefaults.Create();
        }
    }

    // =========================================================
    // QUERIES
    // =========================================================

    public IReadOnlyList<AchievementData> All => achievements;

    public int UnlockedCount
    {
        get
        {
            int count = 0;

            for (int i = 0; i < achievements.Count; i++)
            {
                if (IsUnlocked(achievements[i].Id))
                    count++;
            }

            return count;
        }
    }

    public bool IsUnlocked(string id)
    {
        AchievementSaveData.Entry entry = save.Find(id);
        return entry != null && entry.unlocked;
    }

    public string GetUnlockedAt(string id)
    {
        AchievementSaveData.Entry entry = save.Find(id);
        return entry != null ? entry.unlockedAt : string.Empty;
    }

    public float GetRequired(AchievementData achievement)
    {
        if (achievement == null)
            return 1f;

        if (achievement.TargetValue > 0f)
            return achievement.TargetValue;

        switch (achievement.ConditionType)
        {
            case AchievementConditionType.WinWithUniqueWeapons:
                UpgradeManager upgradeManager = UpgradeManager.Instance;
                int weaponCount = upgradeManager != null
                    ? upgradeManager.GetAvailableWeapons().Count
                    : 0;
                return weaponCount;

            default:
                return 1f;
        }
    }

    /// <summary>Текущий прогресс для UI: накопленный или за забег.</summary>
    public float GetProgress(AchievementData achievement)
    {
        if (achievement == null)
            return 0f;

        string id = achievement.Id;

        if (IsCollection(achievement.ConditionType))
        {
            AchievementSaveData.Entry entry = save.Find(id);
            return entry != null ? entry.collected.Count : 0f;
        }

        if (IsPerRun(achievement.ConditionType))
        {
            float value;
            return runProgress.TryGetValue(id, out value) ? value : 0f;
        }

        AchievementSaveData.Entry stored = save.Find(id);
        return stored != null ? stored.progress : 0f;
    }

    private static bool IsPerRun(AchievementConditionType type)
    {
        return type == AchievementConditionType.KillsWithoutDamageInRun ||
               type == AchievementConditionType.KillsInSingleEvent ||
               type == AchievementConditionType.ReachWaveWithoutDamage ||
               type == AchievementConditionType.PerfectWavesInRun;
    }

    private static bool IsCollection(AchievementConditionType type)
    {
        return type == AchievementConditionType.WinWithUniqueWeapons ||
               type == AchievementConditionType.UniqueBosses;
    }

    // =========================================================
    // EVENT HANDLERS
    // =========================================================

    private void HandleEnemyKilled(EnemyKillInfo info)
    {
        if (!runActive)
            return;

        bool changed = false;

        for (int i = 0; i < achievements.Count; i++)
        {
            AchievementData achievement = achievements[i];

            if (achievement == null || IsUnlocked(achievement.Id))
                continue;

            switch (achievement.ConditionType)
            {
                case AchievementConditionType.CumulativeKills:
                    AddStoredProgress(achievement, 1f);
                    changed = true;
                    break;

                case AchievementConditionType.KillsWithoutDamageInRun:
                    if (!runTookDamage)
                    {
                        runKillsSinceDamage++;
                        SetRunProgress(achievement.Id, runKillsSinceDamage);
                        changed = true;
                    }
                    break;

                case AchievementConditionType.KillsByDamageSource:
                    if (info.source == achievement.DamageSource)
                    {
                        AddStoredProgress(achievement, 1f);
                        changed = true;
                    }
                    break;

                case AchievementConditionType.KillsInSingleEvent:
                    if (info.sourceEventId != 0)
                    {
                        int count = IncrementEventCount(info.sourceEventId);

                        float current;
                        runProgress.TryGetValue(achievement.Id, out current);

                        if (count > current)
                            SetRunProgress(achievement.Id, count);

                        changed = true;
                    }
                    break;

                case AchievementConditionType.UniqueBosses:
                    if (info.isBoss && !string.IsNullOrEmpty(info.bossId))
                    {
                        AddCollected(achievement, info.bossId);
                        changed = true;
                    }
                    break;
            }

            TryComplete(achievement);
        }

        if (changed)
            OnProgressChanged?.Invoke();
    }

    private void HandlePlayerDamaged()
    {
        if (!runActive)
            return;

        runTookDamage = true;
        runKillsSinceDamage = 0;

        for (int i = 0; i < achievements.Count; i++)
        {
            AchievementData achievement = achievements[i];

            if (achievement == null)
                continue;

            if (achievement.ConditionType == AchievementConditionType.KillsWithoutDamageInRun)
                SetRunProgress(achievement.Id, 0f);
        }

        OnProgressChanged?.Invoke();
    }

    private void HandleWaveCleared(int wave, bool wasPerfect)
    {
        if (!runActive)
            return;

        for (int i = 0; i < achievements.Count; i++)
        {
            AchievementData achievement = achievements[i];

            if (achievement == null || IsUnlocked(achievement.Id))
                continue;

            switch (achievement.ConditionType)
            {
                case AchievementConditionType.ReachWaveWithoutDamage:
                    if (runTookDamage)
                        break;

                    SetRunProgress(achievement.Id, wave);
                    break;

                case AchievementConditionType.PerfectWavesInRun:
                    if (wasPerfect)
                    {
                        runPerfectWaves++;
                        SetRunProgress(achievement.Id, runPerfectWaves);
                    }
                    break;
            }

            TryComplete(achievement);
        }

        OnProgressChanged?.Invoke();
    }

    private void HandleRunStarted()
    {
        runActive = true;
        runTookDamage = false;
        runKillsSinceDamage = 0;
        runPerfectWaves = 0;

        runProgress.Clear();
        eventKillCounts.Clear();

        OnProgressChanged?.Invoke();
    }

    private void HandleRunEnded(RunEndInfo info)
    {
        if (!runActive)
            return;

        if (info.won)
        {
            string weaponName = !string.IsNullOrEmpty(info.weaponName)
                ? info.weaponName
                : ResolveEquippedWeaponName();

            for (int i = 0; i < achievements.Count; i++)
            {
                AchievementData achievement = achievements[i];

                if (achievement == null || IsUnlocked(achievement.Id))
                    continue;

                if (achievement.ConditionType == AchievementConditionType.WinWithUniqueWeapons &&
                    !string.IsNullOrEmpty(weaponName))
                {
                    AddCollected(achievement, weaponName);
                    TryComplete(achievement);
                }
            }
        }

        // Накопительный прогресс фиксируем в конце забега, чтобы не
        // писать в PlayerPrefs на каждом убийстве.
        SaveState();
        OnProgressChanged?.Invoke();
    }

    // =========================================================
    // PROGRESS MUTATION
    // =========================================================

    private void AddStoredProgress(AchievementData achievement, float amount)
    {
        AchievementSaveData.Entry entry = save.GetOrCreate(achievement.Id);
        entry.progress += amount;
    }

    private void SetRunProgress(string id, float value)
    {
        runProgress[id] = value;
    }

    private void AddCollected(AchievementData achievement, string value)
    {
        if (string.IsNullOrEmpty(value))
            return;

        AchievementSaveData.Entry entry = save.GetOrCreate(achievement.Id);

        if (entry.collected == null)
            entry.collected = new List<string>();

        if (!entry.collected.Contains(value))
            entry.collected.Add(value);
    }

    private int IncrementEventCount(int eventId)
    {
        int count;
        eventKillCounts.TryGetValue(eventId, out count);
        count++;
        eventKillCounts[eventId] = count;

        return count;
    }

    private string ResolveEquippedWeaponName()
    {
        Weapon weapon = FindAnyObjectByType<Weapon>();

        if (weapon != null && weapon.Data != null)
            return weapon.Data.WeaponName;

        return string.Empty;
    }

    // =========================================================
    // COMPLETION
    // =========================================================

    private void TryComplete(AchievementData achievement)
    {
        if (achievement == null || IsUnlocked(achievement.Id))
            return;

        float required = GetRequired(achievement);

        // required <= 0 означает «не смог вычислить цель» (например,
        // оружие ещё не заведено) — не разблокируем, чтобы не выдать
        // награду авансом.
        if (required <= 0f)
            return;

        if (GetProgress(achievement) < required)
            return;

        Unlock(achievement);
    }

    private void Unlock(AchievementData achievement)
    {
        AchievementSaveData.Entry entry = save.GetOrCreate(achievement.Id);

        if (entry.unlocked)
            return;

        entry.unlocked = true;
        entry.unlockedAt = DateTime.UtcNow.ToString("o");

        GrantRewards(achievement);

        SaveState();

        Debug.Log($"[Achievements] Разблокировано: {achievement.Id}");

        OnAchievementUnlocked?.Invoke(achievement);
        OnProgressChanged?.Invoke();

        if (AchievementNotificationUI.Instance != null)
            AchievementNotificationUI.Instance.Enqueue(achievement);
    }

    /// <summary>
    /// Выдаёт все награды достижения ровно один раз. Ключ выдачи —
    /// id достижения + индекс награды; если ключ уже есть в сохранении,
    /// награда пропускается (защита от повторной загрузки/события).
    /// </summary>
    private void GrantRewards(AchievementData achievement)
    {
        IReadOnlyList<AchievementReward> rewards = achievement.Rewards;

        if (rewards == null)
            return;

        for (int i = 0; i < rewards.Count; i++)
        {
            AchievementReward reward = rewards[i];

            if (reward == null)
                continue;

            string key = achievement.Id + ":" + i;

            if (save.grantedRewards.Contains(key))
                continue;

            AchievementRewardGranter.Grant(reward);

            save.grantedRewards.Add(key);
        }
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
            AchievementSaveData parsed = JsonUtility.FromJson<AchievementSaveData>(json);

            if (parsed == null)
                return;

            save.entries = parsed.entries ?? new List<AchievementSaveData.Entry>();
            save.grantedRewards = parsed.grantedRewards ?? new List<string>();

            for (int i = 0; i < save.entries.Count; i++)
            {
                if (save.entries[i] == null)
                {
                    save.entries.RemoveAt(i);
                    i--;
                    continue;
                }

                if (save.entries[i].collected == null)
                    save.entries[i].collected = new List<string>();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Achievements] Не удалось прочитать сохранение: " + e.Message);
            save.entries = new List<AchievementSaveData.Entry>();
            save.grantedRewards = new List<string>();
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
            Debug.LogWarning("[Achievements] Не удалось сохранить: " + e.Message);
        }
    }

    // =========================================================
    // DEBUG / EDITOR
    // =========================================================

    /// <summary>Полный сброс прогресса достижений (для отладки/меню).</summary>
    public void ResetAll()
    {
        save.entries.Clear();
        save.grantedRewards.Clear();
        runProgress.Clear();
        eventKillCounts.Clear();

        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();

        OnProgressChanged?.Invoke();
    }
}
