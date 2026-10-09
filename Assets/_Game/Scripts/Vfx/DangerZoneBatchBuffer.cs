using UnityEngine;

/// <summary>
/// Instance data consumed by Graphics.RenderMeshInstanced for the
/// danger-zone layer.
///
/// Like BlastInstanceData: RenderMeshInstanced extracts only the
/// documented fields (objectToWorld, renderingLayerMask,
/// prevObjectToWorld) from a custom struct. Color and phase params
/// therefore travel through MaterialPropertyBlock vector arrays, not
/// through this struct.
/// </summary>
public struct DangerZoneInstanceData
{
    public Matrix4x4 objectToWorld;
}

/// <summary>
/// Reusable accumulator for the single danger-zone layer.
///
/// Two per-instance arrays travel together:
///   colors = (r, g, b, baseAlpha)
///   params = (warnProgress, spinPhase, active, flash)
///
/// drawItems, drawColors and drawParams stay fixed at the
/// RenderMeshInstanced per-draw limit and are reused every frame: a
/// MaterialPropertyBlock vector array length must never change after
/// the first assignment ("The array length can't be changed once it
/// has been added to the block"), so the same fixed-length arrays are
/// submitted for every draw call.
/// </summary>
public sealed class DangerZoneBatchBuffer
{
    private const int InitialCapacity = 64;

    private const int MaxDrawCapacity = 511;

    private DangerZoneInstanceData[] items =
        new DangerZoneInstanceData[InitialCapacity];

    private Vector4[] allColors = new Vector4[InitialCapacity];
    private Vector4[] allParams = new Vector4[InitialCapacity];

    private readonly DangerZoneInstanceData[] drawItems =
        new DangerZoneInstanceData[MaxDrawCapacity];

    private readonly Vector4[] drawColors =
        new Vector4[MaxDrawCapacity];

    private readonly Vector4[] drawParams =
        new Vector4[MaxDrawCapacity];

    public int Count { get; private set; }

    public int Capacity => items.Length;

    public DangerZoneInstanceData[] DrawItems => drawItems;

    public Vector4[] DrawColors => drawColors;

    public Vector4[] DrawParams => drawParams;

    public void Add(
        Matrix4x4 matrix,
        Vector4 color,
        Vector4 parameters)
    {
        int slot = Count;

        if (slot >= items.Length)
            Grow(items.Length * 2);

        items[slot].objectToWorld = matrix;
        allColors[slot] = color;
        allParams[slot] = parameters;

        Count = slot + 1;
    }

    public void Clear()
    {
        Count = 0;
    }

    /// <summary>
    /// Copies one chunk into the fixed-size arrays starting at index 0.
    /// Matrix, color and params are prepared together on purpose: the
    /// shader reads them by the same instance ID, so all three arrays
    /// must stay index-aligned.
    /// </summary>
    public void PrepareChunk(int start, int count)
    {
        if (count < 0 || count > MaxDrawCapacity)
            throw new System.ArgumentOutOfRangeException(nameof(count));

        if (start < 0 || start + count > Count)
            throw new System.ArgumentOutOfRangeException(nameof(start));

        System.Array.Copy(items, start, drawItems, 0, count);
        System.Array.Copy(allColors, start, drawColors, 0, count);
        System.Array.Copy(allParams, start, drawParams, 0, count);
    }

    private void Grow(int capacity)
    {
        System.Array.Resize(ref items, capacity);
        System.Array.Resize(ref allColors, capacity);
        System.Array.Resize(ref allParams, capacity);
    }
}
