using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class BossCampaignProgressUI : MonoBehaviour
{
    private WaveManager waves;
    private RectTransform root;
    private Slider progress;
    private TMP_Text caption;
    private readonly Image[] marks = new Image[BossCampaign.BossCount];
    private readonly TMP_Text[] labels = new TMP_Text[BossCampaign.BossCount];
    private Enemy activeBoss;
    private float nextBossSearch;
    private int shownWave = -1;
    private int shownDefeated = -1;
    private LangCode shownLanguage;
    private bool shownEndless;

    public void Bind(WaveManager manager) => waves = manager;

    private void Update()
    {
        if (waves == null || GameModeManager.IsPvp) return;
        if (root == null && !Build()) return;
        bool playing = GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing;
        root.gameObject.SetActive(playing);
        if (!playing) return;

        float bossHealthFraction = 1f;
        if (waves.IsBossWave(waves.CurrentWave))
        {
            if ((activeBoss == null || activeBoss.IsDead) && Time.unscaledTime >= nextBossSearch)
            {
                nextBossSearch = Time.unscaledTime + 0.3f;
                activeBoss = null;
                foreach (Enemy enemy in Enemy.SnapshotAlive())
                    if (enemy.GetEnemyData() != null && enemy.GetEnemyData().EnemyType == EnemyType.Boss)
                    { activeBoss = enemy; break; }
            }
            if (activeBoss != null)
                bossHealthFraction = activeBoss.CurrentHealth / Mathf.Max(activeBoss.MaxHealth, 1f);
        }
        progress.SetValueWithoutNotify(BossCampaign.Progress(waves.CurrentWave, waves.BossesDefeated, bossHealthFraction));
        if (shownWave == waves.CurrentWave && shownDefeated == waves.BossesDefeated &&
            shownLanguage == Lang.Code && shownEndless == waves.EndlessMode)
            return;
        shownWave = waves.CurrentWave;
        shownDefeated = waves.BossesDefeated;
        shownLanguage = Lang.Code;
        shownEndless = waves.EndlessMode;
        caption.text = waves.EndlessMode
            ? Lang.Get("boss_campaign.endless", waves.CurrentWave)
            : Lang.Get("boss_campaign.progress", waves.BossesDefeated, BossCampaign.BossCount,
                waves.CurrentWave, BossCampaign.FinalWave);
        for (int i = 0; i < marks.Length; i++)
        {
            bool beaten = i < waves.BossesDefeated;
            bool fighting = waves.CurrentWave == (i + 1) * BossCampaign.Interval && !beaten;
            marks[i].color = beaten ? ArcadeUiTheme.Green : fighting ? ArcadeUiTheme.Gold : ArcadeUiTheme.TextMuted;
            BossData profile = waves.GetBossForWave((i + 1) * BossCampaign.Interval);
            labels[i].text = ((i + 1) * BossCampaign.Interval) + "\n" + (profile != null ? profile.BossName : "—");
            labels[i].color = marks[i].color;
        }
    }

    private bool Build()
    {
        BossHealthBar healthBar = FindAnyObjectByType<BossHealthBar>(FindObjectsInactive.Include);
        Canvas canvas = healthBar != null ? healthBar.GetComponentInParent<Canvas>() : null;
        if (canvas == null) return false;
        float width = Mathf.Clamp(((RectTransform)canvas.transform).rect.width - 48f, 240f, 680f);
        root = ArcadeUiFactory.At(canvas.transform, "BossCampaignProgress", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -160f), new Vector2(width, 82f));
        caption = ArcadeUiFactory.Label(ArcadeUiFactory.At(root, "ProgressCaption", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), Vector2.zero, new Vector2(width, 22f)), "",
            ArcadeKitAssets.BodyBold, 15f, ArcadeUiTheme.TextSecondary, TextAlignmentOptions.Center);
        RectTransform track = ArcadeUiFactory.Rect(root, "CampaignSlider", new Vector2(0f, 1f),
            new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(0f, 8f));
        Image background = ArcadeUiFactory.Image(track, null, ArcadeUiTheme.Line);
        progress = track.gameObject.AddComponent<Slider>();
        progress.interactable = false;
        progress.transition = Selectable.Transition.None;
        progress.navigation = new Navigation { mode = Navigation.Mode.None };
        progress.minValue = 0f;
        progress.maxValue = BossCampaign.FinalWave;
        progress.targetGraphic = background;
        RectTransform fill = ArcadeUiFactory.Stretch(track, "Fill");
        ArcadeUiFactory.Image(fill, null, ArcadeUiTheme.Cyan);
        progress.fillRect = fill;
        RectTransform handle = ArcadeUiFactory.At(track, "PlayerPosition", new Vector2(0f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5f, 18f));
        ArcadeUiFactory.Image(handle, null, ArcadeUiTheme.Gold);
        progress.handleRect = handle;
        for (int i = 0; i < marks.Length; i++)
        {
            float x = (i + 1f) / BossCampaign.BossCount;
            marks[i] = ArcadeUiFactory.Image(ArcadeUiFactory.At(track, "BossMark" + (i + 1),
                new Vector2(x, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12f, 12f)),
                null, ArcadeUiTheme.TextMuted);
            labels[i] = ArcadeUiFactory.Label(ArcadeUiFactory.At(track, "BossLabel" + (i + 1),
                new Vector2(x, 0f), new Vector2(i == 4 ? 1f : 0.5f, 1f), new Vector2(0f, -10f),
                new Vector2(width / 5f, 32f)), "", ArcadeKitAssets.BodyBold, 12f,
                ArcadeUiTheme.TextMuted, TextAlignmentOptions.Center);
            labels[i].enableAutoSizing = true;
            labels[i].fontSizeMin = 9f;
            labels[i].fontSizeMax = 12f;
        }
        return true;
    }

    private void OnDestroy()
    {
        if (root != null) Destroy(root.gameObject);
    }
}
