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
    private const int TotalSteps = 7;

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
    private Enemy tutorialEnemy;
    private Enemy rangedTutorialEnemy;
    private Vector3 dodgeStartPosition;
    private readonly List<EnemyProjectile> observedProjectiles = new List<EnemyProjectile>();

    /// <summary>True — обучение прямо сейчас идёт.</summary>
    public bool IsRunning { get; private set; }

    private Transform player;
    private bool subscribedState;
    private bool skipWired;

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
        if (dimBackground != null)
            dimBackground.SetActive(true);

        if (image != null)
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
        Time.timeScale <= 0f ||
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

            if (moving)
            {
                activeMoveDuration += Time.unscaledDeltaTime;
                if (activeMoveDuration >= minimumInputHoldDuration)
                    yield break;
            }
            else
            {
                activeMoveDuration = 0f;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForAim()
    {
        float distance = 0f;
        float elapsed = 0f;
        float activeAimDuration = 0f;

        while ((distance < aimDistanceRequired ||
                activeAimDuration < minimumInputHoldDuration) &&
               elapsed < combatStepTimeout)
        {
            float delta = !IsGamePaused && Mouse.current != null
                ? Mouse.current.delta.ReadValue().magnitude
                : 0f;

            if (delta > 0.01f)
            {
                distance += delta;
                activeAimDuration += Time.unscaledDeltaTime;
            }
            else
            {
                activeAimDuration = 0f;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForAttack()
    {
        yield return null;
        float elapsed = 0f;
        while (elapsed < combatStepTimeout)
        {
            if (!IsGamePaused && Mouse.current != null &&
                Mouse.current.leftButton.isPressed)
                yield break;
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private IEnumerator WaitForAttackRelease()
    {
        float elapsed = 0f;
        while (elapsed < 2f &&
               (Mouse.current == null || Mouse.current.leftButton.isPressed))
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
        while (true)
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
        if (!upgradeUI.IsShowing)
        {
            UnsubscribeUpgrade();
            yield break;
        }

        float elapsed = 0f;
        while (!upgradeChosen && elapsed < 60f)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
        UnsubscribeUpgrade();
        if (upgradeUI.IsShowing)
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
        if (upgradeUI == null || !upgradeUI.IsShowing) return;
        upgradeUI.Hide();
        Time.timeScale = 1f;
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
        if (objectiveText == null)
            return;

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