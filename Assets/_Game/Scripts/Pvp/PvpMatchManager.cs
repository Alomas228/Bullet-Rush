using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PvpMatchManager : MonoBehaviour
{
    private const float PvpHealth = 5000f;
    private const float RoundDuration = 45f;
    private const float IntermissionDuration = 15f;
    private const int RoundsToWin = 2;

    private enum BoostType
    {
        Power,
        Armor,
        Recovery
    }

    [SerializeField] private WeaponData aiWeapon;
    [SerializeField] private float aiHealth = PvpHealth;

    private PvpCombatant aiCombatant;
    private bool matchStarted;
    private bool matchEnded;
    private Canvas hudCanvas;
    private GameObject resultPanel;
    private TMP_Text playerHealthLabel;
    private TMP_Text aiHealthLabel;
    private TMP_Text matchStatusLabel;
    private TMP_Text resultDetails;
    private TMP_Text resultTitle;
    private Image playerHealthFill;
    private Image aiHealthFill;
    private GameObject searchPanel;
    private GameObject searchBackdrop;
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
    private int opponentRating;
    private int opponentStyle;
    private int roundNumber;
    private int playerRoundWins;
    private int opponentRoundWins;
    private float roundTimeRemaining;
    private float intermissionTimeRemaining;
    private float roundPlayerDamageMultiplier = 1f;
    private float roundDamageTakenMultiplier = 1f;
    private float nextRoundHealthMultiplier = 1f;
    private bool roundActive;
    private bool intermissionActive;
    private int lastRatingDelta;
    private bool dailyRewardEarned;
    private PvpProgressionManager progression;
    private GameObject intermissionPanel;
    private TMP_Text intermissionTitle;
    private TMP_Text intermissionTimerLabel;
    private TMP_Text intermissionScore;
    private TMP_Text intermissionHint;
    private Button[] boostButtons;
    private TMP_Text[] boostLabels;

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

        EnsurePvpArena();
        progression = PvpProgressionManager.EnsureExists();
        searchRoutine = StartCoroutine(SearchRoutine());
    }

    private void CancelSearch()
    {
        if (searchRoutine == null)
            return;

        StopCoroutine(searchRoutine);
        searchRoutine = null;

        if (searchPanel != null)
            searchPanel.SetActive(false);

        if (searchBackdrop != null)
            searchBackdrop.SetActive(false);

        SetStandardHudHidden(false);
    }

    public void PreviewArena()
    {
        EnvironmentController environment = FindAnyObjectByType<EnvironmentController>();
        if (arenaReady && environment != null && environment.CurrentMap != pvpMap)
            arenaReady = false;

        if (arenaRoutine != null || arenaReady)
            return;

        if (pvpMap == null)
            pvpMap = CreatePvpMap();
        arenaRoutine = StartCoroutine(PrepareArenaVisuals());
    }

    private void EnsurePvpArena()
    {
        EnvironmentController environment = FindAnyObjectByType<EnvironmentController>();
        if (environment != null && environment.CurrentMap != pvpMap)
            arenaReady = false;

        PreviewArena();
    }

    public void CancelAndReturnToPve()
    {
        modeGeneration++;
        Time.timeScale = 1f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToMenuMusic();

        initialized = false;
        arenaReady = false;
        matchStarted = false;
        matchEnded = false;
        aiCombatant = null;
        opponentName = string.Empty;
        roundActive = false;
        intermissionActive = false;
        roundPlayerDamageMultiplier = 1f;
        roundDamageTakenMultiplier = 1f;
        nextRoundHealthMultiplier = 1f;
        PvpCombatant.SetPlayerDamageMultiplier(1f);
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

        if (restoreRoutine != null)
        {
            StopCoroutine(restoreRoutine);
            restoreRoutine = null;
        }

        PvpCombatant[] combatants = FindObjectsByType<PvpCombatant>(
            FindObjectsInactive.Include, FindObjectsSortMode.None
        );
        for (int i = 0; i < combatants.Length; i++)
        {
            PvpCombatant combatant = combatants[i];
            if (combatant == null)
                continue;

            if (combatant.IsPlayer)
            {
                Destroy(combatant);
                continue;
            }

            combatant.gameObject.SetActive(false);
            Destroy(combatant.gameObject);
        }

        PvpProjectile[] projectiles = FindObjectsByType<PvpProjectile>(
            FindObjectsInactive.Include, FindObjectsSortMode.None
        );
        for (int i = 0; i < projectiles.Length; i++)
        {
            projectiles[i].gameObject.SetActive(false);
            Destroy(projectiles[i]);
        }

        ClearPlayerBullets();

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
            else
            {
                WaveManager waves = FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);
                if (waves != null)
                    waves.ActivateForPve();
            }
            if (GameStateManager.Instance != null &&
                GameStateManager.Instance.CurrentState == GameState.Menu)
                Time.timeScale = 0f;
            return;
        }

        WaveManager pveWaves = FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (pveWaves != null)
            pveWaves.ActivateForPve();
        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Menu)
            Time.timeScale = 0f;
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

        WaveManager waves = FindAnyObjectByType<WaveManager>(FindObjectsInactive.Include);
        if (waves != null)
            waves.ActivateForPve();

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

        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToMainMusic();

        matchStarted = true;
        progression = PvpProgressionManager.EnsureExists();
        if (string.IsNullOrEmpty(opponentName))
            SelectOpponent();
        playerRoundWins = 0;
        opponentRoundWins = 0;
        roundNumber = 0;
        roundPlayerDamageMultiplier = 1f;
        roundDamageTakenMultiplier = 1f;
        nextRoundHealthMultiplier = 1f;
        dailyRewardEarned = false;
        PvpCombatant playerCombatant = player.GetComponent<PvpCombatant>();
        if (playerCombatant == null)
            playerCombatant = player.AddComponent<PvpCombatant>();
        playerCombatant.Configure(PvpHealth, true);
        matchSetupRoutine = StartCoroutine(PrepareArenaAndSpawn(player.transform));
    }

    private void SelectOpponent()
    {
        opponentName = OpponentFirstNames[Random.Range(0, OpponentFirstNames.Length)] +
            " " + OpponentLastNames[Random.Range(0, OpponentLastNames.Length)];
        int playerRating = progression != null ? progression.Rating : 1000;
        opponentRating = Mathf.Max(0, playerRating + Random.Range(-90, 91));
        opponentStyle = Random.Range(0, 3);
    }

    private IEnumerator SearchRoutine()
    {
        if (searchPanel != null)
            searchPanel.SetActive(true);

        if (searchBackdrop != null)
            searchBackdrop.SetActive(true);

        float elapsed = 0f;
        float nextFeedbackTime = 0f;
        int duration = Random.Range(3, 11);
        string searchText = Lang.GetOr("menu.pvp.search", "ПОИСК ПРОТИВНИКА");
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            if (elapsed >= nextFeedbackTime)
            {
                int dots = Mathf.FloorToInt(elapsed * 2f) % 3 + 1;
                int seconds = Mathf.Clamp(Mathf.FloorToInt(elapsed), 0, duration);
                if (searchLabel != null)
                    searchLabel.text = searchText + "\n" +
                        new string('.', dots) + "  " + seconds + " с";

                if (AudioManager.Instance != null &&
                    AudioManager.Instance.SFXLibrary != null)
                {
                    AudioManager.Instance.PlaySFX(
                        AudioManager.Instance.SFXLibrary.CountdownTick,
                        0.45f,
                        SfxPriority.Low
                    );
                }

                nextFeedbackTime += 0.5f;
            }
            yield return null;
        }

        searchRoutine = null;
        if (searchPanel != null)
            searchPanel.SetActive(false);

        if (searchBackdrop != null)
            searchBackdrop.SetActive(false);
        SelectOpponent();
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
        EnsurePvpArena();
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
            StartRound(player, playerSpawn, aiSpawn);
        }

        matchSetupRoutine = null;
    }

    private static void SetPosition(Transform target, Vector3 position)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = position;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
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
        float difficulty = Mathf.Clamp(1f + (opponentRating -
            (progression != null ? progression.Rating : 1000)) / 1200f, 0.82f, 1.18f);
        combatant.Configure(aiHealth * difficulty, false);
        PvpAiController controller = ai.AddComponent<PvpAiController>();
        controller.Configure(player, aiWeapon != null ? aiWeapon : FindPlayerWeaponData());
        controller.ApplyProfile(opponentStyle, difficulty);
        TintOpponent(ai);
        aiCombatant = combatant;
    }

    private void StartRound(Transform player, Vector3 playerSpawn, Vector3 aiSpawn)
    {
        if (player == null)
            return;

        roundNumber++;
        PvpCombatant playerCombatant = player.GetComponent<PvpCombatant>();
        if (playerCombatant == null)
            playerCombatant = player.gameObject.AddComponent<PvpCombatant>();
        playerCombatant.Configure(PvpHealth * nextRoundHealthMultiplier, true);
        playerCombatant.SetDamageTakenMultiplier(roundDamageTakenMultiplier);
        PvpCombatant.SetPlayerDamageMultiplier(roundPlayerDamageMultiplier);
        SetPosition(player, playerSpawn);
        ClearRoundEntities();
        SpawnAi(player, aiSpawn);
        CameraFollow camera = FindAnyObjectByType<CameraFollow>();
        if (camera != null)
        {
            camera.SetTarget(player);
            camera.MoveToGameplayPositionImmediate();
        }
        roundTimeRemaining = RoundDuration;
        nextRoundHealthMultiplier = 1f;
        roundActive = true;
        intermissionActive = false;
        if (intermissionPanel != null)
            intermissionPanel.SetActive(false);
        if (resultPanel != null)
            resultPanel.SetActive(false);
        Time.timeScale = 1f;
        if (AudioManager.Instance != null)
            AudioManager.Instance.SwitchToMainMusic();
        RefreshMatchStatus();
    }

    private void ClearRoundEntities()
    {
        if (aiCombatant != null)
        {
            GameObject oldAi = aiCombatant.gameObject;
            aiCombatant = null;
            oldAi.SetActive(false);
            Destroy(oldAi);
        }

        PvpProjectile[] projectiles = FindObjectsByType<PvpProjectile>(
            FindObjectsInactive.Include, FindObjectsSortMode.None
        );
        for (int i = 0; i < projectiles.Length; i++)
        {
            projectiles[i].gameObject.SetActive(false);
            Destroy(projectiles[i]);
        }
    }

    private static void ClearPlayerBullets()
    {
        Bullet[] bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);
        for (int i = 0; i < bullets.Length; i++)
        {
            if (bullets[i] != null)
                bullets[i].ReturnToPool();
        }
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
        matchStatusLabel = ArcadeUiFactory.Label(
            canvasRect, "MatchStatus", "", ArcadeKitAssets.BodyBold, 18f,
            ArcadeUiTheme.GoldBright, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -48f), new Vector2(520f, 30f)
        );
        resultPanel = BuildResultPanel(canvasRect);
        BuildIntermissionPanel(canvasRect);
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
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 340f)
        );
        ArcadeUiFactory.Image(
            panel,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f),
            true
        );
        Shadow shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = ArcadeUiTheme.Shadow;
        shadow.effectDistance = new Vector2(0f, -8f);
        shadow.useGraphicAlpha = true;
        resultTitle = ArcadeUiFactory.Label(
            panel, "ResultTitle", "", ArcadeKitAssets.Display, 34f,
            ArcadeUiTheme.GoldBright, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -38f), new Vector2(500f, 60f)
        );
        resultDetails = ArcadeUiFactory.Label(
            panel, "ResultDetails", "", ArcadeKitAssets.BodyBold, 18f,
            ArcadeUiTheme.TextPrimary, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -112f), new Vector2(500f, 84f)
        );

        RectTransform menu = ArcadeUiFactory.At(
            panel, "Menu", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
            new Vector2(0f, 28f), new Vector2(280f, 58f)
        );
        ArcadeUiFactory.Button(
            menu, ArcadeKitAssets.RoundRect, ArcadeUiTheme.PanelRaised,
            Lang.GetOr("menu.pvp.menu", "В МЕНЮ"), ArcadeKitAssets.BodyBold,
            16f, ArcadeUiTheme.TextPrimary, ReturnToMenu
        );
        return panel.gameObject;
    }

    private void BuildIntermissionPanel(RectTransform parent)
    {
        RectTransform panel = ArcadeUiFactory.At(
            parent, "PvpRoundIntermission", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(680f, 450f)
        );
        ArcadeUiFactory.Image(panel, ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f), true);
        intermissionPanel = panel.gameObject;
        intermissionTitle = ArcadeUiFactory.Label(
            panel, "IntermissionTitle", "", ArcadeKitAssets.Display, 28f,
            ArcadeUiTheme.GoldBright, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -60f), new Vector2(620f, 54f)
        );
        intermissionTimerLabel = ArcadeUiFactory.Label(
            panel, "IntermissionTimer", "", ArcadeKitAssets.BodyBold, 17f,
            ArcadeUiTheme.Cyan, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -16f), new Vector2(620f, 28f)
        );
        intermissionScore = ArcadeUiFactory.Label(
            panel, "IntermissionScore", "", ArcadeKitAssets.BodyBold, 20f,
            ArcadeUiTheme.TextPrimary, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -94f), new Vector2(620f, 34f)
        );
        intermissionHint = ArcadeUiFactory.Label(
            panel, "IntermissionHint", "", ArcadeKitAssets.Body, 15f,
            ArcadeUiTheme.TextSecondary, TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -130f), new Vector2(620f, 28f)
        );

        boostButtons = new Button[3];
        boostLabels = new TMP_Text[3];
        for (int i = 0; i < boostButtons.Length; i++)
        {
            int choice = i;
            RectTransform buttonRect = ArcadeUiFactory.At(
                panel, "Boost" + i, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -205f - i * 74f), new Vector2(560f, 62f)
            );
            boostButtons[i] = ArcadeUiFactory.Button(
                buttonRect, ArcadeKitAssets.RoundRect, ArcadeUiTheme.PanelRaised,
                "", ArcadeKitAssets.BodyBold, 16f, ArcadeUiTheme.TextPrimary,
                () => ChooseBoost(choice)
            );
            boostLabels[i] = buttonRect.Find("Label").GetComponent<TMP_Text>();
        }
        intermissionPanel.SetActive(false);
    }

    private void BuildSearchPanel(RectTransform parent)
    {
        RectTransform backdrop = ArcadeUiFactory.Stretch(
            parent,
            "PvpSearchBackdrop"
        );
        Image backdropImage = ArcadeUiFactory.Image(
            backdrop,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Backdrop,
            true
        );
        backdropImage.raycastTarget = true;
        searchBackdrop = backdrop.gameObject;

        RectTransform panel = ArcadeUiFactory.At(
            parent, "PvpSearchPanel", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(560f, 340f)
        );
        ArcadeUiFactory.Image(
            panel, ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.PanelRaised, true
        );
        Shadow shadow = panel.gameObject.AddComponent<Shadow>();
        shadow.effectColor = ArcadeUiTheme.Shadow;
        shadow.effectDistance = new Vector2(0f, -8f);
        shadow.useGraphicAlpha = true;

        searchLabel = ArcadeUiFactory.Label(
            panel, "SearchLabel",
            Lang.GetOr("menu.pvp.search", "ПОИСК ПРОТИВНИКА"),
            ArcadeKitAssets.Display, 26f, ArcadeUiTheme.GoldBright,
            TextAlignmentOptions.Center,
            new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
            new Vector2(0f, -34f), new Vector2(500f, 150f)
        );

        RectTransform cancel = ArcadeUiFactory.At(
            panel,
            "CancelSearch",
            new Vector2(0.5f, 0f),
            new Vector2(0.5f, 0f),
            new Vector2(0f, 28f),
            new Vector2(280f, 56f)
        );
        ArcadeUiFactory.Button(
            cancel,
            ArcadeKitAssets.RoundRect,
            ArcadeUiTheme.Red,
            Lang.GetOr("menu.pvp.cancel_search", "ОТМЕНА"),
            ArcadeKitAssets.BodyBold,
            16f,
            ArcadeUiTheme.TextDark,
            CancelSearch
        );

        searchPanel = panel.gameObject;
        searchBackdrop.SetActive(false);
        searchPanel.SetActive(false);
    }

    private void Update()
    {
        if (!matchStarted || playerHealthLabel == null)
            return;

        if (roundActive && GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing)
        {
            roundTimeRemaining -= Time.deltaTime;
            if (roundTimeRemaining <= 0f)
                ResolveRoundByTime();
        }

        if (intermissionActive)
        {
            intermissionTimeRemaining -= Time.unscaledDeltaTime;
            int seconds = Mathf.CeilToInt(Mathf.Max(intermissionTimeRemaining, 0f));
            intermissionTimerLabel.text = Lang.GetOr(
                "menu.pvp.choice_timer", "CHOICE IN {0} SECONDS"
            ).Replace("{0}", seconds.ToString());

            if (intermissionTimeRemaining <= 0f)
                ChooseBoost(Random.Range(0, boostButtons.Length));
        }

        PvpCombatant player = FindPlayerCombatant();
        if (player != null)
        {
            playerHealthLabel.text = Lang.GetOr("menu.pvp.player", "ИГРОК") +
                "  " + Mathf.CeilToInt(player.CurrentHealth) + " / " + Mathf.CeilToInt(player.MaxHealth);
            playerHealthFill.fillAmount = player.CurrentHealth / player.MaxHealth;
        }

        if (aiCombatant != null)
        {
            aiHealthLabel.text = opponentName + " · " + opponentRating + " RP" +
                "  " + Mathf.CeilToInt(aiCombatant.CurrentHealth) + " / " + Mathf.CeilToInt(aiCombatant.MaxHealth);
            aiHealthFill.fillAmount = aiCombatant.CurrentHealth / aiCombatant.MaxHealth;
        }

        RefreshMatchStatus();
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
        if (matchEnded || !roundActive)
            return;

        ResolveRound(defeated == aiCombatant);
    }

    private void ResolveRoundByTime()
    {
        PvpCombatant player = FindPlayerCombatant();
        float playerHealth = player != null ? player.CurrentHealth : 0f;
        float aiHealthValue = aiCombatant != null ? aiCombatant.CurrentHealth : 0f;
        ResolveRound(playerHealth > aiHealthValue);
    }

    private void ResolveRound(bool playerWon)
    {
        if (!roundActive || matchEnded)
            return;

        roundActive = false;
        if (playerWon)
            playerRoundWins++;
        else
            opponentRoundWins++;

        ClearRoundEntities();
        StartCoroutine(ClearPlayerBulletsNextFrame());
        if (playerRoundWins >= RoundsToWin || opponentRoundWins >= RoundsToWin)
        {
            FinishMatch(playerRoundWins >= RoundsToWin);
            return;
        }

        ShowIntermission();
    }

    private void ShowIntermission()
    {
        intermissionActive = true;
        intermissionTitle.text = Lang.GetOr("menu.pvp.intermission", "ROUND COMPLETE");
        intermissionScore.text = Lang.GetOr("menu.pvp.round_score", "YOU {0} - {1} {2}")
            .Replace("{0}", playerRoundWins.ToString())
            .Replace("{1}", opponentRoundWins.ToString())
            .Replace("{2}", opponentName);
        intermissionHint.text = Lang.GetOr("menu.pvp.boost_prompt", "CHOOSE A BOOST FOR THE NEXT ROUND");
        intermissionTimeRemaining = IntermissionDuration;
        if (intermissionTimerLabel != null)
        {
            intermissionTimerLabel.text = Lang.GetOr(
                "menu.pvp.choice_timer", "CHOICE IN {0} SECONDS"
            ).Replace("{0}", Mathf.CeilToInt(intermissionTimeRemaining).ToString());
        }
        if (boostLabels != null)
        {
            boostLabels[0].text = Lang.GetOr("menu.pvp.boost.power", "POWER +25% DAMAGE");
            boostLabels[1].text = Lang.GetOr("menu.pvp.boost.armor", "ARMOR -20% DAMAGE TAKEN");
            boostLabels[2].text = Lang.GetOr("menu.pvp.boost.recovery", "RECOVERY +35% MAX HEALTH");
        }
        intermissionPanel.SetActive(true);
        Time.timeScale = 0f;
    }

    private void ChooseBoost(int choice)
    {
        if (!intermissionActive || matchEnded)
            return;

        switch ((BoostType)choice)
        {
            case BoostType.Power:
                roundPlayerDamageMultiplier *= 1.25f;
                break;
            case BoostType.Armor:
                roundDamageTakenMultiplier *= 0.8f;
                break;
            case BoostType.Recovery:
                nextRoundHealthMultiplier = 1.35f;
                break;
        }

        intermissionActive = false;
        Time.timeScale = 0f;
        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            Vector3 playerSpawn = FindClearSpawn(new Vector3(0f, 0.5f, -16f));
            Vector3 aiSpawn = FindClearSpawn(new Vector3(0f, 0.5f, 16f));
            matchSetupRoutine = StartCoroutine(StartNextRound(playerObject.transform,
                playerSpawn, aiSpawn));
        }
    }

    private IEnumerator StartNextRound(Transform player, Vector3 playerSpawn, Vector3 aiSpawn)
    {
        yield return null;
        ClearPlayerBullets();
        float countdown = 0f;
        while (countdown < 0.45f)
        {
            countdown += Time.unscaledDeltaTime;
            yield return null;
        }
        if (player != null && !matchEnded)
        {
            Time.timeScale = 1f;
            StartRound(player, playerSpawn, aiSpawn);
        }
        matchSetupRoutine = null;
    }

    private IEnumerator ClearPlayerBulletsNextFrame()
    {
        yield return null;
        ClearPlayerBullets();
    }

    private void RefreshMatchStatus()
    {
        if (matchStatusLabel != null && matchStarted && !matchEnded)
        {
            matchStatusLabel.text = Lang.GetOr("menu.pvp.round", "ROUND {0} / 3")
                .Replace("{0}", Mathf.Max(roundNumber, 1).ToString()) +
                "    " + playerRoundWins + " - " + opponentRoundWins +
                "    " + Mathf.CeilToInt(roundTimeRemaining) + "s";
        }
    }

    private string GetOpponentStyleName()
    {
        string key = opponentStyle == 0
            ? "menu.pvp.style.aggressive"
            : opponentStyle == 1
                ? "menu.pvp.style.sniper"
                : "menu.pvp.style.cover";
        string fallback = opponentStyle == 0
            ? "AGGRESSIVE"
            : opponentStyle == 1 ? "SNIPER" : "COVER FIGHTER";
        return Lang.GetOr(key, fallback);
    }

    private void FinishMatch(bool playerWon)
    {
        matchEnded = true;
        roundActive = false;
        intermissionActive = false;
        lastRatingDelta = progression != null
            ? progression.ApplyMatchResult(playerWon, opponentRating)
            : 0;
        dailyRewardEarned = playerWon && progression != null &&
            progression.TryClaimDailyWinReward();

        resultTitle.text = playerWon
            ? Lang.GetOr("menu.pvp.victory", "ПОБЕДА")
            : Lang.GetOr("menu.pvp.defeat", "ПОРАЖЕНИЕ");
        string ratingLine = Lang.GetOr("menu.pvp.rating_result", "RATING {0} ({1}{2})")
            .Replace("{0}", progression != null ? progression.Rating.ToString() : "1000")
            .Replace("{1}", lastRatingDelta >= 0 ? "+" : string.Empty)
            .Replace("{2}", lastRatingDelta.ToString());
        string dailyLine = dailyRewardEarned
            ? "\n" + Lang.GetOr("menu.pvp.daily_reward", "DAILY WIN BONUS +100 COINS")
            : string.Empty;
        resultDetails.text = Lang.GetOr("menu.pvp.final_score", "{0}  ·  {1} - {2}")
            .Replace("{0}", opponentName)
            .Replace("{1}", playerRoundWins.ToString())
            .Replace("{2}", opponentRoundWins.ToString()) +
            "\n" + ratingLine + dailyLine;
        if (resultPanel != null)
            resultPanel.SetActive(true);

        if (GameStateManager.Instance != null)
            GameStateManager.Instance.SetState(GameState.GameOver);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.StopMusic();
            if (AudioManager.Instance.SFXLibrary != null)
            {
                AudioManager.Instance.PlaySFX(
                    playerWon
                        ? AudioManager.Instance.SFXLibrary.WaveComplete
                        : AudioManager.Instance.SFXLibrary.GameOver,
                    priority: SfxPriority.Critical
                );
            }
        }

        Time.timeScale = 0f;
    }

    private void ReturnToMenu()
    {
        Time.timeScale = 1f;
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.ResetForNewRun();

        GameModeManager.Select(GameMode.Pve);
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.BackToMenuWithoutFinalizing();
        else if (GameStateManager.Instance != null)
            GameStateManager.Instance.ReturnToMenu();

        StartCoroutine(CleanupAfterMenuReturnStarts());
    }

    private IEnumerator CleanupAfterMenuReturnStarts()
    {
        yield return null;
        CancelAndReturnToPve();
    }
}
