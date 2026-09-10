using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private PlayerStats playerStats;

    public int Score { get; private set; }

    private void Awake()
    {
        if (
            Instance != null &&
            Instance != this
        )
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (playerStats == null)
        {
            playerStats =
                FindAnyObjectByType<PlayerStats>();
        }
    }

    public void AddScore(int amount)
    {
        if (amount <= 0)
            return;

        float multiplier = 1f;

        if (playerStats != null)
        {
            multiplier =
                playerStats.ScoreMultiplier;
        }

        int finalAmount =
            Mathf.Max(
                Mathf.RoundToInt(
                    amount *
                    multiplier
                ),
                1
            );

        Score += finalAmount;

        Debug.Log(
            $"Score: +{finalAmount} " +
            $"(Base: {amount}, " +
            $"Multiplier: {multiplier:0.##}, " +
            $"Total: {Score})"
        );
    }

    public void ResetScore()
    {
        Score = 0;
    }
}