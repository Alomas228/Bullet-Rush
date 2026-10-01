using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель «Улучшения» в главном меню: магазин постоянных улучшений.
///
/// Логика экрана простая: у каждого параметра свой уровень от 0
/// до PermanentUpgrades.MaxLevel, следующий уровень покупается
/// за монеты. Опыта и уровня игрока на этом экране нет.
///
/// Композиция — горизонтальная: одновременно видно
/// PermanentUpgrades.CardsPerPage карточек, по краям стрелки
/// листают страницы, снизу точки пагинации. Вертикального
/// списка и длинного скролла нет.
///
/// Кнопка «ОБНОВИТЬ» заново раздаёт набор улучшений за монеты —
/// это заглушка механики, порядок карточек после обновления
/// меняется случайно.
/// </summary>
public class PermanentUpgradesUI : MonoBehaviour, ILangRefreshable
{
    /// <summary>Сколько карточек помещается на экран.</summary>
    public const int CardsPerPage = 4;

    [Tooltip("Порядок выдачи улучшений. Первые четыре — основные группы.")]
    private static readonly PermanentUpgradeStat[] DefaultOrder =
    {
        PermanentUpgradeStat.Damage,
        PermanentUpgradeStat.MaxHealth,
        PermanentUpgradeStat.MoveSpeed,
        PermanentUpgradeStat.CriticalChance,
        PermanentUpgradeStat.FireRate,
        PermanentUpgradeStat.HealthRegen,
        PermanentUpgradeStat.ProjectileSpeed,
        PermanentUpgradeStat.CriticalDamage,
        PermanentUpgradeStat.DashCooldown,
        PermanentUpgradeStat.AbilityCooldown
    };

    private sealed class Dot
    {
        public Image image;
        public LayoutElement layout;
    }

    [Header("Top bar")]
    [SerializeField] private TMP_Text coinsText;

    [Header("Cards")]
    [SerializeField] private List<PermanentUpgradeCardView> cardSlots =
        new List<PermanentUpgradeCardView>();

    [Header("Pagination")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Image previousImage;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image nextImage;
    [SerializeField] private RectTransform dotsRoot;
    [SerializeField] private GameObject dotTemplate;

    [Header("Refresh")]
    [SerializeField] private Button refreshButton;
    [SerializeField] private Image refreshImage;
    [SerializeField] private TMP_Text refreshCostText;

    [Tooltip("Цена обновления набора улучшений.")]
    [SerializeField] private int refreshCost = 50;

    private readonly List<Dot> dots = new List<Dot>();

    private PermanentUpgradeStat[] order;
    private int page;
    private int selectedIndex = -1;
    private bool built;
    private bool subscribed;

    private int PageCount
    {
        get
        {
            int total = order != null ? order.Length : 0;

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

    /// <summary>
    /// Перерисовывает карточки и цену обновления на новом языке.
    /// Refresh() только пересобирает подписи из текущих данных
    /// и не меняет состояние панели, поэтому безопасно вызывать
    /// прямо во время игры.
    /// </summary>
    public void RefreshLang()
    {
        if (!built)
            return;

        Refresh();
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

        if (!HasLayout())
        {
            Debug.LogError(
                "[Upgrades] Панель не собрана новым билдером: " +
                "нет карточек или кнопок пагинации.\n" +
                "Запусти Tools -> Bullet Rush -> Build Upgrades UI.",
                this
            );

            enabled = false;

            return;
        }

        built = true;

        order = new PermanentUpgradeStat[DefaultOrder.Length];

        Array.Copy(DefaultOrder, order, DefaultOrder.Length);

        WireButtons();
        RegisterSlots();
        BuildDots();
    }

    /// <summary>
    /// Проверяет, что на объекте лежит новая иерархия билдера.
    /// Нужна, потому что старая панель в сцене ещё может сохраниться
    /// с прошлой версии компонента, где полей не было.
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

    private void RegisterSlots()
    {
        for (int i = 0; i < cardSlots.Count; i++)
            RegisterCard(cardSlots[i]);
    }

    private void WireButtons()
    {
        if (previousButton != null)
        {
            previousButton.onClick.AddListener(PreviousPage);
        }

        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);

        if (refreshButton != null)
            refreshButton.onClick.AddListener(OnRefreshClicked);
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

        int coins = CurrentCoins();

        if (coinsText != null)
            coinsText.text = coins.ToString();

        ShowPage();

        for (int i = 0; i < cardSlots.Count; i++)
        {
            PermanentUpgradeCardView slot = cardSlots[i];

            if (slot != null && slot.gameObject.activeSelf)
                slot.Refresh(coins);
        }

        if (refreshButton != null)
            refreshButton.interactable = coins >= refreshCost;

        if (refreshImage != null)
        {
            refreshImage.color = coins >= refreshCost
                ? UpgradesWireframeTheme.Action
                : UpgradesWireframeTheme.ActionDisabled;
        }

        if (refreshCostText != null)
            refreshCostText.text = Lang.Get("upg.refresh_cost", refreshCost);
    }

    private void ShowPage()
    {
        if (order == null)
            return;

        int start = page * CardsPerPage;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            PermanentUpgradeCardView slot = cardSlots[i];

            if (slot == null)
                continue;

            int index = start + i;

            bool visible = index < order.Length;

            if (slot.gameObject.activeSelf != visible)
                slot.gameObject.SetActive(visible);

            if (!visible)
                continue;

            if (!slot.HasStat || slot.Stat != order[index])
                slot.Bind(order[index]);

            slot.SetSelected(index == selectedIndex);
        }

        UpdatePagination();
    }

    private void UpdatePagination()
    {
        bool hasPrevious = page > 0;
        bool hasNext = page < PageCount - 1;

        if (previousButton != null)
            previousButton.interactable = hasPrevious;

        if (nextButton != null)
            nextButton.interactable = hasNext;

        if (previousImage != null)
        {
            previousImage.color = hasPrevious
                ? UpgradesWireframeTheme.Surface
                : UpgradesWireframeTheme.SurfaceDisabled;
        }

        if (nextImage != null)
        {
            nextImage.color = hasNext
                ? UpgradesWireframeTheme.Surface
                : UpgradesWireframeTheme.SurfaceDisabled;
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
                    ? UpgradesWireframeTheme.DotActive
                    : UpgradesWireframeTheme.DotInactive;
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
        if (order == null)
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

    private void OnSelectClicked(PermanentUpgradeCardView card)
    {
        if (card == null || !card.HasStat)
            return;

        selectedIndex = IndexOf(card.Stat);

        ShowPage();

        PlayUiClick();
    }

    private void OnBuyClicked(PermanentUpgradeCardView card)
    {
        if (card == null || !card.HasStat)
            return;

        bool bought = PermanentUpgrades.TryBuy(card.Stat);

        if (bought)
            PlayPurchaseSound();
        else
            PlayUiClick();

        Refresh();
    }

    /// <summary>
    /// Перемешивает набор улучшений за монеты: карточки
    /// переезжают на другие страницы.
    /// </summary>
    private void OnRefreshClicked()
    {
        EnsureBuilt();

        XpManager xp = XpManager.Instance;

        if (xp == null)
            return;

        int cost = Mathf.Max(0, refreshCost);

        if (xp.GlobalCoins < cost)
        {
            PlayUiClick();
            return;
        }

        if (cost > 0 && !xp.TrySpendCoins(cost))
            return;

        Shuffle();

        page = 0;
        selectedIndex = -1;

        Refresh();

        PlayUiClick();
    }

    private void Shuffle()
    {
        for (int i = order.Length - 1; i > 0; i--)
        {
            int swap = UnityEngine.Random.Range(0, i + 1);

            PermanentUpgradeStat temp = order[i];

            order[i] = order[swap];
            order[swap] = temp;
        }
    }

    private int IndexOf(PermanentUpgradeStat stat)
    {
        if (order == null)
            return -1;

        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == stat)
                return i;
        }

        return -1;
    }

    private int CurrentCoins()
    {
        return XpManager.Instance != null
            ? XpManager.Instance.GlobalCoins
            : 0;
    }

    // =====================================================
    // EVENTS
    // =====================================================

    private void Subscribe()
    {
        if (subscribed)
            return;

        if (XpManager.Instance == null)
            return;

        XpManager.Instance.OnCoinsChanged += HandleCoinsChanged;

        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed)
            return;

        if (XpManager.Instance != null)
            XpManager.Instance.OnCoinsChanged -= HandleCoinsChanged;

        subscribed = false;
    }

    private void HandleCoinsChanged(int coins)
    {
        Refresh();
    }

    // =====================================================
    // SOUND
    // =====================================================

    private void PlayPurchaseSound()
    {
        SFXLibrary sfx = GetSfx();

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.UpgradePick,
                priority: SfxPriority.High
            );
    }

    private void PlayUiClick()
    {
        SFXLibrary sfx = GetSfx();

        if (sfx != null)
            AudioManager.Instance.PlayUI(sfx.UiClick);
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
    /// Раздаёт карточкам обработчики кнопок. Один раз за жизнь
    /// панели, из EnsureBuilt.
    /// </summary>
    public void RegisterCard(PermanentUpgradeCardView card)
    {
        if (card == null)
            return;

        if (card.SelectButton != null)
        {
            PermanentUpgradeCardView captured = card;

            card.SelectButton.onClick.AddListener(
                () => OnSelectClicked(captured)
            );
        }

        if (card.BuyButton != null)
        {
            PermanentUpgradeCardView captured = card;

            card.BuyButton.onClick.AddListener(
                () => OnBuyClicked(captured)
            );
        }
    }
}
