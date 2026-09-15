using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Панель «Снаряжение» в главном меню. Показывает все оружия проекта
/// (из UpgradeManager.availableWeapons) в виде карточек, сгенерированных
/// из одного шаблона в ScrollRect/Grid.
///
/// Состояния карточки:
///  - заблокировано по уровню       -> «Заблокировано», кнопка неактивна
///  - уровень достигнут, не куплено -> «Купить N», покупка за монеты
///  - куплено, не снаряжено          -> «Снарядить»
///  - куплено и снаряжено             -> «Снаряжено», подсветка
/// </summary>
public class EquipmentUI : MonoBehaviour
{
    private class RuntimeCard
    {
        public GameObject root;
        public WeaponData weapon;

        public Image background;
        public TMP_Text nameText;
        public TMP_Text typeText;
        public TMP_Text statsText;
        public TMP_Text statusText;
        public Button actionButton;
        public Image actionButtonImage;
        public TMP_Text actionButtonText;
    }

    [Header("Panel")]
    [SerializeField] private GameObject equipmentPanel;

    [Header("Header")]
    [SerializeField] private TMP_Text playerLevelText;
    [SerializeField] private TMP_Text playerCoinsText;
    [SerializeField] private TMP_Text equippedText;

    [Header("Card Template")]
    [Tooltip(
        "Шаблон карточки (дочерний объект Content ScrollRect). " +
        "Дочерние объекты по именам: Name, Type, Stats, Status, ActionButton (с текстом)."
    )]
    [SerializeField] private GameObject cardTemplate;

    [Header("Colors")]
    [SerializeField] private Color normalCardColor =
        new Color(0.16f, 0.16f, 0.20f, 1f);
    [SerializeField] private Color equippedCardColor =
        new Color(0.18f, 0.38f, 0.22f, 1f);
    [SerializeField] private Color activeButtonColor =
        new Color(0.25f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color inactiveButtonColor =
        new Color(0.35f, 0.35f, 0.40f, 0.55f);

    private readonly List<RuntimeCard> cards =
        new List<RuntimeCard>();

    private bool built;

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (!built)
            BuildCards();

        RefreshHeader();
        RefreshCards();
    }

    // =========================================================
    // BUILD
    // =========================================================

    private void BuildCards()
    {
        built = true;

        if (cardTemplate == null)
        {
            Debug.LogWarning(
                "EquipmentUI: cardTemplate не назначен.",
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

        Transform content =
            cardTemplate.transform.parent;

        if (content == null)
        {
            Debug.LogWarning(
                "EquipmentUI: шаблон карточки должен быть ребёнком Content.",
                this
            );

            return;
        }

        cardTemplate.SetActive(false);

        foreach (WeaponData weapon in
                 upgradeManager.GetAvailableWeapons())
        {
            if (weapon == null)
                continue;

            GameObject cardObject =
                Instantiate(cardTemplate, content);

            cardObject.name = $"Card_{weapon.WeaponName}";

            RuntimeCard card = new RuntimeCard
            {
                root = cardObject,
                weapon = weapon,
                background = cardObject.GetComponent<Image>(),
                nameText = FindText(cardObject, "Name"),
                typeText = FindText(cardObject, "Type"),
                statsText = FindText(cardObject, "Stats"),
                statusText = FindText(cardObject, "Status")
            };

            Transform actionTransform =
                cardObject.transform.Find("ActionButton");

            if (actionTransform != null)
            {
                card.actionButton =
                    actionTransform.GetComponent<Button>();

                card.actionButtonImage =
                    actionTransform.GetComponent<Image>();

                card.actionButtonText =
                    FindText(actionTransform.gameObject, "Text");
            }

            if (card.actionButton != null)
            {
                RuntimeCard captured = card;

                card.actionButton.onClick.AddListener(
                    () => OnActionClicked(captured)
                );
            }

            cardObject.SetActive(true);

            cards.Add(card);
        }

        // Пересчитываем высоту Content (ContentSizeFitter) под новые карточки.
        if (content is RectTransform contentRect)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            Canvas.ForceUpdateCanvases();
        }
    }

    private static TMP_Text FindText(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);

        return child != null
            ? child.GetComponent<TMP_Text>()
            : null;
    }

    // =========================================================
    // REFRESH
    // =========================================================

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
        {
            string equipped =
                EquipmentManager.EquippedWeaponName;

            equippedText.text =
                string.IsNullOrEmpty(equipped)
                    ? "Снаряжение: —"
                    : $"Снаряжение: {equipped}";
        }
    }

    private void RefreshCards()
    {
        foreach (RuntimeCard card in cards)
        {
            WeaponData weapon = card.weapon;

            if (weapon == null)
                continue;

            bool unlocked = EquipmentManager.IsUnlocked(weapon);
            bool owned = EquipmentManager.IsOwned(weapon);
            bool equipped = EquipmentManager.IsEquipped(weapon);

            if (card.nameText != null)
                card.nameText.text = weapon.WeaponName;

            if (card.typeText != null)
                card.typeText.text = TypeLabel(weapon.WeaponType);

            if (card.statsText != null)
                card.statsText.text = FormatStats(weapon);

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

    // =========================================================
    // ACTIONS
    // =========================================================

    private void OnActionClicked(RuntimeCard card)
    {
        WeaponData weapon = card.weapon;

        if (weapon == null)
            return;

        if (!EquipmentManager.IsUnlocked(weapon))
        {
            PlayUiClick();
            return;
        }

        if (!EquipmentManager.IsOwned(weapon))
        {
            bool bought =
                EquipmentManager.TryPurchase(weapon);

            if (bought)
                PlayPurchaseSound();
            else
                PlayUiClick();

            Refresh();
            return;
        }

        if (!EquipmentManager.IsEquipped(weapon))
        {
            bool equipped =
                EquipmentManager.TryEquip(weapon);

            if (equipped)
                PlayEquipSound();
            else
                PlayUiClick();

            Refresh();
        }
    }

    // =========================================================
    // FORMATTING
    // =========================================================

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

    // =========================================================
    // SOUND
    // =========================================================

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