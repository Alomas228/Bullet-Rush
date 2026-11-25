using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public partial class GameOverUI
{
    private static readonly Color ResultPaper = ArcadeUiTheme.Rgb(0xF4ECD3);
    private static readonly Color ResultGold = ArcadeUiTheme.Rgb(0xF4BD58);
    private static readonly Color ResultMuted = ArcadeUiTheme.Rgb(0xC3C4A8);
    private static readonly Color ResultBorder = ArcadeUiTheme.Rgb(0x82765B);
    private static readonly Color ResultInk = new Color(.095f,.12f,.095f,.96f);
    private static readonly Color ReviveGreen = ArcadeUiTheme.Rgb(0xBCD879);
    private static Sprite resultCut, resultVignette;
    private static readonly Dictionary<string,Sprite> resultGlyphs = new Dictionary<string,Sprite>();

    private RectTransform modernBoard, modernTitleGroup, modernSummary, modernRevive, modernDouble,
        modernDetailsButton, modernJournalButton, modernNavigation, modernDetailLayer, modernDetailBoard;
    private RectTransform modernDetailPanel,modernDetailBack;
    private TMP_Text modernTitle, modernSubtitle, modernScore, modernRank, modernWave, modernKills, modernTime,
        modernCoins, modernLevel, modernXp, modernXpDelta, modernDoubleValue, modernDoubleStatus, modernReviveStatus;
    private TMP_Text modernReviveTitle, modernReviveSubtitle, modernDoubleTitle;
    private TMP_Text modernRankHint;
    private Image modernRankRim,modernRankBand;
    private Image modernXpFill;
    private CanvasGroup modernReviveGroup, modernDoubleGroup;
    private CanvasGroup modernResultsGroup;
    private readonly TMP_Text[] modernDetailLabels = new TMP_Text[18];
    private readonly TMP_Text[] modernDetailValues = new TMP_Text[18];
    private readonly RectTransform[] modernDetailRules = new RectTransform[18];
    private bool? modernPortrait;
    private int shownCoins=-1,shownBonus=-1,shownActionState=-1;

    internal bool UsesModernResults => gameOverPanel != null && gameOverPanel.transform is RectTransform;

    private static string ResultText(string ru,string en,string tr) =>
        Lang.Code==LangCode.Ru?ru:Lang.Code==LangCode.Tr?tr:en;

    private bool EnsureModernResults()
    {
        if (modernBoard != null) return true;
        if (!UsesModernResults) return false;
        RectTransform root=(RectTransform)gameOverPanel.transform;
        UiArtboard.Stretch(root);
        // Hide legacy controls, not the controller or its serialized data.
        for(int i=0;i<root.childCount;i++) root.GetChild(i).gameObject.SetActive(false);
        if(root.TryGetComponent<Image>(out Image oldDim))
        {
            oldDim.sprite=null;
            oldDim.color=new Color(.055f,.07f,.045f,.28f);
            oldDim.raycastTarget=true;
        }
        else ArcadeUiFactory.Image(root,null,new Color(.055f,.07f,.045f,.28f)).raycastTarget=true;
        ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(root,"Battle focus"),ResultVignette(),Color.white);
        modernBoard=UiArtboard.Create(root,new Vector2(1600,900),true);
        modernBoard.name="Battle result";
        modernResultsGroup=modernBoard.gameObject.AddComponent<CanvasGroup>();

        modernTitleGroup=ResultRect(modernBoard,"Title",40,16,1520,150);
        modernTitle=ResultLabel(modernTitleGroup,"GameOverText",0,0,1520,108,"",86,ResultPaper,true,TextAlignmentOptions.Center);
        modernTitle.fontStyle=FontStyles.Italic;
        ResultShadow(modernTitle);
        var titleStrip=ResultRect(modernTitleGroup,"Arena banner",490,109,540,47);
        ArcadeUiFactory.Image(titleStrip,ResultCut(),new Color(.11f,.15f,.12f,.88f),true);
        modernSubtitle=ResultLabel(titleStrip,"Arena and wave",10,0,520,47,"",28,ResultGold,true,TextAlignmentOptions.Center);
        ResultLine(modernTitleGroup,"Left accent",380,130,90,4,ResultGold);
        ResultLine(modernTitleGroup,"Right accent",1050,130,90,4,ResultGold);

        BuildModernSummary();
        modernRevive=BuildModernOffer("Revive",ReviveGreen,ArcadeUiTheme.Rgb(0x344822),ArcadeKitAssets.Icon("heart"),
            OnReviveClicked,out reviveButton,out modernReviveGroup,out modernReviveTitle,out modernReviveSubtitle,out modernReviveStatus);
        modernDouble=BuildModernOffer("Double coins",ResultGold,ArcadeUiTheme.Rgb(0x594019),ArcadeKitAssets.Coin,
            OnDoubleCoinsClicked,out doubleCoinsButton,out modernDoubleGroup,out modernDoubleTitle,out modernDoubleValue,out modernDoubleStatus);

        modernDetailsButton=ResultRect(modernBoard,"Details action",0,0,440,55);
        ResultButton(modernDetailsButton,ResultBorder,ResultInk,()=>OpenModernDetails());
        ResultLabel(modernDetailsButton,"Caption",15,0,410,55,ResultText("ПОДРОБНАЯ СТАТИСТИКА  ›","DETAILED STATS  ›","AYRINTILI ISTATISTIK  ›"),24,ResultPaper,true,TextAlignmentOptions.Center);
        modernJournalButton=ResultRect(modernBoard,"Journey",0,0,280,55);
        ResultButton(modernJournalButton,ResultBorder,ResultInk,()=>GameplayJourney.Instance?.UI?.OpenJournal());
        ResultIcon(modernJournalButton,"Journal icon",ArcadeKitAssets.Icon("book"),12,11,33,33,ResultGold);
        ResultLabel(modernJournalButton,"Caption",54,0,218,55,ResultText("Путь бойца","Fighter's journey","Savascinin yolu"),24,ResultPaper);
        modernNavigation=ResultRect(modernBoard,"Exit actions",0,0,920,112);
        restartButton=BuildModernNav(modernNavigation,"Restart",0,ResultGlyph("restart"),
            ResultText("РЕСТАРТ","RESTART","TEKRAR"),ResultText("Новый забег","New run","Yeni oyun"),OnRestartClicked);
        menuButton=BuildModernNav(modernNavigation,"Camp",480,ResultGlyph("tent"),
            ResultText("В ЛАГЕРЬ","TO CAMP","KAMPA"),ResultText("В главное меню","Main menu","Ana menu"),OnMenuClicked);
        BuildModernDetails(root);
        FitModernResults();
        return true;
    }

    private void BuildModernSummary()
    {
        modernSummary=ResultRect(modernBoard,"Summary",0,0,560,450);
        ResultFrame(modernSummary,ResultBorder,ResultInk,4);
        ResultLabel(modernSummary,"Score caption",28,22,330,35,ResultText("СЧЁТ","SCORE","SKOR"),34,ResultPaper,true);
        modernScore=ResultLabel(modernSummary,"Final score",28,62,350,105,"0",76,ResultGold,true);
        var medal=ResultRect(modernSummary,"Rank badge",393,24,130,132);
        modernRankRim=ArcadeUiFactory.Image(ResultRect(medal,"Medal rim",0,0,130,108),ArcadeKitAssets.HexFill,ResultGold);
        ArcadeUiFactory.Image(ResultRect(medal,"Medal inner",9,8,112,92),ArcadeKitAssets.HexFill,ArcadeUiTheme.Rgb(0x67471D));
        modernRank=ResultLabel(medal,"Rank",4,6,122,93,"—",64,ResultPaper,true,TextAlignmentOptions.Center);
        var rankBand=ResultRect(medal,"Rank strap",5,103,120,27);
        modernRankBand=ArcadeUiFactory.Image(rankBand,ResultCut(),ResultGold,true);
        ResultLabel(rankBand,"Rank label",3,0,114,27,ResultText("РАНГ","RANK","RUTBE"),19,ResultInk,true,TextAlignmentOptions.Center);
        modernRankHint=ResultLabel(modernSummary,"Rank description",368,155,180,18,"",16,ResultMuted,false,TextAlignmentOptions.Center);
        ResultLine(modernSummary,"Divider",25,175,510,2,ResultBorder);
        modernWave=BuildResultMetric(modernSummary,26,ResultGlyph("wave"),ResultText("ВОЛНА","WAVE","DALGA"));
        modernKills=BuildResultMetric(modernSummary,198,ArcadeKitAssets.Icon("target"),ResultText("УБИЙСТВ","KILLS","LESLER"));
        modernTime=BuildResultMetric(modernSummary,370,ResultGlyph("clock"),ResultText("ВРЕМЯ","TIME","SURE"));
        ResultLine(modernSummary,"Metric separator 1",188,190,2,65,ResultBorder);
        ResultLine(modernSummary,"Metric separator 2",360,190,2,65,ResultBorder);
        ResultLine(modernSummary,"Reward divider",25,269,510,2,ResultBorder);
        ResultIcon(modernSummary,"Coin",ArcadeKitAssets.Coin,126,285,62,62,Color.white);
        modernCoins=ResultLabel(modernSummary,"Earned coins",197,278,332,72,"+ 0",53,ResultGold,true);
        ResultLine(modernSummary,"XP divider",25,355,510,2,ResultBorder);
        modernLevel=ResultLabel(modernSummary,"Level",27,367,235,31,"",25,ResultPaper,true);
        modernXp=ResultLabel(modernSummary,"Progress",258,367,273,31,"",25,ResultPaper,false,TextAlignmentOptions.MidlineRight);
        ResultLine(modernSummary,"Level track",27,407,506,15,ArcadeUiTheme.Rgb(0x4F503E));
        modernXpFill=ArcadeUiFactory.Image(ResultRect(modernSummary,"Level fill",27,407,506,15),null,ResultGold);
        // A width-based progress fill uses no interactive slider or extra material.
        modernXpDelta=ResultLabel(modernSummary,"XP gain",27,426,506,20,"",16,ResultMuted,false,TextAlignmentOptions.Center);
    }

    private TMP_Text BuildResultMetric(RectTransform parent,float x,Sprite icon,string caption)
    {
        ResultIcon(parent,"Metric icon",icon,x,197,44,44,ResultPaper);
        ResultLabel(parent,"Caption",x+48,188,117,25,caption,19,ResultMuted,true);
        return ResultLabel(parent,"Value",x+48,215,117,48,"0",36,ResultPaper,true);
    }

    private RectTransform BuildModernOffer(string name, Color accent,Color fill,Sprite icon,UnityAction action,
        out Button button,out CanvasGroup group,out TMP_Text title,out TMP_Text value,out TMP_Text status)
    {
        var rect=ResultRect(modernBoard,name,0,0,560,210);
        button=ResultButton(rect,accent,fill,action);
        group=rect.gameObject.AddComponent<CanvasGroup>();
        ResultIcon(rect,"Action symbol",icon,36,29,108,108,name=="Revive"?ReviveGreen:Color.white);
        if(name=="Revive") ResultIcon(rect,"Medical cross",ArcadeKitAssets.Icon("plus"),72,64,36,36,fill);
        title=ResultLabel(rect,"Action title",165,23,370,59,"",43,ResultPaper,true);
        value=ResultLabel(rect,"Action explanation",165,87,369,49,"",29,accent,true);
        var bar=ResultRect(rect,"Advertisement strip",30,151,500,45);
        ArcadeUiFactory.Image(bar,ResultCut(),new Color(.035f,.055f,.035f,.68f),true);
        ResultIcon(bar,"Video",ResultGlyph("video"),22,7,38,31,ResultPaper);
        status=ResultLabel(bar,"Advertisement status",73,0,407,45,"",25,ResultPaper,true,TextAlignmentOptions.Center);
        return rect;
    }

    private Button BuildModernNav(RectTransform parent,string name,float x,Sprite icon,string title,string subtitle,UnityAction action)
    {
        var rect=ResultRect(parent,name,x,0,440,112);
        Button button=ResultButton(rect,ResultBorder,ResultInk,action);
        ResultIcon(rect,"Icon",icon,24,19,76,76,ResultPaper);
        ResultLine(rect,"Icon divider",116,16,2,80,ResultBorder);
        ResultLabel(rect,"Button title",137,13,283,47,title,34,ResultPaper,true);
        ResultLabel(rect,"Button subtitle",137,62,283,39,subtitle,25,ResultMuted);
        return button;
    }

    private void BuildModernDetails(RectTransform root)
    {
        modernDetailLayer=ArcadeUiFactory.Stretch(root,"Detailed battle report");
        ArcadeUiFactory.Image(modernDetailLayer,null,new Color(.04f,.065f,.045f,.9f)).raycastTarget=true;
        modernDetailBoard=UiArtboard.Create(modernDetailLayer,new Vector2(900,840),true);
        var panel=ResultRect(modernDetailBoard,"Report",25,25,850,790);
        modernDetailPanel=panel;
        ResultFrame(panel,ResultBorder,ResultInk,4);
        ResultLabel(panel,"Report title",28,20,680,74,ResultText("ПОДРОБНАЯ СТАТИСТИКА","DETAILED STATS","AYRINTILI ISTATISTIK"),38,ResultPaper,true);
        var close=ResultRect(panel,"Close report",748,26,74,60);
        ResultButton(close,ResultBorder,ResultInk,()=>modernDetailLayer.gameObject.SetActive(false));
        ResultIcon(close,"Close",ArcadeKitAssets.Icon("close"),18,13,38,34,ResultPaper);
        ResultLine(panel,"Title divider",28,105,794,2,ResultBorder);
        for(int i=0;i<modernDetailLabels.Length;i++)
        {
            float x=i<9?28:435,y=125+(i%9)*61;
            modernDetailLabels[i]=ResultLabel(panel,"Stat label "+i,x,y,370,27,"",21,ResultMuted);
            modernDetailValues[i]=ResultLabel(panel,"Stat value "+i,x,y+27,370,30,"",26,ResultPaper,true);
            modernDetailRules[i]=ResultRect(panel,"Stat divider",x,y+58,370,1);
            ArcadeUiFactory.Image(modernDetailRules[i],null,new Color(.5f,.49f,.36f,.35f));
        }
        var back=ResultRect(panel,"Back",234,700,382,64);
        modernDetailBack=back;
        ResultButton(back,ResultBorder,ResultInk,()=>modernDetailLayer.gameObject.SetActive(false));
        ResultLabel(back,"Back",10,0,362,64,ResultText("НАЗАД К ИТОГАМ","BACK TO RESULTS","SONUCLARA DON"),25,ResultPaper,true,TextAlignmentOptions.Center);
        modernDetailLayer.gameObject.SetActive(false);
    }

    private void FitModernResults()
    {
        if(modernResultsGroup!=null && modernResultsGroup.alpha<1f)
        {
            modernResultsGroup.alpha=Mathf.MoveTowards(modernResultsGroup.alpha,1f,Time.unscaledDeltaTime*5f);
            modernResultsGroup.blocksRaycasts=modernResultsGroup.alpha>.65f;
        }
        bool portrait=ResponsiveUi.Portrait;
        if(modernPortrait==portrait) return;
        modernPortrait=portrait;
        UiArtboard.Resize(modernBoard,portrait?new Vector2(900,1570):new Vector2(1600,900));
        if(portrait)
        {
            PlaceResult(modernTitleGroup,24,18,.56f);
            PlaceResult(modernSummary,100,135,1.25f);
            PlaceResult(modernRevive,100,720,1.25f);
            PlaceResult(modernDouble,100,1000,1.25f);
            PlaceResult(modernDetailsButton,45,1280,1.12f);
            PlaceResult(modernJournalButton,550,1280,1.07f);
            PlaceResult(modernNavigation,36,1380,.9f);
        }
        else
        {
            PlaceResult(modernTitleGroup,40,18,1);
            PlaceResult(modernSummary,38,204,1);
            PlaceResult(modernRevive,1002,204,1);
            PlaceResult(modernDouble,1002,444,1);
            PlaceResult(modernDetailsButton,580,683,1);
            PlaceResult(modernJournalButton,1150,683,1);
            PlaceResult(modernNavigation,340,764,1);
        }
        LayoutModernDetails(portrait);
    }

    private void LayoutModernDetails(bool portrait)
    {
        UiArtboard.Resize(modernDetailBoard,portrait?new Vector2(900,1420):new Vector2(900,840));
        modernDetailPanel.sizeDelta=new Vector2(850,portrait?1370:790);
        modernDetailBack.anchoredPosition=new Vector2(234,portrait?-1280:-700);
        for(int i=0;i<modernDetailLabels.Length;i++)
        {
            var label=modernDetailLabels[i];var value=modernDetailValues[i];var line=modernDetailRules[i];
            float x=i<9?28:435,y=125+(i%9)*61;
            if(portrait)
            {
                y=120+i*63;
                label.rectTransform.anchoredPosition=new Vector2(28,-y);
                label.rectTransform.sizeDelta=new Vector2(540,55);
                label.fontSizeMax=28;label.fontSizeMin=18;
                value.rectTransform.anchoredPosition=new Vector2(580,-y);
                value.rectTransform.sizeDelta=new Vector2(222,55);
                value.fontSizeMax=31;value.fontSizeMin=21;value.alignment=TextAlignmentOptions.MidlineRight;
                line.anchoredPosition=new Vector2(28,-(y+58));
                line.sizeDelta=new Vector2(794,1);
                line.gameObject.SetActive(i<17);
            }
            else
            {
                label.rectTransform.anchoredPosition=new Vector2(x,-y);
                label.rectTransform.sizeDelta=new Vector2(370,27);
                label.fontSizeMax=21;label.fontSizeMin=14;
                value.rectTransform.anchoredPosition=new Vector2(x,-(y+27));
                value.rectTransform.sizeDelta=new Vector2(370,30);
                value.fontSizeMax=26;value.fontSizeMin=17;value.alignment=TextAlignmentOptions.MidlineLeft;
                line.anchoredPosition=new Vector2(x,-(y+58));
                line.sizeDelta=new Vector2(370,1);
                line.gameObject.SetActive(i%9<8);
            }
        }
    }

    private void ShowModernResults()
    {
        var manager=GameOverManager.Instance;
        RunResult result=manager?.LastResult;
        var wave=FindAnyObjectByType<WaveManager>();
        int reached=wave!=null?wave.CurrentWave:result?.wavesCleared??0;
        string map=result?.mapName;
        if(string.IsNullOrEmpty(map)) map=FindAnyObjectByType<EnvironmentController>()?.CurrentMap?.LocalizedName??"";
        bool victory=manager!=null&&manager.WasVictory;
        modernTitle.text=Lang.Get(victory?"boss_campaign.victory":"over.title").ToUpperInvariant();
        modernSubtitle.text=(string.IsNullOrEmpty(map)?"":map.ToUpperInvariant()+"  •  ")+ResultText("ВОЛНА ","WAVE ","DALGA ")+reached;
        modernScore.text=ResultNumber(result?.finalScore??ScoreManager.Instance?.Score??0);
        modernRank.text=string.IsNullOrEmpty(result?.rank)?"—":result.rank;
        Color rankAccent=modernRank.text switch
        {
            "S+"=>ArcadeUiTheme.Rgb(0xFFE3A4),"S"=>ResultGold,"A"=>ArcadeUiTheme.Rgb(0xF5CC6A),
            "B"=>ResultGold,"C"=>ArcadeUiTheme.Rgb(0xB2C2B6),_=>ArcadeUiTheme.Rgb(0xA5ADA4)
        };
        modernRankRim.color=modernRankBand.color=rankAccent;
        modernRankHint.text=result==null?"":Lang.Get(result.rank switch
        {
            "S+"=>"rank.s_plus","S"=>"rank.s","A"=>"rank.a","B"=>"rank.b","C"=>"rank.c",_=>"rank.f"
        });
        modernWave.text=reached.ToString();
        modernKills.text=ResultNumber(result?.kills??ScoreManager.Instance?.Kills??0);
        modernTime.text=ResultTime(result?.runTime??0);
        modernReviveTitle.text=ResultText("ВОЗРОДИТЬСЯ","REVIVE","CANLAN");
        modernReviveSubtitle.text=ResultText("Продолжить этот бой","Continue this run","Bu savasa devam et");
        modernDoubleTitle.text=ResultText("×2 МОНЕТЫ","×2 COINS","×2 ALTIN");
        coinsDoubled=XpManager.Instance==null||XpManager.Instance.RunCoins<=XpManager.Instance.RunCoinAdBonus;
        modernDetailLayer.gameObject.SetActive(false);
        modernResultsGroup.alpha=0f;
        modernResultsGroup.blocksRaycasts=false;
        shownActionState=-1;
        RefreshModernActions(true);
        PopulateModernDetails(result);
        if(xpAnimCoroutine!=null) StopCoroutine(xpAnimCoroutine);
        if(XpManager.Instance!=null) xpAnimCoroutine=StartCoroutine(AnimateXpSlider(XpManager.Instance));
        else
        {
            modernLevel.text="—";modernXp.text="—";modernXpDelta.text="";
            modernXpFill.rectTransform.sizeDelta=new Vector2(0,15);
        }
    }

    private void DisplayModernXp(XpManager xp,int totalXp,int level)
    {
        bool max=level>=GameBalance.MaxPlayerLevel || xp.GetXPForLevel(level+1)<=xp.GetXPForLevel(level);
        int start=xp.GetXPForLevel(level);
        int needed=Mathf.Max(1,xp.GetXPForLevel(level+1)-start);
        int progress=Mathf.Clamp(totalXp-start,0,needed);
        modernLevel.text=Lang.Get("over.level",level).ToUpperInvariant();
        modernXp.text=max?ResultText("МАКСИМУМ","MAX LEVEL","MAKSIMUM"):ResultNumber(progress)+" / "+ResultNumber(needed);
        modernXpFill.rectTransform.sizeDelta=new Vector2(506*(max?1f:(float)progress/needed),15);
        modernXpDelta.text=xp.LastRunReward>0?Lang.Get("menu.player_xp",xp.LastRunReward):"";
        if(xp.LevelsGainedLastRun>0&&totalXp>=xp.GlobalXP)
            modernXpDelta.text=Lang.Get("menu.level_up",xp.LastLevelBeforeGrant,xp.GetPlayerLevel());
    }

    private void RefreshModernActions(bool force=false)
    {
        var xp=XpManager.Instance;
        int coins=xp?.RunCoins??0,bonus=xp?.RunCoinAdBonus??0;
        bool reviveAllowed=GameOverManager.Instance!=null&&GameOverManager.Instance.CanRevive;
        bool doubleAllowed=coins>bonus&&!coinsDoubled&&!GameModeManager.IsPvp;
        bool adAvailable=YandexGameManager.Instance!=null;
        bool busy=adAvailable&&YandexGameManager.Instance.IsRewardedBusy;
        int state=(reviveAllowed?1:0)|(doubleAllowed?2:0)|(adAvailable?4:0)|(busy?8:0);
        if(!force&&shownCoins==coins&&shownBonus==bonus&&shownActionState==state) return;
        shownCoins=coins;shownBonus=bonus;shownActionState=state;
        modernCoins.text="+ "+ResultNumber(coins+bonus);
        modernDoubleValue.text=GameModeManager.IsPvp?ResultText("Недоступно в PVP","Unavailable in PVP","PVP'de kullanilamaz"):doubleAllowed?ResultNumber(coins+bonus)+"  →  "+ResultNumber(coins*2):
            bonus>0?ResultText("Награда получена","Reward claimed","Odul alindi"):ResultText("Нет монет для удвоения","No coins to double","Iki kat icin altin yok");
        reviveButton.interactable=reviveAllowed&&adAvailable&&!busy;
        doubleCoinsButton.interactable=doubleAllowed&&adAvailable&&!busy;
        restartButton.interactable=menuButton.interactable=!busy;
        modernReviveGroup.alpha=reviveAllowed&&adAvailable?1f:.55f;
        modernDoubleGroup.alpha=doubleAllowed&&adAvailable?1f:.55f;
        string advertisement=ResultText("ЗА РЕКЛАМУ","WATCH AN AD","REKLAM IZLE");
        string unavailable=ResultText("Реклама недоступна","Ad unavailable","Reklam yok");
        string pending=ResultText("Загрузка рекламы…","Loading ad…","Reklam yukleniyor…");
        modernReviveStatus.text=!reviveAllowed?ResultText("Недоступно","Unavailable","Kullanilamaz"):!adAvailable?unavailable:busy?pending:advertisement;
        modernDoubleStatus.text=!doubleAllowed?(bonus>0?ResultText("ПОЛУЧЕНО","CLAIMED","ALINDI"):ResultText("Недоступно","Unavailable","Kullanilamaz")):!adAvailable?unavailable:busy?pending:advertisement;
    }

    private void OpenModernDetails()
    {
        PopulateModernDetails(GameOverManager.Instance?.LastResult);
        modernDetailLayer.gameObject.SetActive(true);
    }

    private void PopulateModernDetails(RunResult r)
    {
        string[] labels={
            ResultText("Базовый счёт","Base score","Temel skor"),ResultText("Бонус за стиль","Style bonus","Stil bonusu"),
            ResultText("Убийства","Kills","Lesler"),ResultText("Волны завершены","Waves cleared","Dalgalar"),
            ResultText("Лучшее комбо","Max combo","En iyi kombo"),ResultText("Идеальные волны","Perfect waves","Kusursuz dalgalar"),
            ResultText("Критические попадания","Critical hits","Kritik vurus"),
            ResultText("Использовано способностей","Abilities used","Yetenekler"),ResultText("Эффективность урона","Damage efficiency","Hasar verimi"),
            ResultText("Время забега","Run time","Oyun suresi"),ResultText("Среднее время убийства","Average kill time","Ortalama les suresi"),
            ResultText("Самое быстрое убийство","Fastest kill","En hizli les"),
            ResultText("До первого убийства","First kill","Ilk les"),ResultText("До первого босса","First boss","Ilk boss"),
            ResultText("Лучшая зачистка волны","Best wave clear","En iyi dalga"),
            ResultText("Уклонения рывком","Dash dodges","Kacinma"),ResultText("Массовые убийства","Multi-kills","Coklu les"),ResultText("Нанесено урона","Damage dealt","Verilen hasar")};
        string[] values=r==null?null:new[]{ResultNumber(r.score),"+"+ResultNumber(r.styleBonus),ResultNumber(r.kills),ResultNumber(r.wavesCleared),
            ResultNumber(r.maxCombo),ResultNumber(r.perfectWaves),ResultNumber(r.totalCriticalHits),ResultNumber(r.abilitiesUsed),r.damageEfficiency.ToString("0.0")+"×",
            ResultTime(r.runTime),ResultTime(r.averageKillTime,true),ResultTime(r.fastestKillTime,true),ResultTime(r.timeToFirstKill,true),
            ResultTime(r.timeToFirstBoss,true),ResultTime(r.bestWaveClearTime,true),ResultNumber(r.dashDodges),ResultNumber(r.multiKillStreaks),ResultNumber(Mathf.RoundToInt(r.totalDamageDealt))};
        for(int i=0;i<labels.Length;i++)
        {
            modernDetailLabels[i].text=labels[i];
            modernDetailValues[i].text=values==null?"—":values[i];
        }
    }

    private static string ResultNumber(int number)=>number.ToString("N0",System.Globalization.CultureInfo.InvariantCulture).Replace(',',' ');
    private static string ResultTime(float seconds,bool unknown=false)
    {
        if(unknown&&seconds<=0) return "--:--";
        int s=Mathf.Max(0,Mathf.FloorToInt(seconds));
        return (s/60).ToString("00")+":"+(s%60).ToString("00");
    }

    private static RectTransform ResultRect(Transform parent,string name,float x,float y,float w,float h)
    {
        var top=new Vector2(0,1);
        return ArcadeUiFactory.At(parent,name,top,top,new Vector2(x,-y),new Vector2(w,h));
    }
    private static void PlaceResult(RectTransform group,float x,float y,float scale)
    {
        group.anchoredPosition=new Vector2(x,-y);
        group.localScale=Vector3.one*scale;
    }
    private static TMP_Text ResultLabel(Transform parent,string name,float x,float y,float w,float h,string text,float font,Color tint,bool display=false,TextAlignmentOptions align=TextAlignmentOptions.MidlineLeft)
    {
        var label=ArcadeUiFactory.Label(ResultRect(parent,name,x,y,w,h),text,display?ArcadeKitAssets.Display:ArcadeKitAssets.BodyBold,font,tint,align);
        label.enableAutoSizing=true;label.fontSizeMin=font*.65f;label.fontSizeMax=font;
        label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
        return label;
    }
    private static void ResultShadow(TMP_Text text)
    {
        var shadow=text.gameObject.AddComponent<Shadow>();
        shadow.effectColor=new Color(0,0,0,.65f);shadow.effectDistance=new Vector2(3,-4);
    }
    private static void ResultLine(Transform parent,string name,float x,float y,float w,float h,Color color)=>
        ArcadeUiFactory.Image(ResultRect(parent,name,x,y,w,h),null,color);
    private static void ResultIcon(Transform parent,string name,Sprite sprite,float x,float y,float w,float h,Color color)
    {
        var img=ArcadeUiFactory.Image(ResultRect(parent,name,x,y,w,h),sprite,color);
        img.preserveAspect=true;
    }
    private static Image ResultFrame(RectTransform rect,Color outline,Color fill,float stroke=4)
    {
        Image rim=ArcadeUiFactory.Image(rect,ResultCut(),outline,true);
        ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(rect,"Inset",stroke,stroke,stroke,stroke),ResultCut(),fill,true);
        return rim;
    }
    private static Button ResultButton(RectTransform rect,Color outline,Color fill,UnityAction action)
    {
        Image image=ResultFrame(rect,outline,fill);
        image.raycastTarget=true;
        var button=rect.gameObject.AddComponent<Button>();
        button.targetGraphic=image;
        button.navigation=new Navigation{mode=Navigation.Mode.None};
        var colors=button.colors;colors.normalColor=new Color(.93f,.93f,.93f,1f);colors.highlightedColor=Color.white;
        colors.pressedColor=new Color(.73f,.73f,.73f,1f);colors.disabledColor=new Color(.65f,.65f,.65f,1f);button.colors=colors;
        button.onClick.AddListener(action);
        return button;
    }
    private static Sprite ResultCut()
    {
        if(resultCut!=null)return resultCut;
        const int size=96,cut=17;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            int a=Mathf.Min(x,size-1-x)+Mathf.Min(y,size-1-y);
            pixels[x+y*size]=new Color32(255,255,255,(byte)(a>=cut?255:a==cut-1?128:0));
        }
        Texture2D tex=new Texture2D(size,size,TextureFormat.RGBA32,false);tex.SetPixels32(pixels);tex.Apply(false,true);
        tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
        resultCut=Sprite.Create(tex,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,Vector4.one*(cut+1));
        return resultCut;
    }
    private static Sprite ResultVignette()
    {
        if(resultVignette!=null)return resultVignette;
        const int size=96;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=x/(size-1f)*2-1,v=y/(size-1f)*2-1;
            float edge=Mathf.SmoothStep(0,1,Mathf.Clamp01((Mathf.Sqrt(u*u+v*v)-.38f)/.98f));
            pixels[x+y*size]=new Color(.055f,.075f,.045f,edge*.60f);
        }
        Texture2D tex=new Texture2D(size,size,TextureFormat.RGBA32,false);tex.SetPixels32(pixels);tex.Apply(false,true);
        tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
        resultVignette=Sprite.Create(tex,new Rect(0,0,size,size),Vector2.one*.5f);
        return resultVignette;
    }
    private static Sprite ResultGlyph(string kind)
    {
        if(resultGlyphs.TryGetValue(kind,out Sprite cached))return cached;
        const int size=80;
        var pixels=new Color32[size*size];
        for(int y=0;y<size;y++)for(int x=0;x<size;x++)
        {
            float u=(x+.5f)/size*2-1,v=(y+.5f)/size*2-1;
            float radius=Mathf.Sqrt(u*u+v*v), angle=Mathf.Atan2(v,u)*Mathf.Rad2Deg;
            bool on=kind switch
            {
                "restart"=>(radius>.55f&&radius<.80f&&(angle<28||angle>85))||(u>.32f&&u<.89f&&v>.12f&&v<.82f&&v<1.11f-u),
                "tent"=>(v>-.73f&&v<.82f&&Mathf.Abs(u)<(.82f-v)*.58f&&!(v<-.25f&&Mathf.Abs(u)<.18f)),
                "clock"=>(radius>.62f&&radius<.80f)||(Mathf.Abs(u)<.10f&&v>-.02f&&v<.47f)||(Mathf.Abs(v)<.09f&&u>0&&u<.38f)||(Mathf.Abs(u)<.24f&&v>.76f&&v<.93f),
                "wave"=>Mathf.Abs(v-.35f*Mathf.Sin(u*3.5f)-.26f)<.10f||Mathf.Abs(v-.35f*Mathf.Sin(u*3.5f)+.26f)<.10f,
                "video"=>((Mathf.Abs(u)<.84f&&Mathf.Abs(v)<.58f)&&(Mathf.Abs(u)>.65f||Mathf.Abs(v)>.42f))||(u>-.30f&&u<.39f&&Mathf.Abs(v)<(.39f-u)*.60f),
                _=>radius<.7f
            };
            pixels[x+y*size]=new Color32(255,255,255,(byte)(on?255:0));
        }
        var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);tex.SetPixels32(pixels);tex.Apply(false,true);
        tex.wrapMode=TextureWrapMode.Clamp;tex.filterMode=FilterMode.Bilinear;
        cached=Sprite.Create(tex,new Rect(0,0,size,size),Vector2.one*.5f);
        resultGlyphs[kind]=cached;return cached;
    }
}
