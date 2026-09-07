using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace StudentGrowth.Tests
{
    public class BattleEngineTests
    {
        private readonly List<Object> assets=new List<Object>();
        private int sequence;
        private T Data<T>()where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();assets.Add(a);return a;}
        [TearDown]public void Cleanup(){foreach(var a in assets)Object.DestroyImmediate(a);assets.Clear();}
        private CardData Card(params CardEffectData[] effects)
        {var c=Data<CardData>();c.cardId="card_"+sequence++;c.displayName=c.cardId;c.effectSchemaVersion=CardEffectSchemaVersion.Unified;c.effects.AddRange(effects);return c;}
        private static CardEffectData E(CardEffectType type,int value,PlayerStatKey stat=PlayerStatKey.Energy)=>new CardEffectData{effectType=type,value=value,targetStat=stat};
        private ChallengeData Challenge(BattleRulesVersion rules=BattleRulesVersion.Unified)
        {
            var c=Data<ChallengeData>();c.challengeId="challenge_test";c.displayName="测试";c.battleRulesVersion=rules;
            c.targetProgress=100;c.maxTurns=3;c.startingEnergy=3;c.startingHandSize=5;
            c.fallbackIntent=new ChallengeIntentData{intentId="wait",displayName="等待"};return c;
        }
        private BattleEngine Engine(ChallengeData challenge,CardData[] cards,PlayerState player=null,IEnumerable<BattleEffect> opening=null)
        {
            var preparation=BattleCompiler.Prepare(challenge,cards,opening);
            Assert.IsTrue(preparation.Success,string.Join("\n",preparation.Errors));
            return new BattleEngine(preparation.Definition,player??new PlayerState(),"session","battle",new BattleRandom(123));
        }
        private static BattleRequest R(BattleEngine e,string id=null,string target=null,string request=null,int? revision=null,string session="session",string battle="battle")
            =>new BattleRequest(session,battle,revision??e.Snapshot().Revision,request??Guid.NewGuid().ToString(),id,target);
        private static string First(BattleEngine e)=>e.Snapshot().InPile(BattlePile.Hand)[0].InstanceId;
        private static string Fingerprint(BattleSnapshot s)=>string.Join("/",s.Revision,s.Phase,s.Turn,s.Hp,s.MaxHp,s.Block,s.Energy,s.Progress,s.RandomDraws,
            string.Join(",",s.Cards.Select(c=>c.InstanceId+":"+c.Pile+":"+c.Cost)),string.Join(",",s.LegacyStats.Select(p=>p.Key+":"+p.Value)),s.Events.Count);
        private static void RejectUnchanged(BattleEngine e,BattleFailure failure,Func<BattleActionReceipt> action)
        {string before=Fingerprint(e.Snapshot());Assert.AreEqual(failure,action().Failure);Assert.AreEqual(before,Fingerprint(e.Snapshot()));}

        [Test]public void InvalidRequestsAreZeroMutation()
        {
            var c=Card();c.cost=4;var e=Engine(Challenge(),new[]{c});string id=First(e);
            RejectUnchanged(e,BattleFailure.InsufficientEnergy,()=>e.TryPlayCard(R(e,id)));
            RejectUnchanged(e,BattleFailure.WrongSession,()=>e.TryPlayCard(R(e,id,session:"old")));
            RejectUnchanged(e,BattleFailure.WrongBattle,()=>e.TryPlayCard(R(e,id,battle:"old")));
            RejectUnchanged(e,BattleFailure.StaleInput,()=>e.TryPlayCard(R(e,id,revision:99)));
            RejectUnchanged(e,BattleFailure.CardNotInHand,()=>e.TryPlayCard(R(e,"missing")));
        }
        [Test]public void InvalidTargetDoesNotSpendOrAdvanceRandom()
        {
            var c=Card(E(CardEffectType.AddChallengeProgress,5));c.cost=1;c.targetType=CardTargetType.SingleEnemy;
            var e=Engine(Challenge(),new[]{c});RejectUnchanged(e,BattleFailure.InvalidTarget,()=>e.TryPlayCard(R(e,First(e),"wrong")));
        }
        [Test]public void DuplicateInstancesHaveIndependentCostsAndDefinitionsStayFrozen()
        {
            var c=Card();c.cost=2;var e=Engine(Challenge(),new[]{c,c});var hand=e.Snapshot().InPile(BattlePile.Hand);
            e.TrySetTurnCostDelta(R(e,hand[0].InstanceId),-1);c.cost=99;c.effects.Add(E(CardEffectType.DamageBattleHp,100));
            Assert.AreEqual(1,e.GetCardPreview(hand[0].InstanceId).Cost);Assert.AreEqual(2,e.GetCardPreview(hand[1].InstanceId).Cost);
            e.TryPlayCard(R(e,hand[0].InstanceId));Assert.AreEqual(2,e.Snapshot().Energy);Assert.AreEqual(60,e.Snapshot().Hp);
            Assert.AreEqual(99,c.cost);Assert.AreNotEqual(hand[0].InstanceId,hand[1].InstanceId);
        }
        [Test]public void RepeatedPlayAndEndTurnCannotExecuteTwice()
        {
            var c=Card();c.cost=1;var e=Engine(Challenge(),new[]{c,c});var request=R(e,First(e));
            e.TryPlayCard(request);RejectUnchanged(e,BattleFailure.AlreadyHandled,()=>e.TryPlayCard(request));
            var end=R(e);e.TryEndTurn(end);RejectUnchanged(e,BattleFailure.AlreadyHandled,()=>e.TryEndTurn(end));
            RejectUnchanged(e,BattleFailure.StaleInput,()=>e.TryPlayCard(R(e,First(e),revision:0)));
            Assert.AreEqual(2,e.Snapshot().Turn);
        }
        [Test]public void ResolvingCardNeverDrawsItself()
        {
            var draw=Card(E(CardEffectType.DrawCards,2));var e=Engine(Challenge(),new[]{draw});
            e.TryPlayCard(R(e,First(e)));Assert.AreEqual(0,e.Snapshot().InPile(BattlePile.Hand).Count);
            Assert.AreEqual(1,e.Snapshot().InPile(BattlePile.Discard).Count);
            Assert.IsTrue(e.Snapshot().Events.Any(v=>v.Kind=="Draw"&&v.Source=="card:0"&&v.After==0));
        }
        [Test]public void ReshuffleExcludesResolvingAndExhaust()
        {
            var normal=Card();var exhaust=Card();exhaust.exhaust=true;var draw=Card(E(CardEffectType.DrawCards,9));
            var e=Engine(Challenge(),new[]{normal,exhaust,draw});
            e.TryPlayCard(R(e,"card:0"));e.TryPlayCard(R(e,"card:1"));e.TryPlayCard(R(e,"card:2"));
            Assert.AreEqual("card:0",e.Snapshot().InPile(BattlePile.Hand).Single().InstanceId);
            Assert.AreEqual("card:1",e.Snapshot().InPile(BattlePile.Exhaust).Single().InstanceId);
            Assert.AreEqual("card:2",e.Snapshot().InPile(BattlePile.Discard).Single().InstanceId);
        }
        [TestCase(BattleRulesVersion.Legacy,CardEffectSchemaVersion.Legacy,12,120)]
        [TestCase(BattleRulesVersion.Legacy,CardEffectSchemaVersion.Unified,12,120)]
        [TestCase(BattleRulesVersion.Unified,CardEffectSchemaVersion.Legacy,10,100)]
        [TestCase(BattleRulesVersion.Unified,CardEffectSchemaVersion.Unified,10,100)]
        public void ChallengeRulesControlCapacityAndOverflow(BattleRulesVersion rules,CardEffectSchemaVersion format,int count,int progress)
        {
            var c=Card(E(CardEffectType.AddChallengeProgress,120));c.effectSchemaVersion=format;c.targetType=CardTargetType.SingleEnemy;
            var challenge=Challenge(rules);challenge.startingHandSize=12;
            var e=Engine(challenge,Enumerable.Repeat(c,12).ToArray());Assert.AreEqual(count,e.Snapshot().InPile(BattlePile.Hand).Count);
            e.TryPlayCard(R(e,First(e),e.EnemyTargetId));Assert.AreEqual(progress,e.Snapshot().Progress);
        }
        [Test]public void CapacityStopsOpeningDrawBeforeTakingOrShufflingCards()
        {
            var c=Card();var d=Challenge();d.startingHandSize=10;
            var e=Engine(d,Enumerable.Repeat(c,12).ToArray(),opening:new[]{new BattleEffect(BattleEffectKind.Draw,2)});
            Assert.AreEqual(10,e.Snapshot().InPile(BattlePile.Hand).Count);Assert.AreEqual(2,e.Snapshot().InPile(BattlePile.Draw).Count);
            Assert.AreEqual(0,e.Snapshot().Events.Last(x=>x.Kind=="Draw").After);
        }
        [TestCase(BattleRulesVersion.Legacy,0)]
        [TestCase(BattleRulesVersion.Unified,5)]
        public void OpeningVictoryRespectsChallengeOrder(BattleRulesVersion rules,int hand)
        {
            var c=Card();var e=Engine(Challenge(rules),Enumerable.Repeat(c,5).ToArray(),opening:new[]{new BattleEffect(BattleEffectKind.Progress,100)});
            Assert.AreEqual(hand,e.Snapshot().InPile(BattlePile.Hand).Count);Assert.AreEqual(BattleOutcome.Victory,e.Result.Outcome);
            RejectUnchanged(e,BattleFailure.WrongPhase,()=>e.TryEndTurn(R(e)));
        }
        [TestCase(BattleRulesVersion.Legacy,12,120)]
        [TestCase(BattleRulesVersion.Unified,10,100)]
        public void MixedSchemasUseFrozenChallengeRules(BattleRulesVersion rules,int expectedHand,int expectedProgress)
        {
            var a=Card(E(CardEffectType.DrawCards,20));a.effectSchemaVersion=CardEffectSchemaVersion.Legacy;
            var b=Card(E(CardEffectType.AddChallengeProgress,120));var challenge=Challenge(rules);challenge.startingHandSize=2;
            var prepared=BattleCompiler.Prepare(challenge,new[]{a}.Concat(Enumerable.Repeat(b,12)).ToArray());Assert.IsTrue(prepared.Success);
            var e=new BattleEngine(prepared.Definition,new PlayerState(),"session","battle",new IdentityRandom());
            challenge.battleRulesVersion=rules==BattleRulesVersion.Legacy?BattleRulesVersion.Unified:BattleRulesVersion.Legacy;
            Assert.AreEqual(rules,e.Snapshot().Definition.Rules);
            Assert.IsTrue(e.TryPlayCard(R(e,"card:0")).Accepted);Assert.AreEqual(expectedHand,e.Snapshot().InPile(BattlePile.Hand).Count);
            Assert.IsTrue(e.TryPlayCard(R(e,"card:1")).Accepted);Assert.AreEqual(expectedProgress,e.Snapshot().Progress);
        }
        private sealed class IdentityRandom:IBattleRandom
        {
            public uint Seed=>77;public int Draws{get;private set;}
            public int Next(int exclusiveMax){Draws++;return exclusiveMax-1;}
            public IBattleRandom Copy()=>new IdentityRandom{Draws=Draws};
        }
        [Test]public void EtherealWinsOverRetainAndRetainedCostResets()
        {
            var a=Card();a.retain=true;a.ethereal=true;var b=Card();b.cost=2;b.retain=true;
            var e=Engine(Challenge(),new[]{a,b});e.TrySetTurnCostDelta(R(e,"card:1"),-2);e.TryEndTurn(R(e));
            Assert.AreEqual(BattlePile.Exhaust,e.Snapshot().Cards.Single(x=>x.InstanceId=="card:0").Pile);
            Assert.AreEqual(2,e.GetCardPreview("card:1").Cost);Assert.AreEqual(BattlePile.Hand,e.Snapshot().Cards.Single(x=>x.InstanceId=="card:1").Pile);
        }
        [Test]public void LegacyScalarsAreNotAppliedTwiceAndRestBlockIsExplicit()
        {
            var c=Card(E(CardEffectType.AddChallengeProgress,7),E(CardEffectType.DrawCards,1));c.effectSchemaVersion=CardEffectSchemaVersion.Legacy;
            c.damage=99;c.drawCount=99;c.block=3;var challenge=Challenge(BattleRulesVersion.Legacy);
            var e=Engine(challenge,new[]{c});e.TryPlayCard(R(e,First(e),e.EnemyTargetId));
            Assert.AreEqual(7,e.Snapshot().Progress);Assert.AreEqual(3,e.Snapshot().Block);
            Assert.AreEqual(0,e.Snapshot().InPile(BattlePile.Hand).Count);
        }
        [Test]public void UnifiedSelfDoesNotGainImplicitBlock()
        {
            var c=Card();c.targetType=CardTargetType.Self;c.cardType=CardType.Rest;c.block=100;
            var e=Engine(Challenge(),new[]{c});e.TryPlayCard(R(e,First(e),BattleEngine.SelfTargetId));Assert.AreEqual(0,e.Snapshot().Block);
        }
        [Test]public void LegacyEnergyAndAssociatedCapsStayIsolatedAndOrdered()
        {
            var p=new PlayerState();var c=Card(E(CardEffectType.AddPlayerStat,20,PlayerStatKey.MaxEnergy),E(CardEffectType.GainEnergy,20),
                E(CardEffectType.AddPlayerStat,90,PlayerStatKey.Confidence),E(CardEffectType.ReducePlayerStat,50,PlayerStatKey.Confidence));
            c.effectSchemaVersion=CardEffectSchemaVersion.Legacy;var e=Engine(Challenge(),new[]{c},p);
            e.TryPlayCard(R(e,First(e),BattleEngine.SelfTargetId));
            Assert.AreEqual(80,e.Snapshot().LegacyStats[PlayerStatKey.Energy]);Assert.AreEqual(80,e.Snapshot().LegacyStats[PlayerStatKey.MaxEnergy]);
            Assert.AreEqual(50,e.Snapshot().LegacyStats[PlayerStatKey.Confidence]);Assert.AreEqual(3,e.Snapshot().Energy);
            Assert.AreEqual(60,p.energy);Assert.AreEqual(60,p.maxEnergy);Assert.AreEqual(45,p.confidence);
        }
        [Test]public void UnifiedWaitDoesNotUseLegacyDefaultDamage()
        {var e=Engine(Challenge(),new[]{Card()});e.TryEndTurn(R(e));Assert.AreEqual(60,e.Snapshot().Hp);}
        [Test]public void LegacyDefaultDamageAndCopiedIntentArePreserved()
        {
            var challenge=Challenge(BattleRulesVersion.Legacy);challenge.intents.Add(new ChallengeIntentData{triggerTurn=1,effects=new List<CardEffectData>{E(CardEffectType.AddPressure,2)}});
            var e=Engine(challenge,new[]{Card()});challenge.intents[0].effects[0].value=99;e.TryEndTurn(R(e));
            Assert.AreEqual(58,e.Snapshot().Hp);Assert.AreEqual(22,e.Snapshot().LegacyStats[PlayerStatKey.Pressure]);
            Assert.AreEqual(6,e.Snapshot().Intent.Effects[0].Amount);
        }
        [Test]public void MultiHitDamageUsesRemainingBlockPerHit()
        {
            var d=Challenge();d.fallbackIntent.effects.Add(E(CardEffectType.DamageBattleHp,4));d.fallbackIntent.effects.Add(E(CardEffectType.DamageBattleHp,4));
            var e=Engine(d,new[]{Card()},opening:new[]{new BattleEffect(BattleEffectKind.Block,5)});
            Assert.AreEqual(57,e.GetIntentPreview().Hp);e.TryEndTurn(R(e));Assert.AreEqual(57,e.Snapshot().Hp);
        }
        [Test]public void LastTurnAllowsCardVictoryButEndTurnSkipsEnemy()
        {
            var d=Challenge();d.maxTurns=1;d.fallbackIntent.effects.Add(E(CardEffectType.DamageBattleHp,999));var c=Card(E(CardEffectType.AddChallengeProgress,100));
            var win=Engine(d,new[]{c});win.TryPlayCard(R(win,First(win)));Assert.AreEqual(BattleOutcome.Victory,win.Result.Outcome);
            var lose=Engine(d,new[]{c});Assert.AreEqual(60,lose.GetIntentPreview().Hp);
            lose.TryEndTurn(R(lose));Assert.AreEqual(BattleEndReason.TurnLimit,lose.Result.Reason);Assert.AreEqual(60,lose.Snapshot().Hp);
        }
        [Test]public void DeathOutranksProgressAndStopsLaterEffects()
        {
            var c=Card(E(CardEffectType.AddChallengeProgress,100),E(CardEffectType.DamageBattleHp,60),E(CardEffectType.GainBattleEnergy,9));
            var e=Engine(Challenge(),new[]{c});e.TryPlayCard(R(e,First(e)));Assert.AreEqual(BattleEndReason.HpDepleted,e.Result.Reason);
            Assert.AreEqual(3,e.Snapshot().Energy);Assert.AreEqual(1,e.Snapshot().InPile(BattlePile.Discard).Count);
        }
        [Test]public void ProgressReachedMidCardStillExecutesRemainingEffects()
        {
            var c=Card(E(CardEffectType.AddChallengeProgress,100),E(CardEffectType.GainBlock,5));var e=Engine(Challenge(),new[]{c});
            e.TryPlayCard(R(e,First(e)));Assert.AreEqual(5,e.Result.Final.Block);
        }
        [Test]public void PreviewsDoNotChangeReplayOrRandomPosition()
        {
            var c=Card(E(CardEffectType.DrawCards,3));var d=Challenge();d.startingHandSize=2;
            var a=Engine(d,Enumerable.Repeat(c,8).ToArray());var b=Engine(d,Enumerable.Repeat(c,8).ToArray());
            string before=Fingerprint(a.Snapshot());for(int i=0;i<20;i++){a.GetCardPreview(First(a));a.GetIntentPreview();}
            Assert.AreEqual(before,Fingerprint(a.Snapshot()));a.TryPlayCard(R(a,First(a)));b.TryPlayCard(R(b,First(b)));
            Assert.AreEqual(Fingerprint(a.Snapshot()),Fingerprint(b.Snapshot()));
        }
        [Test]public void ExecutionExceptionFaultsAfterCostAndReturnsResolvingCard()
        {
            var c=Card(E(CardEffectType.GainBattleEnergy,int.MaxValue));c.cost=1;var e=Engine(Challenge(),new[]{c});
            var r=e.TryPlayCard(R(e,First(e)));Assert.AreEqual(BattleFailure.ExecutionFault,r.Failure);Assert.AreEqual(BattlePhase.Faulted,e.Snapshot().Phase);
            Assert.AreEqual(BattleOutcome.Faulted,e.Result.Outcome);Assert.AreEqual(0,e.Snapshot().InPile(BattlePile.Resolving).Count);
            Assert.AreEqual(1,e.Snapshot().InPile(BattlePile.Exhaust).Count);RejectUnchanged(e,BattleFailure.WrongPhase,()=>e.TryEndTurn(R(e)));
        }
        [Test]public void InvalidContentReportsFieldsBeforeEngineStarts()
        {
            var c=Card(E(CardEffectType.AddTemporaryBuff,1));c.cost=-1;c.targetType=CardTargetType.AllEnemies;
            var d=Challenge();d.unifiedHandLimit=0;var p=BattleCompiler.Prepare(d,new[]{c});Assert.IsFalse(p.Success);
            StringAssert.Contains("cost",string.Join(" ",p.Errors));StringAssert.Contains("effects[0]",string.Join(" ",p.Errors));StringAssert.Contains("unifiedHandLimit",string.Join(" ",p.Errors));
        }
        [Test]public void DifferentAssetsCannotShareDefinitionIdButRepeatedSameAssetIsValid()
        {
            var a=Card();var b=Card();b.cardId=a.cardId;
            Assert.IsFalse(BattleCompiler.Prepare(Challenge(),new[]{a,b}).Success);Assert.IsTrue(BattleCompiler.Prepare(Challenge(),new[]{a,a}).Success);
        }
        [Test]public void LoopIntentsNeedEntriesAndByTurnNeedsFallback()
        {
            var d=Challenge();d.intentMode=BattleIntentMode.Loop;Assert.IsFalse(BattleCompiler.Prepare(d,new[]{Card()}).Success);
            d.intentMode=BattleIntentMode.ByTurn;d.fallbackIntent=null;Assert.IsFalse(BattleCompiler.Prepare(d,new[]{Card()}).Success);
        }
        [Test]public void ResultAndSnapshotCannotMutateEngine()
        {
            var e=Engine(Challenge(),new[]{Card()});var s=e.Snapshot();
            Assert.Throws<NotSupportedException>(()=>((IList<CardInstance>)s.Cards).Clear());
            Assert.Throws<NotSupportedException>(()=>((IDictionary<PlayerStatKey,int>)s.LegacyStats)[PlayerStatKey.Energy]=0);
            e.Dispose();RejectUnchanged(e,BattleFailure.WrongPhase,()=>e.TryEndTurn(R(e)));
        }
        [Test]public void OpeningItemsUseTheirTimingAndRecordIsolatedSource()
        {
            var item=Data<ItemData>();item.itemId="passive";item.itemType=ItemType.Passive;item.triggerTiming=ItemTriggerTiming.OnBossStart;
            item.effects.Add(E(CardEffectType.AddPlayerStat,20,PlayerStatKey.MaxEnergy));item.effects.Add(E(CardEffectType.GainEnergy,20));
            var challenge=Challenge(BattleRulesVersion.Legacy);challenge.isBoss=true;var player=new PlayerState();
            var p=BattleCompiler.Prepare(challenge,new[]{Card()},passiveItems:new[]{item});Assert.IsTrue(p.Success);
            var engine=new BattleEngine(p.Definition,player,"session","battle",new BattleRandom(2));
            Assert.AreEqual(80,engine.Snapshot().LegacyStats[PlayerStatKey.Energy]);Assert.AreEqual(60,player.energy);
            Assert.IsTrue(engine.Snapshot().Events.Any(e=>e.Source=="passive"&&e.Field=="MaxEnergy"));
            challenge.isBoss=false;p=BattleCompiler.Prepare(challenge,new[]{Card()},passiveItems:new[]{item});Assert.AreEqual(0,p.Definition.OpeningEffects.Count);
        }
        [Test]public void PreviewAndExecutionReportSameActualCapacityAndClamping()
        {
            var c=Card(E(CardEffectType.AddChallengeProgress,200),E(CardEffectType.GainBlock,7));var e=Engine(Challenge(),new[]{c});
            var preview=e.GetCardPreview(First(e));var receipt=e.TryPlayCard(R(e,First(e)));
            Assert.AreEqual(100,preview.ProgressChange);Assert.AreEqual(receipt.State.Progress,preview.ProgressChange);
            Assert.AreEqual(receipt.State.Block,preview.BlockChange);
            StringAssert.Contains("进度 +100",preview.Summary);
        }
        [Test]public void LoopIntentsSelectOrderedActionsWithoutRandomUse()
        {
            var d=Challenge();d.intentMode=BattleIntentMode.Loop;d.intents.Add(new ChallengeIntentData{intentId="a",effects=new List<CardEffectData>{E(CardEffectType.DamageBattleHp,2)}});
            d.intents.Add(new ChallengeIntentData{intentId="b",effects=new List<CardEffectData>{E(CardEffectType.DamageBattleHp,3)}});
            var e=Engine(d,new[]{Card()});Assert.AreEqual("a",e.Snapshot().Intent.Id);int random=e.Snapshot().RandomDraws;
            e.TryEndTurn(R(e));Assert.AreEqual("b",e.Snapshot().Intent.Id);e.TryEndTurn(R(e));Assert.AreEqual("a",e.Snapshot().Intent.Id);
            Assert.AreEqual(55,e.Snapshot().Hp);Assert.AreEqual(random,e.Snapshot().RandomDraws);
        }
        [Test]public void LegacyEntryCannotAccidentallyInterpretNewRulesOrSchema()
        {
            var c=Card(E(CardEffectType.AddChallengeProgress,8));var d=Challenge();
            Assert.IsTrue(BattleContentValidator.Validate(d,new[]{c}).HasErrors);
            Assert.IsTrue(BattleCompiler.Prepare(d,new[]{c}).Success);
        }
    }
}
