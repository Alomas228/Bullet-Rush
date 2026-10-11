using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// Экран достижений. Как и другие панели меню, он собирается в сцене
/// билдером (Editor/AchievementsUIBuilder) и ссылки приходят через
/// инспектор. Но если панель не заведена — экран соберёт себя сам в
/// рантайме (OpenStandalone), чтобы система работала даже до прогона
/// билдера. Логика в обоих случаях одна и та же.
/// </summary>
public class AchievementsUI : MonoBehaviour
{
    [Header("Panel (optional — build happens in editor)")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;
    [SerializeField] private RectTransform content;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private AchievementRowUI rowTemplate;

    [Header("Standalone fallback")]
    [Tooltip("Размер окна, если UI собран в рантайме (OpenStandalone).")]
    [SerializeField] private Vector2 standaloneSize = new Vector2(1000f, 720f);

    private static AchievementsUI standalone;
    private readonly List<AchievementRowUI> rows = new List<AchievementRowUI>();
    private int builtForUnlocked = -1;

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
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
    }

    // =========================================================
    // PUBLIC API
    // =========================================================

    public void ShowPanel()
    {
        gameObject.SetActive(true);
        Refresh();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    /// <summary>
    /// Открывает экран без заранее собранной панели: строит собственный
    /// оверлей на отдельном канвасе. Используется, если кнопка в меню
    /// есть, а панель ещё не назначена.
    /// </summary>
    public static void OpenStandalone()
    {
        if (standalone == null)
        {
            GameObject host = new GameObject("AchievementsUI (runtime)");
            DontDestroyOnLoad(host);
            standalone = host.AddComponent<AchievementsUI>();
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
        AchievementManager manager = AchievementManager.EnsureExists();

        if (titleText != null)
            titleText.text = Lang.GetOr("ach.title", "Достижения");

        if (progressText != null)
        {
            progressText.text = Lang.Get(
                "ach.progress",
                manager.UnlockedCount,
                manager.All.Count
            );
        }

        if (content == null || rowTemplate == null)
            return;

        // Пересобираем строки только когда число открытых изменилось,
        // но прогресс закрытых обновляем всегда (он меняется в забеге).
        RebuildRowsIfNeeded(manager);
        UpdateRows(manager);
    }

    private void RebuildRowsIfNeeded(AchievementManager manager)
    {
        if (builtForUnlocked == manager.UnlockedCount &&
            rows.Count == manager.All.Count)
        {
            return;
        }

        builtForUnlocked = manager.UnlockedCount;

        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i] != null)
                Destroy(rows[i].gameObject);
        }

        rows.Clear();

        for (int i = 0; i < manager.All.Count; i++)
        {
            AchievementRowUI row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            rows.Add(row);
        }
    }

    private void UpdateRows(AchievementManager manager)
    {
        for (int i = 0; i < rows.Count && i < manager.All.Count; i++)
        {
            AchievementData data = manager.All[i];
            bool unlocked = manager.IsUnlocked(data.Id);

            rows[i].Bind(
                data,
                unlocked,
                manager.GetProgress(data),
                manager.GetRequired(data)
            );
        }
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

        // Затемняющий фон на весь экран.
        Image dim = CreateImage(root, "Dim", new Color(0f, 0f, 0f, 0.85f));
        Stretch(dim.rectTransform);

        // Панель окна.
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
        titleRect.sizeDelta = new Vector2(-240f, 70f);
        titleRect.anchoredPosition = new Vector2(20f, -20f);

        progressText = CreateText(windowRect, "Progress", 26f, TextAlignmentOptions.Right);
        RectTransform progressRect = progressText.rectTransform;
        progressRect.anchorMin = new Vector2(1f, 1f);
        progressRect.anchorMax = new Vector2(1f, 1f);
        progressRect.pivot = new Vector2(1f, 1f);
        progressRect.sizeDelta = new Vector2(220f, 60f);
        progressRect.anchoredPosition = new Vector2(-20f, -20f);

        // Кнопка закрытия.
        Image closeImage = CreateImage(windowRect, "CloseButton", new Color(0.5f, 0.12f, 0.14f, 1f));
        RectTransform closeRect = closeImage.rectTransform;
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.sizeDelta = new Vector2(60f, 60f);
        closeRect.anchoredPosition = new Vector2(-100f, -20f);

        closeButton = closeImage.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(HidePanel);

        TMP_Text closeLabel = CreateText(closeRect, "X", 34f, TextAlignmentOptions.Center);
        Stretch(closeLabel.rectTransform);

        // Прокручиваемая область.
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

    /// <summary>
    /// Строит выключенный шаблон строки достижения. Не кладётся в
    /// content, поэтому не отображается, пока не будет размножен.
    /// </summary>
    private AchievementRowUI BuildRuntimeRowTemplate()
    {
        GameObject rowGo = new GameObject("RowTemplate", typeof(RectTransform));
        rowGo.transform.SetParent(transform, false);

        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.sizeDelta = new Vector2(0f, 130f);

        Image bg = rowGo.AddComponent<Image>();
        bg.color = new Color(1f, 1f, 1f, 0.06f);

        LayoutElement element = rowGo.AddComponent<LayoutElement>();
        element.minHeight = 130f;
        element.preferredHeight = 130f;

        TMP_Text rowTitle = CreateText(rowRect, "Title", 28f, TextAlignmentOptions.TopLeft);
        RectTransform rowTitleRect = rowTitle.rectTransform;
        rowTitleRect.anchorMin = new Vector2(0f, 1f);
        rowTitleRect.anchorMax = new Vector2(1f, 1f);
        rowTitleRect.pivot = new Vector2(0.5f, 1f);
        rowTitleRect.sizeDelta = new Vector2(-24f, 40f);
        rowTitleRect.anchoredPosition = new Vector2(0f, -12f);

        TMP_Text rowDesc = CreateText(rowRect, "Description", 22f, TextAlignmentOptions.TopLeft);
        rowDesc.color = new Color(0.8f, 0.82f, 0.88f, 1f);
        RectTransform rowDescRect = rowDesc.rectTransform;
        rowDescRect.anchorMin = new Vector2(0f, 0f);
        rowDescRect.anchorMax = new Vector2(1f, 1f);
        rowDescRect.offsetMin = new Vector2(12f, 40f);
        rowDescRect.offsetMax = new Vector2(-12f, -52f);

        TMP_Text rowProgress = CreateText(rowRect, "Progress", 20f, TextAlignmentOptions.BottomRight);
        RectTransform rowProgressRect = rowProgress.rectTransform;
        rowProgressRect.anchorMin = new Vector2(0f, 0f);
        rowProgressRect.anchorMax = new Vector2(1f, 0f);
        rowProgressRect.pivot = new Vector2(0.5f, 0f);
        rowProgressRect.sizeDelta = new Vector2(-24f, 34f);
        rowProgressRect.anchoredPosition = new Vector2(0f, 8f);

        AchievementRowUI row = rowGo.AddComponent<AchievementRowUI>();
        row.Configure(rowTitle, rowDesc, rowProgress, bg);

        rowGo.SetActive(false);

        return row;
    }

    // =========================================================
    // SMALL UI HELPERS
    // =========================================================

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
