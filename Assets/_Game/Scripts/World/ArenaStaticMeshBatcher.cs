using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Combines opaque, static decoration into bounded material batches. Interactive
/// objects keep their transforms/colliders; each breakable/occluder is batched separately.</summary>
public sealed class ArenaStaticMeshBatcher : MonoBehaviour
{
    private const float ChunkSize = 24f;
    private const int VertexLimit = 60000;
    private readonly List<Mesh> ownedMeshes = new List<Mesh>();

    public static void BatchDecoration(Transform parent, int firstChild)
    {
        int end = parent.childCount;
        var renderers = new List<MeshRenderer>();
        for (int i = firstChild; i < end; i++)
        {
            GameObject root = parent.GetChild(i).gameObject;
            if (root.GetComponent<WorldStructure>() != null || !CanBatch(root, false)) continue;
            renderers.AddRange(root.GetComponentsInChildren<MeshRenderer>(false));
        }
        Combine(parent, renderers, true);
    }

    public static void BatchObject(GameObject root)
    {
        if (root == null || !CanBatch(root, true)) return;
        var renderers = new List<MeshRenderer>(root.GetComponentsInChildren<MeshRenderer>(false));
        Combine(root.transform, renderers, false);
    }

    private static bool CanBatch(GameObject root, bool allowColliders)
    {
        if (root.GetComponentInChildren<Animator>(true) != null || root.GetComponentInChildren<ParticleSystem>(true) != null ||
            root.GetComponentInChildren<LODGroup>(true) != null) return false;
        foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            if (behaviour != null && !(behaviour is WorldStructure) && !(behaviour is ArenaStaticMeshBatcher)) return false;
        if (!allowColliders)
            foreach (Collider collider in root.GetComponentsInChildren<Collider>(true))
                if (collider != null && collider.enabled) return false;
        return true;
    }

    private static void Combine(Transform owner, List<MeshRenderer> renderers, bool spatial)
    {
        var groups = new Dictionary<(Material material, int layer, ShadowCastingMode shadows, bool receive, int cellX, int cellZ), List<MeshRenderer>>();
        foreach (MeshRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || renderer.HasPropertyBlock() ||
                renderer.renderingLayerMask != 1u || renderer.sharedMaterials.Length != 1 ||
                (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534)) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            Material material = renderer.sharedMaterial;
            if (mesh == null || !mesh.isReadable || mesh.subMeshCount != 1 || mesh.vertexCount > VertexLimit ||
                material == null || material.renderQueue >= 3000) continue;
            Vector3 localCenter = owner.InverseTransformPoint(renderer.bounds.center);
            var key = (material, renderer.gameObject.layer, renderer.shadowCastingMode, renderer.receiveShadows,
                spatial ? Mathf.FloorToInt(localCenter.x / ChunkSize) : 0,
                spatial ? Mathf.FloorToInt(localCenter.z / ChunkSize) : 0);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>();
            list.Add(renderer);
        }

        ArenaStaticMeshBatcher lifetime = null;
        Matrix4x4 inverse = owner.worldToLocalMatrix;
        foreach (var group in groups)
        {
            List<MeshRenderer> list = group.Value;
            int start = 0;
            while (start < list.Count)
            {
                int totalVertices = 0;
                int end = start;
                while (end < list.Count)
                {
                    int next = list[end].GetComponent<MeshFilter>().sharedMesh.vertexCount;
                    if (totalVertices + next > VertexLimit) break;
                    totalVertices += next;
                    end++;
                }
                if (end - start < 2) { start = Mathf.Max(end, start + 1); continue; }
                var instances = new CombineInstance[end - start];
                for (int i = start; i < end; i++)
                    instances[i - start] = new CombineInstance
                    {
                        mesh = list[i].GetComponent<MeshFilter>().sharedMesh,
                        transform = inverse * list[i].transform.localToWorldMatrix,
                        subMeshIndex = 0
                    };
                Mesh combined = new Mesh { name = "Arena material batch" };
                try { combined.CombineMeshes(instances, true, true, false); }
                catch (System.Exception) { Destroy(combined); start = end; continue; }
                if (combined.vertexCount == 0) { Destroy(combined); start = end; continue; }
                if (lifetime == null)
                {
                    lifetime = owner.GetComponent<ArenaStaticMeshBatcher>();
                    if (lifetime == null) lifetime = owner.gameObject.AddComponent<ArenaStaticMeshBatcher>();
                }
                lifetime.ownedMeshes.Add(combined);
                combined.RecalculateBounds();
                var batch = new GameObject("Combined " + group.Key.material.name);
                batch.layer = group.Key.layer;
                batch.transform.SetParent(owner, false);
                batch.AddComponent<MeshFilter>().sharedMesh = combined;
                MeshRenderer result = batch.AddComponent<MeshRenderer>();
                result.sharedMaterial = group.Key.material;
                result.shadowCastingMode = group.Key.shadows;
                result.receiveShadows = group.Key.receive;
                for (int i = start; i < end; i++) list[i].enabled = false;
                start = end;
            }
        }
    }

    private void OnDestroy()
    {
        foreach (Mesh mesh in ownedMeshes) if (mesh != null) Destroy(mesh);
    }
}
