using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class RunOxygenHud : MonoBehaviour
{
    private static readonly Color Cyan = new Color32(80,206,230,255);
    private static readonly Color Amber = new Color32(255,194,85,255);
    private static readonly Color Red = new Color32(238,88,76,255);
    private RectTransform panel;
    private TMP_Text title, timer, iconText;
    private Image meter, icon;
    private int lastSeconds = -1, lastCapacity = -1;
    private bool lastPaused, lastEmpty, lastDanger;
    private LangCode language;

    public static bool Visible => MapSurvivalZones.Instance != null && MapSurvivalZones.Instance.OxygenActive &&
        GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Playing && !GameModeManager.IsPvp;

    public void Build(RectTransform vitals)
    {
        if (panel != null) return;
        Vector2 left = new Vector2(0f,1f), right = Vector2.one;
        panel = ArcadeUiFactory.At(vitals, "Oxygen", left, left, new Vector2(12,-99), new Vector2(236,56));
        ArcadeUiFactory.Image(panel, ArcadeKitAssets.CutPanel, MenuSheet.Ink, true);

        var badge = ArcadeUiFactory.At(panel, "Oxygen badge", left, left, new Vector2(8,-8), new Vector2(38,40));
        icon = ArcadeUiFactory.Image(badge, ArcadeKitAssets.RoundRect, new Color32(31,79,94,255), true);
        iconText = ArcadeUiFactory.Label(ArcadeUiFactory.Stretch(badge,"Oxygen symbol"), "O2", ArcadeKitAssets.BodyBold,
            18, Cyan, TextAlignmentOptions.Center);
        title = ArcadeUiFactory.Label(panel, "Oxygen title", "", ArcadeKitAssets.BodyBold, 14, MenuSheet.Paper,
            TextAlignmentOptions.Left, left, left, new Vector2(54,-6), new Vector2(99,22));
        timer = ArcadeUiFactory.Label(panel, "Oxygen timer", "", ArcadeKitAssets.BodyBold, 17, Cyan,
            TextAlignmentOptions.Right, right, right, new Vector2(-10,-6), new Vector2(80,22));
        title.textWrappingMode = timer.textWrappingMode = TextWrappingModes.NoWrap;
        title.enableAutoSizing = true;
        title.fontSizeMin = 10;
        title.fontSizeMax = 14;
        var track = ArcadeUiFactory.At(panel,"Oxygen track",left,left,new Vector2(54,-33),new Vector2(170,13));
        ArcadeUiFactory.Image(track, ArcadeKitAssets.RoundRect, new Color32(58,73,78,255), true);
        meter = ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(track,"Oxygen fill",2,2,2,2),ArcadeKitAssets.RoundRect,Cyan);
        meter.type = Image.Type.Filled;
        meter.fillMethod = Image.FillMethod.Horizontal;
        panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (panel == null) return;
        bool visible = Visible;
        if (panel.gameObject.activeSelf != visible) panel.gameObject.SetActive(visible);
        if (!visible) return;
        var oxygen = MapSurvivalZones.Instance;
        float seconds = oxygen.OxygenSeconds;
        float fraction = Mathf.Clamp01(seconds / Mathf.Max(1f,oxygen.OxygenCapacity));
        int display = Mathf.CeilToInt(seconds), capacity = Mathf.RoundToInt(oxygen.OxygenCapacity);
        bool paused = oxygen.OxygenSuspended;
        bool empty = seconds <= 0f;
        bool danger = seconds <= 4f;
        if (display != lastSeconds || capacity != lastCapacity || paused != lastPaused || empty != lastEmpty || danger != lastDanger || language != Lang.Code)
        {
            lastSeconds = display; lastCapacity = capacity; lastPaused = paused; lastEmpty = empty; lastDanger = danger; language = Lang.Code;
            title.text = empty ? Lang.GetOr("oxygen.empty", "NO OXYGEN") : danger ? Lang.GetOr("oxygen.low", "LOW OXYGEN") : Lang.GetOr("oxygen.title", "OXYGEN");
            timer.text = paused ? Lang.GetOr("oxygen.paused", "PAUSED") : $"{display} / {capacity}";
        }
        meter.fillAmount = fraction;
        Color color = empty || danger ? Red : fraction < .5f ? Color.Lerp(Amber,Cyan,fraction * 2f) : Cyan;
        if (danger && !paused) color = Color.Lerp(color, Color.white, .16f * (1f + Mathf.Sin(Time.unscaledTime * 6f)));
        meter.color = timer.color = iconText.color = color;
        icon.color = Color.Lerp(MenuSheet.Ink, color, .35f);
        title.color = empty || danger ? color : MenuSheet.Paper;
    }
}
