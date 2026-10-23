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

    private Image iconGlyph;

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
        ApplyUnifiedStyle(data);
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

        if (actionLabel != null)
        {
            actionLabel.color = interactable
                ? ArcadeUiPalette.BackgroundDeep
                : EquipmentWireframeTheme.TextMuted;
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
        {
            priceText.text = text;
            priceText.color = data.Unlocked && !data.Owned
                ? ArcadeUiPalette.Gold
                : EquipmentWireframeTheme.TextSecondary;
        }

        if (priceImage != null)
            priceImage.color = background;
    }

    private void ApplyUnifiedStyle(EquipmentCardData data)
    {
        Color accent = data.Accent;

        if (frameImage != null)
            frameImage.color = ArcadeUiTheme.Line;

        if (surfaceImage != null)
        {
            surfaceImage.sprite = ArcadeKitAssets.RoundRect;
            surfaceImage.type = Image.Type.Sliced;
            surfaceImage.pixelsPerUnitMultiplier = 1f;
            surfaceImage.color = ArcadeUiTheme.Panel;
        }

        if (accentLine != null)
        {
            accentLine.sprite = ArcadeKitAssets.RoundRect;
            accentLine.type = Image.Type.Sliced;
            accentLine.pixelsPerUnitMultiplier = 1f;
            accentLine.color = accent;

            RectTransform lineRect = accentLine.rectTransform;
            lineRect.sizeDelta = new Vector2(320f, 4f);
            lineRect.anchoredPosition = new Vector2(0f, -10f);
        }

        Transform art = iconWash != null
            ? iconWash.transform
            : transform.Find("CardFace/IconArea");

        if (iconWash != null)
        {
            iconWash.sprite = ArcadeKitAssets.RoundRect;
            iconWash.type = Image.Type.Sliced;
            iconWash.pixelsPerUnitMultiplier = 1f;
            iconWash.color = ArcadeCardVisuals.ArtTint(accent);
        }

        if (art is RectTransform artRect)
            ArcadeCardVisuals.Pattern(artRect);

        if (iconDisc != null)
        {
            iconDisc.sprite = ArcadeKitAssets.Glow;
            iconDisc.preserveAspect = true;
            iconDisc.color = ArcadeUiTheme.Alpha(accent, 0.22f);

            RectTransform discRect = iconDisc.rectTransform;
            discRect.sizeDelta = new Vector2(178f, 178f);
            discRect.anchoredPosition = new Vector2(0f, 4f);
        }

        if (art != null)
        {
            string key = string.IsNullOrEmpty(data.IconKey)
                ? "sword"
                : data.IconKey;

            iconGlyph = ArcadeCardVisuals.EnsureImage(
                art,
                "CardGlyph",
                ArcadeKitAssets.Icon(key),
                Color.white
            );

            iconGlyph.preserveAspect = true;
            iconGlyph.color = Color.Lerp(accent, Color.white, 0.28f);

            RectTransform glyphRect = iconGlyph.rectTransform;
            glyphRect.anchorMin = new Vector2(0.5f, 0.5f);
            glyphRect.anchorMax = new Vector2(0.5f, 0.5f);
            glyphRect.pivot = new Vector2(0.5f, 0.5f);
            glyphRect.sizeDelta = new Vector2(82f, 82f);
            glyphRect.anchoredPosition = new Vector2(0f, 6f);
            iconGlyph.transform.SetAsLastSibling();
        }

        if (typeText != null)
        {
            RectTransform rect = typeText.rectTransform;

            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.sizeDelta = new Vector2(270f, 20f);
            rect.anchoredPosition = new Vector2(14f, 12f);

            ArcadeCardVisuals.Text(
                typeText,
                ArcadeKitAssets.BodyBold,
                13f,
                accent,
                TextAlignmentOptions.Left,
                FontStyles.Bold | FontStyles.UpperCase
            );
        }

        PlaceText(nameText, 247f, 34f, 28f, ArcadeUiTheme.TextPrimary,
            FontStyles.Bold);
        PlaceText(statsText, 350f, 92f, 15f, ArcadeUiTheme.TextSecondary,
            FontStyles.Normal);

        if (rarityText != null)
        {
            rarityText.font = ArcadeKitAssets.BodyBold;
            rarityText.fontSize = 14f;
            rarityText.color = accent;
            rarityText.alignment = TextAlignmentOptions.Left;
            rarityText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        }

        if (rarityRow != null)
        {
            rarityRow.sizeDelta = new Vector2(314f, 22f);
            rarityRow.anchoredPosition = new Vector2(177f, -284f);
        }

        Transform face = statsText != null ? statsText.transform.parent : null;

        if (face != null)
        {
            Image statsPanel = ArcadeCardVisuals.InfoPanel(
                face,
                "CardStats",
                304f,
                92f
            );

            if (statsText != null)
            {
                statsPanel.transform.SetSiblingIndex(
                    statsText.transform.GetSiblingIndex()
                );
            }
        }

        if (stateImage != null)
        {
            stateImage.sprite = ArcadeKitAssets.RoundRect;
            stateImage.type = Image.Type.Sliced;
            stateImage.pixelsPerUnitMultiplier = 1f;
            stateImage.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Ink, 0.42f);
        }

        if (stateText != null)
        {
            stateText.font = ArcadeKitAssets.BodyBold;
            stateText.fontSize = 13f;
            stateText.color = ArcadeUiTheme.TextSecondary;
            stateText.alignment = TextAlignmentOptions.Center;
            stateText.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
        }

        if (stateText != null && stateText.transform.parent is RectTransform stateRow)
        {
            stateRow.sizeDelta = new Vector2(314f, 32f);
            stateRow.anchoredPosition = new Vector2(177f, -422f);
        }

        ArcadeCardVisuals.Divider(transform, 442f);

        if (actionButton != null &&
            actionButton.transform is RectTransform actionRect)
        {
            actionRect.sizeDelta = new Vector2(314f, 64f);
            actionRect.anchoredPosition = new Vector2(177f, -481f);
        }

        ArcadeCardVisuals.Action(
            actionImage,
            actionLabel,
            actionButton != null && actionButton.interactable
        );

        if (priceImage != null)
        {
            priceImage.sprite = ArcadeKitAssets.RoundRect;
            priceImage.type = Image.Type.Sliced;
            priceImage.pixelsPerUnitMultiplier = 1f;
            priceImage.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Ink, 0.82f);
        }

        if (priceText != null)
        {
            priceText.font = ArcadeKitAssets.BodyBold;
            priceText.color = data.Unlocked && !data.Owned
                ? ArcadeUiTheme.Gold
                : ArcadeUiTheme.TextMuted;
        }
    }

    private static void PlaceText(
        TMP_Text text,
        float top,
        float height,
        float size,
        Color color,
        FontStyles style)
    {
        if (text == null)
            return;

        RectTransform rect = text.rectTransform;

        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(314f, height);
        rect.anchoredPosition = new Vector2(177f, -top);

        ArcadeCardVisuals.Text(
            text,
            size >= 26f ? ArcadeKitAssets.Display : ArcadeKitAssets.Body,
            size,
            color,
            TextAlignmentOptions.Left,
            style
        );
    }
}
