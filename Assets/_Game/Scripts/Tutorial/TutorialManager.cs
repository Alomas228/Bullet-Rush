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
/// Шаги:
///  1. Картинка WASD — ждём, пока игрок реально нажмёт движение;
///  2. Картинка прицеливания мышью — ждём движения мыши;
///  3. Картинка стрельбы — ждём клика ЛКМ;
///  4. Картинка рывка — ждём нажатия Пробела;
///  5. Один враг с меткой — ждём его смерти;
///  6. Разные враги (быстрый + стрелок) — ждём, пока всех убьют;
///  7. Снаряды — ждём, пока игрок уклонится от залпа;
///  8. Окно улучшений с пояснением редкости — ждём выбора игрока.
/// После этого обучение помечается пройденным и стартует первая волна.
/// </summary>
public class TutorialManager : MonoBehaviour
{
    private const int TotalSteps = 8;

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
    [Tooltip("Текст задачи сверху («УБЕЙ ЕГО!» и т.д.).")]
    [SerializeField] private TMP_Text objectiveText;
    [Tooltip("Счётчик «ШАГ X/8».")]
    [SerializeField] private TMP_Text progressText;
    [Tooltip("Кнопка «Пропустить обучение» (опционально).")]
    [SerializeField] private Button skipButton;

    [Header("Enemy Marker")]
    [Tooltip("Спрайт метки над врагом (стрелка). Если пусто — генерируется простая жёлтая стрелка.")]
    [SerializeField] private Sprite markerSprite;
    [SerializeField] private Vector3 markerOffset =
        new Vector3(0f, 2.4f, 0f);
    [SerializeField] private float markerBobSpeed = 3.5f;
    [SerializeField] private float markerBobHeight = 0.3f;

    [Header("Input Steps")]
    [Tooltip("Суммарное движение мыши, необходимое для завершения шага прицеливания.")]
    [SerializeField] private float aimDistanceRequired = 400f;

    [Header("Combat Steps")]
    [Tooltip("На каком радиусе от игрока появляются обычные враги обучения.")]
    [SerializeField] private float enemySpawnRadius = 8f;
    [Tooltip("Сколько быстрых врагов появляется на шаге «разные враги».")]
    [SerializeField] private int mobsFastCount = 1;
    [Tooltip("Сколько стрелков появляется на шаге «разные враги».")]
    [SerializeField] private int mobsRangedCount = 1;
    [Tooltip("Ставить метку над стрелком на шаге «разные враги».")]
    [SerializeField] private bool markerOnMobsStep = true;
    [Tooltip("Пауза между шагами обучения.")]
    [SerializeField] private float pauseBetweenSteps = 0.6f;

    [Header("Dodge Step")]
    [Tooltip("Радиус появления стрелка на шаге уклонения.")]
    [SerializeField] private float dodgeSpawnRange = 10f;
    [Tooltip("Насколько нужно сдвинуться, пока летит снаряд, чтобы шаг считался пройденным.")]
    [SerializeField] private float dodgeMoveDistance = 1.5f;
    [Tooltip("Переспавнивать стрелка, если он умер, раз в N секунд.")]
    [SerializeField] private float dodgeRespawnInterval = 6f;
    [Tooltip("Ставить метку над стрелком на шаге уклонения.")]
    [SerializeField] private bool markerOnDodgeStep = true;

    [Header("Feedback")]
    [Tooltip("Проигрывать короткий звук при успешном прохождении шага.")]
    [SerializeField] private bool stepCompleteSound = true;

    [Header("Texts")]
    [SerializeField] private string killOneText = "УБЕЙ ЕГО!";
    [SerializeField] private string mobsStepText =
        "БЫСТРЫЕ НАБЕГАЮТ, СТРЕЛКИ БЬЮТ ИЗДАЛЕКА.\n" +
        "НЕ СТОЙ НА МЕСТЕ!";
    [SerializeField] private string dodgeText =
        "УКЛОНЯЙСЯ ОТ СНАРЯДОВ!";
    [SerializeField] private string upgradeHintText =
        "ТЕПЕРЬ ВЫБЕРИ ОДНО УЛУЧШЕНИЕ — ОНО УСИЛИТ ТЕБЯ В ЭТОМ ЗАБЕГЕ.\n" +
        "ЦВЕТ НАЗВАНИЯ = РЕДКОСТЬ:\n" +
        "СЕРЫЙ — ОБЫЧНОЕ · ЗЕЛЁНЫЙ — НЕОБЫЧНОЕ · СИНИЙ — РЕДКОЕ\n" +
        "ФИОЛЕТОВЫЙ — ЭПИЧЕСКОЕ · ОРАНЖЕВЫЙ — ЛЕГЕНДАРНОЕ";

    /// <summary>True — обучение прямо сейчас идёт.</summary>
    public bool IsRunning { get; private set; }

    private Transform player;
    private Enemy dodgeEnemy;
    private bool upgradeChosen;
    private bool upgradeSubscribed;
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
        upgradeChosen = false;
        dodgeEnemy = null;

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

        UnsubscribeUpgrade();

        if (upgradeUI != null && upgradeUI.IsShowing)
            upgradeUI.Hide();

        Time.timeScale = 1f;

        IsRunning = false;
        upgradeChosen = false;
        dodgeEnemy = null;

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

        UnsubscribeUpgrade();

        // Паузу держит только окно улучшений — его и размораживаем.
        bool upgradeWasShowing =
            upgradeUI != null && upgradeUI.IsShowing;

        if (upgradeUI != null && upgradeUI.IsShowing)
            upgradeUI.Hide();

        IsRunning = false;
        upgradeChosen = false;
        dodgeEnemy = null;

        HideAllSteps();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        if (upgradeWasShowing)
            Time.timeScale = 1f;

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

        // 1. Движение
        int step = 1;
        SetStepProgress(step);
        yield return ImageStep(wasdImage, WaitForMove());
        PlayStepComplete();
        step++;

        // 2. Прицеливание
        SetStepProgress(step);
        yield return ImageStep(aimImage, WaitForAim());
        PlayStepComplete();
        step++;

        // 3. Стрельба
        SetStepProgress(step);
        yield return ImageStep(shootImage, WaitForShoot());
        PlayStepComplete();
        step++;

        // 4. Рывок
        SetStepProgress(step);
        yield return ImageStep(dashImage, WaitForDash());
        PlayStepComplete();
        step++;

        yield return new WaitForSeconds(pauseBetweenSteps);

        // 5. Один враг
        SetStepProgress(step);
        SetObjective(killOneText);

        yield return WaitAllDead(SpawnEnemies(1, true));

        PlayStepComplete();
        step++;

        yield return new WaitForSeconds(pauseBetweenSteps);

        // 6. Разные враги
        SetStepProgress(step);
        SetObjective(mobsStepText);

        yield return WaitAllDead(SpawnMobsStep());

        PlayStepComplete();
        step++;

        yield return new WaitForSeconds(pauseBetweenSteps);

        // 7. Уклонение от снарядов
        SetStepProgress(step);
        SetObjective(dodgeText);

        yield return WaitForDodge();

        PlayStepComplete();
        step++;

        yield return new WaitForSeconds(pauseBetweenSteps);

        // 8. Окно улучшений
        SetStepProgress(step);
        SetObjective(upgradeHintText);

        if (upgradeUI != null)
        {
            upgradeChosen = false;

            upgradeUI.OnUpgradeChosen += HandleUpgradeChosen;
            upgradeSubscribed = true;

            upgradeUI.Show();

            while (!upgradeChosen)
                yield return null;

            UnsubscribeUpgrade();

            upgradeUI.Hide();
            Time.timeScale = 1f;
        }

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
        PauseManager.Instance != null &&
        PauseManager.Instance.IsPaused;

    private IEnumerator WaitForMove()
    {
        while (true)
        {
            if (!IsGamePaused &&
                Keyboard.current != null &&
                (Keyboard.current.wKey.isPressed ||
                 Keyboard.current.aKey.isPressed ||
                 Keyboard.current.sKey.isPressed ||
                 Keyboard.current.dKey.isPressed))
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator WaitForAim()
    {
        float distance = 0f;

        while (distance < aimDistanceRequired)
        {
            if (!IsGamePaused && Mouse.current != null)
            {
                distance +=
                    Mouse.current.delta.ReadValue().magnitude;
            }

            yield return null;
        }
    }

    private IEnumerator WaitForShoot()
    {
        // Пропускаем кадр, чтобы клик с прошлого шага не засчитался.
        yield return null;

        while (true)
        {
            if (!IsGamePaused &&
                Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                yield break;
            }

            yield return null;
        }
    }

    private IEnumerator WaitForDash()
    {
        // Пропускаем кадр, чтобы Пробел с прошлого шага не засчитался.
        yield return null;

        while (true)
        {
            if (!IsGamePaused &&
                Keyboard.current != null &&
                Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                yield break;
            }

            yield return null;
        }
    }

    // =========================================================
    // COMBAT STEPS
    // =========================================================

    private List<Enemy> SpawnEnemies(
        int count,
        bool withMarker)
    {
        List<Enemy> result = new List<Enemy>();

        for (int i = 0; i < count; i++)
        {
            Enemy enemy = SpawnEnemy(
                EnemyType.Normal,
                withMarker
            );

            if (enemy != null)
                result.Add(enemy);
        }

        return result;
    }

    private List<Enemy> SpawnMobsStep()
    {
        List<Enemy> result = new List<Enemy>();

        // Стрелок — главная опасность, его помечаем меткой.
        for (int i = 0; i < mobsFastCount; i++)
        {
            Enemy enemy = SpawnEnemy(EnemyType.Fast, false);

            if (enemy != null)
                result.Add(enemy);
        }

        for (int i = 0; i < mobsRangedCount; i++)
        {
            Enemy enemy = SpawnEnemy(
                EnemyType.Ranged,
                markerOnMobsStep
            );

            if (enemy != null)
                result.Add(enemy);
        }

        return result;
    }

    private Enemy SpawnEnemy(
        EnemyType enemyType,
        bool withMarker)
    {
        if (enemySpawner == null || player == null)
        {
            Debug.LogWarning(
                "TutorialManager: Нет EnemySpawner или игрока — " +
                "враги обучения не заспавнены.",
                this
            );

            return null;
        }

        float angle = Random.Range(0f, 360f);

        Vector3 position =
            player.position +
            new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) *
                enemySpawnRadius,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) *
                enemySpawnRadius
            );

        position.y = player.position.y;

        Enemy enemy =
            enemySpawner.SpawnEnemyAtPosition(
                enemyType,
                position
            );

        if (enemy == null || !withMarker)
            return enemy;

        TutorialEnemyMarker.Attach(
            enemy,
            markerSprite,
            markerOffset,
            markerBobSpeed,
            markerBobHeight
        );

        return enemy;
    }

    private void SpawnDodgeEnemy()
    {
        if (enemySpawner == null || player == null)
            return;

        float angle = Random.Range(0f, 360f);

        Vector3 position =
            player.position +
            new Vector3(
                Mathf.Cos(angle * Mathf.Deg2Rad) *
                dodgeSpawnRange,
                0f,
                Mathf.Sin(angle * Mathf.Deg2Rad) *
                dodgeSpawnRange
            );

        position.y = player.position.y;

        dodgeEnemy =
            enemySpawner.SpawnEnemyAtPosition(
                EnemyType.Ranged,
                position
            );

        if (dodgeEnemy != null && markerOnDodgeStep)
        {
            TutorialEnemyMarker.Attach(
                dodgeEnemy,
                markerSprite,
                markerOffset,
                markerBobSpeed,
                markerBobHeight
            );
        }
    }

    /// <summary>
    /// Ждём, пока игрок реально уклонится: двигаелся (или делал рывок)
    /// в те моменты, когда в сцене есть летящий снаряд.
    /// Если стрелок умер и не стреляет — спавним нового.
    /// </summary>
    private IEnumerator WaitForDodge()
    {
        if (player == null)
            yield break;

        SpawnDodgeEnemy();

        float movedWhileProjectile = 0f;
        Vector3 prevPosition = player.position;
        float respawnTimer = dodgeRespawnInterval;

        while (true)
        {
            if (IsGamePaused)
            {
                yield return null;
                continue;
            }

            Vector3 current =
                player.position;

            Vector3 delta =
                current - prevPosition;

            prevPosition = current;

            if (AnyEnemyProjectileActive())
            {
                movedWhileProjectile +=
                    delta.magnitude;

                if (movedWhileProjectile >= dodgeMoveDistance)
                    yield break;
            }
            else
            {
                movedWhileProjectile = 0f;
            }

            bool enemyAlive =
                dodgeEnemy != null &&
                !dodgeEnemy.IsDead;

            if (!enemyAlive)
            {
                respawnTimer -= Time.deltaTime;

                if (respawnTimer <= 0f)
                {
                    SpawnDodgeEnemy();
                    respawnTimer = dodgeRespawnInterval;
                }
            }
            else
            {
                respawnTimer = dodgeRespawnInterval;
            }

            yield return null;
        }
    }

    private static bool AnyEnemyProjectileActive()
    {
        return
            Object.FindObjectsByType<EnemyProjectile>().Length > 0;
    }

    private static IEnumerator WaitAllDead(List<Enemy> enemies)
    {
        while (true)
        {
            bool anyAlive = false;

            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy enemy = enemies[i];

                if (enemy != null && !enemy.IsDead)
                {
                    anyAlive = true;
                    break;
                }
            }

            if (!anyAlive)
                yield break;

            yield return null;
        }
    }

    private void HandleUpgradeChosen(int index)
    {
        upgradeChosen = true;
    }

    private void UnsubscribeUpgrade()
    {
        if (!upgradeSubscribed)
            return;

        if (upgradeUI != null)
            upgradeUI.OnUpgradeChosen -= HandleUpgradeChosen;

        upgradeSubscribed = false;
    }

    // =========================================================
    // UI
    // =========================================================

    private void FinishTutorial()
    {
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
            string.IsNullOrEmpty(text) ? string.Empty : text;

        if (!objectiveText.gameObject.activeSelf)
            objectiveText.gameObject.SetActive(true);
    }

    private void SetStepProgress(int step)
    {
        if (progressText == null)
            return;

        progressText.text =
            $"ШАГ {step}/{TotalSteps}";

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