using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    [Serializable]
    public sealed class LocationSceneRouteResult
    {
        public bool success;
        public string locationId;
        public string sceneName;
        public StageMapLocationStatus status;
        public string message;
        public int remainingTimeBefore;
        public int remainingTimeAfter;
        public bool didLoadUnityScene;
    }

    public sealed class LocationSceneRouter : MonoBehaviour
    {
        public GameSessionState sessionState;
        public StageController stageController;
        public SceneTransitionController sceneTransitionController;
        public StageMapData stageMapData;
        public bool useUnitySceneLoadingForLatestMaps = true;
        public string stageMapSceneName = "StageHubMapScene";

        private readonly StageMapLocationAvailabilityService availabilityService = new StageMapLocationAvailabilityService();

        public void Configure(
            GameSessionState state,
            StageController stage,
            SceneTransitionController sceneTransition,
            StageMapData mapData)
        {
            sessionState = state;
            stageController = stage;
            sceneTransitionController = sceneTransition;
            stageMapData = mapData;
        }

        public void SetStageMapData(StageMapData mapData)
        {
            stageMapData = mapData;
        }

        public LocationSceneRouteResult EnterLocationScene(string locationId)
        {
            if (AudioSettingsController.DisplayTransitionBlocked || PauseMenuRuntime.BlocksGameplayNavigation) return new LocationSceneRouteResult { message = "请先确认或恢复显示模式。" };
            SyncSessionState();
            EnsureStageMapData();

            StageMapLocationEntry entry = FindEntry(locationId);
            StageMapLocationStatus status = availabilityService.ResolveStatus(
                entry,
                sessionState,
                stageController,
                sceneTransitionController);

            LocationSceneRouteResult result = CreateResult(entry, locationId, status);
            if (IsOutsideStageHubRouteRequest())
            {
                result.status = StageMapLocationStatus.Disabled;
                result.message = "请先返回阶段地图，再选择目标地点。";
                return result;
            }

            if (status != StageMapLocationStatus.Unlocked && status != StageMapLocationStatus.Completed)
            {
                result.message = availabilityService.ResolveReason(entry, status);
                return result;
            }

            if (sceneTransitionController == null)
            {
                result.message = "场景切换控制器未初始化。";
                return result;
            }

            if (ShouldLoadUnityScene(entry) && !CanLoadScene(entry.sceneName))
            {
                result.status = StageMapLocationStatus.Disabled;
                result.message = "场景未加入 Build Settings，无法进入：" + entry.sceneName;
                return result;
            }

            LocationData location = FindLocation(entry.locationId);
            if (location == null)
            {
                result.status = StageMapLocationStatus.Disabled;
                result.message = "地点配置不存在。";
                return result;
            }

            result.remainingTimeBefore = GetRemainingTime();
            AddUnique(sessionState.locationState.unlockedLocationIds, location.locationId);
            if (!sceneTransitionController.TryGoToLocation(location, out string reason))
            {
                result.message = reason;
                return result;
            }

            result.remainingTimeAfter = GetRemainingTime();
            result.success = true;
            result.message = "进入地点：" + location.displayName + "。移动和进入地点不消耗时间。";
            result.didLoadUnityScene = TryLoadLocationScene(entry);
            return result;
        }

        public LocationSceneRouteResult ReturnToStageMap()
        {
            if (AudioSettingsController.DisplayTransitionBlocked || PauseMenuRuntime.BlocksGameplayNavigation) return new LocationSceneRouteResult { message = "请先确认或恢复显示模式。" };
            SyncSessionState();
            LocationSceneRouteResult result = new LocationSceneRouteResult
            {
                success = true,
                remainingTimeBefore = GetRemainingTime(),
                message = "已返回阶段地图。返回不消耗时间。"
            };

            if (sceneTransitionController != null)
            {
                sceneTransitionController.ShowMap();
            }

            result.remainingTimeAfter = GetRemainingTime();
            result.didLoadUnityScene = TryLoadStageMapScene(result);
            return result;
        }

        public bool CanEnterLocationScene(string locationId, out string reason)
        {
            SyncSessionState();
            EnsureStageMapData();
            StageMapLocationEntry entry = FindEntry(locationId);
            StageMapLocationStatus status = availabilityService.ResolveStatus(
                entry,
                sessionState,
                stageController,
                sceneTransitionController);
            reason = availabilityService.ResolveReason(entry, status);
            return status == StageMapLocationStatus.Unlocked || status == StageMapLocationStatus.Completed;
        }

        private void SyncSessionState()
        {
            if (sessionState == null && stageController != null)
            {
                sessionState = stageController.sessionState;
            }

            if (sessionState == null && sceneTransitionController != null)
            {
                sessionState = sceneTransitionController.sessionState;
            }
        }

        private void EnsureStageMapData()
        {
            if (stageMapData != null)
            {
                return;
            }

            StageData currentStage = sessionState == null ? null : sessionState.stageState.currentStage;
            stageMapData = MvpSampleDataFactory.CreateDefaultStageMapData(currentStage);
        }

        private StageMapLocationEntry FindEntry(string locationId)
        {
            if (stageMapData == null || stageMapData.locations == null || string.IsNullOrEmpty(locationId))
            {
                return null;
            }

            for (int i = 0; i < stageMapData.locations.Count; i++)
            {
                StageMapLocationEntry entry = stageMapData.locations[i];
                if (entry != null && entry.locationId == locationId)
                {
                    return entry;
                }
            }

            return null;
        }

        private LocationData FindLocation(string locationId)
        {
            if (sceneTransitionController == null || string.IsNullOrEmpty(locationId))
            {
                return null;
            }

            IReadOnlyList<LocationData> locations = sceneTransitionController.GetKnownLocations();
            for (int i = 0; i < locations.Count; i++)
            {
                LocationData location = locations[i];
                if (location != null && location.locationId == locationId)
                {
                    return location;
                }
            }

            return null;
        }

        private LocationSceneRouteResult CreateResult(StageMapLocationEntry entry, string locationId, StageMapLocationStatus status)
        {
            return new LocationSceneRouteResult
            {
                success = false,
                locationId = entry == null ? locationId : entry.locationId,
                sceneName = entry == null ? string.Empty : entry.sceneName,
                status = status,
                remainingTimeBefore = GetRemainingTime(),
                remainingTimeAfter = GetRemainingTime()
            };
        }

        private bool TryLoadLocationScene(StageMapLocationEntry entry)
        {
            if (!ShouldLoadUnityScene(entry))
            {
                return false;
            }

            string activeSceneName = SceneManager.GetActiveScene().name;
            if (activeSceneName == entry.sceneName)
            {
                return false;
            }

            GameBootstrapper.SetSharedSession(sessionState);
            SchedulePlayerRebindAfterSceneLoad();
            SceneManager.LoadScene(entry.sceneName, LoadSceneMode.Single);
            return true;
        }

        private static bool pendingPlayerRebind;

        /// <summary>
        /// 预约「场景加载完成后收拾玩家」并挂一次性 sceneLoaded 回调。
        /// 不能像旧版那样在 LoadScene 后同一帧直接执行——同步 LoadScene 官方语义是
        /// 「下一帧才完成加载」，当场执行跑在旧场景上，对「新场景自带的玩家」去重无效
        /// （历史 bug：进新地图后两个人物共存）。
        /// </summary>
        private static void SchedulePlayerRebindAfterSceneLoad()
        {
            pendingPlayerRebind = true;
            SceneManager.sceneLoaded -= OnSceneLoadedPlayerRebind;
            SceneManager.sceneLoaded += OnSceneLoadedPlayerRebind;
        }

        private static void OnSceneLoadedPlayerRebind(Scene scene, LoadSceneMode mode)
        {
            SceneManager.sceneLoaded -= OnSceneLoadedPlayerRebind;

            if (!pendingPlayerRebind)
            {
                return;
            }

            pendingPlayerRebind = false;
            RebindPlayerAfterSceneLoad();
        }

        /// <summary>
        /// 场景加载完成后：补齐去重（销毁场景自带的那份、保留持久玩家）并把新场景的
        /// 地图控制器重新绑到存活玩家上、传送到该场景出生点。
        /// （PlayerManager 的全局 sceneLoaded 钩子先行兜底；此处重复执行为幂等。）
        /// </summary>
        private static void RebindPlayerAfterSceneLoad()
        {
            RpgPlayerController survivor = PlayerManager.Instance.ResolvePlayerAfterSceneLoad();
            if (survivor == null)
            {
                return;
            }

            FullscreenMapSceneController[] controllers = FindObjectsByType<FullscreenMapSceneController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < controllers.Length; i++)
            {
                FullscreenMapSceneController controller = controllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.RebindPlayer(survivor);
                controller.MovePlayerToSpawn(controller.defaultSpawnPointId);
            }
        }

        private bool TryLoadStageMapScene(LocationSceneRouteResult result)
        {
            string targetSceneName = string.IsNullOrEmpty(stageMapSceneName)
                ? "StageHubMapScene"
                : stageMapSceneName;
            if (!useUnitySceneLoadingForLatestMaps
                || string.IsNullOrEmpty(targetSceneName)
                || SceneManager.GetActiveScene().name == targetSceneName)
            {
                return false;
            }

            if (!CanLoadScene(targetSceneName))
            {
                result.message = "已返回阶段地图状态，但入口场景未加入 Build Settings：" + targetSceneName;
                return false;
            }

            GameBootstrapper.SetSharedSession(sessionState);
            SceneManager.LoadScene(targetSceneName, LoadSceneMode.Single);
            return true;
        }

        private bool ShouldLoadUnityScene(StageMapLocationEntry entry)
        {
            return useUnitySceneLoadingForLatestMaps
                && entry != null
                && IsLatestMapLocation(entry.locationId)
                && !string.IsNullOrEmpty(entry.sceneName);
        }

        private static bool IsLatestMapLocation(string locationId)
        {
            return locationId == "location_home"
                || locationId == "location_school"
                || locationId == "location_shop"
                || locationId == "location_library"
                || locationId == "location_park"
                || locationId == "location_club";
        }

        private bool IsOutsideStageHubRouteRequest()
        {
            if (!Application.isPlaying)
            {
                return false;
            }

            string activeSceneName = SceneManager.GetActiveScene().name;
            string hubSceneName = string.IsNullOrEmpty(stageMapSceneName)
                ? "StageHubMapScene"
                : stageMapSceneName;
            return activeSceneName != hubSceneName && activeSceneName != "StageHubMapScene";
        }

        private static bool CanLoadScene(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName) && Application.CanStreamedLevelBeLoaded(sceneName);
        }

        private int GetRemainingTime()
        {
            return sessionState == null ? 0 : sessionState.stageState.remainingTime;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (values != null && !string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
