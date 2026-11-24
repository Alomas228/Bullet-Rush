using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Shared frame for the calendar, progression lists and mastery collection.
public static class RuntimeWindow
{
    public static RectTransform Open(RectTransform host, TMP_Text title, Button back, int width, int height)
    {
        UiArtboard.Stretch(host);
        var dim = host.GetComponent<Image>() ?? host.gameObject.AddComponent<Image>();
        dim.sprite = null;
        dim.color = ArcadeUiTheme.Backdrop;
        dim.raycastTarget = true;
        foreach (string oldName in new[] { "BackgroundShapes", "Window", "Dim", "SkinDim" })
        {
            var old = host.Find(oldName);
            if (old != null) old.gameObject.SetActive(false);
        }
        var frame = UiArtboard.Create(host, new Vector2(width + 56, height + 56));
        var sheet = ArcadeUiFactory.At(frame, "Sheet", Vector2.one * .5f, Vector2.one * .5f,
            Vector2.zero, new Vector2(width, height));
        ArcadeUiFactory.Image(sheet, ArcadeKitAssets.CutPanel, MenuSheet.Paper, true).raycastTarget = true;
        if (title != null) MenuSheet.Label(title, sheet, 36, 21, width - 270, 58, 36, MenuSheet.Ink, heading: true);
        WindowCloseButton.Attach(sheet, back);
        MenuSheet.Box(sheet, "Rule", 36, 93, width - 72, 2, MenuSheet.Rule);
        return sheet;
    }

    public static void Text(TMP_Text label, Transform parent, float x, float y, float width, float height,
        float size, bool heading = false, TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        if (label != null)
            MenuSheet.Label(label, parent, x, y, width, height, size, heading ? MenuSheet.Ink : MenuSheet.Muted, align, heading);
    }

    public static RectTransform Ledger(RectTransform host, TMP_Text title, Button back, TMP_Text progress,
        RectTransform content, bool missions)
    {
        var sheet = Open(host, title, back, 1100, 718);
        Text(progress, sheet, missions ? 580 : 36, 106, missions ? 480 : 990, 44, 20,
            align: missions ? TextAlignmentOptions.Right : TextAlignmentOptions.Left);
        var scroll = content.GetComponentInParent<ScrollRect>(true);
        if (scroll != null)
        {
            MenuSheet.Place(scroll, sheet, 36, 168, 1028, 520);
            scroll.horizontal = false;
            if (scroll.viewport != null) UiArtboard.Stretch(scroll.viewport);
        }
        if (content.TryGetComponent<VerticalLayoutGroup>(out var rows))
        {
            rows.padding = new RectOffset(6,6,4,4);
            rows.spacing = 10;
            rows.childControlWidth = rows.childForceExpandWidth = rows.childControlHeight = true;
            rows.childForceExpandHeight = false;
        }
        return sheet;
    }

    public static void LedgerRow(RectTransform row, TMP_Text title, TMP_Text detail, TMP_Text reward,
        TMP_Text progress, Slider bar, Button claim)
    {
        var metrics = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
        metrics.minHeight = metrics.preferredHeight = 110;
        var legacy = row.GetComponent<LayoutGroup>();
        if (legacy != null) legacy.enabled = false;
        if (row.TryGetComponent<Image>(out var face)) MenuSheet.Surface(face, MenuSheet.Card);
        Text(title, row, 18, 9, 540, 35, 24, true);
        Text(detail, row, 18, 46, 540, 52, 18);
        Text(reward, row, 576, claim == null ? 24 : 10, 210, 40, 18);
        Text(progress, row, claim == null ? 812 : 576, claim == null ? 24 : 55, 184, 36, 17);
        if (bar != null) MenuSheet.Place(bar, row, 576, 94, 210, 6);
        if (claim != null) MenuSheet.Control(claim, row, 812, 28, 178, 54, MenuSheet.Ochre);
    }

    public static void Settings(RectTransform card)
    {
        card.sizeDelta = new Vector2(980,578);
        var sound = MenuSheet.Box(card, "SoundSection", 36,108, 460,354, MenuSheet.Card);
        var effects = MenuSheet.Box(card, "EffectsSection", 510,108,434,202, MenuSheet.Card);
        var language = MenuSheet.Box(card, "LanguageSection", 510,324,434,138, MenuSheet.Card);
        Place(card, "SoundTitle", sound.transform, 20,12, 390,42);
        Place(card, "EffectsTitle", effects.transform, 20,12, 390,40);
        Place(card, "LanguageTitle", language.transform, 20,12, 390,40);
        string[] channels = { "SfxRow", "MusicRow", "UiRow" };
        for (int i = 0; i < channels.Length; i++)
        {
            float y = 72 + i * 88;
            string channel = channels[i];
            Place(card, channel + "Label", sound.transform, 20,y,326,30);
            Place(card, channel + "Value", sound.transform, 348,y,86,30);
            Place(card, channel + "Bar", sound.transform, 20,y+42,414,16);
        }
        Place(card, "ShakeRow", effects.transform, 16,62,402,54);
        Place(card, "FireShakeRow", effects.transform, 16,126,402,54);
        int index = 0;
        foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
        {
            if (child.parent != card || !child.name.StartsWith("Lang", System.StringComparison.Ordinal) ||
                child.GetComponent<Button>() == null) continue;
            MenuSheet.Place(child, language.transform, 20 + index++ * 133,64,122,48);
        }
        MenuSheet.Box(card, "FooterRule",36,487,908,2,MenuSheet.Rule);
    }

    public static void Place(Transform source, string path, Transform target, float x, float y, float w, float h)
    {
        var child = source.Find(path);
        if (child != null) MenuSheet.Place(child, target, x,y,w,h);
    }
}
