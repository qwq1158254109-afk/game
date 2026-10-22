using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 2026-10-22 每日循环改造后：一天 3 格（早晨/下午/傍晚），夜晚只结算、不产出行动。
    /// 阶段预算仍是 6/6/8 次，所以阶段 = ceil(预算/3) = 2/2/3 天。
    /// </summary>
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
        public void NewSession_StartsOnDayOneMorningWithThreeDailyActions()
        {
            Assert.AreEqual(1, controller.State.currentDay);
            Assert.AreEqual(TimePeriod.Morning, controller.State.currentPeriod);
            Assert.AreEqual(1, controller.State.currentActionPoints);
            Assert.AreEqual(3, controller.State.RemainingDailyActionPoints);
        }

        [Test]
        public void ConsumeAction_WalksMorningAfternoonEvening()
        {
            Assert.IsTrue(controller.TryConsumeAction(1, out TimeActionPreview first));
            Assert.AreEqual(TimePeriod.Afternoon, first.periodAfter);
            Assert.AreEqual(2, first.remainingAfter);

            Assert.IsTrue(controller.TryConsumeAction(1, out TimeActionPreview second));
            Assert.AreEqual(TimePeriod.Evening, second.periodAfter);
            Assert.AreEqual(1, second.remainingAfter);

            Assert.IsTrue(controller.TryConsumeAction(1, out TimeActionPreview third));
            Assert.AreEqual(TimePeriod.Night, third.periodAfter);
            Assert.AreEqual(0, third.remainingAfter);
            Assert.IsTrue(third.requiresSleep);
        }

        [Test]
        public void FourthActionInSameDay_IsRejected()
        {
            Assert.IsTrue(controller.TryConsumeAction(1, out _));
            Assert.IsTrue(controller.TryConsumeAction(1, out _));
            Assert.IsTrue(controller.TryConsumeAction(1, out _));

            Assert.IsFalse(controller.TryConsumeAction(1, out TimeActionPreview result));
            Assert.AreEqual("夜里做不了事了，回卧室睡觉吧。", result.message);
            Assert.AreEqual(TimePeriod.Night, controller.State.currentPeriod);
        }

        [Test]
        public void ConsumeAction_IsAtomicWhenDailyActionsAreInsufficient()
        {
            controller.State.currentPeriod = TimePeriod.Evening;
            controller.State.currentActionPoints = 1;

            Assert.IsFalse(controller.TryConsumeAction(2, out TimeActionPreview result));
            Assert.AreEqual("今天的行动次数用完了，回卧室睡觉开始下一天。", result.message);
            Assert.AreEqual(TimePeriod.Evening, controller.State.currentPeriod);
            Assert.AreEqual(1, controller.State.currentActionPoints);
        }

        [Test]
        public void HomeSleep_AdvancesOnePeriodAndDiscardsUnusedCurrentPoints()
        {
            controller.State.currentActionPoints = 1;
            SleepResult result = controller.Sleep(SleepLocation.Home);

            Assert.AreEqual(1, result.discardedActionPoints);
            Assert.AreEqual(TimePeriod.Afternoon, controller.State.currentPeriod);
            Assert.AreEqual(1, controller.State.currentActionPoints);
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
            Assert.AreEqual(1, controller.State.currentActionPoints);
        }

        [Test]
        public void BusStopSleep_AppliesMoodPenalty()
        {
            int moodBefore = sessionState.playerState.mood;
            SleepResult result = controller.Sleep(SleepLocation.BusStop);

            Assert.AreEqual(-TimeActionController.BusStopMoodPenalty, result.moodChange);
            Assert.AreEqual(moodBefore - TimeActionController.BusStopMoodPenalty, sessionState.playerState.mood);
        }

        [Test]
        public void DaysForStage_ConvertsBudgetToDays()
        {
            Assert.AreEqual(2, TimeActionState.DaysForStage(6));
            Assert.AreEqual(3, TimeActionState.DaysForStage(8));
            Assert.AreEqual(1, TimeActionState.DaysForStage(0));
        }
    }
}
