using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed partial class ArcadeMenuSkin
{
    private enum HubView { Home, Armory, Character, Common, Workbench, Maps }
    private HubView hubView;
    private RectTransform hubBoard, hubHome, hubPage, hubHeader, hubModes, hubRewards, hubPlayGroup;
    private RectTransform hubLeft, hubRight, hubCarousel, hubActions, hubDragArea;
    private CanvasGroup hubHomeFade,hubPageFade;
    private TMP_Text hubCampCaption;
    private Button hubBack, hubAction, hubSecondary, hubPrevious, hubNext, hubRecWeapon, hubRecAbility;
    private TMP_Text hubLogo, hubPageTitle, hubName, hubDetail, hubRightTitle, hubRightText, hubCounter, hubActionText, hubSecondaryText;
    private readonly Button[] hubStations = new Button[4];
    private readonly Button[] hubSlots = new Button[5];
    private readonly Image[] hubSlotIcons = new Image[5];
    private readonly TMP_Text[] hubSlotNames = new TMP_Text[5];
    private readonly TMP_Text[] hubSlotStates = new TMP_Text[5];
    private bool? hubPortrait;
    private float hubAspect;
    private static Sprite hubVignette;
    private int hubIndex, hubFirst;
    private GameMap hubReturnMap;
    private readonly List<ScriptableObject> hubItems = new List<ScriptableObject>();
    private float hubPreviewYaw;
    private float hubLastDrag;
    private static readonly Color HubShade = new Color(.10f,.15f,.12f,.88f);
    private static readonly Color HubMuted = new Color(.75f,.79f,.69f,1);
    private static readonly Vector3[] HubStationPositions =
    {
        new Vector3(-3.25f, 2.53f, 1.48f),
        new Vector3(0, 1.35f, 0),
        new Vector3(3.2f, 2.53f, 1.78f),
        new Vector3(2.8f, 1.42f, -1.45f)
    };

    private void BuildCampMenu(RectTransform safe)
    {
        campSafeArea = safe;
        Image vignette=ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(safe,"Camp atmosphere"),HubVignette(),Color.white);
        vignette.raycastTarget=false;
        hubBoard = UiArtboard.Create(safe, new Vector2(1600,900), true);
        hubHome = ArcadeUiFactory.Stretch(hubBoard, "Camp clearing");
        hubPage = ArcadeUiFactory.Stretch(hubBoard, "Station details");
        hubHomeFade=hubHome.gameObject.AddComponent<CanvasGroup>();
        hubPageFade=hubPage.gameObject.AddComponent<CanvasGroup>();
        hubLogo = CampLabel(hubHome, "Logo", 30, 22, 495, 74, "BULLETRUSH", 65, CampPaper, ArcadeKitAssets.Display);
        hubLogo.fontStyle = FontStyles.Italic;
        HubTextShadow(hubLogo);
        hubCampCaption=CampLabel(hubHome,"Camp caption",34,96,437,28,"",20,CampGold,ArcadeKitAssets.BodyBold);
        AddLangBinding(hubCampCaption,()=>CampText("П О Л Е В О Й   Л А Г Е Р Ь","F I E L D   C A M P","S A H R A   K A M P I"));
        HubTextShadow(hubCampCaption);
        hubHeader = CampRect(hubBoard, "Player resources", 1068, 24, 504, 84);
        BuildCampHeader(hubHeader);
        hubStations[0] = HubStation("Armory",CampGun(),()=>CampText("ОРУЖЕЙНАЯ","ARMORY","SILAHLAR"),OpenEquipment);
        hubStations[1] = HubStation("Fighter",ArcadeKitAssets.Icon("person"),()=>CampText("БОЕЦ","FIGHTER","SAVASCI"),()=>OpenHub(HubView.Character));
        hubStations[2] = HubStation("Workbench",CampUpgrades(),()=>CampText("МАСТЕРСКАЯ","WORKSHOP","ATOLYE"),OpenUpgrades);
        hubStations[3] = HubStation("MapTable",CampMap(),()=>CampText("КАРТЫ","MAPS","HARITALAR"),OpenMaps);
        equipmentCard = new RailCard { Button = hubStations[0] };
        upgradesCard = new RailCard { Button = hubStations[2] };

        hubModes = CampRect(hubHome,"Run options",28,737,418,132);
        pveModeButton = CampAction(hubModes,"PVE",0,0,104,45,CampGold,null,()=>"PVE",SelectPveMode,25,0,false);
        pveModeLabel = pveModeButton.GetComponentInChildren<TMP_Text>();
        pvpModeButton = CampAction(hubModes,"PVP",112,0,104,45,CampButtonColor,null,()=>"PVP",SelectPvpMode,25,0,false);
        pvpModeLabel = pvpModeButton.GetComponentInChildren<TMP_Text>();
        modifierButton = CampAction(hubModes,"Modifier",0,56,216,54,CampButtonColor,ArcadeKitAssets.Icon("flame"),null,CycleModifier,23,28,false);
        modifierValueText = modifierButton.GetComponentInChildren<TMP_Text>();
        campMapFrame = CampRect(hubModes,"Map chip",228,0,190,110);
        Button mapChip = HubButton(campMapFrame,"CurrentMap",0,0,190,110,HubShade,null,null,OpenMaps,28);
        mapNameText = mapChip.GetComponentInChildren<TMP_Text>();

        hubPlayGroup = CampRect(hubHome,"Start run",570,748,460,124);
        playButton = CampAction(hubPlayGroup,"Play",0,0,460,110,CampGold,ArcadeKitAssets.Icon("play"),()=>CampText("В БОЙ","PLAY","OYNA"),Play,65,72);
        playButton.GetComponentInChildren<TMP_Text>().fontStyle = FontStyles.Italic;
        hubRewards = CampRect(hubHome,"Camp journal",1154,736,420,132);
        dailyCard = new RailCard { Button = CampAction(hubRewards,"Rewards",0,0,205,55,CampButtonColor,ArcadeKitAssets.Icon("gift"),()=>CampText("Награды","Rewards","Oduller"),OpenDaily,25,32) };
        dailyCard.Root = dailyCard.Button.gameObject;
        dailyCard.Badge = CampBadge(dailyCard.Button.transform);
        missionsTile = HubSmallTile(hubRewards,"Missions",213,0,ArcadeKitAssets.Icon("scroll"),()=>CampText("Задания","Missions","Gorevler"),OpenMissions);
        achievementsTile = HubSmallTile(hubRewards,"Achievements",0,63,ArcadeKitAssets.Icon("trophy"),()=>CampText("Достижения","Achievements","Basarimlar"),OpenAchievements);
        CampAction(hubRewards,"More",213,63,205,55,CampButtonColor,ArcadeKitAssets.Icon("plus"),()=>CampText("Ещё","More","Daha fazla"),OpenCampExtras,25,32);

        BuildHubPage();
        BuildCampExtras(hubBoard);
        FitHubLayout();
        ResetHubView();
    }

    private Button HubStation(string name,Sprite icon,Func<string> label,UnityAction action)
    {
        Button button=CampAction(hubHome,name,0,0,226,53,new Color(.12f,.17f,.13f,.89f),icon,label,action,25,32,false);
        button.GetComponentInChildren<TMP_Text>().color=CampPaper;
        if(button.transform.Find("Icon") is Transform tag) tag.GetComponent<Image>().color=CampGold;
        CampSolid(button.transform,"Gold line",10,49,40,3,CampGold);
        return button;
    }

    private StatTile HubSmallTile(RectTransform parent,string name,float x,float y,Sprite icon,Func<string> label,UnityAction action)
    {
        Button button=CampAction(parent,name,x,y,205,55,CampButtonColor,icon,label,action,24,26);
        return new StatTile { Root=button.gameObject,Button=button,Background=button.GetComponent<Image>(),Title=button.GetComponentInChildren<TMP_Text>(),Badge=CampBadge(button.transform) };
    }

    private static Sprite HubVignette()
    {
        if(hubVignette!=null) return hubVignette;
        const int size=96;
        Texture2D tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
        var colors=new Color32[size*size];
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            float u=x/(size-1f)*2-1,v=y/(size-1f)*2-1;
            float outer=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Sqrt(u*u+v*v)-.52f)/.87f));
            float baseShade=Mathf.Pow(Mathf.Clamp01(-v),4)*.22f;
            colors[x+y*size]=new Color(.06f,.085f,.05f,Mathf.Clamp01(outer*.34f+baseShade));
        }
        tex.SetPixels32(colors);tex.Apply(false,true);tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
        hubVignette=Sprite.Create(tex,new Rect(0,0,size,size),Vector2.one*.5f);
        return hubVignette;
    }

    private Button HubButton(RectTransform parent,string name,float x,float y,float w,float h,Color color,Sprite icon,Func<string> label,UnityAction action,float size)
    {
        Button button = CampAction(parent,name,x,y,w,h,color,icon,label,action,size,icon != null ? 40 : 0);
        if (color == HubShade)
        {
            button.GetComponentInChildren<TMP_Text>().color = CampPaper;
            if (button.transform.Find("Icon") is Transform it) it.GetComponent<Image>().color = CampGold;
        }
        return button;
    }

    private static void HubTextShadow(TMP_Text text)
    {
        var shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(.04f,.07f,.04f,.9f);
        shadow.effectDistance = new Vector2(2,-3);
    }

    private void BuildHubPage()
    {
        hubBack = HubButton(hubPage,"Back",28,24,160,68,HubShade,null,()=>CampText("‹ Назад","‹ Back","‹ Geri"),HubBack,32);
        hubPageTitle = CampLabel(hubPage,"Station title",208,24,820,84,"",50,CampPaper,ArcadeKitAssets.Display);
        HubTextShadow(hubPageTitle);

        hubDragArea = CampRect(hubPage,"Preview drag area",390,135,820,500);
        ArcadeUiFactory.Image(hubDragArea, null, Color.clear).raycastTarget = true;
        var previewInput=hubDragArea.gameObject.AddComponent<CampHubDrag>();
        previewInput.OnTurn = amount =>
        {
            hubPreviewYaw += amount * .45f;
            hubLastDrag = Time.unscaledTime;
        };
        previewInput.OnClick = ClickHubMiniature;
        hubLeft = HubPanel(hubPage,"Item information",28,140,345,470);
        hubName = CampLabel(hubLeft,"Name",20,14,305,76,"",43,CampGold,ArcadeKitAssets.Display);
        hubDetail = HubParagraph(hubLeft,"Description",20,96,305,350,29);
        hubRight = HubPanel(hubPage,"Loadout and recommendations",1227,140,345,470);
        hubRightTitle = CampLabel(hubRight,"Heading",20,14,305,65,"",30,CampGold,ArcadeKitAssets.BodyBold);
        hubRightText = HubParagraph(hubRight,"Details",20,83,305,359,28);
        hubRecWeapon=HubButton(hubRight,"Recommended weapon",20,389,144,52,CampButtonColor,null,
            ()=>CampText("Оружие ›","Weapon ›","Silah ›"),()=>OpenHubRecommendation(false),23);
        hubRecAbility=HubButton(hubRight,"Recommended skill",178,389,147,52,CampButtonColor,null,
            ()=>CampText("Умения ›","Skills ›","Yetenekler ›"),()=>OpenHubRecommendation(true),23);
        hubActions = CampRect(hubPage,"Actions",440,625,720,76);
        hubSecondary = HubButton(hubActions,"Other",0,0,350,72,CampButtonColor,null,null,HubSecondary,29);
        hubSecondaryText = hubSecondary.GetComponentInChildren<TMP_Text>();
        hubAction = HubButton(hubActions,"Confirm",360,0,360,72,CampGold,null,null,HubConfirm,32);
        hubActionText = hubAction.GetComponentInChildren<TMP_Text>();
        hubCarousel = CampRect(hubPage,"Selection strip",250,716,1100,164);
        hubPrevious = HubButton(hubCarousel,"Previous",0,22,70,115,HubShade,null,()=>"‹",()=>MoveHubSelection(-1),58);
        hubNext = HubButton(hubCarousel,"Next",1030,22,70,115,HubShade,null,()=>"›",()=>MoveHubSelection(1),58);
        for (int i=0;i<hubSlots.Length;i++)
        {
            int slot=i;
            Button button = HubButton(hubCarousel,"Slot "+i,80+i*190,0,180,144,HubShade,null,null,()=>SelectHubItem(hubFirst+slot),24);
            button.GetComponentInChildren<TMP_Text>().gameObject.SetActive(false);
            hubSlotIcons[i] = CampIcon(button.transform,"Preview",null,45,6,90,86,Color.white);
            hubSlotIcons[i].preserveAspect = true;
            hubSlotNames[i] = CampLabel(button.transform,"Item",6,94,168,29,"",24,CampPaper,ArcadeKitAssets.BodyBold,TextAlignmentOptions.Center);
            hubSlotStates[i] = CampLabel(button.transform,"Status",6,122,168,20,"",18,HubMuted,ArcadeKitAssets.Body,TextAlignmentOptions.Center);
            hubSlots[i] = button;
        }
        hubCounter = CampLabel(hubCarousel,"Counter",400,145,300,24,"",20,CampPaper,ArcadeKitAssets.BodyBold,TextAlignmentOptions.Center);
        HubTextShadow(hubCounter);
    }

    private RectTransform HubPanel(Transform parent,string name,float x,float y,float w,float h)
    {
        RectTransform panel=CampRect(parent,name,x,y,w,h);
        ArcadeUiFactory.Image(panel,CampCut(),new Color(.09f,.13f,.10f,.82f),true).raycastTarget = true;
        CampSolid(panel,"Edge",20,85,w-40,2,new Color(.8f,.7f,.45f,.55f));
        CampSolid(panel,"Accent",20,85,43,3,CampGold);
        return panel;
    }

    private static TMP_Text HubParagraph(Transform parent,string name,float x,float y,float w,float h,float size)
    {
        TMP_Text text=CampLabel(parent,name,x,y,w,h,"",size,CampPaper,ArcadeKitAssets.Body,TextAlignmentOptions.TopLeft);
        text.textWrappingMode=TextWrappingModes.Normal;
        text.fontSizeMin=size*.67f;
        text.lineSpacing=4;
        return text;
    }

    private static void HubPlace(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchoredPosition=new Vector2(x,-y);
        rect.sizeDelta=new Vector2(w,h);
    }

    private void FitHubLayout()
    {
        bool portrait=ResponsiveUi.Portrait;
        float aspect=Screen.width/(float)Mathf.Max(1,Screen.height);
        bool resizeCamera=hubAspect>0&&Mathf.Abs(aspect-hubAspect)>.16f;
        if(hubAspect==0||resizeCamera) hubAspect=aspect;
        if (hubPortrait != portrait)
        {
            bool hadLayout=hubPortrait.HasValue;
            hubPortrait=portrait;
            UiArtboard.Resize(hubBoard,portrait ? new Vector2(900,1500) : new Vector2(1600,900));
            HubPlace(hubLogo.rectTransform,30,portrait?119:22,portrait?844:495,74);
            HubPlace(hubCampCaption.rectTransform,34,portrait?191:96,437,28);
            hubHeader.localScale=Vector3.one*.88f;
            HubPlace(hubHeader,portrait?428:1128,24,504,84);
            HubPlace(hubModes,portrait?28:28,portrait?1160:737,418,132);
            HubPlace(hubPlayGroup,portrait?220:570,portrait?1360:748,460,124);
            HubPlace(hubRewards,portrait?452:1154,portrait?1160:736,420,132);
            HubPlace(hubPageTitle.rectTransform,portrait?28:208,portrait?112:24,portrait?844:820,84);
            HubPlace(hubLeft,28,portrait?782:140,345,470);
            HubPlace(hubRight,portrait?527:1227,portrait?782:140,345,470);
            // The detail panels have a fixed internal layout; leave a gap between them on portrait.
            if(portrait)
            {
                hubLeft.localScale=hubRight.localScale=Vector3.one*1.17f;
                HubPlace(hubRight,468,782,345,470);
                HubPlace(hubLeft,28,782,345,470);
            }
            else hubLeft.localScale=hubRight.localScale=Vector3.one;
            HubPlace(hubDragArea,portrait?20:390,portrait?202:135,portrait?860:820,portrait?560:485);
            HubPlace(hubActions,portrait?90:440,portrait?1340:625,720,76);
            HubPlace(hubCarousel,portrait?10:250,portrait?1420:716,1100,164);
            if(portrait)
            {
                // Use the lower part of the portrait preview for the selection strip.
                HubPlace(hubCarousel,10,638,1100,164);
                hubCarousel.localScale=Vector3.one*.8f;
            }
            else hubCarousel.localScale=Vector3.one;
            FitCampExtras(portrait);
            if(hadLayout) resizeCamera=true;
        }
        if(resizeCamera && GameStateManager.Instance?.CurrentState==GameState.Menu)
            FindAnyObjectByType<CameraFollow>()?.MoveToCampView();
        CanvasGroup shown=hubView==HubView.Home?hubHomeFade:hubPageFade;
        if(shown!=null && shown.alpha<1f)
        {
            shown.alpha=Mathf.MoveTowards(shown.alpha,1f,Time.unscaledDeltaTime*3.7f);
            shown.interactable=shown.alpha>.70f;
            shown.blocksRaycasts=shown.interactable;
        }
        if(hubView==HubView.Home) PositionHubStations(portrait);
    }

    private void PositionHubStations(bool portrait)
    {
        Camera camera=Camera.main;
        Canvas canvas=hubBoard.GetComponentInParent<Canvas>();
        Camera uiCamera=canvas!=null && canvas.renderMode!=RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        for(int i=0;i<hubStations.Length;i++)
        {
            RectTransform rect=(RectTransform)hubStations[i].transform;
            float x=portrait? (i%2==0?70:470) : 80+i*350;
            float y=portrait? 970+(i/2)*80 : 420;
            float width=portrait?360:226;
            if(!portrait && campScenery!=null && camera!=null)
            {
                Vector3 screen=camera.WorldToScreenPoint(campScenery.transform.TransformPoint(HubStationPositions[i]));
                if(screen.z>0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(hubBoard,screen,uiCamera,out Vector2 local))
                {
                    x=Mathf.Clamp(local.x+hubBoard.rect.width*.5f-width*.5f,20,1600-width-20);
                    y=Mathf.Clamp(hubBoard.rect.height*.5f-local.y-38,130,655);
                }
            }
            HubPlace(rect,x,y,width,53);
            if(rect.Find("Label") is RectTransform label) label.sizeDelta=new Vector2(width-label.anchoredPosition.x-14,53);
        }
    }

    private void ResetHubView()
    {
        hubView=HubView.Home;
        hubReturnMap=null;
        if(hubBoard==null) return;
        hubHome.gameObject.SetActive(true);
        hubPage.gameObject.SetActive(false);
        hubHomeFade.alpha=1;hubHomeFade.interactable=true;hubHomeFade.blocksRaycasts=true;
    }

    private void OpenHub(HubView view, string preferredClothing = null, ScriptableObject preferredItem = null)
    {
        if(hubBoard==null) return;
        if(view==HubView.Maps && GameModeManager.IsPvp)
        {
            ShowToast(CampText("Карты доступны в PVE","Maps are available in PVE","Haritalar PVE modunda"));
            return;
        }
        hubView=view;
        if(view==HubView.Home) hubReturnMap=null;
        hubHome.gameObject.SetActive(view==HubView.Home);
        hubPage.gameObject.SetActive(view!=HubView.Home);
        CanvasGroup shown=view==HubView.Home?hubHomeFade:hubPageFade;
        shown.alpha=0;shown.interactable=false;shown.blocksRaycasts=false;
        hubIndex=hubFirst=0;
        hubItems.Clear();
        var manager=UpgradeManager.Instance;
        if(manager!=null)
        {
            if(view==HubView.Armory) foreach(var w in manager.GetAvailableWeapons()) { if(w==null) continue; if(preferredItem!=null?w==preferredItem:EquipmentManager.IsEquipped(w)) hubIndex=hubItems.Count; hubItems.Add(w); }
            if(view==HubView.Character) foreach(var c in manager.GetAvailableClothing()) { if(c==null) continue; if(preferredClothing!=null ? c.MapPerkId==preferredClothing : EquipmentManager.IsEquipped(c)) hubIndex=hubItems.Count; hubItems.Add(c); }
            if(view==HubView.Common) foreach(var a in manager.GetAvailableAbilities()) { if(a==null || a.IsCharacterOnly) continue; if(a==preferredItem) hubIndex=hubItems.Count; hubItems.Add(a); }
        }
        if(view==HubView.Maps && environment!=null)
            for(int i=0;i<environment.MapsCount;i++) { var map=environment.GetMap(i); if(map==null) continue; if(preferredItem!=null?map==preferredItem:i==environment.RequestedMapIndex) hubIndex=hubItems.Count; hubItems.Add(map); }
        hubFirst=Mathf.Clamp(hubIndex-2,0,Mathf.Max(0,HubCount()-hubSlots.Length));
        hubPreviewYaw=-18;
        RefreshHubPage();
        FindAnyObjectByType<CameraFollow>()?.MoveToCampView();
    }

    private void HubBack()
    {
        if(hubReturnMap!=null&&hubView!=HubView.Maps)
        {
            GameMap map=hubReturnMap;
            hubReturnMap=null;
            OpenHub(HubView.Maps,preferredItem:map);
        }
        else OpenHub(hubView==HubView.Common?HubView.Character:HubView.Home);
    }
    private int HubCount() => hubView==HubView.Workbench ? PermanentUpgrades.Stats.Count : hubItems.Count;
    private ScriptableObject HubSelected => hubIndex>=0 && hubIndex<hubItems.Count ? hubItems[hubIndex] : null;
    private void MoveHubSelection(int direction) => SelectHubItem(Mathf.Clamp(hubIndex+direction,0,Mathf.Max(0,HubCount()-1)));
    private void SelectHubItem(int index)
    {
        if(index<0 || index>=HubCount()) return;
        hubIndex=index;
        if(index<hubFirst) hubFirst=index;
        if(index>=hubFirst+hubSlots.Length) hubFirst=index-hubSlots.Length+1;
        hubPreviewYaw=-18;
        RefreshHubPage();
    }

    private string HubItemName(int i)
    {
        if(hubView==HubView.Workbench) return PermanentUpgrades.GetName(PermanentUpgrades.Stats[i]);
        return hubItems[i] switch { WeaponData w=>w.LocalizedName, ClothingData c=>c.LocalizedName, AbilityData a=>a.LocalizedName, GameMap m=>m.LocalizedName, _=>"" };
    }
    private Sprite HubItemIcon(int i)
    {
        if(hubView==HubView.Workbench) return CampUpgrades();
        return hubItems[i] switch { WeaponData w=>MenuSheetArt.Weapon(w,"sword"), ClothingData c=>c.Preview, AbilityData a=>AbilityPresentation.Icon(a.Kind), GameMap m=>MenuSheetArt.Arena(m), _=>null };
    }
    private string HubItemState(int i)
    {
        if(hubView==HubView.Workbench) return $"{PermanentUpgrades.GetLevel(PermanentUpgrades.Stats[i])}/{PermanentUpgrades.MaxLevel}";
        if(hubItems[i] is WeaponData w) return HubOwnership(EquipmentManager.IsOwned(w),EquipmentManager.IsEquipped(w),w.UnlockLevel,w.Price);
        if(hubItems[i] is ClothingData c) return HubOwnership(EquipmentManager.IsOwned(c),EquipmentManager.IsEquipped(c),c.UnlockLevel,c.Price);
        if(hubItems[i] is AbilityData a) return HubOwnership(EquipmentManager.IsOwned(a),EquipmentManager.IsEquipped(a),a.UnlockLevel,0);
        if(hubItems[i] is GameMap m) return environment.CurrentMap==m ? CampText("Выбрана","Selected","Secili") : environment.IsMapUnlocked(HubMapIndex(m)) ? CampText("Открыта","Open","Acik") : "Lv. "+m.unlockLevel;
        return "";
    }
    private static string HubOwnership(bool owned,bool equipped,int level,int price) => equipped ? CampText("Выбрано","Equipped","Secili") :
        owned ? CampText("Есть","Owned","Var") : (XpManager.Instance!=null && XpManager.Instance.GlobalLevel>=level ? price>0?price+" ●":CampText("Бесплатно","Free","Ucretsiz") : "Lv. "+level);

    private int HubMapIndex(GameMap map)
    {
        if(environment!=null) for(int i=0;i<environment.MapsCount;i++) if(environment.GetMap(i)==map) return i;
        return -1;
    }

    private void RefreshHubPage()
    {
        if(hubPage==null) return;
        if(hubView==HubView.Home) { RefreshHubPreview(); return; }
        int count=HubCount();
        for(int i=0;i<hubSlots.Length;i++)
        {
            int n=hubFirst+i;
            bool valid=n<count;
            hubSlots[i].gameObject.SetActive(valid);
            if(!valid) continue;
            bool selected=n==hubIndex;
            hubSlots[i].GetComponent<Image>().color=selected?CampGold:HubShade;
            hubSlotNames[i].color=selected?CampInk:CampPaper;
            hubSlotStates[i].color=selected?CampInk:HubMuted;
            hubSlotNames[i].text=HubItemName(n);
            hubSlotStates[i].text=HubItemState(n);
            hubSlotIcons[i].sprite=HubItemIcon(n);
            hubSlotIcons[i].enabled=hubSlotIcons[i].sprite!=null;
        }
        hubPrevious.interactable=hubIndex>0;
        hubNext.interactable=hubIndex+1<count;
        hubCounter.text=count>0?$"{hubIndex+1} / {count}":"—";
        hubPageTitle.text=hubView switch
        {
            HubView.Armory=>CampText("ОРУЖЕЙНАЯ","ARMORY","SILAHLAR"), HubView.Character=>CampText("БОЕЦ","FIGHTER","SAVASCI"),
            HubView.Common=>CampText("ОБЩИЕ УМЕНИЯ","COMMON SKILLS","ORTAK YETENEKLER"), HubView.Workbench=>CampText("МАСТЕРСКАЯ","WORKSHOP","ATOLYE"),
            _=>CampText("ПЛАНИРОВАНИЕ ВЫЛАЗКИ","PLAN YOUR RUN","SEFER PLANLA")
        };
        hubAction.interactable=false;
        hubSecondary.gameObject.SetActive(false);
        hubActionText.text="—";
        hubRightTitle.text=CampText("СНАРЯЖЕНИЕ","LOADOUT","EKIPMAN");
        hubRecWeapon.gameObject.SetActive(false);
        hubRecAbility.gameObject.SetActive(false);
        hubRightText.rectTransform.sizeDelta=new Vector2(305,359);
        hubName.text=count>0?HubItemName(hubIndex):CampText("Нет предметов","No items","Esya yok");
        hubDetail.text=hubRightText.text="";
        if(HubSelected is WeaponData w)
        {
            hubDetail.text=$"{CampText("Урон","Damage","Hasar")}   {w.Damage:0.#}\n{CampText("Темп стрельбы","Fire rate","Atis hizi")}   {w.FireRate:0.##}\n{CampText("Магазин","Magazine","Sarjor")}   {w.BaseMagazineSize}\n{CampText("Перезарядка","Reload","Doldurma")}   {w.BaseReloadSeconds:0.#} s\n\n{w.LocalizedDescription}";
            hubRightText.text=CampText("Боец: ","Fighter: ","Sav asci: ")+(CharacterBuild.Selected()?.LocalizedName??"—")+"\n\n"+HubCommonLoadout();
            HubPurchaseAction(EquipmentManager.IsOwned(w),EquipmentManager.IsEquipped(w),EquipmentManager.IsUnlocked(w),w.UnlockLevel,w.Price);
            HubSecondaryAction(CampText("К бойцу","Fighter","Savasci"));
        }
        else if(HubSelected is ClothingData c)
        {
            hubDetail.text=CharacterBuild.Describe(c)+c.ProtectionDescription;
            hubRightText.text=HubCommonLoadout()+"\n\n"+CampText("Защита карты действует после покупки персонажа, даже при выборе другого бойца.","Map protection stays unlocked when you switch fighters.","Satin alinan harita korumasi diger savascilarda da gecerlidir.");
            HubPurchaseAction(EquipmentManager.IsOwned(c),EquipmentManager.IsEquipped(c),EquipmentManager.IsUnlocked(c),c.UnlockLevel,c.Price);
            HubSecondaryAction(CampText("Общие умения","Common skills","Ortak yetenekler"));
        }
        else if(HubSelected is AbilityData a)
        {
            hubDetail.text=AbilityPresentation.Type(a.IsAutomatic)+"\n\n"+a.LocalizedStats;
            hubRightText.text=HubCommonLoadout();
            bool owned=EquipmentManager.IsOwned(a),equipped=EquipmentManager.IsEquipped(a),innate=CharacterBuild.Innate(a.Kind);
            hubActionText.text=innate?CampText("Встроено в бойца","Innate","Dogustan"):!owned?"Lv. "+a.UnlockLevel:equipped?CampText("Снять","Remove","Cikar"):CampText("Выбрать","Equip","Kusan");
            hubAction.interactable=owned&&!innate&&(equipped||EquipmentManager.HasFreeAbilitySlot(a.IsAutomatic));
            HubSecondaryAction(CampText("К бойцу","Fighter","Savasci"));
        }
        else if(hubView==HubView.Workbench && count>0)
        {
            var stat=PermanentUpgrades.Stats[hubIndex];
            int level=PermanentUpgrades.GetLevel(stat), cost=PermanentUpgrades.GetCost(stat);
            hubDetail.text=$"Lv. {level}/{PermanentUpgrades.MaxLevel}\n\n{PermanentUpgrades.GetDescription(stat)}\n\n{CampText("Сейчас","Current","Mevcut")}: {PermanentUpgrades.FormatTotal(stat)}\n{CampText("Следующий уровень","Next level","Sonraki seviye")}: {PermanentUpgrades.FormatNextLevelGain(stat)}";
            hubRightTitle.text=CampText("ПОСТОЯННЫЕ БОНУСЫ","PERMANENT BONUSES","KALICI BONUSLAR");
            var levels=new System.Text.StringBuilder();
            foreach(var s in PermanentUpgrades.Stats) levels.Append(PermanentUpgrades.GetName(s)).Append("  ").Append(PermanentUpgrades.GetLevel(s)).Append('/').Append(PermanentUpgrades.MaxLevel).Append('\n');
            hubRightText.text=levels.ToString();
            hubActionText.text=PermanentUpgrades.IsMaxLevel(stat)?CampText("Максимум","Max level","Maksimum"):CampText("Улучшить","Upgrade","Gelistir")+"  "+cost+" ●";
            hubAction.interactable=!PermanentUpgrades.IsMaxLevel(stat) && (xpManager?.GlobalCoins??0)>=cost;
        }
        else if(HubSelected is GameMap map)
        {
            int mapIndex=HubMapIndex(map);
            bool unlocked=environment!=null && environment.IsMapUnlocked(mapIndex);
            hubDetail.text=map.LocalizedMechanicName+"\n\n"+MapMechanicText.Description(map.mechanic)+"\n\n"+(unlocked?CampText("Арена открыта","Arena unlocked","Arena acik"):CampText("Требуется уровень ","Required level ","Gerekli seviye ")+map.unlockLevel);
            hubRightTitle.text=CampText("РЕКОМЕНДУЕМ","RECOMMENDED","ONERILEN");
            hubRightText.text=HubMapRecommendation(map);
            hubRightText.rectTransform.sizeDelta=new Vector2(305,300);
            hubRecWeapon.gameObject.SetActive(true);
            hubRecAbility.gameObject.SetActive(true);
            hubActionText.text=unlocked?CampText("Выбрать карту","Select map","Harita sec"):"Lv. "+map.unlockLevel;
            hubAction.interactable=unlocked&&!GameModeManager.IsPvp;
            HubSecondaryAction(HubProtectionId(map)!=null?CampText("Проверить защиту","Check protection","Korumayi kontrol et"):CampText("К бойцу","Fighter","Savasci"));
        }
        hubAction.GetComponent<Image>().color=hubAction.interactable ? CampGold : CampButtonColor;
        HubPlace((RectTransform)hubAction.transform,hubSecondary.gameObject.activeSelf?360:180,0,360,72);
        RefreshHubPreview();
    }

    private void HubPurchaseAction(bool owned,bool equipped,bool unlocked,int level,int price)
    {
        hubActionText.text=equipped?CampText("Выбрано","Equipped","Secili"):owned?CampText("Выбрать","Equip","Kusan"):!unlocked?"Lv. "+level:CampText("Купить","Buy","Satin al")+"  "+price+" ●";
        hubAction.interactable=!equipped&&(owned||(unlocked&&(xpManager?.GlobalCoins??0)>=price));
    }
    private void HubSecondaryAction(string text)
    {
        hubSecondary.gameObject.SetActive(true);
        hubSecondaryText.text=text;
    }
    private void HubSecondary()
    {
        if(hubView==HubView.Character) OpenHub(HubView.Common);
        else if(HubSelected is GameMap map)
        {
            hubReturnMap=map;
            OpenHub(HubView.Character,HubProtectionId(map));
        }
        else OpenHub(HubView.Character);
    }
    private void HubConfirm()
    {
        bool success=false;
        switch(HubSelected)
        {
            case WeaponData w: success=EquipmentManager.IsOwned(w)?EquipmentManager.TryEquip(w):EquipmentManager.TryPurchase(w); break;
            case ClothingData c: success=EquipmentManager.IsOwned(c)?EquipmentManager.TryEquip(c):EquipmentManager.TryPurchase(c); break;
            case AbilityData a: success=EquipmentManager.TryToggleAbility(a); break;
            case GameMap map:
                if(environment!=null && environment.IsMapUnlocked(HubMapIndex(map)) && environment.RequestMenuMap(HubMapIndex(map))) OpenHub(HubView.Home);
                return;
            default:
                if(hubView==HubView.Workbench && HubCount()>0) success=PermanentUpgrades.TryBuy(PermanentUpgrades.Stats[hubIndex]);
                break;
        }
        if(!success) ShowToast(CampText("Действие недоступно","Action unavailable","Islem kullanilamaz"));
        RefreshAll();
    }
    private string HubCommonLoadout()
    {
        var text=new System.Text.StringBuilder();
        text.Append(CampText("Общие умения","Common skills","Ortak yetenekler")).Append("  ").Append(EquipmentManager.EquippedAbilityCount(false)).Append("/1 + ").Append(EquipmentManager.EquippedAbilityCount(true)).Append("/2\n");
        if(UpgradeManager.Instance!=null)
            foreach(var a in UpgradeManager.Instance.GetAvailableAbilities())
                if(a!=null&&EquipmentManager.IsEquipped(a)&&!a.IsCharacterOnly) text.Append(a.IsAutomatic?"• ":"E: ").Append(a.LocalizedName).Append('\n');
        return text.ToString();
    }
    private static string HubProtectionId(GameMap map) => map.langKey switch
    {
        "gory"=>"winter", "pustynya"=>"cowboy", "gorod"=>"police", "kosmos"=>"astronaut", _=>null
    };
    private static WeaponData HubRecommendedWeapon(GameMap map)
    {
        string name=map.langKey switch
        {
            "plyazh"=>"Lightning SMG", "gory"=>"Hunter Rifle", "pustynya"=>"Minigun",
            "gorod"=>"Double Shotgun", "kosmos"=>"Plasma Rifle", _=>"Basic Rifle"
        };
        if(UpgradeManager.Instance==null) return null;
        WeaponData best=null,alternative=null;
        foreach(var w in UpgradeManager.Instance.GetAvailableWeapons())
        {
            if(w==null) continue;
            if(w.WeaponName==name) best=w;
            if(map.langKey=="plyazh"&&w.WeaponName=="Twin SMG") alternative=w;
        }
        // The beach unlocks well before the lightning weapon. Recommend a
        // useful accessible option first, then its natural electric upgrade.
        return best!=null&&!EquipmentManager.IsUnlocked(best)&&alternative!=null?alternative:best??alternative;
    }
    private static AbilityKind HubRecommendedSkill(GameMap map) => map.langKey switch
    {
        "plyazh"=>AbilityKind.LightningStorm, "gory"=>AbilityKind.ThunderSlam,
        "pustynya"=>AbilityKind.Shield, "gorod"=>AbilityKind.CrescentSlash,
        "kosmos"=>AbilityKind.GravityWell, _=>AbilityKind.OrbitBlades
    };
    private static AbilityData HubRecommendedAbility(GameMap map)
    {
        AbilityData best=null,alternative=null;
        if(UpgradeManager.Instance!=null)
            foreach(var a in UpgradeManager.Instance.GetAvailableAbilities())
            {
                if(a==null) continue;
                if(a.Kind==HubRecommendedSkill(map)) best=a;
                if(map.langKey=="plyazh"&&a.Kind==AbilityKind.ThunderSlam) alternative=a;
            }
        return best!=null&&!EquipmentManager.IsOwned(best)&&alternative!=null?alternative:best??alternative;
    }
    private void OpenHubRecommendation(bool ability)
    {
        if(!(HubSelected is GameMap map)) return;
        hubReturnMap=map;
        ScriptableObject item=ability?(ScriptableObject)HubRecommendedAbility(map):HubRecommendedWeapon(map);
        OpenHub(ability?HubView.Common:HubView.Armory,preferredItem:item);
    }
    private string HubMapRecommendation(GameMap map)
    {
        string id=HubProtectionId(map);
        string protection=CampText("Особая защита не нужна.","No special protection required.","Ozel koruma gerekmez.");
        if(id!=null)
        {
            ClothingData gear=null;
            if(UpgradeManager.Instance!=null) foreach(var c in UpgradeManager.Instance.GetAvailableClothing()) if(c!=null&&c.MapPerkId==id) { gear=c; break; }
            bool owned=MapSurvivalZones.HasOutfit(id);
            protection=(owned?"<color=#B7D998>✓ ":"<color=#FFD19A>! ")+(gear?.LocalizedName??id)+"</color>\n"+
                (owned?CampText("Защита уже получена","Protection unlocked","Koruma acik"):CampText("Рекомендуется приобрести","Recommended to unlock","Acmaniz onerilir"))+
                (gear!=null?gear.ProtectionDescription:"");
        }
        var weapon=HubRecommendedWeapon(map);
        var ability=HubRecommendedAbility(map);
        string weaponLine=weapon!=null?weapon.LocalizedName+"  • "+HubOwnership(EquipmentManager.IsOwned(weapon),EquipmentManager.IsEquipped(weapon),weapon.UnlockLevel,weapon.Price):"—";
        string abilityLine=AbilityPresentation.Name(ability!=null?ability.Kind:HubRecommendedSkill(map));
        if(ability!=null) abilityLine+="  • "+(CharacterBuild.Innate(ability.Kind)?CampText("Встроено","Innate","Dogustan"):HubOwnership(EquipmentManager.IsOwned(ability),EquipmentManager.IsEquipped(ability),ability.UnlockLevel,0));
        return protection+"\n\n"+CampText("Оружие: ","Weapon: ","Silah: ")+weaponLine+"\n"+CampText("Умение: ","Skill: ","Yetenek: ")+abilityLine;
    }
}

public sealed class CampHubDrag : MonoBehaviour, IDragHandler, IPointerClickHandler
{
    public Action<float> OnTurn;
    public Action<Vector2> OnClick;
    public void OnDrag(PointerEventData eventData) => OnTurn?.Invoke(eventData.delta.x);
    public void OnPointerClick(PointerEventData eventData) { if(!eventData.dragging) OnClick?.Invoke(eventData.position); }
}
