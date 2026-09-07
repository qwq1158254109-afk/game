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
                    result.message = "Card battle activity prepared.";
                    break;
                case ActivityType.MiniGame:
                    ResolveMiniGamePlaceholder(activityData, result);
                    break;
                case ActivityType.Rest:
                    result.message = "Rest activity resolved.";
                    break;
                case ActivityType.Shop:
                    result.message = "Shop activity resolved with configured rewards.";
                    break;
                case ActivityType.Training:
                    result.message = "Training activity resolved with configured rewards.";
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
                result.message = "Event activity has no linked event.";
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
            result.message = "No available event choice.";
        }

        private static void ResolveMiniGamePlaceholder(ActivityData activityData, ActivityResult result)
        {
            if (activityData.linkedMiniGame == null)
            {
                result.message = "Mini game activity has no linked mini game.";
                return;
            }

            AddRewards(result.rewards, activityData.linkedMiniGame.successRewards);
            result.message = "Mini game placeholder resolved as success.";
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
