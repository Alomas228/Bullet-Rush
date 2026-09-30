using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Отображение одной карточки предмета на экране «Снаряжение».
///
/// Композиция повторяет карточку «Улучшений» (рамка, тень,
/// вертикальный список строк, акцентная полоса действий), но
/// наполнение другое: вместо уровня параметра и полосы прогресса
/// здесь редкость предмета и его состояние — куплен, надет,
/// заблокирован по уровню игрока.
///
/// Данные не лежат в полях: их передаёт EquipmentUI через
/// <see cref="Refresh"/> на каждый элемент списка.
/// </summary>
public class EquipmentCardView : MonoBehaviour
{
    [Header("Frame")]
    [SerializeField] private Image surfaceImage;
    [SerializeField] private Image frameImage;
    [SerializeField] private Image accentLine;
    [Tooltip("Кнопка на всей карточке: тот же эффект, что у строки действия.")]
    [SerializeField] private Button cardButton;

    [Header("Icon")]
    [SerializeField] private Image iconWash;
    [SerializeField] private Image iconDisc;
    [SerializeField] private TMP_Text typeText;

    [Header("Texts")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text rarityText;
    [SerializeField] private RectTransform rarityRow;
    [SerializeField] private TMP_Text statsText;

    [Header("State")]
    [SerializeField] private Image stateImage;
    [SerializeField] private TMP_Text stateText;

    [Header("Action")]
    [SerializeField] private Image actionImage;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Button actionButton;
    [SerializeField] private Image priceImage;
    [SerializeField] private TMP_Text priceText;

    [Tooltip("Ставится на true, когда на карточку повешен обработчик.")]
    [SerializeField] private bool wired;

    private static readonly Color PriceAffordable =
        EquipmentWireframeTheme.White(0.22f);

    private static readonly Color PriceBlocked =
        EquipmentWireframeTheme.White(0.14f);

    public Button CardButton => cardButton;

    public Button ActionButton => actionButton;

    public bool Wired => wired;

    /// <summary>Помечает карточку как готовую к работе.</summary>
    public void MarkWired()
    {
        wired = true;
    }

    /// <summary>
    /// Перерисовывает карточку под текущее состояние предмета.
    /// coins нужен, чтобы кнопка покупки гасла при нехватке монет.
    /// </summary>
    public void Refresh(EquipmentCardData data, int coins)
    {
        if (data == null)
            return;

        Color accent = data.Accent;

        if (accentLine != null)
            accentLine.color = accent;

        if (iconWash != null)
            iconWash.color = EquipmentWireframeTheme.Wash(accent, 0.16f);

        if (iconDisc != null)
            iconDisc.color = EquipmentWireframeTheme.Wash(accent, 0.55f);

        if (typeText != null)
        {
            typeText.text = data.TypeLabel;
            typeText.color = accent;
        }

        if (nameText != null)
            nameText.text = data.Name;

        bool hasRarity = !string.IsNullOrEmpty(data.RarityLabel);

        if (rarityRow != null)
            rarityRow.gameObject.SetActive(hasRarity);

        if (rarityText != null)
        {
            rarityText.text = data.RarityLabel;
            rarityText.color = accent;
        }

        if (statsText != null)
            statsText.text = data.Stats;

        if (stateText != null)
        {
            stateText.text = data.State;
            stateText.color = data.Unlocked
                ? EquipmentWireframeTheme.TextSecondary
                : EquipmentWireframeTheme.TextMuted;
        }

        if (stateImage != null)
        {
            stateImage.color = data.Unlocked
                ? EquipmentWireframeTheme.Track
                : EquipmentWireframeTheme.SurfaceMuted;
        }

        ApplyAction(data, coins);
    }

    private void ApplyAction(EquipmentCardData data, int coins)
    {
        string label;
        bool interactable;

        if (!data.Unlocked)
        {
            label = Lang.Get("eq.action_locked");
            interactable = false;
        }
        else if (!data.Owned)
        {
            label = Lang.Get("eq.action_buy");
            interactable = coins >= data.Price;
        }
        else if (data.Toggles)
        {
            label = data.Equipped
                ? Lang.Get("eq.action_unequip")
                : Lang.Get("eq.action_equip");
            interactable = true;
        }
        else
        {
            label = data.Equipped
                ? Lang.Get("eq.state_equipped")
                : Lang.Get("eq.action_equip");
            interactable = !data.Equipped;
        }

        if (actionLabel != null)
            actionLabel.text = label;

        if (actionButton != null)
            actionButton.interactable = interactable;

        if (actionImage != null)
        {
            actionImage.color = interactable
                ? EquipmentWireframeTheme.Action
                : EquipmentWireframeTheme.ActionDisabled;
        }

        ApplyPrice(data);
    }

    /// <summary>
    /// Бейдж с ценой. У заблокированного предмета вместо цены
    /// показывается уровень разблокировки — так игрок понимает,
    /// чего ему не хватает.
    /// </summary>
    private void ApplyPrice(EquipmentCardData data)
    {
        string text;
        Color background;

        if (!data.Unlocked)
        {
            text = Lang.Get("eq.level_chip", data.UnlockLevel);
            background = PriceBlocked;
        }
        else if (!data.Owned)
        {
            text = data.Price.ToString();
            background = data.Unlocked
                ? PriceAffordable
                : PriceBlocked;
        }
        else
        {
            text = Lang.Get("upg.dash_marker");
            background = PriceBlocked;
        }

        if (priceText != null)
            priceText.text = text;

        if (priceImage != null)
            priceImage.color = background;
    }
}
