using UnityEngine;
using TMPro;

public class GameplayUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerHealth playerHealth;
    [SerializeField] private ScoreManager scoreManager;
    [SerializeField] private WaveManager waveManager;

    [Header("UI")]
    [SerializeField] private TMP_Text healthText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text waveText;

    private void Update()
    {
        if (playerHealth != null && healthText != null)
        {
            healthText.text =
                $"HP: {playerHealth.CurrentHealth:0} / {playerHealth.MaxHealth:0}";
        }

        if (scoreManager != null && scoreText != null)
        {
            scoreText.text = $"SCORE: {scoreManager.Score}";
        }

        if (waveManager != null && waveText != null)
        {
            waveText.text = $"WAVE: {waveManager.CurrentWave}";
        }
    }
}