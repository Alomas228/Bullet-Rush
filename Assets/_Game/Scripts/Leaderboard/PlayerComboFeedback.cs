using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>A single floating combo readout projected beside the player.</summary>
[DefaultExecutionOrder(450)]
public sealed class PlayerComboFeedback : MonoBehaviour
{
    private static readonly Color Ink = new Color32(35, 43, 46, 255);
    private static readonly Color Paper = new Color32(255, 244, 221, 255);
    private static readonly Color Gold = new Color32(244, 187, 84, 255);
    private ComboSystem combo;
    private Canvas canvas;
    private RectTransform canvasRect, root, plate, timerFill, rewardRect;
    private CanvasGroup group;
    private Image bar;
    private TMP_Text countText, caption, bonuses, reward;
    private readonly RectTransform[] sparkRects = new RectTransform[6];
    private readonly Image[] sparks = new Image[6];
    private Transform target;
    private Camera view;
    private int shownCount;
    private float pulseAt = -100f, milestoneAt = -100f, rewardAt = -100f;
    private float punch;
    private bool positioned;
    private bool wasHidden = true;
    private float shownAlpha;
    private Color accent = Paper;

    public void Bind(ComboSystem source)
    {
        if (combo == source) return;
        if (combo != null) combo.OnComboChanged -= OnChanged;
        combo = source;
        if (combo != null) combo.OnComboChanged += OnChanged;
    }

    private void Build()
    {
        if (canvas != null) return;
        GameObject go = new GameObject("Player combo overlay", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        // Owned by this scene's combo system; a separate overlay avoids legacy
        // HUD layout/animation touching this world-anchored indicator.
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 24;
        canvasRect = (RectTransform)go.transform;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        ResponsiveUi.Configure(scaler, new Vector2(1920,1080));

        root = ArcadeUiFactory.At(go.transform, "Combo marker", Vector2.one*.5f, Vector2.one*.5f,
            Vector2.zero, new Vector2(188, 89));
        group = root.gameObject.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        group.alpha = 0f;
        plate = ArcadeUiFactory.At(root, "Animated combo text", Vector2.one*.5f, Vector2.one*.5f,
            Vector2.zero, new Vector2(188,89));
        // Text floats directly over the arena; a modest SDF outline keeps it
        // readable on light ground without any backing panel or frame.
        caption = Label(plate,"Caption", "", 14, new Vector2(0,35), new Vector2(150,18), Paper, false);
        countText = Label(plate,"Count", "", 48, new Vector2(0,1), new Vector2(168,55), Gold, true);
        countText.enableAutoSizing = true;
        countText.fontSizeMin = 25f;
        countText.fontSizeMax = 48f;
        countText.fontStyle = FontStyles.Bold | FontStyles.Italic;
        bonuses = Label(plate,"Bonuses", "", 13, new Vector2(0,-29),new Vector2(190,17),Paper,false);
        Box(plate,"Timer track",new Vector2(0,-41),new Vector2(96,2),new Color32(35,43,46,105),false);
        timerFill = ArcadeUiFactory.At(plate,"Timer fill",Vector2.one*.5f,new Vector2(0,.5f),
            new Vector2(-48,-41),new Vector2(96,2));
        bar = ArcadeUiFactory.Image(timerFill,null,Gold);
        reward = Label(root,"Milestone reward","",21,new Vector2(0,64),new Vector2(200,30),Gold,true);
        rewardRect = reward.rectTransform;
        reward.gameObject.SetActive(false);

        for (int i = 0; i < sparks.Length; i++)
        {
            sparks[i] = Box(root,"Milestone fragment",Vector2.zero,new Vector2(12,4),Gold,false);
            sparkRects[i] = sparks[i].rectTransform;
            sparks[i].gameObject.SetActive(false);
        }
        canvas.enabled = false;
        RefreshText();
    }

    private static Image Box(Transform parent,string name,Vector2 position,Vector2 size,Color color,bool cut)
    {
        RectTransform rect = ArcadeUiFactory.At(parent,name,Vector2.one*.5f,Vector2.one*.5f,position,size);
        return ArcadeUiFactory.Image(rect,cut ? ArcadeKitAssets.CutPanel : null,color,cut);
    }

    private static TMP_Text Label(Transform parent,string name,string text,float size,Vector2 position,Vector2 bounds,Color color,bool display)
    {
        TMP_Text label = ArcadeUiFactory.Label(parent,name,text,display ? ArcadeKitAssets.Display : ArcadeKitAssets.BodyBold,
            size,color,TextAlignmentOptions.Center,Vector2.one*.5f,Vector2.one*.5f,position,bounds);
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.outlineColor = Ink;
        label.outlineWidth = display ? .18f : .22f;
        label.UpdateMeshPadding();
        return label;
    }

    private void OnChanged(int value)
    {
        if (value <= 0)
        {
            shownCount = 0;
            rewardAt = milestoneAt = -100f;
            return;
        }
        int old = shownCount;
        shownCount = value;
        if (value < 2) return;
        Build();
        accent = TierColor(value);
        pulseAt = Time.unscaledTime;
        bool tier = Crossed(old,value,5) || Crossed(old,value,10) || Crossed(old,value,20) ||
            Crossed(old,value,30) || Crossed(old,value,50);
        punch = tier ? .29f : .13f;
        if (tier) milestoneAt = pulseAt;
        if (Crossed(old,value,5))
        {
            reward.text = GameplayJourney.T("+18% ТЕМП", "+18% FIRE RATE", "+%18 ATIS");
            rewardAt = pulseAt;
        }
        RefreshText();
    }

    public void ShowScoreReward(int amount)
    {
        if (shownCount < 2) return;
        Build();
        reward.text = "+" + amount.ToString("N0") + " " + GameplayJourney.T("ОЧКОВ", "POINTS", "PUAN");
        rewardAt = Time.unscaledTime;
        milestoneAt = rewardAt;
        pulseAt = rewardAt;
        punch = .29f;
    }

    private void RefreshText()
    {
        if (countText == null || combo == null) return;
        caption.text = GameplayJourney.T("КОМБО", "COMBO", "KOMBO");
        countText.text = "×" + shownCount;
        countText.color = accent;
        bar.color = accent;
        float score = combo.ComboMultiplier;
        string scoreLabel = GameplayJourney.T("ОЧКИ", "SCORE", "PUAN");
        string tempoLabel = GameplayJourney.T("ТЕМП", "RATE", "ATIS");
        bonuses.text = shownCount >= 5
            ? $"{scoreLabel} ×{score:0.#}  ·  {tempoLabel} +18%"
            : GameplayJourney.T("БОНУС НА ×5", "BONUS AT ×5", "BONUS ×5");
    }

    private void LateUpdate()
    {
        if (combo == null || canvas == null) return;
        if (GameModeManager.IsPvp || GameStateManager.Instance == null ||
            GameStateManager.Instance.CurrentState != GameState.Playing || Time.timeScale <= 0f)
        {
            HideNow();
            return;
        }
        if (shownCount < 2 && shownAlpha <= .001f) { HideNow(); return; }
        if (target == null || !target.gameObject.activeInHierarchy)
            target = PlayerHealth.Instance != null ? PlayerHealth.Instance.transform : null;
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;
        if (target == null || view == null || !target.gameObject.activeInHierarchy) { HideNow(); return; }

        canvas.enabled = true;
        if (wasHidden)
        {
            wasHidden = false;
            RefreshText();
        }

        Vector3 playerScreen = view.WorldToScreenPoint(target.position + Vector3.up*.55f);
        if (playerScreen.z <= 0f) { HideNow(); return; }
        Vector3 ahead = view.WorldToScreenPoint(target.position + Vector3.up*.55f + target.forward);
        Vector2 direction = new Vector2(ahead.x-playerScreen.x,ahead.y-playerScreen.y).normalized;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,playerScreen,null,out Vector2 local)) return;
        // Keep the badge above the actual character, leaning gently toward the
        // aim direction. It doesn't travel out into the firing lane.
        Vector2 desired = local + new Vector2(0,93) + direction * 26f;
        Rect rect = ResponsiveUi.SafeRect(canvasRect);
        desired.x = Mathf.Clamp(desired.x, rect.xMin+112, rect.xMax-112);
        desired.y = Mathf.Clamp(desired.y, rect.yMin+66, rect.yMax-102);
        float dt = Time.unscaledDeltaTime;
        root.anchoredPosition = positioned ? Vector2.Lerp(root.anchoredPosition,desired,1f-Mathf.Exp(-18f*dt)) : desired;
        positioned = true;
        canvas.enabled = true;

        float elapsed = Time.unscaledTime - pulseAt;
        float positive = shownCount >= 2 ? 1f : 0f;
        shownAlpha = Mathf.MoveTowards(shownAlpha,positive,dt*(positive > 0f ? 12f : 6f));
        group.alpha = shownAlpha * (shownCount >= 2 && combo.RemainingFraction < .20f ? .72f+.28f*combo.RemainingFraction/.20f : 1f);
        float pop = elapsed < .32f ? Mathf.Sin(Mathf.Clamp01(elapsed/.32f)*Mathf.PI)*Mathf.Exp(-elapsed*3.8f)*punch*2f : 0f;
        plate.localScale = Vector3.one*(1f+pop);
        plate.localRotation = Quaternion.Euler(0,0,-pop*7f);
        timerFill.sizeDelta = new Vector2(96*combo.RemainingFraction,2);

        float life = (Time.unscaledTime - milestoneAt)/.42f;
        countText.color = Color.Lerp(accent, Color.white, Mathf.Clamp01(1f-life)*.65f);
        for (int i = 0; i < sparks.Length; i++)
        {
            bool visible = shownCount>=2 && life>=0f && life<1f;
            if (sparks[i].gameObject.activeSelf != visible) sparks[i].gameObject.SetActive(visible);
            if (!visible) continue;
            float angle = (i*60f+20f)*Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle));
            sparkRects[i].anchoredPosition = new Vector2(dir.x*94,dir.y*43)+dir*(life*34);
            sparkRects[i].localRotation = Quaternion.Euler(0,0,angle*Mathf.Rad2Deg);
            sparkRects[i].localScale = Vector3.one*Mathf.Lerp(1.25f,.25f,life);
            Color c = accent; c.a = (1-life)*(1-life); sparks[i].color = c;
        }

        float rewardLife = (Time.unscaledTime-rewardAt)/.87f;
        bool showReward = shownCount>=2 && rewardLife>=0f && rewardLife<1f;
        if (reward.gameObject.activeSelf != showReward) reward.gameObject.SetActive(showReward);
        if (showReward)
        {
            rewardRect.anchoredPosition = new Vector2(0,64+rewardLife*15);
            Color c = accent; c.a = Mathf.Clamp01((1-rewardLife)*3f); reward.color = c;
        }
    }

    private static bool Crossed(int before,int after,int threshold) => before < threshold && after >= threshold;
    private static Color TierColor(int value)
    {
        if (value >= 50) return new Color32(255,220,130,255);
        if (value >= 30) return new Color32(247,147,83,255);
        if (value >= 20) return new Color32(255,190,96,255);
        if (value >= 10) return Gold;
        if (value >= 5) return new Color32(241,211,145,255);
        return Paper;
    }

    private void HideNow()
    {
        shownAlpha = 0f;
        positioned = false;
        wasHidden = true;
        if (canvas != null) canvas.enabled = false;
    }

    private void OnDisable() => HideNow();
    private void OnDestroy()
    {
        if (combo != null) combo.OnComboChanged -= OnChanged;
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
