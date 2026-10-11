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
        AchievementEvents.RunStarted += HandleRunStarted;

        DailyMissionManager manager = DailyMissionManager.Instance;

        if (manager != null)
            manager.OnMissionCompleted += Enqueue;
    }

    private void OnDisable()
    {
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
            DailyMissionSaveData.Entry entry = pending.Dequeue();

            if (titleLabel != null)
            {
                titleLabel.text =
                    Lang.GetOr("mission.notification.title", "Миссия выполнена!")
                    + "\n" + DailyMissionText.GetTitle(entry);
            }

            if (rewardLabel != null)
                rewardLabel.text = DailyMissionText.GetRewardLabel(entry);

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
        scaler.matchWidthOrHeight = 0.5f;

        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;

        Image panel = CreatePanel();

        titleLabel = CreateText(panel.rectTransform, "Title", 26f, new Color(0.75f, 1f, 0.8f, 1f));
        RectTransform titleRect = titleLabel.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 0.5f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.offsetMin = new Vector2(20f, 0f);
        titleRect.offsetMax = new Vector2(-20f, -12f);

        rewardLabel = CreateText(panel.rectTransform, "Reward", 20f, new Color(0.8f, 0.9f, 1f, 1f));
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
        image.color = new Color(0.1f, 0.14f, 0.12f, 0.96f);
        image.raycastTarget = false;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(720f, 120f);
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
