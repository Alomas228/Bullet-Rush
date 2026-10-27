using System;
using System.Globalization;
using UnityEngine;

public sealed class PvpProgressionManager : MonoBehaviour
{
    private const string SaveKey = "ArcadeSurvivor.PvpProgression";
    private const int StartingRating = 1000;
    private const int DailyWinReward = 100;

    [Serializable]
    private sealed class SaveData
    {
        public int rating = StartingRating;
        public int wins;
        public int losses;
        public int streak;
        public string lastWinDate = string.Empty;
    }

    public static PvpProgressionManager Instance { get; private set; }

    public int Rating => data.rating;
    public int Wins => data.wins;
    public int Losses => data.losses;
    public int Streak => data.streak;
    public string League => GetLeague(data.rating);
    public event Action OnChanged;

    private SaveData data;

    public static PvpProgressionManager EnsureExists()
    {
        if (Instance != null)
            return Instance;

        PvpProgressionManager found = FindAnyObjectByType<PvpProgressionManager>();
        if (found != null)
            return found;

        return new GameObject("PvpProgressionManager")
            .AddComponent<PvpProgressionManager>();
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
        Load();
    }

    public int ApplyMatchResult(bool won, int opponentRating)
    {
        int oldRating = data.rating;
        int expected = Mathf.Clamp(opponentRating - data.rating, -200, 200);
        int delta = won
            ? Mathf.Clamp(24 + expected / 20, 12, 36)
            : -Mathf.Clamp(18 - expected / 25, 10, 26);

        data.rating = Mathf.Max(0, data.rating + delta);
        if (won)
        {
            data.wins++;
            data.streak++;
        }
        else
        {
            data.losses++;
            data.streak = 0;
        }

        Save();
        if (YandexGameManager.Instance != null)
            YandexGameManager.Instance.SubmitLeaderboard(
                YandexGameManager.LeaderboardPvpRating, data.rating
            );
        OnChanged?.Invoke();
        return data.rating - oldRating;
    }

    public bool TryClaimDailyWinReward()
    {
        string today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (data.lastWinDate == today || XpManager.Instance == null)
            return false;

        data.lastWinDate = today;
        Save();
        XpManager.Instance.AddGlobalCoins(DailyWinReward);
        OnChanged?.Invoke();
        return true;
    }

    public static string GetLeague(int rating)
    {
        if (rating >= 1600)
            return Lang.GetOr("menu.pvp.league.diamond", "DIAMOND");
        if (rating >= 1300)
            return Lang.GetOr("menu.pvp.league.gold", "GOLD");
        if (rating >= 1100)
            return Lang.GetOr("menu.pvp.league.silver", "SILVER");
        return Lang.GetOr("menu.pvp.league.bronze", "BRONZE");
    }

    private void Load()
    {
        string json = PlayerPrefs.GetString(SaveKey, string.Empty);
        try
        {
            data = string.IsNullOrEmpty(json)
                ? new SaveData()
                : JsonUtility.FromJson<SaveData>(json);
        }
        catch
        {
            data = new SaveData();
        }

        if (data == null)
            data = new SaveData();

        data.rating = Mathf.Max(data.rating, 0);
        data.wins = Mathf.Max(data.wins, 0);
        data.losses = Mathf.Max(data.losses, 0);
        data.streak = Mathf.Max(data.streak, 0);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void Save()
    {
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }
}
