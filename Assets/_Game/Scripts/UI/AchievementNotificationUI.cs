using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Всплывающее уведомление «Достижение получено». Слушает
/// AchievementManager.OnAchievementUnlocked и собирает собственный
/// канвас в рантайме — отдельного ассета/префаба не требует.
/// Показывает карточки по очереди, чтобы одновременные разблокировки
/// не накладывались друг на друга.
/// </summary>
public class AchievementNotificationUI : MonoBehaviour
{
    [SerializeField] private float visibleSeconds = 3.5f;
    [SerializeField] private float fadeSeconds = 0.35f;

    private static AchievementNotificationUI instance;

    public static AchievementNotificationUI Instance => instance;

    private CanvasGroup group;
    private TMP_Text titleLabel;
    private TMP_Text rewardLabel;
    private RectTransform rootRect;

    private readonly Queue<AchievementData> pending = new Queue<AchievementData>();
    private Coroutine running;

    // =========================================================
    // BOOTSTRAP
    // =========================================================

    public static AchievementNotificationUI EnsureExists()
    {
        if (instance != null)
            return instance;

        GameObject host = new GameObject("AchievementNotificationUI");
        DontDestroyOnLoad(host);

        return host.AddComponent<AchievementNotificationUI>();
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
        AchievementEvents.RunStarted += HandleRunStarted;
    }

    private void OnDisable()
    {
        AchievementEvents.RunStarted -= HandleRunStarted;
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

    /// <summary>Вызывается AchievementManager при разблокировке.</summary>
    public void Enqueue(AchievementData data)
    {
        if (data == null)
            return;

        pending.Enqueue(data);

        if (running == null && isActiveAndEnabled)
            running = StartCoroutine(ShowQueue());
    }

    private IEnumerator ShowQueue()
    {
        while (pending.Count > 0)
        {
            AchievementData data = pending.Dequeue();

            if (titleLabel != null)
                titleLabel.text = Lang.GetOr("ach.notification.title", "Достижение получено!")
                    + "\n" + data.GetTitle();

            if (rewardLabel != null)
                rewardLabel.text = BuildRewardText(data);

            PlayNotificationSound();

            yield return FadeTo(1f);
            yield return new WaitForSecondsRealtime(visibleSeconds);
            yield return FadeTo(0f);
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

    private static string BuildRewardText(AchievementData data)
    {
        if (data.Rewards == null || data.Rewards.Count == 0)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < data.Rewards.Count; i++)
        {
            if (data.Rewards[i] == null)
                continue;

            if (builder.Length > 0)
                builder.Append("   ");

            builder.Append(data.Rewards[i].GetLabel());
        }

        return builder.ToString();
    }

    // =========================================================
    // RUNTIME UI
    // =========================================================

    private void Build()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image panel = CreatePanel();
        rootRect = panel.rectTransform;

        ArcadeUiFactory.Image(
            ArcadeUiFactory.At(
                rootRect,
                "Accent",
                new Vector2(0f, 0.5f),
                new Vector2(0f, 0.5f),
                new Vector2(22f, 0f),
                new Vector2(8f, 64f)
            ),
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Gold,
            true
        );

        ArcadeUiFactory.Icon(
            rootRect,
            "Icon",
            "trophy",
            ArcadeUiTheme.Gold,
            new Vector2(0f, 0.5f),
            new Vector2(0f, 0.5f),
            new Vector2(44f, 0f),
            new Vector2(32f, 32f)
        );

        titleLabel = CreateText(
            rootRect,
            "Title",
            new Vector2(86f, 20f),
            new Vector2(600f, 46f),
            23f,
            ArcadeUiTheme.Gold,
            ArcadeKitAssets.Display
        );

        rewardLabel = CreateText(
            rootRect,
            "Reward",
            new Vector2(86f, -24f),
            new Vector2(600f, 30f),
            18f,
            ArcadeUiTheme.TextSecondary,
            ArcadeKitAssets.BodyBold
        );
    }

    private Image CreatePanel()
    {
        GameObject go = new GameObject("Notification", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        Image image = go.AddComponent<Image>();
        image.sprite = ArcadeKitAssets.RoundRect;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = 1f;
        image.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(720f, 118f);
        rect.anchoredPosition = new Vector2(0f, -40f);

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
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Left;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;

        return text;
    }
}
