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
            // 2026-10-28（T7）：EditMode 里 SceneManager.LoadScene 会抛
            // 「This can only be used during play mode」。这里关掉 Unity 场景加载，
            // 只验路由决策本身（时间不消耗 / 状态判定 / currentLocationId 落盘）——
            // 这些才是这些用例要锁的契约，真加载场景由 PlayMode 覆盖。
            router.useUnitySceneLoadingForLatestMaps = false;
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

            LocationSceneRouteResult result = router.EnterLocationScene("location_club");

            // 2026-10-28（T7）：图书馆三个学段都是 Unlocked，改用社团（stage_01 锁）。
            Assert.IsFalse(result.success, result.message);
            Assert.AreEqual(StageMapLocationStatus.Locked, result.status);
            Assert.IsTrue(sceneTransitionController.isShowingMap);
            Assert.AreEqual(string.Empty, sessionState.locationState.currentLocationId);
            Assert.AreEqual(6, sessionState.stageState.remainingTime);
        }

        [Test]
        public void UnknownLocationIsRejected()
        {
            StartStage("stage_01_adaptation", 6);

            // 2026-10-28（T7）：原来这条是 ComingSoonLocationCannotEnter，但商店已实装、
            // 三个学段都是 Unlocked，整个数据里已经没有 ComingSoon 地点了。
            // 改为锁「未知地点不会把玩家送进任何场景」—— 防 FindEntry 返回 null 时的空引用。
            LocationSceneRouteResult result = router.EnterLocationScene("location_does_not_exist");

            Assert.IsFalse(result.success, result.message);
            Assert.AreEqual(StageMapLocationStatus.Hidden, result.status);
            Assert.IsTrue(sceneTransitionController.isShowingMap);
            Assert.AreEqual(6, sessionState.stageState.remainingTime);
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
