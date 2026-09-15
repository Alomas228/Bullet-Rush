using UnityEngine;
using System;

public class XpManager : MonoBehaviour
{
    public static XpManager Instance { get; private set; }

    [Serializable]
    private class GlobalData
    {
        public int globalXP;
        public int globalCoins;
    }

    private const string PrefsKey = "ArcadeSurvivor.Global";

    [Header("Player Leveling (persistent, between runs)")]
    [Tooltip("Maximum player level.")]
    [SerializeField] private int maxLevel = 50;

    [Header("Run End Reward (Player XP source)")]
    [Tooltip("Fixed Player XP granted when a run ends.")]
    [SerializeField] private int baseXPReward = 100;
    [Tooltip("Player XP granted per wave reached.")]
    [SerializeField] private int xpPerWave = 15;
    [Tooltip("Player XP granted per boss defeated (boss wave reached).")]
    [SerializeField] private int xpPerBoss = 50;
    [Tooltip("Boss wave interval (must match WaveManager).")]
    [SerializeField] private int bossWaveInterval = 10;

    public int RunCoins { get; private set; }

    /// <summary>XP collected during the current run. Reset every run.</summary>
    public int RunXP { get; private set; }

    /// <summary>Persistent Player XP. Survives runs and game restarts.</summary>
    public int GlobalXP { get; private set; }
    public int GlobalCoins { get; private set; }

    public bool IsRunActive { get; private set; }

    private bool runRewardGranted;

    /// <summary>XP granted by the most recent run end.</summary>
    public int LastRunReward { get; private set; }

    /// <summary>Player Level before the most recent run-end grant.</summary>
    public int LastLevelBeforeGrant { get; private set; }

    /// <summary>Number of levels gained from the most recent run-end grant.</summary>
    public int LevelsGainedLastRun { get; private set; }

    public int GlobalLevel => GetLevelForXP(GlobalXP);

    /// <summary>Level for any total XP value, capped at maxLevel.</summary>
    public int GetLevelForXP(int totalXP)
    {
        int level = 1;

        while (level < maxLevel && totalXP >= GetXPForLevel(level + 1))
            level++;

        return level;
    }

    public int GlobalXPToNextLevel => GetXPForLevel(GlobalLevel + 1);

    /// <summary>XP required to go from current level up to the next one (range of the current level).</summary>
    public int GlobalXPNeededForNextLevel =>
        Mathf.Max(GlobalXPToNextLevel - GetXPForLevel(GlobalLevel), 1);

    /// <summary>XP accumulated inside the current level, relative to the start of this level.</summary>
    public int GlobalXPInCurrentLevel =>
        Mathf.Max(GlobalXP - GetXPForLevel(GlobalLevel), 0);

    public float GlobalLevelProgress
    {
        get
        {
            int currentLevelXP = GetXPForLevel(GlobalLevel);
            int nextLevelXP = GetXPForLevel(GlobalLevel + 1);

            float range = nextLevelXP - currentLevelXP;

            if (range <= 0f)
                return 1f;

            return
                Mathf.Clamp01(
                    (GlobalXP - currentLevelXP) / range
                );
        }
    }

    public event Action<int> OnCoinsChanged;

    /// <summary>Fired when XP collected during the run changes.</summary>
    public event Action<int> OnRunXPChanged;

    /// <summary>Fired when persistent Player XP changes.</summary>
    public event Action<int> OnPlayerXPChanged;

    /// <summary>Fired when the persistent Player Level changes.</summary>
    public event Action<int> OnPlayerLevelChanged;

    private bool subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadGlobal();
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void OnDisable()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
    }

    private void EnsureSubscribed()
    {
        if (subscribed)
            return;

        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
        subscribed = true;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing && !IsRunActive)
        {
            IsRunActive = true;
            ResetRunData();
        }
        else if (state != GameState.Playing && IsRunActive)
        {
            IsRunActive = false;
        }
    }

    private void ResetRunData()
    {
        RunCoins = 0;
        RunXP = 0;
        runRewardGranted = false;

        OnCoinsChanged?.Invoke(RunCoins);
        OnRunXPChanged?.Invoke(RunXP);
    }

    // =========================================================
    // RUN XP (inside the run only, resets every run)
    // =========================================================

    /// <summary>Adds XP collected during the current run. It is converted to Player XP at run end.</summary>
    public void AddRunXP(int amount)
    {
        if (amount <= 0)
            return;

        RunXP += amount;

        OnRunXPChanged?.Invoke(RunXP);
    }

    // =========================================================
    // PLAYER XP (persistent, becomes Player Level)
    // =========================================================

    /// <summary>Adds persistent Player XP. Multiple level ups are handled automatically.</summary>
    public void AddPlayerXP(int amount)
    {
        if (amount <= 0)
            return;

        int levelBefore = GlobalLevel;

        GlobalXP += amount;

        int levelAfter = GlobalLevel;

        SaveGlobal();

        OnPlayerXPChanged?.Invoke(GlobalXP);

        if (levelAfter > levelBefore)
        {
            Debug.Log(
                $"PLAYER LEVEL UP! {levelBefore} -> {levelAfter}"
            );

            OnPlayerLevelChanged?.Invoke(levelAfter);
        }
    }

    /// <summary>Current persistent Player XP.</summary>
    public int GetPlayerXP() => GlobalXP;

    /// <summary>Current persistent Player Level.</summary>
    public int GetPlayerLevel() => GlobalLevel;

    /// <summary>Total XP required to reach the next level.</summary>
    public int GetXPRequiredForNextLevel() => GlobalXPToNextLevel;

    /// <summary>XP still needed, counted from the start of the current level, to level up once.</summary>
    public int GetXPNeededForNextLevel() =>
        Mathf.Max(GlobalXPNeededForNextLevel - GlobalXPInCurrentLevel, 0);

    // =========================================================
    // RUN END (convert the run into persistent Player XP)
    // =========================================================

    /// <summary>
    /// Called once when a run ends (Game Over). Computes the run reward,
    /// grants it as persistent Player XP and processes level ups.
    /// The result is stored for UI display in LastRunReward / LevelsGainedLastRun.
    /// </summary>
    public void ProcessRunEnd()
    {
        if (runRewardGranted)
            return;

        Debug.Log("[Xp.ProcessRunEnd] entered, IsRunActive=" + IsRunActive + ", RunXP=" + RunXP + ", RunCoins=" + RunCoins);

        if (!IsRunActive)
        {
            Debug.LogWarning("[Xp.ProcessRunEnd] IsRunActive=false - взвожу принудительно. Причина false обычно: I. подписка HandleStateChanged упала из-за гонки синглтонов (GameStateManager.Instance был null на OnEnable) или II. ProcessRunEnd вызван вне реального забега. Если это конец настоящего забега - награда будет начислена.");
        }

        IsRunActive = true;

        WaveManager waveManager =
            FindAnyObjectByType<WaveManager>();

        int waveReached =
            waveManager != null
                ? waveManager.CurrentWave
                : 0;

        int kills =
            ScoreManager.Instance != null
                ? ScoreManager.Instance.Kills
                : 0;

        int reward =
            CalculateRunReward(
                RunXP,
                waveReached,
                kills
            );

        GrantPlayerXP(reward);
    }

    /// <summary>Computes the Player XP reward for a finished run. Extensible: future XP sources can be added here.</summary>
    public int CalculateRunReward(
        int runXP,
        int waveReached,
        int kills)
    {
        int baseReward =
            Mathf.Max(baseXPReward, 0);

        int waveBonus =
            Mathf.Max(waveReached, 0) *
            Mathf.Max(xpPerWave, 0);

        int bossCount =
            Mathf.Max(bossWaveInterval, 1) > 0
                ? Mathf.Max(waveReached, 0) /
                  Mathf.Max(bossWaveInterval, 1)
                : 0;

        int bossBonus =
            bossCount *
            Mathf.Max(xpPerBoss, 0);

        int collectedRunXP =
            Mathf.Max(runXP, 0);

        int result =
            baseReward +
            waveBonus +
            bossBonus +
            collectedRunXP;

        Debug.Log(
            $"Run reward: base {baseReward} + wave {waveBonus} + boss {bossBonus} + runXP {collectedRunXP} = {result}"
        );

        return result;
    }

    private void GrantPlayerXP(int amount)
    {
        if (runRewardGranted)
            return;

        runRewardGranted = true;

        LastLevelBeforeGrant = GlobalLevel;

        AddPlayerXP(amount);

        LastRunReward = Mathf.Max(amount, 0);
        LevelsGainedLastRun =
            Mathf.Max(GlobalLevel - LastLevelBeforeGrant, 0);

        Debug.Log(
            $"Player XP +{LastRunReward} " +
            $"(Levels gained: {LevelsGainedLastRun}, " +
            $"Level now: {GlobalLevel}, " +
            $"XP: {GlobalXP}/{GlobalXPToNextLevel})"
        );
    }

    // =========================================================
    // COINS
    // =========================================================

    public void AddCoins(int amount)
    {
        if (amount <= 0)
            return;

        RunCoins += amount;
        GlobalCoins += amount;

        OnCoinsChanged?.Invoke(RunCoins);

        SaveGlobal();

        Debug.Log($"Coins: +{amount} (Run: {RunCoins}, Global: {GlobalCoins})");
    }

    /// <summary>
    /// Списывает монеты из постоянного баланса. Возвращает false,
    /// если монет недостаточно (ничего не списывается).
    /// </summary>
    public bool TrySpendCoins(int amount)
    {
        if (amount <= 0)
            return false;

        if (GlobalCoins < amount)
        {
            Debug.LogWarning(
                $"Not enough coins: need {amount}, have {GlobalCoins}."
            );

            return false;
        }

        GlobalCoins -= amount;

        SaveGlobal();

        OnCoinsChanged?.Invoke(RunCoins);

        Debug.Log(
            $"Coins: -{amount} (Global: {GlobalCoins})"
        );

        return true;
    }

    // =========================================================
    // LEVEL TABLE (single source of truth for level requirements)
    // =========================================================

    /// <summary>Cumulative XP required to reach each level from level 1. Index = level - 1.</summary>
    private static readonly int[] XpToLevel =
    {
        0,        // Lvl 1 (start)
        500,      // Lvl 2
        1035,     // Lvl 3
        1607,     // Lvl 4
        2219,     // Lvl 5
        2874,     // Lvl 6
        3574,     // Lvl 7
        4323,     // Lvl 8
        5124,     // Lvl 9
        5981,     // Lvl 10
        6898,     // Lvl 11
        7879,     // Lvl 12
        8929,     // Lvl 13
        10052,    // Lvl 14
        11254,    // Lvl 15
        12540,    // Lvl 16
        13916,    // Lvl 17
        15388,    // Lvl 18
        16963,    // Lvl 19
        18648,    // Lvl 20
        20451,    // Lvl 21
        22380,    // Lvl 22
        24444,    // Lvl 23
        26653,    // Lvl 24
        29017,    // Lvl 25
        31546,    // Lvl 26
        34252,    // Lvl 27
        37147,    // Lvl 28
        40245,    // Lvl 29
        43560,    // Lvl 30
        47107,    // Lvl 31
        50902,    // Lvl 32
        54962,    // Lvl 33
        59306,    // Lvl 34
        63954,    // Lvl 35
        68927,    // Lvl 36
        74248,    // Lvl 37
        79942,    // Lvl 38
        86035,    // Lvl 39
        92555,    // Lvl 40
        99531,    // Lvl 41
        106995,   // Lvl 42
        114981,   // Lvl 43
        123526,   // Lvl 44
        132669,   // Lvl 45
        142452,   // Lvl 46
        152920,   // Lvl 47
        164121,   // Lvl 48
        176106,   // Lvl 49
        188930    // Lvl 50
    };

    public int GetXPForLevel(int level)
    {
        if (level <= 1)
            return 0;

        int index = level - 1;

        if (index >= XpToLevel.Length)
            return XpToLevel[XpToLevel.Length - 1];

        return XpToLevel[index];
    }

    // =========================================================
    // PERSISTENCE
    // =========================================================

    private void LoadGlobal()
    {
        string json =
            PlayerPrefs.GetString(PrefsKey, string.Empty);

        if (string.IsNullOrEmpty(json))
        {
            GlobalXP = 0;
            GlobalCoins = 0;
            return;
        }

        try
        {
            GlobalData data =
                JsonUtility.FromJson<GlobalData>(json);

            GlobalXP = Mathf.Max(data.globalXP, 0);
            GlobalCoins = Mathf.Max(data.globalCoins, 0);
        }
        catch
        {
            GlobalXP = 0;
            GlobalCoins = 0;
        }
    }

    private void SaveGlobal()
    {
        GlobalData data = new GlobalData
        {
            globalXP = GlobalXP,
            globalCoins = GlobalCoins
        };

        PlayerPrefs.SetString(
            PrefsKey,
            JsonUtility.ToJson(data)
        );

        PlayerPrefs.Save();
    }
}