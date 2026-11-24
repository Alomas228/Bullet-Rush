using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeUI : MonoBehaviour, ILangRefreshable
{
    [System.Serializable]
    private class UpgradeCard
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text progressText;

        public GameObject Root => root;
        public TMP_Text NameText => nameText;
        public TMP_Text DescriptionText => descriptionText;
        public Button Button => button;
        public TMP_Text ProgressText => progressText;
    }

    [Header("UI")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private UpgradeCard[] cards;

    [Header("References")]
    [SerializeField] private WaveManager waveManager;

    private GameObject toastRoot;
    private TMP_Text toastText;
    private float toastHideAt;

    public bool IsShowing =>
        upgradePanel != null && upgradePanel.activeInHierarchy;

    /// <summary>
    /// Срабатывает, когда игрок выбрал улучшение (index — слот карточки).
    /// Нужен обучению: оно показывает это окно без WaveManager-волны.
    /// </summary>
    public event System.Action<int> OnUpgradeChosen;

    private WaveChoiceCard[] visuals;
    private RectTransform board;
    private TMP_Text heading;
    private RectTransform vitals;
    private Transform vitalsHome;
    private UnityEngine.Events.UnityAction[] chooseActions;
    private bool? portraitLayout;

    private void Start()
    {
        if (visuals != null) return;
        Prepare();
        Hide();
    }

    private void Prepare()
    {
        if (visuals != null || upgradePanel == null) return;
        board = WaveChoiceCard.PreparePanel(upgradePanel);
        heading = board.Find("UpgradeTitle").GetComponent<TMP_Text>();

        if (cards == null)
            return;

        visuals = new WaveChoiceCard[cards.Length];
        chooseActions = new UnityEngine.Events.UnityAction[cards.Length];
        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;

            if (cards[i].Button != null)
            {
                visuals[i] = new WaveChoiceCard(cards[i].Root, cards[i].Button, cards[i].NameText, cards[i].DescriptionText, cards[i].ProgressText);
                chooseActions[i] = () => ChooseUpgrade(index);
                cards[i].Button.onClick.AddListener(chooseActions[i]);
            }
        }
    }

    public void Show() => Show(0, false);

    public void Show(int wave, bool bossReward)
    {
        if (UpgradeManager.Instance == null)
        {
            Debug.LogWarning(
                "UpgradeUI: UpgradeManager.Instance is null."
            );

            return;
        }

        Prepare();
        if (visuals == null || upgradePanel == null || cards == null || cards.Length == 0) return;
        UpgradeManager.Instance.GenerateChoices(wave, bossReward);
        bool hasSelectableCard = false;
        for (int i = 0; i < Mathf.Min(UpgradeManager.Instance.CurrentChoices.Count, cards.Length); i++)
            if (cards[i] != null && cards[i].Root != null && cards[i].Button != null)
                hasSelectableCard = true;
        if (!hasSelectableCard)
        {
            Debug.LogWarning("UpgradeUI: No available choices; skipping the panel.", this);
            Hide();
            return;
        }

        if (upgradePanel != null)
        {
            upgradePanel.SetActive(true);
            if (!upgradePanel.activeInHierarchy)
            {
                Debug.LogWarning("UpgradeUI: Panel is not visible; skipping the selection.", this);
                Hide();
                return;
            }
            if (vitals == null) vitals = upgradePanel.transform.parent.Find("HUD/GameplayUI/Vitals") as RectTransform;
            if (vitals != null && vitalsHome == null && vitals.gameObject.activeInHierarchy)
            {
                vitalsHome = vitals.parent;
                vitals.SetParent(upgradePanel.transform.Find("Choice safe area"), false);
            }
            var tutorial = upgradePanel.transform.parent.Find("TutorialPanel");
            upgradePanel.transform.SetAsLastSibling();
            if (tutorial != null) tutorial.SetAsLastSibling();
        }

        PlayMenuOpenSound();
        Time.timeScale = 0f;
        RefreshLang();
        if (UpgradeManager.Instance.CurrentChoices.Count > 0 && cards.Length > 0 && cards[0].Button != null)
            cards[0].Button.Select();
        ShowDiscoveredToast();
    }

    public void RefreshLang()
    {
        if (!IsShowing || visuals == null || UpgradeManager.Instance == null) return;
        var choices = UpgradeManager.Instance.CurrentChoices;
        heading.text = UpgradeManager.Instance.ChoiceTitle;

        for (int i = 0; i < cards.Length; i++)
        {
            if (i < choices.Count)
            {
                UpgradeData upgrade = choices[i];

                if (cards[i].Root != null)
                    cards[i].Root.SetActive(true);

                visuals[i]?.Bind(upgrade, i, Mathf.Min(choices.Count, cards.Length));
                if (cards[i].NameText != null)
                {
                    cards[i].NameText.text =
                        BuildCardTitle(upgrade);
                }

                if (cards[i].DescriptionText != null)
                {
                    cards[i].DescriptionText.text =
                        UpgradeManager.Instance.PreviewStat(upgrade) ?? upgrade.LocalizedDescription;
                }
            }
            else
            {
                if (cards[i].Root != null)
                    cards[i].Root.SetActive(false);
            }
        }

        RefreshSynergyWidgets(choices);
        ReflowChoices(true);
    }

    public void Hide()
    {
        if (vitals != null && vitalsHome != null)
        {
            vitals.SetParent(vitalsHome, false);
            vitals.SetAsFirstSibling();
            vitalsHome = null;
        }
        if (toastRoot != null) toastRoot.SetActive(false);
        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    private void Update()
    {
        if (IsShowing) ReflowChoices(false);
        if (toastRoot != null &&
            toastRoot.activeSelf &&
            Time.unscaledTime >= toastHideAt)
        {
            toastRoot.SetActive(false);
        }
    }

    private void ReflowChoices(bool force)
    {
        if (board == null || visuals == null || UpgradeManager.Instance == null) return;
        bool portrait = ResponsiveUi.Portrait;
        if (!force && portraitLayout == portrait) return;
        portraitLayout = portrait;
        UiArtboard.Resize(board, portrait ? new Vector2(680,1050) : new Vector2(1440,810));
        UiArtboard.Place(heading.rectTransform, Vector2.one*.5f, Vector2.one*.5f,
            new Vector2(0,portrait ? 455 : 219), new Vector2(portrait ? 636 : 900,76));
        int count = Mathf.Min(UpgradeManager.Instance.CurrentChoices.Count, visuals.Length);
        for (int i = 0; i < count; i++) visuals[i]?.Layout(i,count,portrait);
        if (toastRoot != null) LayoutToast();
    }

    private void RefreshSynergyWidgets(
        IReadOnlyList<UpgradeData> choices)
    {
        if (cards == null)
            return;

        IReadOnlyList<SynergyProgress> synergies =
            UpgradeManager.Instance != null
                ? UpgradeManager.Instance.ActiveSynergies
                : null;

        for (int i = 0; i < cards.Length; i++)
        {
            TMP_Text slot = cards[i].ProgressText;

            if (slot == null)
                continue;

            UpgradeData upgrade =
                choices != null && i < choices.Count
                    ? choices[i]
                    : null;

            if (upgrade != null && upgrade.Perk != RunPerk.None)
            {
                string hint = RunBuildMechanics.BranchHint(upgrade.Perk);
                slot.gameObject.SetActive(hint != null);
                slot.color = MenuSheet.Muted;
                slot.text = hint ?? "";
                continue;
            }

            SynergyProgress best =
                upgrade != null
                    ? FindBestSynergy(upgrade, synergies)
                    : null;

            if (best == null)
            {
                slot.gameObject.SetActive(false);
                continue;
            }

            slot.gameObject.SetActive(true);

            if (best.IsComplete)
            {
                slot.color = MenuSheet.Muted;
                slot.text =
                    "✓ " + best.Synergy.ShortName;
            }
            else
            {
                slot.text = Lang.Get(
                    "synergy.progress",
                    best.Synergy.ShortName,
                    best.Current,
                    best.Required
                );

                slot.color = MenuSheet.Muted;
            }
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
                progress.Synergy == null)
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
            if (synergy == null)
                continue;

            names.Add(
                synergy.LocalizedName
            );

            if (names.Count >= 3)
                break;
        }

        toastText.text = Lang.Get(
            "synergy.discovered",
            string.Join(" · ", names)
        );

        toastRoot.SetActive(true);
        toastRoot.transform.SetAsLastSibling();

        toastHideAt = Time.unscaledTime + 5f;
    }

    private void CreateToast()
    {
        var card = ArcadeUiFactory.At(board, "SynergyToast", Vector2.one * .5f, Vector2.one * .5f,
            new Vector2(0, -276), new Vector2(820, 66));
        ArcadeUiFactory.Image(card, ArcadeKitAssets.CutPanel, MenuSheet.Ink, true);
        toastText = ArcadeUiFactory.Label(ArcadeUiFactory.Stretch(card, "Label", 18, 4, 18, 4),
            "", ArcadeKitAssets.BodyBold, 18, MenuSheet.Paper, TextAlignmentOptions.Center);
        toastRoot = card.gameObject;
        LayoutToast();
    }

    private void LayoutToast()
    {
        bool portrait = portraitLayout == true;
        UiArtboard.Place((RectTransform)toastRoot.transform, Vector2.one*.5f, Vector2.one*.5f,
            new Vector2(0,portrait ? -463 : -276), new Vector2(portrait ? 630 : 820,66));
    }

    private void ChooseUpgrade(int index)
    {
        if (!IsShowing || UpgradeManager.Instance == null || index >= UpgradeManager.Instance.CurrentChoices.Count) return;
        Hide();
        UpgradeManager.Instance.ChooseUpgrade(index);

        OnUpgradeChosen?.Invoke(index);

        // TutorialManager owns its own completion and pause. WaveManager only
        // continues post-wave selections (the tutorial still has wave zero).
        if (TutorialManager.Instance != null && TutorialManager.Instance.IsRunning)
            return;

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

    private static int GetNextLevel(UpgradeData upgrade) =>
        UpgradeManager.Instance.GetStacks(upgrade) + 1;

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
        if (chooseActions == null) return;
        for (int i = 0; i < chooseActions.Length; i++)
            if (cards[i].Button != null && chooseActions[i] != null)
                cards[i].Button.onClick.RemoveListener(chooseActions[i]);
    }
}
