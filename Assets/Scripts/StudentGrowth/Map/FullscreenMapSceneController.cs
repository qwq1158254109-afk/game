using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class FullscreenMapSceneController : MonoBehaviour
    {
        private const string ExplorationMapIdPrefix = "location_";
        private const string ExplorationHudThemeResourcePath = "StudentGrowth/UI/SchoolMapHudTheme";

        public string mapId = "location_home";
        public string displayName = "家里";
        public string defaultSpawnPointId = "home_entry";
        public Transform player;
        public RpgPlayerController playerController;
        public PlayerFacingInteractor facingInteractor;
        public MapCameraController cameraController;
        public Collider2D cameraBounds;
        public FullscreenMapHudView hudView;
        public TimeActionHudView timeActionHudView;
        public LocationToastView locationToastView;
        public UIThemeConfig hudTheme;
        public MapInteractionPromptView promptView;
        public MapFunctionMenuView functionMenuView;
        public MapInteractionConfirmView confirmView;
        public MapDialogueView dialogueView;
        public MapShopView shopView;
        public MapActivityResultView resultView;
        public MapInteractionController interactionController;
        public LocationSceneRouter locationSceneRouter;
        public SceneTransitionController sceneTransitionController;
        public LocationActionExecutor locationActionExecutor;
        public GameBootstrapper gameBootstrapper;
        public StageController stageController;
        public TimeActionController timeActionController;
        public ActivityController activityController;
        public ChallengeController challengeController;
        public MiniGameController miniGameController;
        public GameSessionState sessionState;
        public bool autoFindRuntimeServices = true;
        public bool createFallbackRuntimeServices = true;
        public bool autoCollectTargets = true;
        public bool createFallbackUi = true;
        public string fallbackStageMapSceneName = "StageHubMapScene";
        public MapInteractionTarget[] interactionTargets = new MapInteractionTarget[0];

        private bool initialized;
        private bool locationToastShown;

        private void Awake()
        {
            InitializeScene();
        }

        private void Start()
        {
            RefreshHud();
            ShowLocationToast();
            if (promptView != null)
            {
                promptView.ShowDefault();
            }
        }

        public void InitializeScene()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            ResolveSceneReferences();
            ResolveRuntimeServices();
            EnsureRuntimeServicesIfNeeded();
            CreateFallbackUiIfNeeded();
            CollectInteractionTargets();
            WireInteractionSystem();
            MovePlayerToSpawn(defaultSpawnPointId);
            if(GetComponent<BattleMapBridge>()==null)gameObject.AddComponent<BattleMapBridge>().Configure(this);
            RefreshHud();
        }

        public void MovePlayerToSpawn(string spawnPointId)
        {
            if (player == null)
            {
                return;
            }

            PlayerSpawnPoint spawnPoint = FindSpawnPoint(spawnPointId);
            if (spawnPoint == null)
            {
                return;
            }

            if (playerController != null)
            {
                playerController.TeleportTo(spawnPoint.transform.position);
            }
            else
            {
                player.position = spawnPoint.transform.position;
            }
        }

        public void RefreshHud()
        {
            ResolveRuntimeServices();
            bool explorationPresentation = IsExplorationMap();
            UIThemeConfig resolvedTheme = ResolveHudTheme();
            if (hudView != null)
            {
                hudView.ConfigureForMap(
                    sessionState,
                    sceneTransitionController,
                    playerController,
                    resolvedTheme,
                    explorationPresentation);
                hudView.SetLocationName(displayName);
                hudView.Refresh();
            }

            if (timeActionHudView != null)
            {
                timeActionHudView.Configure(
                    sessionState,
                    timeActionController,
                    resolvedTheme,
                    explorationPresentation,
                    explorationPresentation);
            }

            if (locationToastView != null)
            {
                locationToastView.Configure(resolvedTheme);
            }
        }

        public void ShowLocationToast()
        {
            if (locationToastShown || !IsExplorationMap() || locationToastView == null)
            {
                return;
            }

            StageData stage = sessionState == null || sessionState.stageState == null
                ? null
                : sessionState.stageState.currentStage;
            locationToastShown = true;
            locationToastView.Show(displayName, stage, "地图探索");
        }

        public void RebindPlayer(RpgPlayerController controller)
        {
            if (controller == null)
            {
                return;
            }

            playerController = controller;
            player = controller.transform;
            facingInteractor = controller.GetComponent<PlayerFacingInteractor>();
            ResolveSceneReferences();
            ResolveRuntimeServices();
            CollectInteractionTargets();
            WireInteractionSystem();
            RefreshHud();
        }

        public void ReturnToStageMap()
        {
            if (AudioSettingsController.DisplayTransitionBlocked || PauseMenuRuntime.BlocksGameplayNavigation) return;
            ResolveRuntimeServices();
            if (locationSceneRouter != null)
            {
                LocationSceneRouteResult result = locationSceneRouter.ReturnToStageMap();
                if (!result.didLoadUnityScene && promptView != null)
                {
                    promptView.ShowMessage(result.message);
                }

                return;
            }

            if (sceneTransitionController != null)
            {
                sceneTransitionController.ShowMap();
            }

            if (!string.IsNullOrEmpty(fallbackStageMapSceneName)
                && SceneManager.GetActiveScene().name != fallbackStageMapSceneName
                && Application.CanStreamedLevelBeLoaded(fallbackStageMapSceneName))
            {
                GameBootstrapper.SetSharedSession(sessionState);
                SceneManager.LoadScene(fallbackStageMapSceneName, LoadSceneMode.Single);
                return;
            }

            if (promptView != null)
            {
                promptView.ShowMessage("已返回阶段地图。返回不消耗时间。");
            }
        }

        private void ResolveSceneReferences()
        {
            if (playerController == null && player != null)
            {
                playerController = player.GetComponent<RpgPlayerController>();
            }

            if (player == null && playerController != null)
            {
                player = playerController.transform;
            }

            if (facingInteractor == null && player != null)
            {
                facingInteractor = player.GetComponent<PlayerFacingInteractor>();
            }

            if (playerController == null)
            {
                playerController = FindSceneObject<RpgPlayerController>();
            }

            if (player == null && playerController != null)
            {
                player = playerController.transform;
            }

            if (facingInteractor == null)
            {
                facingInteractor = FindSceneObject<PlayerFacingInteractor>();
            }

            if (cameraController == null)
            {
                cameraController = FindSceneObject<MapCameraController>();
            }

            if (hudView == null)
            {
                hudView = FindSceneObject<FullscreenMapHudView>();
            }

            if (timeActionHudView == null)
            {
                timeActionHudView = FindSceneObject<TimeActionHudView>();
            }

            if (locationToastView == null)
            {
                locationToastView = FindSceneObject<LocationToastView>();
            }

            if (promptView == null)
            {
                promptView = FindSceneObject<MapInteractionPromptView>();
            }

            if (functionMenuView == null)
            {
                functionMenuView = FindSceneObject<MapFunctionMenuView>();
            }

            if (confirmView == null)
            {
                confirmView = FindSceneObject<MapInteractionConfirmView>();
            }

            if (dialogueView == null)
            {
                dialogueView = FindSceneObject<MapDialogueView>();
            }

            if (shopView == null)
            {
                shopView = FindSceneObject<MapShopView>();
            }

            if (resultView == null)
            {
                resultView = FindSceneObject<MapActivityResultView>();
            }

            if (interactionController == null)
            {
                interactionController = FindSceneObject<MapInteractionController>();
            }
        }

        private void CreateFallbackUiIfNeeded()
        {
            bool needsLegacyFallbackUi = createFallbackUi
                && (hudView == null
                || promptView == null
                || functionMenuView == null
                || confirmView == null
                || dialogueView == null
                || shopView == null
                || resultView == null);
            bool needsExplorationToast = IsExplorationMap() && locationToastView == null;
            if (!needsLegacyFallbackUi && timeActionHudView != null && !needsExplorationToast)
            {
                return;
            }

            Canvas canvas = hudView == null ? null : hudView.GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = MapUiFactory.EnsureRuntimeCanvas();
            }

            RectTransform canvasRect = canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            if (timeActionHudView == null)
            {
                timeActionHudView = MapUiFactory.CreateTimeActionHudView(
                    canvasRect,
                    ResolveHudTheme(),
                    IsExplorationMap());
            }

            if (needsExplorationToast)
            {
                locationToastView = MapUiFactory.CreateLocationToastView(canvasRect, ResolveHudTheme());
            }

            if (!createFallbackUi)
            {
                return;
            }

            if (hudView != null
                && promptView != null
                && functionMenuView != null
                && confirmView != null
                && dialogueView != null
                && shopView != null
                && resultView != null)
            {
                return;
            }

            if (hudView == null)
            {
                hudView = MapUiFactory.CreateHudView(canvasRect);
            }

            if (promptView == null)
            {
                promptView = MapUiFactory.CreatePromptView(canvasRect);
            }

            if (functionMenuView == null)
            {
                functionMenuView = MapUiFactory.CreateFunctionMenuView(canvasRect);
            }

            if (confirmView == null)
            {
                confirmView = MapUiFactory.CreateInteractionConfirmView(canvasRect);
            }

            if (dialogueView == null)
            {
                dialogueView = MapUiFactory.CreateDialogueView(canvasRect);
            }

            if (shopView == null)
            {
                shopView = MapUiFactory.CreateShopView(canvasRect);
            }

            if (resultView == null)
            {
                resultView = MapUiFactory.CreateActivityResultView(canvasRect);
            }
        }

        private void ResolveRuntimeServices()
        {
            if (!autoFindRuntimeServices)
            {
                return;
            }

            if (stageController == null)
            {
                stageController = FindSceneObject<StageController>();
            }

            if (timeActionController == null)
            {
                timeActionController = FindSceneObject<TimeActionController>();
            }

            if (sceneTransitionController == null)
            {
                sceneTransitionController = FindSceneObject<SceneTransitionController>();
            }

            if (locationSceneRouter == null)
            {
                locationSceneRouter = FindSceneObject<LocationSceneRouter>();
            }

            if (locationActionExecutor == null)
            {
                locationActionExecutor = FindSceneObject<LocationActionExecutor>();
            }

            if (gameBootstrapper == null)
            {
                gameBootstrapper = FindSceneObject<GameBootstrapper>();
            }

            if (sessionState == null && stageController != null)
            {
                sessionState = stageController.sessionState;
            }

            if (activityController == null)
            {
                activityController = FindSceneObject<ActivityController>();
            }

            if (challengeController == null)
            {
                challengeController = FindSceneObject<ChallengeController>();
            }

            if (miniGameController == null)
            {
                miniGameController = FindSceneObject<MiniGameController>();
            }

            if (sessionState == null && sceneTransitionController != null)
            {
                sessionState = sceneTransitionController.sessionState;
            }
        }

        private void EnsureRuntimeServicesIfNeeded()
        {
            if (!createFallbackRuntimeServices)
            {
                return;
            }

            if (gameBootstrapper == null)
            {
                gameBootstrapper = GetOrAddService<GameBootstrapper>();
            }

            if (stageController == null)
            {
                stageController = GetOrAddService<StageController>();
            }

            if (timeActionController == null)
            {
                timeActionController = GetOrAddService<TimeActionController>();
            }

            if (activityController == null)
            {
                activityController = GetOrAddService<ActivityController>();
            }

            if (challengeController == null)
            {
                challengeController = GetOrAddService<ChallengeController>();
            }

            if (miniGameController == null)
            {
                miniGameController = GetOrAddService<MiniGameController>();
            }

            if (sceneTransitionController == null)
            {
                sceneTransitionController = GetOrAddService<SceneTransitionController>();
            }

            if (locationSceneRouter == null)
            {
                locationSceneRouter = GetOrAddService<LocationSceneRouter>();
            }

            if (locationActionExecutor == null)
            {
                locationActionExecutor = GetOrAddService<LocationActionExecutor>();
            }

            GameSessionState sharedState = ResolveSharedSessionState();
            if (!HasRuntimeSession(sharedState))
            {
                sharedState = gameBootstrapper == null ? new GameSessionState() : gameBootstrapper.CreateSession();
            }

            sessionState = sharedState;
            stageController.challengeController = challengeController;
            if (stageController.stageSequence == null || stageController.stageSequence.Count == 0)
            {
                stageController.stageSequence = gameBootstrapper == null
                    ? MvpSampleDataFactory.CreateThreeStageSequence()
                    : gameBootstrapper.GetStageSequence();
            }

            stageController.SetSessionState(sessionState);
            timeActionController.SetSessionState(sessionState);
            activityController.SetSessionState(sessionState);
            if (challengeController != null)
            {
                challengeController.SetSessionState(sessionState);
            }

            sceneTransitionController.SetSessionState(sessionState);
            if (sceneTransitionController.GetKnownLocations().Count == 0)
            {
                sceneTransitionController.SetLocations(gameBootstrapper == null
                    ? MvpSampleDataFactory.CreateDefaultLocations()
                    : gameBootstrapper.GetLocations());
            }

            if (sessionState.stageState.currentStage == null
                && sessionState.stageState.remainingTime <= 0
                && stageController.stageSequence != null
                && stageController.stageSequence.Count > 0)
            {
                StageData firstStage = gameBootstrapper == null
                    ? stageController.stageSequence[0]
                    : gameBootstrapper.GetFirstStage(stageController.stageSequence);
                if (firstStage != null)
                {
                    stageController.StartStage(firstStage);
                }
            }

            locationActionExecutor.Configure(
                sessionState,
                stageController,
                activityController,
                miniGameController,
                sceneTransitionController,
                timeActionController);
            locationSceneRouter.Configure(
                sessionState,
                stageController,
                sceneTransitionController,
                MvpSampleDataFactory.CreateDefaultStageMapData(sessionState.stageState.currentStage));
        }

        private GameSessionState ResolveSharedSessionState()
        {
            if (GameBootstrapper.RestoringSession != null) return GameBootstrapper.RestoringSession;
            if (stageController != null && HasRuntimeSession(stageController.sessionState))
            {
                return stageController.sessionState;
            }

            if (activityController != null && HasRuntimeSession(activityController.sessionState))
            {
                return activityController.sessionState;
            }

            if (sceneTransitionController != null && HasRuntimeSession(sceneTransitionController.sessionState))
            {
                return sceneTransitionController.sessionState;
            }

            return sessionState;
        }

        private T GetOrAddService<T>() where T : Component
        {
            T service = FindSceneObject<T>();
            if (service != null)
            {
                return service;
            }

            return gameObject.AddComponent<T>();
        }

        private static bool HasRuntimeSession(GameSessionState state)
        {
            return state != null
                && (state.stageState.currentStage != null
                    || state.stageState.remainingTime > 0
                    || state.deckState.masterDeck.Count > 0
                    || state.locationState.visitedLocationIds.Count > 0);
        }

        private void CollectInteractionTargets()
        {
            if (!autoCollectTargets)
            {
                return;
            }

            interactionTargets = FindObjectsByType<MapInteractionTarget>(FindObjectsInactive.Exclude);
            BindAvailableActivities(interactionTargets);
        }

        private void WireInteractionSystem()
        {
            if (interactionController != null)
            {
                interactionController.locationActionExecutor = locationActionExecutor;
                interactionController.locationSceneRouter = locationSceneRouter;
                interactionController.sceneTransitionController = sceneTransitionController;
            }

            if (hudView != null && hudView.returnButton != null)
            {
                hudView.returnButton.onClick.RemoveListener(ReturnToStageMap);
                hudView.returnButton.onClick.AddListener(ReturnToStageMap);
            }

            if (functionMenuView != null)
            {
                functionMenuView.Configure(sessionState, sceneTransitionController, hudView);
            }

            if (shopView != null)
            {
                shopView.Configure(sessionState, hudView);
            }

            if (facingInteractor != null)
            {
                facingInteractor.mapInteractionController = interactionController;
                facingInteractor.targets = interactionTargets;
            }

            if (playerController != null)
            {
                playerController.facingInteractor = facingInteractor;
                playerController.promptView = promptView;
                MapInteractionFlowController flowController = FindSceneObject<MapInteractionFlowController>();
                if (flowController != null)
                {
                    flowController.interactionController = interactionController;
                    flowController.promptView = promptView;
                    flowController.confirmView = confirmView;
                    flowController.dialogueView = dialogueView;
                    flowController.shopView = shopView;
                    flowController.resultView = resultView;
                    flowController.sceneTransitionController = sceneTransitionController;
                    flowController.sessionState = sessionState;
                    playerController.interactionFlowController = flowController;
                }
            }

            if (cameraController != null)
            {
                cameraController.Configure(player, cameraBounds);
            }
        }

        private void BindAvailableActivities(IEnumerable<MapInteractionTarget> targets)
        {
            if (stageController == null || targets == null)
            {
                return;
            }

            IReadOnlyList<ActivityData> activities = stageController.GetAvailableActivities();
            foreach (MapInteractionTarget target in targets)
            {
                if (target == null || target.fallbackActivity != null)
                {
                    continue;
                }

                target.fallbackActivity = FindMatchingActivity(target, activities);
            }
        }

        private static ActivityData FindMatchingActivity(MapInteractionTarget target, IReadOnlyList<ActivityData> activities)
        {
            if (target == null || activities == null)
            {
                return null;
            }

            string interactionId = target.interactionId;
            string actionId = target.interactionPoint == null ? string.Empty : target.interactionPoint.actionId;
            string display = target.displayName;
            for (int i = 0; i < activities.Count; i++)
            {
                ActivityData activity = activities[i];
                if (activity == null)
                {
                    continue;
                }

                if ((!string.IsNullOrEmpty(interactionId) && activity.activityId == interactionId)
                    || (!string.IsNullOrEmpty(actionId) && activity.activityId == actionId)
                    || (!string.IsNullOrEmpty(display) && activity.displayName == display))
                {
                    return activity;
                }
            }

            return null;
        }

        private static PlayerSpawnPoint FindSpawnPoint(string spawnPointId)
        {
            PlayerSpawnPoint[] spawnPoints = FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Exclude);
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                PlayerSpawnPoint spawnPoint = spawnPoints[i];
                if (spawnPoint != null && spawnPoint.Matches(spawnPointId))
                {
                    return spawnPoint;
                }
            }

            return null;
        }

        private static T FindSceneObject<T>() where T : Object
        {
            return FindAnyObjectByType<T>();
        }

        private bool IsExplorationMap()
        {
            return !string.IsNullOrWhiteSpace(mapId)
                && mapId.StartsWith(ExplorationMapIdPrefix, System.StringComparison.Ordinal);
        }

        private UIThemeConfig ResolveHudTheme()
        {
            if (hudTheme != null)
            {
                return hudTheme;
            }

            if (IsExplorationMap())
            {
                hudTheme = Resources.Load<UIThemeConfig>(ExplorationHudThemeResourcePath);
            }

            if (hudTheme == null && hudView != null)
            {
                hudTheme = hudView.theme;
            }

            return GameUiStyle.ResolveTheme(hudTheme);
        }
    }
}
