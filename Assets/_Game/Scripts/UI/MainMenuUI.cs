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

    [Header("Panel")]
    [SerializeField] private GameObject menuPanel;

    [Header("Buttons")]
    [SerializeField] private Button playButton;

    [Header("Optional")]
    [SerializeField] private TMP_Text titleText;

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
        if (playButton != null)
            playButton.onClick.AddListener(OnPlayClicked);
    }

    private void OnEnable()
    {
        EnsureSubscribed();
    }

    private void OnDestroy()
    {
        if (playButton != null)
            playButton.onClick.RemoveListener(OnPlayClicked);
    }

    private void Start()
    {
        EnsureSubscribed();

        CaptureRestPositions();

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

    public void ShowMenu()
    {
        slidingOut = false;

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

        Time.timeScale = 1f;

        if (slideCoroutine != null)
            StopCoroutine(slideCoroutine);
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