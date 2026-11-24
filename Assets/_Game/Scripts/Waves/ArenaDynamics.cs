using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small optional battlefield rewards and breakable scenery for normal waves.</summary>
public sealed class ArenaDynamics : MonoBehaviour
{
    private readonly List<GameObject> objects = new List<GameObject>(8);
    private GameObject barrelPrefab;
    private GameObject boostBasePrefab;
    private readonly GameObject[] boostPrefabs = new GameObject[3];
    private bool modelsLoaded;
    private WaveManager waves;
    private WorldStructureGenerator world;
    private Transform player;
    private GameObject pickup;
    private Transform pickupVisual;
    private float nextPickup;
    private float pickupExpires;
    private int wave;
    private int pickupsSpawned;
    private bool active;
    private PlayerStats.BattleBoost pickupKind;
    private System.Random layoutRandom;
    private Material propMaterial;
    private Texture2D propPalette;
    private Mesh coolantMesh, supplyMesh;

    public void StartWave(WaveManager manager, WorldStructureGenerator generator, int currentWave)
    {
        ClearWave();
        if (GameModeManager.IsPvp || PlayerHealth.Instance == null || generator == null) return;
        LoadModels();
        waves = manager;
        world = generator;
        player = PlayerHealth.Instance.transform;
        wave = currentWave;
        layoutRandom = new System.Random(unchecked(System.Environment.TickCount * 397 ^ wave));
        active = true;
        pickupsSpawned = 0;
        nextPickup = Time.time + 2.25f;
        int propCount = wave <= 2 ? layoutRandom.Next(2, 4) : layoutRandom.Next(3, 6);
        int firstKind = layoutRandom.Next(0, 3);
        for (int i = 0; i < propCount; i++)
        {
            if (!TryPlace(out Vector3 pos, 6f, 12f, i > 0)) continue;
            ArenaBreakable.Kind kind = (ArenaBreakable.Kind)(i < 3 ? (firstKind + i) % 3 : layoutRandom.Next(0, 3));
            SpawnBreakable(kind, pos);
        }
    }

    private void SpawnBreakable(ArenaBreakable.Kind kind, Vector3 position)
    {
        if (kind == ArenaBreakable.Kind.Explosive && barrelPrefab == null) kind = ArenaBreakable.Kind.Supply;
        if (kind != ArenaBreakable.Kind.Explosive && propMaterial == null) return;
        GameObject root = new GameObject("Arena " + kind);
        root.transform.position = position;
        root.transform.rotation = Quaternion.Euler(0f, layoutRandom.Next(0, 4) * 90f, 0f);
        int layer = LayerMask.NameToLayer("World");
        if (layer >= 0) root.layer = layer;
        root.AddComponent<WorldStructure>();
        BoxCollider collider = root.AddComponent<BoxCollider>();
        bool supply = kind == ArenaBreakable.Kind.Supply;
        collider.size = supply ? new Vector3(.94f, .8f, .78f) : new Vector3(.76f, 1.12f, .76f);
        collider.center = Vector3.up * (collider.size.y * .5f);
        root.AddComponent<ArenaBreakable>().Initialize(kind, layoutRandom.NextDouble() < .45, layoutRandom.Next(4, 9));

        if (kind == ArenaBreakable.Kind.Explosive)
            Instantiate(barrelPrefab, root.transform, false);
        else
        {
            root.AddComponent<MeshFilter>().sharedMesh = supply ? supplyMesh : coolantMesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = propMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }
        objects.Add(root);
        // Breakables may fade via occlusion, so keep their own renderer.
    }

    private void Update()
    {
        if (!active) return;
        if (GameStateManager.Instance == null || GameStateManager.Instance.CurrentState == GameState.Menu || GameModeManager.IsPvp)
        { ClearWave(); return; }
        if (GameStateManager.Instance.CurrentState != GameState.Playing || Time.timeScale <= 0f || player == null ||
            waves == null || !waves.IsWaveActive) return;

        if (pickup != null)
        {
            if (pickupVisual != null)
            {
                pickupVisual.localRotation = Quaternion.Euler(0, Time.time * 55f, 0);
                pickupVisual.localPosition = Vector3.up * (.69f + Mathf.Sin(Time.time * 4f) * .12f);
            }
            Vector3 delta = player.position - pickup.transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 1.15f * 1.15f)
            {
                player.GetComponent<PlayerStats>()?.GrantBattleBoost(pickupKind, 8f);
                GameplayJourney.Instance?.Notify(BoostMessage(pickupKind));
                AudioManager audio = AudioManager.Instance;
                if (audio != null && audio.SFXLibrary != null && audio.SFXLibrary.PickupCollect != null)
                    audio.PlaySFXVariationAt(audio.SFXLibrary.PickupCollect, pickup.transform.position, priority: SfxPriority.Low);
                RemovePickup();
            }
            else if (Time.time >= pickupExpires) RemovePickup();
        }
        if (pickup == null && pickupsSpawned < 2 && Time.time >= nextPickup && Enemy.AliveCount > 0)
        {
            nextPickup = Time.time + 2f;
            if (TryPlace(out Vector3 pos, 4f, 8f)) SpawnPickup(pos);
        }
    }

    private void SpawnPickup(Vector3 position)
    {
        pickupKind = (PlayerStats.BattleBoost)layoutRandom.Next(0, 3);
        GameObject visualPrefab = boostPrefabs[(int)pickupKind];
        if (visualPrefab == null) return;
        pickupsSpawned++;
        pickup = new GameObject("Battle boost " + pickupKind);
        pickup.transform.position = position;
        pickupVisual = Instantiate(visualPrefab, pickup.transform, false).transform;
        pickupVisual.localPosition = Vector3.up * .69f;
        if (boostBasePrefab != null) Instantiate(boostBasePrefab, pickup.transform, false);
        pickupExpires = Time.time + 12f;
        objects.Add(pickup);
        BattleMeshBatcher.RegisterTree(pickup);
        GameplayJourney.Instance?.Notify(GameplayJourney.T("УСИЛЕНИЕ НА АРЕНЕ", "BOOST ON THE ARENA", "ARENADA GUCLENDIRME"));
    }

    private void RemovePickup()
    {
        if (pickup != null)
        {
            pickup.SetActive(false);
            objects.Remove(pickup);
            Destroy(pickup);
        }
        pickup = null;
        pickupVisual = null;
        nextPickup = Time.time + 9f;
    }

    private bool TryPlace(out Vector3 position, float min, float max, bool scatter = false)
    {
        // A separate random stream keeps battlefield props varied without changing enemy RNG.
        float limit = Mathf.Clamp(world.ArenaRadius + 8f, 8f, 46f);
        for (int attempt = 0; attempt < 64; attempt++)
        {
            float angle = (float)layoutRandom.NextDouble() * Mathf.PI * 2f;
            float distance = Mathf.Lerp(min, max, (float)layoutRandom.NextDouble());
            Vector3 p = scatter ? world.ArenaCenter + new Vector3(
                (float)(layoutRandom.NextDouble() * 2 - 1) * limit, 0f,
                (float)(layoutRandom.NextDouble() * 2 - 1) * limit) :
                player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
            p.y = 0f;
            Vector3 toPlayer = p - player.position; toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < min * min) continue;
            Vector3 offset = p - world.ArenaCenter; offset.y = 0f;
            if (Mathf.Abs(offset.x) > limit || Mathf.Abs(offset.z) > limit) continue;
            bool clear = true;
            foreach (GameObject obj in objects)
                if (obj != null && (obj.transform.position - p).sqrMagnitude < 3f) { clear = false; break; }
            if (!clear) continue;
            Collider[] colliders = StructureQuery.OverlapSphere(p + Vector3.up * .65f, .95f, out int count);
            for (int i = 0; i < count; i++)
                if (StructureQuery.IsWorldStructure(colliders[i]) || ColliderKindQuery.GetEnemy(colliders[i]) != null)
                { clear = false; break; }
            if (!clear) continue;
            position = p;
            return true;
        }
        position = default;
        return false;
    }

    private void LoadModels()
    {
        if (modelsLoaded) return;
        modelsLoaded = true;
        barrelPrefab = Resources.Load<GameObject>("ArenaProps/Barrel");
        boostBasePrefab = Resources.Load<GameObject>("ArenaProps/BoostBase");
        boostPrefabs[(int)PlayerStats.BattleBoost.Damage] = Resources.Load<GameObject>("ArenaProps/BoostDamage");
        boostPrefabs[(int)PlayerStats.BattleBoost.FireRate] = Resources.Load<GameObject>("ArenaProps/BoostFireRate");
        boostPrefabs[(int)PlayerStats.BattleBoost.MoveSpeed] = Resources.Load<GameObject>("ArenaProps/BoostMoveSpeed");
        if (barrelPrefab == null || boostBasePrefab == null || System.Array.Exists(boostPrefabs, model => model == null))
            Debug.LogError("[ArenaDynamics] An arena prop prefab is missing.");
        BuildUtilityModels();
    }

    private void BuildUtilityModels()
    {
        propMaterial = LowPolyPaletteMesh.CreateMaterial("Arena utility props", 8, out propPalette);
        if (propMaterial == null) return;
        propPalette.SetPixels32(new[] { new Color32(96, 70, 46, 255), new Color32(172, 126, 69, 255),
            new Color32(73, 84, 83, 255), new Color32(231, 190, 82, 255), new Color32(59, 130, 155, 255),
            new Color32(38, 89, 112, 255), new Color32(163, 210, 216, 255), new Color32(220, 226, 214, 255) });
        propPalette.Apply(false, false);

        var water = new LowPolyPaletteMesh(8);
        water.Box(new Vector3(0, .10f, 0), new Vector3(.74f, .20f, .72f), 5, .94f);
        water.Box(new Vector3(0, .52f, 0), new Vector3(.66f, .80f, .64f), 4, .93f);
        water.Box(new Vector3(0, .32f, 0), new Vector3(.70f, .12f, .68f), 5);
        water.Box(new Vector3(0, .78f, 0), new Vector3(.69f, .12f, .67f), 5);
        water.Box(new Vector3(0, .96f, 0), new Vector3(.54f, .15f, .50f), 6, .85f);
        water.Box(new Vector3(.13f, 1.06f, 0), new Vector3(.20f, .12f, .20f), 7);
        water.Box(new Vector3(0, .53f, -.334f), new Vector3(.30f, .26f, .026f), 6);
        water.Box(new Vector3(0, .53f, .334f), new Vector3(.30f, .26f, .026f), 6);
        coolantMesh = water.Build("Coolant canister");

        var crate = new LowPolyPaletteMesh(8);
        crate.Box(new Vector3(0, .36f, 0), new Vector3(.88f, .72f, .72f), 1, .97f);
        crate.Box(new Vector3(0, .09f, 0), new Vector3(.94f, .16f, .78f), 0);
        crate.Box(new Vector3(0, .72f, 0), new Vector3(.94f, .14f, .78f), 0, .94f);
        foreach (float x in new[] { -.31f, .31f })
            crate.Box(new Vector3(x, .43f, 0), new Vector3(.12f, .65f, .77f), 2);
        foreach (float z in new[] { -.375f, .375f })
            crate.Box(new Vector3(0, .43f, z), new Vector3(.26f, .30f, .024f), 3, .9f);
        supplyMesh = crate.Build("Supply crate");
    }

    private static string BoostMessage(PlayerStats.BattleBoost boost)
    {
        switch (boost)
        {
            case PlayerStats.BattleBoost.Damage: return GameplayJourney.T("УРОН +35% · 8 С", "DAMAGE +35% · 8S", "HASAR +%35 · 8 SN");
            case PlayerStats.BattleBoost.FireRate: return GameplayJourney.T("СКОРОСТРЕЛЬНОСТЬ +40% · 8 С", "FIRE RATE +40% · 8S", "ATIS +%40 · 8 SN");
            default: return GameplayJourney.T("СКОРОСТЬ +25% · 8 С", "SPEED +25% · 8S", "HIZ +%25 · 8 SN");
        }
    }

    public void ClearWave()
    {
        active = false;
        foreach (GameObject obj in objects)
            if (obj != null) { obj.SetActive(false); Destroy(obj); }
        objects.Clear();
        pickup = null;
        pickupVisual = null;
    }

    private void OnDestroy()
    {
        ClearWave();
        if (coolantMesh != null) Destroy(coolantMesh);
        if (supplyMesh != null) Destroy(supplyMesh);
        if (propMaterial != null) Destroy(propMaterial);
        if (propPalette != null) Destroy(propPalette);
    }
}
