using UnityEngine;
using TMPro;

/// <summary>
/// Live HUD — показывает счёт и бонусы в стиле Ultrakill.
/// Одна строка счёта + всплывающие бонусы.
/// </summary>
public class GameplayHUD : MonoBehaviour
{
    [Header("Score Display")]
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private GameObject scorePanel;

    [Header("Combo Display")]
    [SerializeField] private GameObject comboPanel;
    [SerializeField] private TMP_Text comboCountText;
    [SerializeField] private TMP_Text comboMultiplierText;

    [Header("Bonus Popups")]
    [SerializeField] private GameObject bonusPopupPrefab;
    [SerializeField] private Transform bonusPopupContainer;
    [SerializeField] private float popupLifetime = 2f;
    [SerializeField] private float popupMoveSpeed = 100f;

    // =========================================================
    // CACHED REFERENCES
    // =========================================================

    private RunMetrics cachedMetrics;
    private ComboSystem cachedCombo;
    private WaveManager cachedWaveManager;
    private ScoreManager cachedScoreManager;

    private int lastScore;
    private int lastCombo;
    private float lastComboMultiplier;
    private float lastRunTime;

    private int bonusPopupCount;
    private const int maxBonusPopups = 8;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        cachedMetrics = FindAnyObjectByType<RunMetrics>();
        cachedCombo = FindAnyObjectByType<ComboSystem>();
        cachedWaveManager = FindAnyObjectByType<WaveManager>();
        cachedScoreManager = FindAnyObjectByType<ScoreManager>();

        if (comboPanel != null)
            comboPanel.SetActive(false);
        if (scorePanel != null)
            scorePanel.SetActive(true);
    }

    private void OnEnable()
    {
        if (cachedCombo != null)
            cachedCombo.OnComboChanged += OnComboChanged;
    }

    private void OnDisable()
    {
        if (cachedCombo != null)
            cachedCombo.OnComboChanged -= OnComboChanged;
    }

    // =========================================================
    // UPDATE — каждый кадр
    // =========================================================

    private void Update()
    {
        if (cachedScoreManager == null)
            return;

        // Обновляем основной счёт
        int currentScore = cachedScoreManager.Score;
        if (currentScore != lastScore)
        {
            lastScore = currentScore;
            if (scoreText != null)
                scoreText.text = currentScore.ToString("N0");
        }

        // Обновляем волну
        if (cachedWaveManager != null && scoreText != null)
        {
            string suffix = " | WAVE " + cachedWaveManager.CurrentWave;
            scoreText.text += suffix;
        }

        // Обновляем время (каждую секунду)
        if (cachedMetrics != null)
        {
            float currentTime = cachedMetrics.RunTime;
            if (Mathf.Abs(currentTime - lastRunTime) > 1f)
            {
                lastRunTime = currentTime;
                if (scoreText != null)
                    scoreText.text += " | " + FormatTime(currentTime);
            }
        }
    }

    // =========================================================
    // COMBO
    // =========================================================

    private void OnComboChanged(int combo)
    {
        lastCombo = combo;

        if (comboPanel != null)
            comboPanel.SetActive(combo > 0);

        if (comboCountText != null)
            comboCountText.text = combo.ToString();

        if (comboMultiplierText != null)
        {
            float multiplier = GetComboMultiplier(combo);
            if (multiplier >= 1.5f)
            {
                comboMultiplierText.text = multiplier.ToString("0.0") + "x";
                comboMultiplierText.color = GetComboColor(combo);
            }
            else
            {
                comboMultiplierText.text = "";
            }
        }
    }

    private Color GetComboColor(int combo)
    {
        if (combo >= 50) return new Color(1f, 0.84f, 0f); // Золотой
        if (combo >= 30) return new Color(1f, 0.5f, 0f);   // Оранжевый
        if (combo >= 20) return new Color(0f, 1f, 0f);     // Зелёный
        if (combo >= 10) return new Color(0f, 0.6f, 1f);   // Синий
        return Color.white;
    }

    private float GetComboMultiplier(int combo)
    {
        if (combo >= 50) return 5f;
        if (combo >= 30) return 3f;
        if (combo >= 20) return 2f;
        if (combo >= 10) return 1.5f;
        return 1f;
    }

    // =========================================================
    // BONUS POPUPS — всплывающие бонусы
    // =========================================================

    /// <summary>
    /// Вызывается при получении бонуса.
    /// Показывает всплывающий текст "+500" в стиле Ultrakill.
    /// </summary>
    public void ShowBonusPopup(string bonusName, int bonusAmount)
    {
        if (bonusPopupPrefab == null || bonusPopupContainer == null)
            return;

        // Удаляем старую запись если переполнено
        if (bonusPopupCount >= maxBonusPopups && bonusPopupContainer.childCount > 0)
        {
            Destroy(bonusPopupContainer.GetChild(0).gameObject);
            bonusPopupCount--;
        }

        // Создаём новую запись
        GameObject popup = Instantiate(bonusPopupPrefab, bonusPopupContainer);
        TMP_Text popupText = popup.GetComponentInChildren<TMP_Text>();
        if (popupText != null)
        {
            popupText.text = bonusName + " +" + bonusAmount.ToString("N0");
            popupText.color = bonusAmount > 1000 ? new Color(1f, 0.84f, 0f) : Color.white;
        }

        // Анимация движения вверх
        RectTransform rect = popup.GetComponent<RectTransform>();
        if (rect != null)
        {
            Vector3 startPos = rect.anchoredPosition;
            Vector3 endPos = startPos;
            endPos.y += 50f; // Поднимаем вверх

            Destroy(popup, popupLifetime);
        }

        bonusPopupCount++;
    }

    // =========================================================
    // UTILITY
    // =========================================================

    private string FormatTime(float seconds)
    {
        if (seconds <= 0f) return "00:00";

        System.TimeSpan ts = System.TimeSpan.FromSeconds(seconds);
        return $"{ts.Minutes:D2}:{ts.Seconds:D2}";
    }
}
