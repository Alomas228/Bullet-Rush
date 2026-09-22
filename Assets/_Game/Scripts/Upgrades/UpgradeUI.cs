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
                        upgrade.UpgradeName;
                }

                if (cards[i].DescriptionText != null)
                {
                    cards[i].DescriptionText.text =
                        upgrade.Description;
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
    }

    public void Hide()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);
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

    private void PlayMenuOpenSound()
    {
        if (AudioManager.Instance == null)
            return;

        SFXLibrary sfx = AudioManager.Instance.SFXLibrary;

        if (sfx != null)
            AudioManager.Instance.PlaySFX(sfx.MenuOpen);
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