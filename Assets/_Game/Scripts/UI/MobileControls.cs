using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_WEBGL && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

/// <summary>Independent two-thumb controls, leaving the desktop input path untouched.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
public sealed class MobileControls : MonoBehaviour
{
    public enum TutorialHint { None, Move, Aim, Dash }
    private static TutorialHint currentHint;
    public static void SetTutorialHint(TutorialHint hint) => currentHint=hint;
    public static MobileControls Instance { get; private set; }
#if UNITY_EDITOR
    [SerializeField] private bool previewMobileInEditor;
#elif UNITY_WEBGL
    [DllImport("__Internal")]
    private static extern int BulletRushIsMobileBrowser();
    [DllImport("__Internal")] private static extern void BulletRushInstallTouchInput();
    [DllImport("__Internal")] private static extern int BulletRushReadTouchCount();
    [DllImport("__Internal")] private static extern int BulletRushTouchId(int index);
    [DllImport("__Internal")] private static extern float BulletRushTouchX(int index);
    [DllImport("__Internal")] private static extern float BulletRushTouchY(int index);
    [DllImport("__Internal")] private static extern float BulletRushTouchStartX(int index);
    [DllImport("__Internal")] private static extern float BulletRushTouchStartY(int index);
    [DllImport("__Internal")] private static extern int BulletRushTouchPressed(int index);
    private static bool browserMobile;
    private static float nextBrowserCheck = -1f;
#endif

    // Touch-enabled PCs keep desktop controls until an actual touch is used.
    // The editor normally behaves as a PC.
    public static bool IsTouchDevice
    {
        get
        {
#if UNITY_EDITOR
            return Instance != null && Instance.previewMobileInEditor;
#elif UNITY_WEBGL
            if (!browserMobile && Time.unscaledTime >= nextBrowserCheck)
            {
                nextBrowserCheck = Time.unscaledTime + .5f;
                browserMobile = BulletRushIsMobileBrowser() != 0;
            }
            return browserMobile;
#else
            return Application.isMobilePlatform;
#endif
        }
    }
    public static bool CanControl => IsTouchDevice && Instance!=null && Instance.controlsReady && Instance.isActiveAndEnabled && GameStateManager.Instance!=null &&
        GameStateManager.Instance.CurrentState==GameState.Playing && Time.timeScale>0f &&
        !RamLiftArena.InTransit && !JourneyUI.IsBlocking && !CampaignCompletionUI.IsOpen && !YG.YG2.isPauseGame &&
        !(PauseManager.Instance!=null && PauseManager.Instance.IsPaused);
    public static Vector2 MoveInput => CanControl && Instance!=null && Instance.move!=null?Instance.move.Value:Vector2.zero;
    public static Vector2 AimInput => CanControl && Instance!=null && Instance.aim!=null?Instance.aim.Value:Vector2.zero;
    // Holding the right half fires; the player controller chooses the aim target.
    public static bool IsFiring => CanControl && Instance.aim!=null && Instance.aim.IsHeld;

    private Canvas canvas;
    private RectTransform area, canvasRect;
    private MobileStick move,aim;
    private ActionButton dash,bomb,shield,pause,damageChoice,recoveryChoice;
    private PlayerController player;
    private AbilityManager abilities;
    private RectTransform choices;
    private RewardChoicePoint choice;
    private bool visible;
    private bool controlsReady, buildFailed;
    private Rect lastSafe;
    private Vector2 lastSize;
    private Button legacyPause;
    private bool legacyPauseHidden;
    private bool legacyPauseWasActive;
    private HashSet<int> previousContacts = new HashSet<int>();
    private HashSet<int> currentContacts = new HashSet<int>();
    private readonly List<ActionButton> pendingActions = new List<ActionButton>(4);
    private readonly List<ContactSample> contacts = new List<ContactSample>(10);
    private readonly List<RaycastResult> otherUiHits = new List<RaycastResult>(12);
    private EventSystem touchEventSystem;
    private PointerEventData uiProbe;

    private struct ContactSample
    {
        public int Id;
        public Vector2 Position;
        public Vector2 StartPosition;
        public bool Held;
    }

    private sealed class ActionButton
    {
        public RectTransform Rect;
        public Image Ring;
        public Button Button;
        public UnityAction Activate;
        public TMP_Text Time;
        public Image Icon;
        private AbilityKind? abilityKind;
        public void SetAbility(AbilityKind kind)
        {
            if (Icon != null && abilityKind != kind) { abilityKind=kind; Icon.sprite=AbilityPresentation.Icon(kind); }
        }
        public int Seconds = -1;
        public bool Contains(Vector2 screenPoint) => Rect.gameObject.activeInHierarchy &&
            RectTransformUtility.RectangleContainsScreenPoint(Rect,screenPoint,null);
        public void Show(bool active,float left,float total,bool ready)
        {
            if(Rect.gameObject.activeSelf!=active) Rect.gameObject.SetActive(active);
            if(!active) return;
            Button.interactable=ready;
            Ring.fillAmount=total>0f?1f-Mathf.Clamp01(left/total):1f;
            Ring.color=ready?new Color32(238,190,100,245):new Color32(145,158,157,220);
            int count=Mathf.CeilToInt(Mathf.Max(0,left));
            if(count!=Seconds)
            {
                Seconds=count;Time.gameObject.SetActive(count>0);
                if(count>0) Time.SetText("{0}",count);
            }
        }
    }

    public void BindPause(Button previousPause) { legacyPause=previousPause; }
    private void Awake()
    {
        Instance=this;
#if UNITY_WEBGL && !UNITY_EDITOR
        BulletRushInstallTouchInput();
#endif
    }

    private bool HasUiReferences => canvas!=null && canvasRect!=null && area!=null && move!=null && aim!=null &&
        choices!=null && ButtonCreated(dash,true) && ButtonCreated(bomb,true) && ButtonCreated(shield,true) &&
        ButtonCreated(pause,false) && ButtonCreated(damageChoice,false) && ButtonCreated(recoveryChoice,false);

    private static bool ButtonCreated(ActionButton button,bool hasCooldown) => button!=null &&
        button.Rect!=null && button.Button!=null && (!hasCooldown || (button.Ring!=null && button.Time!=null));

    private bool EnsureBuilt()
    {
        if(controlsReady && HasUiReferences) return true;
        // A failed construction must never leave a partially built canvas in use.
        if(canvas!=null || controlsReady) ClearBuiltControls();
        if(buildFailed) return false;
        try
        {
            Build();
            if(!HasUiReferences) throw new System.InvalidOperationException("Mobile controls were not fully created.");
            controlsReady=true;
            return true;
        }
        catch(System.Exception error)
        {
            ClearBuiltControls();
            buildFailed=true;
            Debug.LogException(error,this);
            return false;
        }
    }

    private void Build()
    {
        GameObject go=new GameObject("Touch controls",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
        canvas=go.GetComponent<Canvas>(); canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=26;
        canvasRect=(RectTransform)go.transform;
        ResponsiveUi.Configure(go.GetComponent<CanvasScaler>(),new Vector2(1920,1080));
        area=ArcadeUiFactory.Stretch(go.transform,"Safe area");
        move=Stick("Move",new Vector2(0,0),new Vector2(170,164),UpgradeType.MoveSpeed);
        aim=Stick("Aim and fire",new Vector2(1,0),new Vector2(-170,164),UpgradeType.Weapon);
        dash=Action("Dash",UpgradeType.MoveSpeed,new Vector2(1,0),new Vector2(-390,178),144,()=>player?.TryDash());
        bomb=Action("Ability E",UpgradeType.Bomb,new Vector2(1,0),new Vector2(-338,354),132,()=>abilities?.TryUseActive(1));
        shield=Action("Ability Q",UpgradeType.Shield,new Vector2(1,0),new Vector2(-145,370),132,()=>abilities?.TryUseActive(0));
        var pauseRect=ArcadeUiFactory.At(area,"Pause",new Vector2(0,1),Vector2.one*.5f,new Vector2(64,-62),new Vector2(108,108));
        pause=MakeButton(pauseRect,()=>PauseManager.Instance?.TogglePause());
        ArcadeUiFactory.Label(pauseRect,"Symbol","II",ArcadeKitAssets.BodyBold,35,MenuSheet.Paper,TextAlignmentOptions.Center,
            Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(60,60));
        choices=ArcadeUiFactory.At(area,"Reward choices",new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(0,35),new Vector2(300,128));
        damageChoice=ChoiceButton("Damage reward",UpgradeType.Damage,0,new Vector2(-74,59));
        recoveryChoice=ChoiceButton("Recovery reward",UpgradeType.HealthRegen,1,new Vector2(74,59));
        choices.gameObject.SetActive(false);
        Canvas.ForceUpdateCanvases();
        Reflow();area.gameObject.SetActive(false);
    }

    private MobileStick Stick(string name,Vector2 anchor,Vector2 position,UpgradeType icon)
    {
        RectTransform rect=ArcadeUiFactory.At(area,name,anchor,Vector2.one*.5f,position,new Vector2(264,264));
        Image baseImage=ArcadeUiFactory.Image(rect,CombatCircleArt.Disc,new Color32(32,42,44,115));
        // A new Image already uses the full rectangular hit area (threshold 0).
        // Setting alphaHitTestMinimumThreshold even to zero throws on the
        // non-readable runtime sprite in recent versions of uGUI.
        baseImage.raycastTarget=false; // Floating sticks are routed directly, not by UI raycasts.
        Image outline=ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(rect,"Outline"),CombatCircleArt.Ring,new Color32(236,224,190,115));
        RectTransform knob=ArcadeUiFactory.At(rect,"Handle",Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(100,100));
        ArcadeUiFactory.Image(knob,CombatCircleArt.Disc,new Color32(56,69,70,205));
        ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(knob,"Handle rim"),CombatCircleArt.Ring,new Color32(238,224,184,210));
        var picture=ArcadeUiFactory.Image(ArcadeUiFactory.At(knob,"Symbol",Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(45,45)),MenuSheetArt.Upgrade(icon),Color.white);
        picture.preserveAspect=true;
        var stick=rect.gameObject.AddComponent<MobileStick>();stick.Bind(knob,outline);return stick;
    }

    private ActionButton Action(string name,UpgradeType icon,Vector2 anchor,Vector2 pos,float size,UnityAction action)
    {
        var rect=ArcadeUiFactory.At(area,name,anchor,Vector2.one*.5f,pos,Vector2.one*size);
        ActionButton button=MakeButton(rect,action);
        Image ring=ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(rect,"Readiness"),CombatCircleArt.Ring,new Color32(238,190,100,245));
        ring.type=Image.Type.Filled;ring.fillMethod=Image.FillMethod.Radial360;ring.fillOrigin=(int)Image.Origin360.Top;
        var picture=ArcadeUiFactory.Image(ArcadeUiFactory.At(rect,"Icon",Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(61,61)),MenuSheetArt.Upgrade(icon),Color.white);
        picture.preserveAspect=true;
        var timer=ArcadeUiFactory.Label(rect,"Cooldown","",ArcadeKitAssets.Display,30,MenuSheet.Paper,TextAlignmentOptions.Center,
            Vector2.one*.5f,Vector2.one*.5f,new Vector2(0,-27),new Vector2(66,34));
        timer.outlineColor=MenuSheet.Ink;timer.outlineWidth=.18f;
        timer.gameObject.SetActive(false);
        button.Ring=ring;button.Time=timer;button.Icon=picture;
        return button;
    }

    private static ActionButton MakeButton(RectTransform r,UnityAction action)
    {
        Image bg=ArcadeUiFactory.Image(r,CombatCircleArt.Disc,new Color32(34,43,45,215));
        bg.raycastTarget=true;
        Button button=r.gameObject.AddComponent<Button>();button.targetGraphic=bg;button.navigation=new Navigation{mode=Navigation.Mode.None};
        // Actions are dispatched by the contact router on touch-down. A UI click
        // would fire a second time on release, including on WebGL touch devices.
        button.transition=Selectable.Transition.ColorTint;
        return new ActionButton{Rect=r,Button=button,Activate=action};
    }
    private ActionButton ChoiceButton(string name,UpgradeType icon,int index,Vector2 pos)
    {
        var r=ArcadeUiFactory.At(choices,name,Vector2.one*.5f,Vector2.one*.5f,pos,new Vector2(93,93));
        var button=MakeButton(r,()=> { if(choice!=null) choice.TryChooseMobile(index); });
        ArcadeUiFactory.Image(ArcadeUiFactory.At(r,"Effect",Vector2.one*.5f,Vector2.one*.5f,Vector2.zero,new Vector2(51,51)),MenuSheetArt.Upgrade(icon),Color.white).preserveAspect=true;
        return button;
    }

    public static void SetRewardChoice(RewardChoicePoint point,bool nearby)
    {
        if(Instance==null) return;
        if(nearby) Instance.choice=point;
        else if(Instance.choice==point) Instance.choice=null;
    }

    private void Reflow()
    {
        if(Screen.width<1||Screen.height<1||!HasUiReferences) return;
        lastSafe=ResponsiveUi.ScreenSafeArea;lastSize=canvasRect.rect.size;
        area.anchorMin=new Vector2(lastSafe.xMin/Screen.width,lastSafe.yMin/Screen.height);
        area.anchorMax=new Vector2(lastSafe.xMax/Screen.width,lastSafe.yMax/Screen.height);
        area.offsetMin=area.offsetMax=Vector2.zero;
        float width=Mathf.Max(1f,lastSize.x*lastSafe.width/Screen.width);
        float height=Mathf.Max(1f,lastSize.y*lastSafe.height/Screen.height);
        // Reserve space between the two thumb clusters. No minimum scale that
        // could force controls off a particularly small/square viewport.
        float scale=Mathf.Min(1.22f,Mathf.Min(width/980f,height/720f));
        move.SetHome(new Vector2(170,164)*scale,scale);
        aim.SetHome(new Vector2(-170,164)*scale,scale);
        Place(dash.Rect,new Vector2(-390,178),scale);
        Place(bomb.Rect,new Vector2(-338,354),scale);
        Place(shield.Rect,new Vector2(-145,370),scale);
        Place(pause.Rect,new Vector2(64,-62),scale);
        // In portrait, put contextual choices above the right-thumb cluster.
        choices.anchoredPosition=new Vector2(0f,(width<height*1.1f?450f:35f)*scale);
        choices.localScale=Vector3.one*scale;
        move.ResetStick();
        aim.ResetStick();
    }
    private static void Place(RectTransform rect,Vector2 pos,float scale)
    { rect.anchoredPosition=pos*scale;rect.localScale=Vector3.one*scale; }

    private void Update()
    {
#if UNITY_EDITOR
        // F8 allows a quick mouse / Device Simulator preview in the editor.
        if(Keyboard.current!=null && Keyboard.current.f8Key.wasPressedThisFrame)
            previewMobileInEditor=!previewMobileInEditor;
#endif
        bool mobile=IsTouchDevice;
        if(mobile && !EnsureBuilt()) return;
        if(!mobile) buildFailed=false; // Allow a new attempt after switching modes in the editor.
        if(!controlsReady || !HasUiReferences) { RestoreDesktopPause(); return; }
        bool show=CanControl;
        bool previouslyVisible=visible;
        if(visible!=show)
        {
            visible=show;area.gameObject.SetActive(show);move.ResetStick();aim.ResetStick();
        }
        if (mobile && legacyPause != null)
        {
            if (!legacyPauseHidden) legacyPauseWasActive = legacyPause.gameObject.activeSelf;
            legacyPauseHidden = true;
            if (legacyPause.gameObject.activeSelf) legacyPause.gameObject.SetActive(false);
        }
        else RestoreDesktopPause();
        if(!show){ReadContacts(false);return;}
        if(lastSafe!=ResponsiveUi.ScreenSafeArea || lastSize!=canvasRect.rect.size) Reflow();
        if(player==null || !player.gameObject.activeInHierarchy)
        {
            player=PlayerHealth.Instance!=null?PlayerHealth.Instance.GetComponent<PlayerController>():null;
            abilities=player!=null?player.GetComponent<AbilityManager>():null;
        }
        dash.Show(player!=null,player!=null?player.DashCooldownRemaining:0f,player!=null?player.DashCooldown:1f,
            player!=null && player.DashCooldownRemaining<=0f && !player.IsDashing);
        move.SetTutorialHighlight(currentHint==TutorialHint.Move);
        aim.SetTutorialHighlight(currentHint==TutorialHint.Aim);
        if(currentHint==TutorialHint.Dash && dash.Button.interactable)
            dash.Ring.color=Color.Lerp(new Color32(238,190,100,245),new Color32(255,246,171,255),
                Mathf.PingPong(Time.unscaledTime*2f,1f));
        AbilityManager.SlotInfo s=abilities!=null && !GameModeManager.IsPvp?abilities.GetActiveSlot(0):default;
        AbilityManager.SlotInfo b=abilities!=null && !GameModeManager.IsPvp?abilities.GetActiveSlot(1):default;
        if(s.Exists) shield.SetAbility(s.Kind); if(b.Exists) bomb.SetAbility(b.Kind);
        shield.Show(s.Exists,s.Remaining,s.Duration,s.Ready);
        bomb.Show(b.Exists,b.Remaining,b.Duration,b.Ready);
        bool showChoice=choice!=null && choice.isActiveAndEnabled;
        if(choices.gameObject.activeSelf!=showChoice) choices.gameObject.SetActive(showChoice);
        ReadContacts(previouslyVisible);
    }

    private void ReadContacts(bool acceptNew)
    {
        if(!controlsReady || move==null || aim==null) return;
        currentContacts.Clear();
        pendingActions.Clear();
        contacts.Clear();
#if UNITY_WEBGL && !UNITY_EDITOR
        int count=BulletRushReadTouchCount();
        if(count>=0)
        {
            for(int i=0;i<count;i++)
                CollectContact(BulletRushTouchId(i),new Vector2(BulletRushTouchX(i)*Screen.width,
                    BulletRushTouchY(i)*Screen.height),new Vector2(BulletRushTouchStartX(i)*Screen.width,
                    BulletRushTouchStartY(i)*Screen.height),BulletRushTouchPressed(i)!=0);
        }
        else ReadNativeContacts();
#else
        ReadNativeContacts();
#endif
        if(move.IsHeld && !currentContacts.Contains(move.PointerId)) move.ResetStick();
        if(aim.IsHeld && !currentContacts.Contains(aim.PointerId)) aim.ResetStick();
        foreach(var contact in contacts) Contact(contact.Id,contact.Position,contact.StartPosition,contact.Held,acceptNew);
        var old=previousContacts;previousContacts=currentContacts;currentContacts=old;
        // Update both sticks first, so a simultaneous dash uses the current movement.
        foreach(var button in pendingActions)
            if(CanControl && button.Button.interactable) button.Activate?.Invoke();
    }

    private void ReadNativeContacts()
    {
        bool touched=false;
        var touchScreen=Touchscreen.current;
        if(touchScreen!=null)
        {
            foreach(var touch in touchScreen.touches)
            {
                bool held=touch.press.isPressed;
                if(!held && !touch.press.wasPressedThisFrame) continue;
                touched=true;
                CollectContact(touch.touchId.ReadValue(),touch.position.ReadValue(),touch.startPosition.ReadValue(),held);
            }
        }
#if UNITY_EDITOR
        var mouse=Mouse.current;
        if(!touched && mouse!=null && (mouse.leftButton.isPressed || mouse.leftButton.wasPressedThisFrame))
            CollectContact(-2,mouse.position.ReadValue(),mouse.position.ReadValue(),mouse.leftButton.isPressed);
#endif
    }

    private void CollectContact(int id,Vector2 screenPoint,Vector2 startPoint,bool held)
    {
        if(held) currentContacts.Add(id);
        contacts.Add(new ContactSample{Id=id,Position=screenPoint,StartPosition=startPoint,Held=held});
    }

    private void Contact(int id,Vector2 screenPoint,Vector2 startPoint,bool held,bool acceptNew)
    {
        if(move.PointerId==id){if(held) move.UpdateContact(screenPoint);return;}
        if(aim.PointerId==id){if(held) aim.UpdateContact(screenPoint);return;}
        if(!acceptNew || previousContacts.Contains(id)) return;
        if(QueueButton(dash,startPoint) || QueueButton(bomb,startPoint) || QueueButton(shield,startPoint) ||
           QueueButton(pause,startPoint) || QueueButton(damageChoice,startPoint) || QueueButton(recoveryChoice,startPoint)) return;
        if(!held || !lastSafe.Contains(startPoint) || IsOverOtherUi(startPoint)) return;
        // A contact belongs to the half where it began until release, even if
        // it later crosses the middle of the screen. Each half owns one finger.
        MobileStick stick=startPoint.x<lastSafe.center.x?move:aim;
        if(stick.TryCapture(id,startPoint)) stick.UpdateContact(screenPoint);
    }

    private bool IsOverOtherUi(Vector2 screenPoint)
    {
        EventSystem events=EventSystem.current;
        if(events==null || !events.isActiveAndEnabled) return false;
        if(touchEventSystem!=events || uiProbe==null)
        {
            touchEventSystem=events;
            uiProbe=new PointerEventData(events);
        }
        uiProbe.Reset();
        uiProbe.position=screenPoint;
        otherUiHits.Clear();
        events.RaycastAll(uiProbe,otherUiHits);
        foreach(var hit in otherUiHits)
        {
            if(hit.gameObject==null || hit.gameObject.transform.IsChildOf(area)) continue;
            if(hit.gameObject.GetComponentInParent<Selectable>()!=null ||
                hit.gameObject.GetComponentInParent<ScrollRect>()!=null) return true;
        }
        return false;
    }

    private bool QueueButton(ActionButton button,Vector2 screenPoint)
    {
        if(button==null || !button.Contains(screenPoint)) return false;
        if(button.Button.interactable) pendingActions.Add(button);
        return true;
    }

    public static Vector3 WorldDirection(Vector2 input,Camera camera)
    {
        if(camera==null) return new Vector3(input.x,0,input.y);
        Vector3 right=camera.transform.right;right.y=0f;
        if(right.sqrMagnitude<.001f) right=Vector3.right;else right.Normalize();
        return right*input.x+Vector3.Cross(right,Vector3.up)*input.y;
    }

    private void RestoreDesktopPause()
    {
        if (!legacyPauseHidden) return;
        legacyPauseHidden = false;
        if (legacyPause != null) legacyPause.gameObject.SetActive(legacyPauseWasActive);
    }

    private void ResetInput(){if(move!=null) move.ResetStick();if(aim!=null) aim.ResetStick();}

    private void ClearBuiltControls()
    {
        ResetInput();
        controlsReady=false;
        visible=false;
        if(canvas!=null)
        {
            canvas.gameObject.SetActive(false);
            Destroy(canvas.gameObject);
        }
        canvas=null; canvasRect=null; area=null;
        move=null; aim=null; choices=null;
        dash=bomb=shield=pause=damageChoice=recoveryChoice=null;
        previousContacts.Clear(); currentContacts.Clear(); contacts.Clear(); pendingActions.Clear();
        RestoreDesktopPause();
    }
    private void OnApplicationFocus(bool focus){if(!focus) ResetInput();}
    private void OnApplicationPause(bool paused){if(paused) ResetInput();}
    private void OnDisable()
    {
        ResetInput();
        visible=false;
        if(area!=null) area.gameObject.SetActive(false);
        RestoreDesktopPause();
    }
    private void OnDestroy(){if(Instance==this)Instance=null;ClearBuiltControls();}
}
