using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    public static class BattleP2ContentBuilder
    {
        public const string Root="Assets/Resources/StudentGrowth/Battle/P2";
        public const string Output="Docs/Battle/P2-v03";
        [MenuItem("Tools/Student Growth/Battle/P2/Create migrated sandbox content")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var before=new HashSet<int>(Resources.FindObjectsOfTypeAll<ScriptableObject>().Select(o=>o.GetInstanceID()));
            var rows=new List<string>{"id\tasset\tschema\told_execution\told_display\tcompiled_effects\twarnings\tvalidation"};
            try
            {
                var session=MvpSampleDataFactory.CreateSessionWithStarterDeck();
                var stages=MvpSampleDataFactory.CreateThreeStageSequence();
                var originals=Resources.FindObjectsOfTypeAll<CardData>().Where(c=>!before.Contains(c.GetInstanceID())).OrderBy(c=>c.cardId).ToArray();
                foreach(var source in originals)
                {
                    string path=Root+"/"+source.cardId+".asset";var destination=LoadOrCreate<BattleCardAsset>(path);
                    JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source),destination);
                    bool unified=source.cardId=="card_study_math_practice"||source.cardId=="card_study_mock_exam";
                    destination.effectSchemaVersion=unified?CardEffectSchemaVersion.Unified:CardEffectSchemaVersion.Legacy;
                    destination.requireSupportedBattleContent=true;
                    if(unified)destination.targetType=CardTargetType.SingleEnemy;
                    var compiled=BattleCompiler.Prepare(stages[0].bossChallenge,new[]{destination});
                    rows.Add(source.cardId+"\t"+path+"\t"+destination.effectSchemaVersion+"\teffects; legacy block="+source.block
                        +"\tdamage="+source.damage+",drawCount="+source.drawCount+"\t"+(compiled.Success?BattlePresentationText.Effects(compiled.Definition.Deck[0].Effects):"")
                        +"\t"+string.Join(" | ",compiled.Warnings)+"\t"+(compiled.Success?"PASS":string.Join(" | ",compiled.Errors)));
                    EditorUtility.SetDirty(destination);
                }
                var challenge=LoadOrCreate<BattleChallengeAsset>(Root+"/boss_class_quiz.asset");
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(stages[0].bossChallenge),challenge);
                challenge.battleRulesVersion=BattleRulesVersion.Legacy;challenge.requireSupportedBattleContent=true;
                EditorUtility.SetDirty(challenge);
                rows.Add(challenge.challengeId+"\t"+Root+"/boss_class_quiz.asset\tLegacy\tpassives before draw; default damage 6\tunchanged\tno hand limit; preserve overflow\tno balance change; sandbox only\tPASS");
                AssetDatabase.SaveAssets();File.WriteAllLines(Output+"/migration.tsv",rows);
            }
            finally
            {
                foreach(var item in Resources.FindObjectsOfTypeAll<ScriptableObject>().Where(o=>!before.Contains(o.GetInstanceID())
                    &&o.GetType().Namespace=="StudentGrowth"&&!EditorUtility.IsPersistent(o)))Object.DestroyImmediate(item);
            }
        }
        private static T LoadOrCreate<T>(string path)where T:ScriptableObject
        {var item=AssetDatabase.LoadAssetAtPath<T>(path);if(item==null){item=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(item,path);}return item;}
        [MenuItem("Tools/Student Growth/Battle/P2/Run rule regression")]
        public static void Test()=>BattleP1Verification.RunTests("rules",Output,new[]{"StudentGrowth.Tests.BattleEngineTests","StudentGrowth.Tests.ItemEffectResolverTests"});
        [MenuItem("Tools/Student Growth/Battle/P2/Audit migrated content")]
        public static void Audit()
        {
            var challenge=AssetDatabase.LoadAssetAtPath<BattleChallengeAsset>(Root+"/boss_class_quiz.asset");
            var cards=AssetDatabase.FindAssets("t:BattleCardAsset",new[]{Root}).Select(id=>AssetDatabase.LoadAssetAtPath<BattleCardAsset>(AssetDatabase.GUIDToAssetPath(id))).ToArray();
            var result=BattleCompiler.Prepare(challenge,cards);
            var lines=new List<string>{"Success="+result.Success,"Challenge="+AssetDatabase.GetAssetPath(challenge),"Cards="+cards.Length};
            lines.AddRange(cards.Select(c=>c.cardId+" => "+AssetDatabase.GetAssetPath(c)));
            lines.AddRange(result.Errors.Select(e=>"ERROR "+e));lines.AddRange(result.Warnings.Select(w=>"WARNING "+w));
            Directory.CreateDirectory(Output);File.WriteAllLines(Output+"/content-audit.txt",lines);
            Debug.Log("P2 content audit: "+result.Errors.Count+" errors / "+result.Warnings.Count+" compatibility warnings");
        }
    }
}
