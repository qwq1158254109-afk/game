using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class ActivityControllerTests
    {
        private GameObject gameObject;
        private ActivityController activityController;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("ActivityControllerTests");
            activityController = gameObject.AddComponent<ActivityController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void ResolveActivityIncludesFixedRewards()
        {
            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.activityType = ActivityType.Training;
            activityData.fixedRewards.Add(new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.Math,
                value = 5
            });

            ActivityResult result = activityController.ResolveActivity(activityData);

            Assert.IsTrue(result.success);
            Assert.AreEqual(1, result.rewards.Count);
            Assert.AreEqual(PlayerStatKey.Math, result.rewards[0].targetStat);
        }

        [Test]
        public void EventActivitySelectsFirstAvailableChoice()
        {
            GameEventData eventData = ScriptableObject.CreateInstance<GameEventData>();
            eventData.choices.Add(new EventChoiceData
            {
                choiceText = "Need confidence",
                requirements =
                {
                    new EventConditionData
                    {
                        statKey = PlayerStatKey.Confidence,
                        comparison = ComparisonType.GreaterOrEqual,
                        value = 80
                    }
                }
            });
            eventData.choices.Add(new EventChoiceData
            {
                choiceText = "Available",
                rewards =
                {
                    new RewardData
                    {
                        rewardType = RewardType.AddPlayerStat,
                        targetStat = PlayerStatKey.Mood,
                        value = 2
                    }
                }
            });

            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.activityType = ActivityType.Event;
            activityData.linkedEvent = eventData;

            ActivityResult result = activityController.ResolveActivity(activityData);

            Assert.IsTrue(result.success);
            Assert.AreEqual("Available", result.selectedChoice.choiceText);
            Assert.AreEqual(1, result.rewards.Count);
        }

        [Test]
        public void MiniGameActivityReturnsPlaceholderSuccessRewards()
        {
            MiniGameData miniGameData = ScriptableObject.CreateInstance<MiniGameData>();
            miniGameData.successRewards.Add(new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.English,
                value = 4
            });

            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.activityType = ActivityType.MiniGame;
            activityData.linkedMiniGame = miniGameData;

            ActivityResult result = activityController.ResolveActivity(activityData);

            Assert.IsTrue(result.success);
            Assert.AreEqual(1, result.rewards.Count);
            Assert.AreEqual(PlayerStatKey.English, result.rewards[0].targetStat);
        }

        [Test]
        public void CanActivityAppearUsesAppearConditions()
        {
            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.appearConditions.Add(new EventConditionData
            {
                statKey = PlayerStatKey.ParentRelation,
                comparison = ComparisonType.GreaterOrEqual,
                value = 50
            });

            Assert.IsTrue(activityController.CanActivityAppear(activityData));

            activityController.sessionState.playerState.SetStat(PlayerStatKey.ParentRelation, 10);

            Assert.IsFalse(activityController.CanActivityAppear(activityData));
        }

        [Test]
        public void SampleStageActivitiesIncludeCardReward()
        {
            Assert.IsTrue(SampleStageActivitiesContainReward(RewardType.AddCard));
        }

        [Test]
        public void SampleStageActivitiesIncludeItemReward()
        {
            Assert.IsTrue(SampleStageActivitiesContainReward(RewardType.AddItem));
        }

        private static bool SampleStageActivitiesContainReward(RewardType rewardType)
        {
            List<StageData> stages = MvpSampleDataFactory.CreateThreeStageSequence();
            foreach (StageData stage in stages)
            {
                foreach (ActivityData activity in stage.availableActivities)
                {
                    if (ContainsReward(activity.fixedRewards, rewardType)
                        || (activity.linkedMiniGame != null && ContainsReward(activity.linkedMiniGame.successRewards, rewardType))
                        || (activity.linkedMiniGame != null && ContainsReward(activity.linkedMiniGame.failureRewards, rewardType))
                        || ContainsEventReward(activity.linkedEvent, rewardType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool ContainsEventReward(GameEventData eventData, RewardType rewardType)
        {
            if (eventData == null)
            {
                return false;
            }

            foreach (EventChoiceData choice in eventData.choices)
            {
                if (ContainsReward(choice.rewards, rewardType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsReward(IEnumerable<RewardData> rewards, RewardType rewardType)
        {
            if (rewards == null)
            {
                return false;
            }

            foreach (RewardData reward in rewards)
            {
                if (reward != null && reward.rewardType == rewardType)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
