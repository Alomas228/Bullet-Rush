using System.Collections;
using UnityEngine;

public sealed class RamLiftArena : MonoBehaviour
{
    private const float HalfWidth = 9f;
    private const float HalfDepth = 8f;
    private const float LiftHeight = 16f;
    private readonly BoxCollider[] posts = new BoxCollider[2];
    private Transform rider;
    private Rigidbody riderBody;
    private Enemy opponent;
    private Vector3 landing;
    private Vector3 baseCenter;
    private Material metal;
    private float elevation;

    public static RamLiftArena Active { get; private set; }
    public static bool InTransit => Active != null && Active.Moving;
    public static float FloorY => Active != null ? Active.transform.position.y : 0f;
    public bool Moving { get; private set; }
    public Vector3 Landing => landing;
    public Vector3 BossStart => transform.position + new Vector3(0f, 0.5f, 6.5f);

    public static RamLiftArena Create(Transform player)
    {
        var arena = new GameObject("RamLiftArena").AddComponent<RamLiftArena>();
        arena.rider = player;
        arena.riderBody = player.GetComponent<Rigidbody>();
        arena.landing = player.position;
        arena.baseCenter = new Vector3(player.position.x, 0f, player.position.z);
        arena.transform.position = arena.baseCenter;
        arena.Moving = true;
        arena.BuildDeck();
        Active = arena;
        return arena;
    }

    private void BuildDeck()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null) metal = new Material(shader);
        Color steel = new Color(0.37f, 0.44f, 0.49f);
        Color rail = new Color(0.18f, 0.25f, 0.29f);
        AddBlock("Deck", new Vector3(0f, -0.45f, 0f), new Vector3(18.5f, 0.9f, 16.5f), steel, false);
        for (int side = -1; side <= 1; side += 2)
        {
            AddBlock("SideRail", new Vector3(side * HalfWidth, 0.5f, 0f),
                new Vector3(0.4f, 1f, 16.4f), rail, true);
            AddBlock("EndRail", new Vector3(0f, 0.5f, side * HalfDepth),
                new Vector3(18.4f, 1f, 0.4f), rail, true);
            posts[(side + 1) / 2] = AddBlock("ImpactPost", new Vector3(side * 4.5f, 1.1f, 0f),
                new Vector3(1.5f, 2.2f, 1.5f), new Color(1f, 0.7f, 0.14f), true);
        }
    }

    private BoxCollider AddBlock(string label, Vector3 position, Vector3 size, Color color, bool obstacle)
    {
        GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
        block.name = label;
        block.transform.SetParent(transform, false);
        block.transform.localPosition = position;
        block.transform.localScale = size;
        if (obstacle) block.AddComponent<WorldStructure>();
        var renderer = block.GetComponent<Renderer>();
        if (metal != null) renderer.sharedMaterial = metal;
        var properties = new MaterialPropertyBlock();
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
        return block.GetComponent<BoxCollider>();
    }

    public IEnumerator Raise() => Travel(true);
    public IEnumerator Lower()
    {
        foreach (var warning in GetComponentsInChildren<BossAttackZone>())
            warning.gameObject.SetActive(false);
        foreach (var line in GetComponentsInChildren<BossLineTelegraph>())
            line.gameObject.SetActive(false);
        return Travel(false);
    }
    public void SetOpponent(Enemy ram) => opponent = ram;

    private IEnumerator Travel(bool up)
    {
        Moving = true;
        Vector3 riderOffset = rider.position - transform.position;
        float duration = up ? 2f : 1.5f;
        for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            elevation = up ? t : 1f - t;
            transform.position = baseCenter + Vector3.up * (LiftHeight * elevation);
            // Return to the exact clear footprint where the player boarded.
            Vector3 offset = up ? riderOffset : Vector3.Lerp(riderOffset, landing - baseCenter, t);
            SetRiderPosition(transform.position + offset);
            yield return null;
        }
        elevation = up ? 1f : 0f;
        transform.position = baseCenter + Vector3.up * (LiftHeight * elevation);
        SetRiderPosition(landing + Vector3.up * (LiftHeight * elevation));
        Physics.SyncTransforms();
        Moving = !up;
    }

    private void SetRiderPosition(Vector3 position)
    {
        if (riderBody != null) riderBody.position = position;
        if (rider != null) rider.position = position;
    }

    public Vector3 CameraPosition(Vector3 normal, Vector3 offset, float aspect)
    {
        float distance = Mathf.Max(1.65f, 1.65f / Mathf.Max(aspect, 0.3f));
        return Vector3.Lerp(normal, transform.position + offset * distance, elevation);
    }

    public Vector3 ClampFighter(Vector3 position, float radius)
    {
        Vector3 local = position - transform.position;
        local.x = Mathf.Clamp(local.x, -HalfWidth + radius, HalfWidth - radius);
        local.z = Mathf.Clamp(local.z, -HalfDepth + radius, HalfDepth - radius);
        return transform.position + local;
    }

    public Vector3 RevivePosition()
    {
        float side = opponent != null && opponent.transform.position.z < transform.position.z ? 1f : -1f;
        return transform.position + new Vector3(0f, landing.y - baseCenter.y, side * 5.5f);
    }

    // Sweep the boss footprint, so a fast charge cannot skip a narrow post.
    public float ChargeDistance(Vector3 start, Vector3 direction, float range)
    {
        Vector3 local = start - transform.position;
        // Stop at the first rail along the ray, without changing the locked heading.
        float x = DistanceToRail(local.x, direction.x, HalfWidth - 1.25f);
        float z = DistanceToRail(local.z, direction.z, HalfDepth - 1.25f);
        float distance = Mathf.Clamp(Mathf.Min(x, z), 0f, range);
        FirstPost(start, direction, ref distance);
        return distance;
    }

    private static float DistanceToRail(float start, float direction, float limit)
    {
        if (Mathf.Abs(direction) < 0.0001f) return float.PositiveInfinity;
        return ((direction > 0f ? limit : -limit) - start) / direction;
    }

    public BoxCollider FirstPost(Vector3 start, Vector3 direction, ref float distance)
    {
        var ray = new Ray(start, direction);
        BoxCollider nearest = null;
        foreach (var post in posts)
        {
            if (post == null || !post.gameObject.activeInHierarchy) continue;
            Bounds bounds = post.bounds;
            bounds.Expand(new Vector3(2f, 0f, 2f));
            if (bounds.Contains(start))
            {
                if (Vector3.Dot(bounds.center - start, direction) > 0f)
                {
                    distance = 0f;
                    return post;
                }
                continue;
            }
            if (bounds.IntersectRay(ray, out float hit) && hit >= 0f && hit <= distance)
            {
                distance = hit;
                nearest = post;
            }
        }
        return nearest;
    }

    public void HitPost(BoxCollider post, bool breaks)
    {
        VfxFactory.TrySpawnExplosion(post.bounds.center, 1.8f, new Color(1f, 0.75f, 0.2f));
        if (breaks) post.gameObject.SetActive(false);
    }

    public void Close()
    {
        if (Active != this) return;
        Active = null;
        if (opponent != null) opponent.Retreat();
        SetRiderPosition(landing);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Active == this) Active = null;
        if (metal != null) Destroy(metal);
    }
}
