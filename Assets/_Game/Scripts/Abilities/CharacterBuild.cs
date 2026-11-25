using UnityEngine;

// Existing skin IDs remain the permanent character IDs and save keys.
public static class CharacterBuild
{
    public readonly struct Profile
    {
        public readonly AbilityKind Active, Auto;
        public readonly string Talent;
        public readonly float Power, Range, Haste, Speed, Health, Crit;
        public Profile(AbilityKind active,AbilityKind auto,string talent,float power=0,float range=0,float haste=0,float speed=0,float health=0,float crit=0)
        { Active=active; Auto=auto; Talent=talent; Power=power; Range=range; Haste=haste; Speed=speed; Health=health; Crit=crit; }
    }
    public static Profile For(string id) => id switch
    {
        "ninja" => new(AbilityKind.CrescentSlash,AbilityKind.OrbitBlades,"Быстрые рефлексы / Quick reflexes",haste:.10f,speed:.08f),
        "cowboy" => new(AbilityKind.CrescentSlash,AbilityKind.RicochetVolley,"Меткий стрелок / Sharpshooter",crit:.10f),
        "king" => new(AbilityKind.Shield,AbilityKind.ShadowFamiliar,"Королевская мощь / Royal power",power:.20f),
        "dinosaur" => new(AbilityKind.ThunderSlam,AbilityKind.OrbitBlades,"Могучее тело / Tough body",health:.20f),
        "bee" => new(AbilityKind.CrescentSlash,AbilityKind.BeeSwarm,"Проворство / Agility",speed:.12f),
        "santa" => new(AbilityKind.FrostNova,AbilityKind.IceLance,"Морозная энергия / Frost energy",range:.15f),
        "astronaut" => new(AbilityKind.GravityWell,AbilityKind.FallingStar,"Космическая энергия / Cosmic energy",haste:.12f),
        "pumpkin" => new(AbilityKind.Bomb,AbilityKind.FlameRing,"Жар / Heat",power:.18f),
        "police" => new(AbilityKind.Shield,AbilityKind.LightningStorm,"Тактическая подготовка / Tactical training",haste:.12f),
        "shark" => new(AbilityKind.CrescentSlash,AbilityKind.IceLance,"Хищник / Predator",crit:.07f,speed:.05f),
        "zombie" => new(AbilityKind.VampirePulse,AbilityKind.GuardianAura,"Живучесть / Resilience",health:.15f),
        "winter" => new(AbilityKind.FrostNova,AbilityKind.OrbitBlades,"Холодная выдержка / Cold endurance",health:.12f),
        "camo" => new(AbilityKind.Bomb,AbilityKind.IceLance,"Тренировка / Training",power:.10f,crit:.04f),
        "gamer" => new(AbilityKind.GravityWell,AbilityKind.LightningStorm,"Быстрые реакции / Fast reactions",haste:.10f),
        "hawaiian" => new(AbilityKind.ThunderSlam,AbilityKind.FallingStar,"Легкость / Light step",speed:.08f,range:.05f),
        "hipster" => new(AbilityKind.FrostNova,AbilityKind.ShadowFamiliar,"Изобретатель / Inventor",range:.10f),
        "neon" => new(AbilityKind.GravityWell,AbilityKind.FlameRing,"Импульс / Pulse",power:.12f),
        "suit" => new(AbilityKind.Shield,AbilityKind.FallingStar,"Расчет / Calculation",crit:.06f,haste:.05f),
        _ => new(AbilityKind.Bomb,AbilityKind.OrbitBlades,"Универсальность / Versatility",health:.08f,power:.06f)
    };
    public static ClothingData Selected()
    {
        if(UpgradeManager.Instance==null) return null;
        foreach(var c in UpgradeManager.Instance.GetAvailableClothing()) if(c!=null&&EquipmentManager.IsEquipped(c)) return c;
        return null;
    }
    public static Profile Current => For(Selected()?.MapPerkId);
    public static bool Innate(AbilityKind kind) { var p=Current; return p.Active==kind||p.Auto==kind; }
    public static void Equip(AbilityManager manager,GameObject player)
    {
        var p=Current;
        manager.SetInnate(p.Active,p.Auto);
        foreach(var a in UpgradeManager.Instance.GetAvailableAbilities())
            if(a!=null&&(a.Kind==p.Active||a.Kind==p.Auto)) manager.EquipAbility(a);
        manager.UpgradePower(p.Power); manager.UpgradeRange(p.Range); manager.ReduceCooldown(p.Haste);
        var stats=player.GetComponent<PlayerStats>();
        if(stats!=null) { if(p.Speed>0) stats.AddMoveSpeedPercent(p.Speed); if(p.Crit>0) stats.AddCriticalChance(p.Crit); }
        if(p.Health>0) player.GetComponent<PlayerHealth>()?.AddMaxHealthPercent(p.Health);
    }
    public static string Describe(ClothingData character)
    {
        var p=For(character.MapPerkId);
        string text=GameplayJourney.T("Q: ","Q: ","Q: ")+AbilityPresentation.Name(p.Active)+" — "+AbilityPresentation.Description(p.Active)+"\n"+
            GameplayJourney.T("Авто: ","Auto: ","Otomatik: ")+AbilityPresentation.Name(p.Auto)+" — "+AbilityPresentation.Description(p.Auto)+"\n";
        string talent=Lang.Code==LangCode.Ru?p.Talent.Split('/')[0].Trim():p.Talent.Split('/')[1].Trim();
        text+=talent+": ";
        if(p.Power>0) text+=$"+{p.Power:P0} "+GameplayJourney.T("сила ","power ","guc ");
        if(p.Range>0) text+=$"+{p.Range:P0} "+GameplayJourney.T("радиус ","range ","menzil ");
        if(p.Haste>0) text+=$"-{p.Haste:P0} CD ";
        if(p.Speed>0) text+=$"+{p.Speed:P0} "+GameplayJourney.T("скорость ","speed ","hiz ");
        if(p.Health>0) text+=$"+{p.Health:P0} HP ";
        if(p.Crit>0) text+=$"+{p.Crit:P0} "+GameplayJourney.T("крит","crit","kritik");
        return text;
    }
}
