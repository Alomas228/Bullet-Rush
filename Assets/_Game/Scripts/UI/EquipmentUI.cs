using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Панель «Снаряжение» в главном меню с тремя вкладками:
///  - Магазин      — все оружия проекта (UpgradeManager.availableWeapons);
///  - Способности  — все способности (UpgradeManager.availableAbilities);
///  - Одежда       — вся одежда (UpgradeManager.availableClothing).
///
/// Карточки генерируются из одного шаблона на вкладку (ScrollRect/Content).
/// Состояния карточки:
///  - заблокировано по уровню        -> «Заблокировано», кнопка неактивна
///  - уровень достигнут, не куплено  -> «Купить N», покупка за монеты
///  - куплено, не снаряжено          -> «Снарядить»
///  - куплено и снаряжено            -> «Снаряжено» (способности: «Снять»)
/// </summary>
public class EquipmentUI : MonoBehaviour
{
    private enum TabKind
    {
        Shop = 0,
        Abilities = 1,
        Clothing = 2
    }

    private abstract class RuntimeCard
    {
        public GameObject root;

        public Image background;
        public TMP_Text nameText;
        public TMP_Text typeText;
        public TMP_Text statsText;
        public TMP_Text statusText;
        public Button actionButton;
        public Image actionButtonImage;
        public TMP_Text actionButtonText;
    }

    private sealed class WeaponCard : RuntimeCard
    {
        public WeaponData data;
    }

    private sealed class AbilityCard : RuntimeCard
    {
        public AbilityData data;
    }

    private sealed class ClothingCard : RuntimeCard
    {
        public ClothingData data;
    }

    [Header("Panel")]
    [SerializeField] private GameObject equipmentPanel;

    [Header("Header")]
    [SerializeField] private TMP_Text playerLevelText;
    [SerializeField] private TMP_Text playerCoinsText;
    [SerializeField] private TMP_Text equippedText;

    [Header("Tabs")]
    [Tooltip("Кнопки вкладок в порядке: Магазин, Способности, Одежда.")]
    [SerializeField] private Button[] tabButtons;

    [Tooltip("Корни областей прокрутки вкладок (тот же порядок).")]
    [SerializeField] private RectTransform[] tabContentRoots;

    [Tooltip("Шаблоны карточек каждой вкладки (тот же порядок).")]
    [SerializeField] private GameObject[] cardTemplates;

    [Header("Colors")]
    [SerializeField] private Color normalCardColor =
        new Color(0.16f, 0.16f, 0.20f, 1f);
    [SerializeField] private Color equippedCardColor =
        new Color(0.18f, 0.38f, 0.22f, 1f);
    [SerializeField] private Color activeButtonColor =
        new Color(0.25f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color inactiveButtonColor =
        new Color(0.35f, 0.35f, 0.40f, 0.55f);
    [SerializeField] private Color activeTabColor =
        new Color(0.28f, 0.55f, 0.30f, 1f);
    [SerializeField] private Color inactiveTabColor =
        new Color(0.16f, 0.16f, 0.22f, 0.9f);

    private readonly List<WeaponCard> weaponCards =
        new List<WeaponCard>();

    private readonly List<AbilityCard> abilityCards =
        new List<AbilityCard>();

    private readonly List<ClothingCard> clothingCards =
        new List<ClothingCard>();

    private bool built;
    private bool subscribed;

    private TabKind activeTab = TabKind.Shop;

    private void OnEnable()
    {
        EnsureTabListeners();

        Refresh();
    }

    private void OnDisable()
    {
        RemoveTabListeners();
    }

    public void Refresh()
    {
        if (!built)
            BuildCards();

        RefreshHeader();

        ShowTab(activeTab);
    }

    // =====================================================
    // TABS
    // =====================================================

    private void EnsureTabListeners()
    {
        if (subscribed)
            return;

        subscribed = true;

        if (tabButtons == null)
            return;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            Button button = tabButtons[i];

            if (button == null)
                continue;

            var captured = i;

            button.onClick.AddListener(
                () => OnTabClicked((TabKind)captured)
            );
        }
    }

    private void RemoveTabListeners()
    {
        if (!subscribed)
            return;

        subscribed = false;

        if (tabButtons == null)
            return;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            if (tabButtons[i] != null)
                tabButtons[i].onClick.RemoveAllListeners();
        }
    }

    private void OnTabClicked(TabKind tab)
    {
        if (activeTab == tab)
        {
            Refresh();
            return;
        }

        activeTab = tab;

        ShowTab(tab);
    }

    private void ShowTab(TabKind tab)
    {
        int index = (int)tab;

        if (tabContentRoots != null)
        {
            for (int i = 0; i < tabContentRoots.Length; i++)
            {
                if (tabContentRoots[i] == null)
                    continue;

                tabContentRoots[i].gameObject.SetActive(i == index);
            }
        }

        if (tabButtons != null)
        {
            for (int i = 0; i < tabButtons.Length; i++)
            {
                if (tabButtons[i] == null)
                    continue;

                Image image =
                    tabButtons[i].GetComponent<Image>();

                if (image != null)
                {
                    image.color =
                        i == index
                            ? activeTabColor
                            : inactiveTabColor;
                }
            }
        }

        if (index == (int)TabKind.Shop)
            RefreshShopCards();
        else if (index == (int)TabKind.Abilities)
            RefreshAbilityCards();
        else if (index == (int)TabKind.Clothing)
            RefreshClothingCards();

        RebuildContentLayout(GetTabContent(tab));
    }

    private RectTransform GetTabContent(TabKind tab)
    {
        if (tabContentRoots == null)
            return null;

        int index = (int)tab;

        if (index < 0 || index >= tabContentRoots.Length)
            return null;

        RectTransform root = tabContentRoots[index];

        if (root == null)
            return null;

        Transform content =
            root.Find("Viewport/Content");

        return content as RectTransform;
    }

    private void RebuildContentLayout(RectTransform content)
    {
        if (content == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        Canvas.ForceUpdateCanvases();
    }

    // =====================================================
    // BUILD
    // =====================================================

    private void BuildCards()
    {
        built = true;

        if (cardTemplates == null || cardTemplates.Length == 0)
        {
            Debug.LogWarning(
                "EquipmentUI: cardTemplates не назначены.",
                this
            );

            return;
        }

        UpgradeManager upgradeManager =
            UpgradeManager.Instance;

        if (upgradeManager == null)
        {
            Debug.LogWarning(
                "EquipmentUI: UpgradeManager.Instance is null.",
                this
            );

            return;
        }

        BuildWeaponCards(upgradeManager);
        BuildAbilityCards(upgradeManager);
        BuildClothingCards(upgradeManager);
    }

    private void BuildWeaponCards(UpgradeManager upgradeManager)
    {
        GameObject template =
            GetCardTemplate(TabKind.Shop);

        RectTransform content =
            template != null
                ? template.transform.parent as RectTransform
                : GetContentRoot(TabKind.Shop);

        template?.SetActive(false);

        if (content == null)
        {
            Debug.LogWarning(
                "EquipmentUI: нет Content для вкладки «Магазин».",
                this
            );

            return;
        }

        foreach (WeaponData weapon in
                 upgradeManager.GetAvailableWeapons())
        {
            if (weapon == null)
                continue;

            GameObject cardObject =
                template != null
                    ? Instantiate(template, content)
                    : CreateFallbackCard(content, "WeaponCard");

            cardObject.name = $"Card_{weapon.WeaponName}";

            WeaponCard card = new WeaponCard
            {
                root = cardObject,
                data = weapon,
                background = cardObject.GetComponent<Image>(),
                nameText = FindText(cardObject, "Name"),
                typeText = FindText(cardObject, "Type"),
                statsText = FindText(cardObject, "Stats"),
                statusText = FindText(cardObject, "Status")
            };

            BindActionButton(
                card,
                cardObject,
                () => OnActionClicked(card)
            );

            cardObject.SetActive(true);

            weaponCards.Add(card);
        }

        RebuildContentLayout(content);
    }

    private void BuildAbilityCards(UpgradeManager upgradeManager)
    {
        GameObject template =
            GetCardTemplate(TabKind.Abilities);

        RectTransform content =
            template != null
                ? template.transform.parent as RectTransform
                : GetContentRoot(TabKind.Abilities);

        template?.SetActive(false);

        if (content == null)
        {
            Debug.LogWarning(
                "EquipmentUI: нет Content для вкладки «Способности».",
                this
            );

            return;
        }

        foreach (AbilityData ability in
                 upgradeManager.GetAvailableAbilities())
        {
            if (ability == null)
                continue;

            GameObject cardObject =
                template != null
                    ? Instantiate(template, content)
                    : CreateFallbackCard(content, "AbilityCard");

            cardObject.name = $"Card_{ability.AbilityName}";

            AbilityCard card = new AbilityCard
            {
                root = cardObject,
                data = ability,
                background = cardObject.GetComponent<Image>(),
                nameText = FindText(cardObject, "Name"),
                typeText = FindText(cardObject, "Type"),
                statsText = FindText(cardObject, "Stats"),
                statusText = FindText(cardObject, "Status")
            };

            BindActionButton(
                card,
                cardObject,
                () => OnActionClicked(card)
            );

            cardObject.SetActive(true);

            abilityCards.Add(card);
        }

        RebuildContentLayout(content);
    }

    private void BuildClothingCards(UpgradeManager upgradeManager)
    {
        GameObject template =
            GetCardTemplate(TabKind.Clothing);

        RectTransform content =
            template != null
                ? template.transform.parent as RectTransform
                : GetContentRoot(TabKind.Clothing);

        template?.SetActive(false);

        if (content == null)
        {
            Debug.LogWarning(
                "EquipmentUI: нет Content для вкладки «Одежда».",
                this
            );

            return;
        }

        foreach (ClothingData clothing in
                 upgradeManager.GetAvailableClothing())
        {
            if (clothing == null)
                continue;

            GameObject cardObject =
                template != null
                    ? Instantiate(template, content)
                    : CreateFallbackCard(content, "ClothingCard");

            cardObject.name = $"Card_{clothing.ClothingName}";

            ClothingCard card = new ClothingCard
            {
                root = cardObject,
                data = clothing,
                background = cardObject.GetComponent<Image>(),
                nameText = FindText(cardObject, "Name"),
                typeText = FindText(cardObject, "Type"),
                statsText = FindText(cardObject, "Stats"),
                statusText = FindText(cardObject, "Status")
            };

            BindActionButton(
                card,
                cardObject,
                () => OnActionClicked(card)
            );

            cardObject.SetActive(true);

            clothingCards.Add(card);
        }

        RebuildContentLayout(content);
    }

    private void BindActionButton(
        RuntimeCard card,
        GameObject cardObject,
        UnityEngine.Events.UnityAction action)
    {
        Transform actionTransform =
            cardObject.transform.Find("ActionButton");

        if (actionTransform == null)
            return;

        card.actionButton =
            actionTransform.GetComponent<Button>();

        card.actionButtonImage =
            actionTransform.GetComponent<Image>();

        card.actionButtonText =
            FindText(actionTransform.gameObject, "Text");

        if (card.actionButton != null)
            card.actionButton.onClick.AddListener(action);
    }

    private GameObject GetCardTemplate(TabKind tab)
    {
        if (cardTemplates == null)
            return null;

        int index = (int)tab;

        if (index < 0 || index >= cardTemplates.Length)
            return null;

        return cardTemplates[index];
    }

    private RectTransform GetContentRoot(TabKind tab)
    {
        if (tabContentRoots == null)
            return null;

        int index = (int)tab;

        if (index < 0 || index >= tabContentRoots.Length)
            return null;

        RectTransform root = tabContentRoots[index];

        return root != null
            ? root.Find("Viewport")?.Find("Content") as RectTransform
            : null;
    }

    /// <summary>Создаёт простую карточку без шаблона (аварийный путь при сборке).</summary>
    private static GameObject CreateFallbackCard(
        Transform content,
        string name)
    {
        var rect = new GameObject(
            name,
            typeof(RectTransform)
        );

        rect.transform.SetParent(content, false);

        return rect;
    }

    private static TMP_Text FindText(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);

        return child != null
            ? child.GetComponent<TMP_Text>()
            : null;
    }

    // =====================================================
    // REFRESH
    // =====================================================

    private void RefreshHeader()
    {
        XpManager xp = XpManager.Instance;

        int level =
            xp != null ? xp.GetPlayerLevel() : 1;

        int coins =
            xp != null ? xp.GlobalCoins : 0;

        if (playerLevelText != null)
            playerLevelText.text = $"LEVEL {level}";

        if (playerCoinsText != null)
            playerCoinsText.text = $"МОНЕТЫ: {coins}";

        if (equippedText != null)
            equippedText.text = FormatEquippedSummary();
    }

    private string FormatEquippedSummary()
    {
        string weapon =
            EquipmentManager.EquippedWeaponName;

        string weaponLabel =
            string.IsNullOrEmpty(weapon)
                ? "—"
                : weapon;

        List<string> abilities =
            new List<string>(
                EquipmentManager.EquippedAbilityNames
            );

        string abilityLabel =
            abilities.Count > 0
                ? string.Join(", ", abilities)
                : "—";

        string clothing =
            EquipmentManager.EquippedClothingName;

        string clothingLabel =
            string.IsNullOrEmpty(clothing)
                ? "—"
                : clothing;

        return
            $"Оружие: {weaponLabel}\n" +
            $"Способности: {abilityLabel}\n" +
            $"Образ: {clothingLabel}";
    }

    private void RefreshShopCards()
    {
        foreach (WeaponCard card in weaponCards)
        {
            WeaponData weapon = card.data;

            if (weapon == null)
                continue;

            bool unlocked = EquipmentManager.IsUnlocked(weapon);
            bool owned = EquipmentManager.IsOwned(weapon);
            bool equipped = EquipmentManager.IsEquipped(weapon);

            SetCardTexts(
                card,
                weapon.WeaponName,
                TypeLabel(weapon.WeaponType),
                FormatStats(weapon)
            );

            if (card.background != null)
            {
                card.background.color =
                    equipped
                        ? equippedCardColor
                        : normalCardColor;
            }

            if (!unlocked)
            {
                SetCardState(
                    card,
                    $"УРОВЕНЬ {weapon.UnlockLevel}",
                    "Заблокировано",
                    false
                );
            }
            else if (!owned)
            {
                SetCardState(
                    card,
                    $"{weapon.Price} монет",
                    "Купить",
                    true
                );
            }
            else if (equipped)
            {
                SetCardState(
                    card,
                    "Снаряжено",
                    "Снаряжено",
                    false
                );
            }
            else
            {
                SetCardState(
                    card,
                    "Куплено",
                    "Снарядить",
                    true
                );
            }
        }
    }

    private void RefreshAbilityCards()
    {
        foreach (AbilityCard card in abilityCards)
        {
            AbilityData ability = card.data;

            if (ability == null)
                continue;

            bool unlocked = EquipmentManager.IsUnlocked(ability);
            bool owned = EquipmentManager.IsOwned(ability);
            bool equipped = EquipmentManager.IsEquipped(ability);

            SetCardTexts(
                card,
                ability.AbilityName,
                AbilityKindLabel(ability.Kind),
                FormatAbility(ability)
            );

            if (card.background != null)
            {
                card.background.color =
                    equipped
                        ? equippedCardColor
                        : normalCardColor;
            }

            if (!unlocked)
            {
                SetCardState(
                    card,
                    $"УРОВЕНЬ {ability.UnlockLevel}",
                    "Заблокировано",
                    false
                );
            }
            else if (!owned)
            {
                SetCardState(
                    card,
                    $"{ability.Price} монет",
                    "Купить",
                    true
                );
            }
            else if (equipped)
            {
                SetCardState(
                    card,
                    "Снаряжена",
                    "Снять",
                    true
                );
            }
            else
            {
                SetCardState(
                    card,
                    "Куплена",
                    "Снарядить",
                    true
                );
            }
        }
    }

    private void RefreshClothingCards()
    {
        foreach (ClothingCard card in clothingCards)
        {
            ClothingData clothing = card.data;

            if (clothing == null)
                continue;

            bool unlocked = EquipmentManager.IsUnlocked(clothing);
            bool owned = EquipmentManager.IsOwned(clothing);
            bool equipped = EquipmentManager.IsEquipped(clothing);

            SetCardTexts(
                card,
                clothing.ClothingName,
                "Одежда",
                FormatClothing(clothing)
            );

            if (card.background != null)
            {
                card.background.color =
                    equipped
                        ? equippedCardColor
                        : normalCardColor;
            }

            if (!unlocked)
            {
                SetCardState(
                    card,
                    $"УРОВЕНЬ {clothing.UnlockLevel}",
                    "Заблокировано",
                    false
                );
            }
            else if (!owned)
            {
                SetCardState(
                    card,
                    $"{clothing.Price} монет",
                    "Купить",
                    true
                );
            }
            else if (equipped)
            {
                SetCardState(
                    card,
                    "Надета",
                    "Надета",
                    false
                );
            }
            else
            {
                SetCardState(
                    card,
                    "Куплена",
                    "Надеть",
                    true
                );
            }
        }
    }

    private static void SetCardTexts(
        RuntimeCard card,
        string name,
        string type,
        string stats)
    {
        if (card.nameText != null)
            card.nameText.text = name;

        if (card.typeText != null)
            card.typeText.text = type;

        if (card.statsText != null)
            card.statsText.text = stats;
    }

    private void SetCardState(
        RuntimeCard card,
        string statusText,
        string actionText,
        bool interactable)
    {
        if (card.statusText != null)
            card.statusText.text = statusText;

        if (card.actionButtonText != null)
            card.actionButtonText.text = actionText;

        if (card.actionButton != null)
            card.actionButton.interactable = interactable;

        if (card.actionButtonImage != null)
        {
            card.actionButtonImage.color =
                interactable
                    ? activeButtonColor
                    : inactiveButtonColor;
        }
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    private void OnActionClicked(RuntimeCard card)
    {
        switch (card)
        {
            case WeaponCard weaponCard:
                HandleWeaponAction(weaponCard);
                break;

            case AbilityCard abilityCard:
                HandleAbilityAction(abilityCard);
                break;

            case ClothingCard clothingCard:
                HandleClothingAction(clothingCard);
                break;
        }
    }

    private void HandleWeaponAction(WeaponCard card)
    {
        WeaponData weapon = card.data;

        if (weapon == null)
            return;

        if (!EquipmentManager.IsUnlocked(weapon))
        {
            PlayUiClick();
            return;
        }

        if (!EquipmentManager.IsOwned(weapon))
        {
            bool bought = EquipmentManager.TryPurchase(weapon);

            if (bought) PlayPurchaseSound();
            else PlayUiClick();

            Refresh();
            return;
        }

        if (!EquipmentManager.IsEquipped(weapon))
        {
            bool equipped = EquipmentManager.TryEquip(weapon);

            if (equipped) PlayEquipSound();
            else PlayUiClick();

            Refresh();
        }
    }

    private void HandleAbilityAction(AbilityCard card)
    {
        AbilityData ability = card.data;

        if (ability == null)
            return;

        if (!EquipmentManager.IsUnlocked(ability))
        {
            PlayUiClick();
            return;
        }

        if (!EquipmentManager.IsOwned(ability))
        {
            bool bought = EquipmentManager.TryPurchase(ability);

            if (bought) PlayPurchaseSound();
            else PlayUiClick();

            Refresh();
            return;
        }

        bool toggled = EquipmentManager.TryToggleAbility(ability);

        if (toggled) PlayEquipSound();
        else PlayUiClick();

        Refresh();
    }

    private void HandleClothingAction(ClothingCard card)
    {
        ClothingData clothing = card.data;

        if (clothing == null)
            return;

        if (!EquipmentManager.IsUnlocked(clothing))
        {
            PlayUiClick();
            return;
        }

        if (!EquipmentManager.IsOwned(clothing))
        {
            bool bought = EquipmentManager.TryPurchase(clothing);

            if (bought) PlayPurchaseSound();
            else PlayUiClick();

            Refresh();
            return;
        }

        if (!EquipmentManager.IsEquipped(clothing))
        {
            bool equipped = EquipmentManager.TryEquip(clothing);

            if (equipped) PlayEquipSound();
            else PlayUiClick();

            Refresh();
        }
    }

    // =====================================================
    // FORMATTING
    // =====================================================

    private static string TypeLabel(WeaponType type)
    {
        switch (type)
        {
            case WeaponType.Rifle:
                return "Винтовка";

            case WeaponType.Shotgun:
                return "Дробовик";

            case WeaponType.SMG:
                return "Пистолет-пулемёт";

            default:
                return type.ToString();
        }
    }

    private static string AbilityKindLabel(AbilityKind kind)
    {
        switch (kind)
        {
            case AbilityKind.Bomb:
                return "Бомба [E]";

            case AbilityKind.Shield:
                return "Щит [Q]";

            default:
                return kind.ToString();
        }
    }

    private static string RarityLabel(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Common:
                return "Обычное";

            case Rarity.Uncommon:
                return "Необычное";

            case Rarity.Rare:
                return "Редкое";

            case Rarity.Epic:
                return "Эпическое";

            case Rarity.Legendary:
                return "Легендарное";

            default:
                return rarity.ToString();
        }
    }

    private static string FormatStats(WeaponData weapon)
    {
        return
            $"Урон: {weapon.Damage:0.#}   •  " +
            $"Скорострельность: {weapon.FireRate:0.#}/с   •  " +
            $"Снарядов: {weapon.ProjectileCount}   •  " +
            $"Пробитие: {weapon.PierceCount}   •  " +
            $"Редкость: {RarityLabel(weapon.Rarity)}";
    }

    private static string FormatAbility(AbilityData ability)
    {
        if (!string.IsNullOrEmpty(ability.Stats))
            return ability.Stats;

        return ability.Description;
    }

    private static string FormatClothing(ClothingData clothing)
    {
        return clothing.Description;
    }

    // =====================================================
    // SOUND
    // =====================================================

    private void PlayPurchaseSound()
    {
        SFXLibrary sfx = GetSfx();

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.UpgradePick);
    }

    private void PlayEquipSound()
    {
        SFXLibrary sfx = GetSfx();

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.WeaponSwitch);
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