using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class SceneTransitionControllerTests
    {
        private GameObject gameObject;
        private SceneTransitionController controller;
        private readonly List<Object> createdObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("SceneTransitionControllerTests");
            controller = gameObject.AddComponent<SceneTransitionController>();
            controller.SetSessionState(new GameSessionState());
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
        public void GoToLocationUpdatesCurrentLocationId()
        {
            LocationData home = CreateLocation("location_home", LocationType.Home, true);
            controller.SetLocations(new[] { home });

            controller.GoToLocation(home);

            Assert.AreEqual("location_home", controller.sessionState.locationState.currentLocationId);
            Assert.AreEqual(home, controller.currentLocation);
            Assert.IsFalse(controller.isShowingMap);
        }

        [Test]
        public void ShowMapDoesNotChangePlayerState()
        {
            LocationData school = CreateLocation("location_school", LocationType.School, true);
            controller.sessionState.playerState.money = 123;
            controller.SetLocations(new[] { school });
            controller.GoToLocation(school);

            controller.ShowMap();

            Assert.IsTrue(controller.isShowingMap);
            Assert.AreEqual(123, controller.sessionState.playerState.money);
        }

        [Test]
        public void LockedLocationCannotEnterUntilConditionMet()
        {
            LocationData club = CreateLocation("location_club", LocationType.Club, false);
            club.unlockConditions.Add(new EventConditionData
            {
                statKey = PlayerStatKey.Interest,
                comparison = ComparisonType.GreaterOrEqual,
                value = 80
            });
            controller.SetLocations(new[] { club });

            Assert.IsFalse(controller.TryGoToLocation(club, out string lockedReason));
            Assert.IsFalse(string.IsNullOrEmpty(lockedReason));

            controller.sessionState.playerState.SetStat(PlayerStatKey.Interest, 80);

            Assert.IsTrue(controller.TryGoToLocation(club, out _));
            Assert.AreEqual("location_club", controller.sessionState.locationState.currentLocationId);
        }

        [Test]
        public void DefaultLocationsIncludeCoreMapLocations()
        {
            List<LocationData> locations = MvpSampleDataFactory.CreateDefaultLocations();

            Assert.IsTrue(ContainsLocation(locations, "location_home"));
            Assert.IsTrue(ContainsLocation(locations, "location_school"));
            Assert.IsTrue(ContainsLocation(locations, "location_shop"));
            Assert.IsTrue(ContainsLocation(locations, "location_park"));
        }

        private LocationData CreateLocation(string id, LocationType type, bool unlocked)
        {
            LocationData location = ScriptableObject.CreateInstance<LocationData>();
            location.locationId = id;
            location.displayName = id;
            location.locationType = type;
            location.unlockedByDefault = unlocked;
            createdObjects.Add(location);
            return location;
        }

        private static bool ContainsLocation(IEnumerable<LocationData> locations, string locationId)
        {
            foreach (LocationData location in locations)
            {
                if (location != null && location.locationId == locationId)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
