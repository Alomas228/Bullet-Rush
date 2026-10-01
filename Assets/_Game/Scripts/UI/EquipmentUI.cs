using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель «Снаряжение» в главном меню: магазин предметов и
/// подбор боевого набора.
///
/// Три вкладки — Оружие, Способности, Одежда — приходят из
/// UpgradeManager (availableWeapons / availableAbilities /
/// availableClothing). Внутри каждой вкладки работает тот же
/// горизонтальный приём, что и на «Улучшениях»: по
/// CardsPerPage карточек, стрелки листают страницы, точки
/// показывают позицию. Вертикального списка нет.
///
/// Отличия от «Улучшений» намеренные:
///  - вкладка — главный навигатор, и на ней же написано, что
///    сейчас надето, поэтому отдельной строки «снаряжено» нет;
///  - состояние предмета (закрыт / куплен / надет) показывается
///    плашкой на карточке, а не уровнем в прогресс-баре;
///  - акцент карточки задаёт редкость предмета, а не группа
///    параметра;
///  - вместо кнопки обновления — счётчик страниц.
///
/// Состояние покупок и выбора живёт в EquipmentManager
/// (PlayerPrefs), здесь только отображение.
/// </summary>
public class EquipmentUI : MonoBehaviour, ILangRefreshable
{
    /// <summary>
    /// Сколько карточек помещается на страницу. Задаётся ассетом
    /// EquipmentLayoutSettings, чтобы размеры карточек и разбивка
    /// на страницы менялись из одного места.
    /// </summary>
    [Header("Разметка")]
    [SerializeField] private int cardsPerPage = 4;

    private int CardsPerPage => Mathf.Max(1, cardsPerPage);

    private enum TabKind
    {
        Weapons = 0,
        Abilities = 1,
        Clothing = 2
    }

    private sealed class TabRefs
    {
        public Button button;
        public Image image;
        public TMP_Text label;
        public TMP_Text value;
    }

    private sealed class Dot
    {
        public Image image;
        public LayoutElement layout;
    }

    [Header("Top bar")]
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text levelText;

    [Header("Tabs")]
    [Tooltip("Кнопки вкладок в порядке: Оружие, Способности, Одежда.")]
    [SerializeField] private List<Button> tabButtons = new List<Button>();

    [Tooltip("Подписи «что надето» под названием вкладки, тот же порядок.")]
    [SerializeField] private List<TMP_Text> tabValueTexts = new List<TMP_Text>();

    [Header("Cards")]
    [SerializeField] private List<EquipmentCardView> cardSlots =
        new List<EquipmentCardView>();

    [Header("Pagination")]
    [SerializeField] private Button previousButton;
    [SerializeField] private Image previousImage;
    [SerializeField] private Button nextButton;
    [SerializeField] private Image nextImage;
    [SerializeField] private RectTransform dotsRoot;
    [SerializeField] private GameObject dotTemplate;
    [SerializeField] private TMP_Text pageCounterText;

    [Header("Empty state")]
    [SerializeField] private GameObject emptyState;
    [SerializeField] private TMP_Text emptyStateText;

    /// <summary>
    /// Что показывать в подписи вкладки, когда ничего не надето.
    /// Спрашиваем у EquipmentManager, чтобы не дублировать его
    /// форматирование. Значение по умолчанию — ключ перевода,
    /// он разворачивается в язык, определённый при запуске.
    /// </summary>
    [SerializeField] private string emptySlotLabel = "{eq.empty_slot}";

    private sealed class WeaponEntry
    {
        public WeaponData data;
        public EquipmentCardData card;
    }

    private sealed class AbilityEntry
    {
        public AbilityData data;
        public EquipmentCardData card;
    }

    private sealed class ClothingEntry
    {
        public ClothingData data;
        public EquipmentCardData card;
    }

    private readonly List<WeaponEntry> weapons = new List<WeaponEntry>();
    private readonly List<AbilityEntry> abilities = new List<AbilityEntry>();
    private readonly List<ClothingEntry> clothing = new List<ClothingEntry>();
    private readonly List<Dot> dots = new List<Dot>();
    private readonly List<TabRefs> tabs = new List<TabRefs>();

    /// <summary>Страница у каждой вкладки своя.</summary>
    private readonly int[] pages = new int[3];

    private bool built;
    private bool subscribed;

    private TabKind activeTab = TabKind.Weapons;

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
    /// Перерисовывает вкладки и карточки на новом языке.
    /// Refresh() пересобирает подписи из текущих данных и не
    /// трогает снаряжение и прогресс, поэтому вызывается безопасно.
    /// </summary>
    public void RefreshLang()
    {
        if (!built)
            return;

        Refresh();
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
                "[Equipment] Панель не собрана новым билдером: " +
                "нет карточек или вкладок.\n" +
                "Запусти Tools -> Bullet Rush -> Build Equipment UI.",
                this
            );

            enabled = false;

            return;
        }

        built = true;

        CacheTabs();
        WireButtons();
        EnsureItems();
    }

    /// <summary>
    /// Проверяет, что на объекте лежит новая иерархия билдера.
    /// Нужна, потому что старая панель может сохраниться с
    /// прошлой версии компонента, где полей не было.
    /// </summary>
    private bool HasLayout()
    {
        if (tabButtons == null || tabButtons.Count == 0)
            return false;

        if (cardSlots == null || cardSlots.Count == 0)
            return false;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (cardSlots[i] == null)
                return false;
        }

        return previousButton != null && nextButton != null;
    }

    private void CacheTabs()
    {
        tabs.Clear();

        for (int i = 0; i < tabButtons.Count; i++)
        {
            Button button = tabButtons[i];

            TMP_Text value = null;

            if (tabValueTexts != null && i < tabValueTexts.Count)
                value = tabValueTexts[i];

            // Не пропускаем пустые кнопки: индекс в списке должен
            // совпадать с порядком вкладок, иначе в TabKind
            // попадёт не та вкладка.
            tabs.Add(
                new TabRefs
                {
                    button = button,
                    image = button != null
                        ? button.GetComponent<Image>()
                        : null,
                    label = button != null
                        ? button.GetComponentInChildren<TMP_Text>(true)
                        : null,
                    value = value
                }
            );
        }
    }

    private void WireButtons()
    {
        for (int i = 0; i < tabs.Count; i++)
        {
            TabRefs tab = tabs[i];
            int index = i;

            if (tab.button != null)
                tab.button.onClick.AddListener(() => SelectTab(index));
        }

        if (previousButton != null)
            previousButton.onClick.AddListener(PreviousPage);

        if (nextButton != null)
            nextButton.onClick.AddListener(NextPage);

        for (int i = 0; i < cardSlots.Count; i++)
            RegisterCard(cardSlots[i]);
    }

    /// <summary>
    /// Раздаёт карточкам обработчики. Клик по карточке и по строке
    /// действия делают одно и то же, поэтому обработчика один.
    /// </summary>
    public void RegisterCard(EquipmentCardView card)
    {
        if (card == null)
            return;

        if (card.Wired)
            return;

        card.MarkWired();

        EquipmentCardView captured = card;

        if (card.CardButton != null)
        {
            card.CardButton.onClick.AddListener(
                () => OnActionClicked(captured)
            );
        }

        if (card.ActionButton != null)
        {
            card.ActionButton.onClick.AddListener(
                () => OnActionClicked(captured)
            );
        }
    }

    // =====================================================
    // TABS
    // =====================================================

    private void SelectTab(int index)
    {
        if (index < 0 || index >= tabs.Count)
            return;

        if ((int)activeTab == index)
        {
            PlayUiClick();
            return;
        }

        activeTab = (TabKind)index;

        PlayUiClick();
        Refresh();
    }

    private void ShowTab()
    {
        int index = (int)activeTab;

        for (int i = 0; i < tabs.Count; i++)
        {
            TabRefs tab = tabs[i];

            bool active = i == index;

            if (tab.image != null)
            {
                tab.image.color = active
                    ? EquipmentWireframeTheme.Surface
                    : EquipmentWireframeTheme.SurfaceMuted;
            }

            if (tab.label != null)
            {
                tab.label.color = active
                    ? EquipmentWireframeTheme.TextPrimary
                    : EquipmentWireframeTheme.TextSecondary;
            }

            if (tab.value != null)
            {
                tab.value.color = active
                    ? EquipmentWireframeTheme.TextSecondary
                    : EquipmentWireframeTheme.TextMuted;
            }
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

        int coins = CurrentCoins();
        int level = CurrentLevel();

        if (coinsText != null)
            coinsText.text = coins.ToString();

        if (levelText != null)
            levelText.text = Lang.Get("eq.level_chip", level);

        ShowTab();
        RefreshTabValues();
        ShowPage(coins);
    }

    /// <summary>
    /// Под вкладкой показываем то, что сейчас надето в этой
    /// категории. Это заменяет отдельную строку «снаряжено»
    /// в шапке макета.
    /// </summary>
    private void RefreshTabValues()
    {
        if (tabs.Count < 3)
            return;

        SetTabValue(0, EquipmentManager.EquippedWeaponName);
        SetTabValue(1, JoinAbilities(EquipmentManager.EquippedAbilityNames));
        SetTabValue(2, EquipmentManager.EquippedClothingName);
    }

    private void SetTabValue(int index, string value)
    {
        if (index >= tabs.Count)
            return;

        TMP_Text text = tabs[index].value;

        if (text == null)
            return;

        text.text = string.IsNullOrEmpty(value)
            ? LangBinder.ResolveKey(emptySlotLabel)
            : value;
    }

    private static string JoinAbilities(IReadOnlyList<string> names)
    {
        if (names == null || names.Count == 0)
            return string.Empty;

        return string.Join(", ", names);
    }

    private void ShowPage(int coins)
    {
        int tabIndex = (int)activeTab;
        int count = CountOf(tabIndex);

        // Mathf.Clamp возвращает max, когда max меньше min, а при
        // пустом списке pageCount равен нулю и clamp давал -1. Из-за
        // этого start уходил в минус и CardAt падал на отрицательном
        // индексе. Пустая категория — это page 0, а не -1.
        int pageCount = PageCount(CountOf(tabIndex));

        int page = pageCount > 0
            ? Mathf.Clamp(pages[tabIndex], 0, pageCount - 1)
            : 0;

        pages[tabIndex] = page;

        int start = page * CardsPerPage;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            EquipmentCardView slot = cardSlots[i];

            if (slot == null)
                continue;

            int index = start + i;
            bool visible = index < count;

            if (slot.gameObject.activeSelf != visible)
                slot.gameObject.SetActive(visible);

            if (!visible)
                continue;

            EquipmentCardData card = CardAt(tabIndex, index);

            if (card == null)
                continue;

            // Владение и выбор меняются после покупки, поэтому
            // состояние перечитывается на каждом Refresh.
            card.SyncState();
            FillState(card);

            slot.Refresh(card, coins);
        }

        if (emptyState != null)
            emptyState.SetActive(count == 0);

        if (emptyStateText != null && count == 0)
        {
            emptyStateText.text = Lang.Get("eq.empty_state");
        }

        UpdatePagination(page, pageCount);
    }

    /// <summary>
    /// Дописывает в карточку текст состояния. Владение и выбор
    /// считает EquipmentCardData.SyncState — у неё есть ссылка
    /// на ассет и доступ к EquipmentManager.
    /// </summary>
    private static void FillState(EquipmentCardData card)
    {
        if (!card.Unlocked)
        {
            card.State = Lang.Get(
                "eq.state_locked_level",
                card.UnlockLevel
            );
            return;
        }

        if (!card.Owned)
        {
            card.State = Lang.Get("eq.state_not_owned");
            return;
        }

        card.State = card.Equipped
            ? Lang.Get("eq.state_equipped")
            : Lang.Get("eq.state_owned");
    }

    private void UpdatePagination(int page, int pageCount)
    {
        bool hasPrevious = page > 0;
        bool hasNext = page < pageCount - 1;

        if (previousButton != null)
            previousButton.interactable = hasPrevious;

        if (nextButton != null)
            nextButton.interactable = hasNext;

        if (previousImage != null)
        {
            previousImage.color = hasPrevious
                ? EquipmentWireframeTheme.Surface
                : EquipmentWireframeTheme.SurfaceDisabled;
        }

        if (nextImage != null)
        {
            nextImage.color = hasNext
                ? EquipmentWireframeTheme.Surface
                : EquipmentWireframeTheme.SurfaceDisabled;
        }

        if (pageCounterText != null)
        {
            pageCounterText.text = pageCount > 0
                ? Lang.Get("eq.page_counter", page + 1, pageCount)
                : Lang.Get("upg.dash_marker");
        }

        RebuildDots(pageCount);
        UpdateDots(page);
    }

    /// <summary>
    /// Точек может быть разное число: у оружия их четыре
    /// (15 предметов по четыре на страницу), у одежды одна.
    /// Пересобираем список, когда количество изменилось.
    /// </summary>
    private void RebuildDots(int pageCount)
    {
        if (dotsRoot == null || dotTemplate == null)
            return;

        if (dots.Count == pageCount)
            return;

        for (int i = 0; i < dots.Count; i++)
        {
            if (dots[i].image != null)
                Kill(dots[i].image.gameObject);
        }

        dots.Clear();

        for (int i = 0; i < pageCount; i++)
        {
            GameObject dotObject = Instantiate(dotTemplate, dotsRoot);

            dotObject.name = $"Dot_{i + 1}";
            dotObject.SetActive(true);

            Button dotButton = dotObject.GetComponent<Button>();

            if (dotButton != null)
            {
                int target = i;

                dotButton.onClick.AddListener(() => GoToPage(target));
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

    private void UpdateDots(int page)
    {
        for (int i = 0; i < dots.Count; i++)
        {
            Dot dot = dots[i];
            bool active = i == page;

            if (dot.layout != null)
            {
                float size = active ? 20f : 12f;

                dot.layout.preferredWidth = size;
                dot.layout.preferredHeight = size;
            }

            if (dot.image != null)
            {
                dot.image.color = active
                    ? EquipmentWireframeTheme.DotActive
                    : EquipmentWireframeTheme.DotInactive;
            }
        }
    }

    /// <summary>
    /// Точки пересоздаются при смене вкладки, и OnEnable панели
    /// в редакторе тоже отрабатывает. Destroy в режиме правки
    /// бросает ошибку, поэтому способ удаления зависит от режима.
    /// </summary>
    private static void Kill(GameObject target)
    {
        if (target == null)
            return;

        if (Application.isPlaying)
            Destroy(target);
        else
            DestroyImmediate(target);
    }

    // =====================================================
    // PAGINATION
    // =====================================================

    private void PreviousPage()
    {
        GoToPage(CurrentPage() - 1);
    }

    private void NextPage()
    {
        GoToPage(CurrentPage() + 1);
    }

    private int CurrentPage()
    {
        return pages[(int)activeTab];
    }

    private void GoToPage(int value)
    {
        int tabIndex = (int)activeTab;
        int pageCount = PageCount(CountOf(tabIndex));

        // Пустой список — это одна фиктивная страница, иначе clamp
        // с max меньше min вернул бы -1.
        int clamped = pageCount > 0
            ? Mathf.Clamp(value, 0, pageCount - 1)
            : 0;

        if (clamped == pages[tabIndex])
            return;

        pages[tabIndex] = clamped;

        PlayUiClick();
        Refresh();
    }

    private int PageCount(int itemCount)
    {
        if (itemCount <= 0)
            return 0;

        return Mathf.CeilToInt(itemCount / (float)CardsPerPage);
    }

    // =====================================================
    // COLLECTIONS
    // =====================================================

    private int CountOf(int tabIndex)
    {
        switch ((TabKind)tabIndex)
        {
            case TabKind.Weapons:
                return weapons.Count;

            case TabKind.Abilities:
                return abilities.Count;

            default:
                return clothing.Count;
        }
    }

    private EquipmentCardData CardAt(int tabIndex, int index)
    {
        // Отрицательный индекс тоже вне диапазона, а List бросает
        // на нём исключение, поэтому проверяем обе границы.
        if (index < 0)
            return null;

        switch ((TabKind)tabIndex)
        {
            case TabKind.Weapons:
                return index < weapons.Count
                    ? weapons[index].card
                    : null;

            case TabKind.Abilities:
                return index < abilities.Count
                    ? abilities[index].card
                    : null;

            default:
                return index < clothing.Count
                    ? clothing[index].card
                    : null;
        }
    }

    /// <summary>
    /// Перечитывает списки предметов из UpgradeManager. Списки
    /// меняются только в редакторе (когда добавляют ассеты), поэтому
    /// пересборка происходит один раз за жизнь панели.
    /// </summary>
    private void EnsureItems()
    {
        if (weapons.Count > 0
            || abilities.Count > 0
            || clothing.Count > 0)
        {
            return;
        }

        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
        {
            Debug.LogError(
                "[Equipment] На сцене нет UpgradeManager, поэтому список "
                + "предметов пуст и вкладка останется пустой.\n" +
                "Проверь, что объект UpgradeManager есть в сцене и "
                + "не выключен.",
                this
            );

            return;
        }

        LoadWeapons(upgradeManager);
        LoadAbilities(upgradeManager);
        LoadClothing(upgradeManager);
    }

    private void LoadWeapons(UpgradeManager upgradeManager)
    {
        foreach (WeaponData data in upgradeManager.GetAvailableWeapons())
        {
            if (data == null)
                continue;

            weapons.Add(
                new WeaponEntry
                {
                    data = data,
                    card = BuildWeaponCard(data)
                }
            );
        }
    }

    private void LoadAbilities(UpgradeManager upgradeManager)
    {
        foreach (AbilityData data in upgradeManager.GetAvailableAbilities())
        {
            if (data == null)
                continue;

            abilities.Add(
                new AbilityEntry
                {
                    data = data,
                    card = BuildAbilityCard(data)
                }
            );
        }
    }

    private void LoadClothing(UpgradeManager upgradeManager)
    {
        foreach (ClothingData data in upgradeManager.GetAvailableClothing())
        {
            if (data == null)
                continue;

            clothing.Add(
                new ClothingEntry
                {
                    data = data,
                    card = BuildClothingCard(data)
                }
            );
        }
    }

    private EquipmentCardData BuildWeaponCard(WeaponData data)
    {
        return new EquipmentCardData
        {
            Weapon = data,
            Name = data.LocalizedName,
            TypeLabel = WeaponTypeLabel(data.WeaponType),
            RarityLabel = RarityLabel(data.Rarity),
            Stats = FormatWeapon(data),
            Price = data.Price,
            UnlockLevel = data.UnlockLevel,
            Accent = EquipmentWireframeTheme.GetRarityColor(data.Rarity),
            Toggles = false
        };
    }

    private EquipmentCardData BuildAbilityCard(AbilityData data)
    {
        return new EquipmentCardData
        {
            Ability = data,
            Name = data.LocalizedName,
            TypeLabel = AbilityKindLabel(data.Kind),
            RarityLabel = string.Empty,
            Stats = string.IsNullOrEmpty(data.LocalizedStats)
                ? data.LocalizedDescription
                : data.LocalizedStats,
            Price = data.Price,
            UnlockLevel = data.UnlockLevel,
            Accent = EquipmentWireframeTheme.AbilityAccent,
            Toggles = true
        };
    }

    private EquipmentCardData BuildClothingCard(ClothingData data)
    {
        return new EquipmentCardData
        {
            Clothing = data,
            Name = data.LocalizedName,
            TypeLabel = Lang.Get("eq.clothing_type"),
            RarityLabel = string.Empty,
            Stats = data.LocalizedDescription,
            Price = data.Price,
            UnlockLevel = data.UnlockLevel,
            Accent = EquipmentWireframeTheme.ClothingAccent,
            Toggles = false
        };
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    private void OnActionClicked(EquipmentCardView view)
    {
        if (view == null)
            return;

        int tabIndex = (int)activeTab;
        int start = CurrentPage() * CardsPerPage;

        for (int i = 0; i < cardSlots.Count; i++)
        {
            if (cardSlots[i] != view)
                continue;

            Handle(tabIndex, start + i);

            return;
        }
    }

    private void Handle(int tabIndex, int index)
    {
        switch ((TabKind)tabIndex)
        {
            case TabKind.Weapons:
                if (index < weapons.Count)
                    HandleWeapon(weapons[index].data);

                break;

            case TabKind.Abilities:
                if (index < abilities.Count)
                    HandleAbility(abilities[index].data);

                break;

            default:
                if (index < clothing.Count)
                    HandleClothing(clothing[index].data);

                break;
        }
    }

    private void HandleWeapon(WeaponData data)
    {
        if (data == null)
            return;

        if (!EquipmentManager.IsUnlocked(data))
        {
            PlayUiClick();
            Refresh();

            return;
        }

        if (!EquipmentManager.IsOwned(data))
        {
            bool bought = EquipmentManager.TryPurchase(data);

            if (bought)
                PlayPurchaseSound();
            else
                PlayUiClick();

            Refresh();

            return;
        }

        if (EquipmentManager.TryEquip(data))
            PlayEquipSound();
        else
            PlayUiClick();

        Refresh();
    }

    private void HandleAbility(AbilityData data)
    {
        if (data == null)
            return;

        if (!EquipmentManager.IsUnlocked(data))
        {
            PlayUiClick();
            Refresh();

            return;
        }

        if (!EquipmentManager.IsOwned(data))
        {
            bool bought = EquipmentManager.TryPurchase(data);

            if (bought)
                PlayPurchaseSound();
            else
                PlayUiClick();

            Refresh();

            return;
        }

        if (EquipmentManager.TryToggleAbility(data))
            PlayEquipSound();
        else
            PlayUiClick();

        Refresh();
    }

    private void HandleClothing(ClothingData data)
    {
        if (data == null)
            return;

        if (!EquipmentManager.IsUnlocked(data))
        {
            PlayUiClick();
            Refresh();

            return;
        }

        if (!EquipmentManager.IsOwned(data))
        {
            bool bought = EquipmentManager.TryPurchase(data);

            if (bought)
                PlayPurchaseSound();
            else
                PlayUiClick();

            Refresh();

            return;
        }

        if (EquipmentManager.TryEquip(data))
            PlayEquipSound();
        else
            PlayUiClick();

        Refresh();
    }

    // =====================================================
    // FORMATTING
    // =====================================================

    private static string WeaponTypeLabel(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Rifle:
                return Lang.Get("eq.type_rifle");

            case WeaponType.Shotgun:
                return Lang.Get("eq.type_shotgun");

            case WeaponType.SMG:
                return Lang.Get("eq.type_smg");

            default:
                return type.ToString();
        }
    }

    private static string AbilityKindLabel(AbilityKind kind)
    {
        switch (kind)
        {
            case AbilityKind.Bomb:
                return Lang.Get("eq.ability_bomb");

            case AbilityKind.Shield:
                return Lang.Get("eq.ability_shield");

            default:
                return kind.ToString();
        }
    }

    private static string RarityLabel(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common:
                return Lang.Get("rarity.common");

            case Rarity.Uncommon:
                return Lang.Get("rarity.uncommon");

            case Rarity.Rare:
                return Lang.Get("rarity.rare");

            case Rarity.Epic:
                return Lang.Get("rarity.epic");

            case Rarity.Legendary:
                return Lang.Get("rarity.legendary");

            default:
                return rarity.ToString();
        }
    }

    /// <summary>
    /// Характеристики оружия в виде строк, а не одной простыни:
    /// карточка 340 пикселей шириной, длинная строка не влезет.
    /// </summary>
    private static string FormatWeapon(WeaponData data)
    {
        var lines = new List<string>
        {
            Lang.Get("eq.stat_damage", Num(data.Damage)),
            Lang.Get("eq.stat_firerate", Num(data.FireRate))
        };

        if (data.ProjectileCount > 1)
        {
            lines.Add(
                Lang.Get("eq.stat_projectiles", data.ProjectileCount)
            );
        }

        if (data.PierceCount > 0)
        {
            lines.Add(
                Lang.Get("eq.stat_pierce", data.PierceCount)
            );
        }

        if (data.IsBurstWeapon)
        {
            lines.Add(
                Lang.Get("eq.stat_burst", data.ShotsPerBurst)
            );
        }

        if (data.CriticalChanceBonus > 0f)
        {
            lines.Add(
                Lang.Get(
                    "eq.stat_crit",
                    Num(data.CriticalChanceBonus * 100f)
                )
            );
        }

        if (data.ScoreBonusPercent > 0f)
        {
            lines.Add(
                Lang.Get("eq.stat_score", Num(data.ScoreBonusPercent))
            );
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Число без локальных разделителей: точка в любом языке.
    /// </summary>
    private static string Num(float value)
    {
        return value.ToString(
            "0.#",
            System.Globalization.CultureInfo.InvariantCulture
        );
    }

    // =====================================================
    // SOURCES
    // =====================================================

    private static int CurrentCoins()
    {
        return XpManager.Instance != null
            ? XpManager.Instance.GlobalCoins
            : 0;
    }

    private static int CurrentLevel()
    {
        return XpManager.Instance != null
            ? XpManager.Instance.GetPlayerLevel()
            : 1;
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

    private void PlayEquipSound()
    {
        SFXLibrary sfx = GetSfx();

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.WeaponSwitch,
                priority: SfxPriority.Medium
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
}
