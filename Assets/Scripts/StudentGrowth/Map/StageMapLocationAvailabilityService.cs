using System.Collections.Generic;

namespace StudentGrowth
{
    public sealed class StageMapLocationAvailabilityService
    {
        public const string Stage2OrLaterCondition = "stage_02_or_later";
        public const string Stage3OrLaterCondition = "stage_03_or_later";
        public const string ClubInterestOrSocialCondition = "club_interest_or_social";

        public StageMapLocationStatus ResolveStatus(
            StageMapLocationEntry entry,
            GameSessionState sessionState,
            StageController stageController,
            SceneTransitionController sceneTransitionController)
        {
            if (entry == null || entry.defaultStatus == StageMapLocationStatus.Hidden)
            {
                return StageMapLocationStatus.Hidden;
            }

            if (entry.defaultStatus == StageMapLocationStatus.ComingSoon
                || entry.defaultStatus == StageMapLocationStatus.Disabled
                || entry.defaultStatus == StageMapLocationStatus.Completed)
            {
                return entry.defaultStatus;
            }

            if (sceneTransitionController != null && !IsKnownLocation(sceneTransitionController, entry.locationId))
            {
                return StageMapLocationStatus.Disabled;
            }

            if (entry.defaultStatus == StageMapLocationStatus.Locked)
            {
                return IsUnlockConditionMet(entry.unlockConditionKey, sessionState, stageController)
                    ? StageMapLocationStatus.Unlocked
                    : StageMapLocationStatus.Locked;
            }

            return StageMapLocationStatus.Unlocked;
        }

        public string ResolveReason(StageMapLocationEntry entry, GameSessionState sessionState)
        {
            StageMapLocationStatus status = ResolveStatus(entry, sessionState, null, null);
            return ResolveReason(entry, status);
        }

        public string ResolveReason(StageMapLocationEntry entry, StageMapLocationStatus status)
        {
            if (entry == null)
            {
                return "地点配置不存在。";
            }

            switch (status)
            {
                case StageMapLocationStatus.Locked:
                    return string.IsNullOrEmpty(entry.lockedReason) ? "解锁条件未满足。" : entry.lockedReason;
                case StageMapLocationStatus.ComingSoon:
                    return string.IsNullOrEmpty(entry.comingSoonText) ? "功能开发中。" : entry.comingSoonText;
                case StageMapLocationStatus.Disabled:
                    return string.IsNullOrEmpty(entry.lockedReason) ? "当前不可进入。" : entry.lockedReason;
                case StageMapLocationStatus.Completed:
                    return "当前阶段重点事件已完成。";
                case StageMapLocationStatus.Unlocked:
                    return "进入地点不消耗时间。";
                default:
                    return string.Empty;
            }
        }

        public bool CanEnter(StageMapLocationEntry entry, GameSessionState sessionState, StageController stageController, SceneTransitionController sceneTransitionController)
        {
            StageMapLocationStatus status = ResolveStatus(entry, sessionState, stageController, sceneTransitionController);
            return status == StageMapLocationStatus.Unlocked || status == StageMapLocationStatus.Completed;
        }

        private static bool IsKnownLocation(SceneTransitionController sceneTransitionController, string locationId)
        {
            IReadOnlyList<LocationData> knownLocations = sceneTransitionController.GetKnownLocations();
            for (int i = 0; i < knownLocations.Count; i++)
            {
                LocationData location = knownLocations[i];
                if (location != null && location.locationId == locationId)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsUnlockConditionMet(string conditionKey, GameSessionState sessionState, StageController stageController)
        {
            if (string.IsNullOrEmpty(conditionKey))
            {
                return false;
            }

            int stageRank = ResolveStageRank(sessionState, stageController);
            switch (conditionKey)
            {
                case Stage2OrLaterCondition:
                    return stageRank >= 2;
                case Stage3OrLaterCondition:
                    return stageRank >= 3;
                case ClubInterestOrSocialCondition:
                    return stageRank >= 3 && HasClubAccess(sessionState);
                default:
                    return false;
            }
        }

        private static int ResolveStageRank(GameSessionState sessionState, StageController stageController)
        {
            StageData stage = null;
            if (stageController != null && stageController.sessionState != null)
            {
                stage = stageController.sessionState.stageState.currentStage;
            }

            if (stage == null && sessionState != null)
            {
                stage = sessionState.stageState.currentStage;
            }

            if (stage == null || string.IsNullOrEmpty(stage.stageId))
            {
                return 0;
            }

            switch (stage.stageId)
            {
                case "stage_01_adaptation":
                    return 1;
                case "stage_02_month_exam":
                    return 2;
                case "stage_03_final_sprint":
                    return 3;
                default:
                    return stage.isFinalStage ? 3 : 0;
            }
        }

        private static bool HasClubAccess(GameSessionState sessionState)
        {
            if (sessionState == null)
            {
                return false;
            }

            PlayerState player = sessionState.playerState;
            if (player != null && (player.interest >= 45 || player.social >= 45))
            {
                return true;
            }

            return sessionState.locationState.locationFlags != null
                && sessionState.locationState.locationFlags.TryGetValue("club_intro", out bool completed)
                && completed;
        }
    }
}
