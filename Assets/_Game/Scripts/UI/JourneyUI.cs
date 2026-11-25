using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using static GameplayJourney;

// Scene-local overlay: no prefab setup or new input mapping is required.
public sealed partial class JourneyUI : MonoBehaviour, ILangRefreshable
{
    public static bool IsBlocking => current != null && current.modal != null && current.modal.gameObject.activeSelf;
    public static bool IsJournalOpen => IsBlocking && current.journalOpen;
    private static JourneyUI current;
    private GameplayJourney journey;
    private RectTransform canvasRoot, modal, card, content;
    private TMP_Text notice, caption;
    private Button journalButton;
    private GameOverUI modernResults;
    private bool ownsPause, journalOpen;
    private int tab;
    public void Bind(GameplayJourney owner)
    {
        journey = owner; current = this;
        modernResults = FindAnyObjectByType<GameOverUI>(FindObjectsInactive.Include);
        var host = new GameObject("JourneyCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        host.transform.SetParent(transform, false);
        canvasRoot = host.GetComponent<RectTransform>();
        var canvas = host.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 210;
        ResponsiveUi.Configure(host.GetComponent<CanvasScaler>(), new Vector2(1280,720));
        var safe = ResponsiveUi.SafeContent(canvasRoot);
        journalButton = ButtonAt(safe, "Journey", new Vector2(1, 0), new Vector2(-18, 18), new Vector2(230, 40), "", OpenJournal);
        notice = Label(safe, "Notice", new Vector2(0.5f, 0), new Vector2(0, 76), new Vector2(700, 56), 19, TextAlignmentOptions.Center);
        notice.enableAutoSizing = true;
        notice.fontSizeMin = 14;
        notice.fontSizeMax = 19;
        notice.rectTransform.anchorMin = new Vector2(.08f,0);
        notice.rectTransform.anchorMax = new Vector2(.92f,0);
        notice.rectTransform.sizeDelta = new Vector2(0,56);
        notice.color = MenuSheet.Paper;
        var shadow = notice.gameObject.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(1, -2);
        shadow.effectColor = ArcadeUiTheme.Ink;
        notice.raycastTarget = false;
        RefreshLang();
    }
    private void Update()
    {
        GameState state = GameStateManager.Instance != null ? GameStateManager.Instance.CurrentState : GameState.Menu;
        if (GameModeManager.IsPvp && state == GameState.Playing)
        {
            if (IsBlocking) Close();
            canvasRoot.gameObject.SetActive(false);
            return;
        }
        canvasRoot.gameObject.SetActive(true);
        journalButton.gameObject.SetActive(!IsBlocking && state == GameState.GameOver &&
            (modernResults == null || !modernResults.UsesModernResults));
        notice.gameObject.SetActive(!IsBlocking && state == GameState.Playing && Time.timeScale > 0f && Time.unscaledTime < journey.NoticeUntil);
        notice.text = journey.Notice;
        if (Keyboard.current != null && Keyboard.current.jKey.wasPressedThisFrame)
        {
            if (journalOpen) Close();
            else if (!IsBlocking && !CampaignCompletionUI.IsOpen && (state != GameState.Playing || Time.timeScale > 0)) OpenJournal();
        }
        if (IsBlocking && ownsPause && state != GameState.Playing) Close();
        if (IsBlocking && illustratedJournal && journalPortrait != ResponsiveUi.Portrait) OpenJournal();
        notice.rectTransform.anchoredPosition = new Vector2(0, MobileControls.IsTouchDevice
            ? (ResponsiveUi.Portrait ? 445 : 150) : 76);
    }
    public void Close()
    {
        if (modal != null) modal.gameObject.SetActive(false);
        journalOpen = false;
        illustratedJournal = false;
        GetComponent<WeaponRangePreview>()?.StopPreview();
        if (ownsPause && GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Playing &&
            !CampaignCompletionUI.IsOpen && !(PauseManager.Instance != null && PauseManager.Instance.IsPaused)) Time.timeScale = 1;
        ownsPause = false;
    }
    private void Prepare(string title, bool closeable, UnityEngine.Events.UnityAction dismiss = null, bool journalStyle = false)
    {
        GetComponent<WeaponRangePreview>()?.StopPreview();
        if (modal != null) { modal.gameObject.SetActive(false); Destroy(modal.gameObject); }
        ownsPause = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Playing &&
            (ownsPause || Time.timeScale > 0f);
        if (ownsPause) Time.timeScale = 0;
        illustratedJournal=journalStyle;
        journalPortrait=ResponsiveUi.Portrait;
        modal = ArcadeUiFactory.Stretch(canvasRoot, "JourneyModal");
        ArcadeUiFactory.Image(modal, null, ArcadeUiTheme.Backdrop).raycastTarget = true;
        Canvas.ForceUpdateCanvases();
        Vector2 dimensions=journalStyle?(journalPortrait?new Vector2(880,1370):new Vector2(1160,780)):new Vector2(860,620);
        var board = UiArtboard.Create(modal, dimensions+new Vector2(24,24),journalStyle);
        card = ArcadeUiFactory.At(board, "Card", Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero,
            dimensions);
        ArcadeUiFactory.Image(card, journalStyle?JournalCut():ArcadeKitAssets.RoundRect,
            journalStyle?JournalBase:ArcadeUiTheme.Alpha(ArcadeUiTheme.Panel, 0.98f), true);
        var shadow = card.gameObject.AddComponent<Shadow>();
        shadow.effectColor = ArcadeUiTheme.Shadow; shadow.effectDistance = new Vector2(0, -8);
        caption = Label(card, "Title", new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(card.sizeDelta.x - 100, 40), 26, TextAlignmentOptions.Center);
        caption.text = title;
        caption.font = ArcadeKitAssets.Display;
        caption.color = journalStyle?JournalPaper:MenuSheet.Ink;
        if(journalStyle)
        {
            caption.alignment=TextAlignmentOptions.MidlineLeft;
            JournalPlace(caption.rectTransform,26,20,dimensions.x-110,50);
            caption.fontSizeMax=caption.fontSize=34;
            JournalLine(card,26,78,dimensions.x-52,2,JournalGold);
        }
        if (closeable) WindowCloseButton.Create(card, dismiss ?? Close);
        var view = ArcadeUiFactory.Stretch(card, "Scroll", 20, 22, 20, journalStyle?150:80);
        var image = view.gameObject.AddComponent<Image>(); image.color = new Color(0, 0, 0, 0.01f);
        view.gameObject.AddComponent<RectMask2D>();
        var scroll = view.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.viewport = view;
        content = ArcadeUiFactory.Rect(view, "Content", new Vector2(0, 1), Vector2.one, new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);
        var layout = content.gameObject.AddComponent<VerticalLayoutGroup>(); layout.spacing = 12; layout.padding = new RectOffset(4, 4, 4, 4);
        layout.childControlHeight = true; layout.childControlWidth = true; layout.childForceExpandHeight = false;
        var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        scroll.content = content; scroll.movementType = ScrollRect.MovementType.Clamped;
    }
    private void Paragraph(string text, float height = 80)
    {
        var label = Label(content, "Text", new Vector2(0.5f, 1), Vector2.zero, new Vector2(700, height), 18, TextAlignmentOptions.TopLeft);
        var block = ArcadeUiFactory.At(content, "Entry", Vector2.up, Vector2.up, Vector2.zero, Vector2.zero);
        block.SetSiblingIndex(label.transform.GetSiblingIndex());
        ArcadeUiFactory.Image(block, ArcadeKitAssets.CutPanel, MenuSheet.Card, true);
        var layout = block.gameObject.AddComponent<LayoutElement>();
        label.rectTransform.SetParent(block, false);
        UiArtboard.Stretch(label.rectTransform);
        label.margin = new Vector4(16,8,16,8);
        label.text = text;
        label.textWrappingMode = TextWrappingModes.Normal;
        float width = Mathf.Max(140, card.rect.width - 110);
        layout.preferredHeight = Mathf.Max(height, label.GetPreferredValues(text, width, float.PositiveInfinity).y + 24);
    }
    private void RowButton(string text, Action action)
    {
        var button = ButtonAt(content, "Action", new Vector2(0.5f, 1), Vector2.zero, new Vector2(700, 56), text, action);
        button.gameObject.AddComponent<LayoutElement>().preferredHeight = 56;
    }
    public void ShowChoice(string title, string explanation, string[] choices, Action<int> selected)
    {
        journalOpen = false; Prepare(title, false); Paragraph(explanation, 115);
        for (int i = 0; i < choices.Length; i++)
        {
            int index = i;
            RowButton(choices[i], () => { Close(); selected(index); });
        }
    }
    public void OpenJournal()
    {
        if (CampaignCompletionUI.IsOpen) return;
        if (GameModeManager.IsPvp && GameStateManager.Instance != null &&
            GameStateManager.Instance.CurrentState == GameState.Playing) return;
        canvasRoot.gameObject.SetActive(true);
        journalOpen = true;
        Prepare(T("ПУТЬ БОЙЦА", "FIGHTER'S JOURNEY", "SAVAŞÇININ YOLU"), true, journalStyle:true);
        BuildJournalNavigation();
        if (tab == 0) BuildJournalOverview();
        if (tab == 1) BuildJournalWeapons();
        if (tab == 2) BuildJournalMaps();
        if (tab == 3) BuildJournalSynergies();
    }
    private void BuildPageNavigation()
    {
        string[] pages = { T("Звание / Итоги", "Rank / Results", "Rütbe / Sonuçlar"),
            T("Оружие", "Weapons", "Silahlar"), T("Карты", "Maps", "Haritalar"),
            T("Синергии", "Synergies", "Sinerjiler") };
        // Keep the page controls visible while the contents scroll independently.
        var viewport = (RectTransform)content.parent;
        viewport.offsetMax = new Vector2(viewport.offsetMax.x, -148f);
        var navigation = ArcadeUiFactory.Rect(card, "PageNavigation", new Vector2(0, 1), Vector2.one,
            new Vector2(0.5f, 1), new Vector2(0, -82), new Vector2(-40, 48));
        for (int i = 0; i < pages.Length; i++)
        {
            int page = i;
            float left = i / (float)pages.Length, right = (i + 1f) / pages.Length;
            var segment = ArcadeUiFactory.Rect(navigation, "Page" + i, new Vector2(left,0), new Vector2(right,1),
                Vector2.one * .5f, Vector2.zero, new Vector2(-6,0));
            var button = ArcadeUiFactory.Button(segment, ArcadeKitAssets.CutPanel, page == tab ? MenuSheet.Ochre : MenuSheet.Recess,
                pages[i], ArcadeKitAssets.BodyBold, 17, MenuSheet.Ink, () => { tab = page; OpenJournal(); });
            var text = button.GetComponentInChildren<TMP_Text>();
            text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = 17;
        }
    }
    private void BuildOverview()
    {
        Paragraph(RankName(journey.Data.rank) + "\n" + T("Звания сохраняются после поражений. Награды меняют внешний вид, а не силу.",
            "Ranks survive defeats. Rewards change your appearance, not your power.", "Rütbeler yenilgide korunur. Ödüller gücü değil görünümü değiştirir."), 85);
        string[] requirements = {
            T("Боец: зачистить волну 10 + 1 испытание оружия.", "Fighter: clear wave 10 + 1 weapon trial.", "Savaşçı: 10. dalga + 1 silah sınavı."),
            T("Ветеран: победа на 1 карте + 3 испытания оружия.", "Veteran: win on 1 map + 3 weapon trials.", "Kıdemli: 1 haritada zafer + 3 silah sınavı."),
            T("Мастер: победа на 2 картах + 5 испытаний + 3 разных босса без урона.", "Master: 2 map wins + 5 trials + 3 different untouched bosses.", "Usta: 2 harita zaferi + 5 sınav + 3 farklı hasarsız patron."),
            T("Легенда: 3 карты + 8 испытаний + 5 разных боссов без урона + волна 70.", "Legend: 3 map wins + 8 trials + 5 untouched bosses + clear wave 70.", "Efsane: 3 harita + 8 sınav + 5 hasarsız patron + 70. dalga.") };
        if (journey.Data.rank < 4) Paragraph(requirements[journey.Data.rank], 72);
        var text = new StringBuilder();
        int wins = 0, trials = 0; foreach (var m in journey.Data.maps) if (m.won) wins++;
        foreach (var t in journey.Data.trials) if (t.progress >= JourneyRules.TrialTarget(t.id)) trials++;
        text.AppendLine(T("Победные карты / испытания / чистые боссы: ", "Map wins / trials / untouched bosses: ", "Harita zaferi / sınav / hasarsız patron: ") + $"{wins} / {trials} / {journey.Data.cleanBossIds.Count}");
        text.AppendLine(T("Этот забег — зачищено волн: ", "This run — waves cleared: ", "Bu koşu — biten dalga: ") + journey.LastCleared);
        text.AppendLine(T("Уклонения / тайники: ", "Dodges / caches: ", "Kaçış / sandık: ") + journey.Dodges + " / " + journey.CachesWon);
        foreach (string highlight in journey.Highlights) text.AppendLine("• " + highlight);
        Paragraph(text.ToString(), 115 + journey.Highlights.Count * 40);
    }
    private void BuildWeapons()
    {
        Paragraph(T("Проходи испытания в забегах, чтобы открывать новые расцветки оружия. Здесь можно выбрать расцветку.",
            "Complete trials during runs to unlock weapon colors. Choose a color below.",
            "Koşularda sınavları tamamlayarak silah renklerini aç. Aşağıdan bir renk seç."), 95);
        var weapons = UpgradeManager.Instance?.GetAvailableWeapons(); if (weapons == null) return;
        foreach (var weapon in weapons)
        {
            if (weapon == null) continue;
            string id = weapon.WeaponName;
            var trial = journey.WeaponTrial(id);
            Paragraph(weapon.LocalizedName + $" — {trial.progress}/{JourneyRules.TrialTarget(id)}\n" + TrialDescription(id), 90);
            if (journey.TrialComplete(id)) RowButton((journey.Data.equippedTrial == id ? "✓ " : "") + T("Расцветка: ", "Weapon color: ", "Silah rengi: ") + weapon.LocalizedName,
                () => { journey.Equip(id); OpenJournal(); });
            if (GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Menu)
                RowButton(T("Попробовать: ", "Try: ", "Dene: ") + weapon.LocalizedName, () => ShowWeaponTrial(weapon));
        }
    }
    public static string TrialDescription(string id)
    {
        if (JourneyRules.UsesPiercingTrial(id)) return T("Попади одной пулей в 3 разных врагов (можно с улучшением пробития).", "Hit 3 different enemies with one bullet (piercing upgrades count).", "Tek mermiyle 3 farklı düşmanı vur (delme geliştirmeleri sayılır).");
        if (JourneyRules.UsesPairTrial(id)) return T("10 раз попади обоими залпами одной пары в одну цель.", "Hit the same target with both volleys of one pair 10 times.", "Bir çift atışın iki salvosuyla aynı hedefi 10 kez vur.");
        if (id == "Lightning SMG") return T("Порази молниями 25 целей.", "Hit 25 targets with lightning.", "Yıldırımla 25 hedef vur.");
        if (id == "Inferno Shotgun") return T("Убей 30 горящих противников.", "Kill 30 burning enemies.", "30 yanan düşman öldür.");
        if (id == "Golden Rifle" || id == "Assassin SMG") return T("Нанеси 40 критических попаданий.", "Land 40 critical hits.", "40 kritik vuruş yap.");
        if (id == "Minigun") return T("Нанеси 250 попаданий.", "Land 250 hits.", "250 vuruş yap.");
        return T("Победи 100 противников этим оружием.", "Defeat 100 enemies with this weapon.", "Bu silahla 100 düşman yen.");
    }
    private void BuildMaps()
    {
        Paragraph(T("Медали: волна 20 / победа на 50-й / особая задача за один забег. Пляж: 12 попаданий молниями по мокрым врагам. Горы: 12 разрушенных покровов. Остальные: волна 20, 8 точных уклонений и 1 тайник.",
            "Medals: wave 20 / victory at 50 / one-run challenge. Beach: 12 lightning hits on wet enemies. Mountains: break 12 ice covers. Others: wave 20, 8 precise dodges and 1 cache.",
            "Madalyalar: 20. dalga / 50. dalgada zafer / tek koşu görevi. Sahil: ıslak düşmana 12 yıldırım. Dağ: 12 buz kır. Diğerleri: 20. dalga, 8 tam kaçış ve 1 sandık."), 165);
        foreach (var map in journey.Data.maps)
            Paragraph(Lang.GetOr("map." + map.id, map.id) + "\n" + T("Rekor: ", "Best: ", "Rekor: ").Replace("Rekor", Lang.Code == LangCode.Ru ? "Рекорд" : "Rekor") + map.best +
                "    " + (map.best >= 20 ? "★" : "☆") + "  " + (map.won ? "★" : "☆") + "  " + (map.special ? "★" : "☆"), 80);
    }
    public static string BiasName(RunSpawnBias bias) => bias == RunSpawnBias.Fast ? T("Больше бегунов", "More runners", "Daha çok koşucu") :
        bias == RunSpawnBias.Tank ? T("Больше танков", "More tanks", "Daha çok tank") : T("Больше дальников", "More shooters", "Daha çok nişancı");
    private void BuildSynergies()
    {
        Paragraph(T("Все сочетания доступны с первого забега. Журнал сохраняет открытия и ничего не блокирует.",
            "Every combination is available from the first run. The journal records discoveries without locking effects.",
            "Her birleşim ilk koşudan itibaren açık. Günlük yalnızca keşifleri kaydeder."), 75);
        var synergies = UpgradeManager.Instance?.GetAllSynergies(); if (synergies == null) return;
        foreach (var synergy in synergies)
        {
            if (synergy == null) continue;
            bool known = journey.Data.discoveries.Contains(synergy.name);
            Paragraph((known ? "✓ " : "◇ ") + synergy.LocalizedName + "\n" + synergy.LocalizedDescription + "\n" + SynergyCombat.Description(synergy.name), 150);
        }
    }
    private void ShowWeaponTrial(WeaponData weapon)
    {
        Prepare(weapon.LocalizedName + " — " + T("ТИР", "RANGE", "ATIŞ ALANI"), true, OpenJournal);
        journalOpen = true;
        gameObject.GetComponent<WeaponRangePreview>()?.StopPreview();
        var range = gameObject.GetComponent<WeaponRangePreview>() ?? gameObject.AddComponent<WeaponRangePreview>();
        range.Build(content, weapon);
    }
    public void RefreshLang()
    {
        if (journalButton != null) journalButton.GetComponentInChildren<TMP_Text>().text = T("Путь бойца [J]", "Fighter's journey [J]", "Savaşçının yolu [J]");
        if (journalOpen) OpenJournal();
    }
    private static TMP_Text Label(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        var rect = ArcadeUiFactory.At(parent, name, anchor, anchor, position, size);
        var text = ArcadeUiFactory.Label(rect, "", ArcadeKitAssets.Body, fontSize, ArcadeUiTheme.TextPrimary, alignment);
        text.enableAutoSizing = true; text.fontSizeMin = 12; text.fontSizeMax = fontSize; return text;
    }
    private static Button ButtonAt(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, string text, Action action)
    {
        var rect = ArcadeUiFactory.At(parent, name, anchor, anchor, position, size);
        var button = ArcadeUiFactory.Button(rect, ArcadeKitAssets.RoundRect, ArcadeUiTheme.Alpha(ArcadeUiTheme.PanelRaised, 0.96f), text, ArcadeKitAssets.BodyBold, 18, ArcadeUiTheme.TextPrimary, () => action());
        var label = button.GetComponentInChildren<TMP_Text>(); label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = 18;
        return button;
    }
    private void OnDestroy() { if (current == this) current = null; }
}
