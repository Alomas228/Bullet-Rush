#if UNITY_EDITOR

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Подключает подсистему личного рекорда к сцене.
/// Пункт меню: Tools -> Bullet Rush -> Build Personal Best.
///
/// За один клик:
///  - создаёт подпись «PERSONAL BEST — WAVE N» в главном меню
///    (PersonalBestLabel);
///  - создаёт в HUD блок «Wave 14 / PB 17», прогресс до рекорда
///    и предупреждение «3 WAVES TO BEAT YOUR RECORD»
///    (PersonalBestHud).
///
/// Скрипт идемпотентный: повторный запуск пересобирает объекты
/// заново. Позиции заданы константами внизу файла — после сборки
/// объекты можно просто перетащить в нужное место сцены, все
/// тексты и правила показа живут в инспекторе компонентов.
/// </summary>
public static class PersonalBestUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Personal Best";

    private const string MenuLabelObjectName =
        "PersonalBestLabel";

    private const string HudObjectName =
        "PersonalBestHUD";

    // =========================================================
    // ENTRY POINTS
    // =========================================================

    [MenuItem(MenuPath)]
    public static void Build()
    {
        string report = BuildInternal();

        EditorUtility.DisplayDialog(
            "Build Personal Best",
            report,
            "OK"
        );
    }

    /// <summary>
    /// То же самое, но открывает сцену, сохраняет её и не показывает
    /// диалогов — для запуска из командной строки Unity.
    /// </summary>
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
            "[PersonalBestUIBuilder] " + report.Replace("\n", " ")
        );

        EditorApplication.Exit(0);
    }

    private static string BuildInternal()
    {
        StringBuilderLog log = new StringBuilderLog();

        TMP_FontAsset font = FindDefaultFont();

        BuildMenuLabel(font, log);
        BuildHudBlock(font, log);

        return log.ToString();
    }

    // =========================================================
    // MENU LABEL
    // =========================================================

    private static void BuildMenuLabel(
        TMP_FontAsset font,
        StringBuilderLog log)
    {
        MainMenuUI menuUi =
            Object.FindAnyObjectByType<MainMenuUI>();

        if (menuUi == null)
        {
            log.Add(
                "ВНИМАНИЕ: MainMenuUI в сцене не найден — " +
                "подпись рекорда в меню не создана."
            );

            return;
        }

        GameObject menuPanel =
            new SerializedObject(menuUi)
                .FindProperty("menuPanel")
                ?.objectReferenceValue as GameObject;

        if (menuPanel == null)
        {
            log.Add(
                "ВНИМАНИЕ: у MainMenuUI не назначена панель " +
                "(menuPanel) — подпись рекорда не создана."
            );

            return;
        }

        Transform oldLabel =
            menuPanel.transform.Find(MenuLabelObjectName);

        if (oldLabel != null)
            Object.DestroyImmediate(oldLabel.gameObject);

        TextMeshProUGUI label = CreateText(
            menuPanel.transform,
            MenuLabelObjectName,
            MenuLabelAnchor,
            MenuLabelPosition,
            MenuLabelSize,
            font,
            MenuLabelFontSize,
            TextAlignmentOptions.Center,
            RecordColor,
            FontStyles.Bold
        );

        PersonalBestLabel component =
            Undo.AddComponent<PersonalBestLabel>(label.gameObject);

        SetRefs(component, new (string, Object)[]
        {
            ("label", label),
        });

        Undo.RegisterCreatedObjectUndo(
            label.gameObject,
            "Build Personal Best"
        );

        log.Add(
            "Подпись рекорда создана в главном меню " +
            "( PersonalBestLabel )."
        );
    }

    // =========================================================
    // HUD BLOCK
    // =========================================================

    private static void BuildHudBlock(
        TMP_FontAsset font,
        StringBuilderLog log)
    {
        GameplayUI gameplayUi =
            Object.FindAnyObjectByType<GameplayUI>();

        if (gameplayUi == null)
        {
            log.Add(
                "ВНИМАНИЕ: GameplayUI в сцене не найден — " +
                "блок рекорда в HUD не создан."
            );

            return;
        }

        GameObject hudRoot =
            new SerializedObject(gameplayUi)
                .FindProperty("hudRoot")
                ?.objectReferenceValue as GameObject;

        if (hudRoot == null)
        {
            log.Add(
                "ВНИМАНИЕ: у GameplayUI не назначен hudRoot — " +
                "блок рекорда в HUD не создан."
            );

            return;
        }

        Transform oldWidget =
            hudRoot.transform.Find(HudObjectName);

        if (oldWidget != null)
            Object.DestroyImmediate(oldWidget.gameObject);

        GameObject container = CreateRect(
            HudObjectName,
            hudRoot.transform
        );

        RectTransform containerRect =
            container.GetComponent<RectTransform>();

        containerRect.anchorMin = HudAnchor;
        containerRect.anchorMax = HudAnchor;
        containerRect.pivot = HudPivot;
        containerRect.anchoredPosition = HudPosition;
        containerRect.sizeDelta = HudSize;

        TextMeshProUGUI progressText = CreateText(
            container.transform,
            "ProgressText",
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 52f),
            new Vector2(700f, 76f),
            font,
            56f,
            TextAlignmentOptions.Center,
            Color.white,
            FontStyles.Bold
        );

        TextMeshProUGUI wavePbText = CreateText(
            container.transform,
            "WavePbText",
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 4f),
            new Vector2(700f, 36f),
            font,
            26f,
            TextAlignmentOptions.Center,
            StatColor,
            FontStyles.Bold
        );

        TextMeshProUGUI approachText = CreateText(
            container.transform,
            "ApproachText",
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -40f),
            new Vector2(700f, 36f),
            font,
            24f,
            TextAlignmentOptions.Center,
            RecordColor,
            FontStyles.Bold
        );

        TextMeshProUGUI newRecordText = CreateText(
            container.transform,
            "NewRecordText",
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -84f),
            new Vector2(700f, 40f),
            font,
            30f,
            TextAlignmentOptions.Center,
            NewRecordColor,
            FontStyles.Bold
        );

        progressText.gameObject.SetActive(false);
        wavePbText.gameObject.SetActive(false);
        approachText.gameObject.SetActive(false);
        newRecordText.gameObject.SetActive(false);

        PersonalBestHud hud =
            container.AddComponent<PersonalBestHud>();

        SetRefs(hud, new (string, Object)[]
        {
            ("progressText", progressText),
            ("wavePbText", wavePbText),
            ("approachText", approachText),
            ("newRecordText", newRecordText),
            ("waveManager", Object.FindAnyObjectByType<WaveManager>()),
        });

        Undo.RegisterCreatedObjectUndo(
            container,
            "Build Personal Best"
        );

        log.Add(
            "Блок рекорда создан в HUD " +
            "( PersonalBestHud )."
        );
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static void SetRefs(
        Component target,
        (string property, Object value)[] refs)
    {
        SerializedObject so = new SerializedObject(target);

        foreach ((string property, Object value) in refs)
        {
            SerializedProperty found = so.FindProperty(property);

            if (found == null)
            {
                Debug.LogWarning(
                    $"PersonalBestUIBuilder: свойство '{property}' " +
                    "не найдено."
                );

                continue;
            }

            found.objectReferenceValue = value;
        }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static GameObject CreateRect(
        string objectName,
        Transform parent)
    {
        GameObject go =
            new GameObject(objectName, typeof(RectTransform));

        go.transform.SetParent(parent, false);

        return go;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        Vector2 anchor,
        Vector2 position,
        Vector2 size,
        TMP_FontAsset font,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color,
        FontStyles fontStyle)
    {
        GameObject go = CreateRect(objectName, parent);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TextMeshProUGUI text =
            go.AddComponent<TextMeshProUGUI>();

        text.text = string.Empty;
        text.font = font;
        text.fontSize = fontSize;
        text.lineSpacing = 1f;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;

        return text;
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

    // =========================================================
    // LAYOUT / STYLE
    // =========================================================
    // Стартовые позиции под референс 1920x1080. После сборки их
    // можно менять перетаскиванием объектов в сцене — компоненты
    // на позицию не полагаются.
    // =========================================================

    private static readonly Vector2 MenuLabelAnchor =
        new Vector2(0.5f, 1f);

    private static readonly Vector2 MenuLabelPosition =
        new Vector2(0f, -70f);

    private static readonly Vector2 MenuLabelSize =
        new Vector2(900f, 60f);

    private const float MenuLabelFontSize = 34f;

    private static readonly Vector2 HudAnchor =
        new Vector2(0.5f, 1f);

    private static readonly Vector2 HudPivot =
        new Vector2(0.5f, 1f);

    private static readonly Vector2 HudPosition =
        new Vector2(0f, -220f);

    private static readonly Vector2 HudSize =
        new Vector2(700f, 200f);

    private static readonly Color RecordColor =
        new Color(1f, 0.84f, 0f, 1f);

    private static readonly Color StatColor =
        new Color(0.86f, 0.88f, 0.92f, 1f);

    private static readonly Color NewRecordColor =
        new Color(0.35f, 1f, 0.55f, 1f);

    // =========================================================
    // REPORT
    // =========================================================

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
