using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UI для отображения метрик и ранга на экране Game Over.
/// </summary>
public class GameOverLeaderboardUI : MonoBehaviour
{
    [Header("Root")]
    [Tooltip("Контейнер всей панели. Показывается только на экране Game Over.")]
    [SerializeField] private GameObject rootPanel;

    [Header("Rank Display")]
    [SerializeField] private GameObject rankPanel;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private TMP_Text rankTitleText;

    [Header("Stats Panels")]
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private GameObject speedPanel;
    [SerializeField] private GameObject stylePanel;

    [Header("Score Display")]
    [SerializeField] private TMP_Text baseScoreText;
    [SerializeField] private TMP_Text styleBonusText;
    [SerializeField] private TMP_Text finalScoreText;

    [Header("Detailed Stats")]
    [SerializeField] private TMP_Text killsText;
    [SerializeField] private TMP_Text wavesText;
    [SerializeField] private TMP_Text runTimeText;
    [SerializeField] private TMP_Text maxComboText;
    [SerializeField] private TMP_Text perfectWavesText;
    [SerializeField] private TMP_Text avgKillTimeText;
    [SerializeField] private TMP_Text fastestKillTimeText;
    [SerializeField] private TMP_Text timeToFirstKillText;
    [SerializeField] private TMP_Text timeToFirstBossText;
    [SerializeField] private TMP_Text bestWaveClearTimeText;
    [SerializeField] private TMP_Text damageEfficiencyText;
    [SerializeField] private TMP_Text criticalHitsText;
    [SerializeField] private TMP_Text abilitiesUsedText;
    [SerializeField] private TMP_Text dashDodgesText;
    [SerializeField] private TMP_Text multiKillStreaksText;

    private void Awake()
    {
        HideAll();
        HideRoot();
    }

    /// <summary>
    /// Панель результатов живёт отдельно от основного Game Over,
    /// поэтому её видимость держим по состоянию забега.
    /// </summary>
    private void Update()
    {
        bool visible =
            GameOverManager.Instance != null &&
            GameOverManager.Instance.IsGameOver;

        if (rootPanel != null &&
            rootPanel.activeSelf != visible)
        {
            rootPanel.SetActive(visible);
        }
    }

    private void ShowRoot()
    {
        if (rootPanel != null)
            rootPanel.SetActive(true);
    }

    private void HideRoot()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    /// <summary>
    /// Показать результаты забега. Результат приходит уже посчитанным:
    /// экран итогов не пересчитывает метрики, поэтому его счёт совпадает
    /// с отправленным в лидерборд.
    /// </summary>
    public void ShowResults(RunResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[LeaderboardUI] RunResult is null");
            return;
        }

        // Показываем ранг
        ShowRank(result.rank);

        // Показываем очки
        ShowScores(result);

        // Показываем детали
        ShowStats(result);
        ShowSpeedStats(result);
        ShowStyleStats(result);

        // Показываем панели
        ShowAll();
        ShowRoot();
    }

    // =========================================================
    // RANK DISPLAY
    // =========================================================

    private void ShowRank(string rank)
    {
        if (rankPanel != null)
            rankPanel.SetActive(true);

        if (rankText != null)
            rankText.text = rank;

        if (rankTitleText != null)
        {
            rankTitleText.text = GetRankTitle(rank);
        }

        // Красим ранг в цвет
        if (rankText != null)
        {
            rankText.color = GetRankColor(rank);
        }
    }

    private string GetRankTitle(string rank)
    {
        switch (rank)
        {
            case "S+": return Lang.Get("rank.s_plus");
            case "S": return Lang.Get("rank.s");
            case "A": return Lang.Get("rank.a");
            case "B": return Lang.Get("rank.b");
            case "C": return Lang.Get("rank.c");
            default: return Lang.Get("rank.f");
        }
    }

    private Color GetRankColor(string rank)
    {
        switch (rank)
        {
            case "S+": return new Color(1f, 0.84f, 0f); // Золотой
            case "S": return new Color(1f, 0.5f, 0f);   // Оранжевый
            case "A": return new Color(0f, 1f, 0f);     // Зелёный
            case "B": return new Color(0f, 0.6f, 1f);   // Синий
            case "C": return new Color(0.7f, 0.7f, 0.7f); // Серый
            default: return new Color(1f, 0f, 0f);      // Красный
        }
    }

    // =========================================================
    // SCORE DISPLAY
    // =========================================================

    private void ShowScores(RunResult result)
    {
        if (baseScoreText != null)
            baseScoreText.text = Lang.Get("results.score", result.score.ToString("N0"));

        if (styleBonusText != null)
            styleBonusText.text = Lang.Get("results.style_bonus", result.styleBonus.ToString("N0"));

        if (finalScoreText != null)
            finalScoreText.text = Lang.Get("results.total", result.finalScore.ToString("N0"));
    }

    // =========================================================
    // STATS DISPLAY
    // =========================================================

    private void ShowStats(RunResult result)
    {
        if (statsPanel != null)
            statsPanel.SetActive(true);

        if (killsText != null)
            killsText.text = Lang.Get("stat.kills", result.kills);

        if (wavesText != null)
            wavesText.text = Lang.Get("stat.waves", result.wavesCleared);

        if (runTimeText != null)
            runTimeText.text = Lang.Get("stat.time", FormatTime(result.runTime));

        if (maxComboText != null)
            maxComboText.text = Lang.Get("stat.max_combo", result.maxCombo);

        if (perfectWavesText != null)
            perfectWavesText.text = Lang.Get("stat.perfect_waves", result.perfectWaves);

        if (criticalHitsText != null)
            criticalHitsText.text = Lang.Get("stat.critical_hits", result.totalCriticalHits);

        if (abilitiesUsedText != null)
            abilitiesUsedText.text = Lang.Get("stat.abilities_used", result.abilitiesUsed);

        if (dashDodgesText != null)
            dashDodgesText.text = Lang.Get("stat.dash_dodges", result.dashDodges);

        if (multiKillStreaksText != null)
            multiKillStreaksText.text = Lang.Get("stat.multi_kills", result.multiKillStreaks);
    }

    private void ShowSpeedStats(RunResult result)
    {
        if (speedPanel != null)
            speedPanel.SetActive(true);

        if (avgKillTimeText != null)
            avgKillTimeText.text = Lang.Get("stat.avg_kill_time", FormatTime(result.averageKillTime));

        if (fastestKillTimeText != null)
            fastestKillTimeText.text = Lang.Get("stat.fastest_kill", FormatTime(result.fastestKillTime));

        if (timeToFirstKillText != null)
            timeToFirstKillText.text = Lang.Get("stat.time_to_first_kill", FormatTime(result.timeToFirstKill));

        if (timeToFirstBossText != null)
            timeToFirstBossText.text = Lang.Get("stat.time_to_first_boss", FormatTime(result.timeToFirstBoss));

        if (bestWaveClearTimeText != null)
            bestWaveClearTimeText.text = Lang.Get("stat.best_wave_clear", FormatTime(result.bestWaveClearTime));
    }

    private void ShowStyleStats(RunResult result)
    {
        if (stylePanel != null)
            stylePanel.SetActive(true);

        if (damageEfficiencyText != null)
        {
            damageEfficiencyText.text = Lang.Get("stat.damage_efficiency", result.damageEfficiency.ToString("F1"));
        }
    }

    // =========================================================
    // UTILITY
    // =========================================================

    private string FormatTime(float seconds)
    {
        if (seconds <= 0f || seconds == float.MaxValue)
            return Lang.Get("stat.no_time");

        TimeSpan ts = TimeSpan.FromSeconds(seconds);
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }

    private void ShowAll()
    {
        if (rankPanel != null) rankPanel.SetActive(true);
        if (statsPanel != null) statsPanel.SetActive(true);
        if (speedPanel != null) speedPanel.SetActive(true);
        if (stylePanel != null) stylePanel.SetActive(true);
    }

    private void HideAll()
    {
        if (rankPanel != null) rankPanel.SetActive(false);
        if (statsPanel != null) statsPanel.SetActive(false);
        if (speedPanel != null) speedPanel.SetActive(false);
        if (stylePanel != null) stylePanel.SetActive(false);
    }
}
