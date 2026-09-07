using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class ItemEffectResolverTests
    {
        private ItemEffectResolver itemEffectResolver;

        [SetUp]
        public void SetUp()
        {
            itemEffectResolver = new ItemEffectResolver();
        }

        [Test]
        public void ConsumableUseAppliesEffectAndRemovesItem()
        {
            GameSessionState sessionState = new GameSessionState();
            sessionState.playerState.SetStat(PlayerStatKey.Energy, 20);
            ItemData item = CreateItem(ItemType.Consumable, ItemTriggerTiming.OnUse, CardEffectType.GainEnergy, PlayerStatKey.Energy, 15);
            item.canUseInStageScreen = true;
            item.consumedOnUse = true;
            sessionState.itemInventory.AddItem(item);

            ItemUseResult result = itemEffectResolver.UseItem(item, sessionState, inBattle: false);

            Assert.IsTrue(result.success);
            Assert.IsTrue(result.consumed);
            Assert.AreEqual(35, sessionState.playerState.energy);
            Assert.IsFalse(sessionState.itemInventory.ContainsItem(item));
        }

        [Test]
        public void PassiveBattleStartItemAppliesAtBattleStart()
        {
            GameSessionState sessionState = new GameSessionState();
            ChallengeRuntimeState challengeState = new ChallengeRuntimeState();
            ItemData item = CreateItem(ItemType.Passive, ItemTriggerTiming.OnBattleStart, CardEffectType.AddChallengeProgress, PlayerStatKey.Chinese, 10);
            sessionState.itemInventory.AddItem(item);

            int appliedCount = itemEffectResolver.ApplyPassiveItems(ItemTriggerTiming.OnBattleStart, sessionState, challengeState);

            Assert.AreEqual(1, appliedCount);
            Assert.AreEqual(10, challengeState.currentProgress);
        }

        [Test]
        public void PassiveBossStartItemAppliesWhenChallengeStartsAsBoss()
        {
            GameObject gameObject = new GameObject("ItemEffectResolverTests");
            ChallengeController challengeController = gameObject.AddComponent<ChallengeController>();
            GameSessionState sessionState = new GameSessionState();
            ItemData item = CreateItem(ItemType.Passive, ItemTriggerTiming.OnBossStart, CardEffectType.AddChallengeProgress, PlayerStatKey.Chinese, 12);
            sessionState.itemInventory.AddItem(item);
            ChallengeData challengeData = ScriptableObject.CreateInstance<ChallengeData>();
            challengeData.isBoss = true;
            challengeData.targetProgress = 100;
            challengeData.maxTurns = 3;
            challengeData.startingEnergy = 3;
            challengeData.startingHandSize = 0;

            challengeController.SetSessionState(sessionState);
            challengeController.StartChallenge(challengeData);

            Assert.AreEqual(12, challengeController.runtimeState.currentProgress);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void StageOnlyItemsAreCleared()
        {
            GameSessionState sessionState = new GameSessionState();
            ItemData stageOnlyItem = ScriptableObject.CreateInstance<ItemData>();
            stageOnlyItem.itemType = ItemType.StageOnly;
            ItemData passiveItem = ScriptableObject.CreateInstance<ItemData>();
            passiveItem.itemType = ItemType.Passive;
            sessionState.itemInventory.AddItem(stageOnlyItem);
            sessionState.itemInventory.AddItem(passiveItem);

            int removedCount = itemEffectResolver.ClearStageOnlyItems(sessionState);

            Assert.AreEqual(1, removedCount);
            Assert.IsFalse(sessionState.itemInventory.ContainsItem(stageOnlyItem));
            Assert.IsTrue(sessionState.itemInventory.ContainsItem(passiveItem));
        }

        [Test]
        public void StageControllerClearsStageOnlyItemsWhenMovingToNextStage()
        {
            GameObject gameObject = new GameObject("StageOnlyMoveTests");
            StageController stageController = gameObject.AddComponent<StageController>();
            StageData firstStage = ScriptableObject.CreateInstance<StageData>();
            firstStage.stageId = "stage_first";
            firstStage.nextStageId = "stage_second";
            StageData secondStage = ScriptableObject.CreateInstance<StageData>();
            secondStage.stageId = "stage_second";
            stageController.stageSequence.Add(firstStage);
            stageController.stageSequence.Add(secondStage);
            ItemData stageOnlyItem = ScriptableObject.CreateInstance<ItemData>();
            stageOnlyItem.itemType = ItemType.StageOnly;
            stageController.sessionState.itemInventory.AddItem(stageOnlyItem);

            stageController.StartStage(firstStage);
            stageController.MoveToNextStage();

            Assert.AreEqual(secondStage, stageController.sessionState.stageState.currentStage);
            Assert.IsFalse(stageController.sessionState.itemInventory.ContainsItem(stageOnlyItem));
            Object.DestroyImmediate(gameObject);
        }

        private static ItemData CreateItem(
            ItemType itemType,
            ItemTriggerTiming triggerTiming,
            CardEffectType effectType,
            PlayerStatKey targetStat,
            int value)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.itemType = itemType;
            item.triggerTiming = triggerTiming;
            item.effects.Add(new CardEffectData
            {
                effectType = effectType,
                targetStat = targetStat,
                value = value
            });
            return item;
        }
    }
}
