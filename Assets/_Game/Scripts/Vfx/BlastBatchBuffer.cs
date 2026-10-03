using UnityEngine;

/// <summary>
/// Instance data consumed by Graphics.RenderMeshInstanced.
///
/// RenderMeshInstanced extracts only Unity's documented fields
/// (objectToWorld, renderingLayerMask, prevObjectToWorld) from a
/// custom struct and ignores everything else. So per-instance color
/// is NOT stored here - it goes through a MaterialPropertyBlock
/// vector array instead.
/// </summary>
public struct BlastInstanceData
{
    public Matrix4x4 objectToWorld;
}

/// <summary>
/// Reusable accumulator for one explosion layer.
///
/// items and allColors grow on demand, so there is no per-frame
/// allocation. drawItems and drawColors stay fixed at the
/// RenderMeshInstanced per-draw limit and are reused every frame: the
/// MaterialPropertyBlock vector array length must never change after
/// the first assignment ("The array length can't be changed once it
/// has been added to the block"), so the same fixed-length array is
/// submitted for every draw call.
/// </summary>
public sealed class BlastBatchBuffer
{
    private const int InitialCapacity = 64;

    private const int MaxDrawCapacity = 511;

    private BlastInstanceData[] items =
        new BlastInstanceData[InitialCapacity];

    private Vector4[] allColors = new Vector4[InitialCapacity];

    private readonly BlastInstanceData[] drawItems =
        new BlastInstanceData[MaxDrawCapacity];

    private readonly Vector4[] drawColors =
        new Vector4[MaxDrawCapacity];

    public int Count { get; private set; }

    public int Capacity => items.Length;

    public BlastInstanceData[] Items => items;

    /// <summary>
    /// Fixed-length staging area. Always MaxDrawCapacity long, because
    /// the MPB array length has to stay constant forever.
    /// </summary>
    public BlastInstanceData[] DrawItems => drawItems;

    public Vector4[] DrawColors => drawColors;

    public void Add(Matrix4x4 matrix, Vector4 color)
    {
        int slot = Count;

        if (slot >= items.Length)
            Grow(items.Length * 2);

        items[slot].objectToWorld = matrix;
        allColors[slot] = color;

        Count = slot + 1;
    }

    public void Clear()
    {
        Count = 0;
    }

    /// <summary>
    /// Copies one chunk into the fixed-size arrays starting at index 0.
    /// Matrix and color are prepared together on purpose: the shader
    /// reads color by the same instance ID as the matrix, so both
    /// arrays must be indexed identically or they will drift apart.
    ///
    /// Copying to zero also means the draw always uses startInstance =
    /// 0 and never depends on how Unity counts instances for a
    /// non-zero startInstance.
    /// </summary>
    public void PrepareChunk(int start, int count)
    {
        if (count < 0 || count > MaxDrawCapacity)
            throw new System.ArgumentOutOfRangeException(nameof(count));

        if (start < 0 || start + count > Count)
            throw new System.ArgumentOutOfRangeException(nameof(start));

        System.Array.Copy(items, start, drawItems, 0, count);
        System.Array.Copy(allColors, start, drawColors, 0, count);
    }

    private void Grow(int capacity)
    {
        System.Array.Resize(ref items, capacity);
        System.Array.Resize(ref allColors, capacity);
    }
}
