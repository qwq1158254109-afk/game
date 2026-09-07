using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class LocationSceneRouterTests
    {
        private GameObject gameObject;
        private GameSessionState sessionState;
        private StageController stageController;
        private SceneTransitionController sceneTransitionController;
        private LocationSceneRouter router;
        private readonly List<Object> createdObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("LocationSceneRouterTests");
            stageController = gameObject.AddComponent<StageController>();
            sceneTransitionController = gameObject.AddComponent<SceneTransitionController>();
            router = gameObject.AddComponent<LocationSceneRouter>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void EnterUnlockedLocationDoesNotConsumeTime()
        {
            StartStage("stage_01_adaptation", 6);

            LocationSceneRouteResult result = router.EnterLocationScene("location_home");

            Assert.IsTrue(result.success);
            Assert.AreEqual(6, result.remainingTimeBefore);
            Assert.AreEqual(6, result.remainingTimeAfter);
            Assert.AreEqual(6, sessionState.stageState.remainingTime);
            Assert.AreEqual("location_home", sessionState.locationState.currentLocationId);
            Assert.IsFalse(sceneTransitionController.isShowingMap);
        }

        [Test]
        public void ReturnToStageMapDoesNotConsumeTimeOrResetPlayerState()
        {
            StartStage("stage_01_adaptation", 6);
            sessionState.playerState.SetStat(PlayerStatKey.Money, 33);
            router.EnterLocationScene("location_school");

            LocationSceneRouteResult result = router.ReturnToStageMap();

            Assert.IsTrue(result.success);
            Assert.AreEqual(6, result.remainingTimeBefore);
            Assert.AreEqual(6, result.remainingTimeAfter);
            Assert.AreEqual(6, sessionState.stageState.remainingTime);
            Assert.AreEqual(33, sessionState.playerState.money);
            Assert.IsTrue(sceneTransitionController.isShowingMap);
            Assert.AreEqual(string.Empty, sessionState.locationState.currentLocationId);
        }

        [Test]
        public void LockedLocationCannotEnter()
        {
            StartStage("stage_01_adaptation", 6);

            LocationSceneRouteResult result = router.EnterLocationScene("location_library");

            Assert.IsFalse(result.success);
            Assert.AreEqual(StageMapLocationStatus.Locked, result.status);
            Assert.IsTrue(sceneTransitionController.isShowingMap);
            Assert.AreEqual(string.Empty, sessionState.locationState.currentLocationId);
            Assert.AreEqual(6, sessionState.stageState.remainingTime);
        }

        [Test]
        public void ComingSoonLocationCannotEnter()
        {
            StartStage("stage_01_adaptation", 6);

            LocationSceneRouteResult result = router.EnterLocationScene("location_shop");

            Assert.IsFalse(result.success);
            Assert.AreEqual(StageMapLocationStatus.ComingSoon, result.status);
            Assert.IsTrue(sceneTransitionController.isShowingMap);
            Assert.AreEqual(6, sessionState.stageState.remainingTime);
        }

        [Test]
        public void StageMapUnlockedLocationOverridesLegacyLocationLock()
        {
            StartStage("stage_02_month_exam", 6);

            LocationSceneRouteResult result = router.EnterLocationScene("location_library");

            Assert.IsTrue(result.success);
            Assert.AreEqual("location_library", sessionState.locationState.currentLocationId);
            Assert.AreEqual(6, sessionState.stageState.remainingTime);
        }

        private void StartStage(string stageId, int timeLimit)
        {
            sessionState = new GameSessionState();
            stageController.SetSessionState(sessionState);
            sceneTransitionController.SetSessionState(sessionState);

            StageData stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageId = stageId;
            stage.displayName = stageId;
            stage.timeLimit = timeLimit;
            stage.isFinalStage = stageId == "stage_03_final_sprint";
            createdObjects.Add(stage);

            stageController.StartStage(stage);

            List<LocationData> locations = MvpSampleDataFactory.CreateDefaultLocations();
            foreach (LocationData location in locations)
            {
                createdObjects.Add(location);
            }

            sceneTransitionController.SetLocations(locations);
            sceneTransitionController.ShowMap();

            StageMapData mapData = MvpSampleDataFactory.CreateDefaultStageMapData(stage);
            createdObjects.Add(mapData);
            router.Configure(sessionState, stageController, sceneTransitionController, mapData);
        }
    }
}
