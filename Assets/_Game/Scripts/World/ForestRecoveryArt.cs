using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// A small woodland shrine, an organic moss clearing and a readable healing sign.
// All three live sites share their meshes/materials and have no physics bodies.
public sealed class ForestRecoveryArt
{
    private readonly Mesh[] meshes = new Mesh[4];
    private readonly Material[] materials = new Material[4];

    public ForestRecoveryArt(float radius)
    {
        var moss = new Geometry();
        moss.Patch(radius);
        var wood = new Geometry();
        wood.Lathe(Vector3.zero, 9, new Vector2(0,.04f), new Vector2(.73f,.04f), new Vector2(.63f,.25f),
            new Vector2(.47f,.35f), new Vector2(.45f,.77f), new Vector2(.53f,.83f), new Vector2(0,.83f));
        // Short roots and angular bark ridges break up the stump silhouette.
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2f / 5f;
            Vector3 root = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * .64f;
            wood.Lathe(root, 5, new Vector2(.23f,.04f), new Vector2(.17f,.22f), new Vector2(0,.29f));
        }
        var leaves = new Geometry();
        leaves.Lathe(Vector3.zero, 9, new Vector2(.51f,.80f), new Vector2(.56f,.86f), new Vector2(.42f,.91f), new Vector2(0,.91f));
        // Low faceted sprigs, arranged naturally around the foot rather than on a ring.
        for (int i = 0; i < 7; i++)
        {
            float a = i * 2.39996f;
            float d = .82f + (i % 3) * .30f;
            Vector3 p = new Vector3(Mathf.Cos(a) * d,.055f,Mathf.Sin(a) * d);
            for (int j = 0; j < 3; j++) leaves.Leaf(p, a + j * 2.0944f, .32f + .08f * ((i+j)%3));
        }
        var sign = new Geometry();
        // Horizontal, beveled-looking chunky plus, readable from a top-down camera.
        sign.Box(Vector3.zero, new Vector3(1.06f,.19f,.33f));
        sign.Box(new Vector3(0,0,.345f), new Vector3(.33f,.19f,.36f));
        sign.Box(new Vector3(0,0,-.345f), new Vector3(.33f,.19f,.36f));

        meshes[0] = moss.ToMesh("Recovery clearing - moss");
        meshes[1] = wood.ToMesh("Recovery clearing - carved stump");
        meshes[2] = leaves.ToMesh("Recovery clearing - healing sprigs");
        meshes[3] = sign.ToMesh("Recovery clearing - medical cross");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Unlit");
        materials[0] = MaterialOf(shader, "Soft moss", new Color32(145,177,105,255));
        materials[1] = MaterialOf(shader, "Warm bark", new Color32(132,91,56,255));
        materials[2] = MaterialOf(shader, "Healing leaves", new Color32(92,173,98,255));
        materials[3] = MaterialOf(shader, "Healing marker", new Color32(205,255,189,255));
        if (materials[3].HasProperty("_EmissionColor"))
        {
            materials[3].EnableKeyword("_EMISSION");
            materials[3].SetColor("_EmissionColor", new Color(.17f,.36f,.14f));
        }
    }

    private static Material MaterialOf(Shader shader, string name, Color color)
    {
        var material = new Material(shader) { name = name, color = color, enableInstancing = true };
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", .1f);
        return material;
    }

    public GameObject Create(Transform parent, out Transform marker)
    {
        var root = new GameObject("Forest healing shrine");
        root.transform.SetParent(parent, false);
        marker = null;
        for (int i = 0; i < meshes.Length; i++)
        {
            var part = new GameObject(meshes[i].name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(root.transform, false);
            part.GetComponent<MeshFilter>().sharedMesh = meshes[i];
            MeshRenderer renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[i];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (i == meshes.Length - 1)
            {
                marker = part.transform;
                marker.localPosition = Vector3.up * 1.24f;
            }
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
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<int> indices = new List<int>();

        private void Face(Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;
            vertices.Add(a); vertices.Add(b); vertices.Add(c);
            indices.Add(start); indices.Add(start+1); indices.Add(start+2);
        }

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Face(a,b,c); Face(a,c,d);
        }

        private static Vector3 Point(Vector3 center, Vector2 profile, int i, int sides)
        {
            float a = i * Mathf.PI * 2f / sides;
            return center + new Vector3(Mathf.Cos(a)*profile.x,profile.y,Mathf.Sin(a)*profile.x);
        }

        public void Lathe(Vector3 center, int sides, params Vector2[] profile)
        {
            for (int row = 0; row < profile.Length-1; row++)
                for (int i = 0; i < sides; i++)
                {
                    Vector3 a = Point(center,profile[row],i,sides), b = Point(center,profile[row+1],i,sides);
                    Vector3 c = Point(center,profile[row+1],i+1,sides), d = Point(center,profile[row],i+1,sides);
                    if (profile[row].x == 0) Face(a,b,c);
                    else if (profile[row+1].x == 0) Face(a,c,d);
                    else Quad(a,b,c,d);
                }
        }

        public void Patch(float radius)
        {
            // An irregular silhouette, not a line ring; almost the whole effective radius is visible.
            const int sides = 24;
            Vector3 center = Vector3.up * .035f;
            var points = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float angle = i * Mathf.PI * 2f / sides;
                float r = radius * (.94f + .06f * Mathf.Sin(i * 2.4f));
                points[i] = center + new Vector3(Mathf.Cos(angle)*r,0,Mathf.Sin(angle)*r);
            }
            for (int i=0; i<sides; i++) Face(center,points[(i+1)%sides],points[i]);
        }

        public void Leaf(Vector3 p, float angle, float length)
        {
            Vector3 dir = new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            Vector3 side = new Vector3(-dir.z,0,dir.x) * length * .30f;
            Vector3 tip = p + dir * length + Vector3.up * length * .45f;
            Vector3 ridge = p + dir * length * .47f + Vector3.up * length * .42f;
            Vector3 left = p + dir * length * .43f + side + Vector3.up * length * .14f;
            Vector3 right = p + dir * length * .43f - side + Vector3.up * length * .14f;
            Face(p,left,ridge); Face(left,tip,ridge); Face(ridge,tip,right); Face(p,ridge,right);
            Face(ridge,left,p); Face(ridge,tip,left); Face(right,tip,ridge); Face(right,ridge,p);
        }

        public void Box(Vector3 center, Vector3 size)
        {
            Vector3 h = size*.5f;
            Vector3 p0=center+new Vector3(-h.x,-h.y,-h.z), p1=center+new Vector3(h.x,-h.y,-h.z);
            Vector3 p2=center+new Vector3(h.x,h.y,-h.z), p3=center+new Vector3(-h.x,h.y,-h.z);
            Vector3 p4=center+new Vector3(-h.x,-h.y,h.z), p5=center+new Vector3(h.x,-h.y,h.z);
            Vector3 p6=center+new Vector3(h.x,h.y,h.z), p7=center+new Vector3(-h.x,h.y,h.z);
            Quad(p0,p3,p2,p1); Quad(p4,p5,p6,p7); Quad(p0,p4,p7,p3);
            Quad(p1,p2,p6,p5); Quad(p0,p1,p5,p4); Quad(p3,p7,p6,p2);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices); mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
