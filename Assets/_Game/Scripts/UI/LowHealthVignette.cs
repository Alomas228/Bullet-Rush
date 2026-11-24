using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerHealth))]
public sealed class LowHealthVignette : MonoBehaviour
{
    private const float StartThreshold = 0.40f;
    private const float CriticalThreshold = 0.10f;
    private PlayerHealth health;
    private Canvas canvas;
    private RawImage image;
    private Texture2D mask;
    private float intensity;
    private float phase;

    private void Awake() => health = GetComponent<PlayerHealth>();

    private void LateUpdate()
    {
        if (health != PlayerHealth.Instance || health.CurrentHealth <= 0f ||
            (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Playing))
        {
            Hide();
            return;
        }

        float fraction = health.CurrentHealth / Mathf.Max(health.MaxHealth, 0.01f);
        float severity = Mathf.InverseLerp(StartThreshold, CriticalThreshold, fraction);
        float target = fraction <= StartThreshold ? Mathf.Lerp(0.10f, 0.82f, severity) : 0f;
        intensity = Mathf.MoveTowards(intensity, target, Time.unscaledDeltaTime * 2.5f);
        if (intensity <= 0f)
        {
            Hide();
            return;
        }

        if (canvas == null) CreateOverlay();
        canvas.enabled = true;
        phase = Mathf.Repeat(phase + Time.unscaledDeltaTime * Mathf.Lerp(0.65f, 1.25f, severity), 1f);
        float pulse = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
        image.color = new Color(0.85f, 0.035f, 0.045f, intensity * Mathf.Lerp(0.32f, 1f, pulse));
    }

    private void CreateOverlay()
    {
        var root = new GameObject("Low Health Vignette", typeof(RectTransform), typeof(Canvas));
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1;
        var edge = new GameObject("Red edge", typeof(RectTransform), typeof(RawImage));
        edge.transform.SetParent(root.transform, false);
        image = edge.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.rectTransform.anchorMin = Vector2.zero;
        image.rectTransform.anchorMax = Vector2.one;
        image.rectTransform.offsetMin = image.rectTransform.offsetMax = Vector2.zero;

        const int size = 128;
        mask = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Low Health Edge Mask",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };
        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float nx = x / (size - 1f) * 2f - 1f;
            float ny = y / (size - 1f) * 2f - 1f;
            float radius = Mathf.Sqrt(nx * nx + ny * ny);
            float edgeAlpha = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.58f, 1.15f, radius));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(edgeAlpha * 255f));
        }
        mask.SetPixels32(pixels);
        mask.Apply(false, true);
        image.texture = mask;
    }

    private void Hide()
    {
        intensity = phase = 0f;
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDisable() => Hide();

    private void OnDestroy()
    {
        if (canvas != null) Destroy(canvas.gameObject);
        if (mask != null) Destroy(mask);
    }
}
