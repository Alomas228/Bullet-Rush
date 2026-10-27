using UnityEngine;

public class ObjectiveMarkerMaterialCleanup : MonoBehaviour
{
    private Material runtimeMaterial;

    public void Initialize(Material material)
    {
        runtimeMaterial = material;
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
            Destroy(runtimeMaterial);
    }
}
