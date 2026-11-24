using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CooldownUI : MonoBehaviour
{
    // Existing scenes use these references to locate the HUD being replaced.
    [SerializeField] private Slider dashSlider, fireSlider, bombSlider, shieldSlider;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Weapon weapon;
    [SerializeField] private AbilityManager abilityManager;

    private RectTransform row, autoRow;
    private Slot fire, dash, shield, bomb;
    private readonly Slot[] automatic = new Slot[AbilityPresentation.AutomaticSlots];
    private int visibleAutomatic = -1;
    private bool autoLayoutInitialized, autoMobile;
    private float nextLookup;
    private int visibleSlots = -1;
    private LangCode language;

    private sealed class Slot
    {
        public readonly RectTransform Rect;
        public readonly TMP_Text Key;
        private readonly Image iconImage;
        private readonly Image meter;
        private readonly TMP_Text timer;
        private readonly Button button;
        private int lastSeconds = -1;
        private int lastFill = -1;
        private AbilityKind? abilityKind;
        public void SetAbility(AbilityKind kind)
        {
            if (abilityKind == kind) return;
            abilityKind = kind; iconImage.sprite = AbilityPresentation.Icon(kind);
        }

        public Slot(RectTransform parent, string name, UpgradeType icon, string key, UnityAction action = null)
        {
            var center = Vector2.one * .5f;
            Rect = ArcadeUiFactory.At(parent, name, center, center, Vector2.zero, new Vector2(68,64));
            var face = ArcadeUiFactory.Image(Rect, ArcadeKitAssets.CutPanel, MenuSheet.Ink, true);
            if (action != null)
            {
                face.raycastTarget = true;
                button = Rect.gameObject.AddComponent<Button>();
                button.targetGraphic = face;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(action);
            }
            var art = ArcadeUiFactory.At(Rect, "Icon", center, center, new Vector2(0,9), new Vector2(36,36));
            iconImage = ArcadeUiFactory.Image(art, MenuSheetArt.Upgrade(icon), Color.white);
            iconImage.preserveAspect = true;
            Key = ArcadeUiFactory.Label(Rect, "Key", key, ArcadeKitAssets.BodyBold, 15, MenuSheet.Paper,
                TextAlignmentOptions.Center, center, center, new Vector2(0,-16), new Vector2(62,20));
            var bar = ArcadeUiFactory.At(Rect, "Readiness", new Vector2(.5f,0), new Vector2(.5f,0),
                new Vector2(0,3), new Vector2(52,3));
            ArcadeUiFactory.Image(bar, null, ArcadeUiTheme.TextMuted);
            meter = ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(bar, "Fill"), ArcadeKitAssets.RoundRect, MenuSheet.Sage);
            meter.type = Image.Type.Filled;
            meter.fillMethod = Image.FillMethod.Horizontal;
            if (button != null)
            {
                timer = ArcadeUiFactory.Label(art, "Seconds", "", ArcadeKitAssets.Display, 21, MenuSheet.Paper,
                    TextAlignmentOptions.Center, center, center, Vector2.zero, new Vector2(52,34));
                timer.raycastTarget = false;
                timer.gameObject.SetActive(false);
            }
        }

        public void Show(float progress, float left, bool available = true)
        {
            int step = Mathf.RoundToInt(Mathf.Clamp01(progress) * 52);
            if (step != lastFill)
            {
                meter.fillAmount = step / 52f;
                lastFill = step;
            }
            if (button == null) return;
            bool ready = left <= 0 && available;
            if (button.interactable != ready) button.interactable = ready;
            int seconds = Mathf.CeilToInt(Mathf.Max(0,left));
            if (seconds == lastSeconds) return;
            lastSeconds = seconds;
            timer.gameObject.SetActive(seconds > 0);
            iconImage.color = seconds > 0 ? new Color(1,1,1,.25f) : Color.white;
            if (seconds > 0) timer.SetText("{0}", seconds);
        }
    }

    private void Start()
    {
        // CooldownUI itself lives on the scene Canvas, outside the visible HUD.
        var host = transform.Find("HUD/GameplayUI") as RectTransform;
        if (host == null && dashSlider != null) host = dashSlider.transform.parent.parent as RectTransform;
        if (host == null) return;
        var old = host.Find("CooldownUI");
        if (old != null) old.gameObject.SetActive(false);
        foreach (var slider in new[] { dashSlider, fireSlider, bombSlider, shieldSlider })
            if (slider != null) slider.gameObject.SetActive(false);
        var bottom = new Vector2(.5f,0);
        row = ArcadeUiFactory.At(host, "Actions", bottom, bottom, new Vector2(0,22), new Vector2(300,64));
        fire = new Slot(row, "Fire", UpgradeType.Weapon, "");
        dash = new Slot(row, "Dash", UpgradeType.MoveSpeed, "");
        shield = new Slot(row, "Ability Q", UpgradeType.Shield, "Q", () => abilityManager?.TryUseActive(0));
        bomb = new Slot(row, "Ability E", UpgradeType.Bomb, "E", () => abilityManager?.TryUseActive(1));
        autoRow = ArcadeUiFactory.At(host, "Automatic abilities", bottom, bottom, new Vector2(0,94), new Vector2(228,64));
        for(int i=0;i<automatic.Length;i++) automatic[i]=new Slot(autoRow,"Automatic "+i,UpgradeType.Special,"AUTO");
        RefreshKeys();
        row.gameObject.SetActive(false);
        autoRow.gameObject.SetActive(false);
    }

    private void RefreshKeys()
    {
        language = Lang.Code;
        fire.Key.text = GameplayJourney.T("ЛКМ", "LMB", "Sol fare");
        dash.Key.text = GameplayJourney.T("Пробел", "Space", "Boşluk");
    }

    private void LateUpdate()
    {
        if (row == null) return;
        bool playing = GameStateManager.Instance != null && GameStateManager.Instance.CurrentState == GameState.Playing &&
            !GameModeManager.IsPvp && Time.timeScale > 0 && !RamLiftArena.InTransit;
        bool desktop=playing&&!MobileControls.IsTouchDevice;
        if (row.gameObject.activeSelf != desktop) row.gameObject.SetActive(desktop);
        if (!playing) { if(autoRow.gameObject.activeSelf) autoRow.gameObject.SetActive(false); return; }
        if (language != Lang.Code) RefreshKeys();
        if ((abilityManager == null || weapon == null || playerController == null) && Time.unscaledTime >= nextLookup)
        {
            nextLookup = Time.unscaledTime + .4f;
            if (playerController == null) playerController = FindAnyObjectByType<PlayerController>();
            if (playerController != null)
            {
                if (weapon == null) weapon = playerController.GetComponentInChildren<Weapon>();
                if (abilityManager == null) abilityManager = playerController.GetComponent<AbilityManager>();
            }
        }

        var first=abilityManager!=null?abilityManager.GetActiveSlot(0):default;
        var second=abilityManager!=null?abilityManager.GetActiveSlot(1):default;
        bool hasShield = first.Exists, hasBomb = second.Exists;
        UpdateAutomatic();
        if(!desktop) return;
        int mask = (weapon != null ? 1 : 0) | (playerController != null ? 2 : 0) | (hasShield ? 4 : 0) | (hasBomb ? 8 : 0);
        if (mask != visibleSlots)
        {
            visibleSlots = mask;
            int count = (weapon != null ? 1 : 0) + (playerController != null ? 1 : 0) + (hasShield ? 1 : 0) + (hasBomb ? 1 : 0);
            float x = -(count - 1) * 38f;
            Place(fire, weapon != null, ref x);
            Place(dash, playerController != null, ref x);
            Place(shield, hasShield, ref x);
            Place(bomb, hasBomb, ref x);
        }
        if (weapon != null) fire.Show(weapon.IsReloading ? weapon.ReloadProgress : weapon.FireCooldownProgress, 0);
        if (playerController != null) dash.Show(Progress(playerController.DashCooldownRemaining, playerController.DashCooldown), 0);
        if (hasShield)
        {
            shield.SetAbility(first.Kind);
            shield.Show(Progress(first.Remaining, first.Duration), first.Remaining, first.Ready);
        }
        if (hasBomb)
        {
            bomb.SetAbility(second.Kind);
            bomb.Show(Progress(second.Remaining, second.Duration), second.Remaining, second.Ready);
        }
    }

    private void UpdateAutomatic()
    {
        int count=abilityManager!=null?abilityManager.AutomaticCount:0;
        if(autoRow.gameObject.activeSelf!=(count>0)) autoRow.gameObject.SetActive(count>0);
        if(count==0) return;
        bool mobile=MobileControls.IsTouchDevice;
        if(!autoLayoutInitialized||autoMobile!=mobile)
        {
            autoLayoutInitialized=true; autoMobile=mobile;
            autoRow.anchoredPosition=new Vector2(0,mobile?18:94);
            autoRow.localScale=Vector3.one*(mobile ? .77f : 1f);
        }
        if(visibleAutomatic!=count)
        {
            visibleAutomatic=count; float x=-(count-1)*38f;
            for(int i=0;i<automatic.Length;i++) Place(automatic[i],i<count,ref x);
        }
        for(int i=0;i<count;i++)
        {
            var info=abilityManager.GetAutomaticSlot(i);
            automatic[i].SetAbility(info.Kind);
            automatic[i].Show(Progress(info.Remaining,info.Duration),info.Remaining);
        }
    }

    private static float Progress(float left, float duration) => duration > 0 ? 1 - left / duration : 1;
    private static void Place(Slot slot, bool visible, ref float x)
    {
        slot.Rect.gameObject.SetActive(visible);
        if (!visible) return;
        slot.Rect.anchoredPosition = new Vector2(x,0);
        x += 76;
    }
}
