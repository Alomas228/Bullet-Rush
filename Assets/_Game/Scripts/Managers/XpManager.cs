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
    [Tooltip("XP needed to reach level 2. Grows by this step each level.")]
    [SerializeField] private int baseLevelXP = 50;
    [Tooltip("XP added to the requirement per level.")]
    [SerializeField] private int xpGrowthPerLevel = 25;

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

    /// <summary>XP granted by the most recent run end.</summary>
    public int LastRunReward { get; private set; }

    /// <summary>Player Level before the most recent run-end grant.</summary>
    public int LastLevelBeforeGrant { get; private set; }

    /// <summary>Number of levels gained from the most recent run-end grant.</summary>
    public int LevelsGainedLastRun { get; private set; }

    public int GlobalLevel
    {
        get
        {
            int level = 1;

            while (GlobalXP >= GetXPForLevel(level + 1))
                level++;

            return level;
        }
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

    // =========================================================
    // LEVEL FORMULA (single source of truth for level requirements)
    // =========================================================

    public int GetXPForLevel(int level)
    {
        if (level <= 1)
            return 0;

        int xpToLevel2 = Mathf.Max(baseLevelXP, 1);

        return xpToLevel2 + (level - 2) * Mathf.Max(xpGrowthPerLevel, 1);
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