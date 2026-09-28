using UnityEngine;

/// <summary>
/// Светлая wireframe-палитра панели «Карты».
///
/// Та же заглушка-конвенция, что у «Улучшений» и «Снаряжения»:
/// все цвета макета собраны в одном месте, чтобы потом заменить
/// их одним набором переменных в Figma.
///
/// Фон панели намеренно полупрозрачный — за ним видно, как мир
/// уезжает/приезжает при выборе карты.
/// </summary>
public static class MapSelectionTheme
{
    public static readonly Color PanelBackground =
        new Color(0xF1 / 255f, 0xF4 / 255f, 0xF8 / 255f, 0.93f);

    public static readonly Color PanelWash =
        new Color(0xE4 / 255f, 0xEA / 255f, 0xF2 / 255f, 0.55f);

    public static readonly Color Surface = Rgb(0xFFFFFF);
    public static readonly Color SurfaceMuted = Rgb(0xE9EEF4);
    public static readonly Color Track = Rgb(0xE6EAF0);

    public static readonly Color TextPrimary = Rgb(0x1F2933);
    public static readonly Color TextSecondary = Rgb(0x6B7684);
    public static readonly Color TextMuted = Rgb(0x9AA5B1);

    public static readonly Color Action = Rgb(0x3A4552);
    public static readonly Color ActionDisabled = Rgb(0xAFBAC7);
    public static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.10f);

    /// <summary>Рамка невыбранной карточки.</summary>
    public static readonly Color FrameIdle = Rgb(0xD5DDE6);

    /// <summary>Рамка выбранной карточки.</summary>
    public static readonly Color FrameSelected = Rgb(0x3A4552);

    /// <summary>Фон чипа «ВЫБРАНО».</summary>
    public static readonly Color ChipBackground = Rgb(0x3A4552);

    public static readonly Color DotActive = Rgb(0x3A4552);
    public static readonly Color DotInactive = Rgb(0xCFD7E0);

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