using UnityEngine;

public class LootPickup : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 10f;
    [SerializeField] private float collectDistance = 0.7f;

    private LootType lootType;
    private float healPercent;
    private int coinValue;

    private Transform player;
    private bool collected;

    public static void SpawnHealth(
        Vector3 position,
        float healPercent)
    {
        CreatePickup(
            position,
            LootType.Health,
            healPercent,
            0
        );
    }

    public static void SpawnCoin(
        Vector3 position,
        int coins)
    {
        CreatePickup(
            position,
            LootType.Coin,
            0f,
            coins
        );
    }

    private static void CreatePickup(
        Vector3 position,
        LootType type,
        float healPercent,
        int coins)
    {
        position.y = 0.5f;

        LootConfig config = LootConfig.Instance;

        if (config == null)
        {
            Debug.LogWarning(
                "LootConfig.Instance is null. " +
                "Добавь объект с LootConfig в сцену и назначь префабы.",
                null
            );

            return;
        }

        GameObject prefab = null;

        switch (type)
        {
            case LootType.Health:
                prefab = config.HealthPrefab;
                break;

            case LootType.Coin:
                prefab = config.CoinPrefab;
                break;
        }

        if (prefab == null)
        {
            Debug.LogWarning(
                $"LootConfig: префаб для {type} не назначен.",
                config
            );

            return;
        }

        GameObject pickupObject =
            Instantiate(prefab, position, Quaternion.identity);

        pickupObject.name =
            $"Loot_{type}";

        LootPickup pickup =
            pickupObject.GetComponent<LootPickup>();

        if (pickup == null)
            pickup = pickupObject.AddComponent<LootPickup>();

        pickup.Initialize(
            type,
            healPercent,
            coins
        );
    }

    // =========================================================
    // INIT
    // =========================================================

    private void Initialize(
        LootType type,
        float heal,
        int coins)
    {
        lootType = type;
        healPercent = Mathf.Max(heal, 0f);
        coinValue = Mathf.Max(coins, 0);
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
            player = playerObject.transform;
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (collected)
            return;

        if (player == null)
        {
            GameObject playerObject =
                GameObject.FindGameObjectWithTag("Player");

            if (playerObject == null)
                return;

            player = playerObject.transform;
        }

        Vector3 diff =
            player.position - transform.position;

        if (diff.sqrMagnitude <
            collectDistance * collectDistance)
        {
            Collect();
        }
    }

    // =========================================================
    // COLLECT
    // =========================================================

    private void Collect()
    {
        if (collected)
            return;

        collected = true;

        switch (lootType)
        {
            case LootType.Health:

                PlayerHealth health =
                    player.GetComponent<PlayerHealth>();

                if (health != null && health.CurrentHealth < health.MaxHealth)
                {
                    health.Heal(
                        health.MaxHealth * healPercent
                    );
                }

                break;

            case LootType.Coin:

                if (XpManager.Instance != null)
                    XpManager.Instance.AddCoins(coinValue);

                break;
        }

        PlayPickupSound();

        Destroy(gameObject);
    }

    private void PlayPickupSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.PickupCollect);
    }
}