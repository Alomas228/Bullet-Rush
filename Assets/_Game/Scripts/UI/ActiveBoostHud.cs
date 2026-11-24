using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ActiveBoostHud : MonoBehaviour
{
    private sealed class Badge
    {
        public RectTransform Root;
        public Image Ring;
        public TMP_Text Seconds;
        public int LastSeconds = -1;
    }

    private RectTransform row;
    private readonly Badge[] badges = new Badge[4];
    private PlayerStats stats;
    private AbilityManager abilities;
    private static readonly Color Green = new Color32(113,216,133,255);
    private static readonly Color Yellow = new Color32(255,202,92,255);
    private static readonly Color Red = new Color32(231,89,74,255);

    public void Build(RectTransform vitals)
    {
        if (row != null) return;
        Vector2 topLeft = new Vector2(0,1);
        row = ArcadeUiFactory.At(vitals,"Active effects",topLeft,topLeft,new Vector2(12,-99),new Vector2(236,54));
        UpgradeType[] icons = { UpgradeType.Damage, UpgradeType.FireRate, UpgradeType.MoveSpeed, UpgradeType.Shield };
        for (int i=0;i<badges.Length;i++)
        {
            RectTransform r=ArcadeUiFactory.At(row,icons[i].ToString(),topLeft,topLeft,Vector2.zero,new Vector2(49,49));
            ArcadeUiFactory.Image(r,CombatCircleArt.Disc,new Color32(37,47,48,234));
            ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(r,"Ring track"),CombatCircleArt.Ring,new Color32(89,100,96,235));
            Image ring=ArcadeUiFactory.Image(ArcadeUiFactory.Stretch(r,"Time ring"),CombatCircleArt.Ring,Green);
            ring.type=Image.Type.Filled; ring.fillMethod=Image.FillMethod.Radial360; ring.fillOrigin=(int)Image.Origin360.Top;
            ring.fillClockwise=true;
            var image=ArcadeUiFactory.Image(ArcadeUiFactory.At(r,"Effect",Vector2.one*.5f,Vector2.one*.5f,new Vector2(0,3),new Vector2(30,30)),
                MenuSheetArt.Upgrade(icons[i]),Color.white);
            image.preserveAspect=true;
            TMP_Text seconds=ArcadeUiFactory.Label(r,"Seconds","",ArcadeKitAssets.BodyBold,12,MenuSheet.Paper,
                TextAlignmentOptions.Center,Vector2.one*.5f,Vector2.one*.5f,new Vector2(0,-16),new Vector2(31,15));
            badges[i]=new Badge { Root=r,Ring=ring,Seconds=seconds };
            r.gameObject.SetActive(false);
        }
        row.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (row == null) return;
        row.anchoredPosition = new Vector2(12, RunOxygenHud.Visible ? -162 : -99);
        bool playing=GameStateManager.Instance != null && GameStateManager.Instance.CurrentState==GameState.Playing && !GameModeManager.IsPvp;
        if (!playing || PlayerHealth.Instance == null || !PlayerHealth.Instance.gameObject.activeInHierarchy)
        { if(row.gameObject.activeSelf) row.gameObject.SetActive(false); return; }
        if (stats==null || stats.gameObject!=PlayerHealth.Instance.gameObject)
        {
            stats=PlayerHealth.Instance.GetComponent<PlayerStats>();
            abilities=PlayerHealth.Instance.GetComponent<AbilityManager>();
        }
        int visible=0;
        for (int i=0;i<3;i++)
        {
            var boost=(PlayerStats.BattleBoost)i;
            Show(badges[i],stats!=null?stats.BattleBoostRemaining(boost):0f,stats!=null?stats.BattleBoostFraction(boost):0f,ref visible);
        }
        ShieldAbility shield=abilities!=null?abilities.Shield:null;
        Show(badges[3],shield!=null?shield.ActiveRemaining:0f,shield!=null?shield.ActiveFraction:0f,ref visible);
        if (row.gameObject.activeSelf!=(visible>0)) row.gameObject.SetActive(visible>0);
    }

    private static void Show(Badge badge,float seconds,float fraction,ref int visible)
    {
        bool active=seconds>.001f;
        if (badge.Root.gameObject.activeSelf!=active) badge.Root.gameObject.SetActive(active);
        if (!active) return;
        badge.Root.anchoredPosition=new Vector2(visible++*55f,0);
        badge.Ring.fillAmount=fraction;
        badge.Ring.color=fraction>.5f?Color.Lerp(Yellow,Green,(fraction-.5f)*2f):Color.Lerp(Red,Yellow,fraction*2f);
        int count=Mathf.CeilToInt(seconds);
        if (badge.LastSeconds!=count) { badge.Seconds.SetText("{0}",count);badge.LastSeconds=count; }
    }
}
