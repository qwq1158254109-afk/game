using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public sealed class RpgMapSceneComponentTests
    {
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly List<GameObject> createdGameObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdGameObject in createdGameObjects)
            {
                Object.DestroyImmediate(createdGameObject);
            }

            createdGameObjects.Clear();

            foreach (Object createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void FullscreenMapSceneController_MovesPlayerToSpawnWithoutChangingState()
        {
            GameSessionState state = new GameSessionState();
            state.stageState.remainingTime = 6;
            state.playerState.SetStat(PlayerStatKey.Energy, 44);
            GameObject playerObject = CreateGameObject("Player");
            RpgPlayerController player = playerObject.AddComponent<RpgPlayerController>();
            GameObject spawnObject = CreateGameObject("Spawn");
            spawnObject.transform.position = new Vector3(2f, -1f, 0f);
            PlayerSpawnPoint spawn = spawnObject.AddComponent<PlayerSpawnPoint>();
            spawn.spawnPointId = "home_entry";

            GameObject controllerObject = CreateGameObject("MapController");
            FullscreenMapSceneController controller = controllerObject.AddComponent<FullscreenMapSceneController>();
            controller.autoFindRuntimeServices = false;
            controller.autoCollectTargets = false;
            controller.sessionState = state;
            controller.player = playerObject.transform;
            controller.playerController = player;
            controller.defaultSpawnPointId = "home_entry";

            controller.MovePlayerToSpawn("home_entry");

            Assert.AreEqual(new Vector2(2f, -1f), new Vector2(playerObject.transform.position.x, playerObject.transform.position.y));
            Assert.AreEqual(6, state.stageState.remainingTime);
            Assert.AreEqual(44, state.playerState.energy);
        }

        [Test]
        public void MapInteractionTargetFallbackActivity_ExecutesThroughLocationActionExecutor()
        {
            GameObject root = CreateGameObject("Runtime");
            StageController stageController = root.AddComponent<StageController>();
            ActivityController activityController = root.AddComponent<ActivityController>();
            MiniGameController miniGameController = root.AddComponent<MiniGameController>();
            SceneTransitionController sceneTransitionController = root.AddComponent<SceneTransitionController>();
            LocationActionExecutor executor = root.AddComponent<LocationActionExecutor>();
            GameSessionState state = new GameSessionState();
            stageController.SetSessionState(state);
            activityController.SetSessionState(state);
            sceneTransitionController.SetSessionState(state);
            executor.Configure(state, stageController, activityController, miniGameController, sceneTransitionController);

            // 2026-10-28（T7）：原来用「早点休息」验扣时间，但睡眠活动走的是换日结算
            // （LocationActionExecutor 传 consumeStageTime = !isSleepActivity），本来就不扣
            // 行动点，断言 3-1=2 一直是不可能成立的期望。换成普通活动。
            ActivityData activity = CreateActivity("activity_family_dinner", "家庭晚饭聊天", 1);
            StageData stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageId = "stage_test";
            stage.displayName = "测试阶段";
            stage.timeLimit = 3;
            stage.availableActivities.Add(activity);
            createdObjects.Add(stage);
            stageController.StartStage(stage);

            GameObject targetObject = CreateGameObject("BedTarget");
            MapInteractionPoint point = targetObject.AddComponent<MapInteractionPoint>();
            point.locationId = "location_home";
            point.actionId = "activity_family_dinner";
            MapInteractionTarget target = targetObject.AddComponent<MapInteractionTarget>();
            target.interactionPoint = point;
            target.fallbackActivity = activity;

            MapInteractionController mapController = root.AddComponent<MapInteractionController>();
            mapController.locationActionExecutor = executor;

            LocationActionExecutionResult result = mapController.TryInteract(target);

            Assert.IsTrue(result.success, "活动执行失败：" + result.message);
            Assert.AreEqual(2, state.stageState.remainingTime,
                "活动消耗 1 点时间，3 - 1 应为 2，实际 " + state.stageState.remainingTime);
            Assert.IsTrue(result.timeAdvanced);
        }

        [Test]
        public void MapInteractionController_EnterLocationPortal_UsesLocationSceneRouterWithoutConsumingTime()
        {
            GameObject root = CreateGameObject("Runtime");
            StageController stageController = root.AddComponent<StageController>();
            SceneTransitionController sceneTransitionController = root.AddComponent<SceneTransitionController>();
            LocationSceneRouter router = root.AddComponent<LocationSceneRouter>();
            MapInteractionController mapController = root.AddComponent<MapInteractionController>();
            GameSessionState state = new GameSessionState();
            stageController.SetSessionState(state);
            sceneTransitionController.SetSessionState(state);

            StageData stage = CreateStage("stage_01_adaptation", 6);
            stageController.StartStage(stage);
            List<LocationData> locations = MvpSampleDataFactory.CreateDefaultLocations();
            foreach (LocationData location in locations)
            {
                createdObjects.Add(location);
            }

            sceneTransitionController.SetLocations(locations);
            StageMapData mapData = MvpSampleDataFactory.CreateDefaultStageMapData(stage);
            createdObjects.Add(mapData);
            router.Configure(state, stageController, sceneTransitionController, mapData);
            mapController.locationSceneRouter = router;

            GameObject portalObject = CreateGameObject("HomePortal");
            MapInteractionPoint point = portalObject.AddComponent<MapInteractionPoint>();
            point.locationId = "location_home";
            point.interactionType = MapInteractionType.EnterLocationScene;

            LocationActionExecutionResult result = mapController.TryInteract(point);

            Assert.IsTrue(result.success, "进入地点失败：" + result.message);
            Assert.AreEqual(6, result.remainingTimeBefore);
            Assert.AreEqual(6, result.remainingTimeAfter);
            Assert.AreEqual("location_home", state.locationState.currentLocationId);
        }

        [Test]
        public void MapInteractionPromptView_BuildsActivityPromptFromFallbackActivity()
        {
            ActivityData activity = CreateActivity("activity_family_dinner", "家庭晚饭聊天", 1);
            activity.fixedRewards.Add(new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.ParentRelation,
                value = 5
            });
            GameObject targetObject = CreateGameObject("FamilyTarget");
            MapInteractionTarget target = targetObject.AddComponent<MapInteractionTarget>();
            target.displayName = "家庭晚饭聊天";
            target.fallbackActivity = activity;

            string prompt = MapInteractionPromptView.BuildPromptForTarget(target);

            Assert.IsTrue(prompt.Contains("家庭晚饭聊天"));
            Assert.IsTrue(prompt.Contains("消耗 1 小时"));
            Assert.IsTrue(prompt.Contains("亲子+5"));
        }

        [Test]
        public void RpgPlayerController_SetFacingDirection_UsesCardinalDirection()
        {
            GameObject playerObject = CreateGameObject("Player");
            RpgPlayerController player = playerObject.AddComponent<RpgPlayerController>();

            player.SetFacingDirection(new Vector2(0.2f, 1f));

            Assert.AreEqual(Vector2.up, player.FacingDirection);
        }

        [Test]
        public void RpgPlayerController_ClampMovePosition_RespectsMapBounds()
        {
            GameObject playerObject = CreateGameObject("Player");
            RpgPlayerController player = playerObject.AddComponent<RpgPlayerController>();
            player.useMovementBounds = true;
            player.movementMin = new Vector2(-1f, -2f);
            player.movementMax = new Vector2(3f, 4f);

            Vector2 clamped = player.ClampMovePosition(new Vector2(9f, -8f));

            Assert.AreEqual(new Vector2(3f, -2f), clamped);
        }

        [Test]
        public void StageHubMapSceneController_RefreshPortals_AppliesLockedPrompt()
        {
            StageData stage = CreateStage("stage_01_adaptation", 6);
            GameSessionState state = new GameSessionState();
            state.stageState.currentStage = stage;
            StageMapData mapData = MvpSampleDataFactory.CreateDefaultStageMapData(stage);
            createdObjects.Add(mapData);

            GameObject portalObject = CreateGameObject("LibraryPortal");
            MapInteractionPoint point = portalObject.AddComponent<MapInteractionPoint>();
            MapInteractionTarget target = portalObject.AddComponent<MapInteractionTarget>();
            MapLocationPortal portal = portalObject.AddComponent<MapLocationPortal>();
            // 2026-10-28（T7）：图书馆三个学段都是 Unlocked，原来用它在 stage_01
            // 验「锁定提示」已经验不到东西了。改用社团（stage_01 锁、第三学段开放）。
            portal.locationId = "location_club";
            portal.displayName = "社团活动室";
            target.interactionPoint = point;

            GameObject controllerObject = CreateGameObject("StageHubController");
            StageHubMapSceneController controller = controllerObject.AddComponent<StageHubMapSceneController>();
            controller.autoFindRuntimeServices = false;
            controller.sessionState = state;
            controller.stageMapData = mapData;

            controller.RefreshPortals();

            Assert.AreEqual(StageMapLocationStatus.Locked, portal.CurrentStatus);
            Assert.IsTrue(target.promptText.Contains("第三学段起开放"), "锁定提示应带出 lockedReason");
        }

        private ActivityData CreateActivity(string activityId, string displayName, int timeCost)
        {
            ActivityData activity = ScriptableObject.CreateInstance<ActivityData>();
            activity.activityId = activityId;
            activity.displayName = displayName;
            activity.activityType = ActivityType.Rest;
            activity.timeCost = timeCost;
            createdObjects.Add(activity);
            return activity;
        }

        private StageData CreateStage(string stageId, int timeLimit)
        {
            StageData stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageId = stageId;
            stage.displayName = stageId;
            stage.timeLimit = timeLimit;
            stage.isFinalStage = stageId == "stage_03_final_sprint";
            createdObjects.Add(stage);
            return stage;
        }

        private GameObject CreateGameObject(string name)
        {
            GameObject gameObject = new GameObject(name);
            createdGameObjects.Add(gameObject);
            return gameObject;
        }
    }
}
