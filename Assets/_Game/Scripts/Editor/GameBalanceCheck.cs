#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class GameBalanceCheck
{
    [MenuItem("ArcadeSurvivor/Checks/Economy and balance")]
    public static void Run()
    {
        try
        {
            var weapons = Load<WeaponData>("Assets/Data/Weapons");
            var skins = Load<ClothingData>("Assets/Data/Clothing");
            var abilities = Load<AbilityData>("Assets/Data/Abilities");
            var maps = Load<GameMap>("Assets/Data/Maps");
            Require(weapons.Length == 15 && skins.Length == 19 &&
                    abilities.Length == 12 && maps.Length == 6, "Complete catalog");
            Require(weapons.All(x => x.UnlockLevel >= 1 && x.UnlockLevel <= 50 &&
                    (x.Price > 0 || x.UnlockLevel == 1)), "Weapons have valid prices/levels");
            Require(skins.Count(x => x.Price == 0) == 1, "Only the default outfit is free");
            Require(weapons.Count(x => x.Price == 0) == 3, "Three starter playstyles");
            Require(maps.Min(x => x.unlockLevel) == 1 && maps.Max(x => x.unlockLevel) == 32,
                "Map progression");

            int upgrades = PermanentUpgrades.GetTotalCost() * PermanentUpgrades.Stats.Count;
            int total = weapons.Sum(x => x.Price) + skins.Sum(x => x.Price) +
                        abilities.Sum(x => x.Price) + upgrades;
            Require(total == 31980 && upgrades == 13400, "Full catalog costs");
            Require(abilities.Count(a => a.IsAutomatic) == 6 && abilities.Count(a => !a.IsAutomatic) == 6, "Six active and six automatic abilities");
            Require(abilities.Select(a => a.Kind).Distinct().Count() == abilities.Length, "Unique ability kinds");
            Require(abilities.Select(a => a.AbilityName).Distinct().Count() == abilities.Length, "Unique saved ability IDs");
            Require(abilities.Where(a => !a.IsLegacy).All(a => a.Damage > 0 && a.Radius > 0 && a.Cooldown > 0 && a.MaxTargets <= 16), "Bounded ability settings");
            Require(GameBalance.LevelXp(1) == 0 && GameBalance.LevelXp(50) == 34692,
                "Level curve endpoints");
            for (int level = 2; level <= 50; level++)
                Require(GameBalance.LevelXp(level) > GameBalance.LevelXp(level - 1), "XP curve monotonic");
            for (int level = 1; level < PermanentUpgrades.MaxLevel; level++)
                Require(GameBalance.UpgradeCost(level) > GameBalance.UpgradeCost(level - 1), "Upgrade price curve");
            Require(GameBalance.UpgradeCost(10) == 0 && GameBalance.RunXp(0) == 0,
                "No XP for empty attempts, no cost beyond max level");
            Require(GameBalance.RunXp(10) - GameBalance.RunXp(9) ==
                    GameBalance.XpPerClearedWave + GameBalance.XpPerClearedBoss, "Completed boss reward");
            Require(GameBalance.WaveCoins(10, true) - GameBalance.WaveCoins(10, false) ==
                    GameBalance.PerfectCoinBonus, "Perfect bonus");
            Require(GameBalance.WaveCoins(10, false) > GameBalance.WaveCoins(9, false) &&
                    GameBalance.WaveCoins(51, false) == GameBalance.WaveCoins(101, false),
                "Boss bonus and bounded endless wave income");
            Require(WeaponData.ResolveProjectileCount(1, .45f) == 2 &&
                    WeaponData.ResolveProjectileCount(1, 1.2f) == 3 &&
                    WeaponData.ResolveProjectileCount(6, .45f) == 8 &&
                    WeaponData.ResolveProjectileCount(6, 5f) == 8,
                "Projectile upgrade works for all weapon families and is capped");
            Require(Mathf.Abs(WeaponData.GetVolleyDamageScale(1, 2) * 2 - 1.45f) < .001f,
                "Extra shot cannot double base damage");

            float baseline = Dps(weapons.Single(x => x.WeaponName == "Basic Rifle"));
            foreach (WeaponData weapon in weapons)
            {
                float dps = Dps(weapon);
                Require(dps >= baseline * .70f && dps <= baseline * 1.45f,
                    "Direct theoretical DPS outside expected range: " + weapon.WeaponName + " " + dps);
            }
            Debug.Log($"BALANCE_OK weapons={weapons.Sum(x => x.Price)}, skins={skins.Sum(x => x.Price)}, " +
                $"abilities={abilities.Sum(x => x.Price)}, upgrades={upgrades}, total={total}");

            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    private static float Dps(WeaponData weapon)
    {
        int volleys = weapon.SpecialType == WeaponSpecialType.Burst ? weapon.ShotsPerBurst :
            weapon.SpecialType == WeaponSpecialType.DoubleShot ? 2 : 1;
        float critScale = 1f + weapon.CriticalChanceBonus * (1f + weapon.CriticalDamageBonus);
        float cycle = weapon.GetFireCycleDuration(1);
        float firingTime = weapon.BaseMagazineSize * cycle / volleys;
        return weapon.Damage * weapon.ProjectileCount * volleys * critScale / cycle *
            firingTime / (firingTime + weapon.BaseReloadSeconds);
    }

    private static T[] Load<T>(string folder) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { folder })
            .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(asset => asset != null).ToArray();

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
#endif
