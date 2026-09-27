using UnityEngine;

/// <summary>
/// Светлая палитра экрана «Снаряжение».
///
/// Тот же визуальный язык, что и на «Улучшениях» (светлый фон,
/// белые поверхности, тонкие рамки, один приглушённый акцент),
/// но со своим набором токенов: здесь акцент задаёт не группа
/// параметра, а редкость предмета, а главный цветной элемент —
/// вкладка с названием вкладки и тем, что сейчас надето.
///
/// Как и UpgradesWireframeTheme, это заглушка для переноса в Figma,
/// а не финальный арт. Все цвета собраны здесь, чтобы заменить их
/// одним набором переменных.
/// </summary>
public static class EquipmentWireframeTheme
{
    public static readonly Color PanelBackground = Rgb(0xF4F1EC);
    public static readonly Color PanelWash = Rgb(0xE9E4DC);
    public static readonly Color DecorWash = Rgb(0xE2DCD2);

    public static readonly Color Surface = Rgb(0xFFFFFF);
    public static readonly Color SurfaceMuted = Rgb(0xEFEAE2);
    public static readonly Color SurfaceDisabled = Rgb(0xDDD6CB);
    public static readonly Color Track = Rgb(0xE8E2D8);

    public static readonly Color TextPrimary = Rgb(0x2B2723);
    public static readonly Color TextSecondary = Rgb(0x6E675E);
    public static readonly Color TextMuted = Rgb(0x9C9489);

    public static readonly Color Action = Rgb(0x3B3733);
    public static readonly Color ActionDisabled = Rgb(0xB4ADA2);
    public static readonly Color CardShadow = new Color(0f, 0f, 0f, 0.09f);

    public static readonly Color DotActive = Rgb(0x3B3733);
    public static readonly Color DotInactive = Rgb(0xD2CABE);

    /// <summary>Редкость предмета: у оружия она задана ассетом.</summary>
    public static readonly Color RarityCommon = Rgb(0x8C949C);

    public static readonly Color RarityUncommon = Rgb(0x54A06A);
    public static readonly Color RarityRare = Rgb(0x4A8FD4);
    public static readonly Color RarityEpic = Rgb(0x8B72C7);
    public static readonly Color RarityLegendary = Rgb(0xE09B3E);

    /// <summary>Акцент вкладок «Способности» и «Одежда».</summary>
    public static readonly Color AbilityAccent = Rgb(0x3E8C8C);

    public static readonly Color ClothingAccent = Rgb(0xB06A8C);

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
