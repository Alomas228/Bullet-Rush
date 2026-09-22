using UnityEngine;

/// <summary>
/// Парящая метка (стрелка) над врагом во время обучения.
/// Вешается на врага рантаймом через Attach и уничтожается вместе с ним.
/// </summary>
public class TutorialEnemyMarker : MonoBehaviour
{
    private static Sprite fallbackSprite;

    [SerializeField] private Vector3 offset =
        new Vector3(0f, 2.4f, 0f);
    [SerializeField] private float bobSpeed = 3.5f;
    [SerializeField] private float bobHeight = 0.3f;

    private SpriteRenderer visual;
    private Camera cachedCamera;
    private float phase;

    public static TutorialEnemyMarker Attach(
        Enemy enemy,
        Sprite sprite,
        Vector3 markerOffset,
        float markerBobSpeed,
        float markerBobHeight)
    {
        if (enemy == null)
            return null;

        TutorialEnemyMarker marker =
            enemy.GetComponent<TutorialEnemyMarker>();

        if (marker == null)
            marker =
                enemy.gameObject.AddComponent<TutorialEnemyMarker>();

        marker.Initialize(
            sprite,
            markerOffset,
            markerBobSpeed,
            markerBobHeight
        );

        return marker;
    }

    private void Initialize(
        Sprite sprite,
        Vector3 markerOffset,
        float markerBobSpeed,
        float markerBobHeight)
    {
        offset = markerOffset;
        bobSpeed = markerBobSpeed;
        bobHeight = markerBobHeight;
        phase = Random.Range(0f, Mathf.PI * 2f);
        cachedCamera = Camera.main;

        if (visual != null)
            return;

        GameObject visualObject = new GameObject(
            "TutorialEnemyMarker",
            typeof(SpriteRenderer)
        );

        visualObject.transform.SetParent(transform, false);
        visualObject.transform.localPosition = offset;

        visual =
            visualObject.GetComponent<SpriteRenderer>();

        visual.sprite =
            sprite != null
                ? sprite
                : GetFallbackSprite();

        visual.sortingOrder = 300;
    }

    private void LateUpdate()
    {
        if (visual == null)
            return;

        if (cachedCamera == null)
            cachedCamera = Camera.main;

        float bob =
            Mathf.Sin(Time.time * bobSpeed + phase) *
            bobHeight;

        visual.transform.localPosition =
            offset + Vector3.up * bob;

        float pulse =
            1f +
            Mathf.Sin(Time.time * bobSpeed * 2f + phase) *
            0.08f;

        visual.transform.localScale =
            new Vector3(pulse, pulse, 1f);

        // Метка всегда лежит в плоскости камеры — не зависит
        // от угла обзора и не «поворачивается» боком.
        if (cachedCamera != null)
        {
            visual.transform.rotation =
                cachedCamera.transform.rotation;
        }
    }

    // =========================================================
    // FALLBACK SPRITE (простая жёлтая стрелка вниз)
    // =========================================================

    private static Sprite GetFallbackSprite()
    {
        if (fallbackSprite != null)
            return fallbackSprite;

        const int size = 64;

        Texture2D texture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false
        );

        texture.filterMode = FilterMode.Bilinear;

        Color fill = new Color(1f, 0.85f, 0.1f, 1f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float fx = x / (float)(size - 1);
                float fy = y / (float)(size - 1);

                bool inside = false;

                // Стебель (верх стрелки).
                if (fx >= 0.38f && fx <= 0.62f &&
                    fy >= 0.5f && fy <= 0.92f)
                {
                    inside = true;
                }

                // Острый наконечник вниз.
                if (fy <= 0.55f && fy >= 0.08f)
                {
                    float halfWidth =
                        Mathf.Lerp(
                            0.42f,
                            0f,
                            (0.55f - fy) /
                            (0.55f - 0.08f)
                        );

                    if (Mathf.Abs(fx - 0.5f) <= halfWidth)
                        inside = true;
                }

                texture.SetPixel(
                    x,
                    y,
                    inside ? fill : Color.clear
                );
            }
        }

        texture.Apply();

        fallbackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f
        );

        return fallbackSprite;
    }
}
