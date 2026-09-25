using UnityEngine;
using TMPro;
using System.Collections;

/// <summary>
/// Live HUD — показывает счёт и бонусы в стиле Ultrakill.
/// Одна строка счёта + всплывающие бонусы с названием.
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

    // =========================================================
    // CACHED REFERENCES
    // =========================================================

    private RunMetrics cachedMetrics;
    private ComboSystem cachedCombo;
    private WaveManager cachedWaveManager;
    private ScoreManager cachedScoreManager;
    private BonusSettings bonusSettings;

    private int lastScore;
    private int lastCombo;
    private int lastWave;
    private float lastRunTime;
    private string lastTimeString;

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
        bonusSettings = FindAnyObjectByType<BonusSettings>();

        if (comboPanel != null)
            comboPanel.SetActive(false);
        if (scorePanel != null)
            scorePanel.SetActive(true);

        RebuildScoreText();
    }

    private void OnEnable()
    {
        if (cachedCombo != null)
            cachedCombo.OnComboChanged += OnComboChanged;

        if (cachedScoreManager != null)
            cachedScoreManager.OnScoreAdded += OnScoreAdded;
    }

    private void OnDisable()
    {
        if (cachedCombo != null)
            cachedCombo.OnComboChanged -= OnComboChanged;

        if (cachedScoreManager != null)
            cachedScoreManager.OnScoreAdded -= OnScoreAdded;
    }

    // =========================================================
    // UPDATE — каждый кадр
    // =========================================================

    private void Update()
    {
        if (cachedScoreManager == null)
            return;

        bool changed = false;

        int currentScore = cachedScoreManager.Score;
        if (currentScore != lastScore)
        {
            lastScore = currentScore;
            changed = true;
        }

        if (cachedWaveManager != null)
        {
            int currentWave = cachedWaveManager.CurrentWave;
            if (currentWave != lastWave)
            {
                lastWave = currentWave;
                changed = true;
            }
        }

        // Время — обновляем раз в секунду
        if (cachedMetrics != null)
        {
            float currentTime = cachedMetrics.RunTime;
            if (Mathf.Abs(currentTime - lastRunTime) > 1f)
            {
                lastRunTime = currentTime;
                lastTimeString = FormatTime(currentTime);
                changed = true;
            }
        }

        if (changed)
            RebuildScoreText();
    }

    // Собираем строку только при изменении счёта/волны/времени —
    // без Split/переприсвоения text каждый кадр.
    private void RebuildScoreText()
    {
        if (scoreText == null)
            return;

        string text = lastScore.ToString("N0");

        if (cachedWaveManager != null)
            text += " | WAVE " + lastWave;

        if (cachedMetrics != null &&
            lastTimeString != null)
        {
            text += " | TIME " + lastTimeString;
        }

        scoreText.text = text;
    }

    // =========================================================
    // SCORE ADDED — всплывающие бонусы с названием
    // =========================================================

    private void OnScoreAdded(int amount, string bonusName)
    {
        // Показываем всплывающий бонус с названием
        ShowBonusPopup(bonusName, amount);
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

        // Проверяем пороги комбо и начисляем бонусы
        CheckComboThresholds(combo);
    }

    private void CheckComboThresholds(int combo)
    {
        if (bonusSettings == null || cachedScoreManager == null)
            return;

        int lastThreshold = cachedCombo.GetLastComboThresholdReached();

        // Проверяем каждый порог
        if (combo >= bonusSettings.comboThreshold50 && lastThreshold < 50)
        {
            cachedScoreManager.AddBonus(bonusSettings.comboBonus50, "Комбо 50x");
            cachedCombo.SetLastComboThresholdReached(50);
        }
        else if (combo >= bonusSettings.comboThreshold30 && lastThreshold < 30)
        {
            cachedScoreManager.AddBonus(bonusSettings.comboBonus30, "Комбо 30x");
            cachedCombo.SetLastComboThresholdReached(30);
        }
        else if (combo >= bonusSettings.comboThreshold20 && lastThreshold < 20)
        {
            cachedScoreManager.AddBonus(bonusSettings.comboBonus20, "Комбо 20x");
            cachedCombo.SetLastComboThresholdReached(20);
        }
        else if (combo >= bonusSettings.comboThreshold10 && lastThreshold < 10)
        {
            cachedScoreManager.AddBonus(bonusSettings.comboBonus10, "Комбо 10x");
            cachedCombo.SetLastComboThresholdReached(10);
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
    // BONUS POPUPS — всплывающие бонусы с названием
    // =========================================================

    private void ShowBonusPopup(string bonusName, int amount)
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
            popupText.text = bonusName + ": +" + amount.ToString("N0");
            popupText.color = new Color(1f, 0.84f, 0f); // Золотой
        }

        // Анимация движения вверх и смещение вниз
        RectTransform rect = popup.GetComponent<RectTransform>();
        if (rect != null)
        {
            // Начальная позиция — чуть выше контейнера
            rect.anchoredPosition = new Vector2(0, 50f * bonusPopupCount);
            
            // Анимация подъёма
            Vector3 endPos = rect.anchoredPosition;
            endPos.y += 50f;
            
            // Запускаем корутину для анимации
            StartCoroutine(AnimatePopup(rect, endPos));

            // Смещаем все остальные popup'ы вниз
            ShiftPopupsDown(bonusPopupCount);
        }

        bonusPopupCount++;

        // Удаляем через popupLifetime с пересчётом позиций
        StartCoroutine(DestroyWithShift(popup));
    }

    private System.Collections.IEnumerator AnimatePopup(RectTransform rect, Vector3 endPos)
    {
        float duration = 0.3f;
        float elapsed = 0f;
        Vector3 startPos = rect.anchoredPosition;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            rect.anchoredPosition = Vector3.Lerp(startPos, endPos, t);
            yield return null;
        }

        rect.anchoredPosition = endPos;
    }

    private System.Collections.IEnumerator DestroyWithShift(GameObject popup)
    {
        yield return new WaitForSeconds(popupLifetime);
        
        // Удаляем popup
        Destroy(popup);
        bonusPopupCount--;
        
        // Пересчитываем позиции всех popup'ов
        ShiftPopupsUp();
    }

    private void ShiftPopupsDown(int startIndex)
    {
        for (int i = startIndex; i < bonusPopupContainer.childCount; i++)
        {
            RectTransform rect = bonusPopupContainer.GetChild(i).GetComponent<RectTransform>();
            if (rect != null)
            {
                Vector3 pos = rect.anchoredPosition;
                pos.y += 50f; // Сдвигаем вверх (новые popup'ы выше)
                rect.anchoredPosition = pos;
            }
        }
    }

    private void ShiftPopupsUp()
    {
        // Пересчитываем позиции всех popup'ов
        for (int i = 0; i < bonusPopupContainer.childCount; i++)
        {
            RectTransform rect = bonusPopupContainer.GetChild(i).GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchoredPosition = new Vector2(0, 50f * i);
            }
        }
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
