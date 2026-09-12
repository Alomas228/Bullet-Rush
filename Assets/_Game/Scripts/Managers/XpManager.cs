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

    [Header("Leveling")]
    [Tooltip("XP needed to reach level 2. Grows by this step each level.")]
    [SerializeField] private int baseLevelXP = 50;
    [Tooltip("XP added to the requirement per level.")]
    [SerializeField] private int xpGrowthPerLevel = 25;

    public int RunXP { get; private set; }
    public int RunLevel { get; private set; } = 1;
    public int RunCoins { get; private set; }

    public int GlobalXP { get; private set; }
    public int GlobalCoins { get; private set; }

    public bool IsRunActive { get; private set; }

    public int RunXPToNextLevel => GetXPForLevel(RunLevel + 1);

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

    public float RunLevelProgress
    {
        get
        {
            int currentLevelXP = GetXPForLevel(RunLevel);
            int nextLevelXP = GetXPForLevel(RunLevel + 1);

            float range = nextLevelXP - currentLevelXP;

            if (range <= 0f)
                return 1f;

            return
                Mathf.Clamp01(
                    (RunXP - currentLevelXP) / range
                );
        }
    }

    public event Action<int> OnXPChanged;
    public event Action<int> OnLevelUp;
    public event Action<int> OnCoinsChanged;

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
        RunXP = 0;
        RunLevel = 1;
        RunCoins = 0;

        OnXPChanged?.Invoke(RunXP);
        OnCoinsChanged?.Invoke(RunCoins);
    }

    // =========================================================
    // XP
    // =========================================================

    public void AddXP(int amount)
    {
        if (amount <= 0)
            return;

        RunXP += amount;
        GlobalXP += amount;

        OnXPChanged?.Invoke(RunXP);

        CheckLevelUps();

        SaveGlobal();
    }

    private void CheckLevelUps()
    {
        while (RunXP >= RunXPToNextLevel)
        {
            RunXP -= RunXPToNextLevel;
            RunLevel++;

            OnLevelUp?.Invoke(RunLevel);

            Debug.Log($"LEVEL UP! Level {RunLevel}");
        }
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
    // LEVEL FORMULA
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