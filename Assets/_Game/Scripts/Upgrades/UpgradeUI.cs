using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UpgradeUI : MonoBehaviour
{
    [System.Serializable]
    private class UpgradeCard
    {
        public GameObject root;
        public TMP_Text nameText;
        public TMP_Text descriptionText;
        public Button button;
    }

    [Header("UI")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private UpgradeCard[] cards;

    [Header("References")]
    [SerializeField] private WaveManager waveManager;

    private void Start()
    {
        Hide();

        if (cards == null)
            return;

        for (int i = 0; i < cards.Length; i++)
        {
            int index = i;

            if (cards[i].button != null)
            {
                cards[i].button.onClick.AddListener(
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

                if (cards[i].root != null)
                    cards[i].root.SetActive(true);

                if (cards[i].nameText != null)
                {
                    cards[i].nameText.text =
                        upgrade.UpgradeName;
                }

                if (cards[i].descriptionText != null)
                {
                    cards[i].descriptionText.text =
                        upgrade.Description;
                }
            }
            else
            {
                if (cards[i].root != null)
                    cards[i].root.SetActive(false);
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
            if (card.button != null)
                card.button.onClick.RemoveAllListeners();
        }
    }
}