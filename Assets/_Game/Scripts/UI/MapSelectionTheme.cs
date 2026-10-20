using UnityEngine;

/// <summary>
/// Тёмная неоновая палитра панели «Карты».
/// </summary>
public static class MapSelectionTheme
{
    public static readonly Color PanelBackground =
        new Color(0.02f, 0.04f, 0.09f, 0.94f);

    public static readonly Color PanelWash =
        new Color(0.07f, 0.12f, 0.23f, 0.72f);

    public static readonly Color Surface = ArcadeUiPalette.Surface;
    public static readonly Color SurfaceMuted = ArcadeUiPalette.SurfaceMuted;
    public static readonly Color Track = ArcadeUiPalette.Track;

    public static readonly Color TextPrimary = ArcadeUiPalette.TextPrimary;
    public static readonly Color TextSecondary = ArcadeUiPalette.TextSecondary;
    public static readonly Color TextMuted = ArcadeUiPalette.TextMuted;
    public static readonly Color DarkText = ArcadeUiPalette.BackgroundDeep;

    public static readonly Color Action = ArcadeUiPalette.Cyan;
    public static readonly Color ActionDisabled = ArcadeUiPalette.SurfaceMuted;
    public static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.45f);

    /// <summary>Рамка невыбранной карточки.</summary>
    public static readonly Color FrameIdle = ArcadeUiPalette.Frame;

    /// <summary>Рамка выбранной карточки.</summary>
    public static readonly Color FrameSelected = ArcadeUiPalette.Cyan;

    /// <summary>Фон чипа «ВЫБРАНО».</summary>
    public static readonly Color ChipBackground = ArcadeUiPalette.Cyan;

    public static readonly Color DotActive = ArcadeUiPalette.Cyan;
    public static readonly Color DotInactive = ArcadeUiPalette.Track;

    /// <summary>Белый с заданной прозрачностью — бейджи и подписи на кнопке.</summary>
    public static Color White(float alpha)
    {
        return new Color(1f, 1f, 1f, alpha);
    }

    private static Color Rgb(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f
        );
    }
}