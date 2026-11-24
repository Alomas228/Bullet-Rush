using UnityEngine;

public partial class Enemy
{
    private static readonly uint[] bodySkinSequence = new uint[7];
    private bool bodySkinAssigned;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetBodySkinSequence()
    {
        System.Array.Clear(bodySkinSequence, 0, bodySkinSequence.Length);
    }

    private void ApplyBodySkin()
    {
        if (bodySkinAssigned || isBoss || enemyData == null || variantRenderer == null)
            return;

        Mesh[] options = enemyData.BodySkins;
        Material material = enemyData.BodySkinMaterial;
        if (options == null || options.Length == 0 || material == null) return;

        MeshFilter filter = variantRenderer.GetComponent<MeshFilter>();
        if (filter == null) return;

        // Rotate within each role independently. No draws from the gameplay
        // RNG, no new mesh/material instances, and no changes to colliders.
        int role = (int)cachedEnemyType;
        uint sequence = role >= 0 && role < bodySkinSequence.Length
            ? bodySkinSequence[role]++ : 0u;
        int start = (int)(sequence % (uint)options.Length);
        for (int i = 0; i < options.Length; i++)
        {
            Mesh skin = options[(start + i) % options.Length];
            if (skin == null) continue;

            filter.sharedMesh = skin;
            variantRenderer.sharedMaterial = material;
            // Existing variant/map tinting must start from the neutral atlas
            // material, rather than multiplying the original solid body color.
            baseVariantMaterial = material;
            bodySkinAssigned = true;
            break;
        }
    }
}
