using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeUI : MonoBehaviour
{
    [System.Serializable]
    private class UpgradeCard
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button button;

        public GameObject Root => root;
        public TMP_Text NameText => nameText;
        public TMP_Text DescriptionText => descriptionText;
        public Button Button => button;
    }

    [Header("UI")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private UpgradeCard[] cards;

    [Header("References")]
    [SerializeField] private WaveManager waveManager;

    private TMP_Text[] badges;

    private GameObject trackerRoot;
    private TMP_Text trackerText;

    private GameObject toastRoot;
    private TMP_Text toastText;
    private float toastHideAt;

    public bool IsShowing =>
        upgradePanel != null && upgradePanel.activeSelf;

    /// <summary>
    /// Срабатывает, когда игрок выбрал улучшение (index — слот карточки).
    /// Нужен обучению: оно показывает это окно без WaveManager-волны.
    /// </summary>
    public event System.Action<int> OnUpgradeChosen;

    private void Start()
    {
        Hide();

        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;

            if (cards[i].Button != null)
            {
                cards[i].Button.onClick.AddListener(
                    () => ChooseUpgrade(index)
                );
            }
        }
    }

    public void Show()
    {
        if (UpgradeManager.Instance == null)
        {
            Debug.LogWarning(
                "UpgradeUI: UpgradeManager.Instance is null."
            );

            return;
        }

        UpgradeManager.Instance.GenerateChoices();

        var choices =
            UpgradeManager.Instance.CurrentChoices;

        if (upgradePanel != null)
            upgradePanel.SetActive(true);

        PlayMenuOpenSound();

        for (int i = 0; i < cards.Length; i++)
        {
            if (i < choices.Count)
            {
                UpgradeData upgrade = choices[i];

                if (cards[i].Root != null)
                    cards[i].Root.SetActive(true);

                if (cards[i].NameText != null)
                {
                    cards[i].NameText.text =
                        BuildCardTitle(upgrade);

                    // Цвет названия = редкость (об этом говорит обучение).
                    cards[i].NameText.color =
                        RarityColor(upgrade.Rarity);
                }

                if (cards[i].DescriptionText != null)
                {
                    cards[i].DescriptionText.text =
                        BuildCardDescription(upgrade);
                }
            }
            else
            {
                if (cards[i].Root != null)
                    cards[i].Root.SetActive(false);
            }
        }

        // Останавливаем игру во время выбора
        Time.timeScale = 0f;

        RefreshSynergyWidgets(choices);
        ShowDiscoveredToast();
    }

    public void Hide()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    private void Update()
    {
        if (toastRoot != null &&
            toastRoot.activeSelf &&
            Time.unscaledTime >= toastHideAt)
        {
            toastRoot.SetActive(false);
        }
    }

    private void RefreshSynergyWidgets(
        IReadOnlyList<UpgradeData> choices)
    {
        UpdateBadges(choices);
        UpdateTracker();
    }

    private void UpdateBadges(IReadOnlyList<UpgradeData> choices)
    {
        if (cards == null)
            return;

        EnsureBadges();

        IReadOnlyList<SynergyProgress> synergies =
            UpgradeManager.Instance != null
                ? UpgradeManager.Instance.ActiveSynergies
                : null;

        for (int i = 0; i < cards.Length; i++)
        {
            if (badges == null || badges[i] == null)
                continue;

            UpgradeData upgrade =
                choices != null && i < choices.Count
                    ? choices[i]
                    : null;

            SynergyProgress best =
                upgrade != null
                    ? FindBestSynergy(upgrade, synergies)
                    : null;

            if (best == null)
            {
                badges[i].gameObject.SetActive(false);
                continue;
            }

            badges[i].gameObject.SetActive(true);

            badges[i].text =
                best.Synergy.ShortName + " " +
                best.Current + "/" + best.Required;

            badges[i].color =
                RarityColor(best.Synergy.Rarity);
        }
    }

    private void EnsureBadges()
    {
        if (badges != null)
            return;

        badges = new TMP_Text[cards.Length];

        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i].Root == null)
                continue;

            badges[i] = CreateLabel(
                cards[i].Root.transform,
                "SynergyBadge",
                new Vector2(0.5f, 0.5f),
                new Vector2(0f, 5f),
                new Vector2(260f, 24f),
                16f,
                TextAlignmentOptions.Center,
                TextWrappingModes.NoWrap
            );
        }
    }

    private static SynergyProgress FindBestSynergy(
        UpgradeData upgrade,
        IReadOnlyList<SynergyProgress> synergies)
    {
        if (synergies == null)
            return null;

        SynergyProgress best = null;

        for (int i = 0; i < synergies.Count; i++)
        {
            SynergyProgress progress = synergies[i];

            if (progress == null ||
                progress.Synergy == null ||
                progress.IsComplete)
            {
                continue;
            }

            if (!progress.Synergy.Involves(
                    upgrade,
                    upgrade.Families))
            {
                continue;
            }

            if (best == null ||
                progress.Ratio > best.Ratio)
            {
                best = progress;
            }
        }

        return best;
    }

    private void UpdateTracker()
    {
        if (upgradePanel == null)
            return;

        if (trackerRoot == null)
            CreateTracker();

        IReadOnlyList<SynergyProgress> synergies =
            UpgradeManager.Instance != null
                ? UpgradeManager.Instance.ActiveSynergies
                : null;

        string line = BuildProgressLine(synergies);

        trackerText.text = line;
        trackerRoot.SetActive(!string.IsNullOrEmpty(line));
    }

    private void CreateTracker()
    {
        trackerText = CreateLabel(
            upgradePanel.transform,
            "SynergyTracker",
            new Vector2(0.5f, 0f),
            new Vector2(0f, -46f),
            new Vector2(660f, 44f),
            16f,
            TextAlignmentOptions.Center,
            TextWrappingModes.Normal
        );

        trackerRoot = trackerText.gameObject;
    }

    private static string BuildProgressLine(
        IReadOnlyList<SynergyProgress> synergies)
    {
        if (synergies == null || synergies.Count == 0)
            return string.Empty;

        var parts = new List<string>();

        for (int i = 0; i < synergies.Count; i++)
        {
            SynergyProgress progress = synergies[i];

            if (progress == null ||
                progress.Synergy == null ||
                progress.IsComplete)
            {
                continue;
            }

            parts.Add(
                progress.Synergy.ShortName + " " +
                progress.Current + "/" + progress.Required
            );

            if (parts.Count >= 4)
                break;
        }

        return string.Join("  •  ", parts);
    }

    private void ShowDiscoveredToast()
    {
        if (upgradePanel == null ||
            UpgradeManager.Instance == null)
        {
            return;
        }

        List<SynergyData> discovered =
            UpgradeManager.Instance.DrainDiscoveredSynergies();

        if (discovered == null || discovered.Count == 0)
            return;

        if (toastRoot == null)
            CreateToast();

        var names = new List<string>(discovered.Count);

        foreach (SynergyData synergy in discovered)
        {
            if (synergy != null)
                names.Add(synergy.LocalizedName);
        }

        toastText.text = Lang.Get(
            "synergy.discovered",
            string.Join(", ", names)
        );

        toastRoot.SetActive(true);
        toastRoot.transform.SetAsLastSibling();

        toastHideAt = Time.unscaledTime + 4f;
    }

    private void CreateToast()
    {
        var root = new GameObject(
            "SynergyToast",
            typeof(RectTransform),
            typeof(Image)
        );

        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(upgradePanel.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = root.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0.85f);
        image.raycastTarget = false;

        toastText = CreateLabel(
            root.transform,
            "Label",
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(700f, 90f),
            24f,
            TextAlignmentOptions.Center,
            TextWrappingModes.Normal
        );

        toastRoot = root;
    }

    private static TMP_Text CreateLabel(
        Transform parent,
        string name,
        Vector2 anchor,
        Vector2 anchoredPosition,
        Vector2 size,
        float fontSize,
        TextAlignmentOptions alignment,
        TextWrappingModes wrapping)
    {
        var root = new GameObject(name, typeof(RectTransform));

        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        var text = root.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.textWrappingMode = wrapping;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;

        return text;
    }

    private void ChooseUpgrade(int index)
    {
        if (UpgradeManager.Instance == null)
            return;

        UpgradeManager.Instance.ChooseUpgrade(index);

        OnUpgradeChosen?.Invoke(index);

        // Передаём управление WaveManager
        if (waveManager != null)
        {
            waveManager.ContinueAfterUpgrade();
        }
        else
        {
            Debug.LogWarning(
                "UpgradeUI: WaveManager is not assigned."
            );

            Time.timeScale = 1f;
            Hide();
        }
    }

    /// <summary>
    /// Заголовок карточки с номером уровня: «Горение II». Без
    /// номера повторный уровень выглядит как то же самое
    /// улучшение, и непонятно, брать его или нет. Оружие
    /// уровней не имеет.
    /// </summary>
    private static string BuildCardTitle(UpgradeData upgrade)
    {
        string name = upgrade.LocalizedName;

        int nextLevel = GetNextLevel(upgrade);

        if (nextLevel <= 1)
            return name;

        return name + " " + ToRomanNumeral(nextLevel);
    }

    /// <summary>
    /// Описание с явным «уровень N из M». Числа в описании
    /// эффектов намеренно не указаны: они суммируются от уровня к
    /// уровню и в тексте устаревают, из-за чего карточка обещает
    /// одно, а работает другое.
    /// </summary>
    private static string BuildCardDescription(UpgradeData upgrade)
    {
        string description = upgrade.LocalizedDescription;

        int nextLevel = GetNextLevel(upgrade);
        int maxLevel = GetMaxLevel(upgrade);

        if (maxLevel <= 1)
            return description;

        return description + "\n" + Lang.Get(
            "runup.level_of",
            nextLevel,
            maxLevel
        );
    }

    private static int GetNextLevel(UpgradeData upgrade) =>
        UpgradeManager.Instance.GetStacks(upgrade) + 1;

    private static int GetMaxLevel(UpgradeData upgrade) =>
        UpgradeManager.Instance.GetMaxStacks(upgrade);

    private static string ToRomanNumeral(int value)
    {
        switch (value)
        {
            case 2:
                return "II";

            case 3:
                return "III";

            case 4:
                return "IV";

            default:
                return value.ToString();
        }
    }

    private static Color RarityColor(Rarity rarity)
    {
        switch (rarity)
        {
            case Rarity.Uncommon:
                return new Color(0.42f, 0.8f, 0.35f);

            case Rarity.Rare:
                return new Color(0.4f, 0.58f, 0.95f);

            case Rarity.Epic:
                return new Color(0.66f, 0.42f, 0.92f);

            case Rarity.Legendary:
                return new Color(0.96f, 0.62f, 0.22f);

            default:
                return new Color(0.58f, 0.6f, 0.64f);
        }
    }

    private void PlayMenuOpenSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(
                sfx.MenuOpen,
                priority: SfxPriority.High
            );
    }

    private void OnDestroy()
    {
        if (cards == null)
            return;

        foreach (UpgradeCard card in cards)
        {
            if (card.Button != null)
                card.Button.onClick.RemoveAllListeners();
        }
    }
}