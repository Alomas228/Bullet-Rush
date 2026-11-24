using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// Shared, faceted oxygen cylinder meshes. No colliders, lights or particle systems.
public sealed class OxygenTankArt
{
    private readonly Mesh[] meshes = new Mesh[3];
    private readonly Material[] materials = new Material[3];

    public OxygenTankArt()
    {
        var shell = new Geometry();
        shell.Lathe(new Vector2(0f,-.47f), new Vector2(.23f,-.47f), new Vector2(.31f,-.37f),
            new Vector2(.31f,.24f), new Vector2(.29f,.34f), new Vector2(.20f,.44f), new Vector2(.11f,.46f), new Vector2(0f,.46f));

        var rubber = new Geometry();
        rubber.Lathe(new Vector2(0f,-.53f), new Vector2(.28f,-.53f), new Vector2(.33f,-.46f),
            new Vector2(.33f,-.36f), new Vector2(.26f,-.32f), new Vector2(0f,-.32f));
        rubber.Lathe(new Vector2(0f,.42f), new Vector2(.13f,.42f), new Vector2(.13f,.56f), new Vector2(0f,.56f));
        // Protective valve handle, open at the front for a recognizable silhouette.
        rubber.Box(new Vector3(-.17f,.55f,0f), new Vector3(.10f,.25f,.14f));
        rubber.Box(new Vector3(.17f,.55f,0f), new Vector3(.10f,.25f,.14f));
        rubber.Box(new Vector3(0f,.69f,0f), new Vector3(.44f,.09f,.14f));
        rubber.Box(new Vector3(0f,.23f,.30f), new Vector3(.26f,.23f,.12f));

        var accent = new Geometry();
        accent.Lathe(new Vector2(.313f,-.12f), new Vector2(.325f,-.09f), new Vector2(.325f,.09f), new Vector2(.313f,.12f));
        accent.Box(new Vector3(0f,.56f,0f), new Vector3(.19f,.065f,.20f));
        accent.Box(new Vector3(0f,.23f,.37f), new Vector3(.18f,.15f,.035f));
        meshes[0] = shell.ToMesh("Oxygen tank - shell");
        meshes[1] = rubber.ToMesh("Oxygen tank - valve and bumper");
        meshes[2] = accent.ToMesh("Oxygen tank - identification band");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Unlit");
        materials[0] = MakeMaterial(shader, "Oxygen ceramic", new Color32(225,237,239,255));
        materials[1] = MakeMaterial(shader, "Oxygen charcoal", new Color32(39,60,72,255));
        materials[2] = MakeMaterial(shader, "Oxygen blue", new Color32(45,173,203,255));
    }

    private static Material MakeMaterial(Shader shader, string name, Color color)
    {
        var material = new Material(shader) { name = name, color = color, enableInstancing = true };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .22f);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", .10f);
        return material;
    }

    public GameObject Create(Transform parent)
    {
        var root = new GameObject("Oxygen canister");
        root.transform.SetParent(parent, false);
        root.transform.localScale = Vector3.one * 1.25f;
        for (int i = 0; i < meshes.Length; i++)
        {
            var part = new GameObject(meshes[i].name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(root.transform, false);
            part.GetComponent<MeshFilter>().sharedMesh = meshes[i];
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[i];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        BattleMeshBatcher.RegisterTree(root);
        return root;
    }

    public void Dispose()
    {
        foreach (Mesh mesh in meshes) Object.Destroy(mesh);
        foreach (Material material in materials) Object.Destroy(material);
    }

    private sealed class Geometry
    {
        private const int Sides = 10;
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> indices = new List<int>();

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }

        private void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
        }

        private static Vector3 Point(Vector2 profile, int i)
        {
            float a = i * Mathf.PI * 2f / Sides;
            return new Vector3(Mathf.Cos(a) * profile.x, profile.y, Mathf.Sin(a) * profile.x);
        }

        public void Lathe(params Vector2[] profile)
        {
            for (int row = 0; row < profile.Length - 1; row++)
                for (int i = 0; i < Sides; i++)
                {
                    Vector3 a = Point(profile[row],i), b = Point(profile[row+1],i);
                    Vector3 c = Point(profile[row+1],i+1), d = Point(profile[row],i+1);
                    if (profile[row].x <= 0f) Triangle(a,b,c);
                    else if (profile[row+1].x <= 0f) Triangle(a,c,d);
                    else Quad(a,b,c,d);
                }
        }

        public void Box(Vector3 center, Vector3 size)
        {
            Vector3 h = size * .5f;
            Vector3 p0 = center + new Vector3(-h.x,-h.y,-h.z), p1 = center + new Vector3(h.x,-h.y,-h.z);
            Vector3 p2 = center + new Vector3(h.x,h.y,-h.z), p3 = center + new Vector3(-h.x,h.y,-h.z);
            Vector3 p4 = center + new Vector3(-h.x,-h.y,h.z), p5 = center + new Vector3(h.x,-h.y,h.z);
            Vector3 p6 = center + new Vector3(h.x,h.y,h.z), p7 = center + new Vector3(-h.x,h.y,h.z);
            Quad(p0,p3,p2,p1); Quad(p4,p5,p6,p7);
            Quad(p0,p4,p7,p3); Quad(p1,p2,p6,p5);
            Quad(p0,p1,p5,p4); Quad(p3,p7,p6,p2);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
