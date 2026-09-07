using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace StudentGrowth.Tests
{
    public class BattleP4Tests
    {
        private readonly List<Object> assets=new List<Object>();
        private T Data<T>()where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
        [TearDown]public void Cleanup(){foreach(var a in assets)Object.DestroyImmediate(a);assets.Clear();}
        private static CardEffectData E(CardEffectType type,int value,int duration=1)=>new CardEffectData{effectType=type,value=value,durationTurns=duration};
        private CardData Card(params CardEffectData[] effects)
        {var c=Data<CardData>();c.cardId="card_"+assets.Count;c.cardType=CardType.Study;c.effectSchemaVersion=CardEffectSchemaVersion.Unified;c.effects.AddRange(effects);return c;}
        private ChallengeData Challenge(params CardEffectData[] enemy)
        {
            var c=Data<ChallengeData>();c.challengeId="p4_test";c.battleRulesVersion=BattleRulesVersion.Unified;c.maxTurns=6;
            c.targetProgress=1000;c.startingHandSize=10;c.startingEnergy=20;
            c.intents.Add(new ChallengeIntentData{intentId="debuff",triggerTurn=1,effects=enemy.ToList()});
            c.fallbackIntent=new ChallengeIntentData{intentId="wait"};return c;
        }
        private BattleEngine Engine(ChallengeData challenge,params CardData[] cards)
        {var p=BattleCompiler.Prepare(challenge,cards);Assert.IsTrue(p.Success,string.Join("\n",p.Errors));return new BattleEngine(p.Definition,new PlayerState(),"session","battle",new BattleRandom(123));}
        private static BattleRequest R(BattleEngine engine,string id=null)=>new BattleRequest("session","battle",engine.Snapshot().Revision,Guid.NewGuid().ToString(),id);
        private static void Play(BattleEngine e,int index)
        {
            var s=e.Snapshot();string id="card:"+index;var card=s.Cards.Single(c=>c.InstanceId==id);
            string target=card.Definition.Target==CardTargetType.SingleEnemy?e.EnemyTargetId:card.Definition.Target==CardTargetType.Self?BattleEngine.SelfTargetId:null;
            Assert.IsTrue(e.TryPlayCard(new BattleRequest(s.RuntimeSessionId,s.BattleId,s.Revision,Guid.NewGuid().ToString(),id,target)).Accepted);
        }
        private static void End(BattleEngine e)=>Assert.IsTrue(e.TryEndTurn(R(e)).Accepted);
        private static BattleStatus Status(BattleEngine e,BattleStatusKind kind)=>e.Snapshot().Statuses.Single(s=>s.Kind==kind);
        private static string Fingerprint(BattleSnapshot s)=>string.Join("/",s.Revision,s.Turn,s.Phase,s.Hp,s.Block,s.Progress,s.Energy,s.RandomDraws,s.Events.Count,
            string.Join(",",s.Cards.Select(c=>c.InstanceId+":"+c.Pile)),string.Join(",",s.Statuses.Select(t=>t.Kind+":"+t.Intensity+":"+t.ActiveFromPlayerTurn+":"+t.ExpiresAfterPlayerTurn)));

        [Test]public void T35_EnemyOneTurnStatusCoversNextActionThenExpires()
        {
            var e=Engine(Challenge(E(CardEffectType.ApplyDistraction,7500)),Card(E(CardEffectType.AddChallengeProgress,6)));
            var before=Fingerprint(e.Snapshot());var preview=e.GetIntentPreview();Assert.AreEqual(before,Fingerprint(e.Snapshot()));
            Assert.AreEqual(2,preview.Statuses[0].ActiveFromPlayerTurn);Assert.AreEqual(2,preview.Statuses[0].ExpiresAfterPlayerTurn);
            End(e);Assert.AreEqual(1,Status(e,BattleStatusKind.Distraction).RemainingTurns(2));
            Assert.AreEqual(4,e.GetCardPreview("card:0").ProgressChange);Play(e,0);Assert.AreEqual(4,e.Snapshot().Progress);
            End(e);Assert.IsEmpty(e.Snapshot().Statuses);Assert.AreEqual(6,e.GetCardPreview("card:0").ProgressChange);
        }
        [Test]public void T36_FocusThenMultiplierThenFloorMatchesExampleC()
        {
            var focus=Card(E(CardEffectType.GainFocus,2));var practice=Card(E(CardEffectType.AddChallengeProgress,6));
            var e=Engine(Challenge(E(CardEffectType.ApplyDistraction,7500)),focus,practice);Play(e,0);End(e);
            Assert.AreEqual(6,e.GetCardPreview("card:1").ProgressChange);Play(e,1);Assert.AreEqual(6,e.Snapshot().Progress);
            End(e);Assert.AreEqual(2,Status(e,BattleStatusKind.Focus).Intensity);Assert.AreEqual(8,e.GetCardPreview("card:1").ProgressChange);
        }
        [Test]public void FocusStacksToFrozenChallengeCap()
        {
            var c=Challenge();c.focusIntensityLimit=3;var focus=Card(E(CardEffectType.GainFocus,2));var e=Engine(c,focus,focus);
            c.focusIntensityLimit=999;Play(e,0);var first=e.Snapshot();Play(e,1);
            Assert.AreEqual(3,Status(e,BattleStatusKind.Focus).Intensity);Assert.AreEqual(2,first.Statuses[0].Intensity);
        }
        [Test]public void StrongerIntensityAndLaterExpiryStackIndependently()
        {
            var e=Engine(Challenge(),Card(E(CardEffectType.ApplyDistraction,5000,1)),Card(E(CardEffectType.ApplyDistraction,7500,3)),Card(E(CardEffectType.AddChallengeProgress,7)));
            Play(e,0);Play(e,1);var status=Status(e,BattleStatusKind.Distraction);
            Assert.AreEqual(5000,status.Intensity);Assert.AreEqual(3,status.ExpiresAfterPlayerTurn);Assert.AreEqual(3,e.GetCardPreview("card:2").ProgressChange);
            End(e);Assert.AreEqual(2,Status(e,BattleStatusKind.Distraction).RemainingTurns(2));End(e);End(e);Assert.IsEmpty(e.Snapshot().Statuses);
        }
        [Test]public void ExpiredStrongDebuffDoesNotPolluteLaterWeakerApplication()
        {
            var e=Engine(Challenge(E(CardEffectType.ApplyDistraction,7500)),Card(E(CardEffectType.ApplyDistraction,2500)));
            Play(e,0);End(e);Assert.AreEqual(7500,Status(e,BattleStatusKind.Distraction).Intensity);Assert.AreEqual(2,Status(e,BattleStatusKind.Distraction).ExpiresAfterPlayerTurn);
        }
        [Test]public void WeakDefenseOnlyModifiesCardBlockAndKeepsSeparateDuration()
        {
            var e=Engine(Challenge(E(CardEffectType.ApplyWeakDefense,5000,2)),Card(E(CardEffectType.GainBlock,5)),Card(E(CardEffectType.AddChallengeProgress,6)));
            End(e);Assert.AreEqual(2,e.GetCardPreview("card:0").BlockChange);Assert.AreEqual(6,e.GetCardPreview("card:1").ProgressChange);
            Play(e,0);Assert.AreEqual(2,e.Snapshot().Block);End(e);Assert.AreEqual(1,Status(e,BattleStatusKind.WeakDefense).RemainingTurns(3));End(e);Assert.IsEmpty(e.Snapshot().Statuses);
        }
        [Test]public void NonStudyProgressAndSignedLegacyLossKeepTheirSemantics()
        {
            var focus=Card(E(CardEffectType.GainFocus,2),E(CardEffectType.ApplyDistraction,5000));
            var social=Card(E(CardEffectType.AddChallengeProgress,6));social.cardType=CardType.Social;
            var negative=Card(E(CardEffectType.AddChallengeProgress,-3));negative.effectSchemaVersion=CardEffectSchemaVersion.Legacy;
            var e=Engine(Challenge(),focus,social,negative);Play(e,0);Play(e,1);Play(e,2);Assert.AreEqual(3,e.Snapshot().Progress);
        }
        [Test]public void EachProgressEffectGetsFocusAndSequentialStatusChangesArePreviewed()
        {
            var e=Engine(Challenge(),Card(E(CardEffectType.GainFocus,2),E(CardEffectType.ApplyDistraction,7500),E(CardEffectType.AddChallengeProgress,6),E(CardEffectType.AddChallengeProgress,3)));
            string before=Fingerprint(e.Snapshot());var preview=e.GetCardPreview("card:0");Assert.AreEqual(9,preview.ProgressChange);Assert.AreEqual(before,Fingerprint(e.Snapshot()));
            Play(e,0);Assert.AreEqual(9,e.Snapshot().Progress);StringAssert.Contains("专注",preview.Summary);
        }
        [Test]public void UnaffordableCardStillShowsDynamicPreviewButCannotMutate()
        {
            var study=Card(E(CardEffectType.AddChallengeProgress,6));study.cost=99;
            var e=Engine(Challenge(),Card(E(CardEffectType.GainFocus,2)),study);Play(e,0);
            string before=Fingerprint(e.Snapshot());var p=e.GetCardPreview("card:1");Assert.AreEqual(8,p.ProgressChange);Assert.AreEqual(BattleFailure.InsufficientEnergy,p.Failure);
            Assert.IsFalse(e.TryPlayCard(R(e,"card:1")).Accepted);Assert.AreEqual(before,Fingerprint(e.Snapshot()));
        }
        [Test]public void RepeatedPreviewsNeverAdvanceRandomOrChangeStatusAndIntent()
        {
            var cards=new[]{Card(E(CardEffectType.GainFocus,2),E(CardEffectType.DrawCards,2)),Card(E(CardEffectType.AddChallengeProgress,6))};
            var c=Challenge(E(CardEffectType.ApplyDistraction,7500));var a=Engine(c,cards);var b=Engine(c,cards);
            for(int i=0;i<20;i++){a.GetCardPreview("card:0");a.GetIntentPreview();}
            Play(a,0);Play(b,0);End(a);End(b);Assert.AreEqual(Fingerprint(a.Snapshot()),Fingerprint(b.Snapshot()));
        }
        [Test]public void IntentPreviewsSequentialDamageAfterBlockAndStatusWithoutSelectingAgain()
        {
            var e=Engine(Challenge(E(CardEffectType.ApplyWeakDefense,5000),E(CardEffectType.DamageBattleHp,4),E(CardEffectType.DamageBattleHp,4)),Card(E(CardEffectType.GainBlock,5)));
            Play(e,0);var state=e.Snapshot();var preview=e.GetIntentPreview();Assert.AreEqual(3,state.Hp-preview.Hp);Assert.AreEqual(0,preview.Block);
            Assert.AreSame(state.Intent,e.Snapshot().Intent);End(e);Assert.AreEqual(preview.Hp,e.Snapshot().Hp);Assert.AreEqual(2,Status(e,BattleStatusKind.WeakDefense).ActiveFromPlayerTurn);
        }
        [TestCase(BattleRulesVersion.Legacy)][TestCase(BattleRulesVersion.Unified)]
        public void StatusCardsWorkWithBothChallengeVersionsWithoutChangingRules(BattleRulesVersion rules)
        {
            var c=Challenge();c.battleRulesVersion=rules;c.targetProgress=5;
            var e=Engine(c,Card(E(CardEffectType.GainFocus,2)),Card(E(CardEffectType.AddChallengeProgress,6)));Play(e,0);Play(e,1);
            Assert.AreEqual(rules==BattleRulesVersion.Legacy?8:5,e.Snapshot().Progress);Assert.IsEmpty(e.Snapshot().Statuses);
        }
        [Test]public void FinalTurnSkipsEnemyAndClearsBattleStatuses()
        {
            var c=Challenge(E(CardEffectType.ApplyDistraction,7500));c.maxTurns=1;
            var e=Engine(c,Card(E(CardEffectType.GainFocus,2)));Play(e,0);Assert.IsEmpty(e.GetIntentPreview().Statuses.Where(s=>s.Kind==BattleStatusKind.Distraction));
            End(e);Assert.AreEqual(BattleEndReason.TurnLimit,e.Result.Reason);Assert.IsEmpty(e.Result.Final.Statuses);
        }
        [Test]public void OpeningTimedStatusCountsCurrentPlayerTurn()
        {
            var c=Challenge();var card=Card(E(CardEffectType.AddChallengeProgress,6));
            var p=BattleCompiler.Prepare(c,new[]{card},new[]{new BattleEffect(BattleEffectKind.Distraction,5000)});
            var e=new BattleEngine(p.Definition,new PlayerState(),"session","battle",new BattleRandom(1));
            Assert.AreEqual(1,Status(e,BattleStatusKind.Distraction).ActiveFromPlayerTurn);Assert.AreEqual(3,e.GetCardPreview("card:0").ProgressChange);End(e);Assert.IsEmpty(e.Snapshot().Statuses);
        }
        [Test]public void LegacyIntentCopyPreservesExplicitDuration()
        {
            var c=Challenge(E(CardEffectType.ApplyDistraction,7500,3));c.battleRulesVersion=BattleRulesVersion.Legacy;
            var e=Engine(c,Card());End(e);Assert.AreEqual(4,Status(e,BattleStatusKind.Distraction).ExpiresAfterPlayerTurn);
        }
        [TestCase(-1,1)][TestCase(10001,1)][TestCase(5000,0)][TestCase(5000,100)]
        public void InvalidDebuffConfigurationBlockedBeforeBattle(int multiplier,int turns)
        {var c=Card(E(CardEffectType.ApplyDistraction,multiplier,turns));Assert.IsFalse(BattleCompiler.Prepare(Challenge(),new[]{c}).Success);}
        [TestCase(0)][TestCase(10001)]public void InvalidFocusCapBlocked(int cap)
        {var c=Challenge();c.focusIntensityLimit=cap;Assert.IsFalse(BattleCompiler.Prepare(c,new[]{Card()}).Success);}
        [Test]public void UnsupportedHookEffectRemainsBlocked()
        {Assert.IsFalse(BattleCompiler.Prepare(Challenge(),new[]{Card(E(CardEffectType.ModifyNextCardEffect,1))}).Success);}
        [Test]public void WideArithmeticAndZeroMultiplierAreExact()
        {
            var c=Challenge();c.targetProgress=int.MaxValue;
            var e=Engine(c,Card(E(CardEffectType.GainFocus,2),E(CardEffectType.ApplyDistraction,7500),E(CardEffectType.AddChallengeProgress,int.MaxValue)));
            Assert.AreEqual(1610612736,e.GetCardPreview("card:0").ProgressChange);Play(e,0);Assert.AreEqual(1610612736,e.Snapshot().Progress);
            var zero=Engine(Challenge(),Card(E(CardEffectType.GainFocus,2),E(CardEffectType.ApplyDistraction,0),E(CardEffectType.AddChallengeProgress,6)));
            Play(zero,0);Assert.AreEqual(0,zero.Snapshot().Progress);
        }
        [Test]public void StrategyAssetsCompileAndBindAllSixArtworks()
        {
            var c=Resources.Load<BattleChallengeAsset>("StudentGrowth/Battle/P4/challenge_strategy_practice");Assert.IsNotNull(c);
            Assert.AreEqual(10,c.loanDeck.Count);Assert.AreEqual(6,c.loanDeck.Distinct().Count());Assert.IsTrue(c.loanDeck.All(a=>a.cardSprite!=null));
            Assert.IsTrue(BattleCompiler.Prepare(c,c.loanDeck).Success);Assert.AreEqual(BattleIntentMode.Loop,c.intentMode);
        }
    }
}
