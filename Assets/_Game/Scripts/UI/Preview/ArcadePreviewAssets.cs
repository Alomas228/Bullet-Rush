using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

public static class ArcadePreviewAssets
{
    private const string Root = "ArcadePreview/";
    private const string IconRoot = Root + "Icons/";
    private const string FontRoot = Root + "Fonts/";
    private const string KenneyRoot = Root + "Kenney/";

    private static readonly Dictionary<string, Sprite> icons =
        new Dictionary<string, Sprite>();

    public static Sprite Panel { get; private set; }
    public static Sprite ButtonBlue { get; private set; }
    public static Sprite ButtonGreen { get; private set; }
    public static Sprite ButtonRed { get; private set; }
    public static Sprite ButtonYellow { get; private set; }
    public static Sprite ButtonGrey { get; private set; }
    public static Sprite BarTrack { get; private set; }
    public static Sprite BarGreen { get; private set; }
    public static Sprite BarYellow { get; private set; }
    public static Sprite BarBlue { get; private set; }
    public static Sprite BarRed { get; private set; }
    public static Sprite Toggle { get; private set; }
    public static Sprite Metal { get; private set; }

    public static TMP_FontAsset Display { get; private set; }
    public static TMP_FontAsset Body { get; private set; }
    public static TMP_FontAsset BodyBold { get; private set; }

    public static void Ensure()
    {
        if (Panel == null)
            Panel = Sliced(Load(KenneyRoot + "panel"), 48f, 48f, 48f, 48f);

        if (ButtonBlue == null)
            ButtonBlue = Sliced(Load(KenneyRoot + "btn_blue"), 120f, 72f, 40f, 40f);

        if (ButtonGreen == null)
            ButtonGreen = Sliced(Load(KenneyRoot + "btn_green"), 120f, 72f, 40f, 40f);

        if (ButtonRed == null)
            ButtonRed = Sliced(Load(KenneyRoot + "btn_red"), 120f, 72f, 40f, 40f);

        if (ButtonYellow == null)
            ButtonYellow = Sliced(Load(KenneyRoot + "btn_yellow"), 120f, 72f, 40f, 40f);

        if (ButtonGrey == null)
            ButtonGrey = Sliced(Load(KenneyRoot + "btn_grey"), 120f, 72f, 40f, 40f);

        if (BarTrack == null)
            BarTrack = Sliced(Load(KenneyRoot + "bar_track"), 14f, 14f, 14f, 14f);

        if (BarGreen == null)
            BarGreen = Sliced(Load(KenneyRoot + "bar_green"), 14f, 14f, 14f, 14f);

        if (BarYellow == null)
            BarYellow = Sliced(Load(KenneyRoot + "bar_yellow"), 14f, 14f, 14f, 14f);

        if (BarBlue == null)
            BarBlue = Sliced(Load(KenneyRoot + "bar_blue"), 14f, 14f, 14f, 14f);

        if (BarRed == null)
            BarRed = Sliced(Load(KenneyRoot + "bar_red"), 14f, 14f, 14f, 14f);

        if (Toggle == null)
            Toggle = Sliced(Load(KenneyRoot + "toggle"), 30f, 30f, 30f, 30f);

        if (Metal == null)
            Metal = MakeSprite(Resources.Load<Texture2D>(Root + "metal"));

        if (Display == null)
            Display = MakeFont(FontRoot + "RussoOne-Regular", 72, 2048);

        if (Body == null)
            Body = MakeFont(FontRoot + "PT_Sans-Regular", 64, 1024);

        if (BodyBold == null)
            BodyBold = MakeFont(FontRoot + "PT_Sans-Bold", 64, 1024);
    }

    public static Sprite Icon(string name)
    {
        if (icons.TryGetValue(name, out Sprite cached))
            return cached;

        Sprite sprite = MakeSprite(Resources.Load<Texture2D>(IconRoot + name));
        icons[name] = sprite;
        return sprite;
    }

    private static Texture2D Load(string path)
    {
        return Resources.Load<Texture2D>(path);
    }

    private static Sprite MakeSprite(Texture2D texture)
    {
        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );
    }

    private static Sprite Sliced(
        Texture2D texture,
        float left,
        float top,
        float right,
        float bottom)
    {
        if (texture == null)
            return null;

        return Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(left, bottom, right, top)
        );
    }

    private static TMP_FontAsset MakeFont(
        string resourcePath,
        int samplingSize,
        int atlasSize)
    {
        Font source = Resources.Load<Font>(resourcePath);

        if (source == null)
        {
            Debug.LogWarning(
                "Arcade preview font is missing: " + resourcePath
            );

            return TMP_Settings.defaultFontAsset;
        }

        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
            source,
            samplingSize,
            8,
            GlyphRenderMode.SDFAA,
            atlasSize,
            atlasSize,
            AtlasPopulationMode.Dynamic,
            true
        );

        if (font == null)
            return TMP_Settings.defaultFontAsset;

        font.name = resourcePath;
        font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
        font.isMultiAtlasTexturesEnabled = true;
        return font;
    }
}
