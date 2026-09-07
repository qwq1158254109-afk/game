using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object=UnityEngine.Object;

namespace StudentGrowth.Tests
{
    public class BattleP3Tests
    {
        private readonly List<Object> owned=new List<Object>();
        private GameSessionState session;
        private GameApplicationRuntime app;
        private object previousApp;
        private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
        private T Asset<T>()where T:ScriptableObject{var a=ScriptableObject.CreateInstance<T>();owned.Add(a);return a;}
        private T Component<T>()where T:Component{var go=new GameObject("P3Test");go.SetActive(false);owned.Add(go);return go.AddComponent<T>();}
        [SetUp]public void Setup()
        {
            var field=typeof(GameApplicationRuntime).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
            previousApp=field.GetValue(null);app=Component<GameApplicationRuntime>();field.SetValue(null,app);app.Lifetime.Begin("test");
            session=new GameSessionState();var stage=Asset<StageData>();stage.stageId="stage_test";session.stageState.currentStage=stage;session.stageState.remainingTime=4;
        }
        [TearDown]public void Cleanup()
        {
            session.BattleFlow.CancelSession();
            typeof(GameApplicationRuntime).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic).SetValue(null,previousApp);
            foreach(var obj in owned)Object.DestroyImmediate(obj);owned.Clear();
        }
        private ChallengeData Challenge(bool boss=false)
        {
            var c=Asset<ChallengeData>();c.challengeId="test";c.displayName="Test";c.targetProgress=1;c.maxTurns=1;c.startingEnergy=3;c.startingHandSize=1;c.isBoss=boss;
            if(boss){session.stageState.currentStage.bossChallenge=c;session.stageState.bossUnlocked=true;}
            return c;
        }
        private CardData Card(params CardEffectData[] effects)
        {var c=Asset<CardData>();c.cardId="card";c.effects.AddRange(effects);session.deckState.masterDeck.Add(c);return c;}
        private static CardEffectData E(PlayerStatKey stat,int value)=>new CardEffectData{effectType=CardEffectType.AddPlayerStat,targetStat=stat,value=value};
        private EngineBattleResult Finish(ChallengeData challenge,bool win,CardData card=null)
        {
            if(card==null)card=Card();
            if(win)card.effects.Add(new CardEffectData{effectType=CardEffectType.AddChallengeProgress,value=10});
            var p=BattleCompiler.Prepare(challenge,new[]{card});Assert.IsTrue(p.Success,string.Join("\n",p.Errors));
            var engine=new BattleEngine(p.Definition,session.playerState,GameApplicationRuntime.CurrentRuntimeSessionId,"battle",new BattleRandom(4));
            var snapshot=engine.Snapshot();var instance=snapshot.InPile(BattlePile.Hand)[0];
            string target=instance.Definition.Target==CardTargetType.SingleEnemy?"challenge:test":instance.Definition.Target==CardTargetType.Self?BattleEngine.SelfTargetId:null;
            var request=new BattleRequest(snapshot.RuntimeSessionId,snapshot.BattleId,snapshot.Revision,"finish",instance.InstanceId,target);
            if(win)engine.TryPlayCard(request);else engine.TryEndTurn(request);
            Assert.NotNull(engine.Result);return engine.Result;
        }
        private BattleResultRecord Record(ChallengeData challenge)
        {var r=session.BattleResults.Begin(challenge);typeof(BattleResultRecord).GetProperty("BattleId").SetValue(r,"battle");return r;}
        [Test]public void CombinedCapAndEnergyAreCommittedWithoutIntermediateClamp()
        {
            var c=Challenge();var card=Card(E(PlayerStatKey.MaxEnergy,20),E(PlayerStatKey.Energy,20));var terminal=Finish(c,true,card);
            var before=new BattleCheckpoint(session);var player=session.playerState;var r=Record(c);
            Assert.IsTrue(session.BattleResults.CompleteEngine(r,terminal,before,null));Assert.AreSame(player,session.playerState);
            Assert.AreEqual(80,player.maxEnergy);Assert.AreEqual(80,player.energy);Assert.AreEqual(0,session.stageState.clearedStageCount);
        }
        [Test]public void PartialCommitRollsBackThenRetriesFrozenRewardPlanExactlyOnce()
        {
            var c=Challenge();c.successRewards.Add(new RewardData{rewardType=RewardType.AddMoney,value=7});var terminal=Finish(c,true);var before=new BattleCheckpoint(session);var r=Record(c);
            typeof(BattleResultConsumer).GetField("CommitProbe",Hidden).SetValue(session.BattleResults,(Action<string>)(point=>{if(point=="AfterWrite")throw new Exception("injected");}));
            Assert.IsFalse(session.BattleResults.CompleteEngine(r,terminal,before,null));Assert.IsTrue(before.Matches(session));Assert.AreEqual(BattleResultConsumption.Faulted,r.Consumption);
            c.successRewards[0].value=999;
            typeof(BattleResultConsumer).GetField("CommitProbe",Hidden).SetValue(session.BattleResults,null);
            Assert.IsTrue(session.BattleResults.CompleteEngine(r,terminal,before,null));Assert.AreEqual(107,session.playerState.money);
            Assert.IsTrue(session.BattleResults.CompleteEngine(r,terminal,before,null));Assert.AreEqual(107,session.playerState.money);
        }
        [Test]public void ExternalMutationRejectsWholeCommit()
        {
            var c=Challenge();var terminal=Finish(c,true);var baseline=new BattleCheckpoint(session);var r=Record(c);session.playerState.money++;
            Assert.IsFalse(session.BattleResults.CompleteEngine(r,terminal,baseline,null));Assert.AreEqual(101,session.playerState.money);Assert.IsNull(r.Plan);
        }
        [TestCase(true)][TestCase(false)]public void OrdinaryOutcomeNeverAdvancesStage(bool win)
        {
            var c=Challenge();var terminal=Finish(c,win);var before=new BattleCheckpoint(session);var r=Record(c);
            Assert.IsTrue(session.BattleResults.CompleteEngine(r,terminal,before,null));Assert.AreEqual(0,session.stageState.clearedStageCount);Assert.IsFalse(session.endingReached);
        }
        [Test]public void FinalBossLossCanRetryAndWinWithoutLosingCompletionKey()
        {
            var c=Challenge(true);session.stageState.currentStage.isFinalStage=true;
            var loss=Finish(c,false);var before=new BattleCheckpoint(session);Assert.IsTrue(session.BattleResults.CompleteEngine(Record(c),loss,before,null));
            Assert.IsFalse(session.endingReached);Assert.IsFalse(session.BattleResults.IsStageCleared(session.stageState.currentStage));
            session.deckState.masterDeck.Clear();var win=Finish(c,true);before=new BattleCheckpoint(session);
            Assert.IsTrue(session.BattleResults.CompleteEngine(Record(c),win,before,null));Assert.AreEqual(1,session.stageState.defeatedBossCount);Assert.IsTrue(session.endingReached);
        }
        [TestCase(BattleRewardTiming.Participation,true,5)][TestCase(BattleRewardTiming.Participation,false,5)]
        [TestCase(BattleRewardTiming.Victory,false,0)][TestCase(BattleRewardTiming.Victory,true,5)]
        [TestCase(BattleRewardTiming.Failure,true,0)][TestCase(BattleRewardTiming.Failure,false,5)]
        [TestCase(BattleRewardTiming.Stage,true,0)]
        public void FixedRewardTimingIsExplicit(BattleRewardTiming timing,bool win,int gain)
        {
            var activity=Asset<ActivityData>();activity.battleRewardTiming=timing;activity.fixedRewards.Add(new RewardData{rewardType=RewardType.AddMoney,value=5});
            var c=Challenge();var terminal=Finish(c,win);var before=new BattleCheckpoint(session);
            Assert.IsTrue(session.BattleResults.CompleteEngine(Record(c),terminal,before,activity));Assert.AreEqual(100+gain,session.playerState.money);
        }
        [Test]public void CheckpointRestoresDictionariesListsTimeAndSamePlayer()
        {
            session.locationState.actionUseCount["desk"]=1;var before=new BattleCheckpoint(session);var player=session.playerState;
            session.locationState.actionUseCount["desk"]=9;session.locationState.locationFlags["x"]=true;session.timeActionState.currentDay++;
            session.playerState.money=1;session.stageState.bossUnlocked=true;session.clearedStageIds.Add("bad");
            before.Restore(session);Assert.IsTrue(before.Matches(session));Assert.AreSame(player,session.playerState);
        }
        [Test]public void TimeNotificationsAreDiscardedAndNextActionIsClean()
        {
            var time=Component<TimeActionController>();time.SetSessionState(session);int calls=0;time.ActionConsumed+=_=>calls++;
            var before=new BattleCheckpoint(session);using(time.DeferNotifications()){Assert.IsTrue(time.TryConsumeAction(1,out _));Assert.AreEqual(0,calls);before.Restore(session);}
            Assert.AreEqual(0,calls);Assert.IsTrue(time.TryConsumeAction(1,out _));Assert.AreEqual(1,calls);
        }
        [TestCase("Prepared")][TestCase("CostConsumed")][TestCase("ViewReady")]
        public void StartupFailureRestoresCostAndPendingEvents(string point)
        {
            var c=Challenge();Card();var activity=Asset<ActivityData>();activity.timeCost=1;
            var stage=Component<StageController>();stage.SetSessionState(session);var controller=Component<ChallengeController>();controller.SetSessionState(session);stage.challengeController=controller;
            var time=Component<TimeActionController>();time.SetSessionState(session);int calls=0;time.StateChanged+=()=>calls++;
            var before=new BattleCheckpoint(session);typeof(BattleSessionFlow).GetField("StartupProbe",Hidden).SetValue(session.BattleFlow,(Action<string>)(p=>{if(p==point)throw new Exception("injected");}));
            Assert.IsFalse(session.BattleFlow.Start("req",c,activity,stage,time,controller,BattleReturnContext.Capture(()=>{}),1,out _,false));
            Assert.IsTrue(before.Matches(session));Assert.AreEqual(0,calls);Assert.IsFalse(session.BattleFlow.PendingBoss);Assert.IsFalse(session.BattleFlow.BlocksSave);
        }
        [Test]public void OldSessionCannotCommit()
        {
            var c=Challenge();var terminal=Finish(c,true);var before=new BattleCheckpoint(session);var r=Record(c);app.Lifetime.Begin("other");
            Assert.IsFalse(session.BattleResults.CompleteEngine(r,terminal,before,null));Assert.IsTrue(before.Matches(session));
        }
        [Test]public void LoanDeckIsUsedWithoutChangingPermanentOwnershipAndInvalidLoanCostsNothing()
        {
            var c=Challenge();var original=Card();var loan=Asset<CardData>();loan.cardId="loan";
            loan.effectSchemaVersion=CardEffectSchemaVersion.Unified;loan.effects.Add(new CardEffectData{effectType=CardEffectType.AddChallengeProgress,value=1});
            c.loanDeck.Add(loan);var activity=Asset<ActivityData>();activity.timeCost=1;
            var stage=Component<StageController>();stage.SetSessionState(session);var controller=Component<ChallengeController>();controller.SetSessionState(session);
            var time=Component<TimeActionController>();time.SetSessionState(session);
            loan.cost=-1;var baseline=new BattleCheckpoint(session);
            Assert.IsFalse(session.BattleFlow.Start("bad",c,activity,stage,time,controller,BattleReturnContext.Capture(()=>{}),1,out _,false));Assert.IsTrue(baseline.Matches(session));
            loan.cost=0;Assert.IsTrue(session.BattleFlow.Start("good",c,activity,stage,time,controller,BattleReturnContext.Capture(()=>{}),1,out var error,false),error);
            Assert.AreEqual("loan",controller.SandboxEngine.Snapshot().Cards[0].DefinitionId);Assert.AreEqual(1,session.deckState.masterDeck.Count);Assert.AreSame(original,session.deckState.masterDeck[0]);
            var snapshot=controller.SandboxEngine.Snapshot();controller.PlayEngineCard(new BattleRequest(snapshot.RuntimeSessionId,snapshot.BattleId,snapshot.Revision,"play","card:0"));
            session.BattleFlow.ProcessResult();Assert.IsTrue(session.BattleFlow.TryReturn());Assert.AreEqual(1,session.deckState.masterDeck.Count);Assert.AreSame(original,session.deckState.masterDeck[0]);
        }
        [Test]public void LegacyCompletionCannotAwardAnEngineOwnedBattle()
        {
            var c=Challenge(true);c.bossClearRewards.Add(new RewardData{rewardType=RewardType.AddMoney,value=50});
            var r=Record(c);typeof(BattleResultRecord).GetProperty("EngineOwned").SetValue(r,true);
            Assert.IsFalse(session.BattleResults.Complete(r,true));Assert.IsFalse(session.BattleResults.CompleteStage(r.Stage,true));
            Assert.AreEqual(100,session.playerState.money);Assert.AreEqual(0,session.stageState.clearedStageCount);
        }
        [Test]public void NotificationFailureDoesNotRollbackOrRepeatAwards()
        {
            var c=Challenge();c.successRewards.Add(new RewardData{rewardType=RewardType.AddMoney,value=5});var terminal=Finish(c,true);var baseline=new BattleCheckpoint(session);var r=Record(c);
            int calls=0;session.BattleResults.Applied+=_=>throw new Exception("notification probe");session.BattleResults.Applied+=_=>calls++;
            Assert.IsTrue(session.BattleResults.CompleteEngine(r,terminal,baseline,null));Assert.AreEqual(105,session.playerState.money);Assert.AreEqual(1,calls);
            Assert.IsTrue(session.BattleResults.CompleteEngine(r,terminal,baseline,null));Assert.AreEqual(1,calls);
        }
        [Test]public void StageTransitionAndEntryRewardsRollbackTogetherAndStayFrozen()
        {
            var c=Challenge(true);var next=Asset<StageData>();next.stageId="next";next.timeLimit=7;
            next.enterStageRewards.Add(new RewardData{rewardType=RewardType.AddMoney,value=12});
            var terminal=Finish(c,true);var before=new BattleCheckpoint(session);var r=Record(c);
            typeof(BattleResultRecord).GetField("NextStage",Hidden).SetValue(r,next);
            typeof(BattleResultConsumer).GetField("CommitProbe",Hidden).SetValue(session.BattleResults,(Action<string>)(p=>{if(p=="AfterWrite")throw new Exception("probe");}));
            Assert.IsFalse(session.BattleResults.CompleteEngine(r,terminal,before,null));Assert.IsTrue(before.Matches(session));
            next.enterStageRewards[0].value=500;next.timeLimit=99;
            typeof(BattleResultConsumer).GetField("CommitProbe",Hidden).SetValue(session.BattleResults,null);
            Assert.IsTrue(session.BattleResults.CompleteEngine(r,terminal,before,null));Assert.AreSame(next,session.stageState.currentStage);
            Assert.AreEqual(112,session.playerState.money);Assert.AreEqual(7,session.stageState.remainingTime);Assert.AreEqual(1,session.stageState.clearedStageCount);
        }
        [Test]public void MixedParticipationAndVictoryRewardsAreAppliedPerEntry()
        {
            var activity=Asset<ActivityData>();activity.activityId="exercise";
            activity.fixedRewards.Add(new RewardData{rewardType=RewardType.AddMoney,value=3,battleTiming=BattleRewardTiming.Participation});
            activity.fixedRewards.Add(new RewardData{rewardType=RewardType.AddMoney,value=50,battleTiming=BattleRewardTiming.Victory});
            var c=Challenge();var terminal=Finish(c,false);var baseline=new BattleCheckpoint(session);
            Assert.IsTrue(session.BattleResults.CompleteEngine(Record(c),terminal,baseline,activity));Assert.AreEqual(103,session.playerState.money);
            Assert.AreEqual(1,session.locationState.actionUseCount["battle:exercise:defeat"]);
        }
    }
}
