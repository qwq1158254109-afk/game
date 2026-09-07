using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class LocationActionExecutorTests
    {
        private GameObject gameObject;
        private GameSessionState sessionState;
        private StageController stageController;
        private ActivityController activityController;
        private MiniGameController miniGameController;
        private SceneTransitionController sceneTransitionController;
        private LocationActionExecutor executor;
        private readonly List<Object> createdObjects = new List<Object>();
        private readonly List<GameObject> createdGameObjects = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("LocationActionExecutorTests");
            stageController = gameObject.AddComponent<StageController>();
            activityController = gameObject.AddComponent<ActivityController>();
            miniGameController = gameObject.AddComponent<MiniGameController>();
            sceneTransitionController = gameObject.AddComponent<SceneTransitionController>();
            executor = gameObject.AddComponent<LocationActionExecutor>();
        }

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
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void LocationActionExecutor_LegacyAndMapSource_ProduceSameResult()
        {
            ActivityData legacyActivity = CreateRewardActivity("activity_shared", 2, PlayerStatKey.Math, 5);
            StartSession(legacyActivity, 6, out _, out _);
            LocationActionExecutionResult legacyResult = executor.TryExecute(new LocationActionExecutionRequest
            {
                source = LocationActionSource.LegacyButton,
                activity = legacyActivity
            });
            int legacyRemainingTime = sessionState.stageState.remainingTime;
            int legacyMath = sessionState.playerState.math;

            ActivityData mapActivity = CreateRewardActivity("activity_shared", 2, PlayerStatKey.Math, 5);
            StartSession(mapActivity, 6, out LocationData mapLocation, out LocationActionData mapAction);
            LocationActionExecutionResult mapResult = executor.TryExecute(new LocationActionExecutionRequest
            {
                source = LocationActionSource.Map,
                locationId = mapLocation.locationId,
                actionId = mapAction.actionId
            });

            Assert.IsTrue(legacyResult.success);
            Assert.IsTrue(mapResult.success);
            Assert.AreEqual(legacyRemainingTime, sessionState.stageState.remainingTime);
            Assert.AreEqual(legacyMath, sessionState.playerState.math);
            Assert.AreEqual(legacyResult.activityResult.success, mapResult.activityResult.success);
        }

        [Test]
        public void LocationActionExecutor_MapInteraction_ConsumesTimeOnce()
        {
            ActivityData activity = CreateRewardActivity("activity_once", 1, PlayerStatKey.English, 3);
            StartSession(activity, 3, out LocationData location, out LocationActionData action);

            LocationActionExecutionResult result = executor.TryExecute(new LocationActionExecutionRequest
            {
                source = LocationActionSource.Map,
                locationId = location.locationId,
                actionId = action.actionId
            });

            Assert.IsTrue(result.success);
            Assert.AreEqual(3, result.remainingTimeBefore);
            Assert.AreEqual(2, result.remainingTimeAfter);
            Assert.AreEqual(2, sessionState.stageState.remainingTime);
        }

        [Test]
        public void MapInteractionController_ReturnToMainUi_DoesNotResetSession()
        {
            sessionState = new GameSessionState();
            sessionState.playerState.SetStat(PlayerStatKey.Money, 123);
            sessionState.playerState.SetStat(PlayerStatKey.Energy, 44);
            sceneTransitionController.SetSessionState(sessionState);

            LocationData location = CreateLocation();
            sceneTransitionController.SetLocations(new[] { location });
            sceneTransitionController.GoToLocation(location);

            MapInteractionController mapController = gameObject.AddComponent<MapInteractionController>();
            mapController.sceneTransitionController = sceneTransitionController;
            MapInteractionPoint returnPoint = CreateMapInteractionPoint(MapInteractionType.ReturnToMainUi);

            LocationActionExecutionResult result = mapController.TryInteract(returnPoint);

            Assert.IsTrue(result.success);
            Assert.IsTrue(result.shouldReturnToMap);
            Assert.AreEqual("已返回阶段地图。返回不消耗时间。", result.message);
            Assert.IsTrue(sceneTransitionController.isShowingMap);
            Assert.AreEqual(123, sessionState.playerState.money);
            Assert.AreEqual(44, sessionState.playerState.energy);
        }

        private void StartSession(
            ActivityData activity,
            int timeLimit,
            out LocationData location,
            out LocationActionData action)
        {
            sessionState = new GameSessionState();
            stageController.SetSessionState(sessionState);
            activityController.SetSessionState(sessionState);
            sceneTransitionController.SetSessionState(sessionState);
            executor.Configure(
                sessionState,
                stageController,
                activityController,
                miniGameController,
                sceneTransitionController);

            StageData stage = CreateStage(timeLimit);
            stage.availableActivities.Add(activity);
            stageController.StartStage(stage);

            location = CreateLocation();
            action = CreateLocationAction(activity);
            location.actions.Add(action);
            sceneTransitionController.SetLocations(new[] { location });
        }

        private StageData CreateStage(int timeLimit)
        {
            StageData stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageId = "stage_test";
            stage.displayName = "Test Stage";
            stage.timeLimit = timeLimit;
            createdObjects.Add(stage);
            return stage;
        }

        private ActivityData CreateRewardActivity(string activityId, int timeCost, PlayerStatKey statKey, int value)
        {
            ActivityData activity = ScriptableObject.CreateInstance<ActivityData>();
            activity.activityId = activityId;
            activity.displayName = activityId;
            activity.activityType = ActivityType.Training;
            activity.timeCost = timeCost;
            activity.fixedRewards.Add(new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = statKey,
                value = value
            });
            createdObjects.Add(activity);
            return activity;
        }

        private LocationData CreateLocation()
        {
            LocationData location = ScriptableObject.CreateInstance<LocationData>();
            location.locationId = "location_test";
            location.displayName = "Test Location";
            location.locationType = LocationType.School;
            location.unlockedByDefault = true;
            createdObjects.Add(location);
            return location;
        }

        private LocationActionData CreateLocationAction(ActivityData activity)
        {
            LocationActionData action = ScriptableObject.CreateInstance<LocationActionData>();
            action.actionId = "action_test";
            action.displayName = "Test Action";
            action.actionType = LocationActionType.StartActivity;
            action.linkedActivity = activity;
            createdObjects.Add(action);
            return action;
        }

        private MapInteractionPoint CreateMapInteractionPoint(MapInteractionType interactionType)
        {
            GameObject pointObject = new GameObject("MapInteractionPointTests");
            createdGameObjects.Add(pointObject);
            MapInteractionPoint point = pointObject.AddComponent<MapInteractionPoint>();
            point.interactionType = interactionType;
            point.locationId = "location_test";
            point.actionId = "action_test";
            return point;
        }
    }
}
