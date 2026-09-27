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

        accent = UpgradesWireframeTheme.GetAccent(category);

        if (frame != null)
            frame.color = accent;

        if (iconArea != null)
            iconArea.color = UpgradesWireframeTheme.Wash(accent, 0.12f);

        if (iconMark != null)
            iconMark.color = accent;

        if (categoryText != null)
        {
            categoryText.text =
                PermanentUpgradeCategories.GetLabel(category);

            categoryText.color = UpgradesWireframeTheme.Shade(accent, 0.85f);
        }

        if (nameText != null)
            nameText.text = PermanentUpgrades.GetName(stat);

        if (descriptionText != null)
        {
            descriptionText.text =
                PermanentUpgrades.GetDescription(stat);
        }
    }

    /// <summary>
    /// Обновляет уровень, прогресс, бонусы и доступность покупки.
    /// </summary>
    public void Refresh(int coins)
    {
        if (!HasStat)
            return;

        int level = PermanentUpgrades.GetLevel(Stat);

        bool maxed = level >= PermanentUpgrades.MaxLevel;

        int cost = PermanentUpgrades.GetCost(Stat);

        bool affordable = !maxed && coins >= cost;

        if (levelText != null)
        {
            levelText.text =
                $"УРОВЕНЬ {level} / {PermanentUpgrades.MaxLevel}";
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
                ? "—"
                : PermanentUpgrades.FormatLevelNext(Stat);
        }

        SetActiveSafe(buyButton, !maxed);
        SetActiveSafe(maxedImage, maxed);

        if (maxedLabel != null)
            maxedLabel.text = "МАКСИМАЛЬНЫЙ УРОВЕНЬ";

        if (maxed)
            return;

        if (buyLabel != null)
            buyLabel.text = "УЛУЧШИТЬ";

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
            ? Color.white
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
    }

    /// <summary>
    /// Визуальное выделение выбранной карточки: чуть крупнее,
    /// рамка темнее, сверху появляется индикатор.
    /// </summary>
    public void SetSelected(bool value)
    {
        if (face != null)
        {
            float inset = value ? FaceInsetSelected : FaceInset;

            face.offsetMin = new Vector2(inset, inset);
            face.offsetMax = new Vector2(-inset, -inset);
        }

        SetActiveSafe(selectedIndicator, value);

        if (frame != null)
        {
            frame.color = value
                ? UpgradesWireframeTheme.Shade(accent, 0.8f)
                : accent;
        }

        transform.localScale = Vector3.one * (value ? SelectedScale : 1f);
    }

    private static void SetActiveSafe(Component component, bool value)
    {
        if (component == null)
            return;

        GameObject target = component.gameObject;

        if (target.activeSelf != value)
            target.SetActive(value);
    }
}
