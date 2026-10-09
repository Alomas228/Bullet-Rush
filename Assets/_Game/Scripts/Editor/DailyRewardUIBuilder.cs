#if UNITY_EDITOR

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class DailyRewardUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Daily Rewards";

    private const string PanelName = "DailyRewardPanel";

    private const string ExistingButtonName = "Btn_DailyReward";

    private const string SlotPrefabGuid =
        "92a2f25713b8149ca89143e1d60f65b5";

    private const int SlotCount = 7;

    private const float SlotWidth = 190f;
    private const float SlotHeight = 230f;
    private const float SlotStep = 205f;
    private const float SlotY = -210f;

    private static readonly Color PanelBackground =
        new Color(0f, 0f, 0f, 0.55f);

    private static readonly Color ButtonBackground =
        new Color(1f, 1f, 1f, 0.2f);

    [MenuItem(MenuPath)]
    public static void Build()
    {
        string report = BuildInternal();

        EditorUtility.DisplayDialog(
            "Build Daily Rewards",
            report,
            "OK"
        );
    }

    public static void BuildFromCommandLine()
    {
        Scene scene =
            EditorSceneManager.OpenScene(
                "Assets/Scenes/Game.unity",
                OpenSceneMode.Single
            );

        string report = BuildInternal();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        Debug.Log(
            "[DailyRewardUIBuilder] " + report.Replace("\n", " ")
        );

        EditorApplication.Exit(0);
    }

    private static string BuildInternal()
    {
        StringBuilderLog log = new StringBuilderLog();

        MainMenuUI menu =
            Object.FindAnyObjectByType<MainMenuUI>();

        if (menu == null)
        {
            return "MainMenuUI не найден в сцене. " +
                   "Открой сцену Game и повтори.";
        }

        SerializedObject menuSo = new SerializedObject(menu);

        GameObject menuPanel =
            menuSo.FindProperty("menuPanel")
                ?.objectReferenceValue as GameObject;

        if (menuPanel == null)
        {
            return "menuPanel не назначен на MainMenuUI.";
        }

        Transform panelParent = menuPanel.transform.parent;

        if (panelParent == null)
            panelParent = menuPanel.transform;

        Button shopButton =
            menuSo.FindProperty("shopButton")
                ?.objectReferenceValue as Button;

        Button dailyButton =
            EnsureDailyRewardButton(menuSo, shopButton);

        if (dailyButton == null)
        {
            return "Кнопка «Ежедневные награды» не найдена, " +
                   "а клонировать её некому (shopButton не назначен).";
        }

        DeletePreviousPanel(panelParent, menuSo);

        GameObject slotPrefab =
            AssetDatabase.LoadAssetAtPath<GameObject>(
                AssetDatabase.GUIDToAssetPath(SlotPrefabGuid)
            );

        if (slotPrefab == null)
        {
            return "Slot.prefab не найден в Assets/Prefabs.";
        }

        TMP_FontAsset font = FindDefaultFont();

        GameObject panelObject = CreatePanel(panelParent);

        TextMeshProUGUI titleText =
            CreateTitle(panelObject.transform, font);

        Button backButton =
            CreateBackButton(panelObject.transform, font);

        DailyRewardSlotUI[] slots =
            CreateSlots(panelObject.transform, slotPrefab);

        DailyRewardUI rewardUi =
            panelObject.AddComponent<DailyRewardUI>();

        SerializedObject rewardSo =
            new SerializedObject(rewardUi);

        SetRef(rewardSo, "titleText", titleText);
        SetRef(rewardSo, "closeButton", backButton);

        SerializedProperty slotArray =
            rewardSo.FindProperty("rewardSlots");

        slotArray.arraySize = slots.Length;

        for (int i = 0; i < slots.Length; i++)
            slotArray.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];

        rewardSo.ApplyModifiedPropertiesWithoutUndo();

        SubPanelUI subPanel =
            panelObject.AddComponent<SubPanelUI>();

        SerializedObject subSo = new SerializedObject(subPanel);

        SetRef(subSo, "backButton", backButton);

        subSo.ApplyModifiedPropertiesWithoutUndo();

        menuSo.FindProperty("dailyRewardButton")
            .objectReferenceValue = dailyButton;

        menuSo.FindProperty("dailyRewardPanel")
            .objectReferenceValue = panelObject;

        menuSo.ApplyModifiedPropertiesWithoutUndo();

        DailyRewardManager manager = EnsureDailyRewardManager();

        if (manager != null)
        {
            SerializedObject managerSo =
                new SerializedObject(manager);

            SerializedProperty cycle =
                managerSo.FindProperty("cycleLength");

            if (cycle != null)
            {
                cycle.intValue = SlotCount;
                managerSo.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        Undo.RegisterCreatedObjectUndo(panelObject, MenuPath);

        panelObject.SetActive(false);

        EditorSceneManager.MarkSceneDirty(panelObject.scene);

        Selection.activeGameObject = panelObject;

        log.Add("Панель «Ежедневные награды» пересобрана:");
        log.Add("слотов: " + slots.Length + ", цикл дней: " + SlotCount);
        log.Add("кнопка меню: " + dailyButton.name);
        log.Add("награды: день 1 = " + manager.CoinsForDay(1) +
                " монет / " + manager.XpForDay(1) + " XP, " +
                "день " + SlotCount + " = " + manager.CoinsForDay(SlotCount) +
                " монет / " + manager.XpForDay(SlotCount) + " XP.");

        return log.ToString();
    }

    private static Button EnsureDailyRewardButton(
        SerializedObject menuSo,
        Button shopButton)
    {
        Button existing =
            menuSo.FindProperty("dailyRewardButton")
                ?.objectReferenceValue as Button;

        if (existing == null)
        {
            GameObject found = GameObject.Find(ExistingButtonName);

            if (found != null)
                existing = found.GetComponent<Button>();
        }

        if (existing == null && shopButton != null)
        {
            GameObject buttonObject = Object.Instantiate(
                shopButton.gameObject,
                shopButton.transform.parent
            );

            buttonObject.name = "DailyRewardButton";

            Undo.RegisterCreatedObjectUndo(buttonObject, MenuPath);

            existing = buttonObject.GetComponent<Button>();

            if (existing == null)
                existing = buttonObject.AddComponent<Button>();
        }

        if (existing == null)
            return null;

        TextMeshProUGUI label =
            existing.GetComponentInChildren<TextMeshProUGUI>(true);

        if (label != null)
            label.text = "{daily.btn}";

        return existing;
    }

    private static void DeletePreviousPanel(
        Transform panelParent,
        SerializedObject menuSo)
    {
        menuSo.FindProperty("dailyRewardPanel")
            .objectReferenceValue = null;

        menuSo.ApplyModifiedPropertiesWithoutUndo();

        if (panelParent == null)
            return;

        Transform oldPanel = panelParent.Find(PanelName);

        if (oldPanel != null)
            Object.DestroyImmediate(oldPanel.gameObject);
    }

    private static GameObject CreatePanel(Transform parent)
    {
        GameObject panel =
            new GameObject(PanelName, typeof(RectTransform));

        panel.transform.SetParent(parent, false);

        RectTransform rect =
            panel.GetComponent<RectTransform>();

        SetFullStretch(rect);

        rect.gameObject.AddComponent<Image>().color = PanelBackground;

        return panel;
    }

    private static TextMeshProUGUI CreateTitle(
        Transform parent,
        TMP_FontAsset font)
    {
        RectTransform block = AddBlock(
            parent,
            "TitleText",
            new Vector2(0.5f, 1f),
            new Vector2(0f, -10f),
            new Vector2(1500f, 56f),
            new Vector2(0.5f, 1f)
        );

        TextMeshProUGUI text =
            block.gameObject.AddComponent<TextMeshProUGUI>();

        text.font = font;
        text.text = "{daily.title}";
        text.fontSize = 36f;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        return text;
    }

    private static Button CreateBackButton(
        Transform parent,
        TMP_FontAsset font)
    {
        RectTransform block = AddBlock(
            parent,
            "BackButton",
            new Vector2(0f, 0f),
            new Vector2(80f, 60f),
            new Vector2(220f, 84f),
            new Vector2(0f, 0f)
        );

        Image image = block.gameObject.AddComponent<Image>();
        image.color = ButtonBackground;

        Button button = block.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = image;

        GameObject labelObject =
            new GameObject("LabelText", typeof(RectTransform));

        labelObject.transform.SetParent(block, false);

        RectTransform labelRect =
            labelObject.GetComponent<RectTransform>();

        SetFullStretch(labelRect);

        TextMeshProUGUI label =
            labelObject.AddComponent<TextMeshProUGUI>();

        label.font = font;
        label.text = "{daily.back}";
        label.fontSize = 24f;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;

        return button;
    }

    private static DailyRewardSlotUI[] CreateSlots(
        Transform parent,
        GameObject slotPrefab)
    {
        var slots = new DailyRewardSlotUI[SlotCount];

        float start = -(SlotCount - 1) * SlotStep * 0.5f;

        for (int i = 0; i < SlotCount; i++)
        {
            GameObject slotObject = Object.Instantiate(
                slotPrefab,
                parent
            );

            slotObject.name = "Slot_" + (i + 1);

            RectTransform rect =
                slotObject.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition =
                new Vector2(start + i * SlotStep, SlotY);
            rect.sizeDelta = new Vector2(SlotWidth, SlotHeight);

            slots[i] = slotObject.GetComponent<DailyRewardSlotUI>();
        }

        return slots;
    }

    private static DailyRewardManager EnsureDailyRewardManager()
    {
        DailyRewardManager existing =
            Object.FindAnyObjectByType<DailyRewardManager>();

        if (existing != null)
            return existing;

        GameObject managerObject =
            GameObject.Find("DailyRewardManager");

        if (managerObject == null)
            managerObject = new GameObject("DailyRewardManager");

        Undo.RegisterCreatedObjectUndo(managerObject, MenuPath);

        return managerObject.AddComponent<DailyRewardManager>();
    }

    private static void SetRef(
        SerializedObject so,
        string property,
        Object value)
    {
        SerializedProperty found = so.FindProperty(property);

        if (found == null)
        {
            Debug.LogWarning(
                "DailyRewardUIBuilder: свойство '" + property +
                "' не найдено."
            );

            return;
        }

        found.objectReferenceValue = value;
    }

    private static RectTransform AddBlock(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        Vector2 pivot)
    {
        GameObject go =
            new GameObject(name, typeof(RectTransform));

        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        return rect;
    }

    private static void SetFullStretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static TMP_FontAsset FindDefaultFont()
    {
        if (TMP_Settings.defaultFontAsset != null)
            return TMP_Settings.defaultFontAsset;

        TMP_FontAsset[] all =
            Resources.FindObjectsOfTypeAll<TMP_FontAsset>();

        foreach (TMP_FontAsset asset in all)
        {
            if (asset != null &&
                asset.name.Contains("LiberationSans"))
                return asset;
        }

        foreach (TMP_FontAsset asset in all)
        {
            if (asset != null &&
                !asset.name.StartsWith("Legacy"))
                return asset;
        }

        return null;
    }

    private sealed class StringBuilderLog
    {
        private readonly System.Text.StringBuilder builder =
            new System.Text.StringBuilder();

        public void Add(string line)
        {
            if (builder.Length > 0)
                builder.Append('\n');

            builder.Append("- ").Append(line);
        }

        public override string ToString()
        {
            return builder.Length > 0
                ? builder.ToString()
                : "Ничего не изменено.";
        }
    }
}

#endif