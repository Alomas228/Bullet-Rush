using UnityEngine;
using TMPro;
using System.Collections;

public class WaveUI : MonoBehaviour, ILangRefreshable
{
    [Header("UI")]
    [SerializeField] private GameObject wavePanel;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text prepareText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text waveCompleteText;

    // Запоминаем, что именно показано, чтобы RefreshLang мог
    // перерисовать подпись, не трогая видимость панелей.
    private int lastWave = -1;
    private string lastSubtitle;
    private bool prepareShown;
    private bool completeShown;
    private bool showingEvent;
    private CanvasGroup motionGroup;
    private Coroutine motionRoutine;
    private TMP_Text motionLabel;
    private Vector2 restPosition;
    private Vector3 restScale;

    private void Start()
    {
        Hide();
    }

    public void ShowWave(
        int wave,
        string subtitle = null,
        float visibleSeconds = 0f)
    {
        if (wavePanel != null)
            wavePanel.SetActive(true);

        if (waveText != null)
            waveText.gameObject.SetActive(true);

        if (prepareText != null)
            prepareText.gameObject.SetActive(false);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        if (waveCompleteText != null)
            waveCompleteText.gameObject.SetActive(false);

        if (waveText != null)
        {
            lastWave = wave;
            lastSubtitle = subtitle;

            prepareShown = false;
            completeShown = false;

            waveText.fontSizeMax = 36f;
            waveText.text = BuildWaveLabel(wave, subtitle);
        }
        PlayMotion(waveText, visibleSeconds);
    }

    public void ShowPrepare(float visibleSeconds = 0f)
    {
        if (wavePanel != null)
            wavePanel.SetActive(true);

        if (waveText != null)
            waveText.gameObject.SetActive(false);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        if (waveCompleteText != null)
            waveCompleteText.gameObject.SetActive(false);

        if (prepareText != null)
        {
            prepareText.gameObject.SetActive(true);
            prepareText.text = Lang.Get("wave.prepare");
        }

        prepareShown = true;
        completeShown = false;
        PlayMotion(prepareText, visibleSeconds);
    }

    public void ShowCountdown(int number, float visibleSeconds = 0f)
    {
        if (wavePanel != null)
            wavePanel.SetActive(true);

        if (waveText != null)
            waveText.gameObject.SetActive(false);

        if (prepareText != null)
            prepareText.gameObject.SetActive(false);

        if (waveCompleteText != null)
            waveCompleteText.gameObject.SetActive(false);

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
            countdownText.text = number.ToString();
        }
        PlayMotion(countdownText, visibleSeconds, true);
    }

    public void ShowWaveComplete(float visibleSeconds = 0f)
    {
        if (wavePanel != null)
            wavePanel.SetActive(true);

        if (waveText != null)
            waveText.gameObject.SetActive(false);

        if (prepareText != null)
            prepareText.gameObject.SetActive(false);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        if (waveCompleteText != null)
        {
            waveCompleteText.gameObject.SetActive(true);
            waveCompleteText.text = Lang.Get("wave.complete");
        }

        prepareShown = false;
        completeShown = true;
        PlayMotion(waveCompleteText, visibleSeconds);
    }

    public void Hide()
    {
        CancelMotion();
        showingEvent = false;
        if (wavePanel != null)
            wavePanel.SetActive(false);

        if (waveText != null)
            waveText.gameObject.SetActive(false);

        if (prepareText != null)
            prepareText.gameObject.SetActive(false);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        if (waveCompleteText != null)
            waveCompleteText.gameObject.SetActive(false);

        lastWave = -1;
        lastSubtitle = null;
        prepareShown = false;
        completeShown = false;
    }

    /// <summary>
    /// Переводит подписи баннера на новом языке. Видимость панелей
    /// не меняется: перерисовывается только то, что сейчас показано.
    /// </summary>
    public void RefreshLang()
    {
        if (waveText != null &&
            waveText.gameObject.activeSelf &&
            lastWave > 0)
        {
            waveText.text = BuildWaveLabel(lastWave, lastSubtitle);
        }

        if (prepareShown &&
            prepareText != null &&
            prepareText.gameObject.activeSelf)
        {
            prepareText.text = Lang.Get("wave.prepare");
        }

        if (completeShown &&
            waveCompleteText != null &&
            waveCompleteText.gameObject.activeSelf)
        {
            waveCompleteText.text = Lang.Get("wave.complete");
        }
    }

    // Короткий баннер игрового события (амбуш, пачка, зона) —
    // переиспользует панель волны, которая во время боя скрыта.
    public void ShowEvent(string label)
    {
        bool continuingEvent = showingEvent && wavePanel != null && wavePanel.activeSelf &&
            waveText != null && waveText.gameObject.activeSelf;
        if (wavePanel != null)
            wavePanel.SetActive(true);

        if (prepareText != null)
            prepareText.gameObject.SetActive(false);

        if (countdownText != null)
            countdownText.gameObject.SetActive(false);

        if (waveCompleteText != null)
            waveCompleteText.gameObject.SetActive(false);

        if (waveText != null)
        {
            waveText.gameObject.SetActive(true);
            waveText.fontSizeMax = 26f;
            waveText.text = label;
        }

        // Подпись события приходит уже переведённой от вызывающего,
        // поэтому номер волны больше не актуален.
        lastWave = -1;
        lastSubtitle = null;
        prepareShown = false;
        completeShown = false;
        if (!continuingEvent)
            PlayMotion(waveText, 0f, false, true);
    }

    private static string BuildWaveLabel(int wave, string subtitle)
    {
        string title = Lang.Get("wave.number", wave);
        return string.IsNullOrEmpty(subtitle) ? title : title + "\n<size=68%>" + subtitle + "</size>";
    }

    private void PlayMotion(TMP_Text label, float visibleSeconds, bool countdown = false, bool isEvent = false)
    {
        CancelMotion();
        showingEvent = isEvent;
        if (wavePanel == null || label == null || !isActiveAndEnabled) return;

        // Keep persistent gameplay prompts a little higher than the wave intro.
        var panel = (RectTransform)wavePanel.transform;
        var min = panel.anchorMin;
        var max = panel.anchorMax;
        min.y = max.y = isEvent ? .74f : .64f;
        panel.anchorMin = min;
        panel.anchorMax = max;
        panel.anchoredPosition = Vector2.zero;

        if (motionGroup == null)
            motionGroup = wavePanel.GetComponent<CanvasGroup>();
        if (motionGroup == null)
            motionGroup = wavePanel.AddComponent<CanvasGroup>();
        if (motionGroup == null) return;
        motionGroup.interactable = false;
        motionGroup.blocksRaycasts = false;
        motionLabel = label;
        restPosition = label.rectTransform.anchoredPosition;
        restScale = label.rectTransform.localScale;
        motionRoutine = StartCoroutine(Animate(visibleSeconds, countdown));
    }

    private IEnumerator Animate(float visibleSeconds, bool countdown)
    {
        bool timed = visibleSeconds > 0f;
        float entry = timed ? Mathf.Min(.25f, visibleSeconds * .55f) : .25f;
        float fade = timed ? Mathf.Min(.16f, visibleSeconds * .28f) : 0f;
        float lifetime = timed ? visibleSeconds : entry;
        var rect = motionLabel.rectTransform;
        float elapsed = 0f;
        while (elapsed < lifetime)
        {
            float t = Mathf.Clamp01(elapsed / entry);
            float ease = 1f - Mathf.Pow(1f - t, 3f);
            float scale = Mathf.Lerp(countdown ? .70f : .90f, 1f, ease) +
                Mathf.Sin(t * Mathf.PI) * (countdown ? .18f : .055f);
            rect.localScale = restScale * scale;
            rect.anchoredPosition = restPosition + Vector2.down * (10f * (1f - ease));
            motionGroup.alpha = Mathf.Clamp01(t / .6f) *
                (timed ? Mathf.Clamp01((lifetime - elapsed) / fade) : 1f);
            yield return null;
            elapsed += timed ? Time.deltaTime : Time.unscaledDeltaTime;
        }

        rect.localScale = restScale;
        rect.anchoredPosition = restPosition;
        motionGroup.alpha = timed ? 0f : 1f;
        motionLabel = null;
        motionRoutine = null;
    }

    private void CancelMotion()
    {
        if (motionRoutine != null) StopCoroutine(motionRoutine);
        motionRoutine = null;
        if (motionLabel != null)
        {
            motionLabel.rectTransform.localScale = restScale;
            motionLabel.rectTransform.anchoredPosition = restPosition;
        }
        motionLabel = null;
        if (motionGroup != null) motionGroup.alpha = 1f;
    }

    private void OnDisable() => CancelMotion();
}
