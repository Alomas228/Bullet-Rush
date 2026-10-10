using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель «Карты» в главном меню: выбор карты для следующего забега.
///
/// Список карт берёт из EnvironmentController (maps, заданные в его
/// инспекторе). Композиция — горизонтальная: на экран помещается
/// MapSelectionUI.CardsPerPage карточек, по краям стрелки листают
/// страницы, снизу точки пагинации.
///
/// Клик по карточке вызывает EnvironmentController.SwitchTo(index) —
/// мир под камерой уезжает/приезжает прямо на фоне панели, а рамка
/// выбранной карточки переезжает следом. Намеренно не блокирует
/// слайд: тот идёт через Time.unscaledDeltaTime.
/// </summary>
public class MapSelectionUI : MonoBehaviour
{
    /// <summary>Сколько карточек помещается на экран.</summary>
    public const int CardsPerPage = 5;

    private sealed class Dot
    {
        public Image image;
        public LayoutElement layout;
    }

    [Header("Cards")]
    [SerializeField] private List<MapCardView> cardSlots =
        new List<MapCardView>();

    [Header("Pagination")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Image previousImage;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image nextImage;
    [SerializeField] private RectTransform dotsRoot;
    [SerializeField] private GameObject dotTemplate;

    private readonly List<Dot> dots = new List<Dot>();

    private EnvironmentController environment;
    private int page;
    private bool built;
    private bool subscribed;
    private bool warnedNoEnvironment;

    private int PageCount
    {
        get
        {
            if (environment == null)
                return 1;

            int total = environment.MapsCount;

            return Mathf.Max(1, Mathf.CeilToInt(total / (float)CardsPerPage));
        }
    }

    private void OnEnable()
    {
        EnsureBuilt();

        if (!built)
            return;

        Subscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    // =====================================================
    // BUILD
    // =====================================================

    private void EnsureBuilt()
    {
        if (built)
            return;

        environment =
            FindAnyObjectByType<EnvironmentController>();

        if (environment == null)
        {
            if (!warnedNoEnvironment)
            {
                warnedNoEnvironment = true;

                Debug.LogWarning(
                    "[Maps] EnvironmentController не найден в сцене.\n" +
                    "Запусти Tools -> Bullet Rush -> Build Maps UI, " +
                    "он создаст объект с картами, либо повесь " +
                    "EnvironmentController вручную и назначь GameMap.",
                    this
                );
            }

            enabled = false;

            return;
        }

        if (!HasLayout())
        {
            Debug.LogError(
                "[Maps] Панель не собрана билдером: нет карточек или " +
                "кнопок пагинации.\n" +
                "Запусти Tools -> Bullet Rush -> Build Maps UI.",
                this
            );

            enabled = false;

            return;
        }

        built = true;

        WireButtons();
        RegisterSlots();
        BuildDots();
    }

    /// <summary>
    /// Проверяет, что на объекте лежит иерархия билдера.
    /// </summary>
    private bool HasLayout()
    {
        if (cardSlots == null || cardSlots.Count == 0)
            return false;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (cardSlots[i] == null)
                return false;
        }

        return previousButton != null && nextButton != null;
    }

    private void WireButtons()
    {
        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousPage);

        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);
    }

    private void RegisterSlots()
    {
        for (int i = 0; i < cardSlots.Count; i++)
            RegisterCard(cardSlots[i]);
    }

    private void BuildDots()
    {
        if (dotsRoot == null || dotTemplate == null)
            return;

        dotTemplate.SetActive(false);

        for (int i = 0; i < PageCount; i++)
        {
            GameObject dotObject = Instantiate(dotTemplate, dotsRoot);

            dotObject.name = $"Dot_{i + 1}";

            dotObject.SetActive(true);

            Button dotButton = dotObject.GetComponent<Button>();

            if (dotButton != null)
            {
                int target = i;

                dotButton.onClick.AddListener(
                    () => GoToPage(target)
                );
            }

            dots.Add(
                new Dot
                {
                    image = dotObject.GetComponent<Image>(),
                    layout = dotObject.GetComponent<LayoutElement>()
                }
            );
        }
    }

    // =====================================================
    // REFRESH
    // =====================================================

    public void Refresh()
    {
        EnsureBuilt();

        if (!built)
            return;

        if (environment == null)
            return;

        ShowPage();
    }

    private void ShowPage()
    {
        if (environment == null)
            return;

        int start = page * CardsPerPage;
        int mapsCount = environment.MapsCount;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            MapCardView slot = cardSlots[i];

            if (slot == null)
                continue;

            int index = start + i;

            bool visible =
                index < mapsCount &&
                environment.GetMap(index) != null;

            if (slot.gameObject.activeSelf != visible)
                slot.gameObject.SetActive(visible);

            if (!visible)
                continue;

            slot.Bind(environment.GetMap(index));

            slot.SetSelected(index == environment.CurrentIndex);
        }

        UpdatePagination();
    }

    private void UpdatePagination()
    {
        if (environment == null)
            return;

        bool hasPrevious = page > 0;
        bool hasNext = page < PageCount - 1;

        if (previousButton != null)
            previousButton.interactable = hasPrevious;

        if (nextButton != null)
            nextButton.interactable = hasNext;

        if (previousImage != null)
        {
            previousImage.color = hasPrevious
                ? MapSelectionTheme.Surface
                : MapSelectionTheme.SurfaceMuted;
        }

        if (nextImage != null)
        {
            nextImage.color = hasNext
                ? MapSelectionTheme.Surface
                : MapSelectionTheme.SurfaceMuted;
        }

        for (int i = 0; i < dots.Count; i++)
        {
            Dot dot = dots[i];
            bool active = i == page;

            if (dot.layout != null)
            {
                float size = active ? 22f : 12f;

                dot.layout.preferredWidth = size;
                dot.layout.preferredHeight = size;
            }

            if (dot.image != null)
            {
                dot.image.color = active
                    ? MapSelectionTheme.DotActive
                    : MapSelectionTheme.DotInactive;
            }
        }
    }

    // =====================================================
    // PAGINATION
    // =====================================================

    private void PreviousPage()
    {
        GoToPage(page - 1);
    }

    private void NextPage()
    {
        GoToPage(page + 1);
    }

    private void GoToPage(int value)
    {
        if (environment == null)
            return;

        int clamped = Mathf.Clamp(value, 0, PageCount - 1);

        if (clamped == page)
            return;

        page = clamped;

        ShowPage();

        PlayUiClick();
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    private void OnMapClicked(MapCardView card)
    {
        if (environment == null)
            return;

        int slotIndex = cardSlots.IndexOf(card);

        if (slotIndex < 0)
            return;

        int mapIndex = page * CardsPerPage + slotIndex;

        if (mapIndex < 0 || mapIndex >= environment.MapsCount)
            return;

        environment.SwitchTo(mapIndex);

        ShowPage();

        PlayMapChange();
    }

    // =====================================================
    // EVENTS
    // =====================================================

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (environment == null)
            return;

        environment.OnMapChanged += HandleMapChanged;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (environment != null)
            environment.OnMapChanged -= HandleMapChanged;

        subscribed = false;
    }

    private void HandleMapChanged(GameMap map)
    {
        Refresh();
    }

    // =====================================================
    // SOUND
    // =====================================================

    private void PlayUiClick()
    {
        SFXLibrary sfx = GetSfx();

        if (sfx != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayUI(sfx.UiClick);
    }

    private void PlayMapChange()
    {
        SFXLibrary sfx = GetSfx();

        if (sfx != null && AudioManager.Instance != null)
            AudioManager.Instance.PlayUI(sfx.MapChange);
    }

    private static SFXLibrary GetSfx()
    {
        if (AudioManager.Instance == null)
            return null;

        return AudioManager.Instance.SFXLibrary;
    }

    // =====================================================
    // SETUP
    // =====================================================

    /// <summary>
    /// Раздаёт карточкам обработчик выбора. Один раз за жизнь панели,
    /// из EnsureBuilt.
    /// </summary>
    public void RegisterCard(MapCardView card)
    {
        if (card == null)
            return;

        if (card.Wired)
            return;

        if (card.SelectButton != null)
        {
            MapCardView captured = card;

            card.SelectButton.onClick.AddListener(
                () => OnMapClicked(captured)
            );
        }

        card.MarkWired();
    }
}