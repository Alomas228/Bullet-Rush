using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Обучение нового игрока. Запускается автоматически при нажатии «Играть»,
/// если игрок ещё не проходил его (флаг хранится в PlayerPrefs).
///
/// Шаги:
///  1. Картинка WASD — ждём, пока игрок реально нажмёт движение;
///  2. Картинка прицеливания мышью — ждём движения мыши;
///  3. Картинка стрельбы — ждём клика ЛКМ;
///  4. Один враг с меткой — ждём его смерти;
///  5. Группа врагов — ждём, пока всех убьют;
///  6. Окно улучшений с пояснением — ждём выбора игрока.
/// После этого обучение помечается пройденным и стартует первая волна.
/// </summary>
public class TutorialManager : MonoBehaviour
{
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
    [Tooltip("Текст задачи сверху («УБЕЙ ЕГО!» и т.д.).")]
    [SerializeField] private TMP_Text objectiveText;

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
    [Tooltip("На каком радиусе от игрока появляются враги обучения.")]
    [SerializeField] private float enemySpawnRadius = 8f;
    [SerializeField] private int groupEnemyCount = 3;
    [Tooltip("Ставить метку над врагами из второй группы.")]
    [SerializeField] private bool markerOnGroup = true;
    [Tooltip("Пауза между шагами обучения.")]
    [SerializeField] private float pauseBetweenSteps = 0.6f;

    [Header("Texts")]
    [SerializeField] private string killOneText = "УБЕЙ ЕГО!";
    [SerializeField] private string killGroupText = "УБЕЙ ОСТАЛЬНЫХ!";
    [SerializeField] private string upgradeHintText =
        "ЭТО ОКНО УЛУЧШЕНИЙ.\n" +
        "ВЫБЕРИ ОДНО — ОНО УСИЛИТ ТЕБЯ В ЭТОМ ЗАБЕГЕ.";

    /// <summary>True — обучение прямо сейчас идёт.</summary>
    public bool IsRunning { get; private set; }

    private Transform player;
    private bool upgradeChosen;
    private bool subscribedState;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
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
        if (Instance == this)
            Instance = null;
    }

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Menu && IsRunning)
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

        IsRunning = true;
        upgradeChosen = false;

        if (tutorialPanel != null)
            tutorialPanel.SetActive(true);

        HideAllSteps();

        StartCoroutine(TutorialRoutine());
        return true;
    }

    [ContextMenu("Reset Tutorial Flag")]
    private void ResetTutorialFlag()
    {
        IsCompleted = false;
    }

    private void AbortTutorial()
    {
        StopAllCoroutines();

        IsRunning = false;
        upgradeChosen = false;

        if (upgradeUI != null)
        {
            upgradeUI.OnUpgradeChosen -= HandleUpgradeChosen;

            if (upgradeUI.IsShowing)
                upgradeUI.Hide();
        }

        HideAllSteps();

        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);

        Time.timeScale = 1f;
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
        yield return ImageStep(wasdImage, WaitForMove());

        // 2. Прицеливание
        yield return ImageStep(aimImage, WaitForAim());

        // 3. Стрельба
        yield return ImageStep(shootImage, WaitForShoot());

        // 4. Один враг
        SetObjective(killOneText);

        yield return WaitAllDead(SpawnEnemies(1, true));

        yield return new WaitForSeconds(pauseBetweenSteps);

        // 5. Группа врагов
        SetObjective(killGroupText);

        yield return WaitAllDead(
            SpawnEnemies(groupEnemyCount, markerOnGroup)
        );

        yield return new WaitForSeconds(pauseBetweenSteps);

        // 6. Окно улучшений
        SetObjective(upgradeHintText);

        if (upgradeUI != null)
        {
            upgradeChosen = false;
            upgradeUI.OnUpgradeChosen += HandleUpgradeChosen;

            upgradeUI.Show();

            while (!upgradeChosen)
                yield return null;

            upgradeUI.OnUpgradeChosen -= HandleUpgradeChosen;

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

    // =========================================================
    // COMBAT STEPS
    // =========================================================

    private List<Enemy> SpawnEnemies(
        int count,
        bool withMarker)
    {
        List<Enemy> result = new List<Enemy>();

        if (enemySpawner == null || player == null)
        {
            Debug.LogWarning(
                "TutorialManager: Нет EnemySpawner или игрока — " +
                "враги обучения не заспавнены.",
                this
            );

            return result;
        }

        for (int i = 0; i < count; i++)
        {
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
                    EnemyType.Normal,
                    position
                );

            if (enemy == null)
                continue;

            if (withMarker)
            {
                TutorialEnemyMarker.Attach(
                    enemy,
                    markerSprite,
                    markerOffset,
                    markerBobSpeed,
                    markerBobHeight
                );
            }

            result.Add(enemy);
        }

        return result;
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

        if (objectiveText != null)
        {
            objectiveText.text = string.Empty;
            objectiveText.gameObject.SetActive(false);
        }
    }
}
