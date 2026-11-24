using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Shared screen-space layout rules. Never applies to world-space canvases.</summary>
public static class ResponsiveUi
{
    private static int safeFrame = -1;
    private static Rect cachedSafe;

    public static Rect ScreenSafeArea
    {
        get
        {
            if (Application.isPlaying && safeFrame == Time.frameCount) return cachedSafe;
            float width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            Rect safe = Screen.safeArea;
            safe = Rect.MinMaxRect(Mathf.Clamp(safe.xMin, 0, width), Mathf.Clamp(safe.yMin, 0, height),
                Mathf.Clamp(safe.xMax, 0, width), Mathf.Clamp(safe.yMax, 0, height));
            cachedSafe = safe.width > 0 && safe.height > 0 ? safe : new Rect(0,0,width,height);
            safeFrame = Time.frameCount;
            return cachedSafe;
        }
    }

    public static bool Portrait => ScreenSafeArea.height > ScreenSafeArea.width;

    // The smallest axis determines the UI scale; a narrow or short window never crops
    // the reference coordinates. Portrait uses its own reference, not a tiny landscape canvas.
    public static Vector2 Reference(Vector2 landscape, bool portrait) => portrait
        ? new Vector2(landscape.y, landscape.y * 4f / 3f) : landscape;

    public static float Fit(Vector2 available, Vector2 design, bool enlarge = false)
    {
        if (available.x <= 0 || available.y <= 0 || design.x <= 0 || design.y <= 0) return 1f;
        float scale = Mathf.Min(available.x / design.x, available.y / design.y);
        return enlarge ? scale : Mathf.Min(1f, scale);
    }

    public static void Configure(CanvasScaler scaler, Vector2 landscape)
    {
        if (scaler == null) return;
        var canvas = scaler.GetComponent<Canvas>();
        if (canvas == null || canvas.renderMode == RenderMode.WorldSpace || !canvas.isRootCanvas) return;
        var policy = scaler.GetComponent<ResponsiveCanvas>();
        if (policy == null) policy = scaler.gameObject.AddComponent<ResponsiveCanvas>();
        policy.Configure(scaler, landscape);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        ConfigureScene();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ConfigureScene();

    private static void ConfigureScene()
    {
        foreach (Canvas canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include))
        {
            if (canvas.GetComponent<ResponsiveCanvas>() != null) continue;
            var scaler = canvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize)
                Configure(scaler, scaler.referenceResolution);
        }
    }

    /// <summary>Safe screen rectangle intersected with the host, in host-local UI units.</summary>
    public static Rect SafeRect(RectTransform host)
    {
        Rect bounds = host.rect;
        Canvas canvas = host.GetComponentInParent<Canvas>();
        if (canvas == null || canvas.rootCanvas.renderMode == RenderMode.WorldSpace) return bounds;
        Canvas root = canvas.rootCanvas;
        Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
        Rect screen = ScreenSafeArea;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(host, screen.min, camera, out Vector2 min) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(host, screen.max, camera, out Vector2 max))
            return bounds;
        Rect safe = Rect.MinMaxRect(Mathf.Max(bounds.xMin, min.x), Mathf.Max(bounds.yMin, min.y),
            Mathf.Min(bounds.xMax, max.x), Mathf.Min(bounds.yMax, max.y));
        return safe.width > 0 && safe.height > 0 ? safe : bounds;
    }

    public static RectTransform SafeContent(RectTransform parent, string name = "Safe area")
    {
        var rect = ArcadeUiFactory.Stretch(parent, name);
        BindSafeArea(rect);
        return rect;
    }

    // Use for a full-screen content layer (not the canvas or the modal dimmer itself).
    public static void BindSafeArea(RectTransform rect)
    {
        var safe = rect.GetComponent<ResponsiveSafeArea>();
        if (safe == null) safe = rect.gameObject.AddComponent<ResponsiveSafeArea>();
        safe.Apply();
    }
}
