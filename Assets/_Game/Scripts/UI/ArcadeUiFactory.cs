using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public static class ArcadeUiFactory
{
    public static RectTransform Rect(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        rect.localScale = Vector3.one;

        return rect;
    }

    public static RectTransform At(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        return Rect(parent, name, anchor, anchor, pivot, position, size);
    }

    public static RectTransform Stretch(
        Transform parent,
        string name,
        float left = 0f,
        float bottom = 0f,
        float right = 0f,
        float top = 0f)
    {
        RectTransform rect = Rect(
            parent,
            name,
            Vector2.zero,
            Vector2.one,
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            Vector2.zero
        );

        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(-right, -top);

        return rect;
    }

    public static Image Image(
        RectTransform rect,
        Sprite sprite,
        Color color,
        bool sliced = false)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.color = color;
        image.type = sliced
            ? UnityEngine.UI.Image.Type.Sliced
            : UnityEngine.UI.Image.Type.Simple;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = false;

        return image;
    }

    public static Image Panel(
        Transform parent,
        string name,
        Sprite sprite,
        Color color,
        float left = 0f,
        float bottom = 0f,
        float right = 0f,
        float top = 0f)
    {
        RectTransform rect = Stretch(parent, name, left, bottom, right, top);
        return Image(rect, sprite, color, true);
    }

    public static TextMeshProUGUI Label(
        RectTransform rect,
        string value,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        bool uppercase = false)
    {
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.color = color;
        text.alignment = alignment;
        text.raycastTarget = false;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;

        if (uppercase)
            text.fontStyle = FontStyles.UpperCase;

        return text;
    }

    public static TextMeshProUGUI Label(
        Transform parent,
        string name,
        string value,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 rectSize,
        bool uppercase = false)
    {
        RectTransform rect = At(parent, name, anchor, pivot, position, rectSize);
        return Label(rect, value, font, size, color, alignment, uppercase);
    }

    public static Button Button(
        RectTransform rect,
        Sprite sprite,
        Color tint,
        string label,
        TMP_FontAsset font,
        float fontSize,
        Color textColor,
        UnityAction onClick,
        bool hologram = false)
    {
        Image image = Image(rect, sprite, tint, true);
        image.raycastTarget = true;

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        if (onClick != null)
            button.onClick.AddListener(onClick);

        RectTransform textRect = Stretch(
            rect,
            "Label",
            hologram ? 32f : 18f,
            4f,
            hologram ? 32f : 18f,
            4f
        );

        Label(
            textRect,
            label ?? string.Empty,
            font,
            fontSize,
            textColor,
            TextAlignmentOptions.Center
        );

        ArcadeHoverLift hover = rect.gameObject.AddComponent<ArcadeHoverLift>();
        hover.Bind(image, 1.018f, 0.08f);

        return button;
    }

    public static Slider Bar(
        RectTransform rect,
        float value,
        bool interactive,
        Color trackTint,
        Color fillTint,
        float handleSize = 22f)
    {
        Slider slider = rect.gameObject.AddComponent<Slider>();
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation { mode = Navigation.Mode.None };
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;

        RectTransform trackRect = Stretch(rect, "Track");
        Image track = Image(trackRect, ArcadeKitAssets.BarTrack, trackTint, true);
        track.raycastTarget = interactive;

        RectTransform fillArea = Stretch(rect, "Fill Area", 2f, 2f, 2f, 2f);
        RectTransform fillRect = Stretch(fillArea, "Fill");
        Image fill = Image(fillRect, ArcadeKitAssets.BarFill, fillTint, true);

        RectTransform handleArea = Stretch(rect, "Handle Area");
        RectTransform handleRect = At(
            handleArea,
            "Handle",
            new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(handleSize, handleSize)
        );
        Image handle = Image(handleRect, ArcadeKitAssets.SliderHandle, Color.white);

        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = interactive ? track : null;
        slider.interactable = interactive;
        slider.SetValueWithoutNotify(Mathf.Clamp01(value));

        if (!interactive)
        {
            handle.gameObject.SetActive(false);
            handleArea.gameObject.SetActive(false);
        }

        return slider;
    }

    public static Image Icon(
        Transform parent,
        string name,
        string iconName,
        Color tint,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect = At(parent, name, anchor, pivot, position, size);
        return Image(rect, ArcadeKitAssets.Icon(iconName), tint);
    }

    public static Image Glow(
        Transform parent,
        string name,
        Color tint,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 position,
        Vector2 size,
        float alpha)
    {
        RectTransform rect = At(parent, name, anchor, pivot, position, size);
        Image image = Image(rect, ArcadeKitAssets.Glow, ArcadeUiTheme.Alpha(tint, alpha));
        image.gameObject.AddComponent<ArcadePulse>().Bind(image, alpha * 0.5f, alpha, 1.1f);
        return image;
    }
}
