using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public class SceneTransitionController : MonoBehaviour
    {
        public SceneTransitionMode transitionMode = SceneTransitionMode.PanelSwitch;
        public GameSessionState sessionState = new GameSessionState();
        public LocationData currentLocation;
        public bool isShowingMap = true;

        private readonly List<LocationData> locations = new List<LocationData>();
        private readonly List<LocationData> locationHistory = new List<LocationData>();
        private readonly Dictionary<LocationType, LocationView> registeredViews = new Dictionary<LocationType, LocationView>();

        public void SetSessionState(GameSessionState state)
        {
            sessionState = state ?? new GameSessionState();
        }

        public void SetLocations(IEnumerable<LocationData> locationData)
        {
            locations.Clear();
            if (locationData == null)
            {
                return;
            }

            foreach (LocationData location in locationData)
            {
                if (location != null)
                {
                    locations.Add(location);
                    if (location.unlockedByDefault)
                    {
                        AddUnique(sessionState.locationState.unlockedLocationIds, location.locationId);
                    }
                }
            }
        }

        public IReadOnlyList<LocationData> GetKnownLocations()
        {
            return locations;
        }

        public void ShowMap()
        {
            if (AudioSettingsController.DisplayTransitionBlocked || PauseMenuRuntime.BlocksGameplayNavigation) return;
            isShowingMap = true;
            currentLocation = null;
            sessionState.locationState.currentLocationId = string.Empty;
        }

        public void GoToLocation(LocationData locationData)
        {
            TryGoToLocation(locationData, out _);
        }

        public bool TryGoToLocation(LocationData locationData, out string reason)
        {
            if (AudioSettingsController.DisplayTransitionBlocked || PauseMenuRuntime.BlocksGameplayNavigation) { reason = "请先确认或恢复显示模式。"; return false; }
            reason = string.Empty;
            if (!CanEnterLocation(locationData))
            {
                reason = GetLockedReason(locationData);
                return false;
            }

            if (currentLocation != null)
            {
                locationHistory.Add(currentLocation);
            }

            currentLocation = locationData;
            isShowingMap = false;
            sessionState.locationState.currentLocationId = locationData.locationId;
            AddUnique(sessionState.locationState.visitedLocationIds, locationData.locationId);
            AddUnique(sessionState.locationState.unlockedLocationIds, locationData.locationId);
            return true;
        }

        public void ReturnToPreviousLocation()
        {
            if (AudioSettingsController.DisplayTransitionBlocked || PauseMenuRuntime.BlocksGameplayNavigation) return;
            if (locationHistory.Count == 0)
            {
                ShowMap();
                return;
            }

            currentLocation = locationHistory[locationHistory.Count - 1];
            locationHistory.RemoveAt(locationHistory.Count - 1);
            isShowingMap = false;
            sessionState.locationState.currentLocationId = currentLocation.locationId;
        }

        public void RefreshCurrentLocation()
        {
            if (currentLocation == null || CanEnterLocation(currentLocation))
            {
                return;
            }

            ShowMap();
        }

        public bool CanEnterLocation(LocationData locationData)
        {
            if (locationData == null || string.IsNullOrEmpty(locationData.locationId))
            {
                return false;
            }

            if (!EventConditionResolver.AreConditionsMet(locationData.appearConditions, sessionState.playerState))
            {
                return false;
            }

            if (locationData.unlockedByDefault || sessionState.locationState.unlockedLocationIds.Contains(locationData.locationId))
            {
                return true;
            }

            if (locationData.unlockConditions == null || locationData.unlockConditions.Count == 0)
            {
                return false;
            }

            return EventConditionResolver.AreConditionsMet(locationData.unlockConditions, sessionState.playerState);
        }

        public string GetLockedReason(LocationData locationData)
        {
            if (locationData == null)
            {
                return "地点不存在。";
            }

            if (!EventConditionResolver.AreConditionsMet(locationData.appearConditions, sessionState.playerState))
            {
                return "当前阶段暂不出现。";
            }

            if (!EventConditionResolver.AreConditionsMet(locationData.unlockConditions, sessionState.playerState))
            {
                return "解锁条件未满足。";
            }

            return "地点暂未开放。";
        }

        public void RegisterLocationView(LocationType type, LocationView view)
        {
            if (view == null)
            {
                registeredViews.Remove(type);
                return;
            }

            registeredViews[type] = view;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (!string.IsNullOrEmpty(value) && !values.Contains(value))
            {
                values.Add(value);
            }
        }
    }
}
