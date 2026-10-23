#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ArcadeUiTextures
{
    public const string Root = "Assets/_Game/UI/ThirdParty";

    private const string OfdnRoot = Root + "/OFDN";
    private const string MetalRoot = Root + "/Metal";
    private const string IconRoot = Root + "/Kenney/Icons";

    private const string PanelPath = OfdnRoot + "/OFDN_InventoryA.png";
    private const string CardPath = OfdnRoot + "/OFDN_InventoryB1.png";
    private const string ButtonPath = OfdnRoot + "/OFDN_InventoryB2.png";
    private const string BarPath = IconRoot + "/bar_fill.png";
    private const string TrackPath = OfdnRoot + "/OFDN_HealthA2.png";
    private const string HealthFillPath = OfdnRoot + "/OFDN_HealthA1.png";
    private const string BackdropPath = MetalRoot + "/Metal_010.png";

    private static readonly Dictionary<string, string> SpritePaths =
        new Dictionary<string, string>
        {
            { "panel", PanelPath },
            { "navy_panel", PanelPath },
            { "frame", CardPath },
            { "surface", CardPath },
            { "secondary", CardPath },
            { "navy_card", CardPath },
            { "primary", ButtonPath },
            { "toggle", ButtonPath },
            { "pill", ButtonPath },
            { "navy_pill", ButtonPath },
            { "gold_button", ButtonPath },
            { "gold_pill", ButtonPath },
            { "logo_paint", ButtonPath },
            { "bar", BarPath },
            { "health_fill", HealthFillPath },
            { "track", TrackPath },
            { "overlay", TrackPath },
            { "glow", IconRoot + "/glow.png" },
            { "circle", IconRoot + "/circle.png" },
            { "dot", IconRoot + "/dot.png" },
            { "coin", IconRoot + "/coin.png" },
            { "arrow", IconRoot + "/arrow.png" },
            { "star", IconRoot + "/star.png" },
            { "diamond", IconRoot + "/gem.png" },
            { "play", IconRoot + "/play.png" },
            { "check", IconRoot + "/check.png" },
            { "cross", IconRoot + "/close.png" },
            { "hex", IconRoot + "/hex.png" },
            { "weapon", IconRoot + "/sword.png" },
            { "icon_crown", IconRoot + "/crown.png" },
            { "icon_gem", IconRoot + "/gem.png" },
            { "icon_gear", IconRoot + "/gear.png" },
            { "icon_gift", IconRoot + "/gift.png" },
            { "icon_cart", IconRoot + "/cart.png" },
            { "icon_chart", IconRoot + "/chart.png" },
            { "icon_book", IconRoot + "/book.png" },
            { "icon_person", IconRoot + "/person.png" },
            { "icon_exit", IconRoot + "/exit.png" },
            { "icon_map", IconRoot + "/scroll.png" },
            { "icon_trophy", IconRoot + "/trophy.png" },
            { "icon_calendar", IconRoot + "/calendar.png" },
            { "icon_chest", IconRoot + "/pouch.png" },
            { "icon_plus", IconRoot + "/plus.png" },
            { "icon_chevron", IconRoot + "/arrow.png" },
            { "icon_lock", IconRoot + "/lock.png" },
            { "icon_pause", IconRoot + "/pause.png" },
            { "icon_sparkle", IconRoot + "/star.png" },
            { "icon_bolt", IconRoot + "/power.png" },
            { "icon_heart", IconRoot + "/heart.png" },
            { "icon_shield", IconRoot + "/shield.png" },
            { "icon_flame", IconRoot + "/flame.png" },
            { "icon_close", IconRoot + "/close.png" },
            { "icon_back", IconRoot + "/back.png" },
            { "icon_target", IconRoot + "/target.png" },
            { "icon_award", IconRoot + "/award.png" }
        };

    private static Dictionary<string, Sprite> sprites;
    private static Sprite backdrop;

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
        AssetDatabase.Refresh();

        HashSet<string> configured = new HashSet<string>();
        List<string> errors = new List<string>();

        foreach (string path in SpritePaths.Values)
        {
            if (!configured.Add(path))
                continue;

            if (!Configure(path))
                errors.Add(path);
        }

        if (!Configure(BackdropPath))
            errors.Add(BackdropPath);

        if (errors.Count > 0)
        {
            Debug.LogError(
                "Arcade UI textures could not be imported:\n" +
                string.Join("\n", errors)
            );
        }

        AssetDatabase.Refresh();
        sprites = null;
        backdrop = null;
    }

    public static Sprite Get(string name)
    {
        if (sprites == null)
            LoadSprites();

        if (sprites.TryGetValue(name, out Sprite sprite) && sprite != null)
            return sprite;

        throw new InvalidOperationException(
            "Arcade UI texture is missing: " + name +
            ". Run Tools/ArcadeSurvivor/Assets/Rebuild UI Textures."
        );
    }

    [MenuItem("Tools/ArcadeSurvivor/Assets/Rebuild UI Textures", false, 30)]
    public static void Rebuild()
    {
        sprites = null;
        backdrop = null;
        Ensure();

        EditorUtility.DisplayDialog(
            "Arcade Survivor UI",
            "Downloaded UI textures refreshed.",
            "OK"
        );
    }

    private static void LoadSprites()
    {
        sprites = new Dictionary<string, Sprite>();

        foreach (KeyValuePair<string, string> pair in SpritePaths)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(pair.Value);

            if (sprite != null)
                sprites[pair.Key] = sprite;
        }

        backdrop = AssetDatabase.LoadAssetAtPath<Sprite>(BackdropPath);
    }

    private static bool Configure(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path)
            as TextureImporter;

        if (importer == null)
            return false;

        Vector4 border = BorderFor(path);
        int maxSize = path.Contains("/Kenney/Icons/") ? 256 : 2048;

        bool changed = false;

        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            changed = true;
        }

        if (importer.spriteImportMode != SpriteImportMode.Single)
        {
            importer.spriteImportMode = SpriteImportMode.Single;
            changed = true;
        }

        if (!Mathf.Approximately(importer.spritePixelsPerUnit, 100f))
        {
            importer.spritePixelsPerUnit = 100f;
            changed = true;
        }

        if (importer.spriteBorder != border)
        {
            importer.spriteBorder = border;
            changed = true;
        }

        if (importer.mipmapEnabled)
        {
            importer.mipmapEnabled = false;
            changed = true;
        }

        if (!importer.alphaIsTransparency)
        {
            importer.alphaIsTransparency = true;
            changed = true;
        }

        if (importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.wrapMode = TextureWrapMode.Clamp;
            changed = true;
        }

        if (importer.filterMode != FilterMode.Bilinear)
        {
            importer.filterMode = FilterMode.Bilinear;
            changed = true;
        }

        if (importer.maxTextureSize != maxSize)
        {
            importer.maxTextureSize = maxSize;
            changed = true;
        }

        if (importer.textureCompression !=
            TextureImporterCompression.CompressedHQ)
        {
            importer.textureCompression =
                TextureImporterCompression.CompressedHQ;
            changed = true;
        }

        if (importer.npotScale != TextureImporterNPOTScale.None)
        {
            importer.npotScale = TextureImporterNPOTScale.None;
            changed = true;
        }

        if (changed)
            importer.SaveAndReimport();

        return true;
    }

    private static Vector4 BorderFor(string path)
    {
        if (path == PanelPath)
            return new Vector4(48f, 48f, 48f, 48f);

        if (path == CardPath)
            return new Vector4(64f, 64f, 64f, 64f);

        if (path == ButtonPath)
            return new Vector4(72f, 32f, 72f, 32f);

        if (path == BarPath)
            return new Vector4(24f, 24f, 24f, 24f);

        if (path == TrackPath || path == HealthFillPath)
            return new Vector4(32f, 16f, 32f, 16f);

        return Vector4.zero;
    }
}

#endif
