#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class MasteryUIBuilder
{
    [MenuItem("Arcade Survivor/UI/Build Mastery Panel")]
    public static void Build()
    {
        MainMenuUI menu = Object.FindAnyObjectByType<MainMenuUI>();
        if (menu == null)
        {
            Debug.LogError("MasteryUIBuilder: MainMenuUI not found in the open scene.");
            return;
        }

        Transform existing = menu.transform.Find("MasteryPanel");
        if (existing != null)
            Object.DestroyImmediate(existing.gameObject);
        GameObject panel = CreatePanel(menu.transform);
        panel.SetActive(false);
        MasteryPanelUI ui = panel.GetComponent<MasteryPanelUI>();
        if (ui == null) ui = panel.AddComponent<MasteryPanelUI>();

        SerializedObject serializedMenu = new SerializedObject(menu);
        serializedMenu.FindProperty("masteryPanel").objectReferenceValue = panel;
        Button button = menu.transform.Find("MasteryButton")?.GetComponent<Button>();
        if (button == null)
        {
            button = CreateButton(menu.transform, "MasteryButton", Lang.GetOr("progression.mastery.button", "MASTERY"), new Vector2(0.5f, 0.5f), new Vector2(177f, -477f));
        }
        else
        {
            RectTransform buttonRect = button.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(177f, -477f);
            buttonRect.sizeDelta = new Vector2(230f, 46f);
        }
        serializedMenu.FindProperty("masteryButton").objectReferenceValue = button;
        serializedMenu.ApplyModifiedPropertiesWithoutUndo();

        ApplyButtonStyle(button, Lang.GetOr("progression.mastery.button", "MASTERY"));

        SerializedObject serializedUI = new SerializedObject(ui);
        serializedUI.FindProperty("titleText").objectReferenceValue = panel.transform.Find("Title")?.GetComponent<TMP_Text>();
        serializedUI.FindProperty("prestigeText").objectReferenceValue = panel.transform.Find("Prestige")?.GetComponent<TMP_Text>();
        serializedUI.FindProperty("prestigeButton").objectReferenceValue = panel.transform.Find("PrestigeButton")?.GetComponent<Button>();
        serializedUI.FindProperty("collectionText").objectReferenceValue = panel.transform.Find("Collection")?.GetComponent<TMP_Text>();
        serializedUI.FindProperty("rowsRoot").objectReferenceValue = panel.transform.Find("Rows/Viewport/Content");
        serializedUI.FindProperty("closeButton").objectReferenceValue = panel.transform.Find("CloseButton")?.GetComponent<Button>();
        serializedUI.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
        EditorSceneManager.SaveScene(menu.gameObject.scene);
        Debug.Log("Mastery panel built and wired.");
    }

    private static GameObject CreatePanel(Transform parent)
    {
        GameObject panel = new GameObject("MasteryPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.15f, 0.12f);
        rect.anchorMax = new Vector2(0.85f, 0.88f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image panelImage = panel.GetComponent<Image>();
        panelImage.sprite = ArcadeKitAssets.RoundRect;
        panelImage.type = Image.Type.Sliced;
        panelImage.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.99f);
        panelImage.raycastTarget = true;
        Shadow shadow = panel.AddComponent<Shadow>();
        shadow.effectColor = ArcadeUiTheme.Shadow;
        shadow.effectDistance = new Vector2(0f, -8f);
        shadow.useGraphicAlpha = true;

        TMP_Text title = CreateText(panel.transform, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -34f), 30f);
        title.text = Lang.GetOr("progression.mastery.title", "MASTERY & COLLECTION");
        TMP_Text prestige = CreateText(panel.transform, "Prestige", new Vector2(0.5f, 1f), new Vector2(0f, -82f), 20f);
        prestige.text = "Prestige";
        CreateButton(panel.transform, "PrestigeButton", Lang.GetOr("progression.prestige.button", "PRESTIGE"), new Vector2(0.5f, 1f), new Vector2(0f, -126f));
        TMP_Text collection = CreateText(panel.transform, "Collection", new Vector2(0.5f, 1f), new Vector2(0f, -174f), 18f);
        collection.text = "Collection";
        GameObject rows = new GameObject("Rows", typeof(RectTransform), typeof(ScrollRect));
        rows.transform.SetParent(panel.transform, false);
        RectTransform rowsRect = rows.GetComponent<RectTransform>();
        rowsRect.anchorMin = Vector2.zero;
        rowsRect.anchorMax = Vector2.one;
        rowsRect.offsetMin = new Vector2(40f, 112f);
        rowsRect.offsetMax = new Vector2(-40f, -218f);

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(rows.transform, false);
        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;
        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(0f, 0f, 0f, 0.18f);
        viewportImage.raycastTarget = true;
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = Vector2.zero;
        VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 12f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        ScrollRect scroll = rows.GetComponent<ScrollRect>();
        scroll.viewport = viewportRect;
        scroll.content = contentRect;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 35f;
        Button close = CreateButton(panel.transform, "CloseButton", Lang.GetOr("ui.back", "BACK"), new Vector2(0.5f, 0f), new Vector2(0f, 30f));
        close.gameObject.name = "CloseButton";
        return panel;
    }

    private static TMP_Text CreateText(Transform parent, string name, Vector2 anchor, Vector2 position, float size)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = new Vector2(700f, 42f);
        TMP_Text text = go.GetComponent<TMP_Text>();
        text.font = size >= 26f ? ArcadeKitAssets.Display : ArcadeKitAssets.Body;
        text.fontSize = size;
        text.alignment = TextAlignmentOptions.Center;
        text.color = size >= 26f ? ArcadeUiTheme.GoldBright : ArcadeUiTheme.TextPrimary;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static void ApplyButtonStyle(Button button, string label)
    {
        Image image = button.GetComponent<Image>();
        if (image == null) image = button.gameObject.AddComponent<Image>();
        image.sprite = ArcadeKitAssets.ButtonDark;
        image.type = Image.Type.Sliced;
        image.color = ArcadeUiTheme.PanelRaised;
        image.raycastTarget = true;
        button.targetGraphic = image;
        TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text == null)
            text = CreateText(button.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, 18f) as TextMeshProUGUI;
        text.text = label;
        text.font = ArcadeKitAssets.BodyBold;
        text.color = ArcadeUiTheme.TextPrimary;
    }

    private static Button CreateButton(Transform parent, string name, string label, Vector2? anchor = null, Vector2? position = null)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        Vector2 a = anchor ?? new Vector2(0.5f, 0.5f);
        rect.anchorMin = a;
        rect.anchorMax = a;
        rect.pivot = a;
        rect.anchoredPosition = position ?? Vector2.zero;
        rect.sizeDelta = new Vector2(230f, 46f);
        Image image = go.GetComponent<Image>();
        image.sprite = ArcadeKitAssets.ButtonDark;
        image.type = Image.Type.Sliced;
        image.color = ArcadeUiTheme.PanelRaised;
        image.raycastTarget = true;
        Button button = go.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ArcadeUiTheme.Bright(ArcadeUiTheme.Cyan, 0.18f);
        colors.pressedColor = ArcadeUiTheme.Dark(ArcadeUiTheme.Cyan, 0.18f);
        button.colors = colors;
        TMP_Text text = CreateText(go.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, 18f);
        text.text = label;
        text.rectTransform.sizeDelta = rect.sizeDelta;
        return button;
    }
}
#endif
