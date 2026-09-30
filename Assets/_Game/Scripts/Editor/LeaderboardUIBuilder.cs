#if UNITY_EDITOR

using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Подключает подсистему лидерборда к сцене.
/// Пункт меню: Tools -> Bullet Rush -> Build Leaderboard.
///
/// Без этого сцену метрики забега существуют, но работают вхолостую:
/// RunMetrics / ComboSystem нигде не добавлены, поэтому GameOverManager
/// не находит их через FindAnyObjectByType и молча пропускает сбор
/// результата, отправку в Yandex и показ экрана ранга.
///
/// За один клик:
///  - добавляет ComboSystem + RunMetrics на игрока и связывает их
///    с ScoreManager / WaveManager / PlayerHealth / AbilityManager;
///  - создаёт объект LeaderboardService (он сам уходит в DontDestroyOnLoad);
///  - строит панель ранга и статистики над Game Over (Rank / Score /
///    Stats / Speed / Style) и вешает на неё GameOverLeaderboardUI;
///  - строит виджет комбо внизу HUD и вешает на него GameplayHUD,
///    который начисляет бонусы за комбо 10/20/30/50 из BonusSettings.
///
/// Скрипт идемпотентный: повторный запуск пересобирает UI заново.
/// </summary>
public static class LeaderboardUIBuilder
{
    private const string MenuPath =
        "Tools/Bullet Rush/Build Leaderboard";

    private const string ServiceObjectName =
        "LeaderboardService";

    private const string PanelObjectName =
        "LeaderboardPanel";

    private const string HudObjectName =
        "LeaderboardHUD";

    // Экраны спроектированы под референс 1920x1080.
    // Панель Game Over занимает центр (±247 по X, ±212 по Y),
    // поэтому результаты забега живут полосой над ним.
    private const float PanelWidth = 1200f;
    private const float PanelHeight = 310f;
    private const float PanelOffsetY = 372f;

    private static readonly Color PanelBackground =
        new Color(0f, 0f, 0f, 0.55f);

    private static readonly Color DividerColor =
        new Color(1f, 1f, 1f, 0.18f);

    private static readonly Color StatColor =
        new Color(0.86f, 0.88f, 0.92f, 1f);

    private static readonly Color CaptionColor =
        new Color(0.68f, 0.70f, 0.75f, 1f);

    private const float StatLineHeight = 30f;

    // =========================================================
    // ENTRY POINTS
    // =========================================================

    [MenuItem(MenuPath)]
    public static void Build()
    {
        string report = BuildInternal();

        EditorUtility.DisplayDialog(
            "Build Leaderboard",
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
            "[LeaderboardUIBuilder] " + report.Replace("\n", " ")
        );

        EditorApplication.Exit(0);
    }

    private static string BuildInternal()
    {
        StringBuilderLog log = new StringBuilderLog();

        TMP_FontAsset font = FindDefaultFont();

        PlayerController player =
            Object.FindAnyObjectByType<PlayerController>();

        if (player == null)
        {
            return "PlayerController в сцене не найден. " +
                   "Открой сцену Game и повтори.";
        }

        GameObject playerObject = player.gameObject;

        // =====================================================
        // 1. Метрики забега живут на игроке: там жеtransform,
        //    по которому считается пройденное расстояние.
        // =====================================================

        ComboSystem combo = EnsureComponent<ComboSystem>(
            playerObject
        );

        RunMetrics metrics = EnsureComponent<RunMetrics>(
            playerObject
        );

        Undo.RecordObject(playerObject, "Build Leaderboard");

        SetRefs(metrics, new (string, Object)[]
        {
            ("comboSystem", combo),
            ("scoreManager", Object.FindAnyObjectByType<ScoreManager>()),
            ("waveManager", Object.FindAnyObjectByType<WaveManager>()),
            ("playerHealth", Object.FindAnyObjectByType<PlayerHealth>()),
            ("abilityManager", Object.FindAnyObjectByType<AbilityManager>()),
        });

        log.Add(
            "RunMetrics + ComboSystem добавлены на " +
            playerObject.name + "."
        );

        // =====================================================
        // 2. Сервис отправки результата
        // =====================================================

        LeaderboardService service =
            EnsureServiceObject();

        log.Add(
            service != null
                ? "LeaderboardService на месте."
                : "LeaderboardService: объект не создан."
        );

        // =====================================================
        // 3. Панель ранга и статистики на экране Game Over
        // =====================================================

        GameOverUI gameOverUi =
            Object.FindAnyObjectByType<GameOverUI>();

        if (gameOverUi == null)
        {
            log.Add(
                "ВНИМАНИЕ: GameOverUI в сцене не найден — " +
                "панель результатов не создана."
            );
        }
        else
        {
            BuildGameOverPanel(gameOverUi, font, log);
        }

        // =====================================================
        // 4. Виджет комбо в HUD + бонусы за комбо
        // =====================================================

        GameplayUI gameplayUi =
            Object.FindAnyObjectByType<GameplayUI>();

        if (gameplayUi == null)
        {
            log.Add(
                "ВНИМАНИЕ: GameplayUI в сцене не найден — " +
                "виджет комбо не создан."
            );
        }
        else
        {
            BuildComboWidget(gameplayUi, font, log);
        }

        return log.ToString();
    }

    // =========================================================
    // GAME OVER PANEL
    // =========================================================

    private static void BuildGameOverPanel(
        GameOverUI gameOverUi,
        TMP_FontAsset font,
        StringBuilderLog log)
    {
        SerializedObject gameOverSo =
            new SerializedObject(gameOverUi);

        GameObject gameOverPanel =
            gameOverSo.FindProperty("gameOverPanel")
                ?.objectReferenceValue as GameObject;

        if (gameOverPanel == null)
        {
            log.Add(
                "ВНИМАНИЕ: у GameOverUI не назначена панель " +
                "(gameOverPanel) — блок результатов не создан."
            );

            return;
        }

        Transform parent = gameOverPanel.transform.parent;

        if (parent == null)
        {
            log.Add(
                "ВНИМАНИЕ: панель Game Over без родителя — " +
                "блок результатов не создан."
            );

            return;
        }

        // Чистим старое, чтобы не было дублей при пересборке.
        Transform oldPanel = parent.Find(PanelObjectName);

        if (oldPanel != null)
            Object.DestroyImmediate(oldPanel.gameObject);

        Transform oldHost = parent.Find("LeaderboardResults");

        if (oldHost != null)
            Object.DestroyImmediate(oldHost.gameObject);

        // =====================================================
        // Панель
        // =====================================================

        GameObject panel = CreateRect(
            PanelObjectName,
            parent
        );

        RectTransform panelRect =
            panel.GetComponent<RectTransform>();

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = new Vector2(0f, PanelOffsetY);
        panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);

        Image background = panel.AddComponent<Image>();
        background.color = PanelBackground;
        background.raycastTarget = false;

        // Рисуем сразу над панелью Game Over.
        panel.transform.SetSiblingIndex(
            gameOverPanel.transform.GetSiblingIndex() + 1
        );

        panel.SetActive(false);

        // =====================================================
        // Ранг
        // =====================================================

        GameObject rankPanel = CreateBlock(
            panel.transform,
            "RankPanel",
            0f,
            102f,
            1000f,
            84f
        );

        TextMeshProUGUI rankText = CreateText(
            rankPanel.transform,
            "RankText",
            0f,
            18f,
            320f,
            80f,
            font,
            64f,
            TextAlignmentOptions.Center,
            Color.white,
            FontStyles.Bold
        );

        TextMeshProUGUI rankTitleText = CreateText(
            rankPanel.transform,
            "RankTitleText",
            0f,
            -36f,
            760f,
            30f,
            font,
            22f,
            TextAlignmentOptions.Center,
            CaptionColor,
            FontStyles.Bold
        );

        // =====================================================
        // Очки: база / бонус за стиль / итог
        // =====================================================

        TextMeshProUGUI baseScoreText = CreateText(
            panel.transform,
            "BaseScoreText",
            -330f,
            34f,
            320f,
            44f,
            font,
            30f,
            TextAlignmentOptions.Center,
            StatColor,
            FontStyles.Bold
        );

        TextMeshProUGUI styleBonusText = CreateText(
            panel.transform,
            "StyleBonusText",
            0f,
            34f,
            320f,
            44f,
            font,
            30f,
            TextAlignmentOptions.Center,
            new Color(1f, 0.84f, 0f, 1f),
            FontStyles.Bold
        );

        TextMeshProUGUI finalScoreText = CreateText(
            panel.transform,
            "FinalScoreText",
            330f,
            34f,
            320f,
            44f,
            font,
            30f,
            TextAlignmentOptions.Center,
            Color.white,
            FontStyles.Bold
        );

        // =====================================================
        // Разделитель
        // =====================================================

        GameObject divider = CreateBlock(
            panel.transform,
            "Divider",
            0f,
            4f,
            PanelWidth - 60f,
            2f
        );

        divider.AddComponent<Image>().color = DividerColor;

        // =====================================================
        // Три колонки статистики
        // =====================================================

        GameObject statsPanel = CreateBlock(
            panel.transform,
            "StatsPanel",
            -290f,
            -74f,
            600f,
            150f
        );

        GameObject speedPanel = CreateBlock(
            panel.transform,
            "SpeedPanel",
            155f,
            -74f,
            260f,
            150f
        );

        GameObject stylePanel = CreateBlock(
            panel.transform,
            "StylePanel",
            420f,
            -74f,
            270f,
            150f
        );

        // Общие показатели — две колонки, чтобы влезло в 150 px.
        float leftTop = 62f;
        float rightTop = 62f;

        TMP_Text killsText = CreateStatLine(
            statsPanel.transform,
            "KillsText",
            -150f,
            ref leftTop,
            font
        );

        TMP_Text wavesText = CreateStatLine(
            statsPanel.transform,
            "WavesText",
            -150f,
            ref leftTop,
            font
        );

        TMP_Text runTimeText = CreateStatLine(
            statsPanel.transform,
            "RunTimeText",
            -150f,
            ref leftTop,
            font
        );

        TMP_Text maxComboText = CreateStatLine(
            statsPanel.transform,
            "MaxComboText",
            -150f,
            ref leftTop,
            font
        );

        TMP_Text perfectWavesText = CreateStatLine(
            statsPanel.transform,
            "PerfectWavesText",
            -150f,
            ref leftTop,
            font
        );

        TMP_Text criticalHitsText = CreateStatLine(
            statsPanel.transform,
            "CriticalHitsText",
            150f,
            ref rightTop,
            font
        );

        TMP_Text abilitiesUsedText = CreateStatLine(
            statsPanel.transform,
            "AbilitiesUsedText",
            150f,
            ref rightTop,
            font
        );

        TMP_Text dashDodgesText = CreateStatLine(
            statsPanel.transform,
            "DashDodgesText",
            150f,
            ref rightTop,
            font
        );

        TMP_Text multiKillStreaksText = CreateStatLine(
            statsPanel.transform,
            "MultiKillStreaksText",
            150f,
            ref rightTop,
            font
        );

        // Скорость
        float speedTop = 62f;

        TMP_Text avgKillTimeText = CreateStatLine(
            speedPanel.transform,
            "AvgKillTimeText",
            0f,
            ref speedTop,
            font
        );

        TMP_Text fastestKillTimeText = CreateStatLine(
            speedPanel.transform,
            "FastestKillTimeText",
            0f,
            ref speedTop,
            font
        );

        TMP_Text timeToFirstKillText = CreateStatLine(
            speedPanel.transform,
            "TimeToFirstKillText",
            0f,
            ref speedTop,
            font
        );

        TMP_Text timeToFirstBossText = CreateStatLine(
            speedPanel.transform,
            "TimeToFirstBossText",
            0f,
            ref speedTop,
            font
        );

        TMP_Text bestWaveClearTimeText = CreateStatLine(
            speedPanel.transform,
            "BestWaveClearTimeText",
            0f,
            ref speedTop,
            font
        );

        // Стиль
        TMP_Text damageEfficiencyText = CreateText(
            stylePanel.transform,
            "DamageEfficiencyText",
            0f,
            62f,
            270f,
            30f,
            font,
            22f,
            TextAlignmentOptions.Left,
            StatColor,
            FontStyles.Normal
        );

        // =====================================================
        // Компонент. Живёт на отдельном объекте: если повесить его
        // на саму панель, то вместе с её выключением пропадёт и
        // Update, который возвращает панель при новом забеге.
        // =====================================================

        GameObject host = new GameObject("LeaderboardResults");

        host.transform.SetParent(parent, false);

        GameOverLeaderboardUI leaderboardUi =
            host.AddComponent<GameOverLeaderboardUI>();

        SerializedObject so = new SerializedObject(leaderboardUi);

        SetRefs(so, new (string, Object)[]
        {
            ("rootPanel", panel),
            ("rankPanel", rankPanel),
            ("rankText", rankText),
            ("rankTitleText", rankTitleText),
            ("statsPanel", statsPanel),
            ("speedPanel", speedPanel),
            ("stylePanel", stylePanel),
            ("baseScoreText", baseScoreText),
            ("styleBonusText", styleBonusText),
            ("finalScoreText", finalScoreText),
            ("killsText", killsText),
            ("wavesText", wavesText),
            ("runTimeText", runTimeText),
            ("maxComboText", maxComboText),
            ("perfectWavesText", perfectWavesText),
            ("avgKillTimeText", avgKillTimeText),
            ("fastestKillTimeText", fastestKillTimeText),
            ("timeToFirstKillText", timeToFirstKillText),
            ("timeToFirstBossText", timeToFirstBossText),
            ("bestWaveClearTimeText", bestWaveClearTimeText),
            ("damageEfficiencyText", damageEfficiencyText),
            ("criticalHitsText", criticalHitsText),
            ("abilitiesUsedText", abilitiesUsedText),
            ("dashDodgesText", dashDodgesText),
            ("multiKillStreaksText", multiKillStreaksText),
        });

        Undo.RegisterCreatedObjectUndo(
            panel,
            "Build Leaderboard"
        );

        Undo.RegisterCreatedObjectUndo(
            host,
            "Build Leaderboard"
        );

        log.Add(
            "Панель ранга и статистики создана над Game Over."
        );
    }

    // =========================================================
    // COMBO WIDGET
    // =========================================================

    private static void BuildComboWidget(
        GameplayUI gameplayUi,
        TMP_FontAsset font,
        StringBuilderLog log)
    {
        SerializedObject uiSo = new SerializedObject(gameplayUi);

        GameObject hudRoot =
            uiSo.FindProperty("hudRoot")
                ?.objectReferenceValue as GameObject;

        if (hudRoot == null)
        {
            log.Add(
                "ВНИМАНИЕ: у GameplayUI не назначен hudRoot — " +
                "виджет комбо не создан."
            );

            return;
        }

        Transform oldWidget = hudRoot.transform.Find(HudObjectName);

        if (oldWidget != null)
            Object.DestroyImmediate(oldWidget.gameObject);

        // Контейнер остаётся активным всегда, а ComboPanel внутри
        // него гасится на нулевом комбо. Если повесить GameplayHUD
        // на саму панель, события перестанут приходить.
        GameObject container = CreateRect(
            HudObjectName,
            hudRoot.transform
        );

        RectTransform containerRect =
            container.GetComponent<RectTransform>();

        containerRect.anchorMin = new Vector2(0.5f, 0f);
        containerRect.anchorMax = new Vector2(0.5f, 0f);
        containerRect.pivot = new Vector2(0.5f, 0f);
        containerRect.anchoredPosition = new Vector2(0f, 70f);
        containerRect.sizeDelta = new Vector2(460f, 130f);

        GameObject comboPanel = CreateRect(
            "ComboPanel",
            container.transform
        );

        Stretch(comboPanel.GetComponent<RectTransform>());

        Image comboImage = comboPanel.AddComponent<Image>();
        comboImage.color = new Color(0f, 0f, 0f, 0.35f);
        comboImage.raycastTarget = false;

        CreateText(
            comboPanel.transform,
            "Caption",
            0f,
            42f,
            460f,
            28f,
            font,
            22f,
            TextAlignmentOptions.Center,
            CaptionColor,
            FontStyles.Bold
        ).text = "{lb.combo}";

        TextMeshProUGUI comboCountText = CreateText(
            comboPanel.transform,
            "ComboCountText",
            0f,
            -4f,
            460f,
            64f,
            font,
            54f,
            TextAlignmentOptions.Center,
            Color.white,
            FontStyles.Bold
        );

        comboCountText.text = "0";

        TextMeshProUGUI comboMultiplierText = CreateText(
            comboPanel.transform,
            "ComboMultiplierText",
            0f,
            -48f,
            460f,
            34f,
            font,
            26f,
            TextAlignmentOptions.Center,
            Color.white,
            FontStyles.Bold
        );

        comboPanel.SetActive(false);

        GameplayHUD gameplayHud =
            container.AddComponent<GameplayHUD>();

        SerializedObject hudSo =
            new SerializedObject(gameplayHud);

        // scoreText намеренно не назначаем: счёт уже показывает
        // GameplayUI, второй раз выводить его не нужно.
        SetRefs(hudSo, new (string, Object)[]
        {
            ("comboPanel", comboPanel),
            ("comboCountText", comboCountText),
            ("comboMultiplierText", comboMultiplierText),
            ("bonusSettings", LoadBonusSettings()),
        });

        Undo.RegisterCreatedObjectUndo(
            container,
            "Build Leaderboard"
        );

        log.Add(
            "Виджет комбо создан внизу HUD, бонусы за комбо подключены."
        );
    }

    // =========================================================
    // HELPERS
    // =========================================================

    private static T EnsureComponent<T>(
        GameObject target)
        where T : Component
    {
        T existing = target.GetComponent<T>();

        if (existing != null)
            return existing;

        return Undo.AddComponent<T>(target);
    }

    private static LeaderboardService EnsureServiceObject()
    {
        LeaderboardService existing =
            Object.FindAnyObjectByType<LeaderboardService>();

        if (existing != null)
            return existing;

        GameObject oldObject = GameObject.Find(ServiceObjectName);

        if (oldObject != null)
            Object.DestroyImmediate(oldObject);

        GameObject serviceObject =
            new GameObject(ServiceObjectName);

        return serviceObject.AddComponent<LeaderboardService>();
    }

    private static BonusSettings LoadBonusSettings()
    {
        string[] guids = AssetDatabase.FindAssets(
            "t:BonusSettings"
        );

        foreach (string guid in guids)
        {
            string path =
                AssetDatabase.GUIDToAssetPath(guid);

            BonusSettings settings =
                AssetDatabase.LoadAssetAtPath<BonusSettings>(
                    path
                );

            if (settings != null)
                return settings;
        }

        return null;
    }

    private static TextMeshProUGUI CreateStatLine(
        Transform parent,
        string objectName,
        float x,
        ref float top,
        TMP_FontAsset font)
    {
        TextMeshProUGUI text = CreateText(
            parent,
            objectName,
            x,
            top,
            300f,
            StatLineHeight,
            font,
            22f,
            TextAlignmentOptions.Left,
            StatColor,
            FontStyles.Normal
        );

        top -= StatLineHeight;
        return text;
    }

    private static void SetRefs(
        Component target,
        (string property, Object value)[] refs)
    {
        SetRefs(
            new SerializedObject(target),
            refs
        );
    }

    private static void SetRefs(
        SerializedObject so,
        (string property, Object value)[] refs)
    {
        foreach ((string property, Object value) in refs)
            SetRef(so, property, value);

        so.ApplyModifiedPropertiesWithoutUndo();
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
                $"LeaderboardUIBuilder: свойство '{property}' " +
                "не найдено."
            );

            return;
        }

        found.objectReferenceValue = value;
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

    private static GameObject CreateBlock(
        Transform parent,
        string objectName,
        float x,
        float y,
        float width,
        float height)
    {
        GameObject go = CreateRect(objectName, parent);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);

        return go;
    }

    private static TextMeshProUGUI CreateText(
        Transform parent,
        string objectName,
        float x,
        float y,
        float width,
        float height,
        TMP_FontAsset font,
        float fontSize,
        TextAlignmentOptions alignment,
        Color color,
        FontStyles fontStyle)
    {
        GameObject go = CreateRect(objectName, parent);

        RectTransform rect = go.GetComponent<RectTransform>();

        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(x, y);
        rect.sizeDelta = new Vector2(width, height);

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

    private static void Stretch(RectTransform rect)
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
