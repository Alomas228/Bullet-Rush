using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("HUD")]
    [Tooltip("Кнопка паузы на экране боя. Подписывается автоматически.")]
    [SerializeField] private Button pauseButton;

    [Header("Pause Panel")]
    [Tooltip("Панель паузы. Показывается/скрывается при паузе.")]
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button menuButton;
    [SerializeField] private Button settingsButton;

    [Header("Settings Panel (optional)")]
    [Tooltip("Необязательная панель настроек, открывается по кнопке «Настройки».")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Button settingsBackButton;

    public bool IsPaused { get; private set; }

    /// <summary>Оповещает о смене паузы (true = игра на паузе). Нужен Gameplay API.</summary>
    public event System.Action<bool> OnPauseChanged;

    private UpgradeUI cachedUpgradeUI;
    private bool subscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (pauseButton != null)
            pauseButton.onClick.AddListener(TogglePause);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(ResumeGame);

        if (restartButton != null)
            restartButton.onClick.AddListener(RestartGame);

        if (menuButton != null)
            menuButton.onClick.AddListener(BackToMenu);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OpenSettings);

        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(CloseSettings);
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void Start()
    {
        EnsureSubscribed();

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void OnDisable()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
    }

    private void OnDestroy()
    {
        if (pauseButton != null)
            pauseButton.onClick.RemoveListener(TogglePause);

        if (resumeButton != null)
            resumeButton.onClick.RemoveListener(ResumeGame);

        if (restartButton != null)
            restartButton.onClick.RemoveListener(RestartGame);

        if (menuButton != null)
            menuButton.onClick.RemoveListener(BackToMenu);

        if (settingsButton != null)
            settingsButton.onClick.RemoveListener(OpenSettings);

        if (settingsBackButton != null)
            settingsBackButton.onClick.RemoveListener(CloseSettings);
    }

    private void EnsureSubscribed()
    {
        if (subscribed)
            return;

        if (GameStateManager.Instance == null)
            return;

        GameStateManager.Instance.OnGameStateChanged += HandleStateChanged;
        subscribed = true;
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            TogglePause();
        }
    }

    private void HandleStateChanged(GameState state)
    {
        if (state != GameState.Playing && IsPaused)
            ResetPause();
    }

    public void TogglePause()
    {
        if (IsPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if (IsPaused)
            return;

        if (GameStateManager.Instance == null)
            return;

        if (GameStateManager.Instance.CurrentState != GameState.Playing)
            return;

        if (IsUpgradeSelectionOpen())
            return;

        IsPaused = true;
        Time.timeScale = 0f;

        if (pausePanel != null)
            pausePanel.SetActive(true);

        OnPauseChanged?.Invoke(true);
    }

    public void ResumeGame()
    {
        if (!IsPaused)
            return;

        IsPaused = false;

        if (GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState == GameState.Playing)
        {
            Time.timeScale = 1f;
        }

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        OnPauseChanged?.Invoke(false);
    }

    private void ResetPause()
    {
        IsPaused = false;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private bool IsUpgradeSelectionOpen()
    {
        if (cachedUpgradeUI == null)
            cachedUpgradeUI = FindAnyObjectByType<UpgradeUI>();

        return
            cachedUpgradeUI != null &&
            cachedUpgradeUI.IsShowing;
    }

    private void RestartGame()
    {
        if (GameOverManager.Instance != null)
            GameOverManager.Instance.RestartGame();
        else
            ResetPause();
    }

    private void BackToMenu()
    {
        if (GameOverManager.Instance != null)
        {
            GameOverManager.Instance.BackToMenu();
        }
        else
        {
            ResetPause();
            Time.timeScale = 1f;
        }
    }

    private void OpenSettings()
    {
        if (settingsPanel == null)
            return;

        if (pausePanel != null)
            pausePanel.SetActive(false);

        settingsPanel.SetActive(true);
    }

    private void CloseSettings()
    {
        if (settingsPanel == null)
            return;

        settingsPanel.SetActive(false);

        if (pausePanel != null && IsPaused)
            pausePanel.SetActive(true);
    }
}