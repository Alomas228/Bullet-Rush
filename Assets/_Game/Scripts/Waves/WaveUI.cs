using UnityEngine;
using TMPro;

public class WaveUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject wavePanel;
    [SerializeField] private TMP_Text waveText;
    [SerializeField] private TMP_Text prepareText;
    [SerializeField] private TMP_Text countdownText;

    private void Start()
    {
        Hide();
    }

    public void ShowWave(int wave)
    {
        if (wavePanel != null)
            wavePanel.SetActive(true);

        waveText.gameObject.SetActive(true);
        prepareText.gameObject.SetActive(false);
        countdownText.gameObject.SetActive(false);

        waveText.text = $"WAVE {wave}";
    }

    public void ShowPrepare()
    {
        if (wavePanel != null)
            wavePanel.SetActive(true);

        waveText.gameObject.SetActive(false);
        prepareText.gameObject.SetActive(true);
        countdownText.gameObject.SetActive(false);

        prepareText.text = "GET READY!";
    }

    public void ShowCountdown(int number)
    {
        if (wavePanel != null)
            wavePanel.SetActive(true);

        waveText.gameObject.SetActive(false);
        prepareText.gameObject.SetActive(false);
        countdownText.gameObject.SetActive(true);

        countdownText.text = number.ToString();
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
    }
}