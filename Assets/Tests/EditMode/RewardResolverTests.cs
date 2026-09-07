using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class RewardResolverTests
    {
        [Test]
        public void AddPlayerStatIncreasesTargetStat()
        {
            GameSessionState sessionState = new GameSessionState();
            RewardResolver resolver = new RewardResolver();

            resolver.ApplyReward(new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.Math,
                value = 5
            }, sessionState);

            Assert.AreEqual(40, sessionState.playerState.math);
        }

        [Test]
        public void ReducePressureLowersPressure()
        {
            GameSessionState sessionState = new GameSessionState();
            RewardResolver resolver = new RewardResolver();

            resolver.ApplyReward(new RewardData
            {
                rewardType = RewardType.ReducePressure,
                value = 7
            }, sessionState);

            Assert.AreEqual(13, sessionState.playerState.pressure);
        }

        [Test]
        public void AddCardAddsToMasterDeck()
        {
            GameSessionState sessionState = new GameSessionState();
            RewardResolver resolver = new RewardResolver();
            CardData card = ScriptableObject.CreateInstance<CardData>();

            resolver.ApplyReward(new RewardData
            {
                rewardType = RewardType.AddCard,
                card = card
            }, sessionState);

            Assert.Contains(card, sessionState.deckState.masterDeck);
        }

        [Test]
        public void AddItemAddsToInventory()
        {
            GameSessionState sessionState = new GameSessionState();
            RewardResolver resolver = new RewardResolver();
            ItemData item = ScriptableObject.CreateInstance<ItemData>();

            resolver.ApplyReward(new RewardData
            {
                rewardType = RewardType.AddItem,
                item = item
            }, sessionState);

            Assert.Contains(item, sessionState.itemInventory.ownedItems);
        }

        [Test]
        public void AddStageTimeIncreasesRemainingTime()
        {
            GameSessionState sessionState = new GameSessionState();
            sessionState.stageState.remainingTime = 2;
            RewardResolver resolver = new RewardResolver();

            resolver.ApplyReward(new RewardData
            {
                rewardType = RewardType.AddStageTime,
                value = 3
            }, sessionState);

            Assert.AreEqual(5, sessionState.stageState.remainingTime);
        }
    }
}
