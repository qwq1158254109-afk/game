using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    public class MapInteractionController : MonoBehaviour
    {
        public LocationActionExecutor locationActionExecutor;
        public LocationSceneRouter locationSceneRouter;
        public SceneTransitionController sceneTransitionController;
        public MapInteractionPoint currentInteractionPoint;

        public event Action<LocationActionExecutionResult> InteractionCompleted;

        public void SetCurrentInteractionPoint(MapInteractionPoint point)
        {
            currentInteractionPoint = point;
        }

        public LocationActionExecutionResult TryInteractCurrent()
        {
            return TryInteract(currentInteractionPoint);
        }

        public LocationActionExecutionResult TryInteract(MapInteractionTarget target)
        {
            return TryInteract(target, null);
        }

        /// <summary>choice 非空 = 玩家已在选项面板里选好分支，直接按该分支结算。</summary>
        public LocationActionExecutionResult TryInteract(MapInteractionTarget target, EventChoiceData choice)
        {
            if(UnityGameSaveSource.CurrentSession()?.BattleFlow.BlocksSave==true)return Fail("BattleBusy","请先结束战斗并返回地图。");
            if (target == null)
            {
                return Fail("InvalidInteractionTarget", "没有可互动对象。");
            }

            if (target.interactionPoint == null)
            {
                return Fail("InvalidInteractionPoint", "互动对象没有绑定执行点。");
            }

            // 房间门（HomeLivingRoom ↔ 卧室/书房/厨房）走自己的路由：
            // 不耗时间、不走地点解锁判定，直接交给 RoomDoor 播开门动画再切场景。
            // 放在其它分支之前，因为房间门也标成 LocationAction，会被下面的
            // ExecuteLocationAction 当成普通活动去查 ActivityData（查不到就报错）。
            RoomDoor roomDoor = target.GetComponent<RoomDoor>();
            if (roomDoor != null)
            {
                roomDoor.Enter();
                LocationActionExecutionResult doorResult = new LocationActionExecutionResult
                {
                    success = true,
                    source = LocationActionSource.Map,
                    message = "开门中……"
                };
                InteractionCompleted?.Invoke(doorResult);
                return doorResult;
            }

            LocationActionExecutionResult result;
            if (target.interactionPoint.interactionType == MapInteractionType.ReturnToMainUi || target.isExitPortal)
            {
                result = ReturnToMainUi();
            }
            else if (target.interactionPoint.interactionType == MapInteractionType.EnterLocationScene)
            {
                result = EnterLocationScene(target.interactionPoint);
            }
            else if (target.interactionPoint.interactionType == MapInteractionType.LocationAction)
            {
                result = ExecuteLocationAction(target, choice);
            }
            else
            {
                result = Fail("UnsupportedInteractionType", "当前互动类型尚未接入执行流程。");
            }

            InteractionCompleted?.Invoke(result);
            return result;
        }

        public LocationActionExecutionResult TryInteract(MapInteractionPoint point)
        {
            if(UnityGameSaveSource.CurrentSession()?.BattleFlow.BlocksSave==true)return Fail("BattleBusy","请先结束战斗并返回地图。");
            if (point == null)
            {
                return Fail("InvalidInteractionPoint", "没有可互动对象。");
            }

            LocationActionExecutionResult result;
            if (point.interactionType == MapInteractionType.ReturnToMainUi)
            {
                result = ReturnToMainUi();
            }
            else if (point.interactionType == MapInteractionType.EnterLocationScene)
            {
                result = EnterLocationScene(point);
            }
            else if (point.interactionType == MapInteractionType.LocationAction)
            {
                result = ExecuteLocationAction(point);
            }
            else
            {
                result = Fail("UnsupportedInteractionType", "当前互动类型尚未接入执行流程。");
            }

            InteractionCompleted?.Invoke(result);
            return result;
        }

        private LocationActionExecutionResult ExecuteLocationAction(MapInteractionPoint point)
        {
            if (locationActionExecutor == null)
            {
                return Fail("MissingLocationActionExecutor", "地点行动执行器未初始化。");
            }

            return locationActionExecutor.TryExecute(point.CreateExecutionRequest(LocationActionSource.Map));
        }

        private LocationActionExecutionResult ExecuteLocationAction(MapInteractionTarget target, EventChoiceData choice)
        {
            if (locationActionExecutor == null)
            {
                return Fail("MissingLocationActionExecutor", "地点行动执行器未初始化。");
            }

            LocationActionExecutionRequest request = target.interactionPoint.CreateExecutionRequest(LocationActionSource.Map);
            if (choice != null)
            {
                request.choice = choice;
            }
            if (target.locationAction != null)
            {
                request.locationAction = target.locationAction;
            }

            if (target.fallbackActivity != null)
            {
                request.activity = target.fallbackActivity;
            }

            return locationActionExecutor.TryExecute(request);
        }

        private LocationActionExecutionResult EnterLocationScene(MapInteractionPoint point)
        {
            if (locationSceneRouter == null)
            {
                return Fail("MissingLocationSceneRouter", "地点场景路由器未初始化。");
            }

            LocationSceneRouteResult routeResult = locationSceneRouter.EnterLocationScene(point.locationId);
            return new LocationActionExecutionResult
            {
                success = routeResult.success,
                source = LocationActionSource.Map,
                message = routeResult.message,
                shouldRefreshUi = true,
                remainingTimeBefore = routeResult.remainingTimeBefore,
                remainingTimeAfter = routeResult.remainingTimeAfter
            };
        }

        private LocationActionExecutionResult ReturnToMainUi()
        {
            if (locationSceneRouter != null)
            {
                LocationSceneRouteResult routeResult = locationSceneRouter.ReturnToStageMap();
                return new LocationActionExecutionResult
                {
                    success = routeResult.success,
                    source = LocationActionSource.Map,
                    message = routeResult.message,
                    shouldRefreshUi = true,
                    shouldReturnToMap = routeResult.success,
                    remainingTimeBefore = routeResult.remainingTimeBefore,
                    remainingTimeAfter = routeResult.remainingTimeAfter
                };
            }

            if (sceneTransitionController == null)
            {
                return Fail("MissingSceneTransitionController", "场景切换控制器未初始化。");
            }

            sceneTransitionController.ShowMap();
            // 2026-10-28（T7）：补 Application.isPlaying —— 编辑器非运行态调 LoadScene
            // 会直接抛「This can only be used during play mode」，把 EditMode 用例全打红。
            // Play 模式行为不变。
            if (Application.isPlaying
                && SceneManager.GetActiveScene().name != "StageHubMapScene"
                && Application.CanStreamedLevelBeLoaded("StageHubMapScene"))
            {
                GameBootstrapper.SetSharedSession(sceneTransitionController.sessionState);
                if (!AudioSettingsController.DisplayTransitionBlocked && !PauseMenuRuntime.BlocksGameplayNavigation) SceneManager.LoadScene("StageHubMapScene", LoadSceneMode.Single);
            }

            return new LocationActionExecutionResult
            {
                success = true,
                source = LocationActionSource.Map,
                message = "已返回阶段地图。返回不消耗时间。",
                shouldRefreshUi = true,
                shouldReturnToMap = true
            };
        }

        private static LocationActionExecutionResult Fail(string errorCode, string message)
        {
            return new LocationActionExecutionResult
            {
                success = false,
                source = LocationActionSource.Map,
                errorCode = errorCode,
                message = message
            };
        }
    }
}
