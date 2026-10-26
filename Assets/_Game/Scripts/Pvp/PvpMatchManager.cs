using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PvpMatchManager : MonoBehaviour
{
    private const float PvpHealth = 5000f;

    [SerializeField] private WeaponData aiWeapon;
    [SerializeField] private float aiHealth = PvpHealth;

    private PvpCombatant aiCombatant;
    private bool matchStarted;
    private bool matchEnded;
    private Canvas hudCanvas;
    private GameObject resultPanel;
    private TMP_Text playerHealthLabel;
    private TMP_Text aiHealthLabel;
    private TMP_Text resultTitle;
    private Image playerHealthFill;
    private Image aiHealthFill;
    private GameObject searchPanel;
    private TMP_Text searchLabel;
    private Coroutine searchRoutine;
    private Coroutine arenaRoutine;
    private Coroutine matchSetupRoutine;
    private Coroutine restoreRoutine;
    private GameMap pvpMap;
    private bool arenaReady;
    private bool initialized;
    private int modeGeneration;
    private string opponentName;

    private static readonly string[] OpponentFirstNames =
    {
        "Alex", "Jordan", "Sam", "Taylor", "Casey", "Morgan",
        "Riley", "Jamie", "Drew", "Avery", "Cameron", "Quinn"
    };

    private static readonly string[] OpponentLastNames =
    {
        "Morgan", "Blake", "Carter", "Reed", "Brooks", "Hayes",
        "Bennett", "Foster", "Parker", "Collins", "Ellis", "Harper"
    };

    public static PvpMatchManager EnsureExists()
    {
        PvpMatchManager existing = FindAnyObjectByType<PvpMatchManager>();
        if (existing != null)
        {
            existing.enabled = true;
            existing.InitializeForPvp();
            return existing;
        }

        PvpMatchManager created =
            new GameObject("PvpMatchManager").AddComponent<PvpMatchManager>();
        created.InitializeForPvp();
        return created;
    }

    public void BeginSearch()
    {
        if (searchRoutine != null || matchStarted)
            return;

        if (hudCanvas != null)
            hudCanvas.gameObject.SetActive(true);

        SetStandardHudHidden(true);

        PreviewArena();
        searchRoutine = StartCoroutine(SearchRoutine());
    }

    public void PreviewArena()
    {
        if (arenaRoutine != null || arenaReady)
            return;

        if (pvpMap == null)
            pvpMap = CreatePvpMap();
        arenaRoutine = StartCoroutine(PrepareArenaVisuals());
    }

    public void CancelAndReturnToPve()
    {
        modeGeneration++;
        Time.timeScale = 1f;
        initialized = false;
        arenaReady = false;
        matchStarted = false;
        matchEnded = false;
        aiCombatant = null;
        opponentName = string.Empty;
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;

        if (searchRoutine != null)
        {
            StopCoroutine(searchRoutine);
            searchRoutine = null;
        }

        if (arenaRoutine != null)
        {
            StopCoroutine(arenaRoutine);
            arenaRoutine = null;
        }

        if (matchSetupRoutine != null)
        {
            StopCoroutine(matchSetupRoutine);
            matchSetupRoutine = null;
        }

        PvpCombatant[] combatants = FindObjectsByType<PvpCombatant>(
            FindObjectsInactive.Include, FindObjectsSortMode.None
        );
        for (int i = 0; i < combatants.Length; i++)
        {
            combatants[i].gameObject.SetActive(false);
            Destroy(combatants[i]);
        }

        PvpProjectile[] projectiles = FindObjectsByType<PvpProjectile>(
            FindObjectsInactive.Include, FindObjectsSortMode.None
        );
        for (int i = 0; i < projectiles.Length; i++)
        {
            projectiles[i].gameObject.SetActive(false);
            Destroy(projectiles[i]);
        }

        if (hudCanvas != null)
        {
            hudCanvas.gameObject.SetActive(false);
            Destroy(hudCanvas.gameObject);
            hudCanvas = null;
        }

        SetStandardHudHidden(false);

        EnvironmentController environment = FindAnyObjectByType<EnvironmentController>();
        if (environment != null)
        {
            if (restoreRoutine != null)
            {
                StopCoroutine(restoreRoutine);
                restoreRoutine = null;
            }

            if (environment.RestorePreviousMap())
            {
                restoreRoutine = StartCoroutine(
                    RebuildPveStructuresAfterRestore(environment, modeGeneration)
                );
            }
            return;
        }
    }

    private void SetStandardHudHidden(bool hidden)
    {
        GameplayUI[] gameplayUis = FindObjectsByType<GameplayUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None
        );
        for (int i = 0; i < gameplayUis.Length; i++)
            gameplayUis[i].SetPvpHidden(hidden);

        GameplayHUD[] leaderboardHuds = FindObjectsByType<GameplayHUD>(
            FindObjectsInactive.Include, FindObjectsSortMode.None
        );
        for (int i = 0; i < leaderboardHuds.Length; i++)
            leaderboardHuds[i].SetPvpHidden(hidden);
    }

    private IEnumerator RebuildPveStructuresAfterRestore(
        EnvironmentController environment,
        int generation)
    {
        while (environment.IsTransitioning || environment.IsRestoringRuntimeMap)
            yield return null;

        if (generation != modeGeneration || GameModeManager.IsPvp)
        {
            restoreRoutine = null;
            yield break;
        }

        WorldStructureGenerator generator = FindAnyObjectByType<WorldStructureGenerator>();
        if (generator != null && environment.CurrentMap != null)
        {
            generator.RebuildForWave(environment.CurrentMap.layoutSeed, 1);
            while (generator.IsGenerating)
                yield return null;
        }

        restoreRoutine = null;
    }

    private void Start()
    {
        InitializeForPvp();
    }

    private void InitializeForPvp()
    {
        if (initialized || !GameModeManager.IsPvp)
        {
            if (!GameModeManager.IsPvp)
                enabled = false;
            return;
        }

        initialized = true;

        if (GameOverManager.Instance != null)
            GameOverManager.Instance.ResetForNewRun();

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
            if (GameStateManager.Instance.CurrentState == GameState.Playing)
                StartMatch();
        }

        if (hudCanvas == null)
            BuildHud();
        SetStandardHudHidden(true);
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;

        if (pvpMap != null)
            Destroy(pvpMap);
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Playing && !matchStarted)
            StartMatch();

        if (hudCanvas != null)
            hudCanvas.gameObject.SetActive(
                state == GameState.Playing || state == GameState.GameOver
            );
    }

    private void StartMatch()
    {
        if (matchStarted)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        matchStarted = true;
        opponentName = OpponentFirstNames[Random.Range(0, OpponentFirstNames.Length)] +
            " " + OpponentLastNames[Random.Range(0, OpponentLastNames.Length)];
        PvpCombatant playerCombatant = player.GetComponent<PvpCombatant>();
        if (playerCombatant == null)
            playerCombatant = player.AddComponent<PvpCombatant>();
        playerCombatant.Configure(PvpHealth, true);
        matchSetupRoutine = StartCoroutine(PrepareArenaAndSpawn(player.transform));
    }

    private IEnumerator SearchRoutine()
    {
        if (searchPanel != null)
            searchPanel.SetActive(true);

        int duration = Random.Range(3, 11);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (searchLabel != null)
                searchLabel.text = Lang.GetOr("menu.pvp.search", "ПОИСК ПРОТИВНИКА") +
                    "\n" + Mathf.Clamp(Mathf.FloorToInt(elapsed) + 1, 1, duration) + " с";
            yield return null;
        }

        searchRoutine = null;
        if (searchPanel != null)
            searchPanel.SetActive(false);
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.StartGame();
    }

    private IEnumerator PrepareArenaVisuals()
    {
        EnvironmentController environment = FindAnyObjectByType<EnvironmentController>();
        if (environment != null)
        {
            while (environment.IsTransitioning || environment.IsRestoringRuntimeMap)
                yield return null;

            if (!environment.SwitchToRuntimeMap(pvpMap))
            {
                arenaRoutine = null;
                yield break;
            }

            while (environment.IsTransitioning)
                yield return null;
        }

        WorldStructureGenerator generator = FindAnyObjectByType<WorldStructureGenerator>();
        if (generator != null)
        {
            generator.RebuildForWave(pvpMap.layoutSeed, 1);
            while (generator.IsGenerating)
                yield return null;
        }

        arenaReady = true;
        arenaRoutine = null;
    }

    private IEnumerator PrepareArenaAndSpawn(Transform player)
    {
        PreviewArena();
        while (!arenaReady)
        {
            if (arenaRoutine == null)
                yield break;
            yield return null;
        }

        if (player != null)
        {
            Vector3 playerSpawn = FindClearSpawn(new Vector3(0f, 0.5f, -16f));
            Vector3 aiSpawn = FindClearSpawn(new Vector3(0f, 0.5f, 16f));
            SetPosition(player, playerSpawn);
            SpawnAi(player, aiSpawn);
        }

        matchSetupRoutine = null;
    }

    private static void SetPosition(Transform target, Vector3 position)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        target.position = position;
    }

    private static Vector3 FindClearSpawn(Vector3 preferred)
    {
        for (int ring = 0; ring < 5; ring++)
        {
            float z = Mathf.Sign(preferred.z) * (16f + ring * 2f);
            for (int xStep = 0; xStep < 7; xStep++)
            {
                int offset = xStep == 0 ? 0 : ((xStep + 1) / 2) * 4 * (xStep % 2 == 1 ? 1 : -1);
                Vector3 candidate = new Vector3(offset, preferred.y, z);
                Collider[] overlaps = Physics.OverlapSphere(candidate, 0.9f);
                bool blocked = false;
                for (int i = 0; i < overlaps.Length; i++)
                {
                    if (overlaps[i] != null && StructureQuery.IsWorldStructure(overlaps[i]))
                    {
                        blocked = true;
                        break;
                    }
                }

                if (!blocked)
                    return candidate;
            }
        }

        return preferred;
    }

    private static GameMap CreatePvpMap()
    {
        GameMap map = ScriptableObject.CreateInstance<GameMap>();
        map.name = "Runtime_Pvp_Arena";
        map.mapName = Lang.GetOr("menu.pvp.arena", "AI Duel Arena");
        map.langKey = "pvp.arena";
        map.layoutSeed = 76123;
        map.skyColor = new Color(0.055f, 0.09f, 0.13f);
        map.ambientLightColor = new Color(0.55f, 0.62f, 0.66f);
        map.buildThemeEnvironment = false;
        map.arenaLayout = new ArenaLayout
        {
            style = ArenaLayoutStyle.Walls,
            minStructures = 14,
            maxStructures = 18,
            minWidth = 2.2f,
            maxWidth = 4.5f,
            minHeight = 1.2f,
            maxHeight = 3f,
            minSpacing = 1.6f,
            halfSize = 25f,
            centerClearRadius = 8f
        };
        return map;
    }

    private void SpawnAi(Transform player, Vector3 spawnPosition)
    {
        GameObject ai = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ai.transform.position = spawnPosition;
        ai.transform.localScale = Vector3.one;
        ai.name = "PvpAIPlayer";

        MeshRenderer aiRenderer = ai.GetComponent<MeshRenderer>();
        MeshRenderer playerRenderer = player.GetComponent<MeshRenderer>();
        if (aiRenderer != null && playerRenderer != null)
            aiRenderer.sharedMaterial = playerRenderer.sharedMaterial;

        Rigidbody body = ai.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.constraints = RigidbodyConstraints.FreezeRotation;

        Transform weaponRoot = new GameObject("Weapon").transform;
        weaponRoot.SetParent(ai.transform, false);
        weaponRoot.localPosition = new Vector3(0f, 0f, 0.7f);

        Transform firePoint = new GameObject("FirePoint").transform;
        firePoint.SetParent(weaponRoot, false);
        firePoint.localPosition = new Vector3(0f, 0f, 0.8f);

        PvpCombatant combatant = ai.AddComponent<PvpCombatant>();
        combatant.Configure(aiHealth, false);
        PvpAiController controller = ai.AddComponent<PvpAiController>();
        controller.Configure(player, aiWeapon != null ? aiWeapon : FindPlayerWeaponData());
        TintOpponent(ai);
        aiCombatant = combatant;
    }

    private static WeaponData FindPlayerWeaponData()
    {
        UpgradeManager manager = UpgradeManager.Instance;
        if (manager == null)
            return null;

        string equipped = EquipmentManager.EquippedWeaponName;
        foreach (WeaponData candidate in manager.GetAvailableWeapons())
        {
            if (candidate != null && candidate.WeaponName == equipped)
                return candidate;
        }

        return manager.GetAvailableWeapons().Count > 0
            ? manager.GetAvailableWeapons()[0]
            : null;
    }

    private static void TintOpponent(GameObject ai)
    {
        Renderer renderer = ai.GetComponent<Renderer>();
        if (renderer == null)
            return;

        MaterialPropertyBlock block = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(block);
        Color color = ArcadeUiTheme.Orange;
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        renderer.SetPropertyBlock(block);
    }

    private void BuildHud()
    {
        GameObject root = new GameObject(
            "PvpHud",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster)
        );
        hudCanvas = root.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 60;

        CanvasScaler scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform canvasRect = root.GetComponent<RectTransform>();

        playerHealthLabel = ArcadeUiFactory.Label(
            canvasRect, "PlayerHealth", "", ArcadeKitAssets.BodyBold, 18f,
            ArcadeUiTheme.TextPrimary, TextAlignmentOptions.Left,
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(48f, -42f),
            new Vector2(340f, 30f)
        );
        playerHealthFill = BuildHealthBar(
            canvasRect, "PlayerHealthBar", new Vector2(48f, -80f), ArcadeUiTheme.Green
        );
        aiHealthLabel = ArcadeUiFactory.Label(
            canvasRect, "AIHealth", "", ArcadeKitAssets.BodyBold, 18f,
            ArcadeUiTheme.TextPrimary, TextAlignmentOptions.Right,
            new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -42f),
            new Vector2(340f, 30f)
        );
        aiHealthFill = BuildHealthBar(
            canvasRect, "AIHealthBar", new Vector2(-48f, -80f), ArcadeUiTheme.Orange, true
        );
        resultPanel = BuildResultPanel(canvasRect);
        BuildSearchPanel(canvasRect);
        resultPanel.SetActive(false);
        root.SetActive(GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing);
    }

    private static Image BuildHealthBar(
        RectTransform parent,
        string name,
        Vector2 position,
        Color color,
        bool right = false)
    {
        RectTransform rect = ArcadeUiFactory.At(
            parent, name,
            right ? new Vector2(1f, 1f) : new Vector2(0f, 1f),
            right ? new Vector2(1f, 1f) : new Vector2(0f, 1f),
            position,
            new Vector2(340f, 14f)
        );
        ArcadeUiFactory.Image(rect, ArcadeKitAssets.BarTrack, ArcadeUiTheme.Panel, true);
        RectTransform fill = ArcadeUiFactory.Stretch(rect, "Fill", 2f, 2f, 2f, 2f);
        Image image = ArcadeUiFactory.Image(fill, ArcadeKitAssets.BarFill, color, true);
        image.type = Image.Type.Filled;
        image.fillMethod = Image.FillMethod.Horizontal;
        image.fillOrigin = right
            ? (int)Image.OriginHorizontal.Right
            : (int)Image.OriginHorizontal.Left;
        return image;
    }

    private GameObject BuildResultPanel(RectTransform parent)
    {
        RectTransform panel = ArcadeUiFactory.At(
            parent, "PvpResultPanel", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(520f, 300f)
        );
        ArcadeUiFactory.Image(
            panel,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f),
            true
        );
        resultTitle = ArcadeUiFactory.Label(
            panel, "ResultTitle", "", ArcadeKitAssets.Display, 34f,
            ArcadeUiTheme.GoldBright, TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.72f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(460f, 60f)
        );

        RectTransform menu = ArcadeUiFactory.At(
            panel, "Menu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 42f), new Vector2(260f, 58f)
        );
        ArcadeUiFactory.Button(
            menu, ArcadeKitAssets.RoundRect, ArcadeUiTheme.PanelRaised,
            Lang.GetOr("menu.pvp.menu", "В МЕНЮ"), ArcadeKitAssets.BodyBold,
            16f, ArcadeUiTheme.TextPrimary, ReturnToMenu
        );
        return panel.gameObject;
    }

    private void BuildSearchPanel(RectTransform parent)
    {
        RectTransform panel = ArcadeUiFactory.At(
            parent, "PvpSearchPanel", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 170f)
        );
        ArcadeUiFactory.Image(
            panel, ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f), true
        );
        searchLabel = ArcadeUiFactory.Label(
            panel, "SearchLabel",
            Lang.GetOr("menu.pvp.search", "ПОИСК ПРОТИВНИКА"),
            ArcadeKitAssets.Display, 26f, ArcadeUiTheme.GoldBright,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            Vector2.zero, new Vector2(420f, 100f)
        );
        searchPanel = panel.gameObject;
        searchPanel.SetActive(false);
    }

    private void Update()
    {
        if (!matchStarted || playerHealthLabel == null)
            return;

        PvpCombatant player = FindPlayerCombatant();
        if (player != null)
        {
            playerHealthLabel.text = Lang.GetOr("menu.pvp.player", "ИГРОК") +
                "  " + Mathf.CeilToInt(player.CurrentHealth) + " / " + Mathf.CeilToInt(player.MaxHealth);
            playerHealthFill.fillAmount = player.CurrentHealth / player.MaxHealth;
        }

        if (aiCombatant != null)
        {
            aiHealthLabel.text = opponentName +
                "  " + Mathf.CeilToInt(aiCombatant.CurrentHealth) + " / " + Mathf.CeilToInt(aiCombatant.MaxHealth);
            aiHealthFill.fillAmount = aiCombatant.CurrentHealth / aiCombatant.MaxHealth;
        }
    }

    private static PvpCombatant FindPlayerCombatant()
    {
        PvpCombatant[] combatants = FindObjectsByType<PvpCombatant>(FindObjectsSortMode.None);
        for (int i = 0; i < combatants.Length; i++)
        {
            if (combatants[i].IsPlayer)
                return combatants[i];
        }

        return null;
    }

    public void HandleCombatantDefeated(PvpCombatant defeated)
    {
        if (matchEnded)
            return;

        matchEnded = true;
        resultTitle.text = defeated == aiCombatant
            ? Lang.GetOr("menu.pvp.victory", "ПОБЕДА")
            : Lang.GetOr("menu.pvp.defeat", "ПОРАЖЕНИЕ");
        defeated.gameObject.SetActive(false);
        resultPanel.SetActive(true);

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.GameOver);

        Time.timeScale = 0f;
    }

    private void ReturnToMenu()
    {
        Time.timeScale = 1f;
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.ResetForNewRun();
        GameModeManager.Select(GameMode.Pve);
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.BackToMenu();
        else if (GameStateManager.Instance != null)
            GameStateManager.Instance.ReturnToMenu();
    }
}
