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

        titleLabel = CreateText(rootRect, "Title", 26f, new Color(1f, 0.95f, 0.7f, 1f));
        RectTransform titleRect = titleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(20f, 0f);
        titleRect.offsetMax = new Vector2(-20f, -12f);

        rewardLabel = CreateText(rootRect, "Reward", 20f, new Color(0.8f, 0.9f, 1f, 1f));
        RectTransform rewardRect = rewardLabel.rectTransform;
        rewardRect.anchorMin = new Vector2(0f, 0f);
        rewardRect.anchorMax = new Vector2(1f, 0.5f);
        rewardRect.offsetMin = new Vector2(20f, 10f);
        rewardRect.offsetMax = new Vector2(-20f, 0f);
    }

    private Image CreatePanel()
    {
        GameObject go = new GameObject("Notification", typeof(RectTransform));
        go.transform.SetParent(transform, false);

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.1f, 0.12f, 0.18f, 0.96f);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(720f, 110f);
        rect.anchoredPosition = new Vector2(0f, -40f);

        return image;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float size,
        Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.raycastTarget = false;

        return text;
    }
}
