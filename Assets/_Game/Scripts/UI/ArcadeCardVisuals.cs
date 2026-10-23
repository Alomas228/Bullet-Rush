using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared runtime look for map, equipment and upgrade cards.
/// Keeps the three card families on one visual language without
/// touching the hand-authored scene hierarchies.
/// </summary>
public static class ArcadeCardVisuals
{
    public static readonly Color Surface = ArcadeUiTheme.Panel;
    public static readonly Color ArtBase = ArcadeUiTheme.CardSoft;
    public static readonly Color Ink = ArcadeUiTheme.Ink;
    public static readonly Color Line = ArcadeUiTheme.Line;

    public static Color ArtTint(Color accent)
    {
        return Color.Lerp(ArtBase, accent, 0.16f);
    }

    public static Image EnsureImage(
        Transform parent,
        string name,
        Sprite sprite,
        Color color)
    {
        Transform child = parent.Find(name);
        GameObject go;

        if (child == null)
        {
            go = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image)
            );

            go.transform.SetParent(parent, false);
        }
        else
        {
            go = child.gameObject;

            if (go.GetComponent<Image>() == null)
                go.AddComponent<Image>();
        }

        Image image = go.GetComponent<Image>();

        image.sprite = sprite;
        image.color = color;
        image.raycastTarget = false;
        image.pixelsPerUnitMultiplier = 1f;
        image.type = sprite != null && sprite.border != Vector4.zero
            ? Image.Type.Sliced
            : Image.Type.Simple;

        return image;
    }

    public static void Stretch(RectTransform rect, float inset)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static void TopStretch(
        RectTransform rect,
        float top,
        float height,
        float sideInset)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(-sideInset * 2f, height);
        rect.anchoredPosition = new Vector2(0f, -top);
    }

    public static void BottomStretch(
        RectTransform rect,
        float bottom,
        float height,
        float sideInset)
    {
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(-sideInset * 2f, height);
        rect.anchoredPosition = new Vector2(0f, bottom);
    }

    public static Image Pattern(RectTransform art)
    {
        if (art == null || ArcadeKitAssets.CardGrid == null)
            return null;

        Image pattern = EnsureImage(
            art,
            "CardPattern",
            ArcadeKitAssets.CardGrid,
            new Color(1f, 1f, 1f, 0.85f)
        );

        pattern.type = Image.Type.Simple;
        Stretch(pattern.rectTransform, 3f);
        pattern.rectTransform.SetAsFirstSibling();

        return pattern;
    }

    public static Image AccentRail(Transform card, Color accent)
    {
        Image rail = EnsureImage(card, "AccentRail", ArcadeKitAssets.RoundRect, accent);

        TopStretch(rail.rectTransform, 9f, 4f, 14f);
        rail.raycastTarget = false;

        return rail;
    }

    public static Image Divider(Transform card, float top)
    {
        Image divider = EnsureImage(card, "CardDivider", null, Line);

        divider.type = Image.Type.Simple;
        divider.color = ArcadeUiTheme.Alpha(Line, 0.34f);
        TopStretch(divider.rectTransform, top, 2f, 16f);
        divider.raycastTarget = false;

        return divider;
    }

    public static Image InfoPanel(Transform parent, string name, float top, float height)
    {
        Image panel = EnsureImage(
            parent,
            name,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(Ink, 0.28f)
        );

        TopStretch(panel.rectTransform, top, height, 25f);
        panel.raycastTarget = false;

        return panel;
    }

    public static void Text(
        TMP_Text text,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions align,
        FontStyles style = FontStyles.Normal)
    {
        if (text == null)
            return;

        if (font != null)
            text.font = font;

        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.fontStyle = style;
    }

    public static void Chip(Image background, TMP_Text label, Color accent)
    {
        if (background != null)
        {
            background.sprite = ArcadeKitAssets.RoundRect;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;
            background.color = ArcadeUiTheme.Alpha(Ink, 0.86f);
        }

        Text(
            label,
            ArcadeKitAssets.BodyBold,
            label != null ? label.fontSize : 14f,
            accent,
            TextAlignmentOptions.Center,
            FontStyles.Bold | FontStyles.UpperCase
        );
    }

    public static void Action(
        Image background,
        TMP_Text label,
        bool enabled)
    {
        if (background != null)
        {
            background.sprite = ArcadeKitAssets.RoundRect;
            background.type = Image.Type.Sliced;
            background.pixelsPerUnitMultiplier = 1f;
            background.color = enabled
                ? ArcadeUiTheme.Orange
                : ArcadeUiTheme.Alpha(ArcadeUiPalette.SurfaceDisabled, 1f);
        }

        Text(
            label,
            ArcadeKitAssets.BodyBold,
            label != null ? label.fontSize : 21f,
            enabled ? ArcadeUiTheme.BackgroundDeep : ArcadeUiTheme.TextMuted,
            TextAlignmentOptions.Center,
            FontStyles.Bold | FontStyles.UpperCase
        );
    }

    public static void InfoText(TMP_Text text, Color color, float size)
    {
        Text(
            text,
            ArcadeKitAssets.Body,
            size,
            color,
            TextAlignmentOptions.Left,
            FontStyles.Normal
        );
    }
}
