using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class ArcadeHoverLift : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerDownHandler,
    IPointerUpHandler
{
    private Image image;
    private Color baseColor;
    private Color lastApplied;
    private Vector3 baseScale = Vector3.one;
    private float hoverScale = 1.018f;
    private float brighten = 0.08f;
    private float targetScale = 1f;
    private bool hovered;
    private bool pressed;

    public void Bind(Image target, float scale, float lift)
    {
        image = target;
        hoverScale = scale;
        brighten = lift;

        if (image != null)
        {
            baseColor = image.color;
            lastApplied = baseColor;
        }
    }

    private void Awake()
    {
        baseScale = transform.localScale;

        if (baseScale == Vector3.zero)
            baseScale = Vector3.one;

        if (image != null)
        {
            baseColor = image.color;
            lastApplied = baseColor;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        pressed = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pressed = true;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        pressed = false;
    }

    private void Update()
    {
        if (image == null)
            return;

        float requested = hovered ? hoverScale : 1f;

        if (pressed)
            requested *= 0.985f;

        targetScale = Mathf.Lerp(targetScale, requested, 14f * Time.unscaledDeltaTime);
        transform.localScale = baseScale * targetScale;

        if (image.color != lastApplied)
            baseColor = image.color;

        Color requestedColor = hovered
            ? ArcadeUiTheme.Bright(baseColor, brighten)
            : baseColor;

        image.color = Color.Lerp(
            image.color,
            requestedColor,
            16f * Time.unscaledDeltaTime
        );

        lastApplied = image.color;
    }

    private void OnDisable()
    {
        transform.localScale = baseScale;
        targetScale = 1f;

        if (image != null)
        {
            image.color = baseColor;
            lastApplied = baseColor;
        }
    }
}

public sealed class ArcadePulse : MonoBehaviour
{
    private Graphic graphic;
    private float minAlpha;
    private float maxAlpha;
    private float speed;
    private float phase;

    public void Bind(Graphic target, float minimum, float maximum, float rate)
    {
        graphic = target;
        minAlpha = minimum;
        maxAlpha = maximum;
        speed = rate;
        phase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (graphic == null)
            return;

        Color color = graphic.color;
        float wave = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * speed + phase);
        color.a = Mathf.Lerp(minAlpha, maxAlpha, wave);
        graphic.color = color;
    }
}

public sealed class ArcadeDriftLayer : MonoBehaviour
{
    private RectTransform[] items;
    private Vector2[] positions;
    private float[] speeds;
    private float[] phases;
    private float[] alphas;
    private float height;
    private float width;

    public void Configure(
        RectTransform showArea,
        int count,
        Color tint,
        float minSize,
        float maxSize)
    {
        width = showArea.rect.width;
        height = showArea.rect.height;

        items = new RectTransform[count];
        positions = new Vector2[count];
        speeds = new float[count];
        phases = new float[count];
        alphas = new float[count];

        for (int i = 0; i < count; i++)
        {
            RectTransform item = ArcadeUiFactory.At(
                transform,
                "Dust_" + i,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                Vector2.one * Random.Range(minSize, maxSize)
            );

            float alpha = Random.Range(0.05f, 0.2f);
            Image image = ArcadeUiFactory.Image(
                item,
                ArcadeKitAssets.Spark,
                ArcadeUiTheme.Alpha(tint, alpha)
            );

            item.localEulerAngles = new Vector3(0f, 0f, Random.Range(0f, 360f));

            items[i] = item;
            positions[i] = new Vector2(
                Random.Range(-width * 0.5f, width * 0.5f),
                Random.Range(-height * 0.5f, height * 0.5f)
            );
            speeds[i] = Random.Range(7f, 22f);
            phases[i] = Random.Range(0f, Mathf.PI * 2f);
            alphas[i] = alpha;

            item.anchoredPosition = positions[i];
            image.raycastTarget = false;
        }
    }

    private void Update()
    {
        if (items == null)
            return;

        float dt = Time.unscaledDeltaTime;

        for (int i = 0; i < items.Length; i++)
        {
            if (items[i] == null)
                continue;

            positions[i].y += speeds[i] * dt;

            if (positions[i].y > height * 0.5f + 30f)
                positions[i].y = -height * 0.5f - 30f;

            float x = positions[i].x + Mathf.Sin(
                Time.unscaledTime * 0.7f + phases[i]
            ) * 8f;

            items[i].anchoredPosition = new Vector2(x, positions[i].y);

            Graphic graphic = items[i].GetComponent<Graphic>();

            if (graphic != null)
            {
                float edge = Mathf.InverseLerp(
                    -height * 0.5f,
                    height * 0.5f,
                    positions[i].y
                );

                Color color = graphic.color;
                color.a = alphas[i] * Mathf.Sin(edge * Mathf.PI);
                graphic.color = color;
            }
        }
    }
}
