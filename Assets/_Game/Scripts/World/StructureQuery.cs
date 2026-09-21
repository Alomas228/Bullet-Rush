using System.Collections.Generic;
using UnityEngine;

public static class StructureQuery
{
    private static readonly Dictionary<EntityId, WorldStructure> structureCache =
        new Dictionary<EntityId, WorldStructure>(64);

    private static Collider[] overlapBuffer = new Collider[16];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ResetStaticState()
    {
        structureCache.Clear();
        overlapBuffer = new Collider[16];
    }

    public static bool TryGetWorldStructure(
        Collider collider,
        out WorldStructure structure
    )
    {
        structure = null;

        if (collider == null)
            return false;

        EntityId id = collider.GetEntityId();

        if (structureCache.TryGetValue(id, out WorldStructure cached))
        {
            structure = cached;
            return cached != null;
        }

        structure = collider.GetComponentInParent<WorldStructure>();
        structureCache[id] = structure;

        return structure != null;
    }

    public static bool IsWorldStructure(Collider collider)
    {
        return TryGetWorldStructure(collider, out _);
    }

    public static Collider[] OverlapSphere(
        Vector3 position,
        float radius,
        out int count
    )
    {
        count = Physics.OverlapSphereNonAlloc(
            position,
            radius,
            overlapBuffer
        );

        while (count == overlapBuffer.Length)
        {
            overlapBuffer = new Collider[overlapBuffer.Length * 2];
            count = Physics.OverlapSphereNonAlloc(
                position,
                radius,
                overlapBuffer
            );
        }

        return overlapBuffer;
    }
}
