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

    private int lastScore;
    private int lastCombo;
    private int lastWave;
    private float lastRunTime;
    private string lastTimeString;

    private int bonusPopupCount;
    private const int maxBonusPopups = 8;
    private bool pvpHidden;

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void Awake()
    {
        pvpHidden = GameModeManager.IsPvp;
        cachedMetrics = FindAnyObjectByType<RunMetrics>();
        cachedCombo = FindAnyObjectByType<ComboSystem>();
        cachedWaveManager = FindAnyObjectByType<WaveManager>();
        cachedScoreManager = FindAnyObjectByType<ScoreManager>();

        if (comboPanel != null)
            comboPanel.SetActive(false);
        if (scorePanel != null)
            scorePanel.SetActive(!pvpHidden);

        RebuildScoreText();
    }

    public void SetPvpHidden(bool hidden)
    {
        pvpHidden = hidden;
        if (scorePanel != null)
            scorePanel.SetActive(!hidden);
        if (comboPanel != null)
            comboPanel.SetActive(!hidden && lastCombo > 0);
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
            comboPanel.SetActive(!pvpHidden && combo > 0);

        if (comboCountText != null)
            comboCountText.text = combo.ToString();

        if (comboMultiplierText != null)
        {
            // Значение берём у самой системы комбо: HUD только показывает,
            // он не задаёт правила начисления.
            float multiplier =
                cachedCombo != null
                    ? cachedCombo.ComboMultiplier
                    : 1f;

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

    // =========================================================
    // BONUS POPUPS — всплывающие бонусы с названием
    // =========================================================

    private void ShowBonusPopup(string bonusName, int amount)
    {
        SpawnPopup(
            bonusName + ": +" + amount.ToString("N0"),
            new Color(1f, 0.84f, 0f),
            FontStyles.Bold
        );
    }

    // =========================================================
    // MAP ANNOUNCEMENT — баннер входа на карту
    // =========================================================

    /// <summary>
    /// Баннер на старте забега: название карты и подпись её механики.
    /// Рисуется через тот же пул всплывающих бонусов, но холодным
    /// цветом и подольше, чтобы успеть прочитаться.
    /// </summary>
    public void ShowMapAnnouncement(
        string title,
        string subtitle)
    {
        string body = title;

        if (!string.IsNullOrEmpty(subtitle))
        {
            body += "\n<size=70%>" + subtitle + "</size>";
        }

        SpawnPopup(
            body,
            new Color(0.65f, 0.85f, 1f),
            FontStyles.Bold
        );
    }

    private GameObject SpawnPopup(
        string text,
        Color textColor,
        FontStyles fontStyle)
    {
        if (pvpHidden)
            return null;

        if (bonusPopupPrefab == null || bonusPopupContainer == null)
            return null;

        // Удаляем старую запись если переполнено
        if (bonusPopupCount >= maxBonusPopups &&
            bonusPopupContainer.childCount > 0)
        {
            Destroy(bonusPopupContainer.GetChild(0).gameObject);
            bonusPopupCount--;
        }

        // Создаём новую запись
        GameObject popup =
            Instantiate(bonusPopupPrefab, bonusPopupContainer);

        TMP_Text popupText =
            popup.GetComponentInChildren<TMP_Text>();

        if (popupText != null)
        {
            popupText.text = text;
            popupText.color = textColor;
            popupText.fontStyle |= fontStyle;
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

        return popup;
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
