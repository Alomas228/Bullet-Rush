using UnityEngine;
using System;

public class DailyRewardManager : MonoBehaviour
{
    public static DailyRewardManager Instance { get; private set; }

    private const string PrefsKey_LastClaim = "DailyReward.LastClaimDate";
    private const string PrefsKey_DayCounter = "DailyReward.DayCounter";

    public int CurrentDayCounter { get; private set; }
    public bool IsAvailableToday { get; private set; }

    public event Action<int> OnRewardClaimed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        CheckAvailability();
    }

    private void CheckAvailability()
    {
        string lastClaimDate = PlayerPrefs.GetString(PrefsKey_LastClaim, "");
        string today = System.DateTime.Now.ToString("yyyy-MM-dd");

        if (lastClaimDate != today)
        {
            IsAvailableToday = true;
            CurrentDayCounter = PlayerPrefs.GetInt(PrefsKey_DayCounter, 1);
        }
        else
        {
            IsAvailableToday = false;
            CurrentDayCounter = PlayerPrefs.GetInt(PrefsKey_DayCounter, 1);
        }
    }

    public int GetCoinsForDay(int day)
    {
        return day * 50;
    }

    public int GetXPForDay(int day)
    {
        return day * 100;
    }

    public bool ClaimReward()
    {
        if (!IsAvailableToday)
        {
            Debug.LogWarning("[DailyReward] Награда ещё не доступна.");
            return false;
        }

        int coins = GetCoinsForDay(CurrentDayCounter);
        int xp = GetXPForDay(CurrentDayCounter);

        XpManager xpManager = XpManager.Instance;
        if (xpManager != null)
        {
            if (coins > 0) xpManager.AddCoins(coins);
            if (xp > 0) xpManager.AddPlayerXP(xp);
        }

        PlayerPrefs.SetString(PrefsKey_LastClaim, System.DateTime.Now.ToString("yyyy-MM-dd"));
        PlayerPrefs.SetInt(PrefsKey_DayCounter, CurrentDayCounter + 1);
        PlayerPrefs.Save();

        IsAvailableToday = false;

        Debug.Log($"[DailyReward] Получена награда за день {CurrentDayCounter}: {coins} монет, {xp} XP");

        OnRewardClaimed?.Invoke(CurrentDayCounter);

        return true;
    }
}