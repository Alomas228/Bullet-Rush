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

    private TMP_Text stateText;
    public void LayoutCard(int width)
    {
        slotBackground = GetComponent<Image>();
        if (slotBackground != null) MenuSheet.Surface(slotBackground, MenuSheet.Card);
        if (claimedOverlay != null) claimedOverlay.SetActive(false);
        RuntimeWindow.Text(dayText, transform, 16, 10, width - 32, 34, 23, true);
        MenuSheet.Box(transform, "Rule", 16, 49, width - 32, 2, MenuSheet.Rule);
        RuntimeWindow.Text(coinsText, transform, 56, 63, width - 68, 32, 21);
        RuntimeWindow.Text(xpText, transform, 56, 100, width - 68, 32, 19);
        var coin = MenuSheet.Box(transform, "Coin", 18,66,26,26, Color.white);
        coin.sprite = ArcadeKitAssets.Coin;
        coin.type = Image.Type.Simple;
        var xp = MenuSheet.Box(transform, "Experience",18,103,26,26, MenuSheet.Muted);
        xp.sprite = ArcadeKitAssets.Icon("star");
        xp.type = Image.Type.Simple;
        stateText = ArcadeUiFactory.Label(ArcadeUiFactory.Stretch(transform, "State"), "",
            ArcadeKitAssets.BodyBold, 18, MenuSheet.Muted, TextAlignmentOptions.Center);
        RuntimeWindow.Text(stateText, transform, 12,148,width - 24,42,18, align: TextAlignmentOptions.Center);
        if (claimButton != null) MenuSheet.Control(claimButton, transform, 12,146,width - 24,46,MenuSheet.Ink);
        if (claimButtonText != null) claimButtonText.color = MenuSheet.Paper;
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

        if (stateText != null)
        {
            stateText.gameObject.SetActive(!isToday);
            stateText.text = Lang.Get(state == State.Claimed ? "daily.claimed" : "daily.locked");
        }
        else if (claimedOverlay != null) claimedOverlay.SetActive(state == State.Claimed);

        if (slotBackground != null)
        {
            switch (state)
            {
                case State.Today:
                    slotBackground.color =
                        MenuSheet.Ochre;
                    break;

                case State.Claimed:
                    slotBackground.color =
                        MenuSheet.Sage;
                    break;

                default:
                    slotBackground.color =
                        MenuSheet.Card;
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