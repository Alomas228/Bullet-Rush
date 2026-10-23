using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Одна карточка улучшения на экране «Улучшения».
///
/// Структура (сверху вниз):
///  - область под иконку + подпись группы;
///  - название параметра;
///  - короткое описание;
///  - «УРОВЕНЬ N / 10» и полоса прогресса уровня
///    (это прогресс конкретного улучшения, а не опыт);
///  - «СЕЙЧАС» и «ДАЛЬШЕ» с бонусами;
///  - кнопка «УЛУЧШИТЬ» с ценой, либо «МАКСИМАЛЬНЫЙ УРОВЕНЬ».
///
/// Сам компонент ничего не покупает — обработчики кнопок вешает
/// PermanentUpgradesUI. Здесь только отображение состояния.
/// </summary>
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

    private const float FaceInset = 3f;
    private const float FaceInsetSelected = 7f;
    private const float SelectedScale = 1.03f;

    private Color accent = Color.gray;
    private PermanentUpgradeCategory category;
    private bool selected;
    private bool affordable = true;
    private bool maxed;

    public PermanentUpgradeStat Stat { get; private set; }

    public bool HasStat { get; private set; }

    public Button BuyButton => buyButton;

    public Button SelectButton => selectTarget;

    /// <summary>
    /// Назначает карточке параметр и его группу. Вызывается, когда
    /// карточка попадает на видимую страницу.
    /// </summary>
    public void Bind(PermanentUpgradeStat stat)
    {
        Stat = stat;
        HasStat = true;

        PermanentUpgradeCategory category =
            PermanentUpgradeCategories.Get(stat);

        this.category = category;

        accent = UpgradesWireframeTheme.GetAccent(category);

        if (frame != null)
            frame.color = accent;

        if (iconArea != null)
            iconArea.color = UpgradesWireframeTheme.Wash(accent, 0.12f);

        if (iconMark != null)
        {
            Sprite glyph = Glyph(category);

            if (glyph != null)
                iconMark.sprite = glyph;

            iconMark.color = accent;
        }

        if (categoryText != null)
        {
            categoryText.text =
                PermanentUpgradeCategories.GetLabel(category);

            categoryText.color = accent;
        }

        if (nameText != null)
            nameText.text = PermanentUpgrades.GetName(stat);

        if (descriptionText != null)
        {
            descriptionText.text =
                PermanentUpgrades.GetDescription(stat);
        }

        ApplyUnifiedStyle();
    }

    /// <summary>
    /// Обновляет уровень, прогресс, бонусы и доступность покупки.
    /// </summary>
    public void Refresh(int coins)
    {
        if (!HasStat)
            return;

        int level = PermanentUpgrades.GetLevel(Stat);

        maxed = level >= PermanentUpgrades.MaxLevel;

        int cost = PermanentUpgrades.GetCost(Stat);

        affordable = !maxed && coins >= cost;

        if (levelText != null)
        {
            levelText.text = Lang.Get(
                "upg.level",
                level,
                PermanentUpgrades.MaxLevel
            );
        }

        // Это прогресс уровня улучшения, а не опыт игрока.
        if (levelBar != null)
        {
            levelBar.SetValueWithoutNotify(
                (float)level / PermanentUpgrades.MaxLevel
            );
        }

        if (currentValueText != null)
        {
            currentValueText.text =
                PermanentUpgrades.FormatLevelTotal(Stat);
        }

        if (nextValueText != null)
        {
            nextValueText.text = maxed
                ? Lang.Get("upg.dash_marker")
                : PermanentUpgrades.FormatLevelNext(Stat);
        }

        SetActiveSafe(buyButton, !maxed);
        SetActiveSafe(maxedImage, maxed);

        if (maxedLabel != null)
            maxedLabel.text = Lang.Get("upg.maxed");

        ApplyUnifiedStyle();

        if (maxed)
            return;

        if (buyLabel != null)
            buyLabel.text = Lang.Get("upg.buy");

        if (buyCost != null)
            buyCost.text = cost.ToString();

        if (buyButton != null)
            buyButton.interactable = affordable;

        if (buyImage != null)
        {
            buyImage.color = affordable
                ? accent
                : UpgradesWireframeTheme.SurfaceDisabled;
        }

        Color textColor = affordable
            ? ArcadeUiPalette.BackgroundDeep
            : UpgradesWireframeTheme.TextMuted;

        if (buyLabel != null)
            buyLabel.color = textColor;

        if (buyCost != null)
            buyCost.color = textColor;

        if (buyCoinIcon != null)
            buyCoinIcon.color = textColor;

        if (buyCostBadge != null)
        {
            buyCostBadge.color = affordable
                ? UpgradesWireframeTheme.White(0.22f)
                : UpgradesWireframeTheme.White(0.12f);
        }

        ApplyUnifiedStyle();
    }

    /// <summary>
    /// Визуальное выделение выбранной карточки: чуть крупнее,
    /// рамка темнее, сверху появляется индикатор.
    /// </summary>
    public void SetSelected(bool value)
    {
        selected = value;

        if (face != null)
        {
            float inset = value ? FaceInsetSelected : FaceInset;

            face.offsetMin = new Vector2(inset, inset);
            face.offsetMax = new Vector2(-inset, -inset);
        }

        SetActiveSafe(selectedIndicator, value);

        transform.localScale = Vector3.one * (value ? SelectedScale : 1f);

        ApplyUnifiedStyle();
    }

    private static void SetActiveSafe(Component component, bool value)
    {
        if (component == null)
            return;

        GameObject target = component.gameObject;

        if (target.activeSelf != value)
            target.SetActive(value);
    }

    private void ApplyUnifiedStyle()
    {
        if (frame != null)
            frame.color = selected ? ArcadeUiTheme.Gold : ArcadeUiTheme.Line;

        if (face != null)
        {
            VerticalLayoutGroup layout =
                face.GetComponent<VerticalLayoutGroup>();

            if (layout != null && layout.enabled)
                layout.enabled = false;

            Transform surface = face.Find("CardSurface");

            if (surface is RectTransform surfaceRect)
                ArcadeCardVisuals.Stretch(surfaceRect, 0f);

            if (surface != null)
            {
                Image image = surface.GetComponent<Image>();

                if (image != null)
                {
                    image.sprite = ArcadeKitAssets.RoundRect;
                    image.type = Image.Type.Sliced;
                    image.color = ArcadeUiTheme.Panel;
                    image.pixelsPerUnitMultiplier = 1f;
                }
            }
        }

        if (iconArea != null)
        {
            RectTransform art = iconArea.rectTransform;

            art.anchorMin = new Vector2(0f, 1f);
            art.anchorMax = new Vector2(0f, 1f);
            art.pivot = new Vector2(0.5f, 0.5f);
            art.sizeDelta = new Vector2(310f, 198f);
            art.anchoredPosition = new Vector2(177f, -123f);

            iconArea.sprite = ArcadeKitAssets.RoundRect;
            iconArea.type = Image.Type.Sliced;
            iconArea.pixelsPerUnitMultiplier = 1f;
            iconArea.color = ArcadeCardVisuals.ArtTint(accent);

            ArcadeCardVisuals.Pattern(art);
        }

        ArcadeCardVisuals.AccentRail(transform, accent);

        if (iconMark != null)
        {
            RectTransform rect = iconMark.rectTransform;

            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(70f, 70f);
            rect.anchoredPosition = new Vector2(0f, 8f);

            Sprite glyph = Glyph(category);

            if (glyph != null)
                iconMark.sprite = glyph;

            iconMark.preserveAspect = true;
            iconMark.color = accent;
        }

        if (categoryText != null)
        {
            RectTransform rect = categoryText.rectTransform;

            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(280f, 22f);
            rect.anchoredPosition = new Vector2(0f, 14f);

            ArcadeCardVisuals.Text(
                categoryText,
                ArcadeKitAssets.BodyBold,
                13f,
                accent,
                TextAlignmentOptions.Center,
                FontStyles.Bold | FontStyles.UpperCase
            );
        }

        PlaceText(nameText, 250f, 34f, 28f, ArcadeUiTheme.TextPrimary,
            FontStyles.Bold);
        PlaceText(descriptionText, 292f, 44f, 15f, ArcadeUiTheme.TextSecondary,
            FontStyles.Normal);
        PlaceText(levelText, 335f, 22f, 16f, ArcadeUiTheme.TextPrimary,
            FontStyles.Bold);

        if (levelBar != null)
        {
            RectTransform rect = levelBar.transform as RectTransform;

            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(310f, 10f);
                rect.anchoredPosition = new Vector2(177f, -360f);
            }

            Image track = levelBar.GetComponent<Image>();

            if (track != null)
            {
                track.sprite = ArcadeKitAssets.BarTrack;
                track.type = Image.Type.Sliced;
                track.color = ArcadeUiTheme.Ink;
                track.pixelsPerUnitMultiplier = 1f;
            }

            if (levelBar.fillRect != null)
            {
                Image fill = levelBar.fillRect.GetComponent<Image>();

                if (fill != null)
                {
                    fill.sprite = ArcadeKitAssets.BarFill;
                    fill.type = Image.Type.Sliced;
                    fill.color = accent;
                    fill.pixelsPerUnitMultiplier = 1f;
                }
            }
        }

        if (face != null)
        {
            Image stats = ArcadeCardVisuals.InfoPanel(
                face,
                "CardStats",
                372f,
                66f
            );

            Transform current = face.Find("CurrentRow");
            Transform next = face.Find("NextRow");

            if (current != null)
            {
                stats.transform.SetSiblingIndex(current.GetSiblingIndex());

                RectTransform rect = current as RectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(310f, 24f);
                rect.anchoredPosition = new Vector2(177f, -389f);
            }

            if (next != null)
            {
                RectTransform rect = next as RectTransform;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(310f, 24f);
                rect.anchoredPosition = new Vector2(177f, -422f);
            }

            StyleRow(current, false);
            StyleRow(next, true);
        }

        ArcadeCardVisuals.Divider(transform, 443f);

        if (buyButton != null)
        {
            RectTransform rect = buyButton.transform as RectTransform;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(310f, 64f);
            rect.anchoredPosition = new Vector2(177f, -480f);
        }

        ArcadeCardVisuals.Action(buyImage, buyLabel, affordable);

        if (buyCostBadge != null)
        {
            buyCostBadge.sprite = ArcadeKitAssets.RoundRect;
            buyCostBadge.type = Image.Type.Sliced;
            buyCostBadge.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Ink, 0.8f);
        }

        if (buyCost != null)
        {
            buyCost.font = ArcadeKitAssets.BodyBold;
            buyCost.color = affordable
                ? ArcadeUiTheme.Gold
                : ArcadeUiTheme.TextMuted;
        }

        if (buyCoinIcon != null)
            buyCoinIcon.color = affordable
                ? ArcadeUiTheme.Gold
                : ArcadeUiTheme.TextMuted;

        if (maxedImage != null)
        {
            RectTransform rect = maxedImage.rectTransform;

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(310f, 64f);
            rect.anchoredPosition = new Vector2(177f, -480f);

            maxedImage.sprite = ArcadeKitAssets.RoundRect;
            maxedImage.type = Image.Type.Sliced;
            maxedImage.color = ArcadeUiTheme.Alpha(ArcadeUiTheme.Ink, 0.32f);
        }

        if (maxedLabel != null)
        {
            maxedLabel.font = ArcadeKitAssets.BodyBold;
            maxedLabel.fontSize = 18f;
            maxedLabel.color = ArcadeUiTheme.Gold;
            maxedLabel.alignment = TextAlignmentOptions.Center;
            maxedLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
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
        rect.sizeDelta = new Vector2(310f, height);
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

    private static void StyleRow(Transform row, bool next)
    {
        if (row == null)
            return;

        TMP_Text label = FindText(row, "LabelText");
        TMP_Text value = FindText(row, "ValueText");

        if (label != null)
        {
            label.font = ArcadeKitAssets.Body;
            label.fontSize = 13f;
            label.color = ArcadeUiTheme.TextMuted;
            label.alignment = TextAlignmentOptions.Left;
        }

        if (value != null)
        {
            value.font = ArcadeKitAssets.BodyBold;
            value.fontSize = next ? 18f : 20f;
            value.color = ArcadeUiTheme.TextPrimary;
            value.alignment = TextAlignmentOptions.Right;
        }
    }

    private static TMP_Text FindText(Transform parent, string name)
    {
        Transform child = parent.Find(name);

        return child != null ? child.GetComponent<TMP_Text>() : null;
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
