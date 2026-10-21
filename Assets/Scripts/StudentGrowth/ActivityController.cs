using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public class ActivityController : MonoBehaviour
    {
        public GameSessionState sessionState = new GameSessionState();

        public void SetSessionState(GameSessionState state)
        {
            sessionState = state ?? new GameSessionState();
        }

        public ActivityResult ResolveActivity(ActivityData activityData)
        {
            if (activityData == null)
            {
                throw new ArgumentNullException("activityData");
            }

            ActivityResult result = new ActivityResult
            {
                activityData = activityData,
                activityType = activityData.activityType,
                success = true,
                linkedChallenge = activityData.linkedChallenge,
                linkedEvent = activityData.linkedEvent,
                linkedMiniGame = activityData.linkedMiniGame
            };

            AddRewards(result.rewards, activityData.fixedRewards);

            switch (activityData.activityType)
            {
                case ActivityType.Event:
                    ResolveEventActivity(activityData, result);
                    break;
                case ActivityType.CardBattle:
                    result.message = "对战即将开始。";
                    break;
                case ActivityType.MiniGame:
                    ResolveMiniGamePlaceholder(activityData, result);
                    break;
                case ActivityType.Rest:
                    result.message = "休息已结束。";
                    break;
                case ActivityType.Shop:
                    result.message = "购物已完成。";
                    break;
                case ActivityType.Training:
                    result.message = "训练已完成。";
                    break;
                default:
                    throw new ArgumentOutOfRangeException("activityData.activityType", activityData.activityType, "Unsupported activity type.");
            }

            return result;
        }

        public bool CanActivityAppear(ActivityData activityData)
        {
            return activityData != null
                && EventConditionResolver.AreConditionsMet(activityData.appearConditions, sessionState.playerState);
        }

        private void ResolveEventActivity(ActivityData activityData, ActivityResult result)
        {
            if (activityData.linkedEvent == null)
            {
                result.message = "该活动没有绑定事件。";
                return;
            }

            foreach (EventChoiceData choice in activityData.linkedEvent.choices)
            {
                if (!EventConditionResolver.AreConditionsMet(choice.requirements, sessionState.playerState))
                {
                    continue;
                }

                result.selectedChoice = choice;
                AddRewards(result.rewards, choice.rewards);
                result.message = choice.choiceText;
                return;
            }

            result.success = false;
            result.message = "当前没有满足条件的事件选项。";
        }

        private static void ResolveMiniGamePlaceholder(ActivityData activityData, ActivityResult result)
        {
            if (activityData.linkedMiniGame == null)
            {
                result.message = "该活动没有绑定小游戏。";
                return;
            }

            AddRewards(result.rewards, activityData.linkedMiniGame.successRewards);
            result.message = "小游戏已完成。";
        }

        private static void AddRewards(List<RewardData> target, IEnumerable<RewardData> rewards)
        {
            if (rewards == null)
            {
                return;
            }

            foreach (RewardData reward in rewards)
            {
                if (reward != null)
                {
                    target.Add(reward);
                }
            }
        }
    }

    [Serializable]
    public class ActivityResult
    {
        public ActivityData activityData;
        public ActivityType activityType;
        public bool success;
        public ChallengeData linkedChallenge;
        public GameEventData linkedEvent;
        public EventChoiceData selectedChoice;
        public MiniGameData linkedMiniGame;
        public List<RewardData> rewards = new List<RewardData>();
        public string message;
    }
}
