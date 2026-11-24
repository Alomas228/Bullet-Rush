using UnityEngine;
using UnityEngine.SceneManagement;
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
    [SerializeField] private int maxLevel = GameBalance.MaxPlayerLevel;

    public int RunCoins { get; private set; }
    public int RunClearedWaves { get; private set; }
    public int RunCoinAdBonus { get; private set; }
    private bool startedRun;

    /// <summary>Persistent Player XP. Survives runs and game restarts.</summary>
    public int GlobalXP { get; private set; }
    public int GlobalCoins { get; private set; }

    public bool IsRunActive { get; private set; }

    // Сколько награды уже выдано за ТЕКУЩИЙ забег. Повторный конец
    // (после возрождения) начисляет только разницу — игрок получает
    // награду за более глубокий забег, но не дважды за один и тот же.
    private int grantedRunReward;

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

    /// <summary>Fired when persistent Player XP changes.</summary>
    public event Action<int> OnPlayerXPChanged;

    /// <summary>Fired when the persistent Player Level changes.</summary>
    public event Action<int> OnPlayerLevelChanged;

    private bool subscribed;
    private GameStateManager subscribedManager;

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
        SceneManager.sceneLoaded += OnSceneLoaded;

        // Сброс данных забега привязан к старту НОВОГО забега, а не к
        // входу в состояние Playing: возрождение (GameOver -> Playing)
        // не должно обнулять заработанные монеты и уже выданную награду.
        AchievementEvents.RunStarted += HandleRunStarted;
        AchievementEvents.WaveCleared += HandleWaveCleared;

        EnsureSubscribed();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        AchievementEvents.RunStarted -= HandleRunStarted;
        AchievementEvents.WaveCleared -= HandleWaveCleared;

        if (subscribedManager != null)
            subscribedManager.OnGameStateChanged -= HandleStateChanged;

        subscribedManager = null;
        subscribed = false;
    }

    private void HandleRunStarted()
    {
        startedRun = !GameModeManager.IsPvp;
        IsRunActive = true;
        ResetRunData();
    }

    private void HandleWaveCleared(int wave, bool perfect)
    {
        if (!startedRun || GameModeManager.IsPvp || wave <= RunClearedWaves) return;
        RunClearedWaves = wave;
        AddCoins(GameBalance.WaveCoins(wave, perfect));
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureSubscribed();
    }

    private void EnsureSubscribed()
    {
        GameStateManager current =
            GameStateManager.Instance;

        if (current == null)
            return;

        if (subscribedManager == current &&
            subscribed)
        {
            return;
        }

        if (subscribedManager != null)
            subscribedManager.OnGameStateChanged -= HandleStateChanged;

        current.OnGameStateChanged += HandleStateChanged;

        subscribedManager = current;
        subscribed = true;

        HandleStateChanged(current.CurrentState);
    }

    private void HandleStateChanged(GameState state)
    {
        // Данные забега сбрасываются в HandleRunStarted (новый забег),
        // поэтому здесь только фиксируем, что забег идёт или прерван.
        if (state == GameState.Playing)
        {
            IsRunActive = true;
        }
        else if (IsRunActive)
        {
            IsRunActive = false;
        }
    }

    private void ResetRunData()
    {
        RunCoins = 0;
        RunClearedWaves = 0;
        RunCoinAdBonus = 0;
        grantedRunReward = 0;
        LastRunReward = 0;
        LevelsGainedLastRun = 0;

        OnCoinsChanged?.Invoke(RunCoins);
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
            PlayLevelUpSound();
        }
    }

    public bool RemovePlayerXP(int amount)
    {
        if (amount <= 0 || GlobalXP < amount)
            return false;

        int previousLevel = GlobalLevel;
        GlobalXP -= amount;
        SaveGlobal();
        OnPlayerXPChanged?.Invoke(GlobalXP);
        if (GlobalLevel != previousLevel)
            OnPlayerLevelChanged?.Invoke(GlobalLevel);
        return true;
    }

    private void PlayLevelUpSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.LevelUp,
                priority: SfxPriority.High
            );
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
        if (!startedRun || GameModeManager.IsPvp) return;
        Debug.Log("[Xp.ProcessRunEnd] entered, IsRunActive=" + IsRunActive + ", RunCoins=" + RunCoins);

        if (!IsRunActive)
        {
            Debug.LogWarning("[Xp.ProcessRunEnd] IsRunActive=false - взвожу принудительно. Причина false обычно: I. подписка HandleStateChanged упала из-за гонки синглтонов (GameStateManager.Instance был null на OnEnable) или II. ProcessRunEnd вызван вне реального забега. Если это конец настоящего забега - награда будет начислена.");
        }

        IsRunActive = true;

        int reward = CalculateRunReward(RunClearedWaves);

        // Награда за забег выдаётся по частям: при возрождении забег
        // продолжается, и следующий конец начисляет только прирост.
        if (reward <= grantedRunReward)
            return;

        int delta = reward - grantedRunReward;

        grantedRunReward = reward;

        LastLevelBeforeGrant = GlobalLevel;

        AddPlayerXP(delta);

        // Суммарная награда за забег — по ней UI рисует рост полосы
        // от старта забега до текущего уровня.
        LastRunReward = reward;
        LevelsGainedLastRun =
            Mathf.Max(GlobalLevel - LastLevelBeforeGrant, 0);

        Debug.Log(
            $"Player XP +{delta} (run total {LastRunReward}, " +
            $"Levels gained: {LevelsGainedLastRun}, " +
            $"Level now: {GlobalLevel}, " +
            $"XP: {GlobalXP}/{GlobalXPToNextLevel})"
        );
    }

    /// <summary>Computes the Player XP reward for a finished run.</summary>
    public int CalculateRunReward(int clearedWaves) => GameBalance.RunXp(clearedWaves);

    // Rewarded doubling applies only to coins not already doubled before a revive.
    public bool TryDoubleRunCoins()
    {
        int eligible = RunCoins - RunCoinAdBonus;
        if (!startedRun || eligible <= 0 || GameModeManager.IsPvp ||
            GameOverManager.Instance == null || !GameOverManager.Instance.IsGameOver) return false;
        RunCoinAdBonus += eligible;
        AddGlobalCoins(eligible);
        return true;
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
    /// Начисляет монеты только в постоянный баланс, не трогая счётчик
    /// текущего забега. Нужно для наград за достижения, которые не должны
    /// влиять на результат забега.
    /// </summary>
    public void AddGlobalCoins(int amount)
    {
        if (amount <= 0)
            return;

        GlobalCoins += amount;

        SaveGlobal();

        OnCoinsChanged?.Invoke(RunCoins);

        Debug.Log($"Coins: +{amount} global (Global: {GlobalCoins})");
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
    // RESET
    // =========================================================

    /// <summary>
    /// Полный сброс постоянного прогресса: XP, уровень и монеты.
    /// </summary>
    [ContextMenu("Reset Progress")]
    public void ResetProgress()
    {
        GlobalXP = 0;
        GlobalCoins = 0;

        LastRunReward = 0;
        LastLevelBeforeGrant = 0;
        LevelsGainedLastRun = 0;

        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();

        OnPlayerXPChanged?.Invoke(GlobalXP);
        OnPlayerLevelChanged?.Invoke(GlobalLevel);
        OnCoinsChanged?.Invoke(RunCoins);

        Debug.Log("[Xp] Прогресс игрока сброшен (XP, уровень, монеты).");
    }

    // =========================================================
    // LEVEL TABLE (single source of truth for level requirements)
    // =========================================================

    public int GetXPForLevel(int level) => GameBalance.LevelXp(level);

    public bool ResetPlayerXPForPrestige()
    {
        if (GlobalLevel < maxLevel || GlobalXP < GetXPForLevel(maxLevel))
            return false;

        int previousLevel = GlobalLevel;
        GlobalXP = 0;
        SaveGlobal();
        OnPlayerXPChanged?.Invoke(GlobalXP);
        if (previousLevel != GlobalLevel)
            OnPlayerLevelChanged?.Invoke(GlobalLevel);
        return true;
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
