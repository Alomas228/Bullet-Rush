using System;
using System.Globalization;
using UnityEngine;

public class DailyRewardManager : MonoBehaviour
{
    public static DailyRewardManager Instance { get; private set; }

    [Header("Daily Reward Cycle")]
    [Tooltip("Сколько дней в цикле. Панель должна содержать столько же слотов.")]
    [SerializeField] private int cycleLength = 7;

    [Tooltip("Награда монетами за день 1. Дальше — день * coinsBase.")]
    [SerializeField] private int coinsBase = 50;

    [Tooltip("Награда XP за день 1. Дальше — день * xpBase.")]
    [SerializeField] private int xpBase = 100;

    private const string PrefsKey_LastClaim = "DailyReward.LastClaimDate";
    private const string PrefsKey_StreakDay = "DailyReward.StreakDay";
    private const string PrefsKey_Mask = "DailyReward.CycleMask";
    private const string PrefsKey_LastClaimDay = "DailyReward.LastClaimDay";
    private const string PrefsKey_OldDayCounter = "DailyReward.DayCounter";

    public int CycleLength => Mathf.Max(cycleLength, 1);

    public bool IsAvailableToday { get; private set; }

    public int NextDay { get; private set; } = 1;

    public int ClaimedDayToday { get; private set; }

    private int mask;

    public event Action<int> OnRewardClaimed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Load();
        EvaluateAvailability();
    }

    public int CoinsForDay(int day)
    {
        return Mathf.Max(day, 1) * Mathf.Max(coinsBase, 1);
    }

    public int XpForDay(int day)
    {
        return Mathf.Max(day, 1) * Mathf.Max(xpBase, 1);
    }

    public bool IsDayClaimed(int day)
    {
        return day >= 1 &&
               day <= CycleLength &&
               ((mask >> (day - 1)) & 1) == 1;
    }

    public bool ClaimReward()
    {
        if (!IsAvailableToday)
            return false;

        int day = NextDay;
        int coins = CoinsForDay(day);
        int xp = XpForDay(day);

        XpManager xpManager = XpManager.Instance;

        if (xpManager != null)
        {
            if (coins > 0)
                xpManager.AddCoins(coins);

            if (xp > 0)
                xpManager.AddPlayerXP(xp);
        }

        mask |= 1 << (day - 1);

        if (day >= CycleLength)
        {
            NextDay = 1;
            mask = 0;
        }
        else
        {
            NextDay = day + 1;
        }

        ClaimedDayToday = day;

        PlayerPrefs.SetString(
            PrefsKey_LastClaim,
            DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        );

        PlayerPrefs.SetInt(PrefsKey_StreakDay, NextDay);
        PlayerPrefs.SetInt(PrefsKey_Mask, mask);
        PlayerPrefs.SetInt(PrefsKey_LastClaimDay, day);
        PlayerPrefs.Save();

        IsAvailableToday = false;

        OnRewardClaimed?.Invoke(day);

        return true;
    }

    private void Load()
    {
        NextDay = Mathf.Clamp(
            PlayerPrefs.GetInt(PrefsKey_StreakDay, 1),
            1,
            CycleLength
        );

        mask = PlayerPrefs.GetInt(PrefsKey_Mask, 0);

        ClaimedDayToday = Mathf.Clamp(
            PlayerPrefs.GetInt(PrefsKey_LastClaimDay, 0),
            0,
            CycleLength
        );

        if (PlayerPrefs.HasKey(PrefsKey_StreakDay))
            return;

        int oldCounter = PlayerPrefs.GetInt(PrefsKey_OldDayCounter, 1);

        if (oldCounter > 1)
        {
            NextDay = Mathf.Clamp(oldCounter, 1, CycleLength);
            mask = 0;
        }
    }

    private void EvaluateAvailability()
    {
        string raw = PlayerPrefs.GetString(PrefsKey_LastClaim, "");

        DateTime? last = TryParseDate(raw);

        if (last == null)
        {
            IsAvailableToday = true;
            ClaimedDayToday = 0;
            return;
        }

        DateTime today = DateTime.Today;

        if (last.Value == today)
        {
            IsAvailableToday = false;
            return;
        }

        if (last.Value == today.AddDays(-1))
        {
            IsAvailableToday = true;
            ClaimedDayToday = 0;
            return;
        }

        IsAvailableToday = true;
        NextDay = 1;
        mask = 0;
        ClaimedDayToday = 0;
    }

    private static DateTime? TryParseDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (DateTime.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out DateTime parsed))
        {
            return parsed;
        }

        return null;
    }
}