#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public static class BossCampaignCheck
{
    [MenuItem("ArcadeSurvivor/Checks/Five boss campaign")]
    public static void Run()
    {
        string scene = File.ReadAllText("Assets/Scenes/Game.unity");
        Match rotation = Regex.Match(scene, @"(?m)^  bossRotation:\r?\n(?:  - [^\r\n]*\r?\n)*");
        MatchCollection guids = Regex.Matches(rotation.Value, @"guid: (\w+)");
        Require(guids.Count == BossCampaign.BossCount, "Game scene must reference exactly five bosses");
        var patterns = new HashSet<BossAbilityPattern>();
        var identities = new HashSet<string>();
        for (int i = 0; i < guids.Count; i++)
        {
            BossData boss = AssetDatabase.LoadAssetAtPath<BossData>(AssetDatabase.GUIDToAssetPath(guids[i].Groups[1].Value));
            Require(boss != null, "Unresolved boss at index " + i);
            Require(patterns.Add(boss.AbilityPattern), "Boss signature patterns must be distinct");
            Require(identities.Add(boss.BossId), "Boss achievement IDs must be distinct");
            Require(boss.Phase1AbilityWarningDuration >= 1f && boss.Phase3AbilityWarningDuration >= 1f,
                "Boss attacks need readable warnings");
            Require(!boss.UsesBasicRangedAttack && !boss.UsesPassiveSummons,
                "Campaign bosses should use their own signatures, not a shared background attack set");
            Require(BossCampaign.IndexForWave((i + 1) * 10) == i, "Boss order mismatch");
        }
        Require(!BossCampaign.IsBossWave(0) && !BossCampaign.IsBossWave(60), "No boss rotation after the finale");
        Require(BossCampaign.IsBossWave(60, true) && BossCampaign.IndexForWave(60, true) == 0,
            "Endless mode restarts the boss rotation");
        Require(BossCampaign.IndexForWave(100, true) == 4 && BossCampaign.IndexForWave(110, true) == 0,
            "Endless boss rotation must stay within five profiles");
        Require(!BossCampaign.CanAdvance(50, false) && BossCampaign.CanAdvance(50, true),
            "Only explicit endless choice unlocks wave 51");
        Require(BossCampaign.ShouldOfferContinuation(50, 5, false) &&
            !BossCampaign.ShouldOfferContinuation(60, 5, true), "Finale choice is offered only once");
        Require(!BossCampaign.IsComplete(50, 4) && BossCampaign.IsComplete(50, 5), "Victory requires final defeat");
        Require(BossCampaign.Progress(50, 4, 0.5f) == 49.5f, "Slider must track current boss fight");
        Debug.Log("FIVE_BOSS_CAMPAIGN_CHECK_OK");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
#endif
