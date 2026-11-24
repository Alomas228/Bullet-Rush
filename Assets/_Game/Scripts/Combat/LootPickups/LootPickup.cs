using UnityEngine;

public class LootPickup : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float lifetime = 75f;
    [SerializeField] private float collectDistance = 0.7f;
    [SerializeField] private float coinMagnetRadius = 3.6f;
    [SerializeField] private float healthMagnetRadius = 2.3f;

    private LootType lootType;
    private float healPercent;
    private int coinValue;

    private Transform player;
    private PlayerHealth health;
    private bool collected;
    private bool attracted;
    private float age, magnetSpeed, remaining;
    private Vector3 restPosition;
    private Vector3 baseScale;
    private RamLiftArena lootLift;
    private bool onLift;
    private static GameObject coinModel, healthModel;

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
        position.y = RamLiftArena.FloorY + .5f;
        // Separate simultaneous health and coin drops without changing their value.
        position.x += type == LootType.Coin ? .24f : -.24f;

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
                if (healthModel == null) healthModel = Resources.Load<GameObject>("ArenaProps/LootHealth");
                prefab = healthModel != null ? healthModel : config.HealthPrefab;
                break;

            case LootType.Coin:
                if (coinModel == null) coinModel = Resources.Load<GameObject>("ArenaProps/LootCoin");
                prefab = coinModel != null ? coinModel : config.CoinPrefab;
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

        PlayPickupSpawnSound(position);
    }

    private static void PlayPickupSpawnSound(Vector3 position)
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFXAt(
                sfx.PickupSpawn,
                position,
                priority: SfxPriority.Low
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
        // Keep loot for a substantial part of the run rather than deleting it
        // in ten seconds; old serialized prefab lifetimes cannot shorten it.
        remaining = Mathf.Max(lifetime, type == LootType.Coin ? 90f : 75f);
        restPosition = transform.position;
        baseScale = transform.localScale;
        lootLift = RamLiftArena.Active;
        onLift = lootLift != null;
        BattleMeshBatcher.RegisterTree(gameObject);
    }

    private void Start()
    {
        if (remaining <= 0f)
        {
            remaining = Mathf.Max(75f, lifetime);
            restPosition = transform.position;
            baseScale = transform.localScale;
        }
        ResolvePlayer();
    }

    // =========================================================
    // UPDATE
    // =========================================================

    private void Update()
    {
        if (collected || Time.timeScale <= 0f || GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Playing)
            return;

        if (onLift)
        {
            float groundY = lootLift != null ? lootLift.transform.position.y + .5f : .5f;
            Vector3 shift = Vector3.up * (groundY - restPosition.y);
            restPosition += shift;
            transform.position += shift;
            if (lootLift == null) onLift = false;
        }
        if (RamLiftArena.InTransit) return;

        if (player == null || !player.gameObject.activeInHierarchy) ResolvePlayer();
        if (player == null || !player.gameObject.activeInHierarchy) return;
        float dt = Time.deltaTime;
        age += dt;
        if (!attracted)
        {
            remaining -= dt;
            if (remaining <= 0f) { Destroy(gameObject); return; }
        }

        Vector3 diff = player.position - transform.position;
        diff.y = 0f;
        bool sameFloor = Mathf.Abs(player.position.y - restPosition.y) < 1.8f;
        bool canCollect = sameFloor && (lootType != LootType.Health || (health != null && health.CurrentHealth < health.MaxHealth - .01f));
        float radius = lootType == LootType.Coin ? coinMagnetRadius : healthMagnetRadius;
        if (canCollect && diff.sqrMagnitude <= radius * radius) attracted = true;
        if (!canCollect && attracted)
        {
            attracted = false;
            restPosition = new Vector3(transform.position.x, restPosition.y, transform.position.z);
            magnetSpeed = 0f;
        }

        float spawnPop = Mathf.Clamp01(age / .25f);
        if (attracted)
        {
            magnetSpeed = Mathf.MoveTowards(magnetSpeed, 19f, dt * 32f);
            Vector3 target = player.position + Vector3.up * .12f;
            transform.position = Vector3.MoveTowards(transform.position, target, Mathf.Max(3f, magnetSpeed) * dt);
            transform.localScale = baseScale * Mathf.Lerp(.86f, 1f, Mathf.Clamp01(diff.magnitude / 1.4f));
            if ((transform.position - target).sqrMagnitude <= collectDistance * collectDistance) Collect();
        }
        else
        {
            float pop = Mathf.Sin(spawnPop * Mathf.PI) * .20f;
            transform.position = restPosition + Vector3.up * (pop + Mathf.Sin(age * 3.8f) * .07f);
            transform.localScale = baseScale * Mathf.Lerp(.65f, 1f, spawnPop);
        }
        transform.rotation = Quaternion.Euler(lootType == LootType.Coin ? 23f : 0f, age * (attracted ? 330f : 65f), 0f);
    }

    private void ResolvePlayer()
    {
        health = PlayerHealth.Instance;
        player = health != null ? health.transform : null;
    }

    public static void ClearRunLoot()
    {
        foreach (LootPickup loot in FindObjectsByType<LootPickup>())
        {
            if (loot == null) continue;
            loot.gameObject.SetActive(false);
            Destroy(loot.gameObject);
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
            AudioManager.Instance.PlaySFXVariationAt(
                sfx.PickupCollect,
                transform.position,
                priority: SfxPriority.Medium
            );
    }
}
