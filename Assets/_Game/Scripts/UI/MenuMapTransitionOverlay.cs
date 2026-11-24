using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A screen-space landscape wipe. It sits behind the menu, above the world.</summary>
public sealed class MenuMapTransitionOverlay : MonoBehaviour
{
    private RectTransform viewport;
    private RectTransform shutter;
    private RectTransform titleRoot;
    private CanvasGroup titleGroup;
    private int direction;

    public static MenuMapTransitionOverlay Create(GameMap target, int enterSide)
    {
        var host = new GameObject("Menu Map Transition", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(MenuMapTransitionOverlay));
        var canvas = host.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -1000;

        var scaler = host.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        ResponsiveUi.Configure(scaler, new Vector2(1920,1080));

        var overlay = host.GetComponent<MenuMapTransitionOverlay>();
        overlay.Initialize(target, enterSide);
        return overlay;
    }

    private void Initialize(GameMap target, int enterSide)
    {
        direction = enterSide < 0 ? -1 : 1;
        Color paper = ArcadeUiTheme.Rgb(0xF4EAD6);
        Color ink = ArcadeUiTheme.Rgb(0x293630);
        Color gold = ArcadeUiTheme.Rgb(0xDDA95A);
        Color theme = target != null ? target.skyColor : ink;
        Color panelColor = Color.Lerp(ink, theme, .27f);
        panelColor.a = 1;
        Color edgeColor = Color.Lerp(panelColor, theme, .28f);
        edgeColor.a = 1;
        Color accent = Color.Lerp(gold, theme, .24f);
        accent.a = 1;

        viewport = ArcadeUiFactory.Stretch(transform, "Viewport");
        viewport.gameObject.AddComponent<RectMask2D>();

        shutter = ArcadeUiFactory.At(viewport, "Transition leaf", Vector2.one * .5f,
            Vector2.one * .5f, Vector2.zero, new Vector2(2400, 1500));
        shutter.localRotation = Quaternion.Euler(0, 0, -6f);
        ArcadeUiFactory.Image(shutter, null, panelColor);
        AddEdge(shutter, false, edgeColor, accent);
        AddEdge(shutter, true, edgeColor, accent);

        titleRoot = ArcadeUiFactory.At(viewport, "Destination", new Vector2(.7f, .53f),
            Vector2.one * .5f, Vector2.zero, new Vector2(600f, 158f));
        titleGroup = titleRoot.gameObject.AddComponent<CanvasGroup>();
        titleGroup.blocksRaycasts = false;
        titleGroup.interactable = false;
        titleGroup.alpha = 0;

        var text = ArcadeUiFactory.Label(titleRoot, "Map name", target != null ? target.LocalizedName : "",
            ArcadeKitAssets.Display, 76, paper, TextAlignmentOptions.Center,
            Vector2.one * .5f, Vector2.one * .5f, Vector2.zero, new Vector2(600, 100));
        text.enableAutoSizing = true;
        text.fontSizeMin = 32;
        text.fontSizeMax = 76;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        var rule = ArcadeUiFactory.At(titleRoot, "Accent", new Vector2(.5f, 0f),
            Vector2.one * .5f, new Vector2(0, 14f), new Vector2(64f, 5f));
        ArcadeUiFactory.Image(rule, null, accent);

        Canvas.ForceUpdateCanvases();
        SetPhase(0);
    }

    private static void AddEdge(RectTransform panel, bool right, Color band, Color accent)
    {
        var anchor = right ? Vector2.right : Vector2.zero;
        var stripe = ArcadeUiFactory.Rect(panel, right ? "Right band" : "Left band",
            anchor, anchor + Vector2.up, new Vector2(right ? 1 : 0, .5f), Vector2.zero, new Vector2(28, 0));
        ArcadeUiFactory.Image(stripe, null, band);

        var trim = ArcadeUiFactory.Rect(panel, right ? "Right trim" : "Left trim",
            anchor, anchor + Vector2.up, new Vector2(right ? 1 : 0, .5f),
            new Vector2(right ? -28f : 28f, 0), new Vector2(7, 0));
        ArcadeUiFactory.Image(trim, null, accent);
    }

    public IEnumerator Cover(float duration)
    {
        yield return Animate(0f, 1f, duration);
    }

    public IEnumerator Reveal(float duration)
    {
        yield return Animate(1f, 2f, duration);
    }

    private IEnumerator Animate(float from, float to, float duration)
    {
        float elapsed = 0;
        SetPhase(from);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / Mathf.Max(.01f, duration)));
            SetPhase(Mathf.Lerp(from, to, t));
            yield return null;
        }
        SetPhase(to);
    }

    private void SetPhase(float phase)
    {
        Vector2 size = viewport.rect.size;
        if (size.x < 1 || size.y < 1) size = new Vector2(Screen.width, Screen.height);
        // Overscan keeps the slanted sheet opaque even at the viewport corners.
        float wide = size.x + size.y * .34f + 40f;
        float tall = size.y * 1.42f + 24f;
        shutter.sizeDelta = new Vector2(wide, tall);
        float travel = (wide + size.x) * .5f + size.y * .16f + 30f;
        shutter.anchoredPosition = Vector2.right * direction * (1f - phase) * travel;

        // Fit the heading to the part of the menu that actually shows the world.
        Rect safe = ResponsiveUi.SafeRect(viewport);
        bool portrait = safe.height > safe.width;
        float layoutScale = ResponsiveUi.Fit(safe.size, new Vector2(1320,1080), true);
        float left = portrait ? safe.xMin : safe.xMin + 748f * layoutScale;
        float center = (left + safe.xMax) * .5f;
        titleRoot.anchorMin = titleRoot.anchorMax = new Vector2((center - viewport.rect.xMin)/size.x, .53f);
        float titleWidth = Mathf.Min(600f, (safe.xMax-left)*.86f);
        titleRoot.sizeDelta = new Vector2(titleWidth, 158f);
        var label = (RectTransform)titleRoot.Find("Map name");
        label.sizeDelta = new Vector2(titleWidth, 100f);
        titleGroup.alpha = phase <= 1f
            ? Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - .90f) / .10f))
            : 1f - Mathf.SmoothStep(0, 1, Mathf.Clamp01((phase - 1f) / .16f));
    }
}
