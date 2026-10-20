using UnityEngine;

/// <summary>
/// Тёмная неоновая палитра экрана «Снаряжение».
/// </summary>
public static class EquipmentWireframeTheme
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
    public static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.42f);

    public static readonly Color DotActive = ArcadeUiPalette.Cyan;
    public static readonly Color DotInactive = ArcadeUiPalette.Track;

    /// <summary>Редкость предмета: у оружия она задана ассетом.</summary>
    public static readonly Color RarityCommon = ArcadeUiPalette.Rgb(0x8BA0C4);

    public static readonly Color RarityUncommon = ArcadeUiPalette.Green;
    public static readonly Color RarityRare = ArcadeUiPalette.Cyan;
    public static readonly Color RarityEpic = ArcadeUiPalette.Purple;
    public static readonly Color RarityLegendary = ArcadeUiPalette.Gold;

    /// <summary>Акцент вкладок «Способности» и «Одежда».</summary>
    public static readonly Color AbilityAccent = ArcadeUiPalette.Cyan;

    public static readonly Color ClothingAccent = ArcadeUiPalette.Magenta;

    public static Color GetRarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Uncommon:
                return RarityUncommon;

            case Rarity.Rare:
                return RarityRare;

            case Rarity.Epic:
                return RarityEpic;

            case Rarity.Legendary:
                return RarityLegendary;

            default:
                return RarityCommon;
        }
    }

    /// <summary>Акцент с пониженной прозрачностью — подложки.</summary>
    public static Color Wash(Color color, float alpha = 0.14f)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }

    /// <summary>Затемнённый цвет — рамка выбранной карточки.</summary>
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
