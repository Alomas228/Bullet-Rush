using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DailyRewardUI : MonoBehaviour, ILangRefreshable
{
    [SerializeField] private DailyRewardSlotUI[] rewardSlots;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(HidePanel);
    }

    private void OnEnable()
    {
        if (DailyRewardManager.Instance != null)
            DailyRewardManager.Instance.OnRewardClaimed += OnRewardClaimed;
    }

    private void OnDisable()
    {
        if (DailyRewardManager.Instance != null)
            DailyRewardManager.Instance.OnRewardClaimed -= OnRewardClaimed;
    }

    private void OnDestroy()
    {
        if (closeButton != null)
            closeButton.onClick.RemoveListener(HidePanel);
    }

    private void Start()
    {
        UpdateUI();
    }

    public void RefreshLang()
    {
        UpdateUI();
    }

    public void ShowPanel()
    {
        gameObject.SetActive(true);
        UpdateUI();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
    }

    private RectTransform calendar;

    public void UpdateUI()
    {
        DailyRewardManager manager = DailyRewardManager.Instance;

        if (manager == null || rewardSlots == null || rewardSlots.Length == 0)
            return;

        if (titleText != null)
            titleText.text = Lang.Get("daily.title");

        int cycle = manager.CycleLength;
        int shown = Mathf.Min(rewardSlots.Length, cycle);

        if (calendar == null)
        {
            int lines = Mathf.CeilToInt(shown / 4f);
            calendar = RuntimeWindow.Open((RectTransform)transform, titleText, closeButton, 1120, 136 + 218 * lines);
            for (int i = 0; i < rewardSlots.Length; i++)
            {
                var slot = rewardSlots[i];
                if (slot == null) continue;
                if (i >= shown) { slot.gameObject.SetActive(false); continue; }
                int width = i == 6 && shown == 7 ? 518 : 251;
                MenuSheet.Place(slot, calendar, 36 + (i % 4) * 265, 112 + (i / 4) * 218, width, 202);
                slot.LayoutCard(width);
            }
        }

        for (int i = 0; i < shown; i++)
        {
            int day = i + 1;

            if (rewardSlots[i] == null)
                continue;

            rewardSlots[i].SetData(
                day,
                manager.CoinsForDay(day),
                manager.XpForDay(day),
                ResolveState(manager, day)
            );
        }
    }

    private static DailyRewardSlotUI.State ResolveState(
        DailyRewardManager manager,
        int day)
    {
        if (day == manager.ClaimedDayToday ||
            manager.IsDayClaimed(day))
        {
            return DailyRewardSlotUI.State.Claimed;
        }

        if (manager.IsAvailableToday && day == manager.NextDay)
            return DailyRewardSlotUI.State.Today;

        return DailyRewardSlotUI.State.Locked;
    }

    private void OnRewardClaimed(int day)
    {
        UpdateUI();
    }
}