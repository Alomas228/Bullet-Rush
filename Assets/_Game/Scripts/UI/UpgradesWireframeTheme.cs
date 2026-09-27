using UnityEngine;

/// <summary>
/// Светлая палитра экрана «Улучшения».
///
/// Это заглушка, а не финальный арт: здесь собраны все цвета
/// макета в одном месте, чтобы потом перенести их в Figma и
/// заменить одним набором переменных.
///
/// Акценты намеренно приглушённые — без неона и свечения.
/// </summary>
public static class UpgradesWireframeTheme
{
    public static readonly Color PanelBackground = Rgb(0xF1F4F8);
    public static readonly Color PanelWash = Rgb(0xE4EAF2);
    public static readonly Color DecorWash = Rgb(0xDDE5EF);

    public static readonly Color Surface = Rgb(0xFFFFFF);
    public static readonly Color SurfaceMuted = Rgb(0xE9EEF4);
    public static readonly Color SurfaceDisabled = Rgb(0xD5DDE6);
    public static readonly Color Track = Rgb(0xE6EAF0);

    public static readonly Color TextPrimary = Rgb(0x1F2933);
    public static readonly Color TextSecondary = Rgb(0x6B7684);
    public static readonly Color TextMuted = Rgb(0x9AA5B1);

    public static readonly Color Action = Rgb(0x3A4552);
    public static readonly Color ActionDisabled = Rgb(0xAFBAC7);
    public static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.10f);

    public static readonly Color DotActive = Rgb(0x3A4552);
    public static readonly Color DotInactive = Rgb(0xCFD7E0);

    public static readonly Color DamageAccent = Rgb(0x4A8FD4);
    public static readonly Color HealthAccent = Rgb(0xE09B3E);
    public static readonly Color SpeedAccent = Rgb(0x8B72C7);
    public static readonly Color CriticalAccent = Rgb(0x54A06A);

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
