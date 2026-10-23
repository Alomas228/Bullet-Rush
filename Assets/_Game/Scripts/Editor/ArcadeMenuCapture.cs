#if UNITY_EDITOR

using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ArcadeMenuCapture
{
    private const string RequestPath = "Temp/arcade_menu_capture.request";
    private const string StatusPath = "Temp/arcade_menu_capture.status";
    private const string DefaultCapturePath = "Temp/arcade_menu_capture.png";
    private const string ActiveKey = "ArcadeMenuCapture.Active";
    private const string StageKey = "ArcadeMenuCapture.Stage";
    private const string ModeKey = "ArcadeMenuCapture.Mode";
    private const string OutputKey = "ArcadeMenuCapture.Output";
    private const string ExitKey = "ArcadeMenuCapture.Exit";
    private const string TourKey = "ArcadeMenuCapture.Tour";

    private static readonly (string Mode, string Path)[] Tour =
    {
        ("menu", "Temp/arcade_tour_01_menu.png"),
        ("upgrades", "Temp/arcade_tour_02_upgrades.png"),
        ("equipment", "Temp/arcade_tour_03_equipment.png"),
        ("maps", "Temp/arcade_tour_04_maps.png"),
        ("daily", "Temp/arcade_tour_05_daily.png"),
        ("achievements", "Temp/arcade_tour_06_achievements.png"),
        ("missions", "Temp/arcade_tour_07_missions.png"),
        ("settings", "Temp/arcade_tour_08_settings.png"),
        ("stats", "Temp/arcade_tour_09_stats.png"),
        ("tips", "Temp/arcade_tour_10_tips.png"),
        ("field", "Temp/arcade_tour_11_field.png")
    };

    private static double stageStarted;

    static ArcadeMenuCapture()
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            stageStarted = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            return;
        }

        if (!File.Exists(RequestPath))
            return;

        string[] lines = File.ReadAllLines(RequestPath);
        string mode = lines.Length > 0 && lines[0].Trim().Length > 0
            ? lines[0].Trim()
            : "menu";
        string output = lines.Length > 1 && lines[1].Trim().Length > 0
            ? lines[1].Trim()
            : DefaultCapturePath;
        bool exit = lines.Length > 2 && lines[2].Trim() == "exit";

        File.Delete(RequestPath);
        Initialize(mode, output, exit);
    }

    [MenuItem("Arcade Survivor/Capture/Tour All Panels")]
    public static void RequestTour()
    {
        Request("tour", Tour[0].Path, false);
    }

    [MenuItem("Arcade Survivor/Capture/Menu Snapshot")]
    public static void RequestMenuSnapshot()
    {
        Request("menu", DefaultCapturePath, false);
    }

    [MenuItem("Arcade Survivor/Capture/Settings Panel")]
    public static void RequestSettingsSnapshot()
    {
        Request("settings", "Temp/arcade_menu_settings.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Stats Panel")]
    public static void RequestStatsSnapshot()
    {
        Request("stats", "Temp/arcade_menu_stats.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Tips Panel")]
    public static void RequestTipsSnapshot()
    {
        Request("tips", "Temp/arcade_menu_tips.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Field Only")]
    public static void RequestFieldSnapshot()
    {
        Request("field", "Temp/arcade_menu_field.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Upgrades Panel")]
    public static void RequestUpgradesSnapshot()
    {
        Request("upgrades", "Temp/arcade_panel_upgrades.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Equipment Panel")]
    public static void RequestEquipmentSnapshot()
    {
        Request("equipment", "Temp/arcade_panel_equipment.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Maps Panel")]
    public static void RequestMapsSnapshot()
    {
        Request("maps", "Temp/arcade_panel_maps.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Daily Reward Panel")]
    public static void RequestDailySnapshot()
    {
        Request("daily", "Temp/arcade_panel_daily.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Achievements Panel")]
    public static void RequestAchievementsSnapshot()
    {
        Request("achievements", "Temp/arcade_panel_achievements.png", false);
    }

    [MenuItem("Arcade Survivor/Capture/Missions Panel")]
    public static void RequestMissionsSnapshot()
    {
        Request("missions", "Temp/arcade_panel_missions.png", false);
    }

    public static void RequestFromCommandLine()
    {
        Initialize("menu", DefaultCapturePath, true);
    }

    private static void Request(string mode, string output, bool exit)
    {
        if (SessionState.GetBool(ActiveKey, false))
        {
            Debug.LogWarning("Arcade menu capture is already running.");
            return;
        }

        File.WriteAllText(
            RequestPath,
            mode + Environment.NewLine + output + Environment.NewLine +
            (exit ? "exit" : "stay")
        );

        Initialize(mode, output, exit);
    }

    private static void Initialize(string mode, string output, bool exit)
    {
        SessionState.SetBool(ActiveKey, true);
        SessionState.SetString(StageKey, "enter");
        SessionState.SetString(ModeKey, mode);
        SessionState.SetString(OutputKey, output);
        SessionState.SetBool(ExitKey, exit);
        SessionState.SetInt(TourKey, 0);
        stageStarted = EditorApplication.timeSinceStartup;
        WriteStatus("enter " + mode);
        EditorApplication.update += Tick;
        EditorApplication.delayCall += BeginPlay;
    }

    private static void BeginPlay()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BeginPlay;
            return;
        }

        if (!EditorApplication.isPlaying)
            EditorApplication.isPlaying = true;
    }

    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;

        string stage = SessionState.GetString(StageKey, "enter");
        double elapsed = EditorApplication.timeSinceStartup - stageStarted;

        if (stage == "enter")
        {
            if (EditorApplication.isPlaying)
            {
                SetStage("wait");
                BeginPlay();
            }
            else if (elapsed > 30.0)
            {
                Fail("Play mode did not start.");
            }

            return;
        }

        if (!EditorApplication.isPlaying)
        {
            if (stage != "done")
                Fail("Play mode exited before capture. Stage: " + stage);

            return;
        }

        if (stage == "wait")
        {
            ArcadeMenuSkin skin = ArcadeMenuSkin.Instance;

            if (skin != null && skin.IsBuilt)
            {
                string mode = SessionState.GetString(ModeKey, "menu");

                if (mode == "tour")
                    mode = Tour[SessionState.GetInt(TourKey, 0)].Mode;

                ApplyMode(mode, skin);
                SetStage("settle");
            }
            else if (elapsed > 30.0)
            {
                Fail("ArcadeMenuSkin was not built.");
            }

            return;
        }

        if (stage == "settle")
        {
            if (elapsed > 1.4)
            {
                string output = Path.GetFullPath(
                    SessionState.GetString(OutputKey, DefaultCapturePath)
                );

                string directory = Path.GetDirectoryName(output);

                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                if (File.Exists(output))
                    File.Delete(output);

                ScreenCapture.CaptureScreenshot(output);
                SetStage("save");
            }

            return;
        }

        if (stage == "save")
        {
            string output = Path.GetFullPath(
                SessionState.GetString(OutputKey, DefaultCapturePath)
            );

            if (File.Exists(output) && new FileInfo(output).Length > 0)
            {
                if (SessionState.GetString(ModeKey, string.Empty) == "tour")
                {
                    AdvanceTour();
                    return;
                }

                Finish();
            }
            else if (elapsed > 8.0)
            {
                Fail("Screenshot file was not written: " + output);
            }
        }
    }

    private static void AdvanceTour()
    {
        int next = SessionState.GetInt(TourKey, 0) + 1;

        if (next >= Tour.Length)
        {
            Finish();
            return;
        }

        SessionState.SetInt(TourKey, next);
        SessionState.SetString(OutputKey, Tour[next].Path);

        ArcadeMenuSkin skin = ArcadeMenuSkin.Instance;

        if (skin == null || !skin.IsBuilt)
        {
            Fail("ArcadeMenuSkin disappeared during tour.");
            return;
        }

        ApplyMode(Tour[next].Mode, skin);
        SetStage("settle");
    }

    private static void ApplyMode(string mode, ArcadeMenuSkin skin)
    {
        MainMenuUI menu = UnityEngine.Object.FindFirstObjectByType<MainMenuUI>(
            FindObjectsInactive.Include
        );

        switch (mode)
        {
            case "settings":
                Invoke(skin, "OpenSettings");
                break;
            case "stats":
                Invoke(skin, "OpenStats");
                break;
            case "tips":
                Invoke(skin, "OpenTips");
                break;
            case "upgrades":
                Invoke(skin, "OpenLegacyPanel", "UpgradesPanel");
                break;
            case "equipment":
                Invoke(skin, "OpenLegacyPanel", "EquipmentPanel");
                break;
            case "maps":
                Invoke(skin, "OpenLegacyPanel", "MapsPanel");
                break;
            case "daily":
                Invoke(menu, "OpenDailyRewardPanel");
                break;
            case "achievements":
                Invoke(menu, "OpenAchievementsPanel");
                break;
            case "missions":
                Invoke(menu, "OpenMissionsPanel");
                break;
            case "field":
                SetRootActive(skin, false);
                break;
        }
    }

    private static void Invoke(object target, string method, params object[] args)
    {
        if (target == null)
        {
            Debug.LogWarning("ArcadeMenuCapture: target is null: " + method);
            return;
        }

        MethodInfo info = target.GetType().GetMethod(
            method,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
        );

        if (info == null)
        {
            Debug.LogWarning("ArcadeMenuCapture: method not found: " + method);
            return;
        }

        info.Invoke(target, args);
    }

    private static void SetRootActive(ArcadeMenuSkin skin, bool active)
    {
        FieldInfo info = typeof(ArcadeMenuSkin).GetField(
            "root",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        GameObject root = info != null ? info.GetValue(skin) as GameObject : null;

        if (root != null)
            root.SetActive(active);
    }

    private static void SetStage(string stage)
    {
        SessionState.SetString(StageKey, stage);
        stageStarted = EditorApplication.timeSinceStartup;
        WriteStatus(stage);
    }

    private static void Finish()
    {
        SessionState.SetString(StageKey, "done");
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        WriteStatus("done");

        if (SessionState.GetBool(ExitKey, false))
        {
            EditorApplication.Exit(0);
            return;
        }

        EditorApplication.isPlaying = false;
    }

    private static void Fail(string message)
    {
        SessionState.SetBool(ActiveKey, false);
        EditorApplication.update -= Tick;
        WriteStatus("failed: " + message);
        Debug.LogError("Arcade menu capture failed: " + message);

        if (SessionState.GetBool(ExitKey, false))
        {
            EditorApplication.Exit(1);
            return;
        }

        if (EditorApplication.isPlaying)
            EditorApplication.isPlaying = false;
    }

    private static void WriteStatus(string value)
    {
        File.WriteAllText(
            Path.GetFullPath(StatusPath),
            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + value
        );
    }
}

#endif
