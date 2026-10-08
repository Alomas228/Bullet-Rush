using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DailyRewardUI : MonoBehaviour, ILangRefreshable
{
    [Header("5 Reward Slots")]
    [SerializeField] private DailyRewardSlotUI[] rewardSlots;

    [Header("Close Button")]
    [SerializeField] private Button closeButton;

    private bool _panelVisible;

    private void Awake()
    {
        Debug.Log("[DailyRewardUI] Awake! Panel: " + gameObject.name);
        
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(OnCloseClicked);
            Debug.Log("[DailyRewardUI] Close button assigned.");
        }
        else
        {
            Debug.LogWarning("[DailyRewardUI] Close button NOT assigned!");
        }
    }

    private void OnEnable()
    {
        if (DailyRewardManager.Instance != null)
        {
            DailyRewardManager.Instance.OnRewardClaimed += OnRewardClaimed;
            Debug.Log("[DailyRewardUI] Subscribed to OnRewardClaimed");
        }
        else
        {
            Debug.LogWarning("[DailyRewardUI] DailyRewardManager.Instance is NULL!");
        }
    }

    private void OnDisable()
    {
        if (DailyRewardManager.Instance != null)
            DailyRewardManager.Instance.OnRewardClaimed -= OnRewardClaimed;
    }

    private void Start()
    {
        Debug.Log("[DailyRewardUI] Start! Slots count: " + (rewardSlots != null ? rewardSlots.Length.ToString() : "NULL"));
        UpdateUI();
    }

    public void RefreshLang()
    {
        UpdateUI();
    }

    public void ShowPanel()
    {
        Debug.Log("[DailyRewardUI] ShowPanel() called! _panelVisible was: " + _panelVisible);
        
        _panelVisible = true;
        UpdateUI();
        gameObject.SetActive(true);
        
        Debug.Log("[DailyRewardUI] Panel active: " + gameObject.activeSelf);
    }

    private void OnCloseClicked()
    {
        Debug.Log("[DailyRewardUI] OnCloseClicked called!");
        _panelVisible = false;
        gameObject.SetActive(false);
    }

    private void UpdateUI()
    {
        if (DailyRewardManager.Instance == null)
        {
            Debug.LogError("[DailyRewardUI] DailyRewardManager.Instance is NULL!");
            return;
        }

        if (rewardSlots == null || rewardSlots.Length < 5)
        {
            Debug.LogWarning("[DailyRewardUI] rewardSlots is null or has less than 5 elements. Count: " + (rewardSlots != null ? rewardSlots.Length.ToString() : "NULL"));
            return;
        }

        for (int i = 0; i < 5; i++)
        {
            int day = DailyRewardManager.Instance.CurrentDayCounter + i;
            int coins = DailyRewardManager.Instance.GetCoinsForDay(day);
            int xp = DailyRewardManager.Instance.GetXPForDay(day);
            bool isToday = (i == 0);
            bool isClaimed = !isToday;

            if (rewardSlots[i] != null)
            {
                rewardSlots[i].SetData(day, coins, xp, isToday, isClaimed);
            }
            else
            {
                Debug.LogWarning("[DailyRewardUI] rewardSlots[" + i + "] is NULL!");
            }
        }
    }

    private void OnRewardClaimed(int day)
    {
        Debug.Log("[DailyRewardUI] OnRewardClaimed! Day: " + day);
        UpdateUI();
    }
}
