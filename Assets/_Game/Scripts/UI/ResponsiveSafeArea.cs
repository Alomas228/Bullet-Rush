using UnityEngine;

[DisallowMultipleComponent]
public sealed class ResponsiveSafeArea : MonoBehaviour
{
    private RectTransform target;

    private void OnEnable() => Apply();
    private void LateUpdate() => Apply();

    public void Apply()
    {
        if (target == null) target = transform as RectTransform;
        if (target == null || !(target.parent is RectTransform parent)) return;
        Rect bounds = parent.rect;
        if (bounds.width <= 0 || bounds.height <= 0) return;
        Rect safe = ResponsiveUi.SafeRect(parent);
        Vector2 min = (safe.min - bounds.min) / bounds.size;
        Vector2 max = (safe.max - bounds.min) / bounds.size;
        if (target.anchorMin != min) target.anchorMin = min;
        if (target.anchorMax != max) target.anchorMax = max;
        if (target.offsetMin != Vector2.zero) target.offsetMin = Vector2.zero;
        if (target.offsetMax != Vector2.zero) target.offsetMax = Vector2.zero;
    }
}
