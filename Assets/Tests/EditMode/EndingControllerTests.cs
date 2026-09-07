using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class EndingControllerTests
    {
        private GameObject gameObject;
        private EndingController endingController;
        private readonly List<Object> createdObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("EndingControllerTests");
            endingController = gameObject.AddComponent<EndingController>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SelectEndingChoosesHighestPriorityValidEnding()
        {
            GameSessionState state = CreateFinishedState();
            EndingData lowPriority = CreateEnding("ending_low", 1);
            EndingData highPriority = CreateEnding("ending_high", 5);

            EndingData selected = endingController.SelectEnding(state, new[] { lowPriority, highPriority });

            Assert.AreEqual(highPriority, selected);
        }

        [Test]
        public void SelectEndingIgnoresUnmetSubjectAverageRequirement()
        {
            GameSessionState state = CreateFinishedState();
            EndingData fallback = CreateEnding("ending_fallback", 0);
            EndingData unavailable = CreateEnding("ending_unavailable", 10);
            unavailable.minimumSubjectAverage = 90;

            EndingData selected = endingController.SelectEnding(state, new[] { unavailable, fallback });

            Assert.AreEqual(fallback, selected);
        }

        [Test]
        public void SelectEndingCanUsePressureCondition()
        {
            GameSessionState state = CreateFinishedState();
            state.playerState.pressure = 90;
            EndingData fallback = CreateEnding("ending_fallback", 0);
            EndingData pressureEnding = CreateEnding("ending_pressure", 10);
            pressureEnding.conditions.Add(new EventConditionData
            {
                statKey = PlayerStatKey.Pressure,
                comparison = ComparisonType.GreaterOrEqual,
                value = 85
            });

            EndingData selected = endingController.SelectEnding(state, new[] { fallback, pressureEnding });

            Assert.AreEqual(pressureEnding, selected);
        }

        [Test]
        public void SelectEndingStoresCurrentEndingOnSessionState()
        {
            GameSessionState state = CreateFinishedState();
            EndingData ending = CreateEnding("ending_selected", 1);
            endingController.SetSessionState(state);
            endingController.SetEndingOptions(new[] { ending });

            EndingData selected = endingController.SelectEnding();

            Assert.AreEqual(ending, selected);
            Assert.AreEqual(ending, state.currentEnding);
        }

        [Test]
        public void SubjectAverageUsesThreeSubjects()
        {
            PlayerState playerState = new PlayerState();
            playerState.chinese = 60;
            playerState.math = 75;
            playerState.english = 90;

            Assert.AreEqual(75, EndingController.GetSubjectAverage(playerState));
        }

        private GameSessionState CreateFinishedState()
        {
            GameSessionState state = new GameSessionState();
            state.endingReached = true;
            state.stageState.clearedStageCount = 3;
            state.stageState.defeatedBossCount = 3;
            state.playerState.chinese = 60;
            state.playerState.math = 60;
            state.playerState.english = 60;
            state.playerState.pressure = 20;
            return state;
        }

        private EndingData CreateEnding(string endingId, int priority)
        {
            EndingData ending = ScriptableObject.CreateInstance<EndingData>();
            ending.endingId = endingId;
            ending.displayName = endingId;
            ending.priority = priority;
            ending.maximumPressure = 100;
            ending.requireEndingReached = true;
            createdObjects.Add(ending);
            return ending;
        }
    }
}
