using UnityEngine;

public static class ArcadeUiTheme
{
    public static readonly Color Ink = Rgb(0x29343A);
    public static readonly Color Background = Rgb(0xE4DFCD);
    public static readonly Color BackgroundDeep = Rgb(0x26312E);
    public static readonly Color Panel = Rgb(0xF3EBD9);
    public static readonly Color PanelRaised = Rgb(0xE4DFCD);
    public static readonly Color Card = Rgb(0xFFFAEE);
    public static readonly Color CardSoft = Rgb(0xEBE5D4);
    public static readonly Color Line = Rgb(0xB5AF9D);

    public static readonly Color TextPrimary = Rgb(0x29343A);
    public static readonly Color TextSecondary = Rgb(0x44534F);
    public static readonly Color TextMuted = Rgb(0x56605A);
    public static readonly Color TextDark = Rgb(0x29343A);

    public static readonly Color Gold = Rgb(0xDDAE6B);
    public static readonly Color GoldBright = Rgb(0xE9C794);
    public static readonly Color Orange = Rgb(0xD69D66);
    public static readonly Color OrangeDeep = Rgb(0xAD734E);
    public static readonly Color Cyan = Rgb(0x98BBBF);
    public static readonly Color Green = Rgb(0xA9C3A1);
    public static readonly Color Red = Rgb(0xD67E70);
    public static readonly Color Purple = Rgb(0xB3A2C2);

    public static readonly Color Backdrop = new Color(0.11f, 0.15f, 0.13f, 0.66f);
    public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.34f);

    public static Color Rgb(int rgb)
    {
        return new Color(
            ((rgb >> 16) & 0xFF) / 255f,
            ((rgb >> 8) & 0xFF) / 255f,
            (rgb & 0xFF) / 255f,
            1f
        );
    }

    public static Color Alpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    public static Color Bright(Color color, float amount)
    {
        return Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
    }

    public static Color Dark(Color color, float amount)
    {
        return Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
    }

    public static Color GoldGradient(float t)
    {
        return Color.Lerp(GoldBright, Orange, Mathf.Clamp01(t));
    }
}



