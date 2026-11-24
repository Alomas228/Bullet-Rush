using TMPro;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Design coordinates with separate landscape/portrait positions inside a fitting board.
public sealed class MenuSheet : MonoBehaviour
{
    public static readonly Color Paper = ArcadeUiTheme.Rgb(0xF3EBD9);
    public static readonly Color Card = ArcadeUiTheme.Rgb(0xFFFAEE);
    public static readonly Color Ink = ArcadeUiTheme.Rgb(0x29343A);
    public static readonly Color Muted = ArcadeUiTheme.Rgb(0x53605B);
    public static readonly Color Rule = ArcadeUiTheme.Rgb(0xC9C4AF);
    public static readonly Color Recess = ArcadeUiTheme.Rgb(0xE4DFCD);
    public static readonly Color Ochre = ArcadeUiTheme.Rgb(0xDDAE6B);
    public static readonly Color Sage = ArcadeUiTheme.Rgb(0xA9C3A1);
    public static readonly Color Coral = ArcadeUiTheme.Rgb(0xDDA08B);
    public static readonly Color Blue = ArcadeUiTheme.Rgb(0xA3C3C6);
    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);

    public RectTransform Body { get; private set; }
    private RectTransform frame;
    private bool? portrait;
    private readonly List<Placement> placements = new List<Placement>();

    private sealed class Placement
    {
        public RectTransform Rect;
        public Vector2 Position, Size, PortraitPosition, PortraitSize;
    }

    public static MenuSheet Build(Component panel, Button previous, Button next, RectTransform dots)
    {
        var sheet = panel.gameObject.AddComponent<MenuSheet>();
        var root = (RectTransform)panel.transform;
        var header = root.Find("TopBar") ?? root;
        var title = header.Find("TitleText").GetComponent<TMP_Text>();
        var subtitle = header.Find("SubtitleText").GetComponent<TMP_Text>();
        var back = root.Find("BackButton").GetComponent<Button>();

        HideChildren(root);
        var dim = root.GetComponent<Image>();
        if (dim != null)
        {
            dim.sprite = null;
            dim.color = new Color(0.12f, 0.16f, 0.15f, 0.62f);
            dim.raycastTarget = true;
        }

        UiArtboard.Stretch(root);
        var frame = UiArtboard.Create(root, new Vector2(1520, 864));
        sheet.frame = frame;
        var body = ArcadeUiFactory.At(frame, "MenuSheet", Vector2.one * 0.5f,
            Vector2.one * 0.5f, Vector2.zero, new Vector2(1472, 820));
        sheet.Body = body;
        ArcadeUiFactory.Image(body, ArcadeKitAssets.RoundRect, Paper, true).raycastTarget = true;
        var headerRule = Box(body, "HeaderRule", 42, 127, 1388, 2, Rule);
        var footerRule = Box(body, "FooterRule", 42, 724, 1388, 2, Rule);
        Label(title, body, 42, 22, 960, 59, 43, Ink, heading: true);
        Label(subtitle, body, 44, 82, 1080, 32, 21, Muted);
        WindowCloseButton.Attach(body, back);
        Control(previous, body, 536, 746, 64, 52, Recess);
        Control(next, body, 872, 746, 64, 52, Recess);
        if (dots != null)
            Place(dots, body, 610, 746, 252, 52);
        sheet.PortraitPlace(headerRule, 42,170,868,2);
        sheet.PortraitPlace(footerRule, 42,1356,868,2);
        sheet.PortraitPlace(title, 42,22,820,59);
        sheet.PortraitPlace(subtitle, 44,82,864,32);
        sheet.PortraitPlace(previous,42,1374,64,52);
        sheet.PortraitPlace(next,366,1374,64,52);
        sheet.PortraitPlace(dots,108,1374,252,52);
        return sheet;
    }

    public void Balance(TMP_Text coins, TMP_Text level = null)
    {
        if (coins != null)
        {
            var chip = Box(Body, "Balance", 1190, 34, 176, 52, Ink);
            var coin = Box(chip.rectTransform, "Coin", 16, 12, 28, 28, Ochre);
            coin.sprite = ArcadeKitAssets.Icon("coin");
            coin.type = Image.Type.Simple;
            Label(coins, chip.rectTransform, 48, 0, 112, 52, 24, Paper, TextAlignmentOptions.Center);
            PortraitPlace(chip, 734,114,176,48);
        }

        if (level != null)
        {
            Label(level, Body, 1016, 34, 160, 52, 21, Ink, TextAlignmentOptions.Right);
            PortraitPlace(level,558,114,160,48);
        }
    }

    // Register a second layout without rebuilding the view or touching its saved
    // visibility, listeners, scroll positions or current selection.
    public void PortraitPlace(Component item, float x, float y, float width, float height)
    {
        if (item == null || !(item.transform is RectTransform rect)) return;
        placements.Add(new Placement { Rect = rect, Position = rect.anchoredPosition, Size = rect.sizeDelta,
            PortraitPosition = new Vector2(x,-y), PortraitSize = new Vector2(width,height) });
        portrait = null;
    }

    private void LateUpdate() => Reflow();

    public void Reflow()
    {
        if (Body == null || frame == null) return;
        bool vertical = ResponsiveUi.Portrait;
        if (portrait == vertical) return;
        portrait = vertical;
        UiArtboard.Resize(frame, vertical ? new Vector2(1000,1494) : new Vector2(1520,864));
        Body.sizeDelta = vertical ? new Vector2(952,1450) : new Vector2(1472,820);
        foreach (Placement place in placements)
        {
            if (place.Rect == null) continue;
            place.Rect.anchoredPosition = vertical ? place.PortraitPosition : place.Position;
            place.Rect.sizeDelta = vertical ? place.PortraitSize : place.Size;
        }
    }

    public static void HideChildren(Transform parent)
    {
        foreach (Transform child in parent)
            child.gameObject.SetActive(false);
    }

    public static void Place(Component item, Transform parent, float x, float y, float w, float h)
    {
        var rect = (RectTransform)item.transform;
        if (rect.parent != parent) rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = TopLeft;
        rect.localScale = Vector3.one;
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.SetAsLastSibling();
        rect.gameObject.SetActive(true);
    }

    public static Image Box(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var rect = ArcadeUiFactory.At(parent, name, TopLeft, TopLeft,
            new Vector2(x, -y), new Vector2(w, h));
        return ArcadeUiFactory.Image(rect, ArcadeKitAssets.RoundRect, color, true);
    }

    public static void Surface(Image image, Color color, bool hits = false)
    {
        image.sprite = ArcadeKitAssets.RoundRect;
        image.type = Image.Type.Sliced;
        image.color = color;
        image.raycastTarget = hits;
    }

    public static void Label(TMP_Text text, Transform parent, float x, float y, float w, float h,
        float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Left, bool heading = false)
    {
        Place(text, parent, x, y, w, h);
        text.font = heading ? ArcadeKitAssets.Display : ArcadeKitAssets.BodyBold;
        text.fontStyle = FontStyles.Normal;
        text.color = color;
        text.alignment = align;
        text.margin = Vector4.zero;
        text.enableAutoSizing = true;
        text.fontSizeMax = text.fontSize = size;
        text.fontSizeMin = size * 0.77f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
    }

    public static void Control(Button button, Transform parent, float x, float y, float w, float h, Color color)
    {
        Place(button, parent, x, y, w, h);
        var image = button.GetComponent<Image>();
        Surface(image, color, true);
        button.targetGraphic = image;
        var label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            Label(label, button.transform, 8, 0, w - 16, h, 23, Ink, TextAlignmentOptions.Center);
    }

    public static Button NewButton(Transform parent, string name, float x, float y, float w, float h,
        Color color, UnityEngine.Events.UnityAction action, out TMP_Text label)
    {
        var rect = ArcadeUiFactory.At(parent, name, TopLeft, TopLeft,
            new Vector2(x, -y), new Vector2(w, h));
        var button = ArcadeUiFactory.Button(rect, ArcadeKitAssets.RoundRect, color, "",
            ArcadeKitAssets.BodyBold, 23, Ink, action);
        label = button.GetComponentInChildren<TMP_Text>();
        return button;
    }
}
