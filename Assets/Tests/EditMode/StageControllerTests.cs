using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class StageControllerTests
    {
        private GameObject gameObject;
        private StageController stageController;
        private ActivityController activityController;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("StageControllerTests");
            stageController = gameObject.AddComponent<StageController>();
            activityController = gameObject.AddComponent<ActivityController>();
            GameSessionState sessionState = new GameSessionState();
            stageController.SetSessionState(sessionState);
            activityController.SetSessionState(sessionState);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void StartStageSetsRemainingTimeToTimeLimit()
        {
            StageData stageData = CreateStage("stage_test", 6, false);

            stageController.StartStage(stageData);

            Assert.AreEqual(6, stageController.sessionState.stageState.remainingTime);
        }

        [Test]
        public void CompleteActivityAppliesRewardsAndReducesRemainingTime()
        {
            StageData stageData = CreateStage("stage_test", 6, false);
            ActivityData activityData = CreateRewardActivity(2, PlayerStatKey.Math, 5);
            stageData.availableActivities.Add(activityData);
            stageController.StartStage(stageData);

            ActivityResult result = activityController.ResolveActivity(activityData);
            stageController.CompleteActivity(activityData, result);

            Assert.AreEqual(4, stageController.sessionState.stageState.remainingTime);
            Assert.AreEqual(40, stageController.sessionState.playerState.math);
        }

        [Test]
        public void RemainingTimeAtZeroShouldStartBoss()
        {
            StageData stageData = CreateStage("stage_test", 1, false);
            stageData.bossChallenge = CreateBoss("boss_test");
            ActivityData activityData = CreateRewardActivity(1, PlayerStatKey.Math, 1);
            stageController.StartStage(stageData);

            stageController.CompleteActivity(activityData, activityController.ResolveActivity(activityData));

            Assert.IsTrue(stageController.ShouldStartBoss());
        }

        [Test]
        public void BossVictoryIncrementsDefeatedBossCount()
        {
            StageData stageData = CreateStage("stage_test", 1, false);
            stageData.bossChallenge = CreateBoss("boss_test");
            stageController.StartStage(stageData);

            stageController.CompleteBoss(true);

            Assert.AreEqual(1, stageController.sessionState.stageState.defeatedBossCount);
            Assert.AreEqual(1, stageController.sessionState.stageState.clearedStageCount);
        }

        [Test]
        public void FinalStageBossCompletionReachesEnding()
        {
            StageData stageData = CreateStage("stage_final", 1, true);
            stageData.bossChallenge = CreateBoss("boss_final");
            stageController.StartStage(stageData);

            stageController.CompleteBoss(true);

            Assert.IsTrue(stageController.sessionState.endingReached);
        }

        [Test]
        public void MoveToNextStageUsesConfiguredNextStageId()
        {
            List<StageData> stages = MvpSampleDataFactory.CreateThreeStageSequence();
            stageController.stageSequence = stages;
            stageController.StartStage(stages[0]);

            stageController.MoveToNextStage();

            Assert.AreEqual("stage_02_month_exam", stageController.sessionState.stageState.currentStage.stageId);
        }

        [Test]
        public void SampleDataCanAdvanceFromStageOneToFinalEnding()
        {
            List<StageData> stages = MvpSampleDataFactory.CreateThreeStageSequence();
            stageController.stageSequence = stages;

            stageController.StartStage(stages[0]);
            stageController.CompleteBoss(true);
            stageController.MoveToNextStage();
            stageController.CompleteBoss(true);
            stageController.MoveToNextStage();
            stageController.CompleteBoss(true);

            Assert.AreEqual("stage_03_final_sprint", stageController.sessionState.stageState.currentStage.stageId);
            Assert.IsTrue(stageController.sessionState.endingReached);
            Assert.AreEqual(3, stageController.sessionState.stageState.defeatedBossCount);
        }

        private static StageData CreateStage(string stageId, int timeLimit, bool isFinalStage)
        {
            StageData stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.stageId = stageId;
            stageData.timeLimit = timeLimit;
            stageData.isFinalStage = isFinalStage;
            return stageData;
        }

        private static ActivityData CreateRewardActivity(int timeCost, PlayerStatKey statKey, int value)
        {
            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.activityType = ActivityType.Training;
            activityData.timeCost = timeCost;
            activityData.fixedRewards.Add(new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = statKey,
                value = value
            });
            return activityData;
        }

        private static ChallengeData CreateBoss(string challengeId)
        {
            ChallengeData challengeData = ScriptableObject.CreateInstance<ChallengeData>();
            challengeData.challengeId = challengeId;
            challengeData.isBoss = true;
            challengeData.targetProgress = 10;
            challengeData.maxTurns = 3;
            challengeData.startingEnergy = 3;
            challengeData.startingHandSize = 5;
            return challengeData;
        }
    }
}
