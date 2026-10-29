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
