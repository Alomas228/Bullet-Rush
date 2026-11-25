using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static GameplayJourney;

public sealed partial class JourneyUI
{
    private static readonly Color JournalBase=ArcadeUiTheme.Rgb(0x25322D);
    private static readonly Color JournalPanel=ArcadeUiTheme.Rgb(0x35443A);
    private static readonly Color JournalInset=ArcadeUiTheme.Rgb(0x1E2924);
    private static readonly Color JournalPaper=ArcadeUiTheme.Rgb(0xF4EBD4);
    private static readonly Color JournalMuted=ArcadeUiTheme.Rgb(0xBAC4AB);
    private static readonly Color JournalGold=ArcadeUiTheme.Rgb(0xE5B66D);
    private static readonly Color JournalGreen=ArcadeUiTheme.Rgb(0xA6CA94);
    private static readonly Color JournalEdge=ArcadeUiTheme.Rgb(0x56654E);
    private static Sprite journalCut;
    private bool illustratedJournal,journalPortrait;
    private float JournalWidth=>card.sizeDelta.x-48;

    private void BuildJournalNavigation()
    {
        string[] titles={T("ЗВАНИЯ","RANKS","RÜTBELER"),T("ОРУЖИЕ","WEAPONS","SİLAHLAR"),
            T("КАРТЫ","MAPS","HARİTALAR"),T("СИНЕРГИИ","SYNERGIES","SİNERJİLER")};
        string[] icons={"award","sword","target","power"};
        float width=(card.sizeDelta.x-48)/4;
        for(int i=0;i<4;i++)
        {
            int next=i;
            var rect=JRect(card,"Journal tab "+i,24+i*width,90,width-8,48);
            var button=JButton(rect,titles[i],i==tab?JournalGold:JournalInset,
                i==tab?JournalInset:JournalPaper,()=>{tab=next;OpenJournal();},20);
            var label=button.GetComponentInChildren<TMP_Text>();
            JournalPlace(label.rectTransform,32,0,width-48,48);
            JIcon(rect,icons[i],6,12,26,26,i==tab?JournalInset:JournalGold);
        }
    }

    private void BuildJournalOverview()
    {
        int rank=Mathf.Clamp(journey.Data.rank,0,4),wins=0,trials=0,best=0;
        foreach(var m in journey.Data.maps)if(m!=null){if(m.won)wins++;best=Mathf.Max(best,m.best);}
        foreach(var t in journey.Data.trials)if(t!=null&&t.progress>=JourneyRules.TrialTarget(t.id))trials++;
        int bosses=journey.Data.cleanBossIds.Count;
        float w=JournalWidth;
        var hero=JBlock("Current rank",journalPortrait?190:160,JournalPanel,JournalGold);
        JMedal(hero,26,22,112,rank,true);
        JLabel(hero,T("ТЕКУЩЕЕ ЗВАНИЕ","CURRENT RANK","MEVCUT RÜTBE"),162,20,w-184,30,20,JournalGold,true);
        JLabel(hero,RankName(rank).ToUpperInvariant(),162,50,w-184,62,43,JournalPaper,true);
        JLabel(hero,T("Звание сохраняется после поражения. Награды — косметические.",
            "Your rank survives defeat. Rewards are cosmetic.","Rütbe yenilgide korunur. Ödüller kozmetiktir."),
            162,112,w-190,journalPortrait?66:38,20,JournalMuted,false,true);

        var ladder=JBlock("Rank ladder",142,JournalInset);
        float part=(w-24)/5;
        for(int i=0;i<5;i++)
        {
            float center=12+part*(i+.5f);
            if(i<4)JournalLine(ladder,center+36,53,part-72,3,i<rank?JournalGold:JournalEdge);
            JMedal(ladder,center-35,14,70,i,i<=rank);
            JLabel(ladder,RankName(i),center-part*.5f+5,90,part-10,38,19,i==rank?JournalGold:i<rank?JournalPaper:JournalMuted,
                true,false,TextAlignmentOptions.Center);
        }

        if(rank<4)
        {
            int next=rank+1;
            var conditions=new List<(string label,string icon,int value,int total)>();
            if(next==1)
            {
                conditions.Add((T("Лучшая волна","Best wave","En iyi dalga"),"chart",best,10));
                conditions.Add((T("Испытания","Weapon trials","Silah sınavları"),"sword",trials,1));
            }
            else
            {
                conditions.Add((T("Победные карты","Map victories","Harita zaferleri"),"trophy",wins,next-1));
                conditions.Add((T("Испытания оружия","Weapon trials","Silah sınavları"),"sword",trials,next==2?3:next==3?5:8));
                if(next>=3)conditions.Add((T("Разные боссы без урона","Untouched bosses","Hasarsız patronlar"),"crown",bosses,next==3?3:5));
                if(next==4)conditions.Add((T("Лучшая волна","Best wave","En iyi dalga"),"chart",best,70));
            }
            float col=(w-48)/2;
            var goal=JBlock("Next rank",66+((conditions.Count+1)/2)*86,JournalPanel);
            JLabel(goal,T("СЛЕДУЮЩЕЕ ЗВАНИЕ: ","NEXT RANK: ","SONRAKİ RÜTBE: ")+RankName(next).ToUpperInvariant(),
                20,10,w-40,40,26,JournalPaper,true);
            for(int i=0;i<conditions.Count;i++)
            {
                var c=conditions[i];
                JMetric(goal,20+(i%2)*col,59+(i/2)*86,col-10,c.icon,c.label,c.value,c.total);
            }
        }
        else JBanner("award",T("ЛЕГЕНДА","LEGEND","EFSANE"),T("Высшее звание получено.","Highest rank earned.","En yüksek rütbe kazanıldı."),JournalGold);

        var stats=JBlock("Career and run",journalPortrait?252:173,JournalPanel);
        JLabel(stats,T("БОЕВАЯ СВОДКА","BATTLE RECORD","SAVAŞ KAYDI"),20,9,w-40,38,26,JournalPaper,true);
        int columns=journalPortrait?2:3;
        float unit=(w-40)/columns;
        (string,string,int)[] metrics={
            ("trophy",T("Карты","Maps won","Haritalar"),wins),("sword",T("Испытания","Trials","Sınavlar"),trials),
            ("crown",T("Чистые боссы","Untouched bosses","Hasarsız patronlar"),bosses),
            ("chart",T("Волн в забеге","Waves this run","Bu koşuda dalgalar"),journey.LastCleared),
            ("shield",T("Уклонения","Dodges","Kaçışlar"),journey.Dodges),("gift",T("Тайники","Caches","Sandıklar"),journey.CachesWon)};
        for(int i=0;i<metrics.Length;i++)
        {
            float x=20+(i%columns)*unit,y=48+(i/columns)*65;
            JIcon(stats,metrics[i].Item1,x,y+7,36,36,JournalGold);
            JLabel(stats,metrics[i].Item2,x+45,y,unit-53,24,17,JournalMuted);
            JLabel(stats,metrics[i].Item3.ToString(),x+45,y+23,unit-53,33,27,JournalPaper,true);
        }
        if(journey.Highlights.Count>0)
        {
            var log=JBlock("Highlights",48+journey.Highlights.Count*40,JournalInset);
            JLabel(log,T("СОБЫТИЯ ЗАБЕГА","RUN HIGHLIGHTS","KOŞU OLAYLARI"),20,8,w-40,34,24,JournalGold,true);
            for(int i=0;i<journey.Highlights.Count;i++)
            {
                JIcon(log,"check",20,46+i*40,25,25,JournalGreen);
                JLabel(log,journey.Highlights[i],60,41+i*40,w-78,38,19,JournalPaper,false,true);
            }
        }
    }

    private void BuildJournalWeapons()
    {
        var weapons=UpgradeManager.Instance?.GetAvailableWeapons();
        if(weapons==null)return;
        int done=0,count=0;
        foreach(var weapon in weapons)if(weapon!=null){count++;if(journey.TrialComplete(weapon.WeaponName))done++;}
        JBanner("sword",T("АРСЕНАЛ ","ARMORY ","CEPHANELİK ")+done+" / "+count,
            T("Испытания открывают расцветки. Тир — без наград и прогресса.","Trials unlock weapon colors. Practice has no rewards or progress.","Sınavlar renk açar. Atış alanında ödül veya ilerleme yok."),JournalGold);
        bool practice=GameStateManager.Instance!=null&&GameStateManager.Instance.CurrentState==GameState.Menu;
        float w=JournalWidth;
        foreach(var weapon in weapons)
        {
            if(weapon==null)continue;
            string id=weapon.WeaponName;
            int target=JourneyRules.TrialTarget(id),progress=journey.WeaponTrial(id).progress;
            bool complete=progress>=target,equipped=journey.Data.equippedTrial==id;
            var row=JBlock("Weapon "+id,journalPortrait?220:160,JournalPanel,complete?JournalGreen:JournalEdge);
            float artW=journalPortrait?180:218, x=artW+30;
            var art=JRect(row,"Weapon illustration",14,14,artW,126);
            ArcadeUiFactory.Image(art,JournalCut(),JournalInset,true);
            JSprite(art,MenuSheetArt.Weapon(weapon,"sword"),12,8,artW-24,104,Color.white);
            if(complete)JIcon(art,"check",artW-36,6,27,27,JournalGreen);
            float textWidth=journalPortrait?w-x-18:w-x-245;
            JLabel(row,weapon.LocalizedName,x,10,textWidth,39,28,JournalPaper,true);
            JLabel(row,TrialDescription(id),x,48,textWidth,journalPortrait?61:55,20,JournalMuted,false,true);
            JLabel(row,progress+" / "+target,x,110,textWidth,24,19,complete?JournalGreen:JournalGold,true);
            JProgress(row,x,139,textWidth,8,progress,target,complete?JournalGreen:JournalGold);
            if(journalPortrait)
            {
                if(practice)JAction(row,16,164,(w-48)/2,42,T("В ТИР","PRACTICE","DENE"),JournalInset,JournalPaper,()=>ShowWeaponTrial(weapon));
                float bx=practice?(w/2+8):16,bw=practice?(w-48)/2:w-32;
                if(complete)JAction(row,bx,164,bw,42,equipped?T("ВЫБРАНО","EQUIPPED","SEÇİLİ"):T("РАСЦВЕТКА","COLOR","RENK"),equipped?JournalGreen:JournalGold,JournalInset,()=>{journey.Equip(id);OpenJournal();});
                else JLabel(row,T("Расцветка закрыта","Color locked","Renk kilitli"),bx,164,bw,42,19,JournalMuted,false,false,TextAlignmentOptions.Center);
            }
            else
            {
                float bx=w-225;
                if(complete)JAction(row,bx,22,207,48,equipped?T("ВЫБРАНО","EQUIPPED","SEÇİLİ"):T("РАСЦВЕТКА","COLOR","RENK"),equipped?JournalGreen:JournalGold,JournalInset,()=>{journey.Equip(id);OpenJournal();});
                else
                {
                    JIcon(row,"lock",bx+7,33,27,27,JournalMuted);
                    JLabel(row,T("Расцветка закрыта","Color locked","Renk kilitli"),bx+42,22,165,48,18,JournalMuted,false,true);
                }
                if(practice)JAction(row,bx,83,207,48,T("В ТИР","PRACTICE","DENE"),JournalInset,JournalPaper,()=>ShowWeaponTrial(weapon));
            }
        }
    }

    private void BuildJournalMaps()
    {
        JBanner("trophy",T("КАРТА ПОБЕД","MAP TROPHIES","HARİTA KUPALARI"),
            T("Три медали на каждой арене: волна 20, победа на 50-й и тематическое испытание.",
                "Three medals per arena: wave 20, victory at 50 and a themed challenge.","Her arenada üç madalya: 20. dalga, 50. dalgada zafer ve özel görev."),JournalGold);
        var environment=FindAnyObjectByType<EnvironmentController>();
        var seen=new HashSet<string>();
        if(environment!=null)
            for(int i=0;i<environment.MapsCount;i++)
            {
                var map=environment.GetMap(i);
                if(map==null||!seen.Add(map.langKey))continue;
                BuildJournalMap(map.langKey,map.LocalizedName,MenuSheetArt.Arena(map),journey.Data.maps.Find(m=>m!=null&&m.id==map.langKey));
            }
        foreach(var m in journey.Data.maps)
            if(m!=null&&seen.Add(m.id))BuildJournalMap(m.id,Lang.GetOr("map."+m.id,m.id),null,m);
    }

    private void BuildJournalMap(string id,string name,Sprite preview,MapRecord record)
    {
        float w=JournalWidth;
        int best=record?.best??0;
        bool won=record?.won??false,special=record?.special??false;
        var row=JBlock("Arena "+id,journalPortrait?252:198,JournalPanel,won?JournalGreen:JournalEdge);
        float artWidth=journalPortrait?210:305;
        var art=JRect(row,"Arena illustration",14,14,artWidth,journalPortrait?124:170);
        ArcadeUiFactory.Image(art,JournalCut(),JournalInset,true);
        JSprite(art,preview??ArcadeKitAssets.Icon("target"),5,5,artWidth-10,art.sizeDelta.y-10,Color.white);
        float x=artWidth+32,available=w-x-18;
        JLabel(row,name.ToUpperInvariant(),x,12,available,40,30,JournalPaper,true);
        JLabel(row,T("РЕКОРД: ВОЛНА ","BEST: WAVE ","REKOR: DALGA ")+best,x,53,available,32,21,JournalGold,true);
        float medalX=journalPortrait?16:x,medalWidth=(journalPortrait?w-32:available)/3,medalY=journalPortrait?142:91;
        JMapMedal(row,medalX,medalY,medalWidth,"chart",T("Волна 20","Wave 20","20. dalga"),best>=20);
        JMapMedal(row,medalX+medalWidth,medalY,medalWidth,"trophy",T("Победа 50","Victory 50","50. zafer"),won);
        JMapMedal(row,medalX+2*medalWidth,medalY,medalWidth,"star",T("Особый вызов","Special trial","Özel görev"),special);
        JLabel(row,MapChallenge(id),journalPortrait?18:x,journalPortrait?205:151,journalPortrait?w-36:available,38,18,JournalMuted,false,true);
    }

    private static string MapChallenge(string id)
    {
        if(id=="plyazh"||id=="beach")return T("Особая медаль: 12 попаданий молнией по мокрым врагам за забег.","Special: hit wet enemies with lightning 12 times in one run.","Özel: bir koşuda ıslak düşmana 12 yıldırım.");
        if(id=="gory"||id=="mountains")return T("Особая медаль: разрушить 12 ледяных покровов за забег.","Special: break 12 ice covers in one run.","Özel: bir koşuda 12 buz örtüsü kır.");
        return T("Особая медаль: волна 20, 8 точных уклонений, 1 тайник за забег.","Special: wave 20, 8 precise dodges and 1 cache in one run.","Özel: tek koşuda 20. dalga, 8 tam kaçış, 1 sandık.");
    }

    private void BuildJournalSynergies()
    {
        var synergies=UpgradeManager.Instance?.GetAllSynergies();
        if(synergies==null)return;
        int count=0,knownCount=0;
        foreach(var s in synergies)if(s!=null){count++;if(journey.Data.discoveries.Contains(s.name))knownCount++;}
        JBanner("power",T("ОТКРЫТИЯ ","DISCOVERIES ","KEŞİFLER ")+knownCount+" / "+count,
            T("Все комбинации доступны с начала игры. Журнал отмечает открытия и показывает условия.",
                "All combinations are available from the first run. The journal records discoveries and conditions.",
                "Tüm birleşimler baştan kullanılabilir. Günlük keşifleri ve koşulları gösterir."),JournalGold);
        float w=JournalWidth;
        foreach(var synergy in synergies)
        {
            if(synergy==null)continue;
            bool known=journey.Data.discoveries.Contains(synergy.name);
            Color accent=SynergyAccent(synergy.name);
            string effect=SynergyCombat.Description(synergy.name);
            string description=synergy.LocalizedDescription;
            float artW=journalPortrait?164:195,x=artW+32;
            float bodyWidth=w-x-20;
            float descHeight=journalPortrait?99:76;
            float effectHeight=string.IsNullOrEmpty(effect)?0:journalPortrait?90:66;
            float reqY=64+descHeight+effectHeight;
            int optionalCount=synergy.OptionalRequirements?.Count??0;
            float height=reqY+76+(optionalCount>0?70:0);
            var row=JBlock("Synergy "+synergy.name,height,JournalPanel,known?accent:JournalEdge);
            BuildSynergyArt(row,synergy,artW,Mathf.Min(158,reqY-26),accent);
            JLabel(row,synergy.LocalizedName.ToUpperInvariant(),x,11,bodyWidth-148,45,28,JournalPaper,true);
            var status=JRect(row,"Discovery state",w-157,15,139,33);
            ArcadeUiFactory.Image(status,JournalCut(),known?JournalGreen:JournalInset,true);
            JLabel(status,known?T("ОТКРЫТО","FOUND","KEŞFEDİLDİ"):T("НЕ НАЙДЕНО","UNDISCOVERED","KEŞFEDİLMEDİ"),
                5,0,129,33,15,known?JournalInset:JournalMuted,true,false,TextAlignmentOptions.Center);
            JLabel(row,description,x,59,bodyWidth,descHeight,20,JournalPaper,false,true);
            if(effectHeight>0)
            {
                JournalLine(row,x,59+descHeight,bodyWidth,2,accent);
                JLabel(row,effect,x,63+descHeight,bodyWidth,effectHeight-4,18,JournalMuted,false,true);
            }
            JLabel(row,T("УСЛОВИЯ КОМБИНАЦИИ","COMBINATION REQUIREMENTS","BİRLEŞİM KOŞULLARI"),16,reqY,w-32,25,16,accent,true);
            BuildRequirementStrip(row,synergy.Requirements,16,reqY+28,w-32,accent);
            if(optionalCount>0)
            {
                JLabel(row,T("ДОПОЛНИТЕЛЬНО","OPTIONAL BONUS","EK BONUS"),16,reqY+76,w-32,22,15,JournalMuted,true);
                BuildRequirementStrip(row,synergy.OptionalRequirements,16,reqY+98,w-32,JournalMuted);
            }
        }
    }

    private void BuildSynergyArt(RectTransform row,SynergyData synergy,float width,float height,Color accent)
    {
        var tile=JRect(row,"Effect illustration",14,14,width,height);
        ArcadeUiFactory.Image(tile,JournalCut(),JournalInset,true);
        // Reuse the game's illustrated upgrade art rather than loading scenes
        // or simulating combat effects in the journal.
        var glow=JSprite(tile,ArcadeKitAssets.Glow,0,0,width,height,new Color(accent.r,accent.g,accent.b,.30f));
        glow.preserveAspect=false;
        Sprite first=synergy.Requirements!=null&&synergy.Requirements.Count>0?RequirementSprite(synergy.Requirements[0]):ArcadeKitAssets.Icon("power");
        Sprite second=synergy.Requirements!=null&&synergy.Requirements.Count>1?RequirementSprite(synergy.Requirements[1]):null;
        if(second==null)JSprite(tile,first,18,13,width-36,height-26,Color.white);
        else
        {
            float side=Mathf.Min(width*.64f,height*.72f);
            JSprite(tile,first,8,7,side,side,Color.white);
            JSprite(tile,second,width-side-7,height-side-7,side,side,Color.white);
            JIcon(tile,"plus",width/2-12,height/2-12,24,24,accent);
        }
    }
    private void BuildRequirementStrip(RectTransform parent,IReadOnlyList<SynergyData.SynergyRequirement> list,float x,float y,float width,Color tint)
    {
        if(list==null||list.Count==0)return;
        float part=width/list.Count;
        for(int i=0;i<list.Count;i++)
        {
            var req=list[i];if(req==null)continue;
            var tile=JRect(parent,"Requirement "+i,x+i*part,y,part-7,40);
            ArcadeUiFactory.Image(tile,JournalCut(),JournalInset,true);
            JSprite(tile,RequirementSprite(req),4,3,34,34,Color.white);
            JLabel(tile,RequirementLabel(req),43,0,part-55,40,17,JournalPaper,false,true);
        }
    }
    private static string RequirementLabel(SynergyData.SynergyRequirement req)
    {
        if(req.Kind==SynergyData.RequirementKind.Upgrade)return (req.Upgrade?.LocalizedName??T("Улучшение","Upgrade","Geliştirme"))+" ×"+req.GetRequired();
        if(req.Kind==SynergyData.RequirementKind.WeaponTaken)return req.Weapon!=null?req.Weapon.LocalizedName:
            T("Разное оружие","Distinct weapons","Farklı silahlar")+" ×"+req.GetRequired();
        if(req.Kind==SynergyData.RequirementKind.DistinctFamilies)
        {
            string families=req.Family==(UpgradeFamily.Fire|UpgradeFamily.Lightning|UpgradeFamily.Bleed|UpgradeFamily.Explosion)?
                T("Разные стихии","Distinct elements","Farklı elementler"):T("Разные ветки","Distinct families","Farklı dallar");
            return families+" ×"+req.GetRequired();
        }
        return FamilyName(req.Family)+" ×"+req.GetRequired();
    }
    private static string FamilyName(UpgradeFamily f)=>f switch
    {
        UpgradeFamily.Fire=>T("Горение","Fire","Ateş"),UpgradeFamily.Lightning=>T("Молния","Lightning","Yıldırım"),
        UpgradeFamily.Bleed=>T("Кровотечение","Bleeding","Kanama"),UpgradeFamily.Explosion=>T("Взрывы","Explosions","Patlamalar"),
        UpgradeFamily.Ricochet=>T("Рикошет","Ricochet","Sekme"),UpgradeFamily.Defensive=>T("Защита","Defense","Savunma"),
        UpgradeFamily.Lifesteal=>T("Вампиризм","Lifesteal","Can çalma"),UpgradeFamily.Ability=>T("Способности","Abilities","Yetenekler"),
        UpgradeFamily.Cold=>T("Холод","Cold","Soğuk"),UpgradeFamily.Weapon=>T("Оружие","Weapon","Silah"),
        UpgradeFamily.Utility=>T("Полезность","Utility","Yardımcı"),_=>T("Основные улучшения","Core upgrades","Temel geliştirmeler")
    };
    private static Sprite RequirementSprite(SynergyData.SynergyRequirement req)
    {
        if(req==null)return ArcadeKitAssets.Icon("power");
        if(req.Kind==SynergyData.RequirementKind.Upgrade&&req.Upgrade!=null)return MenuSheetArt.Upgrade(req.Upgrade.CardIcon);
        if(req.Kind==SynergyData.RequirementKind.WeaponTaken)return MenuSheetArt.Weapon(req.Weapon,"sword");
        if(req.Kind==SynergyData.RequirementKind.DistinctFamilies)return ArcadeKitAssets.Icon("power");
        UpgradeType type=req.Family switch
        {
            UpgradeFamily.Fire=>UpgradeType.BurningRounds,UpgradeFamily.Lightning=>UpgradeType.ChainLightning,
            UpgradeFamily.Bleed=>UpgradeType.Bleeding,UpgradeFamily.Explosion=>UpgradeType.ExplosiveRounds,
            UpgradeFamily.Ricochet=>UpgradeType.Ricochet,UpgradeFamily.Defensive=>UpgradeType.Shield,
            UpgradeFamily.Lifesteal=>UpgradeType.Lifesteal,UpgradeFamily.Ability=>UpgradeType.Bomb,
            UpgradeFamily.Utility=>UpgradeType.MoveSpeed,_=>UpgradeType.Damage
        };
        return MenuSheetArt.Upgrade(type)??ArcadeKitAssets.Icon("power");
    }
    private static Color SynergyAccent(string id)=>id switch
    {
        "Synergy_Firestorm"=>ArcadeUiTheme.Rgb(0xE8A65C),
        "Synergy_BloodBomb"=>ArcadeUiTheme.Rgb(0xDB8B85),
        "Synergy_ChainReaction"=>ArcadeUiTheme.Rgb(0x8BBFCB),
        "Synergy_VoltaicIgnition"=>ArcadeUiTheme.Rgb(0xDDD180),
        "Synergy_ElementalChaos"=>ArcadeUiTheme.Rgb(0xC2A9D6),
        "Synergy_Bulwark"=>JournalGreen,_=>JournalGold
    };

    private void JBanner(string icon,string title,string text,Color tint)
    {
        float w=JournalWidth;
        var block=JBlock("Section banner",journalPortrait?108:86,JournalInset);
        JIcon(block,icon,15,12,58,58,tint);
        JLabel(block,title,88,7,w-107,32,24,tint,true);
        JLabel(block,text,88,40,w-107,journalPortrait?58:38,18,JournalMuted,false,true);
    }
    private RectTransform JBlock(string title,float height,Color fill,Color? border=null)
    {
        var rect=ArcadeUiFactory.At(content,title,Vector2.up,Vector2.up,Vector2.zero,new Vector2(JournalWidth,height));
        rect.gameObject.AddComponent<LayoutElement>().preferredHeight=height;
        ArcadeUiFactory.Image(rect,JournalCut(),border??JournalEdge,true);
        ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(rect,"Surface",2,2,2,2),JournalCut(),fill,true);
        return rect;
    }
    private static void JMedal(RectTransform parent,float x,float y,float size,int rank,bool earned)
    {
        string[] symbols={"person","shield","award","trophy","crown"};
        Color tint=earned?JournalGold:JournalEdge;
        var rect=JRect(parent,"Rank emblem",x,y,size,size);
        JSprite(rect,ArcadeKitAssets.HexFill,0,0,size,size,tint);
        JSprite(rect,ArcadeKitAssets.HexFill,5,5,size-10,size-10,JournalInset);
        JIcon(rect,symbols[Mathf.Clamp(rank,0,4)],size*.22f,size*.20f,size*.56f,size*.56f,earned?JournalGold:JournalMuted);
    }
    private static void JMapMedal(RectTransform parent,float x,float y,float width,string icon,string title,bool earned)
    {
        var tile=JRect(parent,"Map medal",x,y,width-7,55);
        ArcadeUiFactory.Image(tile,JournalCut(),JournalInset,true);
        JIcon(tile,icon,7,9,35,35,earned?JournalGold:JournalEdge);
        JLabel(tile,title,50,0,width-61,55,18,earned?JournalPaper:JournalMuted,false,true);
        if(earned)JIcon(tile,"check",width-34,0,23,23,JournalGreen);
    }
    private static void JMetric(RectTransform parent,float x,float y,float w,string icon,string title,int value,int goal)
    {
        JIcon(parent,icon,x,y+5,37,37,value>=goal?JournalGreen:JournalGold);
        JLabel(parent,title,x+48,y,w-60,30,19,JournalPaper,true);
        JLabel(parent,Mathf.Min(value,goal)+" / "+goal,x+48,y+31,w-60,25,18,value>=goal?JournalGreen:JournalMuted);
        JProgress(parent,x+48,y+60,w-60,8,value,goal,value>=goal?JournalGreen:JournalGold);
    }
    private static void JProgress(RectTransform parent,float x,float y,float w,float h,int value,int goal,Color color)
    {
        JournalLine(parent,x,y,w,h,JournalInset);
        JournalLine(parent,x,y,w*Mathf.Clamp01(value/(float)Mathf.Max(1,goal)),h,color);
    }
    private static Button JAction(Transform parent,float x,float y,float w,float h,string text,Color fill,Color tint,Action action)=>
        JButton(JRect(parent,text,x,y,w,h),text,fill,tint,action,20);
    private static Button JButton(RectTransform rect,string text,Color fill,Color tint,Action action,float size)
    {
        var button=ArcadeUiFactory.Button(rect,JournalCut(),fill,text,ArcadeKitAssets.BodyBold,size,tint,()=>action());
        var label=button.GetComponentInChildren<TMP_Text>();label.enableAutoSizing=true;label.fontSizeMin=size*.72f;label.fontSizeMax=size;
        return button;
    }
    private static TMP_Text JLabel(Transform parent,string value,float x,float y,float w,float h,float size,Color tint,bool heading=false,bool wrap=false,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        var text=ArcadeUiFactory.Label(JRect(parent,"Text",x,y,Mathf.Max(1,w),h),value,heading?ArcadeKitAssets.Display:ArcadeKitAssets.BodyBold,size,tint,align);
        text.enableAutoSizing=true;text.fontSizeMin=size*.72f;text.fontSizeMax=size;
        text.textWrappingMode=wrap?TextWrappingModes.Normal:TextWrappingModes.NoWrap;
        text.overflowMode=TextOverflowModes.Ellipsis;
        return text;
    }
    private static Image JIcon(Transform parent,string key,float x,float y,float w,float h,Color tint)=>
        JSprite(parent,ArcadeKitAssets.Icon(key),x,y,w,h,tint);
    private static Image JSprite(Transform parent,Sprite sprite,float x,float y,float w,float h,Color tint)
    {
        var image=ArcadeUiFactory.Image(JRect(parent,"Illustration",x,y,w,h),sprite,tint);
        image.preserveAspect=true;return image;
    }
    private static void JournalLine(Transform parent,float x,float y,float w,float h,Color color)
    {
        if(w>0)ArcadeUiFactory.Image(JRect(parent,"Line",x,y,w,h),null,color);
    }
    private static RectTransform JRect(Transform parent,string name,float x,float y,float w,float h)=>
        ArcadeUiFactory.At(parent,name,Vector2.up,Vector2.up,new Vector2(x,-y),new Vector2(w,h));
    private static void JournalPlace(RectTransform rect,float x,float y,float w,float h)
        =>UiArtboard.Place(rect,Vector2.up,Vector2.up,new Vector2(x,-y),new Vector2(w,h));
    private static Sprite JournalCut()
    {
        if(journalCut!=null)return journalCut;
        const int size=64,cut=11;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            int d=Mathf.Min(x,size-1-x)+Mathf.Min(y,size-1-y);
            pixels[x+y*size]=new Color32(255,255,255,(byte)(d>=cut?255:0));
        }
        var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
        tex.SetPixels32(pixels);tex.Apply(false,true);tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
        journalCut=Sprite.Create(tex,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,Vector4.one*(cut+1));
        return journalCut;
    }
}
