using System.Collections.Generic;
using TMPro;
using UnityEngine;

public static class ArcadeKitAssets
{
    private const string Root = "ArcadeKit/";

    private static readonly Dictionary<string, Sprite> Sprites =
        new Dictionary<string, Sprite>();

    public static Sprite Panel => Sliced("panel", 72f, 72f, 72f, 72f);
    public static Sprite Card => Sliced("card", 60f, 60f, 60f, 60f);
    public static Sprite CardSoft => Sliced("card_soft", 60f, 60f, 60f, 60f);
    public static Sprite Chip => Sliced("chip", 48f, 44f, 48f, 44f);
    // Бордер должен покрывать весь скруглённый край текстуры
    // (pill 320x96 -> радиус 48, tag 192x64 -> радиус 32). Иначе
    // кривизна попадает в растягиваемую зону и форма плывёт.
    public static Sprite Pill => Sliced("pill", 48f, 48f, 48f, 48f);
    public static Sprite Tag => Sliced("tag", 32f, 32f, 32f, 32f);
    public static Sprite ButtonOrange => Sliced("button_orange", 100f, 56f, 100f, 56f);
    public static Sprite ButtonDark => Sliced("button_dark", 76f, 48f, 76f, 48f);
    public static Sprite ButtonGhost => Sliced("button_ghost", 64f, 40f, 64f, 40f);
    public static Sprite IconTile => Sliced("icon_tile", 48f, 48f, 48f, 48f);
    public static Sprite HexFill => Simple("hex_fill");
    public static Sprite HexRing => Simple("hex_ring");
    public static Sprite BrushDark => Simple("brush_dark");
    public static Sprite BrushOrange => Simple("brush_orange");
    public static Sprite Glow => Simple("glow");
    public static Sprite Vignette => Simple("vignette");
    public static Sprite Spark => Simple("spark");
    public static Sprite Coin => Simple("coin");
    public static Sprite Chest => Simple("chest");
    public static Sprite Knob => Simple("knob");
    // Квадратные полосы с небольшим скруглением вместо овальных pill-спрайтов.
    public static Sprite BarTrack => RoundRect;
    public static Sprite BarFill => RoundRect;
    public static Sprite SliderHandle => RoundRect;
    public static Sprite CardGrid => Simple("card_grid");

    private static Sprite roundRect;

    /// <summary>
    /// Прямоугольник с небольшим фиксированным скруглением углов для UI-полос.
    /// </summary>
    public static Sprite RoundRect
    {
        get
        {
            if (roundRect != null)
                return roundRect;

            const int size = 128;
            const int radius = 6;
            const int samples = 4;

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coverage = 0f;

                    for (int sy = 0; sy < samples; sy++)
                    {
                        for (int sx = 0; sx < samples; sx++)
                        {
                            float px = x + (sx + 0.5f) / samples;
                            float py = y + (sy + 0.5f) / samples;

                            if (InsideRoundRect(px, py, size, radius))
                                coverage += 1f / (samples * samples);
                        }
                    }

                    pixels[y * size + x] = new Color(1f, 1f, 1f, coverage);
                }
            }

            Texture2D texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false
            );
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels(pixels);
            texture.Apply();

            roundRect = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius)
            );
            roundRect.name = "round_rect";

            return roundRect;
        }
    }

    private static bool InsideRoundRect(
        float x,
        float y,
        float size,
        float radius)
    {
        float cx = Mathf.Clamp(x, radius, size - radius);
        float cy = Mathf.Clamp(y, radius, size - radius);
        float dx = x - cx;
        float dy = y - cy;

        return dx * dx + dy * dy <= radius * radius;
    }

    public static TMP_FontAsset Display
    {
        get
        {
            ArcadePreviewAssets.Ensure();
            return ArcadePreviewAssets.Display;
        }
    }

    public static TMP_FontAsset Body
    {
        get
        {
            ArcadePreviewAssets.Ensure();
            return ArcadePreviewAssets.Body;
        }
    }

    public static TMP_FontAsset BodyBold
    {
        get
        {
            ArcadePreviewAssets.Ensure();
            return ArcadePreviewAssets.BodyBold;
        }
    }

    public static Sprite Icon(string name)
    {
        ArcadePreviewAssets.Ensure();
        return ArcadePreviewAssets.Icon(name);
    }

    private static Sprite Sliced(
        string name,
        float left,
        float bottom,
        float right,
        float top)
    {
        if (Sprites.TryGetValue(name, out Sprite cached))
            return cached;

        Texture2D texture = Resources.Load<Texture2D>(Root + name);

        if (texture == null)
        {
            Debug.LogWarning("ArcadeKit sprite is missing: " + name);
            return null;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(left, bottom, right, top)
        );

        sprite.name = name;
        Sprites[name] = sprite;
        return sprite;
    }

    private static Sprite Simple(string name)
    {
        if (Sprites.TryGetValue(name, out Sprite cached))
            return cached;

        Texture2D texture = Resources.Load<Texture2D>(Root + name);

        if (texture == null)
        {
            Debug.LogWarning("ArcadeKit sprite is missing: " + name);
            return null;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );

        sprite.name = name;
        Sprites[name] = sprite;
        return sprite;
    }
}
