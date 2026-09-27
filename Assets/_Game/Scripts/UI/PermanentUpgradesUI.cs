using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель «Улучшения» в главном меню: постоянные покупки параметров
/// игрока за монеты (PermanentUpgrades).
///
/// На каждый параметр — одна карточка:
///  - имя параметра и краткое описание;
///  - текущий суммарный бонус и прирост от следующего уровня;
///  - полоса прогресса уровня (0..PermanentUpgrades.MaxLevel);
///  - кнопка с ценой следующего уровня.
///
/// Состояния кнопки:
///  - не хватает монет  -> «Не хватает N», неактивна
///  - максимум           -> «Максимум», неактивна
///  - можно купить       -> «Улучшить • N», активна
///
/// Карточки генерируются из одного шаблона (ScrollRect/Content)
/// и обновляются при каждом открытии панели.
/// </summary>
public class PermanentUpgradesUI : MonoBehaviour
{
    private sealed class RuntimeCard
    {
        public Image background;
        public TMP_Text nameText;
        public TMP_Text typeText;
        public TMP_Text statsText;
        public TMP_Text nextText;
        public TMP_Text statusText;
        public Slider progress;
        public Button actionButton;
        public Image actionButtonImage;
        public TMP_Text actionButtonText;

        public PermanentUpgradeStat stat;
    }

    [Header("Header")]
    [SerializeField] private TMP_Text playerLevelText;
    [SerializeField] private TMP_Text playerCoinsText;
    [SerializeField] private TMP_Text summaryText;

    [Header("Cards")]
    [Tooltip("Корень ScrollRect прокрутки (Viewport/Content внутри).")]
    [SerializeField] private RectTransform scrollAreaRoot;

    [Tooltip("Шаблон карточки: Name, Type, Stats, Next, Status, Progress, ActionButton.")]
    [SerializeField] private GameObject cardTemplate;

    [Header("Colors")]
    [SerializeField] private Color normalCardColor =
        new Color(0.16f, 0.16f, 0.20f, 1f);
    [SerializeField] private Color maxedCardColor =
        new Color(0.18f, 0.38f, 0.22f, 1f);
    [SerializeField] private Color activeButtonColor =
        new Color(0.25f, 0.55f, 0.25f, 1f);
    [SerializeField] private Color inactiveButtonColor =
        new Color(0.35f, 0.35f, 0.40f, 0.55f);

    private readonly List<RuntimeCard> cards = new List<RuntimeCard>();

    private RectTransform contentRoot;
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

        RebuildContentLayout();
    }

    // =====================================================
    // HEADER
    // =====================================================

    private void RefreshHeader()
    {
        XpManager xp = XpManager.Instance;

        int level = xp != null ? xp.GetPlayerLevel() : 1;
        int coins = xp != null ? xp.GlobalCoins : 0;

        if (playerLevelText != null)
            playerLevelText.text = $"LEVEL {level}";

        if (playerCoinsText != null)
            playerCoinsText.text = $"МОНЕТЫ: {coins}";

        if (summaryText != null)
        {
            summaryText.text =
                $"Куплено улучшений: " +
                $"{PermanentUpgrades.TotalLevelsBought} / " +
                $"{PermanentUpgrades.TotalLevelsAvailable}";
        }
    }

    // =====================================================
    // BUILD
    // =====================================================

    private void BuildCards()
    {
        built = true;

        if (cardTemplate == null)
        {
            Debug.LogWarning(
                "PermanentUpgradesUI: cardTemplate не назначен.",
                this
            );

            return;
        }

        RectTransform content = GetContentRoot();

        if (content == null)
        {
            Debug.LogWarning(
                "PermanentUpgradesUI: нет ScrollRect/Viewport/Content.",
                this
            );

            return;
        }

        contentRoot = content;

        GameObject template = cardTemplate;

        template.SetActive(false);

        IReadOnlyList<PermanentUpgradeStat> stats =
            PermanentUpgrades.Stats;

        for (int i = 0; i < stats.Count; i++)
        {
            PermanentUpgradeStat stat = stats[i];

            GameObject cardObject =
                Instantiate(template, content);

            cardObject.name =
                $"Card_{PermanentUpgrades.GetName(stat)}";

            RuntimeCard card = new RuntimeCard
            {
                stat = stat,
                background = cardObject.GetComponent<Image>(),
                nameText = FindText(cardObject, "Name"),
                typeText = FindText(cardObject, "Type"),
                statsText = FindText(cardObject, "Stats"),
                nextText = FindText(cardObject, "Next"),
                statusText = FindText(cardObject, "Status"),
                progress = FindSlider(cardObject, "Progress")
            };

            BindActionButton(card, cardObject);

            cardObject.SetActive(true);

            cards.Add(card);
        }

        RebuildContentLayout();
    }

    private void BindActionButton(
        RuntimeCard card,
        GameObject cardObject)
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

        if (card.actionButton == null)
            return;

        var captured = card;

        card.actionButton.onClick.AddListener(
            () => OnBuyClicked(captured)
        );
    }

    private RectTransform GetContentRoot()
    {
        if (contentRoot != null)
            return contentRoot;

        if (scrollAreaRoot == null)
            return null;

        if (cardTemplate != null)
            return cardTemplate.transform.parent as RectTransform;

        Transform viewport = scrollAreaRoot.Find("Viewport");

        return viewport != null
            ? viewport.Find("Content") as RectTransform
            : null;
    }

    private void RebuildContentLayout()
    {
        RectTransform content = GetContentRoot();

        if (content == null)
            return;

        LayoutRebuilder.ForceRebuildLayoutImmediate(content);

        Canvas.ForceUpdateCanvases();
    }

    private static TMP_Text FindText(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);

        return child != null
            ? child.GetComponent<TMP_Text>()
            : null;
    }

    private static Slider FindSlider(GameObject root, string childName)
    {
        Transform child = root.transform.Find(childName);

        return child != null
            ? child.GetComponent<Slider>()
            : null;
    }

    // =====================================================
    // REFRESH
    // =====================================================

    private void RefreshCards()
    {
        int coins =
            XpManager.Instance != null
                ? XpManager.Instance.GlobalCoins
                : 0;

        for (int i = 0; i < cards.Count; i++)
        {
            RuntimeCard card = cards[i];

            PermanentUpgradeStat stat = card.stat;

            int level = PermanentUpgrades.GetLevel(stat);
            int cost = PermanentUpgrades.GetCost(stat);
            bool maxed = PermanentUpgrades.IsMaxLevel(stat);

            if (card.nameText != null)
            {
                card.nameText.text =
                    PermanentUpgrades.GetName(stat);
            }

            if (card.typeText != null)
            {
                card.typeText.text =
                    PermanentUpgrades.GetDescription(stat);
            }

            if (card.statsText != null)
            {
                card.statsText.text =
                    "Сейчас: " +
                    PermanentUpgrades.FormatTotal(stat);
            }

            if (card.nextText != null)
            {
                card.nextText.text =
                    PermanentUpgrades.FormatNextLevelGain(stat);
            }

            if (card.statusText != null)
            {
                card.statusText.text =
                    $"УРОВЕНЬ {level} / {PermanentUpgrades.MaxLevel}";
            }

            if (card.progress != null)
            {
                card.progress.value =
                    (float)level / PermanentUpgrades.MaxLevel;
            }

            if (card.background != null)
            {
                card.background.color = maxed
                    ? maxedCardColor
                    : normalCardColor;
            }

            bool affordable = !maxed && coins >= cost;

            if (card.actionButtonText != null)
            {
                card.actionButtonText.text = maxed
                    ? "Максимум"
                    : affordable
                        ? $"Улучшить  •  {cost}"
                        : $"Не хватает  •  {cost}";
            }

            if (card.actionButton != null)
                card.actionButton.interactable = affordable;

            if (card.actionButtonImage != null)
            {
                card.actionButtonImage.color = affordable
                    ? activeButtonColor
                    : inactiveButtonColor;
            }
        }
    }

    // =====================================================
    // ACTIONS
    // =====================================================

    private void OnBuyClicked(RuntimeCard card)
    {
        bool bought =
            PermanentUpgrades.TryBuy(card.stat);

        if (bought) PlayPurchaseSound();
        else PlayUiClick();

        Refresh();
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
