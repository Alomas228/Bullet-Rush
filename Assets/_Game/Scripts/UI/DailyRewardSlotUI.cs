using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DailyRewardSlotUI : MonoBehaviour
{
    [Header("Day Label")]
    [SerializeField] private TMP_Text dayText;

    [Header("Coins")]
    [SerializeField] private TMP_Text coinsText;

    [Header("XP")]
    [SerializeField] private TMP_Text xpText;

    [Header("Claim Button")]
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimButtonText;

    [Header("Visual")]
    [SerializeField] private Image slotBackground;
    [SerializeField] private GameObject claimedOverlay;

    private void Awake()
    {
        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimClicked);
    }

    public void SetData(int day, int coins, int xp, bool isToday, bool isClaimed)
    {
        dayText.text = $"День {day}";
        coinsText.text = $"+{coins}";
        xpText.text = $"+{xp} XP";

        if (claimButton != null)
            claimButton.gameObject.SetActive(isToday);

        if (claimedOverlay != null)
            claimedOverlay.SetActive(isClaimed);

        if (slotBackground != null)
        {
            if (isToday)
                slotBackground.color = Color.white;
            else if (isClaimed)
                slotBackground.color = Color.gray * 0.7f;
            else
                slotBackground.color = Color.gray * 0.5f;
        }
    }

    private void OnClaimClicked()
    {
        DailyRewardManager.Instance.ClaimReward();
    }
}