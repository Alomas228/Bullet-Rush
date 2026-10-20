using UnityEngine;

public static class ArcadeUiPalette
{
    public static readonly Color Background = Rgb(0x050914);
    public static readonly Color BackgroundDeep = Rgb(0x02040A);
    public static readonly Color PanelBackground = Rgb(0x0B1428);
    public static readonly Color PanelWash = Rgb(0x111E3B);
    public static readonly Color DecorWash = Rgb(0x172A52);

    public static readonly Color Surface = Rgb(0x111E3B);
    public static readonly Color SurfaceMuted = Rgb(0x172A52);
    public static readonly Color SurfaceDisabled = Rgb(0x0C1730);
    public static readonly Color Track = Rgb(0x060C1A);

    public static readonly Color TextPrimary = Rgb(0xF4F8FF);
    public static readonly Color TextSecondary = Rgb(0xA6B6D6);
    public static readonly Color TextMuted = Rgb(0x64769A);

    public static readonly Color Cyan = Rgb(0x41F2FF);
    public static readonly Color Magenta = Rgb(0xFF3E8A);
    public static readonly Color Gold = Rgb(0xFFC857);
    public static readonly Color Lime = Rgb(0xB8FF4D);
    public static readonly Color Green = Rgb(0x4BF0A7);
    public static readonly Color Red = Rgb(0xFF5C7A);
    public static readonly Color Purple = Rgb(0x8A6CFF);
    public static readonly Color Orange = Rgb(0xFFA83D);

    public static readonly Color Frame = new Color(0.58f, 0.75f, 1f, 0.28f);
    public static readonly Color Overlay = new Color(0.01f, 0.02f, 0.05f, 0.82f);
    public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);

    public static readonly Color Action = Cyan;
    public static readonly Color ActionDisabled = SurfaceMuted;

    public static readonly Color DotActive = Cyan;
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
