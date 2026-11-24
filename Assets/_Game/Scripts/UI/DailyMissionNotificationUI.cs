using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Всплывающее уведомление «Миссия выполнена». По образцу
/// AchievementNotificationUI: слушает DailyMissionManager.OnMissionCompleted
/// и собирает канвас в рантайме, без отдельного префаба. Карточки
/// показываются по очереди, чтобы одновременное выполнение не
/// накладывалось друг на друга.
/// </summary>
public class DailyMissionNotificationUI : MonoBehaviour
{
    [SerializeField] private float visibleSeconds = 3.5f;
    [SerializeField] private float fadeSeconds = 0.35f;

    private static DailyMissionNotificationUI instance;

    public static DailyMissionNotificationUI Instance => instance;

    private CanvasGroup group;
    private TMP_Text titleLabel;
    private TMP_Text rewardLabel;

    private readonly Queue<DailyMissionSaveData.Entry> pending =
        new Queue<DailyMissionSaveData.Entry>();

    private Coroutine running;

    // =========================================================
    // BOOTSTRAP
    // =========================================================

    public static DailyMissionNotificationUI EnsureExists()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("DailyMissionNotificationUI");
        DontDestroyOnLoad(host);

        return host.AddComponent<DailyMissionNotificationUI>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        Build();
    }

    private void OnEnable()
    {
        if (running == null && pending.Count > 0) running = StartCoroutine(ShowQueue());
        AchievementEvents.RunStarted += HandleRunStarted;

        DailyMissionManager manager = DailyMissionManager.Instance;

        if (manager != null)
            manager.OnMissionCompleted += Enqueue;
    }

    private void OnDisable()
    {
        if (running != null) StopCoroutine(running);
        running = null;
        if (group != null) group.alpha = 0;
        RunNoticeSlot.Release(this);
        AchievementEvents.RunStarted -= HandleRunStarted;

        DailyMissionManager manager = DailyMissionManager.Instance;

        if (manager != null)
            manager.OnMissionCompleted -= Enqueue;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void HandleRunStarted()
    {
        // Новый забег — старые уведомления неактуальны.
        pending.Clear();
    }

    public void Enqueue(DailyMissionSaveData.Entry entry)
    {
        if (entry == null)
            return;

        pending.Enqueue(entry);

        if (running == null && isActiveAndEnabled)
            running = StartCoroutine(ShowQueue());
    }

    private IEnumerator ShowQueue()
    {
        while (pending.Count > 0)
        {
            while (!RunNoticeSlot.TryTake(this)) yield return null;
            if (pending.Count == 0) { RunNoticeSlot.Release(this); break; }
            DailyMissionSaveData.Entry entry = pending.Dequeue();

            if (titleLabel != null)
            {
                titleLabel.text =
                    Lang.GetOr("mission.notification.title", "Миссия выполнена!")
                    + "\n" + DailyMissionText.GetTitle(entry);
            }

            if (rewardLabel != null)
                rewardLabel.text = DailyMissionText.GetRewardLabel(entry);

            PlayNotificationSound();

            yield return FadeTo(1f);
            yield return new WaitForSecondsRealtime(visibleSeconds);
            yield return FadeTo(0f);
            RunNoticeSlot.Release(this);
        }

        running = null;
    }

    private IEnumerator FadeTo(float target)
    {
        if (group == null)
            yield break;

        float start = group.alpha;
        float timer = 0f;

        while (timer < fadeSeconds)
        {
            timer += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(start, target, timer / fadeSeconds);
            yield return null;
        }

        group.alpha = target;
    }

    private static void PlayNotificationSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlayUI(
                sfx.Notification
            );
    }

    // =========================================================
    // RUNTIME UI
    // =========================================================

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 510;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        ResponsiveUi.Configure(scaler, new Vector2(1920,1080));

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image panel = CreatePanel();

        ArcadeUiFactory.Image(
            ArcadeUiFactory.At(
                panel.rectTransform,
                "Accent",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(22f, 0f),
                new Vector2(8f, 64f)
            ),
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Green,
            true
        );

        ArcadeUiFactory.Icon(
            panel.rectTransform,
            "Icon",
            "target",
            ArcadeUiTheme.Green,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(44f, 0f),
            new Vector2(32f, 32f)
        );

        titleLabel = CreateText(
            panel.rectTransform,
            "Title",
            new Vector2(86f, 20f),
            new Vector2(350f, 50f),
            21f,
            MenuSheet.Ink,
            ArcadeKitAssets.Display
        );

        rewardLabel = CreateText(
            panel.rectTransform,
            "Reward",
            new Vector2(86f, -24f),
            new Vector2(350f, 30f),
            18f,
            ArcadeUiTheme.TextSecondary,
            ArcadeKitAssets.BodyBold
        );
    }

    private Image CreatePanel()
    {
        GameObject go = new GameObject("Notification", typeof(RectTransform));
        go.transform.SetParent(ResponsiveUi.SafeContent((RectTransform)transform), false);

        Image image = go.AddComponent<Image>();
        image.sprite = ArcadeKitAssets.CutPanel;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = new Vector2(448f, 104f);
        rect.anchoredPosition = new Vector2(-18f, 78f);
        rect.gameObject.AddComponent<ResponsiveRunNotice>();

        return image;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        TMP_FontAsset font)
    {
        RectTransform rect = ArcadeUiFactory.At(
            (RectTransform)parent,
            name,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            position,
            size
        );

        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.enableAutoSizing = true;
        text.fontSizeMin = 14;
        text.fontSizeMax = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        return text;
    }
}
