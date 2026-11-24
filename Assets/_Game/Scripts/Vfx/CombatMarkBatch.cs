using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// All visible combat insignia share one opaque mesh, including when multiple statuses overlap.
[DefaultExecutionOrder(300)]
public sealed class CombatMarkBatch : MonoBehaviour
{
    private static CombatMarkBatch instance;
    private static readonly List<CombatMarkSource> sources = new List<CombatMarkSource>(96);
    private readonly List<Vector3> vertices = new List<Vector3>(4096);
    private readonly List<Color32> colors = new List<Color32>(4096);
    private readonly List<int> indices = new List<int>(4096);
    private readonly Plane[] planes = new Plane[6];
    private Mesh mesh;
    private Material material;
    private MeshRenderer surface;
    private Camera view;

    internal static void Register(CombatMarkSource source)
    {
        if (instance == null)
            new GameObject("Combat insignia").AddComponent<CombatMarkBatch>();
        sources.Add(source);
    }

    internal static void Unregister(CombatMarkSource source) => sources.Remove(source);

    private void Awake()
    {
        instance = this;
        DontDestroyOnLoad(gameObject);
        var shader = Resources.Load<Shader>("CombatInsignia");
        if (shader == null) { enabled = false; Debug.LogError("Missing CombatInsignia shader."); return; }
        material = new Material(shader) { name = "Combat insignia" };
        mesh = new Mesh { name = "Visible combat insignia" };
        mesh.MarkDynamic();
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        surface = gameObject.AddComponent<MeshRenderer>();
        surface.sharedMaterial = material;
        surface.shadowCastingMode = ShadowCastingMode.Off;
        surface.receiveShadows = false;
        surface.lightProbeUsage = LightProbeUsage.Off;
        surface.reflectionProbeUsage = ReflectionProbeUsage.Off;
        surface.enabled = false;
    }

    private void LateUpdate()
    {
        if (sources.Count == 0) { surface.enabled = false; return; }
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;
        if (view == null) { surface.enabled = false; return; }
        GeometryUtility.CalculateFrustumPlanes(view, planes);
        vertices.Clear();
        colors.Clear();
        for (int i = sources.Count - 1; i >= 0; i--)
        {
            var source = sources[i];
            Vector3 position = source.transform.position;
            if (GeometryUtility.TestPlanesAABB(planes, new Bounds(position, Vector3.one * 6f)))
                source.Draw(this);
        }
        surface.enabled = vertices.Count > 0;
        if (!surface.enabled) return;
        mesh.Clear();
        if (vertices.Count > 65535 && mesh.indexFormat != IndexFormat.UInt32)
            mesh.indexFormat = IndexFormat.UInt32;
        for (int i = indices.Count; i < vertices.Count; i++) indices.Add(i);
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices, 0, vertices.Count, 0, true);
    }

    internal void Stamp(CombatMarkShape shape, Matrix4x4 pose)
    {
        var art = CombatMarkArt.Get(shape);
        for (int i = 0; i < art.Points.Length; i++)
        {
            vertices.Add(pose.MultiplyPoint3x4(art.Points[i]));
            colors.Add(art.Colors[i]);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}

public abstract class CombatMarkSource : MonoBehaviour
{
    protected virtual void OnEnable() => CombatMarkBatch.Register(this);
    protected virtual void OnDisable() => CombatMarkBatch.Unregister(this);
    internal abstract void Draw(CombatMarkBatch batch);
}
