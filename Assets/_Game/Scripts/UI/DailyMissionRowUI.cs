using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Одна строка списка ежедневных миссий: заголовок, описание, шкала
/// прогресса, подпись награды и кнопка получения. Отображение только
/// читает состояние у DailyMissionManager через Bind.
/// </summary>
public class DailyMissionRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_Text rewardLabel;
    [SerializeField] private Image background;
    [SerializeField] private Slider progressBar;
    [SerializeField] private Button claimButton;
    [SerializeField] private TMP_Text claimLabel;

    [Header("Colors")]
    [SerializeField] private Color completedTint = new Color(0.16f, 0.32f, 0.2f, 0.55f);
    [SerializeField] private Color claimedTint = new Color(0.12f, 0.2f, 0.16f, 0.4f);
    [SerializeField] private Color activeTint = new Color(1f, 1f, 1f, 0.06f);

    private DailyMissionSaveData.Entry boundEntry;
    private MissionTrack boundTrack = MissionTrack.Daily;
    private bool claimBound;

    private void Awake()
    {
        EnsureClaimBound();
    }

    public void Configure(
        TMP_Text title,
        TMP_Text description,
        TMP_Text progress,
        TMP_Text reward,
        Image bg,
        Slider bar,
        Button claim,
        TMP_Text claimText)
    {
        titleLabel = title;
        descriptionLabel = description;
        progressLabel = progress;
        rewardLabel = reward;
        background = bg;
        progressBar = bar;
        claimButton = claim;
        claimLabel = claimText;

        EnsureClaimBound();
    }

    private void EnsureClaimBound()
    {
        if (claimBound || claimButton == null)
            return;

        claimButton.onClick.AddListener(HandleClaimClicked);
        claimBound = true;
    }

    private void OnDestroy()
    {
        if (claimButton != null)
            claimButton.onClick.RemoveListener(HandleClaimClicked);
    }

    public void Bind(DailyMissionSaveData.Entry entry)
    {
        Bind(entry, MissionTrack.Daily);
    }

    public void Bind(DailyMissionSaveData.Entry entry, MissionTrack track)
    {
        boundEntry = entry;
        boundTrack = track;

        if (entry == null)
            return;

        DailyMissionManager daily = DailyMissionManager.Instance;
        WeeklyMissionManager weekly = WeeklyMissionManager.Instance;
        bool completed = boundTrack == MissionTrack.Weekly
            ? (weekly != null && weekly.IsCompleted(entry))
            : (daily != null && daily.IsCompleted(entry));

        bool claimed = entry.claimed;

        float target = entry.target > 0f ? entry.target : 1f;
        float progress = Mathf.Clamp(entry.progress, 0f, target);

        if (titleLabel != null)
            titleLabel.text = DailyMissionText.GetTitle(entry);

        if (descriptionLabel != null)
            descriptionLabel.text = DailyMissionText.GetDescription(entry, boundTrack);

        if (rewardLabel != null)
            rewardLabel.text = DailyMissionText.GetRewardLabel(entry);

        if (progressLabel != null)
            progressLabel.text = BuildProgressText(completed, claimed, entry.progress, target);

        if (progressBar != null)
        {
            progressBar.value = Mathf.Clamp01(progress / target);
            progressBar.gameObject.SetActive(!claimed);
        }

        if (background != null)
        {
            background.color = claimed
                ? claimedTint
                : completed
                    ? completedTint
                    : activeTint;
        }

        if (claimButton != null)
        {
            bool canClaim = completed && !claimed;

            claimButton.gameObject.SetActive(canClaim);
            claimButton.interactable = canClaim;
        }

        if (claimLabel != null)
            claimLabel.text = Lang.GetOr("mission.claim", "ПОЛУЧИТЬ");
    }

    private static string BuildProgressText(
        bool completed,
        bool claimed,
        float progress,
        float target)
    {
        if (claimed)
            return Lang.GetOr("mission.claimed", "ПОЛУЧЕНО");

        if (completed)
            return Lang.GetOr("mission.complete", "ВЫПОЛНЕНО");

        string template = Lang.GetOr("mission.progress", "{0} / {1}");

        return string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            template,
            Mathf.FloorToInt(progress),
            Mathf.FloorToInt(target)
        );
    }

    private void HandleClaimClicked()
    {
        if (boundEntry == null)
            return;

        bool claimed = boundTrack == MissionTrack.Weekly
            ? WeeklyMissionManager.Instance != null &&
              WeeklyMissionManager.Instance.Claim(boundEntry)
            : DailyMissionManager.Instance != null &&
              DailyMissionManager.Instance.Claim(boundEntry);

        if (claimed)
            Bind(boundEntry, boundTrack);
    }
}
