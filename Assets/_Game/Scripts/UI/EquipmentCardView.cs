using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays one catalog item as a selectable tile or as the shop detail panel.</summary>
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

    private bool isDetail;
    private Image glyph;
    private Vector2 defaultGlyphPivot;
    private bool expandedPreview;

    public Button CardButton => cardButton;
    public Button ActionButton => actionButton;
    public bool Wired => wired;
    public void MarkWired() => wired = true;

    // Keep the scene's buttons and text bindings. Only their visual hierarchy changes.
    public void Prepare(Transform sheet, float x, float y, float width, float height, bool detail)
    {
        isDetail = detail;
        MenuSheet.Place(this, sheet, x, y, width, height);
        MenuSheet.HideChildren(transform);
        MenuSheet.Place(frameImage, transform, 0, 0, width, height);
        MenuSheet.Surface(frameImage, MenuSheet.Rule);
        MenuSheet.Place(surfaceImage, transform, 3, 3, width - 6, height - 7);
        MenuSheet.Surface(surfaceImage, MenuSheet.Card, !detail);
        cardButton.targetGraphic = surfaceImage;
        cardButton.enabled = !detail;
        MenuSheet.Place(iconWash, transform, 15, 14, width - 30, detail ? 145 : 94);
        MenuSheet.HideChildren(iconWash.transform);
        MenuSheet.Surface(iconWash, MenuSheet.Recess);
        glyph = MenuSheet.Box(iconWash.transform, "ItemGlyph", 0, 0, width - 30, detail ? 145 : 94, Color.white);
        glyph.type = Image.Type.Simple;
        glyph.preserveAspect = true;
        defaultGlyphPivot = glyph.rectTransform.pivot;

        MenuSheet.Label(nameText, transform, 23, detail ? 178 : 113, width - 46, detail ? 49 : 47,
            detail ? 33 : 25, MenuSheet.Ink, heading: true);
        MenuSheet.Label(stateText, transform, 23, detail ? 339 : 160, width - 46, 36, 20, MenuSheet.Muted);

        if (!detail) return;
        MenuSheet.Label(typeText, transform, 23, 157, width - 46, 28, 17, MenuSheet.Muted);
        MenuSheet.Label(statsText, transform, 23, 231, width - 46, 107, 22, MenuSheet.Ink);
        statsText.font = ArcadeKitAssets.Body;
        MenuSheet.Control(actionButton, transform, 22, height - 75, width - 44, 54, MenuSheet.Ochre);
        MenuSheet.HideChildren(actionButton.transform);
        MenuSheet.Label(actionLabel, actionButton.transform, 8, 0, width - 60, 54, 23,
            MenuSheet.Ink, TextAlignmentOptions.Center);
    }

    public void Refresh(EquipmentCardData data, int coins, bool focused = false)
    {
        nameText.text = data.Name;
        bool skinPreview = data.Clothing != null && data.Clothing.Preview != null;
        if (isDetail && skinPreview != expandedPreview)
        {
            expandedPreview = skinPreview;
            // Character detail needs room for its two skills, talent and map protection.
            float extraHeight = skinPreview ? -45f : 0f;
            iconWash.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 145f + extraHeight);
            glyph.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 145f + extraHeight);
            SetTop(typeText.rectTransform, 157f + extraHeight);
            SetTop(nameText.rectTransform, 178f + extraHeight);
            SetTop(statsText.rectTransform, 231f + extraHeight);
            statsText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 107f - extraHeight);
        }
        glyph.sprite = MenuSheetArt.Item(data);
        glyph.color = skinPreview || data.Weapon != null || data.Ability != null ? Color.white : MenuSheet.Ink;
        // Image.preserveAspect aligns the fitted sprite using its pivot.
        var glyphRect = glyph.rectTransform;
        glyphRect.anchorMin = glyphRect.anchorMax = glyphRect.pivot =
            skinPreview ? Vector2.one * 0.5f : defaultGlyphPivot;
        glyphRect.anchoredPosition = Vector2.zero;
        Color accent = Color.Lerp(data.Accent, MenuSheet.Paper, 0.64f);
        iconWash.color = data.Unlocked ? accent : MenuSheet.Recess;
        stateText.text = data.Unlocked && !data.Owned && !isDetail
            ? Lang.Get("upg.refresh_cost", data.Price)
            : data.State;
        stateText.color = data.Equipped ? ArcadeUiTheme.Rgb(0x42614B) : MenuSheet.Muted;
        frameImage.color = focused ? MenuSheet.Ink : MenuSheet.Rule;

        if (!isDetail) return;
        typeText.text = string.IsNullOrEmpty(data.RarityLabel) ? data.TypeLabel
            : data.TypeLabel + "   /   " + data.RarityLabel;
        statsText.text = data.Stats;
        statsText.enableAutoSizing=true;
        statsText.fontSizeMin=14;
        statsText.fontSizeMax=data.Clothing!=null?18:22;
        bool available;
        if(data.Ability!=null&&CharacterBuild.Innate(data.Ability.Kind))
        {
            actionLabel.text=GameplayJourney.T("УЖЕ У ПЕРСОНАЖА","CHARACTER SKILL","KARAKTER YETENEGI");
            available=false;
        }
        else if (!data.Unlocked)
        {
            actionLabel.text = Lang.Get("eq.state_locked_level", data.UnlockLevel);
            available = false;
        }
        else if (!data.Owned)
        {
            actionLabel.text = Lang.Get("eq.action_buy") + "  ·  " + Lang.Get("upg.refresh_cost", data.Price);
            available = coins >= data.Price;
        }
        else if (data.Equipped)
        {
            actionLabel.text = Lang.Get(data.Toggles ? "eq.action_unequip" : "eq.state_equipped");
            available = data.Toggles;
        }
        else
        {
            actionLabel.text = Lang.Get("eq.action_equip");
            available = data.Ability == null || EquipmentManager.HasFreeAbilitySlot(data.Ability.IsAutomatic);
            if (!available) actionLabel.text = GameplayJourney.T("СНАЧАЛА ОСВОБОДИТЕ СЛОТ", "FREE A SLOT FIRST", "ONCE YUVA BOSALTIN");
        }
        actionButton.interactable = available;
        actionImage.color = available ? MenuSheet.Ochre : MenuSheet.Recess;
        actionLabel.color = available ? MenuSheet.Ink : MenuSheet.Muted;
    }

    private static void SetTop(RectTransform rect, float top)
    {
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, -top);
    }

}
