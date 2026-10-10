using System;
using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private BonusSettings bonusSettings;

    public int Score { get; private set; }
    public int Kills { get; private set; }

    public event Action<int, string> OnScoreAdded; // amount, bonusName

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (playerStats == null)
            playerStats = FindAnyObjectByType<PlayerStats>();
    }

    public void ResetRunStats()
    {
        Score = 0;
        Kills = 0;
    }

    /// <summary>
    /// Добавить очки за убийство.
    /// </summary>
    public void AddScore(int amount, string bonusName = "")
    {
        if (amount <= 0)
            return;

        float multiplier = 1f;
        if (playerStats != null)
            multiplier = playerStats.ScoreMultiplier;

        // Тема забега может повышать счёт как компенсацию за риск.
        multiplier *= RunModifierManager.ScoreScale;

        int finalAmount = Mathf.Max(Mathf.RoundToInt(amount * multiplier), 1);

        Score += finalAmount;
        OnScoreAdded?.Invoke(finalAmount, bonusName);

        Debug.Log($"Score: +{finalAmount}" + (string.IsNullOrEmpty(bonusName) ? "" : $" [{bonusName}]"));
    }

    /// <summary>
    /// Добавить бонусные очки.
    /// </summary>
    public void AddBonus(int amount, string bonusName)
    {
        if (amount <= 0)
            return;

        int finalAmount =
            Mathf.Max(
                Mathf.RoundToInt(amount * RunModifierManager.ScoreScale),
                1
            );

        Score += finalAmount;
        OnScoreAdded?.Invoke(finalAmount, bonusName);

        Debug.Log($"BONUS +{finalAmount} [{bonusName}]");
    }

    public void IncrementKills()
    {
        Kills++;
    }

    public void ResetScore()
    {
        Score = 0;
        Kills = 0;
    }
}
