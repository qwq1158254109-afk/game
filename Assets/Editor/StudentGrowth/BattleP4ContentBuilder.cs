using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    public static class BattleP4ContentBuilder
    {
        public const string Root="Assets/Resources/StudentGrowth/Battle/P4";
        public const string Output="Docs/Battle/P4-v03";
        [MenuItem("Tools/Student Growth/Battle/P4/Create strategy content")]
        public static void Build()
        {
            Directory.CreateDirectory(Root);Directory.CreateDirectory(Output);AssetDatabase.Refresh();
            var art=Resources.Load<BattleUIConfig>(BattleUIConfig.ResourcePath);
            var practice=Card("basic_practice","基础练习",1,CardType.Study,CardTargetType.SingleEnemy,false,art,E(CardEffectType.AddChallengeProgress,6));
            var block=Card("organize_thoughts","整理思路",1,CardType.Rest,CardTargetType.Self,false,art,E(CardEffectType.GainBlock,5));
            var draw=Card("browse_notes","翻阅笔记",1,CardType.Study,CardTargetType.Self,false,art,E(CardEffectType.DrawCards,2));
            var energy=Card("adjust_rhythm","调整节奏",0,CardType.Rest,CardTargetType.Self,true,art,E(CardEffectType.GainBattleEnergy,1));
            var burst=Card("focused_review","集中复习",2,CardType.Study,CardTargetType.SingleEnemy,true,art,E(CardEffectType.AddChallengeProgress,12));
            var focus=Card("enter_focus","进入状态",1,CardType.Study,CardTargetType.Self,false,art,E(CardEffectType.GainFocus,2));
            var challenge=LoadOrCreate<BattleChallengeAsset>("challenge_strategy_practice");
            challenge.challengeId="challenge_strategy_practice";challenge.displayName="书桌策略练习";
            challenge.description="借用 10 张练习牌（6 种）。专注提高学习推进；分心和脆弱防线只影响标明的回合。胜利数学 +2，练习牌不加入永久卡组。";
            challenge.battleRulesVersion=BattleRulesVersion.Unified;challenge.unifiedHandLimit=10;challenge.focusIntensityLimit=10;
            challenge.requireSupportedBattleContent=true;challenge.targetProgress=60;challenge.maxTurns=6;challenge.startingEnergy=3;challenge.startingHandSize=5;
            challenge.loanDeck=new List<CardData>{practice,practice,practice,block,block,draw,energy,burst,focus,focus};
            challenge.intentMode=BattleIntentMode.Loop;
            challenge.intents=new List<ChallengeIntentData>{
                new ChallengeIntentData{intentId="strategy_disrupt",displayName="杂念干扰",triggerTurn=1,effects=new List<CardEffectData>{E(CardEffectType.ApplyDistraction,7500),E(CardEffectType.ApplyWeakDefense,5000)}},
                new ChallengeIntentData{intentId="strategy_pressure",displayName="作业催促 · 2 段",triggerTurn=2,effects=new List<CardEffectData>{E(CardEffectType.DamageBattleHp,8),E(CardEffectType.DamageBattleHp,8)}},
                new ChallengeIntentData{intentId="strategy_wait",displayName="短暂缓冲",triggerTurn=3}};
            // Unity inline serialization materializes null nested objects on save.
            challenge.fallbackIntent=new ChallengeIntentData{intentId="strategy_fallback_wait",displayName="等待"};
            challenge.successRewards=new List<RewardData>{new RewardData{rewardType=RewardType.AddPlayerStat,targetStat=PlayerStatKey.Math,value=2}};
            EditorUtility.SetDirty(challenge);AssetDatabase.SaveAssets();Audit();
        }
        private static CardEffectData E(CardEffectType type,int amount)=>new CardEffectData{effectType=type,value=amount,durationTurns=1};
        private static BattleCardAsset Card(string suffix,string name,int cost,CardType type,CardTargetType target,bool exhaust,BattleUIConfig art,params CardEffectData[] effects)
        {
            var card=LoadOrCreate<BattleCardAsset>("card_p4_"+suffix);card.cardId="card_p4_"+suffix;card.displayName=name;
            card.cost=cost;card.cardType=type;card.targetType=target;card.effectSchemaVersion=CardEffectSchemaVersion.Unified;card.requireSupportedBattleContent=true;
            card.effects=effects.ToList();card.exhaust=exhaust;card.cardSprite=art?.GetArtwork("card_art_"+suffix);
            card.description=name+" · 策略练习";EditorUtility.SetDirty(card);return card;
        }
        private static T LoadOrCreate<T>(string id)where T:ScriptableObject
        {string path=Root+"/"+id+".asset";var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset;}
        [MenuItem("Tools/Student Growth/Battle/P4/Audit strategy content")]
        public static void Audit()
        {
            var challenge=AssetDatabase.LoadAssetAtPath<BattleChallengeAsset>(Root+"/challenge_strategy_practice.asset");
            var result=BattleCompiler.Prepare(challenge,challenge?.loanDeck);
            var lines=new List<string>{"Success="+result.Success,"Loan deck="+challenge?.loanDeck.Count,"Rules=Unified; handLimit=10; focusLimit=10; permanent deck unchanged"};
            if(result.Success)lines.AddRange(result.Definition.Deck.Distinct().Select(c=>c.Id+" | cost="+c.Cost+" | "+BattlePresentationText.Effects(c.Effects)+" | exhaust="+c.Exhaust));
            lines.AddRange(result.Errors);lines.AddRange(result.Warnings);
            File.WriteAllLines(Output+"/content-audit.txt",lines);
            if(!result.Success)throw new InvalidOperationException(string.Join("\n",result.Errors));
            if(challenge.loanDeck.Any(c=>c.cardSprite==null))throw new InvalidOperationException("P4 card artwork missing");
            Debug.Log("P4 strategy content validated; six card illustrations bound.");
        }
        [MenuItem("Tools/Student Growth/Battle/P4/Run strategy and battle regression")]
        public static void Tests()=>BattleP1Verification.RunTests("rules",Output,new[]{"StudentGrowth.Tests.BattleEngineTests","StudentGrowth.Tests.ItemEffectResolverTests","StudentGrowth.Tests.BattleP3Tests","StudentGrowth.Tests.BattleP4Tests"});
    }
}
