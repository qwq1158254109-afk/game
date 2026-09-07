using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class GameBootstrapperTests
    {
        private GameObject gameObject;
        private GameBootstrapper bootstrapper;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("GameBootstrapperTests");
            bootstrapper = gameObject.AddComponent<GameBootstrapper>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void CreateSessionFallsBackToMvpSampleData()
        {
            GameSessionState state = bootstrapper.CreateSession();

            Assert.GreaterOrEqual(state.deckState.masterDeck.Count, 6);
            Assert.GreaterOrEqual(state.itemInventory.ownedItems.Count, 2);
        }

        [Test]
        public void GetStageSequenceFallsBackToThreeStages()
        {
            List<StageData> stages = bootstrapper.GetStageSequence();

            Assert.AreEqual(3, stages.Count);
            Assert.AreEqual("stage_01_adaptation", stages[0].stageId);
        }

        [Test]
        public void AssignedBootstrapperDataOverridesFallbackData()
        {
            GameBootstrapperData data = ScriptableObject.CreateInstance<GameBootstrapperData>();
            CardData card = ScriptableObject.CreateInstance<CardData>();
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            StageData stage = ScriptableObject.CreateInstance<StageData>();

            card.cardId = "card_test";
            item.itemId = "item_test";
            stage.stageId = "stage_test";
            data.starterDeck.Add(card);
            data.starterItems.Add(item);
            data.stages.Add(stage);
            data.firstStage = stage;
            bootstrapper.bootstrapperData = data;

            GameSessionState state = bootstrapper.CreateSession();
            List<StageData> stages = bootstrapper.GetStageSequence();

            Assert.AreEqual(1, state.deckState.masterDeck.Count);
            Assert.AreEqual(card, state.deckState.masterDeck[0]);
            Assert.AreEqual(1, state.itemInventory.ownedItems.Count);
            Assert.AreEqual(item, state.itemInventory.ownedItems[0]);
            Assert.AreEqual(1, stages.Count);
            Assert.AreEqual(stage, bootstrapper.GetFirstStage(stages));

            Object.DestroyImmediate(data);
            Object.DestroyImmediate(card);
            Object.DestroyImmediate(item);
            Object.DestroyImmediate(stage);
        }
    }
}
