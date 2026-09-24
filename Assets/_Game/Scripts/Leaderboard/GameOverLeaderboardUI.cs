using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// UI для отображения метрик и ранга на экране Game Over.
/// </summary>
public class GameOverLeaderboardUI : MonoBehaviour
{
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

    private RunMetrics cachedMetrics;

    private void Awake()
    {
        HideAll();
    }

    /// <summary>
    /// Показать результаты забега.
    /// </summary>
    public void ShowResults(RunMetrics metrics)
    {
        cachedMetrics = metrics;

        if (metrics == null)
        {
            Debug.LogWarning("[LeaderboardUI] RunMetrics is null");
            return;
        }

        // Собираем результат
        RunResult result = metrics.CollectResult();

        // Показываем ранг
        ShowRank(result.rank);

        // Показываем очки
        ShowScores(result);

        // Показываем детали
        ShowStats(metrics, result);
        ShowSpeedStats(metrics);
        ShowStyleStats(metrics);

        // Показываем панели
        ShowAll();
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
            case "S+": return "PERFECT RUN";
            case "S": return "LEGENDARY";
            case "A": return "EXCELLENT";
            case "B": return "GREAT";
            case "C": return "GOOD";
            default: return "TRY AGAIN";
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
            baseScoreText.text = $"Score: {result.score:N0}";

        if (styleBonusText != null)
            styleBonusText.text = $"+Style Bonus: {result.styleBonus:N0}";

        if (finalScoreText != null)
            finalScoreText.text = $"Total: {result.finalScore:N0}";
    }

    // =========================================================
    // STATS DISPLAY
    // =========================================================

        private void ShowStats(RunMetrics metrics, RunResult result)
    {
        if (statsPanel != null)
            statsPanel.SetActive(true);

        if (killsText != null)
            killsText.text = $"Kills: {metrics.Kills}";

        if (wavesText != null)
            wavesText.text = $"Waves: {result.wavesCleared}";

        if (runTimeText != null)
            runTimeText.text = $"Time: {FormatTime(metrics.RunTime)}";

        if (maxComboText != null)
            maxComboText.text = $"Max Combo: {metrics.MaxCombo}";

        if (perfectWavesText != null)
            perfectWavesText.text = $"Perfect Waves: {metrics.PerfectWaves}";

        if (criticalHitsText != null)
            criticalHitsText.text = $"Critical Hits: {metrics.CriticalHits}";

        if (abilitiesUsedText != null)
            abilitiesUsedText.text = $"Abilities Used: {metrics.AbilitiesUsed}";

        if (dashDodgesText != null)
            dashDodgesText.text = $"Dash Dodges: {metrics.DashDodges}";

        if (multiKillStreaksText != null)
            multiKillStreaksText.text = $"Multi-Kills: {metrics.MultiKillStreaks}";
    }

    private void ShowSpeedStats(RunMetrics metrics)
    {
        if (speedPanel != null)
            speedPanel.SetActive(true);

        if (avgKillTimeText != null)
            avgKillTimeText.text = $"Avg Kill Time: {FormatTime(metrics.AverageKillTime)}";

        if (fastestKillTimeText != null)
            fastestKillTimeText.text = $"Fastest Kill: {FormatTime(metrics.FastestKillTime)}";

        if (timeToFirstKillText != null)
            timeToFirstKillText.text = $"Time to First Kill: {FormatTime(metrics.TimeToFirstKill)}";

        if (timeToFirstBossText != null)
            timeToFirstBossText.text = $"Time to First Boss: {FormatTime(metrics.TimeToFirstBoss)}";

        if (bestWaveClearTimeText != null)
            bestWaveClearTimeText.text = $"Best Wave Clear: {FormatTime(metrics.BestWaveClearTime)}";
    }

    private void ShowStyleStats(RunMetrics metrics)
    {
        if (stylePanel != null)
            stylePanel.SetActive(true);

        if (damageEfficiencyText != null)
        {
            float efficiency = 0f;
            if (metrics.TotalDamageTaken > 0f)
                efficiency = metrics.TotalDamageDealt / metrics.TotalDamageTaken;

            damageEfficiencyText.text = $"Damage Efficiency: {efficiency:F1}x";
        }
    }

    // =========================================================
    // UTILITY
    // =========================================================

    private string FormatTime(float seconds)
    {
        if (seconds <= 0f || seconds == float.MaxValue)
            return "--:--";

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
