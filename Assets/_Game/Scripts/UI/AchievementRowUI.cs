using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Одна строка списка достижений: заголовок, описание, шкала прогресса
/// и подпись награды. Только отображение — состояние приходит из
/// AchievementManager через Bind.
/// </summary>
public class AchievementRowUI : MonoBehaviour
{
    [SerializeField] private TMP_Text titleLabel;
    [SerializeField] private TMP_Text descriptionLabel;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private Image background;
    [SerializeField] private Slider progressBar;
    [SerializeField] private TMP_Text rewardLabel;

    [Header("Colors")]
    [SerializeField] private Color unlockedTint = new Color(0.16f, 0.32f, 0.2f, 0.55f);
    [SerializeField] private Color lockedTint = new Color(1f, 1f, 1f, 0.05f);

    public void Configure(
        TMP_Text title,
        TMP_Text description,
        TMP_Text progress,
        Image bg)
    {
        titleLabel = title;
        descriptionLabel = description;
        progressLabel = progress;
        background = bg;
    }

    public void Bind(
        AchievementData data,
        bool unlocked,
        float progress,
        float required)
    {
        if (data == null)
            return;

        if (titleLabel != null)
            titleLabel.text = data.GetTitle();

        if (descriptionLabel != null)
            descriptionLabel.text = data.GetDescription(unlocked);

        if (background != null)
            background.color = unlocked ? unlockedTint : lockedTint;

        if (rewardLabel != null)
            rewardLabel.text = BuildRewardText(data);

        if (progressLabel != null)
            progressLabel.text = BuildProgressText(data, unlocked, progress, required);

        if (progressBar != null)
        {
            float fill = required > 0f ? Mathf.Clamp01(progress / required) : (unlocked ? 1f : 0f);
            progressBar.value = fill;
            progressBar.gameObject.SetActive(data.HasProgressBar);
        }
    }

    private static string BuildProgressText(
        AchievementData data,
        bool unlocked,
        float progress,
        float required)
    {
        if (unlocked)
            return Lang.GetOr("ach.complete", "Получено");

        if (!data.HasProgressBar)
            return Lang.GetOr("ach.locked", "Не получено");

        return Lang.Get("ach.progress", Mathf.FloorToInt(progress), Mathf.FloorToInt(required));
    }

    private static string BuildRewardText(AchievementData data)
    {
        if (data.Rewards == null || data.Rewards.Count == 0)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();

        for (int i = 0; i < data.Rewards.Count; i++)
        {
            if (data.Rewards[i] == null)
                continue;

            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append(data.Rewards[i].GetLabel());
        }

        return builder.ToString();
    }
}
