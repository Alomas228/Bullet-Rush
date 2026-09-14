using UnityEngine;
using TMPro;

public class WaveUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject wavePanel;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text prepareText;
    [SerializeField] private TMP_Text countdownText;
    [SerializeField] private TMP_Text waveCompleteText;

    private void Start()
    {
        Hide();
    }

    public void ShowWave(int wave)
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
            waveText.text = $"WAVE {wave}";
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
            prepareText.text = "GET READY!";
        }
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
            waveCompleteText.text = "WAVE COMPLETE";
        }
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
    }
}
