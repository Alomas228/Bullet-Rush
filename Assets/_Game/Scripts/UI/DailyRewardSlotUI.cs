using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DailyRewardSlotUI : MonoBehaviour
{
    public enum State
    {
        Locked,
        Today,
        Claimed
    }

    [SerializeField] private TMP_Text dayText;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text xpText;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimButtonText;
    [SerializeField] private Image slotBackground;
    [SerializeField] private GameObject claimedOverlay;

    private void Awake()
    {
        if (slotBackground == null)
            slotBackground = GetComponent<Image>();

        if (claimButton != null)
            claimButton.onClick.AddListener(OnClaimClicked);
    }

    private void OnDestroy()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(OnClaimClicked);
    }

    public void SetData(int day, int coins, int xp, State state)
    {
        dayText.text = Lang.Get("daily.day", day);
        coinsText.text = Lang.Get("daily.coins", "+" + coins);
        xpText.text = Lang.Get("daily.xp", "+" + xp);

        bool isToday = state == State.Today;

        if (claimButton != null)
        {
            claimButton.gameObject.SetActive(isToday);
            claimButton.interactable = isToday;
        }

        if (claimButtonText != null)
        {
            claimButtonText.text = Lang.Get(
                isToday
                    ? "daily.claim"
                    : state == State.Claimed
                        ? "daily.claimed"
                        : "daily.locked"
            );
        }

        if (claimedOverlay != null)
            claimedOverlay.SetActive(state == State.Claimed);

        if (slotBackground != null)
        {
            switch (state)
            {
                case State.Today:
                    slotBackground.color = new Color(1f, 0.84f, 0f, 0.55f);
                    break;

                case State.Claimed:
                    slotBackground.color = new Color(0.25f, 0.8f, 0.45f, 0.45f);
                    break;

                default:
                    slotBackground.color = new Color(0.5f, 0.5f, 0.5f, 0.35f);
                    break;
            }
        }
    }

    private void OnClaimClicked()
    {
        DailyRewardManager manager = DailyRewardManager.Instance;

        if (manager != null)
            manager.ClaimReward();
    }
}