using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public enum ArcadePanelKind
{
    Menu,
    Settings,
    Hud,
    Wave,
    Pause,
    GameOver,
    LevelUp,
    Tutorial,
    Leaderboard
}

public sealed class ArcadePanelSkin : MonoBehaviour
{
    private static readonly (string Path, ArcadePanelKind Kind)[] Panels =
    {
        ("MenuPanelUI/Menu/UpgradesPanel", ArcadePanelKind.Menu),
        ("MenuPanelUI/Menu/EquipmentPanel", ArcadePanelKind.Menu),
        ("MenuPanelUI/Menu/MapsPanel", ArcadePanelKind.Menu),
        ("MenuPanelUI/Menu/DailyRewardPanel", ArcadePanelKind.Menu),
        ("MenuPanelUI/Menu/AchievementsPanel", ArcadePanelKind.Menu),
        ("MenuPanelUI/Menu/DailyMissionsPanel", ArcadePanelKind.Menu),
        ("MenuPanelUI/Menu/MasteryPanel", ArcadePanelKind.Menu),
        ("Canvas/GameOverPanel", ArcadePanelKind.GameOver),
        ("Canvas/WavePanel", ArcadePanelKind.Wave),
        ("Canvas/TutorialPanel", ArcadePanelKind.Tutorial),
        ("Canvas/HUD", ArcadePanelKind.Hud)
    };

    private readonly HashSet<Image> buttonImages = new HashSet<Image>();
    private readonly HashSet<TMP_Text> buttonLabels = new HashSet<TMP_Text>();

    private ArcadePanelKind kind;
    private bool applied;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;
        StyleScene();
    }

    private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StyleScene();
    }

    private static void StyleScene()
    {
        for (int i = 0; i < Panels.Length; i++)
        {
            Transform panel = FindPath(Panels[i].Path);

            if (panel == null)
                continue;

            ArcadePanelSkin skin = panel.GetComponent<ArcadePanelSkin>();

            if (skin == null)
                skin = panel.gameObject.AddComponent<ArcadePanelSkin>();

            skin.Apply(Panels[i].Kind);
        }
    }

    private static Transform FindPath(string path)
    {
        string[] parts = path.Split('/');
        GameObject[] roots = SceneManager.GetActiveScene().GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            if (roots[i].name != parts[0])
                continue;

            Transform current = roots[i].transform;

            for (int p = 1; p < parts.Length; p++)
            {
                current = FindDeep(current, parts[p]);

                if (current == null)
                    return null;
            }

            return current;
        }

        return null;
    }

    private static Transform FindDeep(Transform parent, string name)
    {
        if (parent.name == name)
            return parent;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindDeep(parent.GetChild(i), name);

            if (found != null)
                return found;
        }

        return null;
    }

    public void Apply(ArcadePanelKind panelKind)
    {
        if (applied)
            return;

        applied = true;
        // These panels build their own controls when first opened.
        if (GetComponent<EquipmentUI>() || GetComponent<PermanentUpgradesUI>() || GetComponent<MapSelectionUI>() ||
            GetComponent<DailyRewardUI>() || GetComponent<AchievementsUI>() || GetComponent<DailyMissionUI>() || GetComponent<MasteryPanelUI>())
            return;

        kind = panelKind;

        Canvas.ForceUpdateCanvases();

        if (kind == ArcadePanelKind.Menu)
            ApplyMenuBackdrop();
        else if (kind != ArcadePanelKind.GameOver && kind != ArcadePanelKind.Hud &&
            kind != ArcadePanelKind.Wave &&
            kind != ArcadePanelKind.Tutorial)
            EnsureBackdrop();

        StyleButtons();
        StyleSliders();
        StyleImages();
        StyleTexts();
        if (kind == ArcadePanelKind.GameOver) LayoutResults();
        if (kind == ArcadePanelKind.Wave) LayoutWave();
        if (kind == ArcadePanelKind.Tutorial) LayoutTutorial();
    }

    private void LayoutWave()
    {
        var rect = (RectTransform)transform;
        rect.anchorMin = new Vector2(.12f, .64f);
        rect.anchorMax = new Vector2(.88f, .64f);
        rect.pivot = Vector2.one * .5f;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(0, 116);
        var plate = GetComponent<Image>();
        if (plate != null)
        {
            plate.enabled = false;
            plate.raycastTarget = false;
        }
        var complete = transform.parent?.Find("HUD/GameplayUI/waveCompleteText");
        if (complete != null) complete.SetParent(rect, false);
        foreach (var label in GetComponentsInChildren<TMP_Text>(true))
        {
            label.rectTransform.pivot = Vector2.one * .5f;
            UiArtboard.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16,4);
            label.rectTransform.offsetMax = new Vector2(-16,-4);
            float size = label.name == "CountdownText" ? 68f : label.name == "WaveText" ? 36f : 32f;
            FitLabel(label, 16, size, MenuSheet.Paper);
            label.richText = true;
            label.outlineColor = MenuSheet.Ink;
            label.outlineWidth = .16f;
        }
    }

    private void LayoutTutorial()
    {
        var root = (RectTransform)transform;
        var safe = ResponsiveUi.SafeContent(root, "Tutorial content");
        // Keep the full-screen dimmer and serialized references intact.
        foreach (string name in new[] { "ProgressText", "ObjectiveText", "SkipButton",
                     "Image_Aim", "Image_WASD", "Image_Dash", "Image_Shoot" })
        {
            var child = root.Find(name) as RectTransform;
            if (child != null) child.SetParent(safe, false);
        }
        var title = safe.Find("ObjectiveText")?.GetComponent<TMP_Text>();
        if (title != null)
        {
            var r = title.rectTransform;
            r.anchorMin = new Vector2(.22f, 1);
            r.anchorMax = new Vector2(.75f, 1);
            r.pivot = new Vector2(.5f,1);
            r.anchoredPosition = new Vector2(0,-22);
            r.sizeDelta = new Vector2(0,74);
            FitLabel(title, 18, 26, MenuSheet.Paper);
        }
        var step = safe.Find("ProgressText")?.GetComponent<TMP_Text>();
        if (step != null)
        {
            UiArtboard.Place(step.rectTransform, new Vector2(0,1), new Vector2(0,1),
                new Vector2(20,-20), new Vector2(180,50));
            FitLabel(step, 16, 22, MenuSheet.Paper);
        }
        if (safe.Find("SkipButton") is RectTransform skip)
            UiArtboard.Place(skip, Vector2.one, Vector2.one, new Vector2(-20,-20), new Vector2(220,64));
        foreach (string name in new[] { "Image_Aim", "Image_WASD", "Image_Dash", "Image_Shoot" })
        {
            if (!(safe.Find(name) is RectTransform image)) continue;
            var frame = ArcadeUiFactory.Stretch(safe, name + " frame", 36, 108, 36, 108);
            if (name == "Image_WASD")
            {
                frame.anchorMin = new Vector2(0,.10f);
                frame.anchorMax = new Vector2(1,.58f);
                frame.offsetMin = new Vector2(36,0);
                frame.offsetMax = new Vector2(-36,0);
            }
            var board = UiArtboard.Create(frame, new Vector2(800,470));
            image.SetParent(board, false);
            UiArtboard.Place(image, Vector2.one*.5f, Vector2.one*.5f, Vector2.zero, new Vector2(760,430));
            if (image.TryGetComponent<Image>(out var art)) art.preserveAspect = true;
        }
        var dim = transform.Find("DimBackground")?.GetComponent<Image>();
        if (dim != null) { dim.sprite = null; dim.color = ArcadeUiTheme.Alpha(MenuSheet.Ink, .54f); dim.raycastTarget = false; }
    }

    private void LayoutResults()
    {
        var root = (RectTransform)transform;
        UiArtboard.Stretch(root);
        var dim = GetComponent<Image>();
        dim.sprite = null;
        dim.color = ArcadeUiTheme.Backdrop;
        dim.raycastTarget = true;
        var details = root.parent.Find("LeaderboardPanel") as RectTransform;
        var board = UiArtboard.Create(root, new Vector2(details != null ? 1520 : 940, 650));
        var mid = Vector2.one * .5f;
        var paper = ArcadeUiFactory.At(board, "ResultSheet", mid, mid, Vector2.zero, new Vector2(892,596));
        ArcadeUiFactory.Image(paper, ArcadeKitAssets.CutPanel, MenuSheet.Paper, true);
        if (details != null)
        {
            paper.anchoredPosition = new Vector2(-294,0);
            LayoutReport(details, board);
            root.gameObject.AddComponent<ResponsiveResultsLayout>().Bind(board,paper,details);
        }
        ResultRow(root, paper, "GameOverText", 244, 56);
        ResultRow(root, paper, "LevelUpText", 193, 32);
        ResultRow(root, paper, "TextLayot", 72, 186);
        ResultRow(root, paper, "LevelText", -48, 32);
        ResultRow(root, paper, "xpProgressBar", -86, 18);
        ResultRow(root, paper, "XpProgressText", -116, 34);
        ResultRow(root, paper, "ButtonLayot", -218, 82);
        var group = paper.Find("ButtonLayot")?.GetComponent<HorizontalLayoutGroup>();
        if (group != null)
        {
            group.spacing = 12;
            group.padding = new RectOffset(0, 0, 0, 0);
            group.childControlWidth = group.childControlHeight = true;
            group.childForceExpandWidth = group.childForceExpandHeight = true;
        }
        var stats = paper.Find("TextLayot")?.GetComponent<VerticalLayoutGroup>();
        if (stats != null)
        {
            stats.padding = new RectOffset(0,0,0,0);
            stats.spacing = 2;
            stats.childControlHeight = stats.childForceExpandHeight = true;
        }
        foreach (var label in paper.GetComponentsInChildren<TMP_Text>(true))
        {
            int max = label.name == "GameOverText" ? 38 : label.GetComponentInParent<Button>() != null ? 19 : 24;
            FitLabel(label, 15, max, MenuSheet.Ink);
        }
    }

    private static void LayoutReport(RectTransform report, RectTransform board)
    {
        report.SetParent(board, false);
        UiArtboard.Place(report, Vector2.one * .5f, Vector2.one * .5f, new Vector2(458,0),new Vector2(568,596));
        if (report.TryGetComponent<Image>(out var sheet)) MenuSheet.Surface(sheet, MenuSheet.Card);
        if (report.Find("Divider") is RectTransform divider) divider.gameObject.SetActive(false);
        string[] totals = { "BaseScoreText", "StyleBonusText", "FinalScoreText" };
        for (int i = 0; i < totals.Length; i++)
        {
            var label = report.Find(totals[i])?.GetComponent<TMP_Text>();
            RuntimeWindow.Text(label,report,24,22+i*37,338,35,i==2 ? 23 : 18,i==2);
        }
        MenuSheet.Box(report,"ReportRule",24,142,520,2,MenuSheet.Rule);
        ReportSection(report,"RankPanel",372,16,170,52);
        ReportSection(report,"StatsPanel",24,169,258,39);
        ReportSection(report,"SpeedPanel",294,169,250,56);
        ReportSection(report,"StylePanel",294,455,250,46);
    }

    private static void ReportSection(RectTransform report,string name,int x,int y,int width,int lineHeight)
    {
        if (!(report.Find(name) is RectTransform group)) return;
        bool visible = group.gameObject.activeSelf;
        var labels = group.GetComponentsInChildren<TMP_Text>(true);
        if (group.TryGetComponent<LayoutGroup>(out var layout)) layout.enabled = false;
        MenuSheet.Place(group,report,x,y,width,lineHeight*labels.Length);
        if (group.TryGetComponent<Image>(out var bg)) MenuSheet.Surface(bg,MenuSheet.Recess);
        for (int i=0;i<labels.Length;i++)
            RuntimeWindow.Text(labels[i],group,8,lineHeight*i,width-16,lineHeight,18);
        group.gameObject.SetActive(visible);
    }

    private static void ResultRow(Transform old, RectTransform sheet, string name, float y, float height)
    {
        if (!(old.Find(name) is RectTransform row)) return;
        row.SetParent(sheet, false);
        UiArtboard.Place(row, Vector2.one * .5f, Vector2.one * .5f, new Vector2(0,y), new Vector2(816,height));
    }

    private static void FitLabel(TMP_Text text, float min, float max, Color color)
    {
        text.font = max >= 30 ? ArcadeKitAssets.Display : ArcadeKitAssets.BodyBold;
        text.fontStyle = FontStyles.Normal;
        text.fontSizeMin = min;
        text.fontSizeMax = max;
        text.enableAutoSizing = true;
        text.alignment = TextAlignmentOptions.Center;
        text.margin = Vector4.zero;
        text.raycastTarget = false;
        text.color = color;
    }

    private void ApplyMenuBackdrop()
    {
        Image own = GetComponent<Image>();

        if (own != null)
        {
            own.sprite = null;
            own.type = Image.Type.Simple;
            own.raycastTarget = true;
            own.color = name == "MapsPanel"
                ? ArcadeUiTheme.Alpha(ArcadeUiTheme.Backdrop, 0f)
                : ArcadeUiTheme.Backdrop;
        }
        else
        {
            EnsureBackdrop();
        }

        Transform shapes = transform.Find("BackgroundShapes");

        if (shapes != null)
            shapes.gameObject.SetActive(false);

        Transform topPlate = transform.Find("TopBar/ArcadeUi_TopBarPlate");

        if (topPlate != null)
            topPlate.gameObject.SetActive(false);
    }

    private void EnsureBackdrop()
    {
        if (transform.Find("SkinDim") != null)
            return;

        GameObject go = new GameObject("SkinDim", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(transform, false);

        RectTransform rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image image = go.GetComponent<Image>();
        image.color = ArcadeUiTheme.Backdrop;
        image.raycastTarget = true;

        go.transform.SetAsFirstSibling();
    }

    /// <summary>
    /// Поддеревья, которые полностью оформляет ArcadeBattleSkin:
    /// пауза, настройки в бою и комбо. Этот скин их не трогает,
    /// иначе порядок применения скинов решает, кто победит.
    /// </summary>
    private static bool IsBattleSkinOwned(Transform transform)
    {
        Transform current = transform;

        while (current != null)
        {
            if (current.name == "SettingPanel" ||
                current.name == "PausePanel" ||
                current.name == "ComboPanel")
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void StyleButtons()
    {
        Button[] buttons = GetComponentsInChildren<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];

            if (button == null)
                continue;

            if (IsBattleSkinOwned(button.transform))
                continue;

            RectTransform rect = button.transform as RectTransform;

            if (rect == null)
                continue;

            Image image = button.targetGraphic as Image;

            if (image == null)
                image = button.GetComponent<Image>();

            if (image == null)
                continue;

            Vector2 size = SizeOf(rect);
            string name = button.name.ToLowerInvariant();

            buttonImages.Add(image);

            if (image.color.a < 0.05f)
                continue;

            if (size.x >= 240f && size.y >= 160f)
            {
                StyleCardButton(image, name);
                StyleCardText(button);
                continue;
            }

            bool tab = name.StartsWith("tab");
            bool primary = ContainsAny(
                name,
                "play",
                "start",
                "claim",
                "buy",
                "continue",
                "restart",
                "retry",
                "resume",
                "revive",
                "confirm",
                "select",
                "free",
                "x2",
                "watch",
                "equip",
                "action",
                "update"
            );
            bool secondary = ContainsAny(
                name,
                "back",
                "close",
                "menu",
                "exit",
                "skip",
                "cancel",
                "next",
                "previous",
                "prev",
                "settings",
                "gear",
                "pause",
                "reset"
            );

            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = 1f;
            image.raycastTarget = true;
            button.targetGraphic = image;
            button.transition = Selectable.Transition.None;

            if (kind == ArcadePanelKind.GameOver && button.name == "MenuButton") primary = true;

            if (tab)
            {
                image.sprite = ArcadeKitAssets.RoundRect;
                image.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.95f);
            }
            else if (primary)
            {
                image.sprite = ArcadeKitAssets.RoundRect;
                image.color = ArcadeUiTheme.Gold;
            }
            else if (secondary)
            {
                image.sprite = ArcadeKitAssets.RoundRect;
                image.color = ArcadeUiTheme.Alpha(
                    ArcadeUiTheme.PanelRaised,
                    0.95f
                );
            }
            else
            {
                image.sprite = ArcadeKitAssets.RoundRect;
                image.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.95f);
            }

            if (button.GetComponent<ArcadeHoverLift>() == null)
            {
                button.gameObject.AddComponent<ArcadeHoverLift>().Bind(
                    image,
                    primary ? 1.03f : 1.018f,
                    0.08f
                );
            }

            StyleButtonText(button, primary, secondary);
        }
    }

    private void StyleCardButton(Image image, string name)
    {
        image.sprite = ArcadeKitAssets.RoundRect;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.raycastTarget = true;

        Color tint = image.color;

        if (IsNearWhite(tint) || IsLegacyDark(tint))
        {
            tint = ArcadeUiTheme.Alpha(
                ContainsAny(name, "slot", "today")
                    ? ArcadeUiTheme.PanelRaised
                    : ArcadeUiTheme.Panel,
                0.97f
            );
        }
        else
        {
            tint = NormalizeColor(tint);
        }

        image.color = tint;
    }

    private void StyleCardText(Button button)
    {
        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];

            if (label == null)
                continue;

            string name = label.name.ToLowerInvariant();

            if (name.Contains("desc") || name.Contains("hint"))
            {
                label.font = ArcadeKitAssets.Body;
                label.color = ArcadeUiTheme.TextSecondary;
            }
            else if (name.Contains("name") || name.Contains("title"))
            {
                label.font = ArcadeKitAssets.Display;
            }
            else if (name.Contains("progress") || name.Contains("level"))
            {
                label.font = ArcadeKitAssets.BodyBold;
                label.color = ArcadeUiTheme.Gold;
            }
            else
            {
                label.font = ArcadeKitAssets.BodyBold;
            }

            buttonLabels.Add(label);
        }
    }

    private void StyleButtonText(Button button, bool primary, bool secondary)
    {
        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < labels.Length; i++)
        {
            TMP_Text label = labels[i];

            if (label == null)
                continue;

            label.font = label.fontSize >= 24f
                ? ArcadeKitAssets.Display
                : ArcadeKitAssets.BodyBold;

            if (primary)
            {
                label.color = ArcadeUiTheme.TextDark;

                if (label.text != null && label.text.Length <= 28)
                    label.fontStyle = FontStyles.UpperCase;
            }
            else if (secondary)
            {
                label.color = ArcadeUiTheme.TextPrimary;
            }
            else
            {
                label.color = ArcadeUiTheme.TextPrimary;
            }

            buttonLabels.Add(label);
        }
    }

    private void StyleSliders()
    {
        Slider[] sliders = GetComponentsInChildren<Slider>(true);

        for (int i = 0; i < sliders.Length; i++)
        {
            Slider slider = sliders[i];

            if (slider == null)
                continue;

            if (IsBattleSkinOwned(slider.transform))
                continue;

            bool settings = IsSettingsSlider(slider);
            Image background = FindSliderBackground(slider);

            if (background != null)
            {
                background.sprite = ArcadeKitAssets.BarTrack;
                background.type = Image.Type.Sliced;
                background.pixelsPerUnitMultiplier = 1f;
                background.color = ArcadeUiTheme.BackgroundDeep;
                background.raycastTarget = settings;
                buttonImages.Add(background);
            }

            if (slider.fillRect != null)
            {
                Image fill = slider.fillRect.GetComponent<Image>();

                if (fill != null)
                {
                    fill.sprite = ArcadeKitAssets.BarFill;
                    fill.type = Image.Type.Sliced;
                    fill.pixelsPerUnitMultiplier = 1f;
                    fill.color = SliderColor(slider.name);
                    buttonImages.Add(fill);
                }
            }

            if (settings)
            {
                if (slider.handleRect != null)
                {
                    Image handle = slider.handleRect.GetComponent<Image>();

                    if (handle != null)
                    {
                        handle.sprite = ArcadeKitAssets.SliderHandle;
                        handle.type = Image.Type.Simple;
                        handle.color = Color.white;
                        handle.raycastTarget = true;
                        buttonImages.Add(handle);
                    }
                }
            }
            else
            {
                slider.interactable = false;

                if (slider.handleRect != null)
                    slider.handleRect.gameObject.SetActive(false);
            }
        }
    }

    private static bool IsSettingsSlider(Slider slider)
    {
        string name = slider.name.ToLowerInvariant();

        if (ContainsAny(name, "music", "sfx", "volume", "sound", "audio"))
            return true;

        Transform parent = slider.transform.parent;

        return parent != null &&
            parent.name.ToLowerInvariant().Contains("setting");
    }

    private static Image FindSliderBackground(Slider slider)
    {
        Transform found = slider.transform.Find("Background");

        if (found != null)
            return found.GetComponent<Image>();

        for (int i = 0; i < slider.transform.childCount; i++)
        {
            Transform child = slider.transform.GetChild(i);

            if (slider.fillRect != null &&
                child == slider.fillRect.parent)
            {
                continue;
            }

            string name = child.name.ToLowerInvariant();

            if (name.Contains("background") || name.Contains("track"))
                return child.GetComponent<Image>();
        }

        return null;
    }

    private static Color SliderColor(string name)
    {
        string lower = name.ToLowerInvariant();

        if (lower.Contains("health") || lower.Contains("hp"))
            return ArcadeUiTheme.Red;

        if (lower.Contains("dash"))
            return ArcadeUiTheme.Cyan;

        if (lower.Contains("fire") || lower.Contains("bomb"))
            return ArcadeUiTheme.Orange;

        if (lower.Contains("boss"))
            return ArcadeUiTheme.Red;

        if (lower.Contains("shield"))
            return ArcadeUiTheme.Purple;

        return ArcadeUiTheme.Gold;
    }

    private void StyleImages()
    {
        List<Image> images = new List<Image>(GetComponentsInChildren<Image>(true));

        Image own = GetComponent<Image>();

        for (int i = 0; i < images.Count; i++)
        {
            Image image = images[i];

            if (image == null ||
                !image.enabled ||
                buttonImages.Contains(image) ||
                IsBattleSkinOwned(image.transform) ||
                (kind == ArcadePanelKind.Menu && image == own))
            {
                continue;
            }

            if (image.GetComponent<Mask>() != null)
                continue;

            string name = image.name.ToLowerInvariant();

            if (name == "skindim" || name == "dimbackground" || (kind == ArcadePanelKind.Hud && image == own))
                continue;

            // Комбо показывается без плашки — фон не трогаем,
            // его отключает ArcadeBattleSkin.
            if (name == "combopanel")
                continue;

            if (image.color.a < 0.05f)
                continue;

            Vector2 size = SizeOf(image.rectTransform);
            bool protectedName = ContainsAny(
                name,
                "icon",
                "art",
                "preview",
                "thumb",
                "glow",
                "line",
                "divider",
                "frame",
                "border",
                "mask",
                "dot",
                "shadow",
                "vignette",
                "gradient",
                "spark",
                "fill",
                "land",
                "sky",
                "dim"
            );

            bool containerName = ContainsAny(
                name,
                "background",
                "panel",
                "card",
                "surface",
                "row",
                "slot",
                "box",
                "container",
                "chip",
                "pill",
                "tag",
                "block",
                "topbar",
                "header",
                "footer",
                "tab",
                "price",
                "state"
            );

            if (!protectedName &&
                containerName &&
                size.x >= 120f &&
                size.y >= 48f)
            {
                StyleContainer(image, size);
            }
            else if (!protectedName &&
                image.sprite == null &&
                image.color.a > 0.08f &&
                size.x >= 200f &&
                size.y >= 80f)
            {
                StyleContainer(image, size);
            }

            image.color = NormalizeColor(image.color);
        }
    }

    private static void StyleContainer(Image image, Vector2 size)
    {
        bool large = size.x >= 700f && size.y >= 360f;
        bool medium = size.y >= 110f || size.x >= 280f;

        image.sprite = ArcadeKitAssets.RoundRect;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;

        Color color = image.color;

        if (IsNearWhite(color) || IsLegacyDark(color))
        {
            color = large
                ? ArcadeUiTheme.Panel
                : medium
                    ? ArcadeUiTheme.PanelRaised
                    : ArcadeUiTheme.Card;
        }
        else
        {
            color = NormalizeColor(color);
        }

        color.a = Mathf.Max(color.a, 0.9f);
        image.color = color;
    }

    private void StyleTexts()
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

        for (int i = 0; i < texts.Length; i++)
        {
            TMP_Text text = texts[i];

            if (text == null || buttonLabels.Contains(text))
                continue;

            if (IsBattleSkinOwned(text.transform))
                continue;

            string name = text.name.ToLowerInvariant();

            text.font = text.fontSize >= 26f
                ? ArcadeKitAssets.Display
                : ContainsAny(name, "title", "header", "score", "coin", "wave", "level", "rank", "value", "name")
                    ? ArcadeKitAssets.BodyBold
                    : ArcadeKitAssets.Body;

            bool title = ContainsAny(name, "title", "header", "gameover") ||
                text.fontSize >= 40f;

            if (title && text.text != null && text.text.Length < 40)
                text.fontStyle |= FontStyles.UpperCase;

            text.color = kind == ArcadePanelKind.Hud || kind == ArcadePanelKind.Wave || kind == ArcadePanelKind.Tutorial
                ? MenuSheet.Paper : TextColor(name, text.color);
        }
    }

    private static Color TextColor(string name, Color color)
    {
        if (ContainsAny(name, "score", "coin", "rank", "final", "record", "gold"))
            return ArcadeUiTheme.Ink;

        if (ContainsAny(name, "secondary", "subtitle", "description", "hint"))
            return ArcadeUiTheme.TextSecondary;

        if (IsNearWhite(color))
            return ArcadeUiTheme.TextPrimary;

        Color normalized = NormalizeColor(color);

        if (normalized == color && color.grayscale < 0.35f)
            return ArcadeUiTheme.TextMuted;

        return normalized.grayscale > .53f ? ArcadeUiTheme.TextPrimary : normalized;
    }

    private static Color NormalizeColor(Color color)
    {
        if (color.a <= 0.01f)
            return color;

        if (Matches(color, 0x050914) ||
            Matches(color, 0x02040A) ||
            Matches(color, 0x0B1428))
        {
            return KeepAlpha(ArcadeUiTheme.Panel, color.a);
        }

        if (Matches(color, 0x111E3B) || Matches(color, 0x172A52))
            return KeepAlpha(ArcadeUiTheme.PanelRaised, color.a);

        if (Matches(color, 0x060C1A))
            return KeepAlpha(ArcadeUiTheme.BackgroundDeep, color.a);

        if (Matches(color, 0x41F2FF))
            return KeepAlpha(ArcadeUiTheme.Cyan, color.a);

        if (Matches(color, 0xFF3E8A))
            return KeepAlpha(ArcadeUiTheme.Red, color.a);

        if (Matches(color, 0xFFC857) || Matches(color, 0xFFC83D))
            return KeepAlpha(ArcadeUiTheme.Gold, color.a);

        if (Matches(color, 0xB8FF4D) || Matches(color, 0x4BF0A7))
            return KeepAlpha(ArcadeUiTheme.Green, color.a);

        if (Matches(color, 0xFF5C7A))
            return KeepAlpha(ArcadeUiTheme.Red, color.a);

        if (Matches(color, 0x8A6CFF))
            return KeepAlpha(ArcadeUiTheme.Purple, color.a);

        if (Matches(color, 0xFFA83D) || Matches(color, 0xFF9D2E))
            return KeepAlpha(ArcadeUiTheme.Orange, color.a);

        if (Matches(color, 0xF4F8FF) || Matches(color, 0xF4F7FF))
            return KeepAlpha(ArcadeUiTheme.TextPrimary, color.a);

        if (Matches(color, 0xA6B6D6) || Matches(color, 0xADBBD4))
            return KeepAlpha(ArcadeUiTheme.TextSecondary, color.a);

        if (Matches(color, 0x64769A) || Matches(color, 0x7385A6))
            return KeepAlpha(ArcadeUiTheme.TextMuted, color.a);


        return color;
    }

    private static bool ContainsAny(string value, params string[] parts)
    {
        for (int i = 0; i < parts.Length; i++)
        {
            if (value.Contains(parts[i]))
                return true;
        }

        return false;
    }

    private static bool IsNearWhite(Color color)
    {
        return color.r > 0.9f && color.g > 0.9f && color.b > 0.9f;
    }

    private static bool IsLegacyDark(Color color)
    {
        return color.r < 0.12f && color.g < 0.16f && color.b < 0.24f;
    }

    private static bool Matches(Color color, int rgb)
    {
        Color other = ArcadeUiTheme.Rgb(rgb);
        return Mathf.Abs(color.r - other.r) < 0.02f &&
            Mathf.Abs(color.g - other.g) < 0.02f &&
            Mathf.Abs(color.b - other.b) < 0.02f;
    }

    private static Color KeepAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    private static Vector2 SizeOf(RectTransform rect)
    {
        if (rect == null)
            return Vector2.zero;

        Vector2 size = rect.rect.size;

        if (size.x <= 1f || size.y <= 1f)
            size = rect.sizeDelta;

        return new Vector2(Mathf.Abs(size.x), Mathf.Abs(size.y));
    }
}
