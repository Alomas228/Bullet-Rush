using UnityEngine;

// Keeps fixed-size controls inside the canvas when the browser changes aspect ratio.
public sealed class UiArtboard : MonoBehaviour
{
    private RectTransform content;
    private Vector2 design;
    private bool enlarge;

    public static RectTransform Create(RectTransform host, Vector2 size, bool enlarge = false)
    {
        var fit = host.gameObject.AddComponent<UiArtboard>();
        fit.design = size;
        fit.enlarge = enlarge;
        fit.content = ArcadeUiFactory.At(host, "Content", Vector2.one * .5f,
            Vector2.one * .5f, Vector2.zero, size);
        fit.Fit();
        return fit.content;
    }

    public static void Resize(RectTransform board, Vector2 size)
    {
        if (board == null || board.parent == null) return;
        foreach (var fit in board.parent.GetComponents<UiArtboard>())
        {
            if (fit.content != board) continue;
            fit.design = size;
            board.sizeDelta = size;
            fit.Fit();
            return;
        }
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 point, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = point;
    }

    private void OnEnable() => Fit();
    private void LateUpdate() => Fit();
    private void OnRectTransformDimensionsChange() => Fit();

    private void Fit()
    {
        if (content == null) return;
        var host = (RectTransform)transform;
        Rect safe = ResponsiveUi.SafeRect(host);
        if (safe.width <= 0 || safe.height <= 0) return;
        float scale = ResponsiveUi.Fit(safe.size, design, enlarge);
        Vector3 nextScale = Vector3.one * scale;
        Vector2 position = safe.center - host.rect.center;
        if (content.localScale != nextScale) content.localScale = nextScale;
        if (content.anchoredPosition != position) content.anchoredPosition = position;
    }
}
