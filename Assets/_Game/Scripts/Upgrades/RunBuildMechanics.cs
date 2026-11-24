using System.Collections.Generic;
using UnityEngine;

public enum RunPerk
{
    None, QuickHands, DeepMagazine, OpeningShot, LastRound, Scavenger,
    FireCatalyst, Wildfire, Conductor, ThunderCrit, DeepCuts, BloodHarvest,
    BlastPressure, Shockwave, RicochetFocus, RicochetPower, Guardian, Sprinter,
    Drone, DroneOverclock, ThermalReaction, PlasmaStorm, BloodBattery, ElectricRicochet,
    CryoRounds, CryoShatter, ColdCircuit,
    RifleMarksman, RiflePiercer, RifleRapid,
    ShotgunSlug, ShotgunWide, ShotgunShock,
    SmgOverdrive, SmgRecycler, SmgArc,
    BasicRifleEdge, BurstFinale, GoldenFortune, HunterChain, PlasmaFurnace,
    BasicShotgunClose, DoubleShotgunEncore, HeavyRam, InfernoCinders, VoidGravity,
    AssassinExecute, BasicSmgDrum, LightningSurge, MinigunHeat, TwinCrossfire,
    RelicHeavy, RelicOverclock, RelicGlass, RelicFortress,
    RelicRunner, RelicPrecision, RelicVampire, RelicReactor
}

/// <summary>Disposable, PvE-only build state. Cards are owned by UpgradeManager.</summary>
public static class RunBuildMechanics
{
    private sealed class Node
    {
        public readonly RunPerk Id, Previous;
        public readonly UpgradeFamily Family, Other;
        public readonly int Rank, OtherRank;
        public readonly UpgradeType Icon;
        public readonly string NameRu, NameEn, DescRu, DescEn;
        public Node(RunPerk id, UpgradeFamily family, UpgradeType icon, string ru, string en, string dr, string de,
            int rank = 0, RunPerk previous = RunPerk.None, UpgradeFamily other = UpgradeFamily.None, int otherRank = 0)
        {
            Id = id; Family = family; Icon = icon; NameRu = ru; NameEn = en; DescRu = dr; DescEn = de;
            Rank = rank; Previous = previous; Other = other; OtherRank = otherRank;
        }
    }

    private static readonly Node[] Nodes =
    {
        new(RunPerk.QuickHands, UpgradeFamily.Utility, UpgradeType.FireRate, "Быстрые руки", "Quick Hands", "Перезарядка на 20% быстрее.", "Reload duration -20%."),
        new(RunPerk.DeepMagazine, UpgradeFamily.Utility, UpgradeType.ProjectileCount, "Увеличенный магазин", "Deep Magazine", "Ёмкость магазина +30%.", "Magazine capacity +30%.", previous: RunPerk.QuickHands),
        new(RunPerk.OpeningShot, UpgradeFamily.Core, UpgradeType.Damage, "Первый выстрел", "Opening Shot", "Первый залп каждого магазина: +50% урона.", "First volley of each magazine: +50% damage.", previous: RunPerk.QuickHands),
        new(RunPerk.LastRound, UpgradeFamily.Explosion, UpgradeType.ExplosiveRounds, "Последний патрон", "Last Round", "Последний залп: +70% урона.", "Last volley of each magazine: +70% damage.", previous: RunPerk.OpeningShot),
        new(RunPerk.Scavenger, UpgradeFamily.Utility, UpgradeType.Weapon, "Сборщик", "Scavenger", "Прямое убийство возвращает 1 патрон (раз в 0,15 с).", "Direct kills return 1 round (0.15s cooldown).", previous: RunPerk.DeepMagazine),
        new(RunPerk.FireCatalyst, UpgradeFamily.Fire, UpgradeType.BurningRounds, "Катализатор", "Catalyst", "Урон горения +35%, длительность +25%.", "Burn damage +35%, duration +25%.", 1),
        new(RunPerk.Wildfire, UpgradeFamily.Fire, UpgradeType.BurningRounds, "Дикий огонь", "Wildfire", "Прямое убийство горящей цели создаёт лужу огня.", "Directly killing a burning enemy leaves a fire pool.", previous: RunPerk.FireCatalyst),
        new(RunPerk.Conductor, UpgradeFamily.Lightning, UpgradeType.ChainLightning, "Проводник", "Conductor", "Урон цепной молнии +30%, шанс +20% относительно базового.", "Chain damage +30%, proc rate +20%.", 1),
        new(RunPerk.ThunderCrit, UpgradeFamily.Lightning, UpgradeType.CriticalChance, "Громовой крит", "Thunder Critical", "Криты дают дополнительную дугу (0,45 с).", "Critical hits create an extra arc (0.45s cooldown).", previous: RunPerk.Conductor),
        new(RunPerk.DeepCuts, UpgradeFamily.Bleed, UpgradeType.Bleeding, "Глубокие раны", "Deep Cuts", "Урон кровотечения +40%, длительность +20%.", "Bleed damage +40%, duration +20%.", 1),
        new(RunPerk.BloodHarvest, UpgradeFamily.Bleed | UpgradeFamily.Lifesteal, UpgradeType.Lifesteal, "Жатва крови", "Blood Harvest", "Прямое убийство кровоточащей цели лечит 2% HP (0,8 с).", "Directly killing a bleeding target heals 2% max HP (0.8s cooldown).", previous: RunPerk.DeepCuts),
        new(RunPerk.BlastPressure, UpgradeFamily.Explosion, UpgradeType.ExplosiveRounds, "Давление", "Blast Pressure", "Урон взрывов +30%.", "Explosion damage +30%.", 1),
        new(RunPerk.Shockwave, UpgradeFamily.Explosion, UpgradeType.ExplosiveRounds, "Ударная волна", "Shockwave", "Радиус взрыва +20%.", "Explosion radius +20%.", previous: RunPerk.BlastPressure),
        new(RunPerk.RicochetFocus, UpgradeFamily.Ricochet, UpgradeType.Ricochet, "Отражатель", "Reflector", "Шанс рикошета +15 п.п.", "Ricochet chance +15 percentage points.", 1),
        new(RunPerk.RicochetPower, UpgradeFamily.Ricochet, UpgradeType.Ricochet, "Импульс", "Momentum", "Урон рикошетов +35%.", "Ricochet damage +35%.", previous: RunPerk.RicochetFocus),
        new(RunPerk.Guardian, UpgradeFamily.Defensive, UpgradeType.MaxHealth, "Страж", "Guardian", "Максимальное HP +20%, регенерация +0,5 HP/с.", "Max HP +20%, regeneration +0.5 HP/s.", 2),
        new(RunPerk.Sprinter, UpgradeFamily.Utility, UpgradeType.MoveSpeed, "Мобильность", "Mobility", "Скорость движения +15%, перезарядка на 10% быстрее.", "Move speed +15%, reload duration -10%.", 2),
        new(RunPerk.Drone, UpgradeFamily.Ability, UpgradeType.Bomb, "Боевой дрон", "Combat Drone", "Дрон автоматически атакует ближайшего врага.", "A drone automatically attacks nearby enemies.", 1),
        new(RunPerk.DroneOverclock, UpgradeFamily.Ability, UpgradeType.FireRate, "Турбодрон", "Drone Overclock", "Дрон атакует на 40% чаще и на 20% сильнее.", "Drone attacks 40% faster and deals 20% more damage.", previous: RunPerk.Drone),
        new(RunPerk.ThermalReaction, UpgradeFamily.Fire | UpgradeFamily.Explosion, UpgradeType.ExplosiveRounds, "Термореакция", "Thermal Reaction", "Прямое убийство горящей цели вызывает взрыв (0,6 с).", "Directly killing a burning enemy triggers a blast (0.6s cooldown).", previous: RunPerk.FireCatalyst, other: UpgradeFamily.Explosion, otherRank: 2),
        new(RunPerk.PlasmaStorm, UpgradeFamily.Fire | UpgradeFamily.Lightning, UpgradeType.ChainLightning, "Плазменный шторм", "Plasma Storm", "Попадание по горящей цели создаёт дугу (0,45 с).", "Hitting burning enemies creates a lightning arc (0.45s cooldown).", previous: RunPerk.Conductor, other: UpgradeFamily.Fire, otherRank: 2),
        new(RunPerk.BloodBattery, UpgradeFamily.Bleed | UpgradeFamily.Lifesteal, UpgradeType.Lifesteal, "Кровавая батарея", "Blood Battery", "Прямое убийство кровоточащего врага возвращает 3 патрона (0,3 с).", "Direct bleed-target kills return 3 rounds (0.3s cooldown).", previous: RunPerk.DeepCuts, other: UpgradeFamily.Lifesteal, otherRank: 2),
        new(RunPerk.ElectricRicochet, UpgradeFamily.Ricochet | UpgradeFamily.Lightning, UpgradeType.ChainLightning, "Электроотскок", "Electric Ricochet", "Попадание рикошетом создаёт дугу (0,45 с).", "Ricochet hits generate a lightning arc (0.45s cooldown).", previous: RunPerk.RicochetFocus, other: UpgradeFamily.Lightning, otherRank: 2),
        new(RunPerk.CryoRounds, UpgradeFamily.Cold, UpgradeType.ProjectileSpeed, "Охлаждающие патроны", "Cryo Rounds", "12% шанс замедлить и намочить врага на короткое время.", "12% chance to briefly slow and wet an enemy."),
        new(RunPerk.CryoShatter, UpgradeFamily.Cold, UpgradeType.CriticalDamage, "Раскол", "Shatter", "Крит по мокрой или покрытой льдом цели наносит ещё 25% урона.", "Crits on wet or ice-coated targets deal 25% extra damage.", previous: RunPerk.CryoRounds),
        new(RunPerk.ColdCircuit, UpgradeFamily.Cold | UpgradeFamily.Lightning, UpgradeType.ChainLightning, "Холодный контур", "Cold Circuit", "Попадания по мокрым или ледяным целям создают дугу (0,45 с).", "Hits on wet or ice-coated targets produce an arc (0.45s cooldown).", previous: RunPerk.CryoRounds, other: UpgradeFamily.Lightning, otherRank: 2),
        new(RunPerk.RifleMarksman, UpgradeFamily.Weapon, UpgradeType.CriticalChance, "Снайпер", "Marksman", "Шанс крита +20 п.п., критический урон +0,5.", "Critical chance +20 points, critical multiplier +0.5."),
        new(RunPerk.RiflePiercer, UpgradeFamily.Weapon, UpgradeType.Pierce, "Пробойник", "Piercer", "Пробитие +2, урон −15%.", "Pierce +2, damage -15%."),
        new(RunPerk.RifleRapid, UpgradeFamily.Weapon, UpgradeType.FireRate, "Штурмовик", "Assault", "Скорострельность +35%, магазин +20%, урон −10%.", "Fire rate +35%, magazine +20%, damage -10%."),
        new(RunPerk.ShotgunSlug, UpgradeFamily.Weapon, UpgradeType.Damage, "Монолит", "Monolith", "Один точный мощный снаряд вместо дроби. Пробитие +1.", "One powerful, accurate slug instead of pellets. Pierce +1."),
        new(RunPerk.ShotgunWide, UpgradeFamily.Weapon, UpgradeType.ProjectileCount, "Картечь", "Wide Buckshot", "Два дополнительных снаряда, шире разброс.", "Two extra pellets, wider spread."),
        new(RunPerk.ShotgunShock, UpgradeFamily.Weapon, UpgradeType.ExplosiveRounds, "Дробовой заряд", "Shock Shell", "Попадание создаёт взрыв (0,6 с).", "Hits cause a blast (0.6s cooldown)."),
        new(RunPerk.SmgOverdrive, UpgradeFamily.Weapon, UpgradeType.FireRate, "Раскрутка", "Overdrive", "Чем дольше стрельба, тем выше темп: до +55%.", "Sustained fire ramps up fire rate by up to 55%."),
        new(RunPerk.SmgRecycler, UpgradeFamily.Weapon, UpgradeType.Weapon, "Рециркулятор", "Recycler", "Магазин +25%; прямые убийства возвращают 2 патрона (0,15 с).", "Magazine +25%; direct kills return 2 rounds (0.15s cooldown)."),
        new(RunPerk.SmgArc, UpgradeFamily.Weapon, UpgradeType.ChainLightning, "Дуговой мотор", "Arc Motor", "Попадание периодически создаёт цепной разряд.", "Hits periodically generate a chain arc."),
        new(RunPerk.BasicRifleEdge, UpgradeFamily.Weapon, UpgradeType.Damage, "Ветеран", "Veteran", "Первые и последние 13% патронов магазина наносят +90% урона.", "The first and final 13% of rounds in the magazine deal +90% damage."),
        new(RunPerk.BurstFinale, UpgradeFamily.Weapon, UpgradeType.FireRate, "Финальный акцент", "Finale", "Последний выстрел каждой очереди наносит двойной урон.", "The final shot of every burst deals double damage."),
        new(RunPerk.GoldenFortune, UpgradeFamily.Weapon, UpgradeType.CriticalDamage, "Золотой шанс", "Golden Fortune", "+12 п.п. шанса крита и +1 к его множителю.", "+12 critical chance points and +1 crit multiplier."),
        new(RunPerk.HunterChain, UpgradeFamily.Weapon, UpgradeType.Pierce, "Сквозной охотник", "Hunter Chain", "+1 пробитие. Каждый следующий пробитый враг получает на 25% больше урона (до +75%).", "+1 pierce. Each successive pierced enemy takes 25% more damage (up to +75%)."),
        new(RunPerk.PlasmaFurnace, UpgradeFamily.Weapon, UpgradeType.BurningRounds, "Плазменная печь", "Plasma Furnace", "Горение оружия +65% урона и +30% длительности; прямой урон −10%.", "Weapon burn damage +65%, duration +30%; direct damage -10%."),
        new(RunPerk.BasicShotgunClose, UpgradeFamily.Weapon, UpgradeType.Damage, "В упор", "Point Blank", "Цели ближе 3,5 м получают +45% урона.", "Targets within 3.5 units take +45% damage."),
        new(RunPerk.DoubleShotgunEncore, UpgradeFamily.Weapon, UpgradeType.Damage, "Бис", "Encore", "Второй залп дуплета наносит на 70% больше урона.", "The second volley of each double shot deals 70% more damage."),
        new(RunPerk.HeavyRam, UpgradeFamily.Weapon, UpgradeType.Pierce, "Таран", "Battering Ram", "+1 пробитие и двойное отталкивание.", "+1 pierce and double knockback."),
        new(RunPerk.InfernoCinders, UpgradeFamily.Weapon, UpgradeType.BurningRounds, "Угли", "Cinders", "Горение оружия +35%; прямое убийство горящего врага оставляет огонь.", "Weapon burn +35%; direct kills of burning enemies leave fire pools."),
        new(RunPerk.VoidGravity, UpgradeFamily.Weapon, UpgradeType.ExplosiveRounds, "Сингулярность", "Singularity", "+1 пробитие; попадание периодически стягивает ближайших врагов.", "+1 pierce; hits periodically pull nearby enemies together."),
        new(RunPerk.AssassinExecute, UpgradeFamily.Weapon, UpgradeType.CriticalDamage, "Ликвидатор", "Executioner", "Цели с менее 35% HP получают +60% урона.", "Targets below 35% HP take +60% damage."),
        new(RunPerk.BasicSmgDrum, UpgradeFamily.Weapon, UpgradeType.ProjectileCount, "Барабан", "Drum Magazine", "Магазин +60%, скорострельность +15%, время перезарядки −10%.", "Magazine +60%, fire rate +15%, reload time -10%."),
        new(RunPerk.LightningSurge, UpgradeFamily.Weapon, UpgradeType.ChainLightning, "Сверхпроводник", "Superconductor", "Молния оружия: +18 п.п. шанса, +1 цель, +20% урона.", "Weapon lightning: +18 chance points, +1 target, +20% damage."),
        new(RunPerk.MinigunHeat, UpgradeFamily.Weapon, UpgradeType.FireRate, "Перегрев", "Heat Drive", "Непрерывная стрельба постепенно повышает урон до +60%.", "Sustained fire ramps up damage by up to 60%."),
        new(RunPerk.TwinCrossfire, UpgradeFamily.Weapon, UpgradeType.Pierce, "Перекрёстный огонь", "Crossfire", "Второй залп: +1 пробитие, +50% урона.", "Second volley: +1 pierce and +50% damage."),
        new(RunPerk.RelicHeavy, UpgradeFamily.None, UpgradeType.Damage, "Тяжёлое ядро", "Heavy Core", "+50% урона, −22% скорострельности.", "+50% damage, -22% fire rate."),
        new(RunPerk.RelicOverclock, UpgradeFamily.None, UpgradeType.FireRate, "Разгон", "Overclock", "+45% скорострельности, −20% ёмкости магазина.", "+45% fire rate, -20% magazine capacity."),
        new(RunPerk.RelicGlass, UpgradeFamily.None, UpgradeType.Damage, "Хрупкий реактор", "Glass Reactor", "+65% урона, −30% максимального HP.", "+65% damage, -30% max HP."),
        new(RunPerk.RelicFortress, UpgradeFamily.None, UpgradeType.Shield, "Крепость", "Fortress", "+40% максимального HP, −15% скорости.", "+40% max HP, -15% move speed."),
        new(RunPerk.RelicRunner, UpgradeFamily.None, UpgradeType.MoveSpeed, "Гипердвигатель", "Hyperdrive", "+25% скорости, −35% времени перезарядки, −12% урона.", "+25% move speed, -35% reload time, -12% damage."),
        new(RunPerk.RelicPrecision, UpgradeFamily.None, UpgradeType.CriticalChance, "Прецизионный модуль", "Precision Module", "+25 п.п. крита, +0,4 к криту, −15% магазин.", "+25 critical chance points, +0.4 crit multiplier, -15% magazine."),
        new(RunPerk.RelicVampire, UpgradeFamily.None, UpgradeType.Lifesteal, "Вампирическое ядро", "Vampiric Core", "Прямой урон восстанавливает 1% нанесённого урона. −20% max HP.", "Direct damage heals 1% of damage dealt. -20% max HP."),
        new(RunPerk.RelicReactor, UpgradeFamily.None, UpgradeType.ChainLightning, "Реактор стихий", "Element Reactor", "+45% урона эффектов, −15% прямого урона.", "+45% effect damage, -15% direct weapon damage.")
    };

    private static readonly HashSet<RunPerk> taken = new();
    private static readonly Dictionary<WeaponData, RunPerk> evolutions = new();
    private static float nextAmmo, nextBloodAmmo, nextHeal, nextArc, nextBlast, nextFirePool, nextDrone, nextGravity;
    private static GameObject drone;
    private static Node Find(RunPerk id) => Nodes[(int)id - 1];
    public static bool Has(RunPerk id) => !GameModeManager.IsPvp && taken.Contains(id);
    private static bool Between(RunPerk id, RunPerk min, RunPerk max) => (int)id >= (int)min && (int)id <= (int)max;
    public static bool IsSignature(RunPerk id) => Between(id, RunPerk.BasicRifleEdge, RunPerk.TwinCrossfire);
    public static bool IsEvolution(RunPerk id) => Between(id, RunPerk.RifleMarksman, RunPerk.TwinCrossfire);
    public static bool IsRelic(RunPerk id) => Between(id, RunPerk.RelicHeavy, RunPerk.RelicReactor);
    public static bool IsTree(RunPerk id) => id != RunPerk.None && !IsEvolution(id) && !IsRelic(id);
    public static string Name(RunPerk id) => GameplayJourney.T(Find(id).NameRu, Find(id).NameEn, Find(id).NameEn);
    public static string Description(RunPerk id) => GameplayJourney.T(Find(id).DescRu, Find(id).DescEn, Find(id).DescEn);
    public static UpgradeType Icon(RunPerk id) => Find(id).Icon;
    public static string BranchHint(RunPerk id)
    {
        if (!IsTree(id)) return null;
        Node node = Find(id);
        if (node.OtherRank > 0) return GameplayJourney.T("ГИБРИДНАЯ СИНЕРГИЯ", "HYBRID SYNERGY", "HYBRID SYNERGY");
        if (node.Previous != RunPerk.None) return GameplayJourney.T("ВЕТКА: ", "BRANCH: ", "BRANCH: ") + Name(node.Previous);
        return GameplayJourney.T("НАЧАЛО ВЕТКИ", "BRANCH START", "BRANCH START");
    }
    public static string ChoiceTitle(bool boss, bool evolution) => boss
        ? GameplayJourney.T("АРТЕФАКТ БОССА", "BOSS ARTIFACT", "BOSS ARTIFACT")
        : evolution ? GameplayJourney.T("ЭВОЛЮЦИЯ ОРУЖИЯ", "WEAPON EVOLUTION", "WEAPON EVOLUTION") : Lang.Get("choice.title");

    public static List<UpgradeData> CreateCards()
    {
        var cards = new List<UpgradeData>(Nodes.Length);
        foreach (Node node in Nodes)
        {
            var card = ScriptableObject.CreateInstance<UpgradeData>();
            card.InitializeRunPerk(node.Id, node.Family, IsRelic(node.Id) ? Rarity.Legendary : IsEvolution(node.Id) ? Rarity.Epic : Rarity.Rare);
            cards.Add(card);
        }
        return cards;
    }

    public static void Reset()
    {
        taken.Clear(); evolutions.Clear(); nextAmmo = nextBloodAmmo = nextHeal = nextArc = nextBlast = nextFirePool = nextDrone = nextGravity = 0f;
        if (drone != null) Object.Destroy(drone);
        drone = null;
    }

    public static RunPerk Evolution(WeaponData weapon) => weapon != null && !GameModeManager.IsPvp && evolutions.TryGetValue(weapon, out var p) ? p : RunPerk.None;

    public static bool CanUseTree(RunPerk id, RunBuildState build)
    {
        if (!IsTree(id) || Has(id) || build == null) return false;
        Node n = Find(id);
        return (n.Previous == RunPerk.None || Has(n.Previous)) &&
            (n.Rank == 0 || build.GetFamilyStacks(n.Family) >= n.Rank) &&
            (n.OtherRank == 0 || build.GetFamilyStacks(n.Other) >= n.OtherRank);
    }

    public static bool CanEvolve(RunPerk id, WeaponData data)
    {
        if (data == null || !IsEvolution(id) || Evolution(data) != RunPerk.None) return false;
        if (IsSignature(id)) return SignatureWeapon(id) == data.WeaponName;
        return data.WeaponType == WeaponType.Rifle ? Between(id, RunPerk.RifleMarksman, RunPerk.RifleRapid) :
            data.WeaponType == WeaponType.Shotgun ? Between(id, RunPerk.ShotgunSlug, RunPerk.ShotgunShock) :
            Between(id, RunPerk.SmgOverdrive, RunPerk.SmgArc);
    }

    private static string SignatureWeapon(RunPerk id) => id switch
    {
        RunPerk.BasicRifleEdge => "Basic Rifle", RunPerk.BurstFinale => "Burst Rifle", RunPerk.GoldenFortune => "Golden Rifle",
        RunPerk.HunterChain => "Hunter Rifle", RunPerk.PlasmaFurnace => "Plasma Rifle", RunPerk.BasicShotgunClose => "Basic Shotgun",
        RunPerk.DoubleShotgunEncore => "Double Shotgun", RunPerk.HeavyRam => "Heavy Shotgun", RunPerk.InfernoCinders => "Inferno Shotgun",
        RunPerk.VoidGravity => "Void Shotgun", RunPerk.AssassinExecute => "Assassin SMG", RunPerk.BasicSmgDrum => "Basic SMG",
        RunPerk.LightningSurge => "Lightning SMG", RunPerk.MinigunHeat => "Minigun", RunPerk.TwinCrossfire => "Twin SMG", _ => null
    };

    public static void Apply(RunPerk id, Weapon weapon, PlayerStats stats, PlayerHealth health)
    {
        if (id == RunPerk.None || GameModeManager.IsPvp) return;
        if (IsEvolution(id))
        {
            if (weapon != null && CanEvolve(id, weapon.Data)) { evolutions[weapon.Data] = id; weapon.RefreshMagazine(); }
            return;
        }
        if (!taken.Add(id)) return;
        switch (id)
        {
            case RunPerk.Guardian: health?.AddMaxHealthPercent(.20f); health?.AddHealthRegen(.5f); break;
            case RunPerk.Sprinter: stats?.AddMoveSpeedPercent(.15f); break;
            case RunPerk.RelicHeavy: stats?.AddDamagePercent(.50f); stats?.AddFireRatePercent(-.22f); break;
            case RunPerk.RelicOverclock: stats?.AddFireRatePercent(.45f); break;
            case RunPerk.RelicGlass: stats?.AddDamagePercent(.65f); health?.ScaleMaxHealth(.70f); break;
            case RunPerk.RelicFortress: health?.AddMaxHealthPercent(.40f); stats?.AddMoveSpeedPercent(-.15f); break;
            case RunPerk.RelicRunner: stats?.AddMoveSpeedPercent(.25f); stats?.AddDamagePercent(-.12f); break;
            case RunPerk.RelicPrecision: stats?.AddCriticalChance(.25f); stats?.AddCriticalDamageMultiplier(.4f); break;
            case RunPerk.RelicVampire: health?.ScaleMaxHealth(.80f); break;
            case RunPerk.RelicReactor: stats?.AddDamagePercent(-.15f); break;
        }
        weapon?.RefreshMagazine();
    }

    public static float MagazineMultiplier(WeaponData data) => (Has(RunPerk.DeepMagazine) ? 1.30f : 1f) *
        (Has(RunPerk.RelicOverclock) ? .80f : 1f) * (Has(RunPerk.RelicPrecision) ? .85f : 1f) *
        (Evolution(data) == RunPerk.RifleRapid ? 1.20f : Evolution(data) == RunPerk.SmgRecycler ? 1.25f : Evolution(data) == RunPerk.BasicSmgDrum ? 1.6f : 1f);
    public static float ReloadMultiplier(WeaponData data) => (Has(RunPerk.QuickHands) ? .80f : 1f) * (Has(RunPerk.Sprinter) ? .90f : 1f) * (Has(RunPerk.RelicRunner) ? .65f : 1f) * (Evolution(data) == RunPerk.BasicSmgDrum ? .9f : 1f);
    public static float VolleyDamage(WeaponData data, bool first, bool last, int volley, float heat, int ammoBefore, int capacity)
    {
        RunPerk evo = Evolution(data);
        float factor = evo == RunPerk.RiflePiercer ? .85f : evo == RunPerk.RifleRapid ? .90f : 1f;
        if (evo == RunPerk.ShotgunSlug) factor *= Mathf.Max(1f, data.ProjectileCount * .72f);
        int edge = Mathf.Max(1, Mathf.CeilToInt(capacity * .13f));
        if (evo == RunPerk.BasicRifleEdge && (ammoBefore > capacity - edge || ammoBefore <= edge)) factor *= 1.90f;
        if (evo == RunPerk.BurstFinale && volley == data.ShotsPerBurst) factor *= 2f;
        if (evo == RunPerk.PlasmaFurnace) factor *= .90f;
        if (evo == RunPerk.DoubleShotgunEncore && volley == 2) factor *= 1.7f;
        if (evo == RunPerk.TwinCrossfire && volley == 2) factor *= 1.5f;
        if (evo == RunPerk.MinigunHeat) factor *= 1f + .6f * heat;
        return factor * (first && Has(RunPerk.OpeningShot) ? 1.50f : 1f) * (last && Has(RunPerk.LastRound) ? 1.70f : 1f);
    }
    public static float HitMultiplier(RunPerk evo, Enemy enemy, Weapon weapon, int hits)
    {
        if (evo == RunPerk.HunterChain) return 1f + .25f * Mathf.Clamp(hits - 1, 0, 3);
        if (evo == RunPerk.AssassinExecute && enemy.CurrentHealth < enemy.MaxHealth * .35f) return 1.60f;
        if (evo == RunPerk.BasicShotgunClose && weapon != null && (weapon.transform.position - enemy.transform.position).sqrMagnitude <= 3.5f * 3.5f) return 1.45f;
        return 1f;
    }
    public static float EffectFactor => Has(RunPerk.RelicReactor) ? 1.45f / .85f : 1f;
    public static float BurnFactor => (Has(RunPerk.FireCatalyst) ? 1.35f : 1f) * EffectFactor;
    public static float BleedFactor => (Has(RunPerk.DeepCuts) ? 1.40f : 1f) * EffectFactor;
    public static float LightningFactor => (Has(RunPerk.Conductor) ? 1.30f : 1f) * EffectFactor;
    public static float ExplosionFactor => (Has(RunPerk.BlastPressure) ? 1.30f : 1f) * EffectFactor;

    public static void OnBulletHit(Enemy enemy, Weapon source, RunPerk evolution, bool ricochet, bool burning, bool bleeding, bool chilled,
        bool killed, bool critical, float damage, string weaponId, PlayerHealth health)
    {
        if (GameModeManager.IsPvp || enemy == null) return;
        float now = Time.time;
        if (!ricochet && !enemy.IsDead && Has(RunPerk.CryoRounds) && !enemy.IsWet && Random.value < .12f)
            enemy.ApplyArenaSlow(1.8f);
        if (!ricochet && !enemy.IsDead && chilled && critical && Has(RunPerk.CryoShatter))
            enemy.TakeDamage(damage * .25f, false, DamageSource.Weapon, 0, weaponId, true);
        killed |= enemy.IsDead;
        if (killed && !ricochet && source != null && source.Data != null && source.Data.WeaponName == weaponId)
        {
            int ammo = (Has(RunPerk.Scavenger) ? 1 : 0) + (evolution == RunPerk.SmgRecycler ? 2 : 0);
            if (ammo > 0 && now >= nextAmmo) { nextAmmo = now + .15f; source.RestoreAmmo(ammo); }
            if (bleeding && Has(RunPerk.BloodBattery) && now >= nextBloodAmmo) { nextBloodAmmo = now + .30f; source.RestoreAmmo(3); }
        }
        if (killed && !ricochet && bleeding && Has(RunPerk.BloodHarvest) && now >= nextHeal)
        { nextHeal = now + .8f; if (health != null) health.Heal(health.MaxHealth * .02f); }
        if (killed && !ricochet && burning && (Has(RunPerk.Wildfire) || evolution == RunPerk.InfernoCinders) && now >= nextFirePool)
        { nextFirePool = now + .45f; SynergyCombat.FirePool(enemy.transform.position, damage * .5f, weaponId); }
        if (!ricochet && evolution == RunPerk.VoidGravity && now >= nextGravity)
        {
            nextGravity = now + .85f;
            Vector3 center = enemy.transform.position;
            int pulled = 0;
            foreach (Enemy other in Enemy.SnapshotAlive())
            {
                if (pulled >= 6) break;
                if (other == null || other == enemy || other.IsDead) continue;
                Vector3 direction = center - other.transform.position; direction.y = 0f;
                if (direction.sqrMagnitude > 9f || direction.sqrMagnitude < .01f) continue;
                other.ApplyKnockback(direction.normalized, 2f); pulled++;
            }
            VfxFactory.TrySpawnExplosion(center, 2f, new Color(.55f,.35f,.85f));
        }
        if (now >= nextArc && ((critical && !ricochet && Has(RunPerk.ThunderCrit)) || (burning && !ricochet && Has(RunPerk.PlasmaStorm)) ||
            (ricochet && Has(RunPerk.ElectricRicochet)) || (!ricochet && chilled && Has(RunPerk.ColdCircuit)) || (!ricochet && evolution == RunPerk.SmgArc)))
        { nextArc = now + .45f; enemy.TriggerLightning(damage * .6f * LightningFactor, 3, 4f, weaponId); }
        if (!ricochet && now >= nextBlast && ((killed && burning && Has(RunPerk.ThermalReaction)) || evolution == RunPerk.ShotgunShock))
        {
            nextBlast = now + .6f;
            Vector3 pos = enemy.transform.position;
            VfxFactory.TrySpawnExplosion(pos, 2f, new Color(1f,.65f,.24f));
            int count = 0, eventId = DamageEventId.Next();
            foreach (Enemy other in Enemy.SnapshotAlive())
            {
                if (count >= 5) break;
                if (other == null || other == enemy || other.IsDead || (other.transform.position - pos).sqrMagnitude > 4f) continue;
                other.TakeDamage(damage * .65f * ExplosionFactor, false, DamageSource.Explosion, eventId, weaponId, true); count++;
            }
        }
    }

    public static void TickDrone(Weapon weapon, PlayerStats stats)
    {
        if (!Has(RunPerk.Drone) || weapon == null || weapon.Data == null) return;
        if (drone == null)
        {
            drone = GameObject.CreatePrimitive(PrimitiveType.Cube); drone.name = "Run Combat Drone";
            var collider = drone.GetComponent<Collider>(); if (collider != null) { collider.enabled = false; Object.Destroy(collider); }
            drone.transform.localScale = new Vector3(.40f,.22f,.40f);
        }
        Transform root = stats != null ? stats.transform : weapon.transform.root;
        float angle = Time.time * 2.5f;
        drone.transform.position = root.position + new Vector3(Mathf.Cos(angle)*1.3f, 1.4f, Mathf.Sin(angle)*1.3f);
        drone.transform.Rotate(0f, 90f * Time.deltaTime, 0f);
        if (Time.time < nextDrone) return;
        bool overclock = Has(RunPerk.DroneOverclock);
        nextDrone = Time.time + (overclock ? .65f : .91f);
        Enemy target = null; float nearest = 64f;
        foreach (Enemy e in Enemy.SnapshotAlive())
        {
            if (e == null || e.IsDead) continue;
            float sq = (e.transform.position - drone.transform.position).sqrMagnitude;
            if (sq < nearest) { nearest = sq; target = e; }
        }
        if (target == null) return;
        int volleys = weapon.Data.SpecialType == WeaponSpecialType.Burst ? weapon.Data.ShotsPerBurst :
            weapon.Data.SpecialType == WeaponSpecialType.DoubleShot ? 2 : 1;
        float dps = weapon.Data.Damage * weapon.Data.ProjectileCount * volleys / Mathf.Max(.10f, weapon.Data.GetFireCycleDuration(1f));
        VfxFactory.SpawnLightning(drone.transform.position, target.transform.position);
        target.TakeDamage(dps * .16f * (overclock ? 1.2f : 1f) * (stats != null ? stats.DamageMultiplier : 1f), false, DamageSource.Ability, 0, weapon.Data.WeaponName, true);
    }
}
