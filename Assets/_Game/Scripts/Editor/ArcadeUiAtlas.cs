#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

public static class ArcadeUiAtlas
{
    public const string Folder = "Assets/_Game/UI/Arcade";
    public const string AtlasPath = Folder + "/ArcadeUiAtlas.png";
    public const string BackdropPath = Folder + "/ArcadeBackdrop.png";

    private const int AtlasWidth = 512;
    private const int AtlasHeight = 1024;
    private const int BackdropWidth = 512;
    private const int BackdropHeight = 288;

    private static Dictionary<string, Sprite> sprites;
    private static Sprite backdrop;

    public static Sprite Panel => Get("panel");
    public static Sprite Surface => Get("surface");
    public static Sprite Frame => Get("frame");
    public static Sprite Primary => Get("primary");
    public static Sprite Secondary => Get("secondary");
    public static Sprite Pill => Get("pill");
    public static Sprite Toggle => Get("toggle");
    public static Sprite Track => Get("track");
    public static Sprite Bar => Get("bar");
    public static Sprite Circle => Get("circle");
    public static Sprite Glow => Get("glow");
    public static Sprite Coin => Get("coin");
    public static Sprite Arrow => Get("arrow");
    public static Sprite Star => Get("star");
    public static Sprite Diamond => Get("diamond");
    public static Sprite Play => Get("play");
    public static Sprite Check => Get("check");
    public static Sprite Cross => Get("cross");
    public static Sprite Hex => Get("hex");

    public static Sprite Backdrop
    {
        get
        {
            if (backdrop == null)
                backdrop = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);

            return backdrop;
        }
    }

    public static void Ensure()
    {
        Directory.CreateDirectory(Folder);

        BuildAtlas();
        BuildBackdrop();

        AssetDatabase.Refresh();
        LoadSprites();
    }

    [MenuItem("Tools/ArcadeSurvivor/Assets/Rebuild UI Atlas", false, 30)]
    public static void Rebuild()
    {
        Ensure();

        EditorUtility.DisplayDialog(
            "Arcade Survivor UI",
            "UI atlas and backdrop rebuilt.",
            "OK"
        );
    }

    private static void LoadSprites()
    {
        sprites = new Dictionary<string, Sprite>();

        Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AtlasPath);

        for (int i = 0; i < assets.Length; i++)
        {
            if (assets[i] is Sprite sprite)
                sprites[sprite.name] = sprite;
        }

        backdrop = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);
    }

    private static Sprite Get(string name)
    {
        if (sprites == null)
            LoadSprites();

        if (sprites != null &&
            sprites.TryGetValue(name, out Sprite sprite) &&
            sprite != null)
        {
            return sprite;
        }

        throw new System.InvalidOperationException(
            "Arcade UI atlas is missing sprite '" + name +
            "'. Run Tools/ArcadeSurvivor/Assets/Rebuild UI Atlas."
        );
    }

    private static void BuildAtlas()
    {
        Color32[] pixels = new Color32[AtlasWidth * AtlasHeight];

        RectInt panel = R(4, 760, 256, 256);
        RectInt surface = R(264, 856, 160, 160);
        RectInt frame = R(264, 692, 160, 160);
        RectInt primary = R(4, 680, 160, 64);
        RectInt secondary = R(4, 612, 160, 64);
        RectInt pill = R(4, 552, 160, 48);
        RectInt toggle = R(4, 508, 64, 32);
        RectInt track = R(172, 608, 64, 20);
        RectInt bar = R(172, 580, 64, 20);
        RectInt circle = R(172, 508, 64, 64);
        RectInt glow = R(4, 436, 64, 64);
        RectInt coin = R(76, 436, 64, 64);
        RectInt arrow = R(148, 436, 64, 64);
        RectInt star = R(220, 436, 64, 64);
        RectInt diamond = R(292, 436, 64, 64);
        RectInt play = R(364, 436, 64, 64);
        RectInt check = R(4, 364, 64, 64);
        RectInt cross = R(76, 364, 64, 64);
        RectInt hex = R(148, 364, 64, 64);

        FillRounded(pixels, panel, 56f,
            C(0.92f), C(0.58f));
        FillRounded(pixels, surface, 42f,
            C(0.95f), C(0.72f));
        FillRounded(pixels, primary, 28f,
            C(1f), C(0.70f));
        FillRounded(pixels, secondary, 28f,
            C(0.90f), C(0.64f));
        FillRounded(pixels, pill, 23f,
            C(0.98f), C(0.72f));
        FillRounded(pixels, toggle, 15f,
            C(0.95f), C(0.66f));
        FillRounded(pixels, track, 9f,
            C(0.55f), C(0.32f));
        FillRounded(pixels, bar, 9f,
            C(1f), C(0.78f));

        DrawRoundedRing(pixels, primary, 28f, 1.4f,
            new Color(1f, 1f, 1f, 0.55f));
        DrawRoundedRing(pixels, secondary, 28f, 1.2f,
            new Color(1f, 1f, 1f, 0.28f));
        DrawRoundedRing(pixels, panel, 56f, 1.2f,
            new Color(1f, 1f, 1f, 0.10f));
        DrawRoundedRing(pixels, surface, 42f, 1.2f,
            new Color(1f, 1f, 1f, 0.20f));
        DrawRoundedRing(pixels, frame, 42f, 2.4f, Color.white);

        FillRadial(pixels, circle, new Color(1f, 1f, 1f, 1f), 0.84f);
        FillRadial(pixels, glow, new Color(1f, 1f, 1f, 0.95f), 2.4f);

        FillPolygon(pixels, arrow, new Vector2[]
        {
            new Vector2(12f, 42f),
            new Vector2(52f, 42f),
            new Vector2(32f, 18f)
        }, Color.white);

        FillPolygon(pixels, star, StarPoints(32f, 32f, 28f, 12f), Color.white);
        FillPolygon(pixels, diamond, new Vector2[]
        {
            new Vector2(32f, 8f),
            new Vector2(56f, 32f),
            new Vector2(32f, 56f),
            new Vector2(8f, 32f)
        }, Color.white);

        FillPolygon(pixels, play, new Vector2[]
        {
            new Vector2(22f, 12f),
            new Vector2(52f, 32f),
            new Vector2(22f, 52f)
        }, Color.white);

        FillPolygon(pixels, hex, HexPoints(32f, 32f, 27f), Color.white);

        DrawLine(pixels, check, new Vector2(14f, 34f), new Vector2(27f, 47f), 7f, Color.white);
        DrawLine(pixels, check, new Vector2(27f, 47f), new Vector2(50f, 18f), 7f, Color.white);
        DrawLine(pixels, cross, new Vector2(18f, 18f), new Vector2(46f, 46f), 7f, Color.white);
        DrawLine(pixels, cross, new Vector2(46f, 18f), new Vector2(18f, 46f), 7f, Color.white);

        DrawRoundedRing(pixels, coin, 26f, 5f, Color.white);
        FillPolygon(pixels, coin, StarPoints(32f, 32f, 11f, 4.8f), Color.white);

        WriteTexture(AtlasPath, AtlasWidth, AtlasHeight, pixels);
        ImportAtlas(new SpriteMetaData[]
        {
            Meta("panel", panel, 56f),
            Meta("surface", surface, 42f),
            Meta("frame", frame, 42f),
            Meta("primary", primary, 28f),
            Meta("secondary", secondary, 28f),
            Meta("pill", pill, 22f),
            Meta("toggle", toggle, 14f),
            Meta("track", track, 9f),
            Meta("bar", bar, 9f),
            Meta("circle", circle, 0f),
            Meta("glow", glow, 0f),
            Meta("coin", coin, 0f),
            Meta("arrow", arrow, 0f),
            Meta("star", star, 0f),
            Meta("diamond", diamond, 0f),
            Meta("play", play, 0f),
            Meta("check", check, 0f),
            Meta("cross", cross, 0f),
            Meta("hex", hex, 0f)
        });
    }

    private static void BuildBackdrop()
    {
        Color32[] pixels = new Color32[BackdropWidth * BackdropHeight];

        Color bottom = new Color(0.006f, 0.012f, 0.032f, 1f);
        Color top = new Color(0.030f, 0.070f, 0.160f, 1f);
        Color cyan = new Color(0.25f, 0.90f, 1f, 1f);
        Color magenta = new Color(1f, 0.24f, 0.54f, 1f);
        Color purple = new Color(0.40f, 0.32f, 0.95f, 1f);
        Color grid = new Color(0.20f, 0.45f, 0.62f, 1f);
        Color star = new Color(0.75f, 0.90f, 1f, 1f);

        for (int y = 0; y < BackdropHeight; y++)
        {
            float v = y / (BackdropHeight - 1f);

            for (int x = 0; x < BackdropWidth; x++)
            {
                float u = x / (BackdropWidth - 1f);

                Color color = Color.Lerp(
                    bottom,
                    top,
                    Mathf.SmoothStep(0f, 1f, v)
                );

                float cyanGlow = Gaussian(u, v, 0.04f, 1.02f, 0.36f, 0.46f);
                float magentaGlow = Gaussian(u, v, 0.98f, 0.02f, 0.48f, 0.52f);
                float purpleGlow = Gaussian(u, v, 0.56f, 0.48f, 0.78f, 0.58f);

                color += cyan * (cyanGlow * 0.24f);
                color += magenta * (magentaGlow * 0.20f);
                color += purple * (purpleGlow * 0.06f);

                float verticalLines = SmoothLine(u * 36f, 0.012f);
                float horizontalLines = SmoothLine(v * 18f, 0.010f);
                color += grid * ((verticalLines + horizontalLines) * 0.045f);

                float beamCoordinate = u * 0.62f + v * 0.78f;
                color += cyan * (SmoothLine(beamCoordinate * 5f, 0.035f) * 0.04f);

                float dx = u - 0.5f;
                float dy = (v - 0.5f) * 0.82f;
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                float vignette = Mathf.SmoothStep(0.34f, 0.92f, distance);
                color *= Mathf.Lerp(1f, 0.48f, vignette);

                if (v > 0.24f && Hash(x, y) > 0.99965f)
                    color += star * 0.40f;

                pixels[y * BackdropWidth + x] = ToColor32(color);
            }
        }

        WriteTexture(BackdropPath, BackdropWidth, BackdropHeight, pixels);
        ImportBackdrop();
    }

    private static void ImportAtlas(SpriteMetaData[] metadata)
    {
        AssetDatabase.ImportAsset(
            AtlasPath,
            ImportAssetOptions.ForceUpdate
        );

        TextureImporter importer =
            AssetImporter.GetAtPath(AtlasPath) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.sRGBTexture = true;

        TextureImporterSettings settings = new TextureImporterSettings();

        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        SpriteDataProviderFactories factories = new SpriteDataProviderFactories();
        factories.Init();

        ISpriteEditorDataProvider provider =
            factories.GetSpriteEditorDataProviderFromObject(importer);

        if (provider == null)
        {
            Debug.LogError(
                "Arcade UI atlas: sprite data provider is unavailable."
            );
            return;
        }

        provider.InitSpriteEditorDataProvider();

        SpriteRect[] previous = provider.GetSpriteRects();
        Dictionary<string, GUID> previousIds =
            new Dictionary<string, GUID>();

        if (previous != null)
        {
            for (int i = 0; i < previous.Length; i++)
            {
                if (previous[i] != null &&
                    !string.IsNullOrEmpty(previous[i].name))
                {
                    previousIds[previous[i].name] = previous[i].spriteID;
                }
            }
        }

        SpriteRect[] rects = new SpriteRect[metadata.Length];

        for (int i = 0; i < metadata.Length; i++)
        {
            SpriteMetaData meta = metadata[i];

            rects[i] = new SpriteRect
            {
                name = meta.name,
                rect = meta.rect,
                alignment = (SpriteAlignment)meta.alignment,
                pivot = meta.pivot,
                border = meta.border,
                spriteID = previousIds.TryGetValue(
                    meta.name,
                    out GUID spriteId)
                    ? spriteId
                    : GUID.Generate()
            };
        }

        provider.SetSpriteRects(rects);
        provider.Apply();
        AssetDatabase.ImportAsset(
            AtlasPath,
            ImportAssetOptions.ForceUpdate
        );
    }

    private static void ImportBackdrop()
    {
        AssetDatabase.ImportAsset(
            BackdropPath,
            ImportAssetOptions.ForceUpdate
        );

        TextureImporter importer =
            AssetImporter.GetAtPath(BackdropPath) as TextureImporter;

        if (importer == null)
            return;

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 1024;
        importer.sRGBTexture = true;

        TextureImporterSettings settings = new TextureImporterSettings();

        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
    }

    private static void WriteTexture(
        string path,
        int width,
        int height,
        Color32[] pixels)
    {
        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false
        );

        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(path, texture.EncodeToPNG());

        Object.DestroyImmediate(texture);
    }

    private static SpriteMetaData Meta(
        string name,
        RectInt rect,
        float border)
    {
        return new SpriteMetaData
        {
            name = name,
            rect = new Rect(rect.x, rect.y, rect.width, rect.height),
            alignment = (int)SpriteAlignment.Center,
            pivot = new Vector2(0.5f, 0.5f),
            border = new Vector4(border, border, border, border)
        };
    }

    private static RectInt R(int x, int y, int width, int height)
    {
        return new RectInt(x, y, width, height);
    }

    private static Color C(float value)
    {
        return new Color(value, value, value, 1f);
    }

    private static void FillRounded(
        Color32[] pixels,
        RectInt rect,
        float radius,
        Color top,
        Color bottom)
    {
        float width = rect.width;
        float height = rect.height;

        for (int y = rect.yMin - 1; y <= rect.yMax; y++)
        {
            for (int x = rect.xMin - 1; x <= rect.xMax; x++)
            {
                float coverage = RoundedCoverage(
                    x + 0.5f - rect.x,
                    y + 0.5f - rect.y,
                    width,
                    height,
                    radius
                );

                if (coverage <= 0f)
                    continue;

                float time = Mathf.Clamp01(
                    (y + 0.5f - rect.y) / height
                );

                Color color = Color.Lerp(bottom, top, time);

                color.a *= coverage;
                Blend(pixels, AtlasWidth, x, y, color);
            }
        }
    }

    private static void DrawRoundedRing(
        Color32[] pixels,
        RectInt rect,
        float radius,
        float thickness,
        Color color)
    {
        float width = rect.width;
        float height = rect.height;

        for (int y = rect.yMin - 1; y <= rect.yMax; y++)
        {
            for (int x = rect.xMin - 1; x <= rect.xMax; x++)
            {
                float signedDistance = RoundedSdf(
                    x + 0.5f - rect.x,
                    y + 0.5f - rect.y,
                    width,
                    height,
                    radius
                );

                float outer = Mathf.Clamp01(0.5f - signedDistance);
                float inner = Mathf.Clamp01(
                    0.5f - (signedDistance + thickness)
                );
                float coverage = Mathf.Max(0f, outer - inner);

                if (coverage <= 0f)
                    continue;

                Color tinted = color;
                tinted.a *= coverage;
                Blend(pixels, AtlasWidth, x, y, tinted);
            }
        }
    }

    private static void FillRadial(
        Color32[] pixels,
        RectInt rect,
        Color color,
        float falloff)
    {
        float halfWidth = rect.width * 0.5f;
        float halfHeight = rect.height * 0.5f;
        float radius = Mathf.Min(halfWidth, halfHeight);

        for (int y = rect.yMin; y < rect.yMax; y++)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                float dx = x + 0.5f - rect.x - halfWidth;
                float dy = y + 0.5f - rect.y - halfHeight;
                float distance = Mathf.Sqrt(dx * dx + dy * dy) / radius;

                if (distance >= 1f)
                    continue;

                float alpha = Mathf.Pow(1f - distance, falloff);
                Color tinted = color;
                tinted.a *= alpha;
                Blend(pixels, AtlasWidth, x, y, tinted);
            }
        }
    }

    private static void FillPolygon(
        Color32[] pixels,
        RectInt rect,
        Vector2[] points,
        Color color)
    {
        for (int y = rect.yMin; y < rect.yMax; y++)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                int hits = 0;

                for (int sy = 0; sy < 2; sy++)
                {
                    for (int sx = 0; sx < 2; sx++)
                    {
                        Vector2 point = new Vector2(
                            x + (sx + 0.5f) * 0.5f - rect.x,
                            y + (sy + 0.5f) * 0.5f - rect.y
                        );

                        if (InsidePolygon(point, points))
                            hits++;
                    }
                }

                if (hits == 0)
                    continue;

                Color tinted = color;
                tinted.a *= hits / 4f;
                Blend(pixels, AtlasWidth, x, y, tinted);
            }
        }
    }

    private static void DrawLine(
        Color32[] pixels,
        RectInt rect,
        Vector2 start,
        Vector2 end,
        float thickness,
        Color color)
    {
        for (int y = rect.yMin; y < rect.yMax; y++)
        {
            for (int x = rect.xMin; x < rect.xMax; x++)
            {
                Vector2 point = new Vector2(
                    x + 0.5f - rect.x,
                    y + 0.5f - rect.y
                );

                float coverage = Mathf.Clamp01(
                    thickness * 0.5f + 0.5f -
                    DistanceToSegment(point, start, end)
                );

                if (coverage <= 0f)
                    continue;

                Color tinted = color;
                tinted.a *= coverage;
                Blend(pixels, AtlasWidth, x, y, tinted);
            }
        }
    }

    private static float RoundedCoverage(
        float x,
        float y,
        float width,
        float height,
        float radius)
    {
        return Mathf.Clamp01(
            0.5f - RoundedSdf(x, y, width, height, radius)
        );
    }

    private static float RoundedSdf(
        float x,
        float y,
        float width,
        float height,
        float radius)
    {
        float bx = Mathf.Abs(x - width * 0.5f) - (width * 0.5f - radius);
        float by = Mathf.Abs(y - height * 0.5f) - (height * 0.5f - radius);
        float qx = Mathf.Max(bx, 0f);
        float qy = Mathf.Max(by, 0f);

        return Mathf.Sqrt(qx * qx + qy * qy) +
               Mathf.Min(Mathf.Max(bx, by), 0f) -
               radius;
    }

    private static bool InsidePolygon(Vector2 point, Vector2[] polygon)
    {
        bool inside = false;

        for (int i = 0, j = polygon.Length - 1;
             i < polygon.Length;
             j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];

            if ((a.y > point.y) != (b.y > point.y) &&
                point.x < (b.x - a.x) * (point.y - a.y) /
                (b.y - a.y) + a.x)
            {
                inside = !inside;
            }
        }

        return inside;
    }

    private static float DistanceToSegment(
        Vector2 point,
        Vector2 start,
        Vector2 end)
    {
        Vector2 segment = end - start;
        float lengthSquared = segment.sqrMagnitude;

        if (lengthSquared <= 0.0001f)
            return Vector2.Distance(point, start);

        float t = Mathf.Clamp01(
            Vector2.Dot(point - start, segment) / lengthSquared
        );

        return Vector2.Distance(point, start + segment * t);
    }

    private static Vector2[] StarPoints(
        float centerX,
        float centerY,
        float outerRadius,
        float innerRadius)
    {
        Vector2[] points = new Vector2[10];

        for (int i = 0; i < points.Length; i++)
        {
            float radius = i % 2 == 0 ? outerRadius : innerRadius;
            float angle = Mathf.Deg2Rad * (90f + i * 36f);

            points[i] = new Vector2(
                centerX + Mathf.Cos(angle) * radius,
                centerY + Mathf.Sin(angle) * radius
            );
        }

        return points;
    }

    private static Vector2[] HexPoints(
        float centerX,
        float centerY,
        float radius)
    {
        Vector2[] points = new Vector2[6];

        for (int i = 0; i < points.Length; i++)
        {
            float angle = Mathf.Deg2Rad * (60f * i);

            points[i] = new Vector2(
                centerX + Mathf.Cos(angle) * radius,
                centerY + Mathf.Sin(angle) * radius
            );
        }

        return points;
    }

    private static float Gaussian(
        float x,
        float y,
        float centerX,
        float centerY,
        float sigmaX,
        float sigmaY)
    {
        float dx = (x - centerX) / sigmaX;
        float dy = (y - centerY) / sigmaY;

        return Mathf.Exp(-(dx * dx + dy * dy) * 0.5f);
    }

    private static float SmoothLine(float coordinate, float width)
    {
        float fraction = Mathf.Repeat(coordinate, 1f);
        float distance = Mathf.Abs(fraction - 0.5f);

        return Mathf.Clamp01(1f - distance / width);
    }

    private static float Hash(int x, int y)
    {
        uint value = (uint)(x * 374761393 + y * 668265263);

        value = (value ^ (value >> 13)) * 1274126177;
        value ^= value >> 16;

        return (value & 0xFFFFFF) / 16777215f;
    }

    private static void Blend(
        Color32[] pixels,
        int width,
        int x,
        int y,
        Color color)
    {
        if (color.a <= 0f)
            return;

        int index = y * width + x;

        if (index < 0 || index >= pixels.Length)
            return;

        Color32 destination = pixels[index];
        float sourceAlpha = Mathf.Clamp01(color.a);
        float destinationAlpha = destination.a / 255f;
        float inverse = destinationAlpha * (1f - sourceAlpha);
        float alpha = sourceAlpha + inverse;

        if (alpha <= 0.0001f)
        {
            pixels[index] = new Color32(0, 0, 0, 0);
            return;
        }

        float red = (
            color.r * sourceAlpha +
            destination.r / 255f * inverse
        ) / alpha;

        float green = (
            color.g * sourceAlpha +
            destination.g / 255f * inverse
        ) / alpha;

        float blue = (
            color.b * sourceAlpha +
            destination.b / 255f * inverse
        ) / alpha;

        pixels[index] = new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(red * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(green * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(blue * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255)
        );
    }

    private static Color32 ToColor32(Color color)
    {
        return new Color32(
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.r * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.g * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.b * 255f), 0, 255),
            (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255)
        );
    }
}

#endif
