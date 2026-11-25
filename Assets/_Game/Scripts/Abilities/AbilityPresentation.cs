using UnityEngine;
using static GameplayJourney;

public static class AbilityPresentation
{
    public const int ActiveSlots = 2, AutomaticSlots = 3;
    public static string Type(bool automatic) => automatic ? T("АВТО", "AUTO", "OTOMATIK") : T("АКТИВНОЕ", "ACTIVE", "AKTIF");
    public static string Name(AbilityKind kind) => kind switch
    {
        AbilityKind.Bomb => T("Бомба", "Bomb", "Bomba"),
        AbilityKind.Shield => T("Щит", "Shield", "Kalkan"),
        AbilityKind.CrescentSlash => T("Лунный разрез", "Crescent Slash", "Hilal Kesigi"),
        AbilityKind.ThunderSlam => T("Удар грома", "Thunder Slam", "Gok Gurultusu"),
        AbilityKind.FrostNova => T("Ледяная вспышка", "Frost Nova", "Buz Patlamasi"),
        AbilityKind.GravityWell => T("Гравитационный колодец", "Gravity Well", "Kutle Cekimi"),
        AbilityKind.OrbitBlades => T("Орбитальные клинки", "Orbit Blades", "Yorunge Bicaklari"),
        AbilityKind.ShadowFamiliar => T("Призрачный спутник", "Shadow Familiar", "Hayalet Dost"),
        AbilityKind.LightningStorm => T("Грозовой разряд", "Lightning Storm", "Yildirim Firtinasi"),
        AbilityKind.FlameRing => T("Огненное кольцо", "Flame Ring", "Ates Halkasi"),
        AbilityKind.IceLance => T("Ледяное копьё", "Ice Lance", "Buz Mizragi"),
        AbilityKind.FallingStar => T("Падающая звезда", "Falling Star", "Kayan Yildiz"),
        AbilityKind.VampirePulse => T("Кровавый импульс","Vampire Pulse","Kan Darbesi"),
        AbilityKind.BeeSwarm => T("Пчелиный рой","Bee Swarm","Ari Surusu"),
        AbilityKind.RicochetVolley => T("Рикошетный залп","Ricochet Volley","Sekme Atisi"),
        AbilityKind.GuardianAura => T("Аура восстановления","Guardian Aura","Koruyucu Aura"),
        _ => kind.ToString()
    };
    public static string Description(AbilityKind kind) => kind switch
    {
        AbilityKind.Bomb => T("Взрыв вокруг героя.","Explosion around the hero.","Dairesel patlama."),
        AbilityKind.Shield => T("Краткая неуязвимость.","Brief invulnerability.","Kisa sureli koruma."),
        AbilityKind.CrescentSlash => T("Широкий рассекающий взмах вперёд.", "A broad slash in the aimed direction.", "Nisan yonunde genis bir kesik."),
        AbilityKind.ThunderSlam => T("Круговой удар и отталкивание.", "Radial strike that pushes enemies away.", "Dusmanlari iten dairesel darbe."),
        AbilityKind.FrostNova => T("Холодная волна замедляет врагов.", "A freezing wave slows nearby enemies.", "Buz dalgasi dusmanlari yavaslatir."),
        AbilityKind.GravityWell => T("Стягивает врагов к точке впереди.", "Draws nearby enemies toward an aimed point.", "Dusmanlari nisan noktasina ceker."),
        AbilityKind.OrbitBlades => T("Два клинка атакуют вокруг игрока.", "Two orbiting blades attack nearby enemies.", "Iki yorunge bicagi yakin dusmanlara vurur."),
        AbilityKind.ShadowFamiliar => T("Спутник атакует ближайшую цель.", "A familiar strikes the nearest target.", "Dost en yakin hedefe saldirir."),
        AbilityKind.LightningStorm => T("Разряд поражает несколько целей.", "A discharge chains between enemies.", "Yildirim dusmanlar arasinda zincirlenir."),
        AbilityKind.FlameRing => T("Кольцо периодически поджигает врагов.", "A periodic ring burns nearby enemies.", "Ates halkasi yakin dusmanlari yakar."),
        AbilityKind.IceLance => T("Ледяное копьё пробивает линию врагов.", "An ice lance pierces a line of enemies.", "Buz mizragi dusman sirasini deler."),
        AbilityKind.FallingStar => T("Звезда падает на ближайшую группу.", "A falling star strikes a nearby group.", "Yildiz yakin bir gruba duser."),
        AbilityKind.VampirePulse => T("Урон вокруг и лечение за поражённых врагов.","Radial damage and healing per enemy hit.","Dairesel hasar ve iyilesme."),
        AbilityKind.BeeSwarm => T("Рой жалит до трёх целей, вызывая кровотечение.","A swarm stings up to three targets, causing bleed.","Uc hedefe kanama uygular."),
        AbilityKind.RicochetVolley => T("Залп прыгает между ближайшими врагами.","A volley bounces between nearby targets.","Atis yakin hedefler arasinda seker."),
        AbilityKind.GuardianAura => T("Периодически восстанавливает здоровье.","Periodically restores health.","Duzeli can yeniler."),
        _ => Name(kind)
    };
    public static string Stats(AbilityData data) => Description(data.Kind) + "\n" +
        T("Урон ", "Damage ", "Hasar ") + data.Damage.ToString("0") + " · " + T("Радиус ", "Range ", "Menzil ") + data.Radius.ToString("0.#") + " · " +
        data.Cooldown.ToString("0.#") + T(" с", " s", " sn");

    public static UpgradeType IconType(AbilityKind kind) => kind switch
    {
        AbilityKind.Shield => UpgradeType.Shield,
        AbilityKind.CrescentSlash => UpgradeType.Pierce,
        AbilityKind.ThunderSlam => UpgradeType.ExplosiveRounds,
        AbilityKind.FrostNova => UpgradeType.ProjectileSpeed,
        AbilityKind.GravityWell => UpgradeType.Special,
        AbilityKind.OrbitBlades => UpgradeType.Ricochet,
        AbilityKind.ShadowFamiliar => UpgradeType.Weapon,
        AbilityKind.LightningStorm => UpgradeType.ChainLightning,
        AbilityKind.FlameRing => UpgradeType.BurningRounds,
        AbilityKind.IceLance => UpgradeType.CriticalDamage,
        AbilityKind.FallingStar => UpgradeType.Damage,
        AbilityKind.VampirePulse => UpgradeType.Lifesteal,
        AbilityKind.BeeSwarm => UpgradeType.Bleeding,
        AbilityKind.RicochetVolley => UpgradeType.Ricochet,
        AbilityKind.GuardianAura => UpgradeType.HealthRegen,
        _ => UpgradeType.Bomb
    };
    public static Sprite Icon(AbilityKind kind) => MenuSheetArt.Upgrade(IconType(kind));
}
