using UnityEngine;
using TMPro;

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

    private void Start()
    {
        Hide();
    }

    public void ShowWave(
        int wave,
        string subtitle = null)
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

            waveText.text =
                string.IsNullOrEmpty(subtitle)
                    ? Lang.Get("wave.number", wave)
                    : Lang.Get("wave.number_subtitle", wave, subtitle);
        }
    }

    public void ShowPrepare()
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
    }

    public void ShowCountdown(int number)
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
    }

    public void ShowWaveComplete()
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
    }

    public void Hide()
    {
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
            waveText.text =
                string.IsNullOrEmpty(lastSubtitle)
                    ? Lang.Get("wave.number", lastWave)
                    : Lang.Get(
                        "wave.number_subtitle",
                        lastWave,
                        lastSubtitle
                    );
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
            waveText.text = label;
        }

        // Подпись события приходит уже переведённой от вызывающего,
        // поэтому номер волны больше не актуален.
        lastWave = -1;
        lastSubtitle = null;
    }
}
