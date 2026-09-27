#if UNITY_EDITOR

using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Заглушки-спрайты для макетов «Улучшения» и «Снаряжение».
///
/// Это нейтральные placeholder-формы (скруглённый прямоугольник
/// 9-slice, круг, мягкое пятно), чтобы wireframe выглядел как
/// настоящий игровой интерфейс, а не как набор серых прямоугольников.
/// В Figma их заменяют на реальные формы без изменения кода.
///
/// Файлы кладутся в Assets/_Game/UI/Placeholder и переиспользуются:
/// повторный запуск билдера их не перезаписывает.
/// </summary>
public static class WireframePlaceholderSprites
{
    public const string Folder = "Assets/_Game/UI/Placeholder";

    private const string CardName = "RoundedCard";
    private const string BarName = "RoundedBar";
    private const string CircleName = "Circle";
    private const string BlobName = "SoftBlob";

    public static Sprite CardSprite => Load(CardName, 96, 22f, 24, false);
    public static Sprite BarSprite => Load(BarName, 24, 8f, 8, false);
    public static Sprite CircleSprite => Load(CircleName, 32, 16f, 16, false);
    public static Sprite BlobSprite => Load(BlobName, 128, 0f, 0, true);

    public static void EnsureAll()
    {
        _ = CardSprite;
        _ = BarSprite;
        _ = CircleSprite;
        _ = BlobSprite;

        AssetDatabase.Refresh();
    }

    private static Sprite Load(
        string name,
        int size,
        float radius,
        int border,
        bool radial)
    {
        Directory.CreateDirectory(Folder);

        string path = $"{Folder}/{name}.png";

        Sprite existing =
            AssetDatabase.LoadAssetAtPath<Sprite>(path);

        if (existing != null)
            return existing;

        var texture =
            new Texture2D(size, size, TextureFormat.RGBA32, false);

        var pixels = new Color32[size * size];

        float half = size * 0.5f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var point = new Vector2(x + 0.5f, y + 0.5f);

                float alpha = radial
                    ? RadialAlpha(point, half)
                    : RoundedAlpha(point, half, radius);

                pixels[y * size + x] = new Color32(
                    255,
                    255,
                    255,
                    (byte)Mathf.RoundToInt(alpha * 255f)
                );
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        File.WriteAllBytes(path, texture.EncodeToPNG());

        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(
            path,
            ImportAssetOptions.ForceUpdate
        );

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression =
            TextureImporterCompression.Uncompressed;

        var settings = new TextureImporterSettings();

        importer.ReadTextureSettings(settings);

        settings.spriteBorder = new Vector4(
            border,
            border,
            border,
            border
        );

        // Полный прямоугольник нужен, чтобы 9-slice не резал
        // спрайт по непрозрачным пикселям.
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteGenerateFallbackPhysicsShape = false;

        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// Прозрачность скруглённого прямоугольника через
    /// расстояние до прямых углов (signed distance).
    /// </summary>
    private static float RoundedAlpha(
        Vector2 point,
        float half,
        float radius)
    {
        float inner = half - radius;

        var corner = new Vector2(
            Mathf.Max(Mathf.Abs(point.x - half) - inner, 0f),
            Mathf.Max(Mathf.Abs(point.y - half) - inner, 0f)
        );

        float distance = corner.magnitude;

        return Mathf.Clamp01(radius + 0.5f - distance);
    }

    /// <summary>Мягкое пятно для фона: гладкое затухание к краям.</summary>
    private static float RadialAlpha(Vector2 point, float half)
    {
        float distance =
            Vector2.Distance(point, new Vector2(half, half));

        float t = Mathf.Clamp01(1f - distance / half);

        return t * t * (3f - 2f * t);
    }
}

#endif
