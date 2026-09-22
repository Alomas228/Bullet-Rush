using UnityEngine;

public class LootConfig : MonoBehaviour
{
    public static LootConfig Instance { get; private set; }

    [Header("Prefabs")]
    [Tooltip("Префаб здоровья (зелёный крест/кристалл).")]
    [SerializeField] private GameObject healthPrefab;
    [Tooltip("Префаб монеты.")]
    [SerializeField] private GameObject coinPrefab;

    public GameObject HealthPrefab => healthPrefab;
    public GameObject CoinPrefab => coinPrefab;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}