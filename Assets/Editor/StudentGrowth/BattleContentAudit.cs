using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    public static class BattleContentAudit
    {
        [MenuItem("Tools/Student Growth/Battle/Audit P0 P1 content")]
        public static void Run()
        {
            var before = new HashSet<int>(Resources.FindObjectsOfTypeAll<ScriptableObject>().Select(a => a.GetInstanceID()));
            var lines = new List<string> { "source\ttype\tid\tdisplayName\tvalidation" };
            try
            {
                var session = MvpSampleDataFactory.CreateSessionWithStarterDeck();
                MvpSampleDataFactory.AddDefaultStarterItems(session);
                MvpSampleDataFactory.CreateThreeStageSequence();
                MvpSampleDataFactory.CreateDefaultShopItems();
                var generated = Resources.FindObjectsOfTypeAll<ScriptableObject>()
                    .Where(a => !before.Contains(a.GetInstanceID()) && a.GetType().Namespace == "StudentGrowth").ToArray();
                foreach (var asset in generated) Describe(asset, "MvpSampleDataFactory", lines);
                foreach (var type in new[] { "CardData", "ChallengeData", "ItemData", "ActivityData", "StageData", "BattleUIConfig" })
                {
                    var ids = AssetDatabase.FindAssets("t:" + type);
                    lines.Add("AssetDatabase\t" + type + "\tCOUNT=" + ids.Length);
                    foreach (var id in ids)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(id);
                        Describe(AssetDatabase.LoadMainAssetAtPath(path), path, lines);
                    }
                }
                Directory.CreateDirectory(BattleP1Verification.Output);
                File.WriteAllLines(BattleP1Verification.Output + "/content-audit.tsv", lines);
                Debug.Log("Battle content audit written; generated objects=" + generated.Length);
            }
            finally
            {
                foreach (var asset in Resources.FindObjectsOfTypeAll<ScriptableObject>().Where(a => !before.Contains(a.GetInstanceID())
                    && a.GetType().Namespace == "StudentGrowth" && !EditorUtility.IsPersistent(a))) UnityEngine.Object.DestroyImmediate(asset);
            }
        }
        private static void Describe(UnityEngine.Object asset, string source, List<string> lines)
        {
            string id = "", name = asset.name, validation = "registered";
            if (asset is CardData card) { id = card.cardId; name = card.displayName; validation = Format(BattleContentValidator.ValidateCard(card, true)); }
            if (asset is ChallengeData challenge) { id = challenge.challengeId; name = challenge.displayName; validation = Format(BattleContentValidator.Validate(challenge, null)); }
            if (asset is ItemData item) { id = item.itemId; name = item.displayName; validation = "effects=" + string.Join(",", item.effects.Where(e => e != null).Select(e => e.effectType + ":" + e.value)); }
            if (asset is ActivityData activity) { id = activity.activityId; name = activity.displayName; validation = "timeCost=" + activity.timeCost + "; fixedRewards=" + activity.fixedRewards.Count; }
            if (asset is StageData stage) { id = stage.stageId; name = stage.displayName; validation = "enterRewards=" + stage.enterStageRewards.Count + "; clearRewards=" + stage.clearStageRewards.Count; }
            lines.Add(source + "\t" + asset.GetType().Name + "\t" + id + "\t" + name + "\t" + validation.Replace("\n", " | "));
        }
        private static string Format(BattleContentValidation result)
        { return "errors=" + result.Errors.Count + "; warnings=" + result.Warnings.Count + "; " + string.Join(" | ", result.Errors.Concat(result.Warnings)); }
    }
}
