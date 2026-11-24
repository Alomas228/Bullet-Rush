using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal sealed class WaveChoiceCard
{
    private readonly Image icon;
    private readonly TMP_Text rarity;
    private readonly RectTransform root;

    public static RectTransform PreparePanel(GameObject panel)
    {
        var rect = (RectTransform)panel.transform;
        UiArtboard.Stretch(rect);
        var dim = panel.GetComponent<Image>();
        dim.sprite = null;
        dim.color = ArcadeUiTheme.Backdrop;
        dim.raycastTarget = true;
        var safe = ResponsiveUi.SafeContent(rect, "Choice safe area");
        var frame = ArcadeUiFactory.Stretch(safe, "Choice layout", 20, 20, 20, 166);
        var board = UiArtboard.Create(frame, new Vector2(1440, 810), true);
        var slots = (RectTransform)rect.Find("UpgradeSlots");
        slots.SetParent(board, false);
        var layout = slots.GetComponent<HorizontalLayoutGroup>();
        if (layout != null) layout.enabled = false;
        var background = slots.GetComponent<Image>();
        if (background != null) background.enabled = false;
        UiArtboard.Stretch(slots);
        var title = rect.Find("UpgradeTitle").GetComponent<TMP_Text>();
        title.rectTransform.SetParent(board, false);
        SetLabel(title, 900, 76, 219, 48, ArcadeKitAssets.Display, MenuSheet.Paper);
        title.gameObject.SetActive(true);
        return board;
    }

    public WaveChoiceCard(GameObject card, Button button, TMP_Text title, TMP_Text body, TMP_Text progress)
    {
        root = (RectTransform)card.transform;
        var rim = card.GetComponent<Image>();
        rim.sprite = ArcadeKitAssets.CutPanel;
        rim.type = Image.Type.Sliced;
        rim.color = Color.white;
        rim.raycastTarget = false;
        var sole = ArcadeUiFactory.Stretch(root, "Edge", 5, 5, 5, 5);
        ArcadeUiFactory.Image(sole, ArcadeKitAssets.CutPanel, MenuSheet.Ink, true);
        sole.SetAsFirstSibling();
        var face = ArcadeUiFactory.Stretch(root, "Paper", 5, 13, 5, 5);
        ArcadeUiFactory.Image(face, ArcadeKitAssets.CutPanel, MenuSheet.Paper, true);
        face.SetSiblingIndex(1);
        icon = ArcadeUiFactory.Image(ArcadeUiFactory.At(root, "Illustration", Vector2.one * .5f,
            Vector2.one * .5f, new Vector2(0,60), new Vector2(172,172)), null, Color.white);
        icon.preserveAspect = true;
        rarity = ArcadeUiFactory.Label(root, "Rarity", "", ArcadeKitAssets.BodyBold, 14, MenuSheet.Muted,
            TextAlignmentOptions.Center, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0,153), new Vector2(230,24));
        SetLabel(title, 254, 54, -38, 31, ArcadeKitAssets.Display, MenuSheet.Ink);
        SetLabel(body, 254, 80, -105, 21, ArcadeKitAssets.Body, MenuSheet.Ink);
        SetLabel(progress, 252, 29, -155, 15, ArcadeKitAssets.BodyBold, MenuSheet.Muted);
        var hit = button.GetComponent<Image>();
        hit.color = Color.clear;
        hit.raycastTarget = true;
        foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            label.gameObject.SetActive(false);
        UiArtboard.Stretch((RectTransform)button.transform);
        button.transform.SetAsLastSibling();
        button.transition = Selectable.Transition.ColorTint;
        button.targetGraphic = rim;
        var colors = ColorBlock.defaultColorBlock;
        colors.normalColor = MenuSheet.Ink;
        colors.selectedColor = colors.highlightedColor = MenuSheet.Ochre;
        colors.pressedColor = ArcadeUiTheme.OrangeDeep;
        colors.fadeDuration = .08f;
        button.colors = colors;
        button.navigation = new Navigation { mode = Navigation.Mode.Horizontal };
    }

    public void Bind(UpgradeData data, int index, int count)
    {
        Layout(index, count, ResponsiveUi.Portrait);
        icon.sprite = MenuSheetArt.Upgrade(data.CardIcon);
        string grade = Lang.Get("rarity." + data.Rarity.ToString().ToLowerInvariant());
        var upgrades = UpgradeManager.Instance;
        int cap = upgrades.GetMaxStacks(data);
        rarity.text = cap > 1 ? grade + "  ·  " + (upgrades.GetStacks(data) + 1) + "/" + cap : grade;
    }

    public void Layout(int index, int count, bool portrait)
    {
        root.anchorMin = root.anchorMax = root.pivot = Vector2.one * .5f;
        root.sizeDelta = new Vector2(290, 372);
        if (!portrait)
            root.anchoredPosition = new Vector2((index - (count - 1) * .5f) * 310, -30);
        else
        {
            int row = index / 2;
            int columns = Mathf.Min(2, count - row * 2);
            float y = count <= 2 ? 0 : 200 - row * 400;
            root.anchoredPosition = new Vector2((index % 2 - (columns - 1) * .5f) * 310, y);
        }
    }

    private static void SetLabel(TMP_Text label, float width, float height, float y, float size, TMP_FontAsset font, Color color)
    {
        if (label == null) return;
        var rect = label.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0,y);
        label.font = font;
        label.fontStyle = FontStyles.Normal;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.enableAutoSizing = true;
        label.fontSizeMin = Mathf.Min(14, size);
        label.fontSizeMax = label.fontSize = size;
        label.margin = Vector4.zero;
        label.raycastTarget = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
    }
}
