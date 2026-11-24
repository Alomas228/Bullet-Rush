using UnityEngine;

public static class ArcadeUiPalette
{
    public static readonly Color Background = ArcadeUiTheme.Background;
    public static readonly Color BackgroundDeep = ArcadeUiTheme.Ink;
    public static readonly Color PanelBackground = ArcadeUiTheme.Panel;
    public static readonly Color PanelWash = ArcadeUiTheme.PanelRaised;
    public static readonly Color DecorWash = ArcadeUiTheme.Card;

    public static readonly Color Surface = ArcadeUiTheme.Card;
    public static readonly Color SurfaceMuted = ArcadeUiTheme.PanelRaised;
    public static readonly Color SurfaceDisabled = ArcadeUiTheme.Background;
    public static readonly Color Track = ArcadeUiTheme.Line;

    public static readonly Color TextPrimary = ArcadeUiTheme.TextPrimary;
    public static readonly Color TextSecondary = ArcadeUiTheme.TextSecondary;
    public static readonly Color TextMuted = ArcadeUiTheme.TextMuted;

    public static readonly Color Cyan = ArcadeUiTheme.Cyan;
    public static readonly Color Magenta = ArcadeUiTheme.Red;
    public static readonly Color Gold = ArcadeUiTheme.Gold;
    public static readonly Color Lime = ArcadeUiTheme.Green;
    public static readonly Color Green = ArcadeUiTheme.Green;
    public static readonly Color Red = ArcadeUiTheme.Red;
    public static readonly Color Purple = ArcadeUiTheme.Purple;
    public static readonly Color Orange = ArcadeUiTheme.Orange;

    public static readonly Color Frame = ArcadeUiTheme.Line;
    public static readonly Color Overlay = ArcadeUiTheme.Backdrop;
    public static readonly Color Shadow = ArcadeUiTheme.Shadow;

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
