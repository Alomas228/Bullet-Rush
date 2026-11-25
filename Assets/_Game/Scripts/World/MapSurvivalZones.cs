using System.Collections.Generic;
using UnityEngine;

// Space oxygen pickups and forest recovery clearings share the run lifecycle.
public sealed class MapSurvivalZones : MonoBehaviour
{
    private const float Radius = 3.5f;
    private const int RecoveryCount = 3;
    private const float RecoveryLifetime = 18f;
    private const float RecoverySpacing = 12f;
    private const float RecoveryMoveDistance = 14f;
    private const float RecoveryHealPerSecond = .015f;
    private sealed class RecoverySite
    {
        public GameObject Model;
        public Transform Marker;
        public Vector3 Position;
        public bool HasPosition, Active;
        public float Remaining;
        public float RespawnIn;
    }
    private readonly List<RecoverySite> sites = new List<RecoverySite>(RecoveryCount);
    private ForestRecoveryArt recoveryArt;
    public const float DefaultOxygenSeconds = 10f;
    public const float SuitOxygenSeconds = 36f;
    private const int TankCount = 8;
    private const float CollectRadius = 1.4f;
    private const float TankSpacing = 8f;
    private const float TankRespawnDelay = 8f;
    private const float TankHoverHeight = .90f;
    private sealed class Tank
    {
        public GameObject Model;
        public Vector3 Position;
        public float RespawnIn;
        public float Phase;
        public bool Available;
    }
    private readonly List<Tank> tanks = new List<Tank>(TankCount);
    private OxygenTankArt tankArt;
    private bool activeRun, space;
    private float oxygen, capacity, healTime, arenaHalfSize, suffocationTime;
    public static MapSurvivalZones Instance { get; private set; }
    public bool OxygenActive => activeRun && space;
    public bool OxygenSuspended => RamLiftArena.InTransit || RamLiftArena.FloorY > 2f;
    public float OxygenSeconds => oxygen;
    public float OxygenCapacity => capacity;

    private void Awake() => Instance = this;

    public static bool HasOutfit(string id)
    {
        if (UpgradeManager.Instance == null) return false;
        foreach (ClothingData clothing in UpgradeManager.Instance.GetAvailableClothing())
            if (clothing != null && clothing.MapPerkId == id && EquipmentManager.IsOwned(clothing)) return true;
        return false;
    }

    public void BeginRun(MapMechanic mechanic, float halfSize)
    {
        EndRun();
        activeRun = true;
        space = mechanic == MapMechanic.LowGravity;
        capacity = HasOutfit("astronaut") ? SuitOxygenSeconds : DefaultOxygenSeconds;
        oxygen = capacity;
        healTime = 0f;
        suffocationTime = 0f;
        arenaHalfSize = halfSize;
        if (space)
        {
            CreateTanks();
            return;
        }
        CreateRecoverySites();
    }

    private void CreateRecoverySites()
    {
        Physics.SyncTransforms();
        if (recoveryArt == null) recoveryArt = new ForestRecoveryArt(Radius);
        for (int i = 0; i < RecoveryCount; i++)
        {
            var site = new RecoverySite();
            site.Model = recoveryArt.Create(transform, out site.Marker);
            site.Model.SetActive(false);
            sites.Add(site);
            if (TryPlaceRecoverySite(site)) site.Remaining = RecoveryLifetime - (RecoveryCount - 1 - i) * 3f;
            else site.RespawnIn = 1f;
        }
    }

    private bool TryPlaceRecoverySite(RecoverySite site)
    {
        float edge = arenaHalfSize - Radius - 2f;
        if (edge < 4f) return false;
        float centerClearance = Mathf.Min(9f, edge * .55f);
        Vector3 player = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : Vector3.zero;
        player.y = 0f;
        for (int attempt = 0; attempt < 96; attempt++)
        {
            Vector3 position = new Vector3(Random.Range(-edge, edge), 0f, Random.Range(-edge, edge));
            if (position.sqrMagnitude < centerClearance * centerClearance ||
                (position - player).sqrMagnitude < 10f * 10f) continue;
            if (site.HasPosition && (position - site.Position).sqrMagnitude < RecoveryMoveDistance * RecoveryMoveDistance) continue;
            bool blocked = false;
            foreach (RecoverySite other in sites)
            {
                if (other == site || !other.HasPosition) continue;
                // Keep away from all current and most recently expired clearings.
                if ((position - other.Position).sqrMagnitude < RecoverySpacing * RecoverySpacing) { blocked = true; break; }
            }
            if (blocked) continue;
            Collider[] hits = StructureQuery.OverlapSphere(position + Vector3.up * .35f, Radius + .25f, out int count);
            for (int j = 0; j < count; j++)
                if (StructureQuery.IsWorldStructure(hits[j])) { blocked = true; break; }
            if (blocked) continue;
            site.Position = position;
            site.HasPosition = true;
            site.Active = true;
            site.Remaining = RecoveryLifetime;
            site.Model.transform.position = position;
            site.Model.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            site.Marker.localScale = Vector3.one;
            site.Model.SetActive(true);
            return true;
        }
        return false;
    }

    private void CreateTanks()
    {
        Physics.SyncTransforms();
        if (tankArt == null) tankArt = new OxygenTankArt();
        float rotation = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < TankCount; i++)
        {
            var tank = new Tank { Model = tankArt.Create(transform), Phase = Random.Range(0f, 6.28f) };
            tank.Model.SetActive(false);
            tanks.Add(tank);
            float angle = rotation + i * Mathf.PI * 2f / TankCount;
            if (!TryPlaceTank(tank, angle, i == 0)) tank.RespawnIn = 1f;
        }
    }

    private bool TryPlaceTank(Tank tank, float preferredAngle, bool first)
    {
        float edge = Mathf.Max(5f, arenaHalfSize - 3f);
        float centerClearance = Mathf.Min(8f, edge * .5f);
        Vector3 player = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform.position : Vector3.zero;
        player.y = 0f;
        for (int attempt = 0; attempt < 64; attempt++)
        {
            float angle = preferredAngle + Random.Range(-.34f, .34f);
            float distance = first ? Random.Range(edge * .35f, edge * .50f) : Random.Range(edge * .55f, edge * 1.08f);
            Vector3 candidate = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            // Sector distribution on initial spawn; broaden the search if blocked.
            if (attempt >= 24)
                candidate = new Vector3(Random.Range(-edge, edge), 0f, Random.Range(-edge, edge));
            if (Mathf.Abs(candidate.x) > edge || Mathf.Abs(candidate.z) > edge ||
                candidate.sqrMagnitude < centerClearance * centerClearance) continue;
            if ((candidate - player).sqrMagnitude < 5f * 5f) continue;
            if (first && attempt < 24 && (candidate - player).sqrMagnitude > 16f * 16f) continue;
            bool blocked = false;
            foreach (Tank other in tanks)
            {
                if (other != tank && other.Available && (other.Position - candidate).sqrMagnitude < TankSpacing * TankSpacing)
                { blocked = true; break; }
            }
            if (blocked || (tank.Position != Vector3.zero && (tank.Position - candidate).sqrMagnitude < 25f)) continue;
            Collider[] hits = StructureQuery.OverlapSphere(candidate + Vector3.up * .65f, 1.6f, out int count);
            for (int j = 0; j < count; j++)
                if (StructureQuery.IsWorldStructure(hits[j])) { blocked = true; break; }
            if (blocked) continue;
            tank.Position = candidate;
            tank.Available = true;
            tank.Model.transform.position = candidate + Vector3.up * TankHoverHeight;
            tank.Model.SetActive(true);
            return true;
        }
        return false;
    }

    private bool Playing => activeRun && GameStateManager.Instance != null &&
        GameStateManager.Instance.CurrentState == GameState.Playing && !RamLiftArena.InTransit;

    private void Update()
    {
        if (!Playing || PlayerHealth.Instance == null || Time.timeScale <= 0f) return;
        PlayerHealth player = PlayerHealth.Instance;
        if (player.CurrentHealth <= 0f) return;
        if (space)
        {
            if (!OxygenSuspended) UpdateOxygen(player);
            return;
        }
        if (RamLiftArena.FloorY > 2f) { healTime = 0f; return; }
        UpdateRecoverySites(player);
    }

    private void UpdateRecoverySites(PlayerHealth player)
    {
        float dt = Time.deltaTime;
        Vector3 playerXZ = player.transform.position;
        playerXZ.y = 0f;
        bool inside = false;
        foreach (RecoverySite site in sites)
        {
            if (!site.Active)
            {
                site.RespawnIn -= dt;
                if (site.RespawnIn <= 0f && !TryPlaceRecoverySite(site)) site.RespawnIn = 1f;
                continue;
            }
            site.Remaining -= dt;
            if (site.Remaining <= 0f)
            {
                site.Active = false;
                site.Model.SetActive(false);
                site.RespawnIn = Random.Range(4f, 6f);
                continue;
            }
            float pulse = site.Remaining <= 3f ? .75f + .25f * Mathf.Sin(Time.time * 12f) : 1f;
            site.Marker.localScale = Vector3.one * pulse;
            site.Marker.localRotation = Quaternion.Euler(0f, Time.time * 28f, 0f);
            if ((playerXZ - site.Position).sqrMagnitude <= Radius * Radius) inside = true;
        }
        if (!inside || player.CurrentHealth >= player.MaxHealth) { healTime = 0f; return; }
        healTime += dt;
        if (healTime >= 1f)
        {
            healTime -= 1f;
            player.Heal(player.MaxHealth * RecoveryHealPerSecond);
        }
    }

    private void UpdateOxygen(PlayerHealth player)
    {
        float dt = Time.deltaTime;
        oxygen = Mathf.Max(0f, oxygen - dt);
        Vector3 playerXZ = player.transform.position;
        playerXZ.y = 0f;
        foreach (Tank tank in tanks)
        {
            if (!tank.Available)
            {
                tank.RespawnIn -= dt;
                if (tank.RespawnIn <= 0f && !TryPlaceTank(tank, Random.Range(0f, Mathf.PI * 2f), false))
                    tank.RespawnIn = 1f;
                continue;
            }
            float phase = Time.time + tank.Phase;
            tank.Model.transform.position = tank.Position + Vector3.up * (TankHoverHeight + Mathf.Sin(phase * 2.7f) * .12f);
            tank.Model.transform.rotation = Quaternion.Euler(12f, phase * 38f, 18f);
            // A full tank never consumes a pickup accidentally.
            if (oxygen >= capacity - .5f || (tank.Position - playerXZ).sqrMagnitude > CollectRadius * CollectRadius) continue;
            oxygen = capacity;
            suffocationTime = 0f;
            tank.Available = false;
            tank.RespawnIn = TankRespawnDelay;
            tank.Model.SetActive(false);
            if (AudioManager.Instance != null && AudioManager.Instance.SFXLibrary != null)
                AudioManager.Instance.PlaySFXVariationAt(AudioManager.Instance.SFXLibrary.PickupCollect,
                    player.transform.position, priority: SfxPriority.Medium);
        }

        if (oxygen > 0f) { suffocationTime = 0f; return; }
        suffocationTime += dt;
        if (suffocationTime >= 1f)
        {
            suffocationTime -= 1f;
            player.TakeSuffocationDamage(player.MaxHealth * .1f);
        }
    }

    public void EndRun()
    {
        activeRun = false;
        foreach (RecoverySite site in sites)
            if (site.Model != null) { site.Model.SetActive(false); Destroy(site.Model); }
        sites.Clear();
        foreach (Tank tank in tanks)
            if (tank.Model != null) { tank.Model.SetActive(false); Destroy(tank.Model); }
        tanks.Clear();
    }

    private void OnDestroy()
    {
        EndRun();
        if (Instance == this) Instance = null;
        recoveryArt?.Dispose();
        tankArt?.Dispose();
    }
}
