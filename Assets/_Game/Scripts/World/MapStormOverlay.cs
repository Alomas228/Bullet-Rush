using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Затемнение экрана во время вспышек песчаной бури (Пустыня).
///
/// Один постоянный RawImage поверх всего UI: когда буря бьёт зонами,
/// экран ненадолго темнеет и прозрачность понимается — песок
/// «реально» закрывает обзор. Полное полуслепое состояние не нужнo,
/// поэтому альфа держится на уровне интенсивности бури.
///
/// RawImage без текстуры рисует сплошной четырехугольник цветом
/// <c>color</c>, отдельный спрайт не нужен.
/// </summary>
public class MapStormOverlay : MonoBehaviour
{
    private static MapStormOverlay instance;
    private static Texture2D stormTexture;

    private RawImage overlay;
    private Color baseColor =
        new Color(0.18f, 0.14f, 0.08f, 0f);

    private float windOffset;

    /// <summary>
    /// Вспышка бури: плавно затемняем, держим, плавно убираем.
    /// Повторный вызов мягко перезапускает текущий цикл с нового
    /// максимума, а не наслаивает ещё один.
    /// </summary>
    public static void Pulse(
        float intensity,
        float fadeIn,
        float hold,
        float fadeOut)
    {
        if (instance == null)
            Create();

        if (instance != null)
            instance.StartPulse(
                Mathf.Clamp01(intensity),
                Mathf.Max(fadeIn, 0.05f),
                Mathf.Max(hold, 0f),
                Mathf.Max(fadeOut, 0.05f)
            );
    }

    private static void Create()
    {
        GameObject host = new GameObject("MapStormOverlay");

        MapStormOverlay overlay = host.AddComponent<MapStormOverlay>();

        instance = overlay;

        DontDestroyOnLoad(host);
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 800;

        overlay = gameObject.AddComponent<RawImage>();

        RectTransform rect = overlay.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        overlay.raycastTarget = false;
        overlay.texture = GetStormTexture();
        overlay.color = baseColor;
    }

    private static Texture2D GetStormTexture()
    {
        if (stormTexture != null)
            return stormTexture;

        const int size = 128;

        stormTexture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false,
            true)
        {
            name = "MapStormNoise",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float broadNoise = Mathf.PerlinNoise(
                    x * 0.075f,
                    y * 0.075f);
                float fineNoise = Mathf.PerlinNoise(
                    x * 0.31f + 17.3f,
                    y * 0.31f + 8.1f);
                float grain = Mathf.Lerp(
                    broadNoise,
                    fineNoise,
                    0.38f);
                float alpha = Mathf.Lerp(0.42f, 0.9f, grain);

                pixels[y * size + x] = new Color(
                    0.8f + grain * 0.2f,
                    0.74f + grain * 0.26f,
                    0.62f + grain * 0.38f,
                    alpha);
            }
        }

        stormTexture.SetPixels(pixels);
        stormTexture.Apply(false, true);

        return stormTexture;
    }

    private void Update()
    {
        windOffset += Time.deltaTime * 0.012f;
        overlay.uvRect = new Rect(
            windOffset,
            windOffset * 0.27f,
            3f,
            2f);
    }

    private void StartPulse(
        float intensity,
        float fadeIn,
        float hold,
        float fadeOut)
    {
        StopAllCoroutines();
        StartCoroutine(PulseRoutine(
            intensity,
            fadeIn,
            hold,
            fadeOut
        ));
    }

    private IEnumerator PulseRoutine(
        float intensity,
        float fadeIn,
        float hold,
        float fadeOut)
    {
        Color target = baseColor;
        target.a = intensity;

        float elapsed = 0f;

        while (elapsed < fadeIn)
        {
            elapsed += Time.deltaTime;

            overlay.color = Color.Lerp(
                baseColor,
                target,
                elapsed / fadeIn);

            yield return null;
        }

        overlay.color = target;

        yield return new WaitForSeconds(hold);

        elapsed = 0f;

        while (elapsed < fadeOut)
        {
            elapsed += Time.deltaTime;

            overlay.color = Color.Lerp(
                target,
                baseColor,
                elapsed / fadeOut);

            yield return null;
        }

        overlay.color = baseColor;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
}
