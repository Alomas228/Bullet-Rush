using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using YG;

/// <summary>
/// Сервис для работы с лидербордом через Yandex Games SDK.
/// Отправляет результаты забега и загружает топ игроков.
/// </summary>
public class LeaderboardService : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private bool enableLeaderboard = true;

    private static LeaderboardService instance;
    public static LeaderboardService Instance => instance;

    private bool isInitialized;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// Инициализация сервиса (вызывается при загрузке меню).
    /// </summary>
    public void Initialize()
    {
        if (isInitialized) return;

        isInitialized = true;
        Debug.Log("[Leaderboard] Service initialized");
    }

    /// <summary>
    /// Отправка результата забега на сервер.
    /// </summary>
    public void SubmitResult(RunResult result)
    {
        if (!enableLeaderboard)
        {
            Debug.Log("[Leaderboard] Disabled — skipping submit");
            return;
        }

        StartCoroutine(SubmitResultCoroutine(result));
    }

    private IEnumerator SubmitResultCoroutine(RunResult result)
    {
        YandexGameManager yg = YandexGameManager.Instance;

        if (yg != null)
        {
            SendToLeaderboard(result.finalScore);
            Debug.Log("[Leaderboard] Final score sent: " + result.finalScore);
            SaveProgress(result);
            yield break;
        }

        // Fallback: локальное сохранение в PlayerPrefs
        Debug.LogWarning("[Leaderboard] YandexGames not available — saving locally");
        SaveLocally(result);
    }

    private void SendToLeaderboard(int score)
    {
        #if Leaderboards_yg
        if (YG2.isSDKEnabled && YG2.player.auth)
        {
            YG2.SetLeaderboard("BestScore", score);
            Debug.Log("[Leaderboard] Score submitted to Yandex: " + score);
        }
        #else
        Debug.LogWarning("[Leaderboard] Leaderboards_yg not defined");
        #endif
    }

    private void SaveProgress(RunResult result)
    {
        #if Storage_yg
        if (YG2.isSDKEnabled)
        {
            YG2.saves.bestScore = Mathf.Max(YG2.saves.bestScore, result.finalScore);
            YG2.SaveProgress();
            Debug.Log("[Leaderboard] Progress saved to cloud");
        }
        #else
        Debug.LogWarning("[Leaderboard] Storage_yg not defined");
        #endif
    }

    /// <summary>
    /// Сохранение результата локально (fallback).
    /// </summary>
    private void SaveLocally(RunResult result)
    {
        const string prefsKey = "BulletRush.Leaderboard";

        // Загружаем существующий список
        List<LeaderboardEntry> entries = LoadLocalEntries();

        // Добавляем новый результат
        entries.Add(new LeaderboardEntry
        {
            score = result.finalScore,
            rank = result.rank,
            kills = result.kills,
            waves = result.wavesCleared,
            time = result.runTime,
            combo = result.maxCombo,
            styleBonus = result.styleBonus,
            timestamp = result.timestamp,
        });

        // Сортируем по score (по убыванию)
        entries.Sort((a, b) => b.score.CompareTo(a.score));

        // Оставляем топ-100
        if (entries.Count > 100)
            entries = entries.GetRange(0, 100);

        // Сохраняем
        string json = JsonUtility.ToJson(new LocalLeaderboardData { entries = entries }, true);
        PlayerPrefs.SetString(prefsKey, json);
        PlayerPrefs.Save();

        Debug.Log("[Leaderboard] Local entry saved. Total entries: " + entries.Count);
    }

    /// <summary>
    /// Загрузка локальных записей.
    /// </summary>
    public List<LeaderboardEntry> LoadLocalEntries()
    {
        const string prefsKey = "BulletRush.Leaderboard";
        string json = PlayerPrefs.GetString(prefsKey, string.Empty);

        if (string.IsNullOrEmpty(json))
            return new List<LeaderboardEntry>();

        try
        {
            LocalLeaderboardData data = JsonUtility.FromJson<LocalLeaderboardData>(json);
            return data != null ? data.entries : new List<LeaderboardEntry>();
        }
        catch
        {
            return new List<LeaderboardEntry>();
        }
    }

    /// <summary>
    /// Очистка локальных данных (для тестов).
    /// </summary>
    public void ClearLocalData()
    {
        PlayerPrefs.DeleteKey("BulletRush.Leaderboard");
        PlayerPrefs.Save();
        Debug.Log("[Leaderboard] Local data cleared");
    }
}

// =========================================================
// DATA STRUCTURES
// =========================================================

[System.Serializable]
public class LeaderboardEntry
{
    public int score;
    public string rank;
    public int kills;
    public int waves;
    public float time;
    public int combo;
    public int styleBonus;
    public long timestamp;
}

[System.Serializable]
public class LocalLeaderboardData
{
    public List<LeaderboardEntry> entries = new List<LeaderboardEntry>();
}
