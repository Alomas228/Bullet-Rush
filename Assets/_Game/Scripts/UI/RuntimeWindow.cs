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
            scroll.vertical = true;
            if (scroll.viewport != null) UiArtboard.Stretch(scroll.viewport);
            content.anchorMin = new Vector2(0,1);
            content.anchorMax = new Vector2(1,1);
            content.pivot = new Vector2(.5f,1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0,content.sizeDelta.y);
            scroll.content = content;
            if (content.TryGetComponent<ContentSizeFitter>(out var fitter))
                fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
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
        metrics.minHeight = metrics.preferredHeight = 116;
        var legacy = row.GetComponent<LayoutGroup>();
        if (legacy != null) legacy.enabled = false;
        if (row.TryGetComponent<ContentSizeFitter>(out var fitter)) fitter.enabled = false;
        row.localScale = Vector3.one;
        if (row.TryGetComponent<Image>(out var face)) MenuSheet.Surface(face, MenuSheet.Card);
        // Use anchors proportional to the actual row width, after instantiation.
        // An inactive template has no reliable measured width.
        LedgerText(title,row,.105f,.575f,9,34,24,true);
        LedgerText(detail,row,.105f,.575f,43,52,19);
        LedgerText(reward,row,.595f,claim==null?.80f:.815f,13,45,18);
        LedgerText(progress,row,claim==null?.82f:.595f,claim==null?.98f:.805f,claim==null?24:59,30,18);
        if (bar != null)
        {
            MenuSheet.Place(bar,row,0,100,1,7);
            LedgerSpan((RectTransform)bar.transform,claim==null?.105f:.595f,claim==null?.98f:.805f,100,7);
            bar.interactable=false;
            if(bar.handleRect!=null) bar.handleRect.gameObject.SetActive(false);
            var fill=bar.fillRect!=null?bar.fillRect.GetComponent<Image>():null;
            if(fill!=null) fill.color=MenuSheet.Ochre;
            foreach(Image image in bar.GetComponentsInChildren<Image>(true))
                if(image!=fill&&(image.name=="Background"||image.name=="Track")) image.color=MenuSheet.Recess;
        }
        if (claim != null)
        {
            MenuSheet.Control(claim,row,0,29,170,54,MenuSheet.Ochre);
            LedgerSpan((RectTransform)claim.transform,.825f,.98f,29,54);
            var caption=claim.GetComponentInChildren<TMP_Text>(true);
            if(caption!=null)
            {
                UiArtboard.Stretch(caption.rectTransform);
                caption.rectTransform.offsetMin=new Vector2(6,0);
                caption.rectTransform.offsetMax=new Vector2(-6,0);
            }
        }
    }

    private static void LedgerText(TMP_Text label,RectTransform row,float left,float right,float y,float height,float size,bool heading=false)
    {
        if(label==null) return;
        Text(label,row,0,y,1,height,size,heading);
        LedgerSpan(label.rectTransform,left,right,y,height);
        label.alignment=TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode=TextWrappingModes.Normal;
    }

    private static void LedgerSpan(RectTransform rect,float left,float right,float y,float height)
    {
        rect.anchorMin=new Vector2(left,1);
        rect.anchorMax=new Vector2(right,1);
        rect.pivot=new Vector2(0,1);
        rect.anchoredPosition=new Vector2(0,-y);
        rect.sizeDelta=new Vector2(0,height);
    }

    public static Image LedgerBadge(RectTransform row,out Image completion)
    {
        var tile=MenuSheet.Box(row,"Emblem",13,18,79,79,MenuSheet.Ink);
        var symbol=ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(tile.rectTransform,"Symbol",13,13,13,13),null,MenuSheet.Ochre);
        symbol.preserveAspect=true;
        var check=ArcadeUiFactory.At(tile.rectTransform,"Completed",Vector2.one,Vector2.one,new Vector2(4,4),new Vector2(25,25));
        completion=ArcadeUiFactory.Image(check,ArcadeKitAssets.Icon("check"),MenuSheet.Sage);
        completion.preserveAspect=true;
        completion.gameObject.SetActive(false);
        return symbol;
    }

    public static string DamageIcon(DamageSource source) => source switch
    {
        DamageSource.Burn=>"flame",
        DamageSource.ChainLightning=>"power",
        DamageSource.Explosion=>"star",
        DamageSource.Bleed=>"heart",
        DamageSource.Ability=>"power",
        _=>"target"
    };

    public static void Settings(RectTransform card)
    {
        card.sizeDelta = new Vector2(980,642);
        var sound = MenuSheet.Box(card, "SoundSection", 36,108, 460,354, MenuSheet.Card);
        var effects = MenuSheet.Box(card, "EffectsSection", 510,108,434,266, MenuSheet.Card);
        var language = MenuSheet.Box(card, "LanguageSection", 510,388,434,138, MenuSheet.Card);
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
        Place(card, "CinematicRow", effects.transform, 16,190,402,54);
        int index = 0;
        foreach (Transform child in card.GetComponentsInChildren<Transform>(true))
        {
            if (child.parent != card || !child.name.StartsWith("Lang", System.StringComparison.Ordinal) ||
                child.GetComponent<Button>() == null) continue;
            MenuSheet.Place(child, language.transform, 20 + index++ * 133,64,122,48);
        }
        MenuSheet.Box(card, "FooterRule",36,551,908,2,MenuSheet.Rule);
    }

    public static void Place(Transform source, string path, Transform target, float x, float y, float w, float h)
    {
        var child = source.Find(path);
        if (child != null) MenuSheet.Place(child, target, x,y,w,h);
    }
}
