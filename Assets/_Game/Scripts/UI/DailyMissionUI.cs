using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Экран ежедневных миссий. По устройству повторяет AchievementsUI:
/// панель собирается в сцене Editor-билдером (DailyMissionsUIBuilder),
/// а если её нет — экран поднимает собственный рантайм-оверлей
/// (OpenStandalone), чтобы система работала и до прогона билдера.
/// </summary>
public enum MissionTrack
{
    Daily,
    Weekly
}

public class DailyMissionUI : MonoBehaviour, ILangRefreshable
{
    [Header("Panel (optional — build happens in editor)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform content;
    [SerializeField] private DailyMissionRowUI rowTemplate;

    [Header("Mission tabs")]
    [SerializeField] private Button dailyTabButton;
    [SerializeField] private Button weeklyTabButton;

    [Header("Standalone fallback")]
    [Tooltip("Размер окна, если UI собран в рантайме (OpenStandalone).")]
    [SerializeField] private Vector2 standaloneSize = new Vector2(1040f, 720f);

    private static DailyMissionUI standalone;
    private readonly List<DailyMissionRowUI> rows = new List<DailyMissionRowUI>();
    private int builtCount = -1;
    private MissionTrack currentTrack = MissionTrack.Daily;

    // =========================================================
    // LIFECYCLE
    // =========================================================

    private void Awake()
    {
        if (titleText == null)
            titleText = FindText("Title");

        if (content == null)
        {
            Transform found = FindDeep(transform, "Content");

            if (found != null)
                content = found as RectTransform;
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);

        EnsureTabButtons();
        if (dailyTabButton != null)
            dailyTabButton.onClick.AddListener(ShowDaily);
        if (weeklyTabButton != null)
            weeklyTabButton.onClick.AddListener(ShowWeekly);
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();

        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
        if (dailyTabButton != null)
            dailyTabButton.onClick.RemoveListener(ShowDaily);
        if (weeklyTabButton != null)
            weeklyTabButton.onClick.RemoveListener(ShowWeekly);
    }

    private void Subscribe()
    {
        DailyMissionManager daily = DailyMissionManager.Instance;
        WeeklyMissionManager weekly = WeeklyMissionManager.Instance;
        if (daily != null)
        {
            daily.OnProgressChanged += Refresh;
            daily.OnMissionsRefreshed += HandleMissionsRefreshed;
        }
        if (weekly != null)
        {
            weekly.OnProgressChanged += Refresh;
            weekly.OnMissionsRefreshed += HandleMissionsRefreshed;
        }
    }

    private void Unsubscribe()
    {
        DailyMissionManager daily = DailyMissionManager.Instance;
        WeeklyMissionManager weekly = WeeklyMissionManager.Instance;
        if (daily != null)
        {
            daily.OnProgressChanged -= Refresh;
            daily.OnMissionsRefreshed -= HandleMissionsRefreshed;
        }
        if (weekly != null)
        {
            weekly.OnProgressChanged -= Refresh;
            weekly.OnMissionsRefreshed -= HandleMissionsRefreshed;
        }
    }

    private void EnsureTabButtons()
    {
        if (dailyTabButton == null)
        {
            Transform daily = FindDeep(transform, "DailyTab");
            if (daily != null)
                dailyTabButton = daily.GetComponent<Button>();
        }

        if (weeklyTabButton == null)
        {
            Transform weekly = FindDeep(transform, "WeeklyTab");
            if (weekly != null)
                weeklyTabButton = weekly.GetComponent<Button>();
        }

        if (dailyTabButton != null && weeklyTabButton != null)
            return;

        Transform parent = titleText != null
            ? titleText.transform.parent
            : transform;

        if (dailyTabButton == null)
            dailyTabButton = CreateTabButton(parent, "DailyTab", "DAILY", -180f);
        if (weeklyTabButton == null)
            weeklyTabButton = CreateTabButton(parent, "WeeklyTab", "WEEKLY", -20f);
    }

    private static Button CreateTabButton(
        Transform parent,
        string name,
        string fallback,
        float x)
    {
        Image image = CreateImage(parent, name, new Color(0.12f, 0.2f, 0.3f, 1f));
        RectTransform rect = image.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(140f, 42f);
        rect.anchoredPosition = new Vector2(x, -86f);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        TMP_Text label = CreateText(image.transform, "Label", 18f, TextAlignmentOptions.Center);
        label.text = fallback;
        Stretch(label.rectTransform);
        return button;
    }

    private void HandleMissionsRefreshed()
    {
        builtCount = -1;
        Refresh();
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void ShowPanel()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void ShowDaily() => SetTrack(MissionTrack.Daily);
    public void ShowWeekly() => SetTrack(MissionTrack.Weekly);

    private void SetTrack(MissionTrack track)
    {
        currentTrack = track;
        builtCount = -1;
        Refresh();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    public void RefreshLang()
    {
        Refresh();
    }

    public static void OpenStandalone()
    {
        if (standalone == null)
        {
            GameObject host = new GameObject("DailyMissionUI (runtime)");
            DontDestroyOnLoad(host);

            standalone = host.AddComponent<DailyMissionUI>();
            standalone.BuildRuntimeOverlay();
        }

        standalone.ShowPanel();
    }

    public static void CloseStandalone()
    {
        if (standalone != null)
            standalone.HidePanel();
    }

    // =========================================================
    // REFRESH
    // =========================================================

    public void Refresh()
    {
        DailyMissionManager daily = null;
        WeeklyMissionManager weekly = null;
        IReadOnlyList<DailyMissionSaveData.Entry> missions;
        int completed;
        int total;

        if (currentTrack == MissionTrack.Weekly)
        {
            weekly = WeeklyMissionManager.EnsureExists();
            weekly.EnsureCurrentWeek();
            missions = weekly.Missions;
            completed = weekly.CompletedCount;
            total = weekly.TotalCount;
        }
        else
        {
            daily = DailyMissionManager.EnsureExists();
            daily.EnsureToday();
            missions = daily.Missions;
            completed = daily.CompletedCount;
            total = daily.TotalCount;
        }

        if (titleText != null)
            titleText.text = currentTrack == MissionTrack.Weekly
                ? Lang.GetOr("mission.weekly.title", "ЕЖЕНЕДЕЛЬНЫЕ МИССИИ")
                : Lang.GetOr("mission.title", "ЕЖЕДНЕВНЫЕ МИССИИ");

        if (progressText != null)
        {
            progressText.text = Lang.GetOr("mission.completed", "Выполнено: {0} / {1}");
            progressText.text = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                progressText.text,
                completed,
                total);
        }

        if (content == null || rowTemplate == null)
            return;

        RebuildRowsIfNeeded(total);
        UpdateRows(missions);
    }

    private void RebuildRowsIfNeeded(int total)
    {
        if (builtCount == total && rows.Count == total)
            return;

        builtCount = total;

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
                Destroy(rows[i].gameObject);
        }

        rows.Clear();

        for (int i = 0; i < total; i++)
        {
            DailyMissionRowUI row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }
    }

    private void UpdateRows(IReadOnlyList<DailyMissionSaveData.Entry> missions)
    {
        for (int i = 0; i < rows.Count && i < missions.Count; i++)
            rows[i].Bind(missions[i], currentTrack);
    }

    // =========================================================
    // RUNTIME OVERLAY BUILDER
    // =========================================================

    private void BuildRuntimeOverlay()
    {
        EnsureEventSystem();

        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        gameObject.AddComponent<GraphicRaycaster>();

        RectTransform root = gameObject.GetComponent<RectTransform>();

        Image dim = CreateImage(root, "Dim", new Color(0f, 0f, 0f, 0.85f));
        Stretch(dim.rectTransform);

        Image window = CreateImage(root, "Window", new Color(0.09f, 0.1f, 0.14f, 1f));
        RectTransform windowRect = window.rectTransform;
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = standaloneSize;
        windowRect.anchoredPosition = Vector2.zero;

        titleText = CreateText(windowRect, "Title", 40f, TextAlignmentOptions.Left);
        RectTransform titleRect = titleText.rectTransform;
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-300f, 70f);
        titleRect.anchoredPosition = new Vector2(20f, -20f);

        progressText = CreateText(windowRect, "Progress", 26f, TextAlignmentOptions.Right);
        RectTransform progressRect = progressText.rectTransform;
        progressRect.anchorMin = new Vector2(1f, 1f);
        progressRect.anchorMax = new Vector2(1f, 1f);
        progressRect.pivot = new Vector2(1f, 1f);
        progressRect.sizeDelta = new Vector2(260f, 60f);
        progressRect.anchoredPosition = new Vector2(-100f, -20f);

        Image closeImage = CreateImage(windowRect, "CloseButton", new Color(0.5f, 0.12f, 0.14f, 1f));
        RectTransform closeRect = closeImage.rectTransform;
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(60f, 60f);
        closeRect.anchoredPosition = new Vector2(-20f, -20f);

        closeButton = closeImage.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(HidePanel);

        TMP_Text closeLabel = CreateText(closeRect, "X", 34f, TextAlignmentOptions.Center);
        Stretch(closeLabel.rectTransform);

        GameObject scrollGo = new GameObject("Scroll", typeof(RectTransform));
        scrollGo.transform.SetParent(windowRect, false);
        RectTransform scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = Vector2.zero;
        scrollRect.anchorMax = Vector2.one;
        scrollRect.offsetMin = new Vector2(20f, 20f);
        scrollRect.offsetMax = new Vector2(-20f, -100f);

        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;

        Image viewport = CreateImage(scrollRect, "Viewport", new Color(0f, 0f, 0f, 0.25f));
        Stretch(viewport.rectTransform);
        viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

        GameObject contentGo = new GameObject("Content", typeof(RectTransform));
        contentGo.transform.SetParent(viewport.rectTransform, false);
        content = contentGo.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0f, 0f);

        VerticalLayoutGroup layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport.rectTransform;
        scroll.content = content;

        rowTemplate = BuildRuntimeRowTemplate();
    }

    private DailyMissionRowUI BuildRuntimeRowTemplate()
    {
        GameObject rowGo = new GameObject("RowTemplate", typeof(RectTransform));
        rowGo.transform.SetParent(transform, false);

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 150f);

        Image bg = rowGo.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.06f);

        LayoutElement element = rowGo.AddComponent<LayoutElement>();
        element.minHeight = 150f;
        element.preferredHeight = 150f;

        TMP_Text rowTitle = CreateText(rowRect, "Title", 28f, TextAlignmentOptions.TopLeft);
        RectTransform rowTitleRect = rowTitle.rectTransform;
        rowTitleRect.anchorMin = new Vector2(0f, 1f);
        rowTitleRect.anchorMax = new Vector2(1f, 1f);
        rowTitleRect.pivot = new Vector2(0.5f, 1f);
        rowTitleRect.sizeDelta = new Vector2(-220f, 40f);
        rowTitleRect.anchoredPosition = new Vector2(0f, -12f);

        TMP_Text rowDesc = CreateText(rowRect, "Description", 22f, TextAlignmentOptions.TopLeft);
        rowDesc.color = new Color(0.8f, 0.82f, 0.88f, 1f);
        RectTransform rowDescRect = rowDesc.rectTransform;
        rowDescRect.anchorMin = new Vector2(0f, 0f);
        rowDescRect.anchorMax = new Vector2(1f, 1f);
        rowDescRect.offsetMin = new Vector2(12f, 44f);
        rowDescRect.offsetMax = new Vector2(-220f, -52f);

        TMP_Text rowReward = CreateText(rowRect, "Reward", 20f, TextAlignmentOptions.BottomLeft);
        rowReward.color = new Color(0.8f, 0.9f, 1f, 1f);
        RectTransform rowRewardRect = rowReward.rectTransform;
        rowRewardRect.anchorMin = new Vector2(0f, 0f);
        rowRewardRect.anchorMax = new Vector2(1f, 0f);
        rowRewardRect.pivot = new Vector2(0.5f, 0f);
        rowRewardRect.sizeDelta = new Vector2(-220f, 34f);
        rowRewardRect.anchoredPosition = new Vector2(0f, 8f);

        TMP_Text rowProgress = CreateText(rowRect, "Progress", 20f, TextAlignmentOptions.Right);
        RectTransform rowProgressRect = rowProgress.rectTransform;
        rowProgressRect.anchorMin = new Vector2(1f, 1f);
        rowProgressRect.anchorMax = new Vector2(1f, 1f);
        rowProgressRect.pivot = new Vector2(1f, 1f);
        rowProgressRect.sizeDelta = new Vector2(200f, 34f);
        rowProgressRect.anchoredPosition = new Vector2(-12f, -14f);

        Slider progressBar = CreateProgressBar(
            rowRect,
            "ProgressBar",
            new Color(0.3f, 0.75f, 0.45f, 0.9f)
        );

        RectTransform barRect = progressBar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0f, 0f);
        barRect.sizeDelta = new Vector2(-24f, 6f);
        barRect.anchoredPosition = new Vector2(0f, 46f);

        Image claimImage = CreateImage(rowRect, "ClaimButton", new Color(0.2f, 0.55f, 0.3f, 1f));
        RectTransform claimRect = claimImage.rectTransform;
        claimRect.anchorMin = new Vector2(1f, 0.5f);
        claimRect.anchorMax = new Vector2(1f, 0.5f);
        claimRect.pivot = new Vector2(1f, 0.5f);
        claimRect.sizeDelta = new Vector2(180f, 54f);
        claimRect.anchoredPosition = new Vector2(-12f, -6f);

        Button claim = claimImage.gameObject.AddComponent<Button>();
        claim.targetGraphic = claimImage;

        TMP_Text claimLabel = CreateText(claimRect, "Label", 22f, TextAlignmentOptions.Center);
        claimLabel.text = Lang.GetOr("mission.claim", "ПОЛУЧИТЬ");
        Stretch(claimLabel.rectTransform);

        DailyMissionRowUI row = rowGo.AddComponent<DailyMissionRowUI>();
        row.Configure(rowTitle, rowDesc, rowProgress, rowReward, bg, progressBar, claim, claimLabel);

        rowGo.SetActive(false);

        return row;
    }

    // =========================================================
    // SMALL UI HELPERS
    // =========================================================

    private static Slider CreateProgressBar(
        Transform parent,
        string name,
        Color fillColor)
    {
        GameObject root = new GameObject(name, typeof(RectTransform));
        root.transform.SetParent(parent, false);

        Image background = CreateImage(
            root.transform,
            "Background",
            new Color(1f, 1f, 1f, 0.12f)
        );

        background.raycastTarget = false;
        Stretch(background.rectTransform);

        Image fill = CreateImage(root.transform, "Fill", fillColor);

        fill.raycastTarget = false;
        Stretch(fill.rectTransform);

        Slider slider = root.AddComponent<Slider>();

        slider.fillRect = fill.rectTransform;
        slider.handleRect = null;
        slider.targetGraphic = null;
        slider.transition = Selectable.Transition.None;
        slider.navigation = new Navigation
        {
            mode = Navigation.Mode.None
        };
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.interactable = false;
        slider.value = 0f;

        return slider;
    }

    private static Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;

        return image;
    }

    private static TMP_Text CreateText(
        Transform parent,
        string name,
        float size,
        TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;

        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
            return;

        if (FindAnyObjectByType<EventSystem>() != null)
            return;

        GameObject host = new GameObject("EventSystem");
        host.AddComponent<EventSystem>();
        host.AddComponent<StandaloneInputModule>();
    }

    private TMP_Text FindText(string name)
    {
        Transform found = FindDeep(transform, name);

        return found != null ? found.GetComponent<TMP_Text>() : null;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindDeep(root.GetChild(i), name);

            if (found != null)
                return found;
        }

        return null;
    }
}
