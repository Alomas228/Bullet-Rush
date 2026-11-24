using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Explicit instancing for matching animated enemy, weapon and loot meshes.
/// Singletons and unsupported materials retain their regular renderers.</summary>
[DefaultExecutionOrder(1000)]
public sealed class BattleMeshBatcher : MonoBehaviour
{
    private static BattleMeshBatcher instance;
    private static bool unavailable;
    private readonly List<(MeshRenderer renderer, MeshFilter filter)> registered = new List<(MeshRenderer, MeshFilter)>(128);
    private readonly HashSet<MeshRenderer> registeredSet = new HashSet<MeshRenderer>();
    private readonly Dictionary<(Mesh mesh, Material material, int layer, ShadowCastingMode shadows, bool receive), List<MeshRenderer>> groups =
        new Dictionary<(Mesh, Material, int, ShadowCastingMode, bool), List<MeshRenderer>>();
    private readonly Matrix4x4[] matrices = new Matrix4x4[511];
    private readonly List<(Mesh mesh, Material material, int layer, ShadowCastingMode shadows, bool receive)> unusedKeys = new List<(Mesh, Material, int, ShadowCastingMode, bool)>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; unavailable = false; }

    public static void Register(MeshRenderer renderer)
    {
        if (renderer == null || !renderer.enabled || renderer.forceRenderingOff || unavailable || !SystemInfo.supportsInstancing ||
            renderer.sharedMaterials.Length != 1 || (renderer.lightmapIndex >= 0 && renderer.lightmapIndex < 65534)) return;
        MeshFilter filter = renderer.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null || filter.sharedMesh.subMeshCount != 1) return;
        Material material = renderer.sharedMaterial;
        if (material != null && material.renderQueue < 3000 && material.shader != null && !material.enableInstancing)
        {
            string shaderName = material.shader.name;
            if (shaderName == "Universal Render Pipeline/Lit" || shaderName == "Universal Render Pipeline/Simple Lit" || shaderName == "Standard")
                material.enableInstancing = true;
        }
        if (instance == null) instance = new GameObject("Battle mesh instances").AddComponent<BattleMeshBatcher>();
        if (instance.registeredSet.Add(renderer)) instance.registered.Add((renderer, filter));
    }

    public static void RegisterTree(GameObject root)
    {
        if (root == null || unavailable || !SystemInfo.supportsInstancing) return;
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true)) Register(renderer);
    }

    private void LateUpdate()
    {
        foreach (var entry in groups) entry.Value.Clear();
        for (int i = registered.Count - 1; i >= 0; i--)
        {
            MeshRenderer renderer = registered[i].renderer;
            if (renderer == null)
            {
                registeredSet.Remove(renderer);
                registered.RemoveAt(i);
                continue;
            }
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.HasPropertyBlock() ||
                renderer.renderingLayerMask != 1u || renderer.shadowCastingMode == ShadowCastingMode.ShadowsOnly)
            { renderer.forceRenderingOff = false; continue; }

            MeshFilter filter = registered[i].filter;
            Mesh mesh = filter != null ? filter.sharedMesh : null;
            Material material = renderer.sharedMaterial;
            if (mesh == null || mesh.subMeshCount != 1 || material == null || !material.enableInstancing || material.renderQueue >= 3000)
            { renderer.forceRenderingOff = false; continue; }

            var key = (mesh, material, renderer.gameObject.layer, renderer.shadowCastingMode, renderer.receiveShadows);
            if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshRenderer>(8);
            list.Add(renderer);
        }

        foreach (var group in groups)
        {
            List<MeshRenderer> list = group.Value;
            if (list.Count < 2)
            {
                if (list.Count == 1) list[0].forceRenderingOff = false;
                continue;
            }
            try
            {
                for (int start = 0; start < list.Count; start += matrices.Length)
                {
                    int count = Mathf.Min(matrices.Length, list.Count - start);
                    Bounds bounds = list[start].bounds;
                    for (int i = 0; i < count; i++)
                    {
                        MeshRenderer renderer = list[start + i];
                        matrices[i] = renderer.transform.localToWorldMatrix;
                        bounds.Encapsulate(renderer.bounds);
                    }
                    var render = new RenderParams(group.Key.material)
                    {
                        layer = group.Key.layer,
                        worldBounds = bounds,
                        shadowCastingMode = group.Key.shadows,
                        receiveShadows = group.Key.receive,
                        lightProbeUsage = LightProbeUsage.BlendProbes
                    };
                    Graphics.RenderMeshInstanced(render, group.Key.mesh, 0, matrices, count);
                    for (int i = 0; i < count; i++) list[start + i].forceRenderingOff = true;
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("[BattleMeshBatcher] Instancing unavailable, using original renderers: " + error.Message);
                unavailable = true;
                RestoreRenderers();
                enabled = false;
                return;
            }
        }

        if (Time.frameCount % 240 == 0)
        {
            unusedKeys.Clear();
            foreach (var group in groups) if (group.Value.Count == 0) unusedKeys.Add(group.Key);
            foreach (var key in unusedKeys) groups.Remove(key);
        }
    }

    private void RestoreRenderers()
    {
        foreach (var entry in registered) if (entry.renderer != null) entry.renderer.forceRenderingOff = false;
    }

    private void OnDestroy()
    {
        RestoreRenderers();
        if (instance == this) instance = null;
    }
}
