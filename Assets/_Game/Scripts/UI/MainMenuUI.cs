using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class MainMenuUI : MonoBehaviour
{
    public static event System.Action OnMenuAppeared;

    [Tooltip("Если true — меню показывается мгновенно, без слайда (для чистого появления после перезагрузки сцены).")]
    public static bool ShowWithoutAnimation;

    [Tooltip("Если true — пока идёт возврат в меню (камера ещё летит к меню), кнопка Play недоступна.")]
    public static bool MenuReloadPending;

    public static MainMenuUI Instance { get; private set; }

    [Header("Panel")]
    [SerializeField] private GameObject menuPanel;

    [Header("Buttons")]
    [SerializeField] private Button playButton;

    [Header("Submenu Buttons")]
    [SerializeField] private Button shopButton;
    [SerializeField] private Button upgradesButton;
    [SerializeField] private Button profileButton;
    [SerializeField] private Button tipsButton;

    [Header("Submenu Panels")]
    [Tooltip("Каждая кнопка ссылается на свою панель. Создай панели в канвасе и перетяни сюда.")]
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject upgradesPanel;
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject tipsPanel;

    [Header("Optional")]
    [SerializeField] private TMP_Text titleText;

    [Header("Player Progress (optional, shows persistent Player Level + XP)")]
    [Tooltip("«LEVEL 5» в главном меню.")]
    [SerializeField] private TMP_Text playerLevelText;
    [Tooltip("«125 / 225 XP» в главном меню.")]
    [SerializeField] private TMP_Text playerXpText;
    [Tooltip("Полоса прогресса текущего уровня игрока (Image with fill).")]
    [SerializeField] private Slider xpProgressBar;

    [Header("Slide Animation (optional)")]
    [Tooltip("Элементы (кнопки, текст), которые будут уезжать. Оставь пустым для мгновенного показа/скрытия.")]
    [SerializeField] private RectTransform[] menuElements;
    [SerializeField] private Vector2 slideOffset = new Vector2(1200f, 0f);
    [SerializeField] private float slideDuration = 0.4f;

    private Vector2[] restPositions;
    private Coroutine slideCoroutine;
    private bool subscribed;
    private bool slidingOut;

    private void Awake()
    {
        Instance = this;

        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);

        if (shopButton != null)
            shopButton.onClick.AddListener(OnShopClicked);

        if (upgradesButton != null)
            upgradesButton.onClick.AddListener(OnUpgradesClicked);

        if (profileButton != null)
            profileButton.onClick.AddListener(OnProfileClicked);

        if (tipsButton != null)
            tipsButton.onClick.AddListener(OnTipsClicked);
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        if (playButton != null)
            playButton.onClick.RemoveListener(OnPlayClicked);

        if (shopButton != null)
            shopButton.onClick.RemoveListener(OnShopClicked);

        if (upgradesButton != null)
            upgradesButton.onClick.RemoveListener(OnUpgradesClicked);

        if (profileButton != null)
            profileButton.onClick.RemoveListener(OnProfileClicked);

        if (tipsButton != null)
            tipsButton.onClick.RemoveListener(OnTipsClicked);
    }

    private void Start()
    {
        EnsureSubscribed();

        CaptureRestPositions();

        RefreshPlayerDisplay();

        if (GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing)
        {
            HideMenu();
        }
        else
        {
            ShowMenu();
        }
    }

    private void CaptureRestPositions()
    {
        if (menuElements == null || menuElements.Length == 0)
            return;

        restPositions = new Vector2[menuElements.Length];

        for (int i = 0; i < menuElements.Length; i++)
        {
            if (menuElements[i] != null)
                restPositions[i] = menuElements[i].anchoredPosition;
        }
    }

    private void OnDisable()
    {
        if (subscribed && GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnGameStateChanged -= HandleStateChanged;
            subscribed = false;
        }
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

    private void HandleStateChanged(GameState state)
    {
        if (state == GameState.Menu)
            ShowMenu();
        else if (!slidingOut)
            HideMenu();
    }

    /// <summary>
    /// Обновляет блок «Уровень игрока / XP» в главном меню из
    /// персистентных данных XpManager (тот же источник, что и GameOverUI).
    /// </summary>
    private void RefreshPlayerDisplay()
    {
        XpManager xp = XpManager.Instance;

        if (xp == null)
            return;

        if (playerLevelText != null)
            playerLevelText.text = $"LEVEL {xp.GetPlayerLevel()}";

        if (playerXpText != null)
        {
            playerXpText.text =
                $"{xp.GlobalXPInCurrentLevel} / " +
                $"{xp.GlobalXPNeededForNextLevel} XP";
        }

        if (xpProgressBar != null)
            xpProgressBar.value = xp.GlobalLevelProgress;
    }

    public void ShowMenu()
    {
        slidingOut = false;

        CloseSubPanels();

        if (menuPanel != null)
            menuPanel.SetActive(true);

        Time.timeScale = 0f;

        if (playButton != null)
            playButton.interactable = !MenuReloadPending;

        if (ShowWithoutAnimation)
        {
            ShowWithoutAnimation = false;
            RestoreElementsToRest();
            OnMenuAppeared?.Invoke();
            return;
        }

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        PlaceElementsAtOffset();

        BeginMenuAppear();
    }

    private void BeginMenuAppear()
    {
        if (HasSlideElements())
        {
            if (slideCoroutine == null)
                slideCoroutine = StartCoroutine(SlideElementsToRest());
        }
        else
        {
            OnMenuAppeared?.Invoke();
        }
    }

    public void HideMenu()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);

        CloseSubPanelsWhenHidden();

        Time.timeScale = 1f;

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);
    }

    private void OnShopClicked()
    {
        OpenSubPanel(shopPanel);
    }

    private void OnUpgradesClicked()
    {
        OpenSubPanel(upgradesPanel);
    }

    private void OnProfileClicked()
    {
        OpenSubPanel(profilePanel);
    }

    private void OnTipsClicked()
    {
        OpenSubPanel(tipsPanel);
    }

    public void OpenSubPanel(GameObject panelToShow)
    {
        CloseSubPanels();
        HideMenuButtons();

        if (panelToShow != null)
            panelToShow.SetActive(true);
    }

    public void CloseSubPanels()
    {
        CloseSubPanelsWhenHidden();
        ShowMenuButtons();
    }

    private void CloseSubPanelsWhenHidden()
    {
        if (shopPanel != null)
            shopPanel.SetActive(false);

        if (upgradesPanel != null)
            upgradesPanel.SetActive(false);

        if (profilePanel != null)
            profilePanel.SetActive(false);

        if (tipsPanel != null)
            tipsPanel.SetActive(false);
    }

    private void HideMenuButtons()
    {
        SetMenuButtonsActive(false);
    }

    private void ShowMenuButtons()
    {
        SetMenuButtonsActive(true);
    }

    private void SetMenuButtonsActive(bool active)
    {
        if (playButton != null)
            playButton.gameObject.SetActive(active);

        if (shopButton != null)
            shopButton.gameObject.SetActive(active);

        if (upgradesButton != null)
            upgradesButton.gameObject.SetActive(active);

        if (profileButton != null)
            profileButton.gameObject.SetActive(active);

        if (tipsButton != null)
            tipsButton.gameObject.SetActive(active);
    }

    private void OnPlayClicked()
    {
        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);

        if (playButton != null)
            playButton.interactable = false;

        Time.timeScale = 1f;

        if (HasSlideElements())
        {
            slidingOut = true;

            slideCoroutine = StartCoroutine(
                SlideMenuOutAndHidePanel()
            );
        }
        else
        {
            HideMenu();
        }

        ThenStartGame();
    }

    private void ThenStartGame()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.StartGame();
    }

    private IEnumerator SlideMenuOutAndHidePanel()
    {
        yield return SlideElements(
            (i, t) => Vector2.LerpUnclamped(
                restPositions[i],
                restPositions[i] + slideOffset,
                t
            )
        );

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }

        slidingOut = false;

        HideMenu();
    }

    private bool HasSlideElements()
    {
        return
            menuElements != null &&
            menuElements.Length > 0 &&
            restPositions != null &&
            restPositions.Length == menuElements.Length;
    }

    private void PlaceElementsAtOffset()
    {
        if (!HasSlideElements())
            return;

        for (int i = 0; i < menuElements.Length; i++)
        {
            if (menuElements[i] == null)
                continue;

            menuElements[i].anchoredPosition =
                restPositions[i] + slideOffset;
        }
    }

    private void RestoreElementsToRest()
    {
        if (menuElements == null)
            return;

        for (int i = 0; i < menuElements.Length; i++)
        {
            if (menuElements[i] == null)
                continue;

            if (restPositions != null && i < restPositions.Length)
                menuElements[i].anchoredPosition = restPositions[i];
        }
    }

    private IEnumerator SlideElementsToRest()
    {
        yield return SlideElements(
            (i, t) => Vector2.LerpUnclamped(
                restPositions[i] + slideOffset,
                restPositions[i],
                t
            )
        );

        if (slideCoroutine != null)
        {
            StopCoroutine(slideCoroutine);
            slideCoroutine = null;
        }

        OnMenuAppeared?.Invoke();
    }

    private IEnumerator SlideElements(System.Func<int, float, Vector2> positionForTime)
    {
        if (menuElements.Length == 0)
            yield break;

        float timer = 0f;

        while (timer < slideDuration)
        {
            timer += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(timer / slideDuration);

            t = Mathf.SmoothStep(0f, 1f, t);

            for (int i = 0; i < menuElements.Length; i++)
            {
                if (menuElements[i] == null)
                    continue;

                menuElements[i].anchoredPosition =
                    positionForTime(i, t);
            }

            yield return null;
        }

        for (int i = 0; i < menuElements.Length; i++)
        {
            if (menuElements[i] == null)
                continue;

            menuElements[i].anchoredPosition =
                positionForTime(i, 1f);
        }
    }
}