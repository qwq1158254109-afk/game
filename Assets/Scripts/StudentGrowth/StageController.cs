using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public class StageController : MonoBehaviour
    {
        public GameSessionState sessionState = new GameSessionState();
        public List<StageData> stageSequence = new List<StageData>();
        public ChallengeController challengeController;

        private readonly RewardResolver rewardResolver = new RewardResolver();
        private readonly ItemEffectResolver itemEffectResolver = new ItemEffectResolver();
        private readonly List<ActivityData> availableActivitiesBuffer = new List<ActivityData>();

        public void SetSessionState(GameSessionState state)
        {
            sessionState = state ?? new GameSessionState();
            if (challengeController != null)
            {
                challengeController.SetSessionState(sessionState);
            }
        }

        public void StartStage(StageData stageData)
        {
            if (stageData == null)
            {
                throw new ArgumentNullException("stageData");
            }

            sessionState.stageState.currentStage = stageData;
            sessionState.stageState.remainingTime = Mathf.Max(0, stageData.timeLimit);
            sessionState.stageState.bossUnlocked = false;
            sessionState.stageState.bossDefeated = false;

            // 进入新阶段时重置日内状态（行动点回满、时段回早晨、天数归 1）。
            // 目的：让「第 N 天」始终是「本阶段第 N 天」，不跨阶段累加成「第 90 天」。
            // 注意：阶段回合数（remainingTime）才是宏观时钟，日期标签由它派生，与天数无关。
            if (sessionState.timeActionState == null)
            {
                sessionState.timeActionState = new TimeActionState();
            }
            else
            {
                sessionState.timeActionState.ResetToDefaults();
            }

            // GDD §5.2：活动可用次数必须按阶段重置。
            // actionUseCount 若跨阶段累计，会让「做过一次的活动永久失效」——
            // 与日内状态一起清空，使 maxUsesPerStage 成为一个每阶段的独立预算。
            if (sessionState.locationState == null)
            {
                sessionState.locationState = new LocationState();
            }

            sessionState.locationState.actionUseCount.Clear();

            GameArtBindingUtility.ApplyKnownArtPath(stageData);
            rewardResolver.ApplyRewards(stageData.enterStageRewards, sessionState);
        }

        public IReadOnlyList<ActivityData> GetAvailableActivities()
        {
            availableActivitiesBuffer.Clear();
            StageData currentStage = sessionState.stageState.currentStage;
            if (currentStage == null || currentStage.availableActivities == null)
            {
                return availableActivitiesBuffer;
            }

            foreach (ActivityData activity in currentStage.availableActivities)
            {
                if (activity == null)
                {
                    continue;
                }

                if (EventConditionResolver.AreConditionsMet(activity.appearConditions, sessionState.playerState))
                {
                    availableActivitiesBuffer.Add(activity);
                }
            }

            return availableActivitiesBuffer;
        }

        public void CompleteActivity(ActivityData activityData, ActivityResult result)
        {
            if (activityData == null)
            {
                throw new ArgumentNullException("activityData");
            }

            if (result != null)
            {
                rewardResolver.ApplyRewards(result.rewards, sessionState);
            }
            else
            {
                rewardResolver.ApplyRewards(activityData.fixedRewards, sessionState);
            }

            int timeCost = Mathf.Max(0, activityData.timeCost);
            sessionState.stageState.remainingTime = Mathf.Max(0, sessionState.stageState.remainingTime - timeCost);
            if (sessionState.stageState.remainingTime <= 0)
            {
                sessionState.stageState.bossUnlocked = true;
            }
        }

        public bool ShouldStartBoss()
        {
            StageState stageState = sessionState.stageState;
            return stageState.currentStage != null
                && stageState.currentStage.bossChallenge != null
                && !sessionState.BattleResults.IsStageCleared(stageState.currentStage)
                && (stageState.bossUnlocked || stageState.remainingTime <= 0);
        }

        public void StartBoss()
        {
            if (!ShouldStartBoss())
            {
                return;
            }

            sessionState.stageState.bossUnlocked = true;
            if (challengeController != null)
            {
                challengeController.SetSessionState(sessionState);
                challengeController.TryStartChallenge(sessionState.stageState.currentStage.bossChallenge, out _);
            }
        }

        public void CompleteBoss(bool success)
        {
            var stage = sessionState.stageState.currentStage;
            if (stage == null) return;
            if (challengeController != null && challengeController.sessionState == sessionState
                && challengeController.runtimeState.data == stage.bossChallenge && !challengeController.runtimeState.isComplete)
            {
                challengeController.CompleteChallenge(success);
                return;
            }
            sessionState.BattleResults.CompleteStage(stage, success);
        }

        public void MoveToNextStage()
        {
            StageData currentStage = sessionState.stageState.currentStage;
            if (currentStage == null || currentStage.isFinalStage)
            {
                sessionState.endingReached = true;
                return;
            }

            StageData nextStage = FindStageById(currentStage.nextStageId);
            if (nextStage == null)
            {
                sessionState.endingReached = true;
                return;
            }

            itemEffectResolver.ClearStageOnlyItems(sessionState);
            StartStage(nextStage);
        }

        private StageData FindStageById(string stageId)
        {
            if (string.IsNullOrEmpty(stageId))
            {
                return null;
            }

            foreach (StageData stageData in stageSequence)
            {
                if (stageData != null && stageData.stageId == stageId)
                {
                    return stageData;
                }
            }

            return null;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
