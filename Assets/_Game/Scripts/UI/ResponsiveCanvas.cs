using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class ResponsiveCanvas : MonoBehaviour
{
    private CanvasScaler scaler;
    private Vector2 landscape;

    public void Configure(CanvasScaler target, Vector2 reference)
    {
        scaler = target;
        landscape = reference;
        Apply();
    }

    private void Update() => Apply();

    private void Apply()
    {
        if (scaler == null) return;
        Vector2 reference = ResponsiveUi.Reference(landscape, ResponsiveUi.Portrait);
        if (scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        if (scaler.screenMatchMode != CanvasScaler.ScreenMatchMode.Expand)
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        if (scaler.referenceResolution != reference) scaler.referenceResolution = reference;
    }
}
