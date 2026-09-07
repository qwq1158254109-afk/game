using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public sealed class TimeActionControllerTests
    {
        private GameObject gameObject;
        private GameSessionState sessionState;
        private TimeActionController controller;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("TimeActionControllerTests");
            sessionState = new GameSessionState();
            controller = gameObject.AddComponent<TimeActionController>();
            controller.SetSessionState(sessionState);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void NewSession_StartsOnDayOneMorningWithTwelveDailyPoints()
        {
            Assert.AreEqual(1, controller.State.currentDay);
            Assert.AreEqual(TimePeriod.Morning, controller.State.currentPeriod);
            Assert.AreEqual(3, controller.State.currentActionPoints);
            Assert.AreEqual(12, controller.State.RemainingDailyActionPoints);
        }

        [Test]
        public void ConsumeAction_CanCrossIntoNextPeriod()
        {
            Assert.IsTrue(controller.TryConsumeAction(1, out _));
            Assert.IsTrue(controller.TryConsumeAction(3, out TimeActionPreview result));

            Assert.AreEqual(TimePeriod.Afternoon, result.periodAfter);
            Assert.AreEqual(2, result.actionPointsAfter);
            Assert.AreEqual(8, controller.State.RemainingDailyActionPoints);
        }

        [Test]
        public void ConsumeAction_IsAtomicWhenDailyPointsAreInsufficient()
        {
            controller.State.currentPeriod = TimePeriod.Night;
            controller.State.currentActionPoints = 1;

            Assert.IsFalse(controller.TryConsumeAction(2, out TimeActionPreview result));
            Assert.AreEqual("今日剩余行动值不足。", result.message);
            Assert.AreEqual(TimePeriod.Night, controller.State.currentPeriod);
            Assert.AreEqual(1, controller.State.currentActionPoints);
        }

        [Test]
        public void HomeSleep_AdvancesOnePeriodAndDiscardsUnusedCurrentPoints()
        {
            controller.State.currentActionPoints = 2;
            SleepResult result = controller.Sleep(SleepLocation.Home);

            Assert.AreEqual(2, result.discardedActionPoints);
            Assert.AreEqual(TimePeriod.Afternoon, controller.State.currentPeriod);
            Assert.AreEqual(3, controller.State.currentActionPoints);
            Assert.AreEqual(1, controller.State.currentDay);
        }

        [Test]
        public void NightSleep_StartsNextDay()
        {
            controller.State.currentPeriod = TimePeriod.Night;
            controller.State.currentActionPoints = 0;
            SleepResult result = controller.Sleep(SleepLocation.Home);

            Assert.IsTrue(result.startedNewDay);
            Assert.AreEqual(2, controller.State.currentDay);
            Assert.AreEqual(TimePeriod.Morning, controller.State.currentPeriod);
            Assert.AreEqual(3, controller.State.currentActionPoints);
        }

        [Test]
        public void BusStopSleep_AppliesMoodPenalty()
        {
            int moodBefore = sessionState.playerState.mood;
            SleepResult result = controller.Sleep(SleepLocation.BusStop);

            Assert.AreEqual(-TimeActionController.BusStopMoodPenalty, result.moodChange);
            Assert.AreEqual(moodBefore - TimeActionController.BusStopMoodPenalty, sessionState.playerState.mood);
        }
    }
}
