using UnityEngine;

public static class ArcadeUiTheme
{
    public static readonly Color Ink = Rgb(0x0A1020);
    public static readonly Color Background = Rgb(0x0D1526);
    public static readonly Color BackgroundDeep = Rgb(0x070C17);
    public static readonly Color Panel = Rgb(0x17233B);
    public static readonly Color PanelRaised = Rgb(0x1D2C49);
    public static readonly Color Card = Rgb(0x223354);
    public static readonly Color CardSoft = Rgb(0x1A2944);
    public static readonly Color Line = Rgb(0x3B5079);

    public static readonly Color TextPrimary = Rgb(0xF4F7FF);
    public static readonly Color TextSecondary = Rgb(0xADBBD4);
    public static readonly Color TextMuted = Rgb(0x7385A6);
    public static readonly Color TextDark = Rgb(0x172138);

    public static readonly Color Gold = Rgb(0xFFC83D);
    public static readonly Color GoldBright = Rgb(0xFFDA6A);
    public static readonly Color Orange = Rgb(0xFF9D2E);
    public static readonly Color OrangeDeep = Rgb(0xE87A19);
    public static readonly Color Cyan = Rgb(0x53DFFF);
    public static readonly Color Green = Rgb(0x54DFA0);
    public static readonly Color Red = Rgb(0xFF667F);
    public static readonly Color Purple = Rgb(0x9D83FF);

    public static readonly Color Backdrop = new Color(0.02f, 0.035f, 0.07f, 0.72f);
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



