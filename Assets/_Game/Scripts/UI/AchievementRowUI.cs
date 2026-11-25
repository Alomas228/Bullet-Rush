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

    private Image progressFill;
    private bool barBuilt;
    private Image badge,completedBadge;

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

    public void LayoutRow()
    {
        EnsureProgressBar();
        RuntimeWindow.LedgerRow((RectTransform)transform, titleLabel, descriptionLabel, rewardLabel, progressLabel,
            progressBar, null);
        if(badge==null) badge=RuntimeWindow.LedgerBadge((RectTransform)transform,out completedBadge);
    }

    public void Bind(
        AchievementData data,
        bool unlocked,
        float progress,
        float required)
    {
        if (data == null)
            return;

        if(badge!=null)
        {
            string icon=data.IsHidden&&!unlocked?"lock":data.ConditionType switch
            {
                AchievementConditionType.KillsWithoutDamageInRun=>"target",
                AchievementConditionType.KillsByDamageSource=>RuntimeWindow.DamageIcon(data.DamageSource),
                AchievementConditionType.KillsInSingleEvent=>"power",
                AchievementConditionType.ReachWaveWithoutDamage=>"shield",
                AchievementConditionType.WinWithUniqueWeapons=>"sword",
                AchievementConditionType.UniqueBosses=>"crown",
                AchievementConditionType.PerfectWavesInRun=>"star",
                AchievementConditionType.CumulativeBossKills=>"trophy",
                AchievementConditionType.CumulativeWaves=>"chart",
                AchievementConditionType.CumulativeRuns=>"award",
                _=>"target"
            };
            bool custom=data.Icon!=null&&(!data.IsHidden||unlocked);
            badge.sprite=custom?data.Icon:ArcadeKitAssets.Icon(icon);
            badge.color=custom?Color.white:unlocked?MenuSheet.Sage:MenuSheet.Ochre;
            completedBadge.gameObject.SetActive(unlocked);
        }

        if (titleLabel != null)
            titleLabel.text = data.GetTitle();

        if (descriptionLabel != null)
            descriptionLabel.text = data.GetDescription(unlocked);

        if (background != null)
            background.color = unlocked ? MenuSheet.Sage : MenuSheet.Card;

        if (rewardLabel != null)
            rewardLabel.text = BuildRewardText(data);

        if (progressLabel != null)
            progressLabel.text = BuildProgressText(data, unlocked, progress, required);

        if (data.HasProgressBar && progressBar == null)
            EnsureProgressBar();

        if (progressBar != null)
        {
            float fill = required > 0f ? Mathf.Clamp01(progress / required) : (unlocked ? 1f : 0f);
            progressBar.value = fill;
            progressBar.gameObject.SetActive(data.HasProgressBar);

            if (progressFill != null)
                progressFill.color = unlocked ? ArcadeUiTheme.Green : ArcadeUiTheme.Gold;
        }
    }

    private void EnsureProgressBar()
    {
        if (barBuilt)
            return;

        barBuilt = true;

        Transform existing = transform.Find("ProgressBar");

        if (existing != null)
            progressBar = existing.GetComponent<Slider>();

        if (progressBar == null)
        {
            GameObject go = new GameObject("ProgressBar", typeof(RectTransform));
            RectTransform rect = (RectTransform)go.transform;

            rect.SetParent(transform, false);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 3f);
            rect.sizeDelta = new Vector2(-72f, 8f);

            progressBar = ArcadeUiFactory.Bar(
                rect,
                0f,
                false,
                ArcadeUiTheme.BackgroundDeep,
                ArcadeUiTheme.Gold,
                0f
            );
        }

        Transform fill = progressBar.transform.Find("Fill Area/Fill");

        if (fill != null)
            progressFill = fill.GetComponent<Image>();

        progressBar.gameObject.SetActive(false);
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
