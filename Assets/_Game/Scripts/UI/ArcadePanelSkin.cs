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
        ("Canvas/GameOverPanel", ArcadePanelKind.GameOver),
        ("Canvas/UpgradePanel", ArcadePanelKind.LevelUp),
        ("Canvas/WavePanel", ArcadePanelKind.Wave),
        ("Canvas/TutorialPanel", ArcadePanelKind.Tutorial),
        ("Canvas/LeaderboardPanel", ArcadePanelKind.Leaderboard),
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
        kind = panelKind;

        Canvas.ForceUpdateCanvases();

        if (kind == ArcadePanelKind.Menu)
            ApplyMenuBackdrop();
        else if (kind != ArcadePanelKind.Hud &&
            kind != ArcadePanelKind.Wave &&
            kind != ArcadePanelKind.Tutorial)
            EnsureBackdrop();

        StyleButtons();
        StyleSliders();
        StyleImages();
        StyleTexts();
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
                        handle.sprite = ArcadeKitAssets.Knob;
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
            return ArcadeUiTheme.Green;

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

            if (name == "skindim")
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

            text.color = TextColor(name, text.color);
        }
    }

    private static Color TextColor(string name, Color color)
    {
        if (ContainsAny(name, "score", "coin", "rank", "final", "record", "gold"))
            return ArcadeUiTheme.Gold;

        if (ContainsAny(name, "secondary", "subtitle", "description", "hint"))
            return ArcadeUiTheme.TextSecondary;

        if (IsNearWhite(color))
            return ArcadeUiTheme.TextPrimary;

        Color normalized = NormalizeColor(color);

        if (normalized == color && color.grayscale < 0.35f)
            return ArcadeUiTheme.TextMuted;

        return normalized;
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

        if (color.r > 0.9f && color.g > 0.9f && color.b > 0.9f)
            return KeepAlpha(ArcadeUiTheme.TextPrimary, color.a);

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
