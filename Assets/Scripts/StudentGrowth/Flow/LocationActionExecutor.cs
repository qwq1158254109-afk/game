using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public enum LocationActionSource
    {
        Unknown,
        LegacyButton,
        Map
    }

    [Serializable]
    public class LocationActionExecutionRequest
    {
        public LocationActionSource source = LocationActionSource.Unknown;
        public string locationId;
        public string actionId;
        public bool requiresUnlockedLocation = true;
        public LocationData location;
        public LocationActionData locationAction;
        public ActivityData activity;
        /// <summary>事件活动由选项面板预先选定的分支；为空时回退到「第一个满足条件的选项」。</summary>
        public EventChoiceData choice;
        public string requestId;
    }

    [Serializable]
    public class LocationActionExecutionResult
    {
        public bool success;
        public string message;
        public string errorCode;
        public bool timeAdvanced;
        public bool shouldRefreshUi;
        public bool shouldOpenBattle;
        public bool shouldOpenDialogue;
        public bool shouldOpenShop;
        public bool shouldReturnToMap;
        public int remainingTimeBefore;
        public int remainingTimeAfter;
        public int remainingDailyActionPointsBefore;
        public int remainingDailyActionPointsAfter;
        public TimePeriod periodBefore;
        public TimePeriod periodAfter;
        public LocationActionSource source;
        public ActivityData activity;
        public ActivityResult activityResult;
    }

    public class LocationActionExecutor : MonoBehaviour
    {
        private const string ErrorInvalidRequest = "InvalidRequest";
        private const string ErrorMissingController = "MissingController";
        private const string ErrorLockedLocation = "LockedLocation";
        private const string ErrorActionUnavailable = "ActionUnavailable";
        private const string ErrorActivityUnavailable = "ActivityUnavailable";
        private const string ErrorInsufficientTime = "InsufficientTime";
        private const string ErrorInsufficientActionPoints = "InsufficientActionPoints";

        private GameSessionState sessionState;
        private StageController stageController;
        private ActivityController activityController;
        private MiniGameController miniGameController;
        private SceneTransitionController sceneTransitionController;
        private TimeActionController timeActionController;

        public void Configure(
            GameSessionState state,
            StageController stage,
            ActivityController activity,
            MiniGameController miniGame,
            SceneTransitionController sceneTransition,
            TimeActionController timeAction = null)
        {
            sessionState = state;
            stageController = stage;
            activityController = activity;
            miniGameController = miniGame;
            sceneTransitionController = sceneTransition;
            timeActionController = timeAction;
        }

        public LocationActionExecutionResult TryExecute(LocationActionExecutionRequest request)
        {
            LocationActionExecutionResult result = CreateResult(request);
            if (request == null)
            {
                return Fail(result, ErrorInvalidRequest, "行动请求无效。");
            }

            if (stageController == null || activityController == null)
            {
                return Fail(result, ErrorMissingController, "活动控制器未初始化。");
            }

            SyncSessionState();
            if(sessionState!=null && sessionState.BattleFlow.BlocksSave)
                return Fail(result,"BattleBusy","正在处理战斗，请先返回地图。");

            LocationData targetLocation = ResolveLocation(request);
            if (!CanUseLocation(targetLocation, request.requiresUnlockedLocation, out string lockedReason))
            {
                return Fail(result, ErrorLockedLocation, lockedReason);
            }

            LocationActionData action = ResolveLocationAction(request, targetLocation);
            if (!CanUseAction(action, out string actionReason))
            {
                return Fail(result, ErrorActionUnavailable, actionReason);
            }

            ActivityData activity = ResolveActivity(request, action);
            result.activity = activity;
            if (activity == null)
            {
                return Fail(result, ErrorActivityUnavailable, "活动数据不存在。");
            }

            if (!activityController.CanActivityAppear(activity))
            {
                return Fail(result, ErrorActivityUnavailable, "当前条件不满足，无法进行该活动。");
            }

            // 睡觉是换日结算，不占阶段预算，也不占当天行动格；但只能在夜晚进行，
            // 否则「睡一觉回精力」会被当成免费补给反复刷（白天小睡同样会回精力）。
            bool isSleepActivity = IsHomeSleepActivity(activity);
            if (isSleepActivity && timeActionController != null
                && timeActionController.State.currentPeriod != TimePeriod.Night)
            {
                return Fail(result, ErrorActivityUnavailable, "白天睡不着，先把今天的事做完。");
            }

            if (!isSleepActivity && !CanSpendActivityTime(activity))
            {
                return Fail(result, ErrorInsufficientTime, "时间不足，无法进行该行动。");
            }

            TimeActionPreview actionPreview = null;
            if (!isSleepActivity && timeActionController != null)
            {
                actionPreview = timeActionController.PreviewAction(Mathf.Max(0, activity.timeCost));
                if (!actionPreview.canConsume)
                {
                    return Fail(result, ErrorInsufficientActionPoints, actionPreview.message);
                }
            }

            result.remainingTimeBefore = GetRemainingTime();
            CaptureDailyTimeBefore(result);
            if(activity.activityType==ActivityType.CardBattle)
            {
                if(string.IsNullOrEmpty(request.requestId))request.requestId=Guid.NewGuid().ToString("N");
                try
                {
                    if(!sessionState.BattleFlow.Start(request.requestId,activity.linkedChallenge,activity,stageController,
                        timeActionController,stageController.challengeController,BattleReturnContext.Capture(),(uint)Environment.TickCount,out var error))
                        return Fail(result,"BattleStartFailed",error);
                    result.success=true;result.shouldOpenBattle=true;result.shouldRefreshUi=true;
                    result.remainingTimeAfter=GetRemainingTime();CaptureDailyTimeAfter(result);
                    result.timeAdvanced=result.remainingTimeBefore!=result.remainingTimeAfter;
                    result.message="挑战开始；固定奖励将在战后结算。";return result;
                }
                catch(Exception ex){return Fail(result,"BattleStartFailed",ex.Message);}
            }
            ActivityResult activityResult = ResolveActivityForCompletion(activity, request.choice);
            if (isSleepActivity && timeActionController != null)
            {
                timeActionController.Sleep(SleepLocation.Home);
            }
            else if (actionPreview != null
                && !timeActionController.TryConsumeAction(Mathf.Max(0, activity.timeCost), out actionPreview))
            {
                return Fail(result, ErrorInsufficientActionPoints, actionPreview.message);
            }

            stageController.CompleteActivity(activity, activityResult, !isSleepActivity);
            result.remainingTimeAfter = GetRemainingTime();
            CaptureDailyTimeAfter(result);
            result.activityResult = activityResult;
            result.success = true;
            result.shouldRefreshUi = true;
            result.shouldOpenBattle = activity.activityType == ActivityType.CardBattle && activity.linkedChallenge != null;
            result.shouldOpenDialogue = activity.activityType == ActivityType.Event && activity.linkedEvent != null;
            result.shouldOpenShop = activity.activityType == ActivityType.Shop;
            result.timeAdvanced = result.remainingDailyActionPointsAfter < result.remainingDailyActionPointsBefore
                || result.periodAfter != result.periodBefore
                || Mathf.Max(0, activity.timeCost) > 0 && result.remainingTimeAfter < result.remainingTimeBefore;
            result.message = BuildSuccessMessage(activity, activityResult);
            return result;
        }

        private void SyncSessionState()
        {
            if (sessionState == null && stageController != null)
            {
                sessionState = stageController.sessionState;
            }

            if (sessionState == null && activityController != null)
            {
                sessionState = activityController.sessionState;
            }
        }

        private LocationData ResolveLocation(LocationActionExecutionRequest request)
        {
            if (request.location != null)
            {
                return request.location;
            }

            if (string.IsNullOrEmpty(request.locationId))
            {
                return null;
            }

            if (sceneTransitionController == null)
            {
                return null;
            }

            IReadOnlyList<LocationData> locations = sceneTransitionController.GetKnownLocations();
            for (int i = 0; i < locations.Count; i++)
            {
                LocationData location = locations[i];
                if (location != null && location.locationId == request.locationId)
                {
                    return location;
                }
            }

            return null;
        }

        private bool CanUseLocation(LocationData location, bool requiresUnlockedLocation, out string reason)
        {
            reason = string.Empty;
            if (location == null || !requiresUnlockedLocation)
            {
                return true;
            }

            if (sceneTransitionController == null || sceneTransitionController.CanEnterLocation(location))
            {
                return true;
            }

            reason = sceneTransitionController.GetLockedReason(location);
            return false;
        }

        private LocationActionData ResolveLocationAction(LocationActionExecutionRequest request, LocationData location)
        {
            if (request.locationAction != null)
            {
                return request.locationAction;
            }

            if (location == null || string.IsNullOrEmpty(request.actionId))
            {
                return null;
            }

            for (int i = 0; i < location.actions.Count; i++)
            {
                LocationActionData action = location.actions[i];
                if (action != null && action.actionId == request.actionId)
                {
                    return action;
                }
            }

            return null;
        }

        private bool CanUseAction(LocationActionData action, out string reason)
        {
            reason = string.Empty;
            if (action == null)
            {
                return true;
            }

            PlayerState player = sessionState == null ? null : sessionState.playerState;
            if (!EventConditionResolver.AreConditionsMet(action.appearConditions, player)
                || !EventConditionResolver.AreConditionsMet(action.executeConditions, player))
            {
                reason = "当前条件不满足，无法执行该地点行动。";
                return false;
            }

            return true;
        }

        private static ActivityData ResolveActivity(LocationActionExecutionRequest request, LocationActionData action)
        {
            if (request.activity != null)
            {
                return request.activity;
            }

            return action == null ? null : action.linkedActivity;
        }

        private bool CanSpendActivityTime(ActivityData activity)
        {
            return activity != null
                && sessionState != null
                && sessionState.stageState.remainingTime >= Mathf.Max(0, activity.timeCost);
        }

        private int GetRemainingTime()
        {
            return sessionState == null ? 0 : sessionState.stageState.remainingTime;
        }

        private void CaptureDailyTimeBefore(LocationActionExecutionResult result)
        {
            TimeActionState state = timeActionController == null ? null : timeActionController.State;
            if (state == null)
            {
                return;
            }

            result.remainingDailyActionPointsBefore = state.RemainingDailyActionPoints;
            result.periodBefore = state.currentPeriod;
        }

        private void CaptureDailyTimeAfter(LocationActionExecutionResult result)
        {
            TimeActionState state = timeActionController == null ? null : timeActionController.State;
            if (state == null)
            {
                return;
            }

            result.remainingDailyActionPointsAfter = state.RemainingDailyActionPoints;
            result.periodAfter = state.currentPeriod;
        }

        private static bool IsHomeSleepActivity(ActivityData activity)
        {
            return activity != null
                && activity.activityType == ActivityType.Rest
                && !string.IsNullOrEmpty(activity.activityId)
                && activity.activityId.IndexOf("sleep", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private ActivityResult ResolveActivityForCompletion(ActivityData activity, EventChoiceData choice)
        {
            if (activity.activityType == ActivityType.MiniGame
                && activity.linkedMiniGame != null
                && miniGameController != null
                && sessionState != null)
            {
                MiniGameResult miniGameResult = miniGameController.ResolveMiniGame(
                    activity.linkedMiniGame,
                    sessionState.playerState,
                    new System.Random());
                return new ActivityResult
                {
                    activityData = activity,
                    activityType = activity.activityType,
                    success = miniGameResult.success,
                    linkedMiniGame = activity.linkedMiniGame,
                    rewards = miniGameResult.rewards,
                    message = "小游戏成功率 " + miniGameResult.successChance + "%，结果：" + (miniGameResult.success ? "成功" : "失败")
                };
            }

            return activityController.ResolveActivity(activity, choice);
        }

        private static LocationActionExecutionResult CreateResult(LocationActionExecutionRequest request)
        {
            return new LocationActionExecutionResult
            {
                source = request == null ? LocationActionSource.Unknown : request.source,
                shouldRefreshUi = false
            };
        }

        private static LocationActionExecutionResult Fail(
            LocationActionExecutionResult result,
            string errorCode,
            string message)
        {
            result.success = false;
            result.errorCode = errorCode;
            result.message = string.IsNullOrEmpty(message) ? "行动无法执行。" : message;
            return result;
        }

        private static string BuildSuccessMessage(ActivityData activity, ActivityResult activityResult)
        {
            string activityName = activity == null ? "活动" : activity.displayName;
            string resultMessage = activityResult == null ? string.Empty : activityResult.message;
            return string.IsNullOrEmpty(resultMessage)
                ? "完成活动：" + activityName + "。"
                : "完成活动：" + activityName + "。" + resultMessage;
        }
    }
}
