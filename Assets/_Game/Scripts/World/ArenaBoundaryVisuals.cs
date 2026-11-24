using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Opaque, collision-safe arena perimeter with one palette mesh per side.</summary>
public sealed class ArenaBoundaryVisuals : MonoBehaviour
{
    private Material material;
    private Texture2D palette;
    private readonly List<Mesh> meshes = new List<Mesh>(4);
    private bool built;
    private MapMechanic previousTheme;
    private bool hasTheme;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnvironmentController environment = FindAnyObjectByType<EnvironmentController>();
        ApplyTheme(environment != null ? environment.CurrentMap : null);
    }

    public static void ApplyTheme(GameMap map)
    {
        GameObject walls = GameObject.Find("Walls");
        if (walls == null) return;
        ArenaBoundaryVisuals boundary = walls.GetComponent<ArenaBoundaryVisuals>();
        if (boundary == null) boundary = walls.AddComponent<ArenaBoundaryVisuals>();
        boundary.Build();
        boundary.UpdatePalette(map != null ? map.mechanic : MapMechanic.None);
    }

    private void Build()
    {
        if (built) return;
        material = LowPolyPaletteMesh.CreateMaterial("Arena wall palette", 8, out palette);
        if (material == null) return;
        built = true;
        int count = transform.childCount;
        int worldLayer = LayerMask.NameToLayer("World");
        for (int i = 0; i < count; i++)
        {
            Transform wall = transform.GetChild(i);
            if (!wall.name.StartsWith("Wall_", System.StringComparison.Ordinal)) continue;
            BoxCollider box = wall.GetComponent<BoxCollider>();
            if (box == null) continue;
            Vector3 size = Vector3.Scale(box.size, wall.localScale);
            bool alongX = Mathf.Abs(size.x) >= Mathf.Abs(size.z);
            float length = alongX ? Mathf.Abs(size.x) : Mathf.Abs(size.z);
            Vector3 center = wall.localPosition + Vector3.Scale(box.center, wall.localScale);
            float groundY = center.y - Mathf.Abs(size.y) * .5f;

            // Preserve the existing physical boundary but raise it above every fighter.
            float localHeight = 3.2f / Mathf.Max(Mathf.Abs(wall.localScale.y), .01f);
            Vector3 oldSize = box.size;
            Vector3 colliderCenter = box.center;
            colliderCenter.y += (localHeight - oldSize.y) * .5f;
            oldSize.y = localHeight;
            if (alongX) oldSize.z = Mathf.Max(oldSize.z, 1.45f / Mathf.Max(Mathf.Abs(wall.localScale.z), .01f));
            else oldSize.x = Mathf.Max(oldSize.x, 1.45f / Mathf.Max(Mathf.Abs(wall.localScale.x), .01f));
            box.center = colliderCenter;
            box.size = oldSize;
            box.isTrigger = false;
            box.enabled = true;
            if (worldLayer >= 0) wall.gameObject.layer = worldLayer;
            WorldStructure structure = wall.GetComponent<WorldStructure>();
            if (structure == null) structure = wall.gameObject.AddComponent<WorldStructure>();
            structure.IsArenaBoundary = true;
            Renderer oldRenderer = wall.GetComponent<Renderer>();
            if (oldRenderer != null) oldRenderer.enabled = false;

            BuildSide(wall.name, center, groundY, length, alongX);
        }
    }

    private void BuildSide(string name, Vector3 center, float groundY, float length, bool alongX)
    {
        var mesh = new LowPolyPaletteMesh(8);
        void Part(float along, float baseY, float width, float depth, float height, int color, float taper = 1f)
        {
            Vector3 pos = center + (alongX ? Vector3.right : Vector3.forward) * along;
            pos.y = groundY + baseY + height * .5f;
            Vector3 dimensions = alongX ? new Vector3(width, height, depth) : new Vector3(depth, height, width);
            mesh.Box(pos, dimensions, color, taper);
        }

        // Continuous foundation and wall, recessed stone/metal panels, pillars and bevel caps.
        Part(0, 0f, length + .7f, 1.45f, .30f, 0);
        Part(0, .28f, length + .25f, 1.0f, 1.10f, 1);
        Part(0, 1.37f, length + .45f, 1.31f, .20f, 3, .90f);
        Part(0, 1.23f, length, 1.06f, .09f, 4);

        int modules = Mathf.Max(1, Mathf.RoundToInt(length / 5f));
        float spacing = length / modules;
        for (int i = 0; i < modules; i++)
        {
            float offset = -length * .5f + (i + .5f) * spacing;
            Part(offset, .39f, spacing - .82f, 1.10f, .72f, i % 2 == 0 ? 2 : 1, .96f);
        }
        for (int i = 0; i <= modules; i++)
        {
            float offset = -length * .5f + i * spacing;
            bool corner = i == 0 || i == modules;
            // The perpendicular wall already supplies the shared corner post.
            if (!alongX && corner) continue;
            float postWidth = corner ? 1.35f : .82f;
            Part(offset, .10f, postWidth, 1.48f, 1.54f, 0);
            Part(offset, .32f, postWidth * .84f, 1.54f, 1.12f, 2, .90f);
            Part(offset, 1.64f, postWidth + .13f, 1.64f, .20f, 3, .82f);
            if (i % 3 == 0 || corner)
                Part(offset, 1.82f, postWidth * .65f, .85f, .11f, 4);
        }

        Mesh combined = mesh.Build(name + " low poly perimeter");
        meshes.Add(combined);
        GameObject visual = new GameObject(name + " visual");
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = combined;
        MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = true;
    }

    private void UpdatePalette(MapMechanic mechanic)
    {
        if (palette == null || (hasTheme && previousTheme == mechanic)) return;
        previousTheme = mechanic;
        hasTheme = true;
        Color baseColor, accent;
        switch (mechanic)
        {
            case MapMechanic.Urban: baseColor = new Color32(102, 116, 120, 255); accent = new Color32(207, 164, 81, 255); break;
            case MapMechanic.Highland: baseColor = new Color32(111, 119, 126, 255); accent = new Color32(189, 192, 174, 255); break;
            case MapMechanic.OpenField: baseColor = new Color32(156, 139, 106, 255); accent = new Color32(219, 197, 149, 255); break;
            case MapMechanic.Sandstorm: baseColor = new Color32(177, 142, 99, 255); accent = new Color32(233, 200, 145, 255); break;
            case MapMechanic.LowGravity: baseColor = new Color32(101, 113, 139, 255); accent = new Color32(128, 189, 209, 255); break;
            default: baseColor = new Color32(114, 126, 95, 255); accent = new Color32(198, 174, 115, 255); break;
        }
        palette.SetPixels(new[] { baseColor * .67f, baseColor, Color.Lerp(baseColor, Color.white, .14f),
            Color.Lerp(baseColor, Color.white, .28f), accent, baseColor, baseColor, baseColor });
        palette.Apply(false, false);
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in meshes) if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
        if (palette != null) Destroy(palette);
    }
}

/// <summary>Produces one flat-shaded mesh using a tiny opaque colour atlas.</summary>
internal sealed class LowPolyPaletteMesh
{
    private readonly List<Vector3> vertices = new List<Vector3>();
    private readonly List<Vector2> uv = new List<Vector2>();
    private readonly List<int> triangles = new List<int>();
    private readonly int paletteSize;

    public LowPolyPaletteMesh(int paletteSize) => this.paletteSize = paletteSize;

    public static Material CreateMaterial(string name, int paletteSize, out Texture2D texture)
    {
        texture = null;
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        if (shader == null) return null;
        texture = new Texture2D(paletteSize, 1, TextureFormat.RGBA32, false)
        { name = name + " atlas", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        Material material = new Material(shader) { name = name, enableInstancing = true, color = Color.white };
        if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", texture);
        else material.mainTexture = texture;
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .08f);
        return material;
    }

    public void Box(Vector3 center, Vector3 size, int color, float topScale = 1f)
    {
        Vector3 h = size * .5f;
        Vector3 b0 = center + new Vector3(-h.x, -h.y, -h.z);
        Vector3 b1 = center + new Vector3(h.x, -h.y, -h.z);
        Vector3 b2 = center + new Vector3(h.x, -h.y, h.z);
        Vector3 b3 = center + new Vector3(-h.x, -h.y, h.z);
        float x = h.x * topScale, z = h.z * topScale;
        Vector3 t0 = center + new Vector3(-x, h.y, -z);
        Vector3 t1 = center + new Vector3(x, h.y, -z);
        Vector3 t2 = center + new Vector3(x, h.y, z);
        Vector3 t3 = center + new Vector3(-x, h.y, z);
        Face(b0, b1, b2, b3, color);
        Face(t0, t3, t2, t1, color);
        Face(b0, t0, t1, b1, color);
        Face(b3, b2, t2, t3, color);
        Face(b0, b3, t3, t0, color);
        Face(b1, t1, t2, b2, color);
    }

    private void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, int color)
    {
        int i = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        Vector2 sample = new Vector2((Mathf.Clamp(color, 0, paletteSize - 1) + .5f) / paletteSize, .5f);
        uv.Add(sample); uv.Add(sample); uv.Add(sample); uv.Add(sample);
        triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
        triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 3);
    }

    public Mesh Build(string name)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
