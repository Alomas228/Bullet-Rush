using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Обучение нового игрока. Запускается автоматически при нажатии «Играть»,
/// если игрок ещё не проходил его (флаг хранится в PlayerPrefs).
///
/// Информативное обучение первого запуска:
/// движение и прицеливание → стрельба → обычный враг → быстрый враг
/// → дальний враг и уклонение → выбор улучшения. Каждый этап показывает
/// короткое объяснение и практическое действие, после чего стартует первая волна.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private const int TotalSteps = 8;
    private const string DashHint = "{tut.dash}";

    public const string CompletedPrefsKey =
        "ArcadeSurvivor.TutorialDone";

    public static TutorialManager Instance { get; private set; }

    /// <summary>True — игрок уже проходил обучение.</summary>
    public static bool IsCompleted
    {
        get => PlayerPrefs.GetInt(CompletedPrefsKey, 0) != 0;
        set
        {
            PlayerPrefs.SetInt(CompletedPrefsKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    [Header("References")]
    [SerializeField] private WaveManager waveManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private UpgradeUI upgradeUI;

    [Header("UI")]
    [Tooltip("Корень UI обучения. У всех его детей Raycast должен быть выключен, иначе они перехватят мышь.")]
    [SerializeField] private GameObject tutorialPanel;
    [Tooltip("Затемнение фона под картинками (показывается только на шагах с инструкциями).")]
    [SerializeField] private GameObject dimBackground;
    [SerializeField] private GameObject wasdImage;
    [SerializeField] private GameObject aimImage;
    [SerializeField] private GameObject shootImage;
    [SerializeField] private GameObject dashImage;
    [Tooltip("Краткая инструкция для текущего шага.")]
    [SerializeField] private TMP_Text objectiveText;
    [Tooltip("Счётчик шагов короткого обучения.")]
    [SerializeField] private TMP_Text progressText;
    [Tooltip("Кнопка «Пропустить обучение» (опционально).")]
    [SerializeField] private Button skipButton;

    [Header("Input Steps")]
    [Tooltip("Суммарное движение мыши, необходимое для завершения шага прицеливания.")]
    [SerializeField] private float aimDistanceRequired = 400f;
    [SerializeField] private float combatStepTimeout = 25f;
    [SerializeField] private float enemySpawnRadius = 8f;
    [SerializeField] private float dodgeMoveDistance = 1.5f;
    [SerializeField] private float minimumInputHoldDuration = 0.5f;
    [SerializeField] private Sprite markerSprite;

    [Header("Tutorial Timing")]
    [Tooltip("Короткая пауза между этапами обучения.")]
    [SerializeField] private float pauseBetweenSteps = 0.6f;

    [Header("Feedback")]
    [Tooltip("Проигрывать короткий звук при успешном прохождении шага.")]
    [SerializeField] private bool stepCompleteSound = true;

    [Header("Texts")]
    [Tooltip("Ключи локализации для этапов обучения.")]
    [SerializeField] private string moveHintText = "{tut.move}";
    [SerializeField] private string aimHintText = "{tut.aim}";
    [SerializeField] private string shootHintText = "{tut.shoot}";
    [SerializeField] private string normalHintText = "{tut.normal}";
    [SerializeField] private string fastHintText = "{tut.fast}";
    [SerializeField] private string rangedHintText = "{tut.ranged}";
    [SerializeField] private string upgradeHintText = "{tut.upgrade}";
    [SerializeField] private string completeHintText = "{tut.complete}";

    private bool tutorialFinished;
    private bool upgradeChosen;
    private bool upgradeSubscribed;
    private bool upgradePauseOwned;
    private Enemy tutorialEnemy;
    private Enemy rangedTutorialEnemy;
    private Vector3 dodgeStartPosition;
    private readonly List<EnemyProjectile> observedProjectiles = new List<EnemyProjectile>();

    /// <summary>True — обучение прямо сейчас идёт.</summary>
    public bool IsRunning { get; private set; }

    private Transform player;
    private bool subscribedState;
    private bool skipWired;
    private string currentObjective;
    private bool lastMobileMode;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        WireSkipButton();
    }

    private void OnEnable()
    {
        if (subscribedState ||
            GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged +=
            HandleStateChanged;

        subscribedState = true;
    }

    private void OnDisable()
    {
        if (!subscribedState ||
            GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged -=
            HandleStateChanged;

        subscribedState = false;
    }

    private void OnDestroy()
    {
        UnwireSkipButton();
        UnsubscribeUpgrade();
        MobileControls.SetTutorialHint(MobileControls.TutorialHint.None);

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (GameStateManager.Instance == null)
            return;

        GameState state =
            GameStateManager.Instance.CurrentState;

        // Резервный контроль: не полагаемся только на событие.
        // Если обучение идёт, а игры уже нет — сворачиваем его.
        if (IsRunning && state != GameState.Playing)
        {
            AbortTutorial();
            return;
        }

        // Защита от «застрявшей» панели: если она осталась активной
        // вне игры (например, при возврате в меню), прячем принудительно.
        if (!IsRunning &&
            state != GameState.Playing &&
            tutorialPanel != null &&
            tutorialPanel.activeSelf)
        {
            HideAllSteps();
            tutorialPanel.SetActive(false);
        }

        if (IsRunning && lastMobileMode != MobileControls.IsTouchDevice)
        {
            if (MobileControls.IsTouchDevice)
            {
                if (dimBackground != null) dimBackground.SetActive(false);
                if (wasdImage != null) wasdImage.SetActive(false);
                if (aimImage != null) aimImage.SetActive(false);
                if (shootImage != null) shootImage.SetActive(false);
                if (dashImage != null) dashImage.SetActive(false);
            }
            SetObjective(currentObjective);
        }
    }

    private void WireSkipButton()
    {
        if (skipWired)
            return;

        if (skipButton != null)
        {
            skipButton.onClick.AddListener(SkipTutorial);
            skipWired = true;
        }
    }

    private void UnwireSkipButton()
    {
        if (!skipWired)
            return;

        if (skipButton != null)
            skipButton.onClick.RemoveListener(SkipTutorial);

        skipWired = false;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Playing && IsRunning)
            AbortTutorial();
    }

    /// <summary>
    /// Вызывается WaveManager при старте забега.
    /// true — обучение перехватывает запуск: волна стартует сама
    /// в конце обучения через StartFirstWave().
    /// </summary>
    public bool BeginTutorial()
    {
        if (IsCompleted)
            return false;

        if (IsRunning)
            return true;

        if (waveManager == null)
            waveManager = FindAnyObjectByType<WaveManager>();
        if (enemySpawner == null)
            enemySpawner = FindAnyObjectByType<EnemySpawner>();
        if (upgradeUI == null)
            upgradeUI = FindAnyObjectByType<UpgradeUI>();

        GameObject playerObject =
            GameObject.FindGameObjectWithTag("Player");

        player =
            playerObject != null
                ? playerObject.transform
                : null;

        if (player == null)
        {
            Debug.LogWarning(
                "TutorialManager: Игрок не найден — обучение пропущено.",
                this
            );

            return false;
        }

        WireSkipButton();

        IsRunning = true;
        tutorialFinished = false;
        Time.timeScale = 1f;

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        HideAllSteps();

        StartCoroutine(TutorialRoutine());
        return true;
    }

    /// <summary>
    /// Пропустить обучение и сразу начать первую волну
    /// (кнопка «Пропустить»).
    /// </summary>
    public void SkipTutorial()
    {
        if (!IsRunning)
            return;

        UnwireSkipButton();

        StopAllCoroutines();
        CloseUpgradeIfShowing();
        ClearTutorialEnemy();

        IsRunning = false;
        tutorialFinished = false;

        IsCompleted = true;
        HideAllSteps();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        WireSkipButton();

        if (waveManager != null)
            waveManager.StartFirstWave();
    }

    [ContextMenu("Reset Tutorial Flag")]
    private void ResetTutorialFlag()
    {
        IsCompleted = false;
    }

    private void AbortTutorial()
    {
        UnwireSkipButton();

        StopAllCoroutines();
        CloseUpgradeIfShowing();
        ClearTutorialEnemy();

        IsRunning = false;
        tutorialFinished = false;

        HideAllSteps();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        WireSkipButton();
    }

    // =========================================================
    // FLOW
    // =========================================================

    private IEnumerator TutorialRoutine()
    {
        // Арена должна существовать до первого боя.
        if (waveManager != null)
            waveManager.PrepareTutorialWorld();

        // Даём камере начать полёт из меню.
        yield return null;

        int step = 1;

        SetStepProgress(step);
        SetObjective(moveHintText);
        yield return ImageStep(wasdImage, WaitForMove());
        PlayStepComplete();
        yield return new WaitForSeconds(pauseBetweenSteps);

        step++;
        SetStepProgress(step);
        SetObjective(aimHintText);
        yield return ImageStep(aimImage, WaitForAim());
        PlayStepComplete();
        yield return new WaitForSeconds(pauseBetweenSteps);

        step++;
        SetStepProgress(step);
        SetObjective(shootHintText);
        yield return ImageStep(shootImage, WaitForAttack());
        PlayStepComplete();
        yield return WaitForAttackRelease();
        yield return new WaitForSeconds(pauseBetweenSteps);

        step++;
        SetStepProgress(step);
        SetObjective(DashHint);
        yield return ImageStep(dashImage, WaitForDash());
        PlayStepComplete();
        yield return new WaitForSeconds(pauseBetweenSteps);

        step++;
        SetStepProgress(step);
        SetObjective(normalHintText);
        yield return WaitForTutorialEnemy(EnemyType.Normal, true, true);

        step++;
        SetStepProgress(step);
        SetObjective(fastHintText);
        yield return WaitForTutorialEnemy(EnemyType.Fast, false, true);

        step++;
        SetStepProgress(step);
        SetObjective(rangedHintText);
        yield return WaitForRangedDodge();
        if (rangedTutorialEnemy != null && !rangedTutorialEnemy.IsDead)
        {
            SetObjective(rangedHintText);
            yield return WaitForEnemyKilledByPlayer(rangedTutorialEnemy);
        }
        ClearRangedTutorialEnemy();
        yield return new WaitForSeconds(pauseBetweenSteps);

        step++;
        SetStepProgress(step);
        SetObjective(upgradeHintText);
        yield return ShowUpgradeStep();
        SetObjective(completeHintText);
        yield return new WaitForSeconds(2f);

        FinishTutorial();
    }

    private IEnumerator ImageStep(
        GameObject image,
        IEnumerator waitForInput)
    {
        if (dimBackground != null && !MobileControls.IsTouchDevice)
            dimBackground.SetActive(true);

        if (image != null && !MobileControls.IsTouchDevice)
            image.SetActive(true);

        yield return waitForInput;

        if (image != null)
            image.SetActive(false);

        if (dimBackground != null)
            dimBackground.SetActive(false);

        yield return new WaitForSeconds(
            pauseBetweenSteps * 0.5f
        );
    }

    // =========================================================
    // INPUT WAITS
    // =========================================================

    private static bool IsGamePaused =>
        Time.timeScale <= 0f || YG.YG2.isPauseGame ||
        (PauseManager.Instance != null &&
         PauseManager.Instance.IsPaused);

    private IEnumerator WaitForMove()
    {
        float elapsed = 0f;
        float activeMoveDuration = 0f;
        while (elapsed < combatStepTimeout &&
               activeMoveDuration < minimumInputHoldDuration)
        {
            bool moving = !IsGamePaused && Keyboard.current != null &&
                (Keyboard.current.wKey.isPressed ||
                 Keyboard.current.aKey.isPressed ||
                 Keyboard.current.sKey.isPressed ||
                 Keyboard.current.dKey.isPressed ||
                 Keyboard.current.upArrowKey.isPressed ||
                 Keyboard.current.leftArrowKey.isPressed ||
                 Keyboard.current.downArrowKey.isPressed ||
                 Keyboard.current.rightArrowKey.isPressed);

            if (MobileControls.IsTouchDevice) moving = !IsGamePaused && MobileControls.MoveInput.sqrMagnitude>.04f;
            if (moving)
            {
                activeMoveDuration += Time.unscaledDeltaTime;
                if (activeMoveDuration >= minimumInputHoldDuration)
                    yield break;
            }
            if (!IsGamePaused) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForAim()
    {
        float distance = 0f, elapsed = 0f, activeAimDuration = 0f, rotation = 0f;
        Vector3 lastForward = player != null ? player.forward : Vector3.forward;
        Vector2 lastMouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        float requiredPixels = Mathf.Clamp(aimDistanceRequired, 80f, Mathf.Max(80f, Screen.width * .16f));
        while (elapsed < combatStepTimeout)
        {
            if (!IsGamePaused)
            {
                if (MobileControls.IsTouchDevice)
                {
                    if (MobileControls.AimInput.sqrMagnitude > .035f)
                        activeAimDuration += Time.unscaledDeltaTime;
                    if (activeAimDuration >= Mathf.Clamp(minimumInputHoldDuration, .15f, .35f)) yield break;
                }
                else if (Mouse.current != null)
                {
                    // Mouse deltas are often zero on individual render frames;
                    // count total movement instead of requiring a continuous delta.
                    Vector2 mouse = Mouse.current.position.ReadValue();
                    distance += Mathf.Max(Vector2.Distance(mouse, lastMouse), Mouse.current.delta.ReadValue().magnitude);
                    if (player != null) rotation += Vector3.Angle(lastForward, player.forward);
                    if (distance >= requiredPixels || rotation >= 45f) yield break;
                    lastMouse = mouse;
                    if (player != null) lastForward = player.forward;
                }
                elapsed += Time.unscaledDeltaTime;
            }
            else
            {
                if (Mouse.current != null) lastMouse = Mouse.current.position.ReadValue();
                if (player != null) lastForward = player.forward;
            }
            yield return null;
        }
    }

    private IEnumerator WaitForAttack()
    {
        yield return null;
        float elapsed = 0f;
        while (elapsed < combatStepTimeout)
        {
            if (!IsGamePaused && (MobileControls.IsTouchDevice ? MobileControls.IsFiring :
                Mouse.current != null && Mouse.current.leftButton.isPressed))
                yield break;
            if (!IsGamePaused) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForDash()
    {
        PlayerController controller = player != null ? player.GetComponent<PlayerController>() : null;
        if (controller == null) yield break;
        float elapsed = 0f;
        float previousCooldown = controller.DashCooldownRemaining;
        while (elapsed < combatStepTimeout)
        {
            if (!IsGamePaused)
            {
                if (controller.IsDashing || controller.DashCooldownRemaining > previousCooldown + .01f) yield break;
                elapsed += Time.unscaledDeltaTime;
            }
            previousCooldown = controller.DashCooldownRemaining;
            yield return null;
        }
    }

    private IEnumerator WaitForAttackRelease()
    {
        float elapsed = 0f;
        while (elapsed < 2f &&
               (MobileControls.IsTouchDevice ? MobileControls.IsFiring : Mouse.current == null || Mouse.current.leftButton.isPressed))
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForTutorialEnemy(
        EnemyType type, bool marked, bool waitForDeath)
    {
        ClearTutorialEnemy();
        tutorialEnemy = SpawnTutorialEnemy(type, marked);
        if (tutorialEnemy == null)
        {
            Debug.LogWarning(
                $"TutorialManager: Could not spawn {type}; skipping tutorial step.",
                this
            );
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < combatStepTimeout)
        {
            if (IsGamePaused)
            {
                yield return null;
                continue;
            }

            if (tutorialEnemy == null || tutorialEnemy.IsDead)
                yield break;
            if (!waitForDeath && tutorialEnemy.HasTakenDamage)
                yield break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ClearTutorialEnemy()
    {
        if (tutorialEnemy != null && !tutorialEnemy.IsDead)
            tutorialEnemy.Kill();

        tutorialEnemy = null;
        ClearRangedTutorialEnemy();
    }

    private void ClearRangedTutorialEnemy()
    {
        if (rangedTutorialEnemy != null && !rangedTutorialEnemy.IsDead)
            rangedTutorialEnemy.Kill();

        rangedTutorialEnemy = null;
    }

    private IEnumerator WaitForEnemyKilledByPlayer(Enemy enemy)
    {
        float elapsed = 0f;
        while (elapsed < combatStepTimeout)
        {
            if (enemy == null)
                yield break;

            if (enemy.IsDead)
            {
                if (enemy.LastDamageSource == DamageSource.Weapon)
                    yield break;

                // If environmental damage or another effect kills the demo enemy,
                // replace it instead of advancing to the upgrade step.
                ClearRangedTutorialEnemy();
                enemy = SpawnTutorialEnemy(EnemyType.Ranged, true);
                rangedTutorialEnemy = enemy;
                if (enemy == null)
                    yield break;
            }

            if (!IsGamePaused) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private Enemy SpawnTutorialEnemy(EnemyType type, bool marked)
    {
        if (enemySpawner == null || player == null)
            return null;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 position = player.position +
            new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * enemySpawnRadius;
        Enemy enemy = enemySpawner.SpawnEnemyAtPosition(
            type,
            position,
            spawnDuration: 0f
        );
        if (enemy != null && marked)
            TutorialEnemyMarker.Attach(
                enemy,
                markerSprite,
                new Vector3(0f, 2.4f, 0f),
                3.5f,
                0.3f
            );
        return enemy;
    }

    private IEnumerator WaitForRangedDodge()
    {
        observedProjectiles.Clear();
        ClearTutorialEnemy();
        ClearRangedTutorialEnemy();
        rangedTutorialEnemy = SpawnTutorialEnemy(EnemyType.Ranged, true);
        if (rangedTutorialEnemy == null)
        {
            Debug.LogWarning("TutorialManager: Ranged enemy unavailable; skipping dodge demonstration.", this);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < combatStepTimeout)
        {
            if (rangedTutorialEnemy == null || rangedTutorialEnemy.IsDead)
                yield break;

            if (IsGamePaused)
            {
                yield return null;
                continue;
            }

            if (player != null)
            {
                EnemyProjectile[] projectiles =
                    FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None);
                bool foundNewProjectile = false;

                for (int i = 0; i < projectiles.Length; i++)
                {
                    EnemyProjectile projectile = projectiles[i];
                    if (projectile == null || observedProjectiles.Contains(projectile))
                        continue;

                    observedProjectiles.Add(projectile);
                    foundNewProjectile = true;
                }

                if (foundNewProjectile)
                {
                    dodgeStartPosition = player.position;
                    break;
                }
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (elapsed >= combatStepTimeout ||
            rangedTutorialEnemy == null ||
            rangedTutorialEnemy.IsDead)
            yield break;

        float moved = 0f;
        while (elapsed < combatStepTimeout && moved < dodgeMoveDistance)
        {
            if (IsGamePaused)
            {
                yield return null;
                continue;
            }

            if (player != null)
            {
                moved += Vector3.Distance(player.position, dodgeStartPosition);
                dodgeStartPosition = player.position;
            }

            for (int i = observedProjectiles.Count - 1; i >= 0; i--)
            {
                if (observedProjectiles[i] == null ||
                    !observedProjectiles[i].gameObject.activeInHierarchy)
                    observedProjectiles.RemoveAt(i);
            }

            if (moved >= dodgeMoveDistance)
                yield break;

            if (observedProjectiles.Count == 0 ||
                rangedTutorialEnemy == null ||
                rangedTutorialEnemy.IsDead)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator ShowUpgradeStep()
    {
        if (upgradeUI == null || UpgradeManager.Instance == null)
        {
            Debug.LogWarning(
                "TutorialManager: Upgrade UI is unavailable; skipping tutorial step.",
                this
            );
            yield break;
        }

        upgradeChosen = false;
        upgradeUI.OnUpgradeChosen += HandleUpgradeChosen;
        upgradeSubscribed = true;
        upgradeUI.Show();
        upgradePauseOwned = upgradeUI.IsShowing;
        if (!upgradeUI.IsShowing)
        {
            UnsubscribeUpgrade();
            yield break;
        }

        float elapsed = 0f;
        while (!upgradeChosen && elapsed < 60f)
        {
            if (!YG.YG2.isPauseGame) elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        CloseUpgradeIfShowing();
    }

    private void HandleUpgradeChosen(int index) => upgradeChosen = true;

    private void UnsubscribeUpgrade()
    {
        if (!upgradeSubscribed) return;
        if (upgradeUI != null) upgradeUI.OnUpgradeChosen -= HandleUpgradeChosen;
        upgradeSubscribed = false;
    }

    private void CloseUpgradeIfShowing()
    {
        UnsubscribeUpgrade();
        if (upgradeUI != null && upgradeUI.IsShowing) upgradeUI.Hide();
        // ChooseUpgrade hides the panel before raising its event. The tutorial
        // still owns that pause and must release it even if the panel is hidden.
        if (upgradePauseOwned && GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing &&
            !(PauseManager.Instance != null && PauseManager.Instance.IsPaused) &&
            !JourneyUI.IsBlocking && !CampaignCompletionUI.IsOpen)
            Time.timeScale = 1f;
        upgradePauseOwned = false;
    }

    // =========================================================
    // UI
    // =========================================================

    private void FinishTutorial()
    {
        if (tutorialFinished)
            return;

        ClearTutorialEnemy();
        CloseUpgradeIfShowing();
        tutorialFinished = true;
        IsCompleted = true;
        IsRunning = false;

        HideAllSteps();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        if (waveManager != null)
            waveManager.StartFirstWave();
    }

    private void SetObjective(string text)
    {
        currentObjective = text;
        lastMobileMode = MobileControls.IsTouchDevice;
        MobileControls.SetTutorialHint(text == moveHintText ? MobileControls.TutorialHint.Move :
            text == aimHintText || text == shootHintText ? MobileControls.TutorialHint.Aim :
            text == rangedHintText || text == DashHint ? MobileControls.TutorialHint.Dash : MobileControls.TutorialHint.None);
        if (objectiveText == null)
            return;

        if (MobileControls.IsTouchDevice)
        {
            if (text==moveHintText) text=GameplayJourney.T("Двигайся левым джойстиком", "Move with the left stick", "Sol cubukla hareket et");
            else if (text==aimHintText) text=GameplayJourney.T("Целься правым джойстиком", "Aim with the right stick", "Sag cubukla nisan al");
            else if (text==shootHintText) text=GameplayJourney.T("Удерживай правый джойстик для стрельбы", "Hold the right stick to fire", "Ates icin sag cubugu tut");
            else if (text==rangedHintText) text=GameplayJourney.T("Уклоняйся кнопкой рывка и победи стрелка", "Use dash to dodge and defeat the shooter", "Atakla kacin ve nisanciyi yen");
        }

        if (text == DashHint)
            text = MobileControls.IsTouchDevice
                ? GameplayJourney.T("Нажми кнопку рывка рядом с правым джойстиком", "Tap the dash button next to the right stick", "Sag cubugun yanindaki atilma tusuna bas")
                : GameplayJourney.T("Нажми пробел для рывка", "Press Space to dash", "Atilmak icin bosluk tusuna bas");

        objectiveText.text =
            string.IsNullOrEmpty(text) ? string.Empty : LangBinder.ResolveKey(text);

        objectiveText.gameObject.SetActive(!string.IsNullOrEmpty(text));
    }

    private void SetStepProgress(int step)
    {
        if (progressText == null)
            return;

        progressText.text =
            Lang.Get("tut.step", step, TotalSteps);

        if (!progressText.gameObject.activeSelf)
            progressText.gameObject.SetActive(true);
    }

    private void PlayStepComplete()
    {
        if (!stepCompleteSound)
            return;

        AudioManager audio = AudioManager.Instance;

        if (audio == null)
            return;

        SFXLibrary sfx = audio.SFXLibrary;

        if (sfx != null && sfx.UpgradePick != null)
            audio.PlayUI(sfx.UpgradePick);
    }

    private void HideAllSteps()
    {
        currentObjective = null;
        MobileControls.SetTutorialHint(MobileControls.TutorialHint.None);
        if (dimBackground != null)
            dimBackground.SetActive(false);

        if (wasdImage != null)
            wasdImage.SetActive(false);

        if (aimImage != null)
            aimImage.SetActive(false);

        if (shootImage != null)
            shootImage.SetActive(false);

        if (dashImage != null)
            dashImage.SetActive(false);

        if (objectiveText != null)
        {
            objectiveText.text = string.Empty;
            objectiveText.gameObject.SetActive(false);
        }

        if (progressText != null)
        {
            progressText.text = string.Empty;
            progressText.gameObject.SetActive(false);
        }
    }
}
