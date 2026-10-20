using UnityEngine;

/// <summary>
/// Тёмная неоновая палитра экрана «Улучшения».
/// </summary>
public static class UpgradesWireframeTheme
{
    public static readonly Color PanelBackground = ArcadeUiPalette.PanelBackground;
    public static readonly Color PanelWash = ArcadeUiPalette.PanelWash;
    public static readonly Color DecorWash = ArcadeUiPalette.DecorWash;

    public static readonly Color Surface = ArcadeUiPalette.Surface;
    public static readonly Color SurfaceMuted = ArcadeUiPalette.SurfaceMuted;
    public static readonly Color SurfaceDisabled = ArcadeUiPalette.SurfaceDisabled;
    public static readonly Color Track = ArcadeUiPalette.Track;

    public static readonly Color TextPrimary = ArcadeUiPalette.TextPrimary;
    public static readonly Color TextSecondary = ArcadeUiPalette.TextSecondary;
    public static readonly Color TextMuted = ArcadeUiPalette.TextMuted;

    public static readonly Color Action = ArcadeUiPalette.Cyan;
    public static readonly Color ActionDisabled = ArcadeUiPalette.SurfaceMuted;
    public static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.45f);

    public static readonly Color DotActive = ArcadeUiPalette.Cyan;
    public static readonly Color DotInactive = ArcadeUiPalette.Track;

    public static readonly Color DamageAccent = ArcadeUiPalette.Magenta;
    public static readonly Color HealthAccent = ArcadeUiPalette.Lime;
    public static readonly Color SpeedAccent = ArcadeUiPalette.Cyan;
    public static readonly Color CriticalAccent = ArcadeUiPalette.Gold;

    public static Color GetAccent(PermanentUpgradeCategory category)
    {
        switch (category)
        {
            case PermanentUpgradeCategory.Damage:
                return DamageAccent;

            case PermanentUpgradeCategory.Health:
                return HealthAccent;

            case PermanentUpgradeCategory.Speed:
                return SpeedAccent;

            default:
                return CriticalAccent;
        }
    }

    /// <summary>Акцент с пониженной прозрачностью — подложки.</summary>
    public static Color Wash(Color color, float alpha = 0.14f)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    /// <summary>Затемнённый акцент — рамка выбранной карточки.</summary>
    public static Color Shade(Color color, float factor)
    {
        return new Color(
            color.r * factor,
            color.g * factor,
            color.b * factor,
            color.a
        );
    }

    /// <summary>Белый с заданной прозрачностью — бейджи на кнопке.</summary>
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
