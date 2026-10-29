using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CampaignCompletionUI : MonoBehaviour, ILangRefreshable
{
    private static CampaignCompletionUI current;
    public static bool IsOpen => current != null && current.root != null && current.root.gameObject.activeInHierarchy;

    private WaveManager waves;
    private RectTransform root;
    private TMP_Text title;
    private TMP_Text description;
    private Button finish;
    private Button keepPlaying;

    public void Show(WaveManager owner)
    {
        waves = owner;
        current = this;
        if (root == null) Build();
        root.gameObject.SetActive(true);
        finish.interactable = true;
        keepPlaying.interactable = true;
        RefreshLang();
    }

    public void Hide()
    {
        if (root != null) root.gameObject.SetActive(false);
    }

    private void Build()
    {
        BossHealthBar bossBar = FindAnyObjectByType<BossHealthBar>(FindObjectsInactive.Include);
        Canvas canvas = bossBar != null ? bossBar.GetComponentInParent<Canvas>() : null;
        if (canvas == null) canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            var canvasObject = new GameObject("CampaignChoiceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.transform.SetParent(transform, false);
        }
        root = ArcadeUiFactory.Stretch(canvas.transform, "CampaignCompletionChoice");
        Canvas overlay = root.gameObject.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = 200;
        root.gameObject.AddComponent<GraphicRaycaster>();
        ArcadeUiFactory.Image(root, null, ArcadeUiTheme.Backdrop).raycastTarget = true;
        float width = Mathf.Clamp(((RectTransform)canvas.transform).rect.width - 48f, 280f, 640f);
        RectTransform card = ArcadeUiFactory.At(root, "ChoiceCard", new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 326f));
        ArcadeUiFactory.Image(card, ArcadeKitAssets.Panel, ArcadeUiTheme.Panel, true);
        title = ArcadeUiFactory.Label(ArcadeUiFactory.At(card, "Title", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(width - 48f, 46f)), "",
            ArcadeKitAssets.Display, 28f, ArcadeUiTheme.Gold, TextAlignmentOptions.Center);
        title.enableAutoSizing = true;
        title.fontSizeMin = 18f;
        title.fontSizeMax = 28f;
        description = ArcadeUiFactory.Label(ArcadeUiFactory.At(card, "Description", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(width - 48f, 76f)), "",
            ArcadeKitAssets.Body, 18f, ArcadeUiTheme.TextSecondary, TextAlignmentOptions.Center);
        description.enableAutoSizing = true;
        description.fontSizeMin = 13f;
        description.fontSizeMax = 18f;
        finish = ArcadeUiFactory.Button(ArcadeUiFactory.At(card, "FinishWithVictory", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -182f), new Vector2(width - 48f, 48f)),
            ArcadeKitAssets.ButtonOrange, ArcadeUiTheme.Gold, "", ArcadeKitAssets.BodyBold, 20f,
            ArcadeUiTheme.TextDark, () => Choose(false));
        keepPlaying = ArcadeUiFactory.Button(ArcadeUiFactory.At(card, "ContinueForRecord", new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f), new Vector2(0f, -244f), new Vector2(width - 48f, 48f)),
            ArcadeKitAssets.ButtonDark, ArcadeUiTheme.Card, "", ArcadeKitAssets.BodyBold, 20f,
            ArcadeUiTheme.TextPrimary, () => Choose(true));
        foreach (Button button in new[] { finish, keepPlaying })
        {
            TMP_Text label = button.GetComponentInChildren<TMP_Text>();
            label.enableAutoSizing = true;
            label.fontSizeMin = 13f;
            label.fontSizeMax = 20f;
        }
    }

    private void Choose(bool continueForRecord)
    {
        if (waves == null || !waves.AwaitingContinuationChoice) return;
        finish.interactable = false;
        keepPlaying.interactable = false;
        waves.ResolveCampaignChoice(continueForRecord);
    }

    public void RefreshLang()
    {
        if (title == null) return;
        title.text = Lang.Get("boss_campaign.choice.title");
        description.text = Lang.Get("boss_campaign.choice.desc");
        finish.GetComponentInChildren<TMP_Text>().text = Lang.Get("boss_campaign.choice.finish");
        keepPlaying.GetComponentInChildren<TMP_Text>().text = Lang.Get("boss_campaign.choice.continue");
    }

    private void Update()
    {
        if (root == null || !root.gameObject.activeSelf) return;
        if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState != GameState.Playing)
            Hide();
    }

    private void OnDestroy()
    {
        if (current == this) current = null;
        if (root != null) Destroy(root.gameObject);
    }
}
