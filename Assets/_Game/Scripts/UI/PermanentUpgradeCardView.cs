using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Displays the current and next bonus. The controller handles purchases and selection.</summary>
public class PermanentUpgradeCardView : MonoBehaviour
{
    [Header("Surface")]
    [SerializeField] private Image frame;
    [SerializeField] private RectTransform face;
    [SerializeField] private Image selectedIndicator;

    [Header("Icon")]
    [SerializeField] private Image iconArea;
    [SerializeField] private Image iconMark;
    [SerializeField] private TMP_Text categoryText;

    [Header("Text")]
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text currentValueText;
    [SerializeField] private TMP_Text nextValueText;

    [Header("Level progress")]
    [SerializeField] private Slider levelBar;

    [Header("Action")]
    [SerializeField] private Button selectTarget;
    [SerializeField] private Button buyButton;
    [SerializeField] private Image buyImage;
    [SerializeField] private Image buyCostBadge;
    [SerializeField] private Image buyCoinIcon;
    [SerializeField] private TMP_Text buyLabel;
    [SerializeField] private TMP_Text buyCost;
    [SerializeField] private Image maxedImage;
    [SerializeField] private TMP_Text maxedLabel;

    public PermanentUpgradeStat Stat { get; private set; }
    public bool HasStat { get; private set; }
    public Button BuyButton => buyButton;
    public Button SelectButton => selectTarget;

    public void Prepare(Transform sheet, float x, float y)
    {
        const float width = 684;
        const float height = 262;
        // The scene builder puts the view on CardFace together with a vertical layout.
        if (TryGetComponent<VerticalLayoutGroup>(out var originalLayout))
            originalLayout.enabled = false;
        MenuSheet.Place(this, sheet, x, y, width, height);
        MenuSheet.HideChildren(transform);
        MenuSheet.Place(frame, transform, 0, 0, width, height);
        MenuSheet.Surface(frame, MenuSheet.Rule);
        MenuSheet.Place(selectTarget, transform, 2, 2, width - 4, height - 5);
        Image paper = selectTarget.GetComponent<Image>();
        MenuSheet.Surface(paper, MenuSheet.Card, true);
        selectTarget.targetGraphic = paper;
        MenuSheet.Place(iconArea, transform, 20, 20, 82, 82);
        MenuSheet.Surface(iconArea, MenuSheet.Sage);
        MenuSheet.Place(iconMark, iconArea.transform, 18, 18, 46, 46);
        iconMark.preserveAspect = true;
        iconMark.type = Image.Type.Simple;
        iconMark.color = MenuSheet.Ink;
        MenuSheet.Label(categoryText, transform, 122, 16, 352, 26, 17, MenuSheet.Muted);
        MenuSheet.Label(nameText, transform, 121, 42, 536, 47, 32, MenuSheet.Ink, heading: true);
        MenuSheet.Label(descriptionText, transform, 22, 106, 637, 44, 20, MenuSheet.Muted);
        descriptionText.font = ArcadeKitAssets.Body;

        MenuSheet.Place(levelBar, transform, 22, 155, 640, 9);
        MenuSheet.Surface(levelBar.GetComponent<Image>(), MenuSheet.Recess);
        MenuSheet.Surface(levelBar.fillRect.GetComponent<Image>(), MenuSheet.Ink);
        MenuSheet.Label(levelText, transform, 465, 18, 195, 25, 18, MenuSheet.Muted, TextAlignmentOptions.Right);

        // The existing captions are localized along with the rest of the scene.
        var currentCaption = currentValueText.transform.parent.Find("LabelText").GetComponent<TMP_Text>();
        var nextCaption = nextValueText.transform.parent.Find("LabelText").GetComponent<TMP_Text>();
        MenuSheet.Label(currentCaption, transform, 22, 183, 128, 24, 16, MenuSheet.Muted);
        MenuSheet.Label(currentValueText, transform, 22, 206, 128, 35, 27, MenuSheet.Ink);
        MenuSheet.Label(nextCaption, transform, 166, 183, 137, 24, 16, MenuSheet.Muted);
        MenuSheet.Label(nextValueText, transform, 166, 206, 137, 35, 27, MenuSheet.Ink);
        MenuSheet.Place(buyButton, transform, 336, 186, 326, 57);
        MenuSheet.Surface(buyImage, MenuSheet.Ochre, true);
        buyButton.targetGraphic = buyImage;
        MenuSheet.Label(buyLabel, buyButton.transform, 10, 0, 184, 57, 22, MenuSheet.Ink, TextAlignmentOptions.Center);
        MenuSheet.Place(buyCostBadge, buyButton.transform, 201, 8, 114, 41);
        MenuSheet.Surface(buyCostBadge, MenuSheet.Ink);
        MenuSheet.Place(buyCoinIcon, buyCostBadge.transform, 8, 9, 23, 23);
        buyCoinIcon.sprite = ArcadeKitAssets.Icon("coin");
        buyCoinIcon.type = Image.Type.Simple;
        buyCoinIcon.color = MenuSheet.Ochre;
        MenuSheet.Label(buyCost, buyCostBadge.transform, 30, 0, 77, 41, 21, MenuSheet.Paper, TextAlignmentOptions.Center);
        MenuSheet.Place(maxedImage, transform, 336, 186, 326, 57);
        MenuSheet.Surface(maxedImage, MenuSheet.Sage);
        MenuSheet.Label(maxedLabel, maxedImage.transform, 8, 0, 310, 57, 20, MenuSheet.Ink, TextAlignmentOptions.Center);
    }

    public void Bind(PermanentUpgradeStat stat)
    {
        Stat = stat;
        HasStat = true;
        var category = PermanentUpgradeCategories.Get(stat);
        categoryText.text = PermanentUpgradeCategories.GetLabel(category);
        nameText.text = PermanentUpgrades.GetName(stat);
        descriptionText.text = PermanentUpgrades.GetDescription(stat);
        iconMark.sprite = Glyph(category);
        switch (category)
        {
            case PermanentUpgradeCategory.Health: iconArea.color = MenuSheet.Sage; break;
            case PermanentUpgradeCategory.Speed: iconArea.color = MenuSheet.Blue; break;
            case PermanentUpgradeCategory.Critical: iconArea.color = MenuSheet.Ochre; break;
            default: iconArea.color = MenuSheet.Coral; break;
        }
    }

    public void Refresh(int coins)
    {
        if (!HasStat) return;
        int level = PermanentUpgrades.GetLevel(Stat);
        int cost = PermanentUpgrades.GetCost(Stat);
        bool maxed = level >= PermanentUpgrades.MaxLevel;
        bool affordable = coins >= cost;
        levelText.text = Lang.Get("upg.level", level, PermanentUpgrades.MaxLevel);
        levelBar.SetValueWithoutNotify((float)level / PermanentUpgrades.MaxLevel);
        currentValueText.text = PermanentUpgrades.FormatLevelTotal(Stat);
        nextValueText.text = maxed ? Lang.Get("upg.dash_marker") : PermanentUpgrades.FormatLevelNext(Stat);
        buyButton.gameObject.SetActive(!maxed);
        maxedImage.gameObject.SetActive(maxed);
        maxedLabel.text = Lang.Get("upg.maxed");
        buyButton.interactable = affordable && !maxed;
        buyImage.color = affordable ? MenuSheet.Ochre : MenuSheet.Recess;
        buyLabel.text = Lang.Get("upg.buy");
        buyLabel.color = affordable ? MenuSheet.Ink : MenuSheet.Muted;
        buyCost.text = cost.ToString();
    }

    public void SetSelected(bool value)
    {
        frame.color = value ? MenuSheet.Ink : MenuSheet.Rule;
    }

    private static Sprite Glyph(PermanentUpgradeCategory category)
    {
        switch (category)
        {
            case PermanentUpgradeCategory.Health:
                return ArcadeKitAssets.Icon("heart");

            case PermanentUpgradeCategory.Speed:
                return ArcadeKitAssets.Icon("power");

            case PermanentUpgradeCategory.Critical:
                return ArcadeKitAssets.Icon("target");

            default:
                return ArcadeKitAssets.Icon("sword");
        }
    }
}
