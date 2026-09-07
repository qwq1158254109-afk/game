using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class StageMapLocationAvailabilityServiceTests
    {
        private readonly StageMapLocationAvailabilityService service = new StageMapLocationAvailabilityService();
        private readonly List<Object> createdObjects = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
            {
                Object.DestroyImmediate(createdObject);
            }

            createdObjects.Clear();
        }

        [Test]
        public void StageOneLocationStatusesMatchMapRules()
        {
            StageContext context = CreateStageContext("stage_01_adaptation");

            AssertStatus(context, "location_home", StageMapLocationStatus.Unlocked);
            AssertStatus(context, "location_school", StageMapLocationStatus.Unlocked);
            AssertStatus(context, "location_park", StageMapLocationStatus.Unlocked);
            AssertStatus(context, "location_library", StageMapLocationStatus.Unlocked);
            AssertStatus(context, "location_shop", StageMapLocationStatus.ComingSoon);
            AssertStatus(context, "location_club", StageMapLocationStatus.Locked);
        }

        [Test]
        public void StageTwoUnlocksLibraryAndKeepsShopComingSoon()
        {
            StageContext context = CreateStageContext("stage_02_month_exam");

            AssertStatus(context, "location_home", StageMapLocationStatus.Unlocked);
            AssertStatus(context, "location_school", StageMapLocationStatus.Unlocked);
            AssertStatus(context, "location_park", StageMapLocationStatus.Unlocked);
            AssertStatus(context, "location_library", StageMapLocationStatus.Unlocked);
            AssertStatus(context, "location_shop", StageMapLocationStatus.ComingSoon);
            AssertStatus(context, "location_club", StageMapLocationStatus.Locked);
        }

        [Test]
        public void StageThreeClubUnlocksWhenInterestOrSocialIsHighEnough()
        {
            StageContext context = CreateStageContext("stage_03_final_sprint");

            AssertStatus(context, "location_club", StageMapLocationStatus.Locked);

            context.sessionState.playerState.SetStat(PlayerStatKey.Social, 45);

            AssertStatus(context, "location_club", StageMapLocationStatus.Unlocked);
        }

        private StageContext CreateStageContext(string stageId)
        {
            StageData stage = ScriptableObject.CreateInstance<StageData>();
            stage.stageId = stageId;
            stage.displayName = stageId;
            stage.isFinalStage = stageId == "stage_03_final_sprint";
            createdObjects.Add(stage);

            GameSessionState sessionState = new GameSessionState();
            sessionState.stageState.currentStage = stage;

            StageMapData mapData = MvpSampleDataFactory.CreateDefaultStageMapData(stage);
            createdObjects.Add(mapData);

            return new StageContext
            {
                sessionState = sessionState,
                mapData = mapData
            };
        }

        private void AssertStatus(StageContext context, string locationId, StageMapLocationStatus expected)
        {
            StageMapLocationEntry entry = FindEntry(context.mapData, locationId);
            StageMapLocationStatus status = service.ResolveStatus(entry, context.sessionState, null, null);
            Assert.AreEqual(expected, status, locationId);
        }

        private static StageMapLocationEntry FindEntry(StageMapData mapData, string locationId)
        {
            foreach (StageMapLocationEntry entry in mapData.locations)
            {
                if (entry.locationId == locationId)
                {
                    return entry;
                }
            }

            return null;
        }

        private sealed class StageContext
        {
            public GameSessionState sessionState;
            public StageMapData mapData;
        }
    }
}
