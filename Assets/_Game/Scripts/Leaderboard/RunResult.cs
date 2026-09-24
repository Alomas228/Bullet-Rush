using System;
using UnityEngine;

/// <summary>
/// Результат забега — структура данных для лидерборда.
/// Собирает все метрики и отправляет на сервер.
/// </summary>
[Serializable]
public class RunResult
{
    // =========================================================
    // BASIC STATS
    // =========================================================

    public string playerName;
    public int score;
    public int kills;
    public int wavesCleared;
    public float runTime;

    // =========================================================
    // COMBO & STYLE
    // =========================================================

    public int maxCombo;
    public int totalCriticalHits;
    public float averageKillTime;
    public float fastestKillTime;

    // =========================================================
    // SPEED METRICS
    // =========================================================

    public float timeToFirstKill;
    public float timeToFirstBoss;
    public float bestWaveClearTime;

    // =========================================================
    // SURVIVAL & EFFICIENCY
    // =========================================================

    public int perfectWaves;
    public int noHitKillStreak;
    public float damageEfficiency;
    public float totalDamageDealt;
    public float totalDamageTaken;

    // =========================================================
    // ABILITIES & MECHANICS
    // =========================================================

    public int abilitiesUsed;
    public int dashDodges;
    public int multiKillStreaks;
    public float totalDistance;
    public int totalDashDistance;

    // =========================================================
    // RANK & BONUS
    // =========================================================

    public string rank;
    public int styleBonus;
    public int finalScore;

    // =========================================================
    // TIMESTAMPS
    // =========================================================

    public long timestamp;
    public string buildHash;

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    public RunResult()
    {
        playerName = string.Empty;
        score = 0;
        kills = 0;
        wavesCleared = 0;
        runTime = 0f;

        maxCombo = 0;
        totalCriticalHits = 0;
        averageKillTime = 0f;
        fastestKillTime = float.MaxValue;

        timeToFirstKill = float.MaxValue;
        timeToFirstBoss = float.MaxValue;
        bestWaveClearTime = float.MaxValue;

        perfectWaves = 0;
        noHitKillStreak = 0;
        damageEfficiency = 0f;
        totalDamageDealt = 0f;
        totalDamageTaken = 0f;

        abilitiesUsed = 0;
        dashDodges = 0;
        multiKillStreaks = 0;

        totalDistance = 0f;
        totalDashDistance = 0;

        rank = "F";
        styleBonus = 0;
        finalScore = 0;

        timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        buildHash = Application.buildGUID;
    }

    // =========================================================
    // JSON SERIALIZATION
    // =========================================================

    public string ToJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    public static RunResult FromJson(string json)
    {
        try
        {
            return JsonUtility.FromJson<RunResult>(json);
        }
        catch
        {
            return new RunResult();
        }
    }

    // =========================================================
    // RANK CALCULATION
    // =========================================================

    public void CalculateRank()
    {
        // S+ : время < 3 мин, комбо > 50, perfect waves > 5
        if (runTime < 180f && maxCombo > 50 && perfectWaves > 5)
        {
            rank = "S+";
        }
        // S  : время < 5 мин, комбо > 30, perfect waves > 3
        else if (runTime < 300f && maxCombo > 30 && perfectWaves > 3)
        {
            rank = "S";
        }
        // A  : время < 8 мин, комбо > 20
        else if (runTime < 480f && maxCombo > 20)
        {
            rank = "A";
        }
        // B  : время < 12 мин, комбо > 10
        else if (runTime < 720f && maxCombo > 10)
        {
            rank = "B";
        }
        // C  : просто финиш (пройдено хотя бы 3 волны)
        else if (wavesCleared >= 3)
        {
            rank = "C";
        }
        // F  : провал
        else
        {
            rank = "F";
        }
    }

    // =========================================================
    // STYLE BONUS CALCULATION
    // =========================================================

    public void CalculateStyleBonus()
    {
        int bonus = 0;

        // Speed bonus: чем быстрее — тем больше
        if (runTime < 180f) bonus += 5000;
        else if (runTime < 300f) bonus += 3000;
        else if (runTime < 480f) bonus += 1500;
        else if (runTime < 720f) bonus += 500;

        // No-hit bonus: за perfect waves
        bonus += perfectWaves * 1000;

        // Combo bonus: за макс. комбо
        if (maxCombo >= 50) bonus += 4000;
        else if (maxCombo >= 30) bonus += 2500;
        else if (maxCombo >= 20) bonus += 1500;
        else if (maxCombo >= 10) bonus += 500;

        // Efficiency bonus: урон/полученный урон
        if (totalDamageTaken > 0f)
        {
            float efficiency = totalDamageDealt / totalDamageTaken;
            if (efficiency > 10f) bonus += 3000;
            else if (efficiency > 5f) bonus += 2000;
            else if (efficiency > 2f) bonus += 1000;
        }

        // Multi-kill streak bonus
        bonus += multiKillStreaks * 500;

        styleBonus = bonus;
        finalScore = score + bonus;
    }
}
