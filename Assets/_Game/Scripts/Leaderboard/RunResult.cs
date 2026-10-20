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
    // RUN CONTEXT (map + theme)
    // =========================================================

    [Tooltip("Индекс карты, на которой прошёл забег. -1 — карта неизвестна.")]
    public int mapIndex = -1;

    [Tooltip("Локализованное имя карты для отображения в локальном списке.")]
    public string mapName;

    [Tooltip("Идентификатор темы забега (RunModifier) для локального списка.")]
    public string runModifier;

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

        // ВАЖНО: конструктор [Serializable]-типа Unity вызывает при
        // десериализации объектов (в т.ч. полей MonoBehaviour), и в этот
        // момент запрещены обращения к движку вроде Application.buildGUID
        // ("get_buildGUID is not allowed to be called during serialization").
        // Хеш сборки заполняется лениво, уже во время игры.
        buildHash = string.Empty;

        mapIndex = -1;
        mapName = string.Empty;
        runModifier = string.Empty;
    }

    // =========================================================
    // JSON SERIALIZATION
    // =========================================================

    public string ToJson()
    {
        EnsureBuildHash();
        return JsonUtility.ToJson(this, true);
    }

    /// <summary>
    /// Заполняет хеш сборки, если он ещё не задан. Читать
    /// Application.buildGUID можно только вне сериализации, поэтому
    /// вызывается из рантайм-путей, а не из конструктора.
    /// </summary>
    public void EnsureBuildHash()
    {
        if (!string.IsNullOrEmpty(buildHash))
            return;

        try
        {
            buildHash = Application.buildGUID;
        }
        catch
        {
            buildHash = string.Empty;
        }
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

    /// <summary>
    /// Ранг забега. Игра бесконечная, поэтому длительность НЕ влияет
    /// на оценку: награждается глубина (пройденные волны), чистота
    /// (идеальные волны) и стиль (максимальное комбо). Прежняя
    /// формула с бонусом за скорость противоречила бесконечному
    /// режиму — долгий забег никогда не мог получить высокий ранг.
    /// </summary>
    public void CalculateRank()
    {
        // S+ : очень глубокий, чистый и стильный забег.
        if (wavesCleared >= 30 && maxCombo >= 50 && perfectWaves >= 10)
        {
            rank = "S+";
        }
        // S  : глубокий забег с высоким комбо и чистыми волнами.
        else if (wavesCleared >= 25 && maxCombo >= 30 && perfectWaves >= 5)
        {
            rank = "S";
        }
        // A  : уверенная глубина и высокое комбо.
        else if (wavesCleared >= 18 && maxCombo >= 20)
        {
            rank = "A";
        }
        // B  : крепкий забег.
        else if (wavesCleared >= 12 && maxCombo >= 10)
        {
            rank = "B";
        }
        // C  : забег состоялся.
        else if (wavesCleared >= 5)
        {
            rank = "C";
        }
        // F  : провал.
        else
        {
            rank = "F";
        }
    }

    // =========================================================
    // STYLE BONUS CALCULATION
    // =========================================================

    /// <summary>
    /// Бонус за стиль. Все числа берутся из BonusSettings — единого
    /// источника правды для правил начисления очков. Скорость здесь
    /// намеренно не участвует: бесконечный режим не должен поощрять
    /// быстрое завершение забега.
    /// </summary>
    public void CalculateStyleBonus(BonusSettings settings)
    {
        int bonus = 0;

        if (settings != null)
        {
            // Глубина забега — основной вклад вместо скорости.
            bonus += wavesCleared * settings.waveBonusPerWave;

            if (settings.deepWaveThreshold > 0 &&
                wavesCleared >= settings.deepWaveThreshold)
            {
                bonus += settings.deepWaveBonus;
            }

            // No-hit bonus: за perfect waves
            bonus += perfectWaves * settings.perfectWaveBonus;

            // Combo bonus: за макс. комбо
            if (maxCombo >= settings.comboThreshold50) bonus += settings.comboBonus50;
            else if (maxCombo >= settings.comboThreshold30) bonus += settings.comboBonus30;
            else if (maxCombo >= settings.comboThreshold20) bonus += settings.comboBonus20;
            else if (maxCombo >= settings.comboThreshold10) bonus += settings.comboBonus10;

            // Efficiency bonus: урон / полученный урон
            if (totalDamageTaken > 0f)
            {
                float efficiency = totalDamageDealt / totalDamageTaken;

                if (efficiency >= settings.efficiencyThresholdHigh)
                    bonus += settings.efficiencyBonusHigh;
                else if (efficiency >= settings.efficiencyThresholdMedium)
                    bonus += settings.efficiencyBonusMedium;
            }

            // Multi-kill streak bonus
            bonus += multiKillStreaks * settings.multiKillBonus;
        }
        else
        {
            // Резервные значения, если настройки не назначены: форма та
            // же, что и в BonusSettings по умолчанию, без скорости.
            bonus += wavesCleared * 200;
            bonus += perfectWaves * 500;

            if (maxCombo >= 50) bonus += 1000;
            else if (maxCombo >= 30) bonus += 500;
            else if (maxCombo >= 20) bonus += 250;
            else if (maxCombo >= 10) bonus += 100;

            if (totalDamageTaken > 0f)
            {
                float efficiency = totalDamageDealt / totalDamageTaken;
                if (efficiency >= 10f) bonus += 1500;
                else if (efficiency >= 5f) bonus += 750;
            }

            bonus += multiKillStreaks * 100;
        }

        styleBonus = bonus;
        finalScore = score + bonus;
    }
}
