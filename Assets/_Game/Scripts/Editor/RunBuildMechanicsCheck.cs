#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RunBuildMechanicsCheck
{
    [MenuItem("Tools/Bullet Rush/Validate Run Builds")]
    public static void Run()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run the check outside Play Mode.");
        RunBuildMechanics.Reset();
        var cards = RunBuildMechanics.CreateCards();
        try
        {
            Require(cards.Select(c => c.Perk).Distinct().Count() == cards.Count, "Duplicate run card IDs");
            Require(cards.Count == Enum.GetValues(typeof(RunPerk)).Length - 1, "Every perk must have exactly one card");
            Require(cards.Count(c => RunBuildMechanics.IsSignature(c.Perk)) == 15, "15 weapon-specific evolutions");
            Require(cards.Count(c => RunBuildMechanics.IsRelic(c.Perk)) == 8, "8 boss relics");
            foreach (var card in cards)
                Require(!string.IsNullOrWhiteSpace(card.LocalizedName) && !string.IsNullOrWhiteSpace(card.LocalizedDescription), "Missing card text: " + card.Perk);

            var weapons = AssetDatabase.FindAssets("t:WeaponData", new[] { "Assets/Data/Weapons" })
                .Select(g => AssetDatabase.LoadAssetAtPath<WeaponData>(AssetDatabase.GUIDToAssetPath(g))).Where(w => w != null).ToArray();
            Require(weapons.Length == 15, "Expected 15 armory weapons");
            foreach (var weapon in weapons)
            {
                var available = cards.Where(c => RunBuildMechanics.CanEvolve(c.Perk, weapon)).ToArray();
                Require(available.Length == 4 && available.Count(c => RunBuildMechanics.IsSignature(c.Perk)) == 1,
                    "Each weapon needs one signature and three shared candidates: " + weapon.WeaponName);
                Require(weapon.BaseMagazineSize > 0 && weapon.BaseReloadSeconds > 0, "Invalid ammo settings: " + weapon.WeaponName);
            }

            var empty = RunBuildState.Build(null, null);
            Require(RunBuildMechanics.CanUseTree(RunPerk.QuickHands, empty), "Reload branch has a starting node");
            Require(!RunBuildMechanics.CanUseTree(RunPerk.DeepMagazine, empty), "Advanced magazine requires Quick Hands");
            RunBuildMechanics.Apply(RunPerk.QuickHands, null, null, null);
            Require(!RunBuildMechanics.CanUseTree(RunPerk.QuickHands, empty), "Do not repeat one-time nodes");
            Require(RunBuildMechanics.CanUseTree(RunPerk.DeepMagazine, empty), "Quick Hands opens the magazine branch");
            Require(Mathf.Approximately(RunBuildMechanics.ReloadMultiplier(weapons[0]), .8f), "Reload modifier");
            RunBuildMechanics.Apply(RunPerk.FireCatalyst, null, null, null);
            Require(!RunBuildMechanics.CanUseTree(RunPerk.ThermalReaction, empty), "Hybrid requires both branches");
            var explosive = cards.First(c => c.Perk == RunPerk.BlastPressure);
            var hybrid = RunBuildState.Build(new Dictionary<UpgradeData, int> { [explosive] = 2 }, null);
            Require(RunBuildMechanics.CanUseTree(RunPerk.ThermalReaction, hybrid), "Fire/explosion hybrid unlocks");
            RunBuildMechanics.Reset();
            Require(!RunBuildMechanics.Has(RunPerk.QuickHands), "New run clears prior cards");
            Debug.Log($"RUN_BUILD_CHECK_OK: {cards.Count} cards, {weapons.Length} weapons");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            if (Application.isBatchMode) EditorApplication.Exit(1); else throw;
        }
        finally
        {
            RunBuildMechanics.Reset();
            foreach (var card in cards) UnityEngine.Object.DestroyImmediate(card);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
