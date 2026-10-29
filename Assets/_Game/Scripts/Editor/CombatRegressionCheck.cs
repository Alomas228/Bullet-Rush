#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CombatRegressionCheck
{
    [MenuItem("ArcadeSurvivor/Checks/Combat regression")]
    public static void Run()
    {
        try
        {
            CheckTiming();
            CheckBurn();
            CheckMapApproaches();
            CheckMapCombatRules();
            Debug.Log("COMBAT_REGRESSION_OK");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    private static void CheckTiming()
    {
        WeaponData burst = Load("Rifle/Weapon_BurstRifle");
        float cycle = burst.GetFireCycleDuration(1f);
        float lastShot = (burst.ShotsPerBurst - 1) * burst.GetBurstInterval(1f);
        Require(cycle > lastShot, "Burst needs a recovery window");
        Near(burst.GetFireCycleDuration(2f), cycle / 2f, "Burst cycle scales");
        Near(burst.GetBurstInterval(2f), burst.BurstInterval / 2f, "Burst spacing scales");

        foreach (string path in new[] { "SMG/Weapon_TwinSMG", "Shotgun/Weapon_DoubleShotgun" })
        {
            WeaponData pair = Load(path);
            foreach (float speed in new[] { 1f, 2f, 10f })
            {
                Require(pair.GetDoubleShotInterval(speed) <= pair.GetFireCycleDuration(speed),
                    "Pair must complete within its cycle: " + path);
                Near(pair.GetFireCycleDuration(speed), pair.GetFireCycleDuration(1f) / speed,
                    "Pair cycle scales: " + path);
            }
        }

        WeaponData rifle = Load("Rifle/Weapon_BasicRifle");
        Near(rifle.GetFireCycleDuration(2f), 1f / (rifle.FireRate * 2f), "Regular fire unchanged");
    }

    private static void CheckMapCombatRules()
    {
        Near(MapCombatRules.LightningRange(4f, true), 5.5f, "Wet origin expands lightning");
        Near(MapCombatRules.LightningRange(5f, true), 6f, "Wet lightning retains range cap");
        Near(MapCombatRules.LightningRange(0f, true), 0f, "Wet does not create missing lightning");
        float ice = 30f;
        Near(MapCombatRules.ResolveIceDamage(20f, false, ref ice), 20f, "Ordinary damage unchanged");
        Near(ice, 10f, "Ordinary damage removes ice");
        Near(MapCombatRules.ResolveIceDamage(10f, true, ref ice), 15f, "Burn bonus stops at ice boundary");
        Near(ice, 0f, "Coating breaks once");
        Near(MapCombatRules.ResolveIceDamage(10f, true, ref ice), 10f, "Flesh takes normal burn damage");
        Require(MapCombatRules.ShouldHaveIce(MapMechanic.Highland, false, 0.1f), "Ordinary foes eligible");
        Require(!MapCombatRules.ShouldHaveIce(MapMechanic.Highland, true, 0.1f), "Bosses excluded");
        Require(!MapCombatRules.ShouldHaveIce(MapMechanic.OpenField, false, 0.1f), "Other maps excluded");
    }

    private static void CheckMapApproaches()
    {
        foreach (MapMechanic mechanic in Enum.GetValues(typeof(MapMechanic)))
        {
            foreach (WaveApproach previous in Enum.GetValues(typeof(WaveApproach)))
            {
                foreach (float roll in new[] { 0f, 0.2f, 0.5f, 0.8f, 1f })
                {
                    WaveApproach pick = MapMechanicManager.ChooseApproach(mechanic, 1f, roll, previous, true);
                    Require(pick != previous, "Map must preserve approach variety: " + mechanic);
                    Require(MapMechanicManager.ChooseApproach(mechanic, 0f, roll, previous, false) ==
                        MapMechanicManager.ChooseApproach(MapMechanic.None, 1f, roll, previous, false),
                        "Zero map strength must use neutral weights");
                }
            }
        }
        Require(MapMechanicManager.ApproachWeights(MapMechanic.Urban).y >
            MapMechanicManager.ApproachWeights(MapMechanic.Urban).x, "City should prefer fronts");
    }

    private static void CheckBurn()
    {
        var host = new GameObject("Combat regression enemy");
        host.SetActive(false);
        int aliveCount = Enemy.AliveCount;
        try
        {
            Enemy enemy = host.AddComponent<Enemy>();
            enemy.ApplyBurn(70f, 3f, 0.5f, "Inferno");
            Set(enemy, "burnRemainingTime", 1f);
            Set(enemy, "burnTickTimer", 0.15f);
            enemy.ApplyBurn(40f, 20f, 0.25f, "Upgrade");
            Near(Get(enemy, "burnDamagePerTick"), 35f, "Weaker burn cannot lower damage");
            Near(Get(enemy, "burnRemainingTime"), 1f, "Weaker burn cannot extend stronger burn");
            Near(Get(enemy, "burnTickTimer"), 0.15f, "Proc cannot postpone next tick");

            enemy.ApplyBurn(70f, 0.5f, 0.5f, "Inferno");
            Near(Get(enemy, "burnRemainingTime"), 1f, "Equal burn cannot shorten duration");
            enemy.ApplyBurn(70f, 3f, 0.5f, "Inferno");
            Near(Get(enemy, "burnRemainingTime"), 3f, "Equal burn can refresh duration");

            enemy.ApplyBurn(90f, 2f, 0.5f, "Plasma");
            Near(Get(enemy, "burnDamagePerTick"), 45f, "Stronger burn replaces damage");
            Near(Get(enemy, "burnRemainingTime"), 2f, "Stronger burn uses its own duration");
            Near(Get(enemy, "burnTickTimer"), 0.15f, "Replacement preserves tick progress");

            Set(enemy, "burnActive", false);
            Set(enemy, "burnRemainingTime", 0f);
            enemy.ApplyBurn(40f, 2f, 0.5f, "Upgrade");
            Near(Get(enemy, "burnDamagePerTick"), 20f, "Weaker burn works after expiry");
            Near(Get(enemy, "burnTickTimer"), 0.5f, "New burn starts its tick timer");

            Set(enemy, "wetUntil", 1000f);
            Set(enemy, "iceHealth", 30f);
            Set(enemy, "initialIceHealth", 30f);
            typeof(Enemy).GetMethod("ResetMapStatuses", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(enemy, null);
            Near(Get(enemy, "wetUntil"), 0f, "Wet status resets on reuse");
            Near(enemy.IceHealth, 0f, "Ice resets on reuse");
            Near(enemy.IceFraction, 0f, "Ice visual progress resets on reuse");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            // An inactive test enemy never registers in Awake; its OnDestroy
            // still adjusts the shared counter. Keep an open scene unchanged.
            typeof(Enemy).GetProperty(nameof(Enemy.AliveCount)).SetValue(null, aliveCount);
        }
    }

    private static WeaponData Load(string path)
    {
        WeaponData data = AssetDatabase.LoadAssetAtPath<WeaponData>("Assets/Data/Weapons/" + path + ".asset");
        Require(data != null, "Missing weapon " + path);
        return data;
    }

    private static FieldInfo Field(string name) => typeof(Enemy).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new Exception("Missing Enemy field: " + name);
    private static float Get(Enemy enemy, string name) => (float)Field(name).GetValue(enemy);
    private static void Set(Enemy enemy, string name, object value) => Field(name).SetValue(enemy, value);
    private static void Near(float actual, float expected, string message) =>
        Require(Mathf.Abs(actual - expected) < 0.0001f, message + ": " + actual + " != " + expected);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
#endif
