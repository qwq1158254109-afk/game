using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class BattleP1Tests
    {
        private GameObject root;
        private ChallengeController battle;
        private StageController stage;
        private readonly List<Object> assets = new List<Object>();
        private T Data<T>() where T : ScriptableObject { var value = ScriptableObject.CreateInstance<T>(); assets.Add(value); return value; }
        [SetUp] public void SetUp()
        {
            root = new GameObject("BattleP1Tests");
            battle = root.AddComponent<ChallengeController>(); stage = root.AddComponent<StageController>();
            stage.challengeController = battle; stage.SetSessionState(new GameSessionState());
        }
        [TearDown] public void TearDown() { Object.DestroyImmediate(root); foreach (var asset in assets) Object.DestroyImmediate(asset); assets.Clear(); }
        private ChallengeData Challenge(bool boss = false)
        {
            var c = Data<ChallengeData>(); c.challengeId = "test_challenge"; c.targetProgress = 100; c.maxTurns = 3; c.startingEnergy = 3; c.startingHandSize = 5; c.isBoss = boss; return c;
        }
        private StageData Stage(ChallengeData c)
        {
            var s = Data<StageData>(); s.stageId = "test_stage"; s.timeLimit = 2; s.bossChallenge = c;
            s.clearStageRewards.Add(new RewardData { rewardType = RewardType.AddMoney, value = 7 });
            stage.StartStage(s); return s;
        }
        [Test] public void RepeatedResultAndStageCallsAwardChallengeAndStageOnce()
        {
            var c = Challenge(true); Stage(c); c.bossClearRewards.Add(new RewardData { rewardType = RewardType.AddMoney, value = 11 });
            battle.StartChallenge(c);
            for (int i = 0; i < 3; i++) { battle.CompleteChallenge(true); stage.CompleteBoss(true); }
            Assert.AreEqual(118, battle.sessionState.playerState.money);
            Assert.AreEqual(1, battle.sessionState.stageState.clearedStageCount);
            Assert.AreEqual(BattleResultConsumption.Applied, battle.Result.Consumption);
        }
        [Test] public void NormalChallengeDoesNotCompleteCurrentStage()
        {
            Stage(Challenge(true)); var c = Challenge(); battle.StartChallenge(c); battle.CompleteChallenge(true);
            Assert.AreEqual(0, battle.sessionState.stageState.clearedStageCount);
            Assert.AreEqual(100, battle.sessionState.playerState.money);
        }
        [Test] public void OpeningPassiveCompletesWithoutPlayerInput()
        {
            var c = Challenge(); c.targetProgress = 5;
            var item = Data<ItemData>(); item.itemType = ItemType.Passive; item.triggerTiming = ItemTriggerTiming.OnBattleStart;
            item.effects.Add(new CardEffectData { effectType = CardEffectType.AddChallengeProgress, value = 6 });
            battle.sessionState.itemInventory.AddItem(item); battle.StartChallenge(c);
            Assert.IsTrue(battle.runtimeState.isSuccess); Assert.AreEqual(6, battle.runtimeState.currentProgress);
            Assert.IsFalse(battle.CanPlayCard(null));
        }
        [Test] public void IntentSnapshotUsesCurrentTurnAndAllLegacyEffects()
        {
            var c = Challenge();
            c.intents.Add(new ChallengeIntentData { triggerTurn = 1, effects = new List<CardEffectData> { new CardEffectData { effectType = CardEffectType.AddPressure, value = 2 } } });
            c.intents.Add(new ChallengeIntentData { triggerTurn = 1, effects = new List<CardEffectData> { new CardEffectData { effectType = CardEffectType.LoseEnergy, value = 3 } } });
            c.intents.Add(new ChallengeIntentData { triggerTurn = 2, effects = new List<CardEffectData> { new CardEffectData { effectType = CardEffectType.AddPressure, value = 9 } } });
            battle.StartChallenge(c); var snapshot = battle.Intent; Assert.AreEqual(2, snapshot.Damage); Assert.AreEqual(2, snapshot.Effects.Count);
            c.intents[0].effects[0].value = 99; // Execution uses the already shown snapshot.
            battle.EndTurn(); Assert.AreEqual(58, battle.runtimeState.playerHp); Assert.AreEqual(22, battle.sessionState.playerState.pressure);
            Assert.AreEqual(57, battle.sessionState.playerState.energy); Assert.AreEqual(9, battle.Intent.Damage);
        }
        [Test] public void IllegalPlayAndInvalidStartDoNotSpendOrMoveCards()
        {
            var card = Data<CardData>(); card.cost = 4; battle.sessionState.deckState.masterDeck.Add(card);
            var c = Challenge(); battle.StartChallenge(c);
            Assert.IsFalse(battle.TryPlayCard(card, true, out _)); Assert.AreEqual(3, battle.runtimeState.currentEnergy);
            Assert.AreEqual(1, battle.deckController.deckState.hand.Count);
            var oldResult = battle.Result; c.maxTurns = 0;
            Assert.IsFalse(battle.TryStartChallenge(c, out _)); Assert.AreSame(oldResult, battle.Result);
        }
        [Test] public void UnsupportedNewEffectsAreBlockedWithFieldLocation()
        {
            var card = Data<CardData>(); card.cardId = "new_card";
            card.effects.Add(new CardEffectData { effectType = CardEffectType.AddTemporaryBuff });
            var validation = BattleContentValidator.ValidateCard(card);
            Assert.IsTrue(validation.HasErrors); StringAssert.Contains("effects[0]", validation.ToString());
        }
        [Test] public void LegacyDrawHasNoTenCardCapAndProgressCanOverflow()
        {
            var card = Data<CardData>(); card.effects.Add(new CardEffectData { effectType = CardEffectType.AddChallengeProgress, value = 120 });
            for (int i = 0; i < 12; i++) battle.sessionState.deckState.masterDeck.Add(card);
            var c = Challenge(); c.startingHandSize = 12; battle.StartChallenge(c);
            Assert.AreEqual(12, battle.deckController.deckState.hand.Count); battle.PlayCard(card);
            Assert.AreEqual(120, battle.runtimeState.currentProgress);
        }
        [Test] public void FinalTurnFailsWithoutEnemyAttack()
        {
            var c = Challenge(); c.maxTurns = 1; battle.StartChallenge(c); battle.EndTurn();
            Assert.IsTrue(battle.runtimeState.isComplete); Assert.IsFalse(battle.runtimeState.isSuccess); Assert.AreEqual(60, battle.runtimeState.playerHp);
        }
        [Test] public void FaultedRewardCannotBeReplayed()
        {
            var c = Challenge(); c.successRewards.Add(new RewardData { rewardType = RewardType.AddMoney, value = 4 });
            c.successRewards.Add(new RewardData { rewardType = (RewardType)999 });
            battle.StartChallenge(c); battle.CompleteChallenge(true); battle.CompleteChallenge(true);
            Assert.AreEqual(104, battle.sessionState.playerState.money); Assert.AreEqual(BattleResultConsumption.Faulted, battle.Result.Consumption);
            Assert.IsFalse(battle.sessionState.BattleResults.Complete(battle.Result, true)); Assert.AreEqual(104, battle.sessionState.playerState.money);
        }
        [Test] public void OldBattleRecordCannotSettleNewAttempt()
        {
            var c = Challenge(); c.successRewards.Add(new RewardData { rewardType = RewardType.AddMoney, value = 4 });
            battle.StartChallenge(c); var old = battle.Result; battle.StartChallenge(c);
            Assert.IsFalse(battle.sessionState.BattleResults.Complete(old, true)); Assert.AreEqual(100, battle.sessionState.playerState.money);
        }
        [Test] public void StaleControllerCannotSpendOrEndNewBattle()
        {
            var card = Data<CardData>(); card.cost = 1;
            battle.sessionState.deckState.masterDeck.Add(card);
            var c = Challenge(); battle.StartChallenge(c);
            battle.sessionState.BattleResults.Begin(c);
            Assert.IsFalse(battle.TryPlayCard(card, true, out _)); battle.EndTurn();
            Assert.AreEqual(3, battle.runtimeState.currentEnergy); Assert.AreEqual(1, battle.runtimeState.currentTurn);
            Assert.AreEqual(1, battle.deckController.deckState.hand.Count);
        }
        [Test] public void PartiallyAppliedResultCannotBeSaved()
        {
            var c = Challenge(); c.successRewards.Add(new RewardData { rewardType = (RewardType)999 });
            battle.StartChallenge(c); battle.CompleteChallenge(true);
            var exception = Assert.Throws<System.InvalidOperationException>(() => new UnityGameSaveSource(null).Capture());
            Assert.AreEqual("SAVE_BATTLE_RESULT_INCOMPLETE", exception.Message);
        }
        [Test] public void SeparateResultViewsShareOneReturnRequest()
        {
            var c = Challenge(true); Stage(c); battle.StartChallenge(c); battle.CompleteChallenge(true);
            Assert.IsTrue(battle.sessionState.BattleResults.TryRequestReturn(battle.Result));
            Assert.IsFalse(battle.sessionState.BattleResults.TryRequestReturn(battle.Result));
        }
    }
}
