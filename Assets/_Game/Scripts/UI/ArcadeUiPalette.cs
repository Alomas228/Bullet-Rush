using UnityEngine;

public static class ArcadeUiPalette
{
    public static readonly Color Background = Rgb(0x0D1526);
    public static readonly Color BackgroundDeep = Rgb(0x0A1020);
    public static readonly Color PanelBackground = Rgb(0x17233B);
    public static readonly Color PanelWash = Rgb(0x1D2C49);
    public static readonly Color DecorWash = Rgb(0x223354);

    public static readonly Color Surface = Rgb(0x1D2C49);
    public static readonly Color SurfaceMuted = Rgb(0x223354);
    public static readonly Color SurfaceDisabled = Rgb(0x141F36);
    public static readonly Color Track = Rgb(0x0A1020);

    public static readonly Color TextPrimary = Rgb(0xF4F7FF);
    public static readonly Color TextSecondary = Rgb(0xADBBD4);
    public static readonly Color TextMuted = Rgb(0x7385A6);

    public static readonly Color Cyan = Rgb(0x53DFFF);
    public static readonly Color Magenta = Rgb(0xFF667F);
    public static readonly Color Gold = Rgb(0xFFC83D);
    public static readonly Color Lime = Rgb(0x54DFA0);
    public static readonly Color Green = Rgb(0x54DFA0);
    public static readonly Color Red = Rgb(0xFF667F);
    public static readonly Color Purple = Rgb(0x9D83FF);
    public static readonly Color Orange = Rgb(0xFF9D2E);

    public static readonly Color Frame = new Color(0.23f, 0.31f, 0.47f, 0.42f);
    public static readonly Color Overlay = new Color(0.02f, 0.035f, 0.07f, 0.82f);
    public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.4f);

    public static readonly Color Action = Gold;
    public static readonly Color ActionDisabled = SurfaceMuted;

    public static readonly Color DotActive = Gold;
    public static readonly Color DotInactive = Track;

    public static Color Wash(Color color, float alpha = 0.14f)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    public static Color Shade(Color color, float factor)
    {
        return new Color(
            color.r * factor,
            color.g * factor,
            color.b * factor,
            color.a
        );
    }

    public static Color White(float alpha)
    {
        return new Color(1f, 1f, 1f, alpha);
    }

    public static Color Rgb(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f
        );
    }
}
