using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class StageHubMapSceneController : MonoBehaviour
    {
        public GameSessionState sessionState;
        public StageController stageController;
        public TimeActionController timeActionController;
        public TimeActionHudView timeActionHudView;
        public SceneTransitionController sceneTransitionController;
        public LocationSceneRouter locationSceneRouter;
        public StageMapData stageMapData;
        public bool autoFindRuntimeServices = true;

        private readonly StageMapLocationAvailabilityService availabilityService = new StageMapLocationAvailabilityService();

        private void Awake()
        {
            RefreshPortals();
        }

        private void Start()
        {
            RefreshPortals();
        }

        public void RefreshPortals()
        {
            ResolveRuntimeServices();
            EnsureStageMapData();
            EnsureTimeActionHud();

            MapLocationPortal[] portals = FindObjectsByType<MapLocationPortal>(FindObjectsInactive.Exclude);
            for (int i = 0; i < portals.Length; i++)
            {
                MapLocationPortal portal = portals[i];
                if (portal == null)
                {
                    continue;
                }

                StageMapLocationEntry entry = FindEntry(portal.locationId);
                StageMapLocationStatus status = availabilityService.ResolveStatus(
                    entry,
                    sessionState,
                    stageController,
                    sceneTransitionController);
                string reason = availabilityService.ResolveReason(entry, status);
                portal.ApplyStatus(status, reason);
            }
        }

        private void ResolveRuntimeServices()
        {
            if (!autoFindRuntimeServices)
            {
                return;
            }

            // A new game/load owns one shared session. Serialized hub defaults must not
            // rebind the clock to a different session when Awake/Start order varies.
            if (GameBootstrapper.RestoringSession != null) sessionState = GameBootstrapper.RestoringSession;
            else if (GameBootstrapper.SharedSession != null) sessionState = GameBootstrapper.SharedSession;

            if (stageController == null)
            {
                stageController = FindAnyObjectByType<StageController>();
            }

            if (timeActionController == null)
            {
                timeActionController = FindAnyObjectByType<TimeActionController>();
            }

            if (sceneTransitionController == null)
            {
                sceneTransitionController = FindAnyObjectByType<SceneTransitionController>();
            }

            if (locationSceneRouter == null)
            {
                locationSceneRouter = FindAnyObjectByType<LocationSceneRouter>();
            }

            if (sessionState == null && stageController != null)
            {
                sessionState = stageController.sessionState;
            }

            if (sessionState == null && sceneTransitionController != null)
            {
                sessionState = sceneTransitionController.sessionState;
            }

            if (timeActionController == null)
            {
                timeActionController = gameObject.AddComponent<TimeActionController>();
            }

            if (sessionState != null)
            {
                timeActionController.SetSessionState(sessionState);
            }

            if (stageMapData == null && locationSceneRouter != null)
            {
                stageMapData = locationSceneRouter.stageMapData;
            }
        }

        private void EnsureTimeActionHud()
        {
            if (timeActionHudView == null)
            {
                StageHubSelectionController selectionController =
                    FindAnyObjectByType<StageHubSelectionController>(FindObjectsInactive.Include);
                if (selectionController != null)
                {
                    timeActionHudView = selectionController.GetComponentInChildren<TimeActionHudView>(true);
                }
            }

            if (timeActionHudView != null)
            {
                timeActionHudView.SetTopLeftLayout();
                timeActionHudView.Configure(sessionState, timeActionController);
            }
            else
            {
                Debug.LogWarning("StageHub 未找到 Layer_04 中预置的 TimeActionHud。", this);
            }
        }

        private void EnsureStageMapData()
        {
            if (stageMapData != null)
            {
                return;
            }

            StageData stage = sessionState == null ? null : sessionState.stageState.currentStage;
            stageMapData = MvpSampleDataFactory.CreateDefaultStageMapData(stage);
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
    }
}
