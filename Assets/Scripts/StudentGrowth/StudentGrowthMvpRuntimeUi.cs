using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudentGrowth
{
    public partial class StudentGrowthMvpRuntimeUi : MonoBehaviour
    {
        private const string RootName = "StudentGrowth MVP Runtime UI";
        private const float SceneMapWidth = 760f;
        private const float SceneMapHeight = 420f;
        private const float SceneMapMoveSpeed = 190f;
        private const float SceneMapPlayerRadius = 16f;
        private const string SceneMapPlayerSpriteSheetPath = "Assets/Art/Characters/Player/player_main_walk_4dir_48x48.png";
        private const int SceneMapPlayerFrameSize = 48;
        private const int SceneMapPlayerFrameCount = 4;
        private const float SceneMapPlayerWalkFrameDuration = 0.14f;

        [SerializeField] private BattleUIConfig battleUiConfig;

        public enum ThemeAuditPreviewMode
        {
            Stage,
            HomeLocation,
            BossIntro,
            ChallengeBattle,
            BossResult,
            Ending
        }

        private enum BattleCardFeedbackKind
        {
            Attack,
            Defense,
            Utility
        }

        private GameSessionState sessionState;
        private StageController stageController;
        private TimeActionController timeActionController;
        private ActivityController activityController;
        private ChallengeController challengeController;
        private MiniGameController miniGameController;
        private GameBootstrapper gameBootstrapper;
        private EndingController endingController;
        private SceneTransitionController sceneTransitionController;
        private LocationActionExecutor locationActionExecutor;
        private LocationSceneRouter locationSceneRouter;
        private ItemEffectResolver itemEffectResolver;
        private Font runtimeFont;
        private Sprite solidSprite;
        private UIThemeConfig runtimeTheme;
        private List<LocationData> locations = new List<LocationData>();
        private StageMapData currentStageMapData;
        private readonly StageMapLocationAvailabilityService stageMapAvailabilityService = new StageMapLocationAvailabilityService();

        private RectTransform rootRect;
        private RectTransform contentRoot;
        private RectTransform topProgressGroup;
        private Text titleText;
        private Text statusText;
        private Text topProgressText;
        private Image topProgressFill;
        private Button phaseStateButton;
        private Button topEndTurnButton;
        private RectTransform bossInfoContent;
        private RectTransform challengeContent;
        private Button bossInfoTabButton;
        private Button challengeTabButton;
        private Text logTitleText;
        private Text logText;
        private RectTransform avatarRect;
        private Image avatarImage;
        private Coroutine avatarAnimationCoroutine;
        private Coroutine avatarDeferredActionCoroutine;
        private GameObject avatarEffectObject;
        private RectTransform battleFeedbackLayer;
        private RectTransform battleEnemyTargetRect;
        private Image battleEnemyTargetHighlight;
        private BattleTooltipView battleTooltipView;
        private Text battleCenterHintText;
        private Coroutine battleFeedbackCoroutine;
        private Vector2 avatarDefaultAnchoredPosition;
        private Vector3 avatarDefaultScale;
        private Quaternion avatarDefaultRotation;
        private Color avatarDefaultColor;
        private bool sceneMapActive;
        private bool sceneMapConfirmOpen;
        private bool sceneMapDebugVisible;
        private bool legacyInputBlocked;
        private RectTransform sceneMapPlayArea;
        private RectTransform sceneMapPlayer;
        private Image sceneMapPlayerImage;
        private RectTransform sceneMapFacingIndicator;
        private RectTransform sceneMapConfirmDialog;
        private Text sceneMapPromptText;
        private Text sceneMapFeedbackText;
        private Text sceneMapStatusText;
        private Vector2 sceneMapPlayerPosition;
        private Vector2 sceneMapFacingDirection = Vector2.up;
        private Texture2D sceneMapPlayerSpriteSheetTexture;
        private Sprite[,] sceneMapPlayerSprites;
        private bool sceneMapPlayerSpritesLoaded;
        private float sceneMapPlayerAnimationTimer;
        private int sceneMapPlayerAnimationFrame;
        private SceneMapInteractionPoint focusedSceneMapInteraction;
        private readonly List<SceneMapInteractionPoint> sceneMapInteractions = new List<SceneMapInteractionPoint>();
        private readonly List<Rect> sceneMapBlockers = new List<Rect>();
        private readonly List<RectTransform> sceneMapDebugObjects = new List<RectTransform>();

        private readonly Color dark = new Color(0.035f, 0.055f, 0.052f, 0.98f);
        private readonly Color panel = new Color(0.07f, 0.10f, 0.09f, 0.96f);
        private readonly Color board = new Color(0.045f, 0.12f, 0.095f, 0.98f);
        private readonly Color cardFill = new Color(0.13f, 0.17f, 0.13f, 0.98f);
        private readonly Color paper = new Color(0.72f, 0.66f, 0.49f, 0.98f);
        private readonly Color green = new Color(0.43f, 0.65f, 0.52f, 1f);
        private readonly Color amber = new Color(0.92f, 0.74f, 0.38f, 1f);
        private readonly Color text = new Color(0.87f, 0.98f, 0.84f, 1f);
        private readonly Color muted = new Color(0.62f, 0.76f, 0.66f, 1f);
        private readonly Color warning = new Color(0.76f, 0.26f, 0.22f, 1f);

        // [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneLoadedHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeUi()
        {
            TryCreateRuntimeUiForScene(SceneManager.GetActiveScene());
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            TryCreateRuntimeUiForScene(scene);
        }

        private static void TryCreateRuntimeUiForScene(Scene scene)
        {
            if (scene.name != "SampleScene")
            {
                return;
            }

            if (GameObject.Find(RootName) != null)
            {
                return;
            }

            GameObject root = new GameObject(RootName);
            root.AddComponent<StudentGrowthMvpRuntimeUi>();
        }

        private void Start()
        {
            BuildSession();
            BuildCanvas();
            ShowStageScreen("新游戏开始。请选择阶段活动。");
        }

        public void BuildThemeAuditPreview(UIThemeConfig auditTheme)
        {
            BuildThemeAuditPreview(auditTheme, ThemeAuditPreviewMode.Stage);
        }

        public void BuildThemeAuditPreview(UIThemeConfig auditTheme, ThemeAuditPreviewMode previewMode)
        {
            BattleUIConfig config = GetBattleUiConfig();
            if (auditTheme != null)
            {
                config.theme = auditTheme;
                runtimeTheme = auditTheme;
            }

            if (rootRect != null)
            {
                return;
            }

            BuildSession();
            BuildCanvas();
            ShowThemeAuditPreview(previewMode);
        }

        private void ShowThemeAuditPreview(ThemeAuditPreviewMode previewMode)
        {
            switch (previewMode)
            {
                case ThemeAuditPreviewMode.HomeLocation:
                    ShowThemeAuditHomeLocationPreview();
                    return;
                case ThemeAuditPreviewMode.BossIntro:
                    sessionState.stageState.bossUnlocked = true;
                    ShowBossIntro("UI 主题审计预览。确认 Boss 介绍界面样式。");
                    return;
                case ThemeAuditPreviewMode.ChallengeBattle:
                    StartThemeAuditChallengePreview();
                    return;
                case ThemeAuditPreviewMode.BossResult:
                    StartThemeAuditBossResultPreview();
                    return;
                case ThemeAuditPreviewMode.Ending:
                    sessionState.endingReached = true;
                    sessionState.stageState.clearedStageCount = 3;
                    sessionState.stageState.defeatedBossCount = 3;
                    ShowEndingScreen();
                    return;
                default:
                    ShowStageScreen("UI 主题审计预览。请选择阶段活动。");
                    return;
            }
        }

        private void ShowThemeAuditHomeLocationPreview()
        {
            if (locationSceneRouter != null)
            {
                locationSceneRouter.useUnitySceneLoadingForLatestMaps = false;
            }

            LocationData home = FindLocationById("location_home");
            if (home == null)
            {
                ShowStageScreen("UI 主题审计预览：家地图配置不存在。");
                return;
            }

            ShowLocationScreen(home, "UI 主题审计预览。确认旧位置界面样式。");
        }

        private void StartThemeAuditChallengePreview()
        {
            sessionState.stageState.remainingTime = 0;
            sessionState.stageState.bossUnlocked = true;
            stageController.StartBoss();
            ShowChallengeScreen("UI 主题审计预览。确认卡牌挑战界面样式。");
        }

        private void StartThemeAuditBossResultPreview()
        {
            sessionState.stageState.remainingTime = 0;
            sessionState.stageState.bossUnlocked = true;
            stageController.StartBoss();
            challengeController.CompleteChallenge(true);
            ShowBossResult(true);
        }

        private void Update()
        {
            UpdateSceneMapInput();
        }

        private void BuildSession()
        {
            gameBootstrapper = gameObject.GetComponent<GameBootstrapper>();
            if (gameBootstrapper == null)
            {
                gameBootstrapper = gameObject.AddComponent<GameBootstrapper>();
            }

            sessionState = gameBootstrapper.CreateSession();

            challengeController = gameObject.AddComponent<ChallengeController>();
            stageController = gameObject.AddComponent<StageController>();
            timeActionController = gameObject.GetComponent<TimeActionController>();
            if (timeActionController == null)
            {
                timeActionController = gameObject.AddComponent<TimeActionController>();
            }
            activityController = gameObject.AddComponent<ActivityController>();
            miniGameController = gameObject.AddComponent<MiniGameController>();
            endingController = gameObject.AddComponent<EndingController>();
            sceneTransitionController = gameObject.AddComponent<SceneTransitionController>();
            locationSceneRouter = gameObject.GetComponent<LocationSceneRouter>();
            if (locationSceneRouter == null)
            {
                locationSceneRouter = gameObject.AddComponent<LocationSceneRouter>();
            }

            locationActionExecutor = gameObject.GetComponent<LocationActionExecutor>();
            if (locationActionExecutor == null)
            {
                locationActionExecutor = gameObject.AddComponent<LocationActionExecutor>();
            }

            itemEffectResolver = new ItemEffectResolver();
            locations = gameBootstrapper.GetLocations();

            stageController.challengeController = challengeController;
            stageController.stageSequence = gameBootstrapper.GetStageSequence();
            stageController.SetSessionState(sessionState);
            timeActionController.SetSessionState(sessionState);
            activityController.SetSessionState(sessionState);
            challengeController.SetSessionState(sessionState);
            endingController.SetSessionState(sessionState);
            endingController.SetEndingOptions(gameBootstrapper.GetEndingOptions());
            sceneTransitionController.SetSessionState(sessionState);
            sceneTransitionController.SetLocations(locations);
            sceneTransitionController.ShowMap();
            locationActionExecutor.Configure(
                sessionState,
                stageController,
                activityController,
                miniGameController,
                sceneTransitionController,
                timeActionController);
            StageData firstStage = gameBootstrapper.GetFirstStage(stageController.stageSequence);
            StageData activeStage = sessionState.stageState.currentStage;
            if (activeStage == null)
            {
                activeStage = firstStage;
                stageController.StartStage(activeStage);
            }

            currentStageMapData = MvpSampleDataFactory.CreateDefaultStageMapData(activeStage);
            locationSceneRouter.Configure(sessionState, stageController, sceneTransitionController, currentStageMapData);
        }

        private void AddStarterItems(GameSessionState state)
        {
            ItemData drink = ScriptableObject.CreateInstance<ItemData>();
            drink.itemId = "item_energy_drink";
            drink.displayName = "功能饮料";
            drink.itemType = ItemType.Consumable;
            drink.triggerTiming = ItemTriggerTiming.OnUse;
            drink.canUseInStageScreen = true;
            drink.canUseInBattle = true;
            drink.consumedOnUse = true;
            drink.effects.Add(new CardEffectData { effectType = CardEffectType.GainEnergy, value = 15 });
            GameArtBindingUtility.ApplyKnownArtPath(drink);
            state.itemInventory.AddItem(drink);

            ItemData luckyPen = ScriptableObject.CreateInstance<ItemData>();
            luckyPen.itemId = "item_lucky_pen";
            luckyPen.displayName = "幸运笔";
            luckyPen.itemType = ItemType.Passive;
            luckyPen.triggerTiming = ItemTriggerTiming.OnBossStart;
            luckyPen.effects.Add(new CardEffectData { effectType = CardEffectType.AddChallengeProgress, value = 10 });
            GameArtBindingUtility.ApplyKnownArtPath(luckyPen);
            state.itemInventory.AddItem(luckyPen);
        }

        private void BuildCanvas()
        {
            EnsureEventSystem();
            BattleUIConfig uiConfig = GetBattleUiConfig();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();
            rootRect = gameObject.GetComponent<RectTransform>();
            Stretch(rootRect);

            RectTransform backgroundLayer = CreateEmpty("BackgroundLayer", rootRect);
            Stretch(backgroundLayer);
            Image background = CreateImage("ChalkboardBackground", backgroundLayer, GetSolidSprite(), new Color(0.025f, 0.038f, 0.04f, 1f));
            Stretch(background.rectTransform);
            background.raycastTarget = false;

            RectTransform top = CreatePanel("TopStatusBar", rootRect, new Color(0.045f, 0.065f, 0.06f, 0.94f), WithAlpha(green, uiConfig.weakBorderAlpha));
            SetTop(top, 24f, 24f, 24f, uiConfig.topBarHeight);
            titleText = CreateText("TitleText", top, "", 34, FontStyle.Bold, TextAnchor.MiddleLeft, text);
            SetStretch(titleText.rectTransform, new Vector2(0f, 0f), new Vector2(0.30f, 1f), new Vector2(28f, 8f), new Vector2(-8f, -8f));

            topProgressGroup = CreateEmpty("ProgressGroup", top);
            SetStretch(topProgressGroup, new Vector2(0.30f, 0f), new Vector2(0.62f, 1f), new Vector2(12f, 10f), new Vector2(-12f, -10f));
            topProgressText = CreateText("ProgressLabel", topProgressGroup, "", 22, FontStyle.Bold, TextAnchor.UpperLeft, amber);
            SetTop(topProgressText.rectTransform, 0f, 0f, 2f, 28f);
            RectTransform topProgressTrack = CreateProgressBar("ProgressBar", topProgressGroup, 0f, new Color(0.02f, 0.05f, 0.045f, 1f), green, out topProgressFill);
            SetBottom(topProgressTrack, 0f, 0f, 4f, 28f);

            phaseStateButton = CreateButton(top, "Boss 已解锁", Vector2.zero, new Vector2(190f, 56f));
            SetTopCenter(phaseStateButton.transform as RectTransform, new Vector2(0f, -20f), new Vector2(190f, 56f));
            SetButtonColors(
                phaseStateButton,
                new Color(0.07f, 0.15f, 0.10f, 1f),
                new Color(0.10f, 0.22f, 0.14f, 1f),
                new Color(0.08f, 0.12f, 0.08f, 1f),
                new Color(0.09f, 0.18f, 0.12f, 1f));
            phaseStateButton.interactable = false;
            phaseStateButton.gameObject.SetActive(false);

            statusText = CreateText("TopStatusText", top, "", 22, FontStyle.Bold, TextAnchor.MiddleRight, muted);
            SetStretch(statusText.rectTransform, new Vector2(0.62f, 0f), Vector2.one, new Vector2(12f, 8f), new Vector2(-230f, -8f));

            topEndTurnButton = CreateButton(top, "结束回合", Vector2.zero, new Vector2(174f, 56f));
            SetRightMiddle(topEndTurnButton.transform as RectTransform, new Vector2(-28f, 0f), new Vector2(174f, 56f));
            topEndTurnButton.onClick.AddListener(OnEndTurnClicked);
            SetTopChallengeControlsVisible(false);

            contentRoot = CreateEmpty("ContentLayer", rootRect);
            SetStretch(
                contentRoot,
                Vector2.zero,
                Vector2.one,
                new Vector2(uiConfig.safePadding.x, uiConfig.safePadding.y + uiConfig.logHeight + 16f),
                new Vector2(-uiConfig.safePadding.x, -uiConfig.safePadding.y - uiConfig.topBarHeight - 16f));

            RectTransform logPanel = CreatePanel("BottomMessageBar", rootRect, new Color(0.045f, 0.075f, 0.068f, 0.94f), WithAlpha(green, uiConfig.weakBorderAlpha));
            SetBottom(logPanel, 24f, 24f, 24f, uiConfig.logHeight);
            logTitleText = CreateText("MessageLabel", logPanel, "行动记录", 20, FontStyle.Bold, TextAnchor.MiddleLeft, amber);
            SetStretch(logTitleText.rectTransform, Vector2.zero, new Vector2(0f, 1f), new Vector2(28f, 8f), new Vector2(154f, -8f));
            logText = CreateText("MessageText", logPanel, "", 20, FontStyle.Normal, TextAnchor.MiddleLeft, text);
            SetStretch(logText.rectTransform, Vector2.zero, Vector2.one, new Vector2(164f, 8f), new Vector2(-28f, -8f));

            RectTransform overlayLayer = CreateEmpty("OverlayLayer", rootRect);
            Stretch(overlayLayer);
        }

        private void ShowStageScreen(string message)
        {
            ClearContent();
            if (sceneTransitionController != null)
            {
                sceneTransitionController.ShowMap();
            }

            StageData stage = sessionState.stageState.currentStage;
            titleText.text = stage.displayName;
            statusText.text = BuildStatusText();
            SetTopChallengeControlsVisible(false);
            SetLogMessage("课堂提示", message);

            RectTransform left = CreatePanel("StudentStatusPanel", contentRoot, panel, green);
            SetStretch(left, Vector2.zero, new Vector2(0.26f, 1f), Vector2.zero, new Vector2(-12f, 0f));

            RectTransform center = CreatePanel("ActivityPanel", contentRoot, panel, green);
            SetStretch(center, new Vector2(0.27f, 0f), new Vector2(0.74f, 1f), Vector2.zero, Vector2.zero);

            RectTransform right = CreatePanel("InventoryPanel", contentRoot, panel, amber);
            SetStretch(right, new Vector2(0.75f, 0f), Vector2.one, new Vector2(12f, 0f), Vector2.zero);

            BuildStudentPanel(left);
            BuildMapPanel(center);
            BuildInventoryPanel(right);
        }

        private void BuildMapPanel(RectTransform parent)
        {
            currentStageMapData = MvpSampleDataFactory.CreateDefaultStageMapData(sessionState.stageState.currentStage);
            if (locationSceneRouter != null)
            {
                locationSceneRouter.SetStageMapData(currentStageMapData);
            }

            CreateHeader(parent, string.IsNullOrEmpty(currentStageMapData.title) ? "阶段地图" : currentStageMapData.title);
            UIThemeConfig theme = GetRuntimeTheme();
            CreateRuntimeInfoStrip(
                parent,
                "MapHintStrip",
                theme.timeIcon,
                theme.gold,
                "地图提示",
                currentStageMapData.description,
                24f,
                24f,
                72f,
                54f,
                GameUiPanelVariant.Dark);

            float startY = -128f;
            float columnWidth = 360f;
            float rowHeight = 116f;
            List<StageMapLocationEntry> entries = new List<StageMapLocationEntry>(currentStageMapData.locations);
            entries.Sort((left, right) => left.sortOrder.CompareTo(right.sortOrder));

            int visibleIndex = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                StageMapLocationEntry entry = entries[i];
                StageMapLocationStatus status = stageMapAvailabilityService.ResolveStatus(
                    entry,
                    sessionState,
                    stageController,
                    sceneTransitionController);
                if (status == StageMapLocationStatus.Hidden)
                {
                    continue;
                }

                LocationData location = FindLocationById(entry.locationId);
                int column = visibleIndex % 2;
                int row = visibleIndex / 2;
                Vector2 position = new Vector2(column == 0 ? -190f : 190f, startY - row * rowHeight);
                Button button = CreateStageMapLocationButton(parent, entry, location, status, position, new Vector2(columnWidth, 98f));

                StageMapLocationEntry capturedEntry = entry;
                LocationData capturedLocation = location;
                StageMapLocationStatus capturedStatus = status;
                button.onClick.AddListener(() => OnStageMapLocationClicked(capturedEntry, capturedLocation, capturedStatus));
                visibleIndex++;
            }

            if (stageController.ShouldStartBoss())
            {
                Button bossButton = CreateBossEntryButton(parent, new Vector2(0f, startY - 3f * rowHeight - 18f), new Vector2(760f, 86f));
                bossButton.onClick.AddListener(StartBossScreen);
            }
        }

        private Button CreateStageMapLocationButton(
            RectTransform parent,
            StageMapLocationEntry entry,
            LocationData location,
            StageMapLocationStatus status,
            Vector2 position,
            Vector2 size)
        {
            UIThemeConfig theme = GetRuntimeTheme();
            Color statusColor = ResolveStageMapStatusColor(theme, status);
            Button button = CreateLegacyEntryButton(
                parent,
                "StageMapLocationButton",
                ResolveStageMapLocationIcon(theme, location, status),
                statusColor,
                ResolveStageMapLocationName(entry, location),
                TruncateRuntimeText(ResolveStageMapLocationDescription(entry, location), 32),
                GetStageMapStatusLabel(status),
                statusColor,
                position,
                size);
            ApplyStageMapLocationButtonStyle(button, status);
            return button;
        }

        private static string ResolveStageMapLocationName(StageMapLocationEntry entry, LocationData location)
        {
            return entry != null && !string.IsNullOrEmpty(entry.displayName)
                ? entry.displayName
                : location != null ? location.displayName : "未知地点";
        }

        private static string ResolveStageMapLocationDescription(StageMapLocationEntry entry, LocationData location)
        {
            return entry != null && !string.IsNullOrEmpty(entry.description)
                ? entry.description
                : location != null && !string.IsNullOrEmpty(location.description) ? location.description : "地点说明待补充。";
        }

        private void OnStageMapLocationClicked(StageMapLocationEntry entry, LocationData location, StageMapLocationStatus status)
        {
            if (!IsStageMapLocationEnterable(status))
            {
                string reason = stageMapAvailabilityService.ResolveReason(entry, status);
                string displayName = entry == null ? "地点" : entry.displayName;
                SetLogMessage("地图提示", displayName + "：" + reason);
                return;
            }

            if (location == null)
            {
                SetLogMessage("地图提示", "地点配置不存在，无法进入。");
                return;
            }

            ShowLocationScreen(location, "进入地点：" + location.displayName);
        }

        private LocationData FindLocationById(string locationId)
        {
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

        private static bool IsStageMapLocationEnterable(StageMapLocationStatus status)
        {
            return status == StageMapLocationStatus.Unlocked || status == StageMapLocationStatus.Completed;
        }

        private static string GetStageMapStatusLabel(StageMapLocationStatus status)
        {
            switch (status)
            {
                case StageMapLocationStatus.Unlocked:
                    return "进入";
                case StageMapLocationStatus.Completed:
                    return "已完成";
                case StageMapLocationStatus.Locked:
                    return "未解锁";
                case StageMapLocationStatus.ComingSoon:
                    return "开发中";
                case StageMapLocationStatus.Disabled:
                    return "不可进入";
                default:
                    return "隐藏";
            }
        }

        private void ApplyStageMapLocationButtonStyle(Button button, StageMapLocationStatus status)
        {
            switch (status)
            {
                case StageMapLocationStatus.Unlocked:
                case StageMapLocationStatus.Completed:
                    SetButtonColors(
                        button,
                        new Color(0.11f, 0.14f, 0.09f, 0.98f),
                        new Color(0.20f, 0.23f, 0.12f, 1f),
                        new Color(0.22f, 0.18f, 0.10f, 1f),
                        new Color(0.07f, 0.08f, 0.07f, 0.65f));
                    break;
                case StageMapLocationStatus.ComingSoon:
                    SetButtonColors(
                        button,
                        new Color(0.12f, 0.105f, 0.06f, 0.96f),
                        new Color(0.16f, 0.135f, 0.075f, 1f),
                        new Color(0.11f, 0.085f, 0.045f, 1f),
                        new Color(0.07f, 0.08f, 0.07f, 0.65f));
                    break;
                default:
                    SetButtonColors(
                        button,
                        new Color(0.055f, 0.075f, 0.068f, 0.96f),
                        new Color(0.075f, 0.10f, 0.088f, 1f),
                        new Color(0.06f, 0.07f, 0.06f, 1f),
                        new Color(0.07f, 0.08f, 0.07f, 0.65f));
                    break;
            }
        }

        private static Color ResolveStageMapStatusColor(UIThemeConfig theme, StageMapLocationStatus status)
        {
            switch (status)
            {
                case StageMapLocationStatus.Unlocked:
                case StageMapLocationStatus.Completed:
                    return theme.success;
                case StageMapLocationStatus.ComingSoon:
                    return theme.time;
                case StageMapLocationStatus.Locked:
                case StageMapLocationStatus.Disabled:
                    return theme.danger;
                default:
                    return theme.hintText;
            }
        }

        private static Sprite ResolveStageMapLocationIcon(UIThemeConfig theme, LocationData location, StageMapLocationStatus status)
        {
            if (location != null && location.icon != null)
            {
                return location.icon;
            }

            switch (status)
            {
                case StageMapLocationStatus.Unlocked:
                case StageMapLocationStatus.Completed:
                    return theme.confirmIcon;
                case StageMapLocationStatus.ComingSoon:
                    return theme.timeIcon;
                default:
                    return theme.cancelIcon;
            }
        }

        private static Sprite ResolveLocationIcon(UIThemeConfig theme, LocationData location)
        {
            if (location != null && location.icon != null)
            {
                return location.icon;
            }

            if (location == null)
            {
                return theme.timeIcon;
            }

            switch (location.locationType)
            {
                case LocationType.Shop:
                    return theme.inventoryIcon;
                case LocationType.Park:
                case LocationType.Club:
                    return theme.relationIcon;
                case LocationType.Home:
                    return theme.moodIcon;
                case LocationType.School:
                case LocationType.Library:
                case LocationType.ExamHall:
                    return theme.cardsIcon;
                default:
                    return theme.timeIcon;
            }
        }

        private void ShowLocationScreen(LocationData location, string message)
        {
            if (locationSceneRouter != null)
            {
                LocationSceneRouteResult routeResult = locationSceneRouter.EnterLocationScene(location.locationId);
                if (!routeResult.success)
                {
                    ShowStageScreen(routeResult.message);
                    return;
                }

                message = routeResult.message;
                if (routeResult.didLoadUnityScene)
                {
                    return;
                }
            }
            else if (sceneTransitionController != null && !sceneTransitionController.TryGoToLocation(location, out string reason))
            {
                ShowStageScreen(reason);
                return;
            }

            ClearContent();
            titleText.text = location.displayName;
            statusText.text = BuildSceneMapStatusText();
            SetTopChallengeControlsVisible(false);
            SetTopStatusMapSpace();
            SetLogMessage("地图提示", message);

            RectTransform left = CreatePanel("LocationStatusPanel", contentRoot, panel, green);
            SetStretch(left, Vector2.zero, new Vector2(0.26f, 1f), Vector2.zero, new Vector2(-12f, 0f));

            RectTransform center = CreatePanel("LocationActionPanel", contentRoot, panel, green);
            SetStretch(center, new Vector2(0.27f, 0f), new Vector2(0.74f, 1f), Vector2.zero, Vector2.zero);

            RectTransform right = CreatePanel("LocationInventoryPanel", contentRoot, panel, amber);
            SetStretch(right, new Vector2(0.75f, 0f), Vector2.one, new Vector2(12f, 0f), Vector2.zero);

            BuildStudentPanel(left);
            BuildLocationMapPanel(center, location);
            BuildInventoryPanel(right);
        }

        private void BuildLocationMapPanel(RectTransform parent, LocationData location)
        {
            sceneMapActive = true;
            sceneMapConfirmOpen = false;
            sceneMapDebugVisible = false;

            CreateHeader(parent, location.displayName);
            string description = string.IsNullOrEmpty(location.description) ? "地点说明待补充。" : location.description;
            UIThemeConfig theme = GetRuntimeTheme();
            CreateRuntimeInfoStrip(
                parent,
                "LocationDescriptionStrip",
                ResolveLocationIcon(theme, location),
                theme.gold,
                "地点说明",
                description,
                24f,
                24f,
                72f,
                54f,
                GameUiPanelVariant.Dark);

            sceneMapStatusText = null;

            sceneMapPlayArea = CreatePanel("SceneMapPlayArea", parent, new Color(0.025f, 0.055f, 0.048f, 0.98f), green);
            SetTopCenter(sceneMapPlayArea, new Vector2(0f, -164f), new Vector2(SceneMapWidth, SceneMapHeight));

            if (!string.IsNullOrEmpty(location.imagePath))
            {
                Image background = CreateImage("LocationImage", sceneMapPlayArea, LoadSprite(location.imagePath), new Color(1f, 1f, 1f, 0.72f));
                background.preserveAspect = true;
                Stretch(background.rectTransform);
                background.transform.SetAsFirstSibling();
            }

            BuildSceneMapObjects(location);
            CreateSceneMapPlayer();
            CreateSceneMapHud(parent);
            RefreshSceneMapFocus();
        }

        private void BuildLocationActionPanel(RectTransform parent, LocationData location)
        {
            BuildLocationMapPanel(parent, location);
        }

        private void CreateSceneMapHud(RectTransform parent)
        {
            sceneMapPromptText = CreateText("SceneMapPromptText", parent, string.Empty, 20, FontStyle.Bold, TextAnchor.MiddleLeft, amber);
            SetTop(sceneMapPromptText.rectTransform, 24f, 24f, 588f, 54f);

            UIThemeConfig theme = GetRuntimeTheme();
            sceneMapFeedbackText = CreateRuntimeInfoStrip(
                parent,
                "SceneMapFeedbackStrip",
                theme.confirmIcon,
                theme.success,
                "操作提示",
                "靠近并面向可互动对象后按 E 交互。",
                24f,
                24f,
                648f,
                72f,
                GameUiPanelVariant.Dark);
        }

        private void BuildSceneMapObjects(LocationData location)
        {
            sceneMapInteractions.Clear();
            sceneMapBlockers.Clear();
            sceneMapDebugObjects.Clear();
            focusedSceneMapInteraction = null;

            CreateSceneMapDebugObject("NorthWall", "墙", new Vector2(0f, 198f), new Vector2(SceneMapWidth, 18f), new Color(0.08f, 0.12f, 0.10f, 1f), green);
            CreateSceneMapDebugObject("SouthWall", "墙", new Vector2(0f, -198f), new Vector2(SceneMapWidth, 18f), new Color(0.08f, 0.12f, 0.10f, 1f), green);
            CreateSceneMapDebugObject("WestWall", "墙", new Vector2(-371f, 0f), new Vector2(18f, SceneMapHeight), new Color(0.08f, 0.12f, 0.10f, 1f), green);
            CreateSceneMapDebugObject("EastWall", "墙", new Vector2(371f, 0f), new Vector2(18f, SceneMapHeight), new Color(0.08f, 0.12f, 0.10f, 1f), green);

            List<ActivityData> activities = GetActivitiesForLocation(location);
            if (location != null && location.locationType == LocationType.Home)
            {
                BuildHomeSceneMap(activities);
            }
            else
            {
                BuildGenericSceneMap(location, activities);
            }
        }

        private void BuildHomeSceneMap(List<ActivityData> activities)
        {
            Vector2 bedCenter = new Vector2(-250f, 104f);
            CreateSceneMapDebugObject("HomeBed", "床", bedCenter, new Vector2(150f, 86f), new Color(0.18f, 0.25f, 0.22f, 0.98f), amber);
            AddSceneMapBlocker(new Vector2(-250f, 104f), new Vector2(162f, 96f));
            ActivityData sleep = FindSceneMapActivity(activities, "early_sleep", "早点休息", ActivityType.Rest, true);
            if (sleep == null)
            {
                sleep = FindSceneMapActivity(activities, "sleep", "睡", ActivityType.Rest, true);
            }
            if (sleep == null)
            {
                sleep = FindSceneMapActivityByType(activities, ActivityType.Rest);
            }

            AddSceneMapInteraction(sleep, sleep == null ? "查看床" : "早点休息", "在床边休息，恢复精力并降低压力。", new Vector2(-250f, 20f), new Vector2(192f, 84f), false, bedCenter);

            Vector2 diningCenter = new Vector2(112f, 54f);
            CreateSceneMapDebugObject("HomeDiningTable", "餐桌", diningCenter, new Vector2(180f, 96f), new Color(0.20f, 0.15f, 0.10f, 0.98f), amber);
            AddSceneMapBlocker(new Vector2(112f, 54f), new Vector2(190f, 106f));
            ActivityData dinner = FindSceneMapActivity(activities, "family_dinner", "家庭晚饭", ActivityType.Event, false);
            if (dinner == null)
            {
                dinner = FindSceneMapActivity(activities, "family", "晚饭", ActivityType.Event, false);
            }

            Vector2 deskCenter = new Vector2(-220f, -126f);
            CreateSceneMapDebugObject("HomeDesk", "书桌", deskCenter, new Vector2(142f, 72f), new Color(0.11f, 0.17f, 0.22f, 0.98f), green);
            AddSceneMapBlocker(new Vector2(-220f, -126f), new Vector2(154f, 82f));
            ActivityData plan = FindSceneMapActivity(activities, "weekend_plan", "周末安排", ActivityType.Event, false);
            AddSceneMapInteraction(plan, plan == null ? "查看书桌" : "整理计划", "在书桌前整理接下来的安排。", new Vector2(-220f, -50f), new Vector2(178f, 76f), false, deskCenter);

            Vector2 familyCenter = new Vector2(242f, 52f);
            CreateSceneMapDebugObject("HomeFamilyNpc", "家人", familyCenter, new Vector2(56f, 72f), new Color(0.28f, 0.21f, 0.16f, 0.98f), green);
            AddSceneMapBlocker(new Vector2(242f, 52f), new Vector2(64f, 80f));
            AddSceneMapInteraction(dinner, dinner == null ? "和家人说话" : "家庭晚饭聊天", "和家人聊聊近况。", new Vector2(242f, -16f), new Vector2(128f, 82f), false, familyCenter);

            AddSceneMapReturnPoint(new Vector2(316f, -132f), new Vector2(120f, 94f));
            sceneMapPlayerPosition = new Vector2(-18f, -148f);
            sceneMapFacingDirection = Vector2.up;
        }

        private void BuildGenericSceneMap(LocationData location, List<ActivityData> activities)
        {
            string propName = GetSceneMapPropName(location);
            Vector2 primaryPropCenter = new Vector2(-230f, 86f);
            Vector2 boardCenter = new Vector2(168f, 104f);
            CreateSceneMapDebugObject("PrimaryMapProp", propName, primaryPropCenter, new Vector2(150f, 84f), new Color(0.14f, 0.20f, 0.18f, 0.98f), green);
            AddSceneMapBlocker(new Vector2(-230f, 86f), new Vector2(162f, 94f));

            CreateSceneMapDebugObject("SecondaryMapProp", "告示板", boardCenter, new Vector2(132f, 70f), new Color(0.12f, 0.16f, 0.12f, 0.98f), amber);
            AddSceneMapBlocker(new Vector2(168f, 104f), new Vector2(142f, 80f));

            Vector2[] interactionPositions =
            {
                new Vector2(-230f, -10f),
                new Vector2(42f, 38f),
                new Vector2(220f, -54f),
                new Vector2(-58f, -128f)
            };

            Vector2[] targetPositions =
            {
                primaryPropCenter,
                boardCenter,
                new Vector2(220f, 18f),
                new Vector2(-58f, -58f)
            };

            for (int i = 0; i < activities.Count && i < interactionPositions.Length; i++)
            {
                ActivityData activity = activities[i];
                AddSceneMapInteraction(activity, activity.displayName, GetActivityMapDescription(activity), interactionPositions[i], new Vector2(186f, 78f), false, targetPositions[i]);
            }

            if (activities.Count == 0)
            {
                Text empty = CreateText("SceneMapEmptyText", sceneMapPlayArea, "当前地点暂无可执行互动。", 22, FontStyle.Bold, TextAnchor.MiddleCenter, muted);
                SetCenter(empty.rectTransform, new Vector2(0f, 6f), new Vector2(360f, 70f));
            }

            AddSceneMapReturnPoint(new Vector2(316f, -132f), new Vector2(120f, 94f));
            sceneMapPlayerPosition = new Vector2(-18f, -148f);
            sceneMapFacingDirection = Vector2.up;
        }

        private string GetSceneMapPropName(LocationData location)
        {
            if (location == null)
            {
                return "互动点";
            }

            switch (location.locationType)
            {
                case LocationType.School:
                    return "课桌";
                case LocationType.Shop:
                    return "柜台";
                case LocationType.Park:
                    return "长椅";
                case LocationType.Library:
                    return "书架";
                case LocationType.Club:
                    return "社团桌";
                default:
                    return "互动点";
            }
        }

        private string GetActivityMapDescription(ActivityData activity)
        {
            if (activity == null)
            {
                return string.Empty;
            }

            return string.IsNullOrEmpty(activity.description)
                ? activity.displayName
                : activity.description;
        }

        private void CreateSceneMapPlayer()
        {
            sceneMapPlayer = CreateEmpty("SceneMapPlayer", sceneMapPlayArea);
            SetCenter(sceneMapPlayer, sceneMapPlayerPosition, new Vector2(48f, 48f));
            sceneMapPlayerImage = CreateImage("SceneMapPlayerSprite", sceneMapPlayer, GetSolidSprite(), new Color(0.16f, 0.36f, 0.48f, 1f));
            Stretch(sceneMapPlayerImage.rectTransform);
            sceneMapPlayerImage.preserveAspect = true;
            sceneMapPlayerAnimationTimer = 0f;
            sceneMapPlayerAnimationFrame = 0;
            UpdateSceneMapPlayerVisual(false);
            sceneMapPlayer.SetAsLastSibling();
        }

        private RectTransform CreateSceneMapObject(string name, string label, Vector2 center, Vector2 size, Color fillColor, Color borderColor)
        {
            RectTransform rect = CreatePanel(name, sceneMapPlayArea, fillColor, borderColor);
            SetCenter(rect, center, size);

            if (!string.IsNullOrEmpty(label))
            {
                Text labelText = CreateText(name + "Label", rect, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter, text);
                Stretch(labelText.rectTransform, 4f);
            }

            return rect;
        }

        private RectTransform CreateSceneMapDebugObject(string name, string label, Vector2 center, Vector2 size, Color fillColor, Color borderColor)
        {
            RectTransform rect = CreateSceneMapObject(name, label, center, size, fillColor, borderColor);
            rect.gameObject.SetActive(sceneMapDebugVisible);
            sceneMapDebugObjects.Add(rect);
            return rect;
        }

        private void AddSceneMapBlocker(Vector2 center, Vector2 size)
        {
            sceneMapBlockers.Add(CreateCenteredRect(center, size));
        }

        private void AddSceneMapInteraction(ActivityData activity, string fallbackName, string description, Vector2 center, Vector2 size, bool returnsToMain, Vector2 facingTarget)
        {
            if (activity == null && !returnsToMain && string.IsNullOrEmpty(fallbackName))
            {
                return;
            }

            string displayName = returnsToMain
                ? "返回阶段地图"
                : activity != null && !string.IsNullOrEmpty(activity.displayName) ? activity.displayName : fallbackName;

            RectTransform debugRect = CreateSceneMapObject(
                "InteractionDebugZone",
                displayName,
                center,
                size,
                returnsToMain ? new Color(0.18f, 0.28f, 0.16f, 0.18f) : new Color(0.82f, 0.64f, 0.18f, 0.16f),
                returnsToMain ? green : amber);
            debugRect.gameObject.SetActive(sceneMapDebugVisible);

            Color markerFill = returnsToMain
                ? new Color(0.10f, 0.22f, 0.14f, 0.94f)
                : new Color(0.22f, 0.16f, 0.06f, 0.94f);
            Color markerBorder = returnsToMain ? green : amber;
            RectTransform marker = CreateSceneMapObject(
                "InteractionMarker",
                returnsToMain ? "E" : "E",
                center,
                returnsToMain ? new Vector2(46f, 32f) : new Vector2(38f, 32f),
                markerFill,
                markerBorder);
            marker.gameObject.SetActive(sceneMapDebugVisible);

            sceneMapInteractions.Add(new SceneMapInteractionPoint
            {
                displayName = displayName,
                description = description,
                activity = activity,
                returnsToMain = returnsToMain,
                interactionRect = CreateCenteredRect(center, size),
                facingTarget = facingTarget,
                marker = marker,
                debugRect = debugRect,
                markerFill = markerFill,
                markerBorder = markerBorder
            });
        }

        private void AddSceneMapReturnPoint(Vector2 center, Vector2 size)
        {
            Vector2 doorCenter = new Vector2(center.x, center.y + 50f);
            CreateSceneMapDebugObject("ExitDoor", "门", doorCenter, new Vector2(82f, 112f), new Color(0.10f, 0.15f, 0.11f, 0.98f), green);
            AddSceneMapInteraction(null, "返回阶段地图", "离开当前地点，回到阶段地图。", center, size, true, doorCenter);
        }

        private static Rect CreateCenteredRect(Vector2 center, Vector2 size)
        {
            return new Rect(center.x - size.x * 0.5f, center.y - size.y * 0.5f, size.x, size.y);
        }

        private void UpdateSceneMapInput()
        {
            if (GameplayInputGate.IsBlocked)
            {
                UpdateSceneMapPlayerVisual(false);
                return;
            }

            if (!sceneMapActive || sceneMapPlayer == null)
            {
                return;
            }

            if (sceneMapConfirmOpen)
            {
                UpdateSceneMapPlayerVisual(false);
                return;
            }

            if (IsSceneMapKeyPressed(KeyCode.F3, "f3Key"))
            {
                ToggleSceneMapDebugOverlay();
            }

            Vector2 input = Vector2.zero;
            if (IsSceneMapKeyHeld(KeyCode.A, "aKey") || IsSceneMapKeyHeld(KeyCode.LeftArrow, "leftArrowKey"))
            {
                input.x -= 1f;
            }

            if (IsSceneMapKeyHeld(KeyCode.D, "dKey") || IsSceneMapKeyHeld(KeyCode.RightArrow, "rightArrowKey"))
            {
                input.x += 1f;
            }

            if (IsSceneMapKeyHeld(KeyCode.W, "wKey") || IsSceneMapKeyHeld(KeyCode.UpArrow, "upArrowKey"))
            {
                input.y += 1f;
            }

            if (IsSceneMapKeyHeld(KeyCode.S, "sKey") || IsSceneMapKeyHeld(KeyCode.DownArrow, "downArrowKey"))
            {
                input.y -= 1f;
            }

            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            if (input.sqrMagnitude > 0f)
            {
                sceneMapFacingDirection = MapFacingInteractionUtility.ToCardinal(input, sceneMapFacingDirection);
                UpdateSceneMapFacingIndicator();
                MoveSceneMapPlayer(input * SceneMapMoveSpeed * Time.unscaledDeltaTime);
                UpdateSceneMapPlayerVisual(true);
            }
            else
            {
                UpdateSceneMapPlayerVisual(false);
            }

            RefreshSceneMapFocus();

            if (IsSceneMapKeyPressed(KeyCode.E, "eKey")
                || IsSceneMapKeyPressed(KeyCode.Space, "spaceKey")
                || IsSceneMapKeyPressed(KeyCode.Return, "enterKey"))
            {
                TryStartSceneMapInteraction();
            }
        }

        private void MoveSceneMapPlayer(Vector2 delta)
        {
            Vector2 next = sceneMapPlayerPosition;
            next.x = Mathf.Clamp(next.x + delta.x, -SceneMapWidth * 0.5f + SceneMapPlayerRadius, SceneMapWidth * 0.5f - SceneMapPlayerRadius);
            if (CanSceneMapPlayerOccupy(next))
            {
                sceneMapPlayerPosition.x = next.x;
            }

            next = sceneMapPlayerPosition;
            next.y = Mathf.Clamp(next.y + delta.y, -SceneMapHeight * 0.5f + SceneMapPlayerRadius, SceneMapHeight * 0.5f - SceneMapPlayerRadius);
            if (CanSceneMapPlayerOccupy(next))
            {
                sceneMapPlayerPosition.y = next.y;
            }

            sceneMapPlayer.anchoredPosition = sceneMapPlayerPosition;
            SortSceneMapVisualsByY();
        }

        private void UpdateSceneMapFacingIndicator()
        {
            if (sceneMapFacingIndicator == null)
            {
                return;
            }

            Vector2 direction = MapFacingInteractionUtility.ToCardinal(sceneMapFacingDirection, Vector2.up);
            SetCenter(sceneMapFacingIndicator, direction * 16f, new Vector2(8f, 8f));
        }

        private void UpdateSceneMapPlayerVisual(bool moving)
        {
            if (sceneMapPlayerImage == null)
            {
                return;
            }

            if (moving)
            {
                sceneMapPlayerAnimationTimer += Time.unscaledDeltaTime;
                while (sceneMapPlayerAnimationTimer >= SceneMapPlayerWalkFrameDuration)
                {
                    sceneMapPlayerAnimationTimer -= SceneMapPlayerWalkFrameDuration;
                    sceneMapPlayerAnimationFrame = (sceneMapPlayerAnimationFrame + 1) % SceneMapPlayerFrameCount;
                }
            }
            else
            {
                sceneMapPlayerAnimationTimer = 0f;
                sceneMapPlayerAnimationFrame = 0;
            }

            Sprite sprite = GetSceneMapPlayerSprite(sceneMapFacingDirection, sceneMapPlayerAnimationFrame);
            if (sprite != null)
            {
                sceneMapPlayerImage.sprite = sprite;
                sceneMapPlayerImage.color = Color.white;
            }
        }

        private Sprite GetSceneMapPlayerSprite(Vector2 direction, int frameIndex)
        {
            EnsureSceneMapPlayerSpritesLoaded();
            if (sceneMapPlayerSprites == null)
            {
                return null;
            }

            int directionIndex = GetSceneMapPlayerDirectionIndex(direction);
            int safeFrameIndex = Mathf.Clamp(frameIndex, 0, SceneMapPlayerFrameCount - 1);
            return sceneMapPlayerSprites[directionIndex, safeFrameIndex];
        }

        private void EnsureSceneMapPlayerSpritesLoaded()
        {
            if (sceneMapPlayerSpriteSheetTexture != null && sceneMapPlayerSprites != null)
            {
                sceneMapPlayerSpritesLoaded = true;
                return;
            }

            if (sceneMapPlayerSpritesLoaded)
            {
                return;
            }

            sceneMapPlayerSpritesLoaded = true;
            string path = GetFullPath(SceneMapPlayerSpriteSheetPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            sceneMapPlayerSpriteSheetTexture = texture;
            sceneMapPlayerSprites = new Sprite[4, SceneMapPlayerFrameCount];
            for (int directionIndex = 0; directionIndex < 4; directionIndex++)
            {
                for (int frameIndex = 0; frameIndex < SceneMapPlayerFrameCount; frameIndex++)
                {
                    Rect rect = new Rect(
                        frameIndex * SceneMapPlayerFrameSize,
                        (3 - directionIndex) * SceneMapPlayerFrameSize,
                        SceneMapPlayerFrameSize,
                        SceneMapPlayerFrameSize);
                    Sprite sprite = Sprite.Create(texture, rect, Vector2.one * 0.5f, 16f);
                    sprite.name = BuildSceneMapPlayerSpriteName(directionIndex, frameIndex);
                    sceneMapPlayerSprites[directionIndex, frameIndex] = sprite;
                }
            }
        }

        private static int GetSceneMapPlayerDirectionIndex(Vector2 direction)
        {
            Vector2 cardinal = MapFacingInteractionUtility.ToCardinal(direction, Vector2.down);
            if (cardinal == Vector2.left)
            {
                return 1;
            }

            if (cardinal == Vector2.right)
            {
                return 2;
            }

            if (cardinal == Vector2.up)
            {
                return 3;
            }

            return 0;
        }

        private static string BuildSceneMapPlayerSpriteName(int directionIndex, int frameIndex)
        {
            string direction;
            switch (directionIndex)
            {
                case 1:
                    direction = "left";
                    break;
                case 2:
                    direction = "right";
                    break;
                case 3:
                    direction = "up";
                    break;
                default:
                    direction = "down";
                    break;
            }

            switch (frameIndex)
            {
                case 1:
                    return "player_main_" + direction + "_step_l";
                case 2:
                    return "player_main_" + direction + "_idle_1";
                case 3:
                    return "player_main_" + direction + "_step_r";
                default:
                    return "player_main_" + direction + "_idle_0";
            }
        }

        private bool CanSceneMapPlayerOccupy(Vector2 position)
        {
            Rect playerRect = CreateCenteredRect(position, new Vector2(SceneMapPlayerRadius * 2f, SceneMapPlayerRadius * 2f));
            for (int i = 0; i < sceneMapBlockers.Count; i++)
            {
                if (playerRect.Overlaps(sceneMapBlockers[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private void RefreshSceneMapFocus()
        {
            SceneMapInteractionPoint next = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < sceneMapInteractions.Count; i++)
            {
                SceneMapInteractionPoint point = sceneMapInteractions[i];
                if (!MapFacingInteractionUtility.IsInsideRangeAndFacing(
                    point.interactionRect,
                    sceneMapPlayerPosition,
                    sceneMapFacingDirection,
                    point.facingTarget))
                {
                    continue;
                }

                float distance = Vector2.SqrMagnitude(point.interactionRect.center - sceneMapPlayerPosition);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    next = point;
                }
            }

            focusedSceneMapInteraction = next;
            UpdateSceneMapInteractionVisuals();
            SortSceneMapVisualsByY();
            if (sceneMapPromptText != null)
            {
                sceneMapPromptText.text = BuildSceneMapPrompt(focusedSceneMapInteraction);
            }
        }

        private string BuildSceneMapPrompt(SceneMapInteractionPoint point)
        {
            if (point == null)
            {
                return MapInteractionPromptFormatter.BuildDefaultPrompt(sceneMapDebugVisible);
            }

            if (point.returnsToMain)
            {
                return MapInteractionPromptFormatter.BuildExitPrompt();
            }

            if (point.activity == null)
            {
                return MapInteractionPromptFormatter.BuildUnavailablePrompt(point.displayName);
            }

            if (!CanSpendActivityTime(point.activity))
            {
                return "不可用：" + point.displayName + "｜时间不足，无法执行该行动。";
            }

            return MapInteractionPromptFormatter.BuildActivityPrompt(
                point.displayName,
                point.activity.timeCost,
                BuildActivityRewardPreview(point.activity));
        }

        private void ToggleSceneMapDebugOverlay()
        {
            SetSceneMapDebugVisible(!sceneMapDebugVisible);
            SetSceneMapFeedback(sceneMapDebugVisible ? "调试范围已显示，仅用于检查互动触发区。" : "调试范围已隐藏。");
            RefreshSceneMapFocus();
        }

        private void SetSceneMapDebugVisible(bool visible)
        {
            sceneMapDebugVisible = visible;
            for (int i = 0; i < sceneMapDebugObjects.Count; i++)
            {
                if (sceneMapDebugObjects[i] != null)
                {
                    sceneMapDebugObjects[i].gameObject.SetActive(visible);
                }
            }

            for (int i = 0; i < sceneMapInteractions.Count; i++)
            {
                SceneMapInteractionPoint point = sceneMapInteractions[i];
                RectTransform debugRect = point.debugRect;
                if (debugRect != null)
                {
                    debugRect.gameObject.SetActive(visible);
                }

                if (point.marker != null)
                {
                    point.marker.gameObject.SetActive(visible || point == focusedSceneMapInteraction);
                }
            }

            SortSceneMapVisualsByY();
        }

        private void UpdateSceneMapInteractionVisuals()
        {
            for (int i = 0; i < sceneMapInteractions.Count; i++)
            {
                SceneMapInteractionPoint point = sceneMapInteractions[i];
                bool focused = point == focusedSceneMapInteraction;
                SetSceneMapMarkerVisual(point, focused);
            }
        }

        private void SetSceneMapMarkerVisual(SceneMapInteractionPoint point, bool focused)
        {
            if (point == null || point.marker == null)
            {
                return;
            }

            point.marker.gameObject.SetActive(sceneMapDebugVisible || focused);
            Image image = point.marker.GetComponent<Image>();
            bool unavailable = !point.returnsToMain && (point.activity == null || !CanSpendActivityTime(point.activity));
            if (image != null)
            {
                Color fillColor = point.markerFill;
                if (unavailable)
                {
                    fillColor = new Color(0.16f, 0.18f, 0.16f, 0.86f);
                }
                else if (focused)
                {
                    fillColor = new Color(0.92f, 0.74f, 0.38f, 0.96f);
                }

                image.color = fillColor;
            }

            Color borderColor = point.markerBorder;
            if (unavailable)
            {
                borderColor = muted;
            }
            else if (focused)
            {
                borderColor = text;
            }

            SetSceneMapBorderColor(point.marker, borderColor);
            point.marker.localScale = focused ? new Vector3(1.12f, 1.12f, 1f) : Vector3.one;
        }

        private void SortSceneMapVisualsByY()
        {
            if (sceneMapPlayArea == null)
            {
                return;
            }

            List<RectTransform> visuals = new List<RectTransform>();
            AddSceneMapVisualIfActive(visuals, sceneMapPlayer);
            for (int i = 0; i < sceneMapDebugObjects.Count; i++)
            {
                AddSceneMapVisualIfActive(visuals, sceneMapDebugObjects[i]);
            }

            for (int i = 0; i < sceneMapInteractions.Count; i++)
            {
                AddSceneMapVisualIfActive(visuals, sceneMapInteractions[i].marker);
                AddSceneMapVisualIfActive(visuals, sceneMapInteractions[i].debugRect);
            }

            visuals.Sort((left, right) => right.anchoredPosition.y.CompareTo(left.anchoredPosition.y));
            for (int i = 0; i < visuals.Count; i++)
            {
                visuals[i].SetAsLastSibling();
            }
        }

        private static void AddSceneMapVisualIfActive(List<RectTransform> visuals, RectTransform rect)
        {
            if (rect != null && rect.gameObject.activeSelf)
            {
                visuals.Add(rect);
            }
        }

        private static void SetSceneMapBorderColor(RectTransform rect, Color color)
        {
            if (rect == null)
            {
                return;
            }

            Image[] images = rect.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < images.Length; i++)
            {
                if (images[i] != null && images[i].transform != rect)
                {
                    images[i].color = color;
                }
            }
        }

        private void TryStartSceneMapInteraction()
        {
            if (focusedSceneMapInteraction == null)
            {
                SetSceneMapFeedback("需要靠近并面向可互动对象。");
                return;
            }

            if (focusedSceneMapInteraction.returnsToMain)
            {
                ReturnFromSceneMap();
                return;
            }

            ActivityData activity = focusedSceneMapInteraction.activity;
            if (activity == null)
            {
                SetSceneMapFeedback("当前没有可执行行动。");
                return;
            }

            if (!CanSpendActivityTime(activity))
            {
                SetSceneMapFeedback("时间不足，无法进行该行动。");
                return;
            }

            ShowSceneMapConfirm(focusedSceneMapInteraction);
        }

        private void ShowSceneMapConfirm(SceneMapInteractionPoint point)
        {
            CloseSceneMapConfirm();
            sceneMapConfirmOpen = true;

            sceneMapConfirmDialog = CreatePanel("InteractionConfirmDialog", contentRoot, new Color(0.035f, 0.055f, 0.052f, 0.98f), amber);
            sceneMapConfirmDialog.SetAsLastSibling();
            SetCenter(sceneMapConfirmDialog, Vector2.zero, new Vector2(560f, 330f));

            Text title = CreateText("InteractionTitle", sceneMapConfirmDialog, point.displayName, 28, FontStyle.Bold, TextAnchor.UpperLeft, text);
            SetTop(title.rectTransform, 28f, 28f, 24f, 42f);

            ActivityData activity = point.activity;
            UIThemeConfig theme = GetRuntimeTheme();
            CreateRuntimeInfoStrip(
                sceneMapConfirmDialog,
                "InteractionDescriptionStrip",
                ResolveActivityIcon(theme, activity),
                ResolveActivityAccentColor(theme, activity),
                "互动说明",
                string.IsNullOrEmpty(point.description) ? point.displayName : point.description,
                28f,
                28f,
                82f,
                54f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                sceneMapConfirmDialog,
                "InteractionTimeStrip",
                theme.timeIcon,
                theme.gold,
                "时间消耗",
                FormatTimeCost(activity == null ? 0 : activity.timeCost),
                28f,
                28f,
                146f,
                48f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                sceneMapConfirmDialog,
                "InteractionRewardStrip",
                theme.confirmIcon,
                theme.success,
                "预计效果",
                BuildSceneMapRewardStripText(activity),
                28f,
                28f,
                204f,
                48f,
                GameUiPanelVariant.Dark);

            Button confirm = CreateButton(sceneMapConfirmDialog, "确认", new Vector2(-82f, -246f), new Vector2(136f, 54f));
            confirm.onClick.AddListener(() => CompleteSceneMapInteraction(point));

            Button cancel = CreateButton(sceneMapConfirmDialog, "取消", new Vector2(82f, -246f), new Vector2(136f, 54f));
            cancel.onClick.AddListener(() =>
            {
                CloseSceneMapConfirm();
                SetSceneMapFeedback("已取消互动，未消耗时间。");
            });
        }

        private void CompleteSceneMapInteraction(SceneMapInteractionPoint point)
        {
            CloseSceneMapConfirm();
            ActivityData activity = point.activity;
            LocationActionExecutionResult executionResult = ExecuteActivity(activity, LocationActionSource.Map);
            if (!executionResult.success)
            {
                SetSceneMapFeedback(executionResult.message);
                return;
            }

            ActivityResult result = executionResult.activityResult;
            statusText.text = BuildSceneMapStatusText();
            if (sceneMapStatusText != null)
            {
                sceneMapStatusText.text = BuildSceneMapStatusText();
            }

            string resultText = "完成互动：" + activity.displayName
                + "\n时间消耗：" + FormatSpentTime(activity.timeCost)
                + BuildRewardSummary(result == null ? null : result.rewards);
            if (stageController.ShouldStartBoss())
            {
                resultText += "\n阶段时间已用完，Boss 已解锁。";
            }

            SetSceneMapFeedback(resultText);
            SetLogMessage("地图反馈", resultText);
        }

        private LocationActionExecutionResult ExecuteActivity(ActivityData activity, LocationActionSource source)
        {
            if (locationActionExecutor == null)
            {
                locationActionExecutor = gameObject.GetComponent<LocationActionExecutor>();
                if (locationActionExecutor == null)
                {
                    locationActionExecutor = gameObject.AddComponent<LocationActionExecutor>();
                }
            }

            locationActionExecutor.Configure(
                sessionState,
                stageController,
                activityController,
                miniGameController,
                sceneTransitionController,
                timeActionController);
            return locationActionExecutor.TryExecute(new LocationActionExecutionRequest
            {
                source = source,
                activity = activity
            });
        }

        private void CloseSceneMapConfirm()
        {
            sceneMapConfirmOpen = false;
            if (sceneMapConfirmDialog != null)
            {
                Destroy(sceneMapConfirmDialog.gameObject);
                sceneMapConfirmDialog = null;
            }
        }

        private void ReturnFromSceneMap()
        {
            string message = "已返回阶段地图。返回不消耗时间。";
            if (locationSceneRouter != null)
            {
                LocationSceneRouteResult routeResult = locationSceneRouter.ReturnToStageMap();
                message = routeResult.message;
                if (routeResult.didLoadUnityScene)
                {
                    return;
                }
            }
            else if (sceneTransitionController != null)
            {
                sceneTransitionController.ShowMap();
            }

            ShowStageScreen(message);
        }

        private void SetSceneMapFeedback(string value)
        {
            if (sceneMapFeedbackText != null)
            {
                sceneMapFeedbackText.text = value;
            }
        }

        private bool CanSpendActivityTime(ActivityData activity)
        {
            return activity != null
                && sessionState != null
                && sessionState.stageState.remainingTime >= Mathf.Max(0, activity.timeCost);
        }

        private string BuildSceneMapStatusText()
        {
            PlayerState player = sessionState.playerState;
            return "时间 " + sessionState.stageState.remainingTime
                + "｜精力 " + player.energy + "/" + player.maxEnergy
                + "｜心情 " + player.mood
                + "｜亲子 " + player.parentRelation
                + "｜金钱 " + player.money;
        }

        private string FormatTimeCost(int cost)
        {
            int safeCost = Mathf.Max(0, cost);
            return safeCost == 0 ? "不消耗时间" : "消耗 " + safeCost + " 小时";
        }

        private string FormatSpentTime(int cost)
        {
            int safeCost = Mathf.Max(0, cost);
            return safeCost == 0 ? "不消耗时间" : safeCost + " 小时";
        }

        private string BuildActivityRewardPreview(ActivityData activity)
        {
            if (activity == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            AddRewardPreviewParts(parts, activity.fixedRewards);
            if (activity.linkedEvent != null && activity.linkedEvent.choices.Count > 0)
            {
                AddRewardPreviewParts(parts, activity.linkedEvent.choices[0].rewards);
            }

            if (activity.linkedMiniGame != null)
            {
                AddRewardPreviewParts(parts, activity.linkedMiniGame.successRewards);
            }

            return parts.Count == 0 ? "\n效果：根据当前事件结算" : "\n效果：" + string.Join("，", parts.ToArray());
        }

        private string BuildSceneMapRewardStripText(ActivityData activity)
        {
            string preview = BuildActivityRewardPreview(activity);
            if (string.IsNullOrEmpty(preview))
            {
                return "根据当前事件结算";
            }

            return preview.TrimStart('\n').Replace("效果：", string.Empty);
        }

        private void AddRewardPreviewParts(List<string> parts, IReadOnlyList<RewardData> rewards)
        {
            if (parts == null || rewards == null)
            {
                return;
            }

            for (int i = 0; i < rewards.Count && parts.Count < 4; i++)
            {
                string summary = BuildRewardSummary(rewards[i]);
                if (!string.IsNullOrEmpty(summary))
                {
                    parts.Add(summary);
                }
            }
        }

        private ActivityData FindSceneMapActivity(List<ActivityData> activities, string idPart, string namePart, ActivityType expectedType, bool requireType)
        {
            if (activities == null)
            {
                return null;
            }

            for (int i = 0; i < activities.Count; i++)
            {
                ActivityData activity = activities[i];
                if (activity == null || requireType && activity.activityType != expectedType)
                {
                    continue;
                }

                string id = activity.activityId == null ? string.Empty : activity.activityId.ToLowerInvariant();
                string displayName = activity.displayName ?? string.Empty;
                bool idMatches = !string.IsNullOrEmpty(idPart) && id.Contains(idPart.ToLowerInvariant());
                bool nameMatches = !string.IsNullOrEmpty(namePart) && displayName.Contains(namePart);
                if (idMatches || nameMatches)
                {
                    return activity;
                }
            }

            return null;
        }

        private ActivityData FindSceneMapActivityByType(List<ActivityData> activities, ActivityType activityType)
        {
            if (activities == null)
            {
                return null;
            }

            for (int i = 0; i < activities.Count; i++)
            {
                ActivityData activity = activities[i];
                if (activity != null && activity.activityType == activityType)
                {
                    return activity;
                }
            }

            return null;
        }

        private bool IsSceneMapKeyHeld(KeyCode legacyKey, string inputSystemKey)
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetKey(legacyKey))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            return ReadInputSystemKey(inputSystemKey, "isPressed");
        }

        private bool IsSceneMapKeyPressed(KeyCode legacyKey, string inputSystemKey)
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetKeyDown(legacyKey))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            return ReadInputSystemKey(inputSystemKey, "wasPressedThisFrame");
        }

        private static bool ReadInputSystemKey(string keyPropertyName, string statePropertyName)
        {
            Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType == null)
            {
                return false;
            }

            object keyboard = keyboardType.GetProperty("current")?.GetValue(null, null);
            if (keyboard == null)
            {
                return false;
            }

            object key = keyboardType.GetProperty(keyPropertyName)?.GetValue(keyboard, null);
            if (key == null)
            {
                return false;
            }

            object value = key.GetType().GetProperty(statePropertyName)?.GetValue(key, null);
            return value is bool boolValue && boolValue;
        }

        private List<ActivityData> GetActivitiesForLocation(LocationData location)
        {
            List<ActivityData> result = new List<ActivityData>();
            if (location == null || stageController == null)
            {
                return result;
            }

            IReadOnlyList<ActivityData> activities = stageController.GetAvailableActivities();
            for (int i = 0; i < activities.Count; i++)
            {
                ActivityData activity = activities[i];
                if (IsActivityInLocation(location, activity))
                {
                    result.Add(activity);
                }
            }

            return result;
        }

        private bool IsActivityInLocation(LocationData location, ActivityData activity)
        {
            if (location == null || activity == null)
            {
                return false;
            }

            string id = activity.activityId == null ? string.Empty : activity.activityId.ToLowerInvariant();
            switch (location.locationType)
            {
                case LocationType.Home:
                    return activity.activityType == ActivityType.Rest || id.Contains("family") || id.Contains("home") || id.Contains("weekend");
                case LocationType.School:
                    return activity.activityType == ActivityType.Training
                        || activity.activityType == ActivityType.MiniGame
                        || activity.activityType == ActivityType.CardBattle
                        || id.Contains("class")
                        || id.Contains("quiz")
                        || id.Contains("word")
                        || id.Contains("math");
                case LocationType.Shop:
                    return activity.activityType == ActivityType.Shop;
                case LocationType.Park:
                    return id.Contains("music") || id.Contains("club") || id.Contains("friend") || id.Contains("walk");
                case LocationType.Library:
                    return id.Contains("review") || id.Contains("book") || id.Contains("formula") || id.Contains("focus");
                case LocationType.Club:
                    return id.Contains("club") || activity.activityType == ActivityType.Event && id.Contains("social");
                default:
                    return false;
            }
        }

        private void BuildStudentPanel(RectTransform parent)
        {
            CreateHeader(parent, "学生状态");
            PlayerState p = sessionState.playerState;
            UIThemeConfig theme = GetRuntimeTheme();
            CreateRuntimeInfoStrip(
                parent,
                "SubjectStatsStrip",
                theme.cardsIcon,
                theme.gold,
                "三科",
                "语 " + p.chinese + " / 数 " + p.math + " / 英 " + p.english,
                18f,
                18f,
                86f,
                54f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                parent,
                "EnergyStateStrip",
                theme.energyIcon,
                theme.success,
                "状态",
                "精力 " + p.energy + "/" + p.maxEnergy + " / 压力 " + p.pressure + " / 心情 " + p.mood,
                18f,
                18f,
                150f,
                54f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                parent,
                "GrowthStatsStrip",
                theme.relationIcon,
                theme.gold,
                "成长",
                "亲子 " + p.parentRelation + " / 社交 " + p.social + " / 兴趣 " + p.interest,
                18f,
                18f,
                214f,
                54f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                parent,
                "InnerStatsStrip",
                theme.moodIcon,
                theme.success,
                "内在",
                "自信 " + p.confidence + " / 自主 " + p.autonomy + " / 金钱 " + p.money,
                18f,
                18f,
                278f,
                54f,
                GameUiPanelVariant.Dark);

            if (GameArtAssetCatalog.CharacterPortraitPaths.TryGetValue("char_player_student", out string portraitPath))
            {
                RectTransform portraitAnchor = CreateEmpty("CharacterPortraitAnchor", parent);
                SetBottomCenter(portraitAnchor, new Vector2(0f, 64f), new Vector2(260f, 300f));

                Image portrait = CreateImage("PlayerCharacterPortrait", portraitAnchor, LoadSprite(portraitPath), Color.white);
                portrait.preserveAspect = true;
                SetStretch(portrait.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                CacheAvatarPortrait(portrait);
            }
        }

        private void BuildActivityPanel(RectTransform parent)
        {
            CreateHeader(parent, "可选活动");
            IReadOnlyList<ActivityData> activities = stageController.GetAvailableActivities();
            float y = -92f;
            for (int i = 0; i < activities.Count; i++)
            {
                ActivityData activity = activities[i];
                Button button = CreateLegacyActivityButton(parent, activity, new Vector2(0f, y), new Vector2(760f, 78f));
                ActivityData captured = activity;
                button.onClick.AddListener(() => OnActivityClicked(captured));
                y -= 94f;
            }

            if (stageController.ShouldStartBoss())
            {
                Button bossButton = CreateBossEntryButton(parent, new Vector2(0f, y - 12f), new Vector2(760f, 86f));
                bossButton.onClick.AddListener(StartBossScreen);
            }
        }

        private Button CreateLegacyActivityButton(RectTransform parent, ActivityData activity, Vector2 position, Vector2 size)
        {
            UIThemeConfig theme = GetRuntimeTheme();
            Color accent = ResolveActivityAccentColor(theme, activity);
            return CreateLegacyEntryButton(
                parent,
                "ActivityEntryButton",
                ResolveActivityIcon(theme, activity),
                accent,
                activity == null ? "未知活动" : activity.displayName,
                TruncateRuntimeText(ResolveActivityDescription(activity), 36),
                activity == null ? "活动" : GetActivitySubtitle(activity),
                accent,
                position,
                size);
        }

        private Button CreateBossEntryButton(RectTransform parent, Vector2 position, Vector2 size)
        {
            UIThemeConfig theme = GetRuntimeTheme();
            ChallengeData boss = sessionState.stageState.currentStage.bossChallenge;
            return CreateLegacyEntryButton(
                parent,
                "BossEntryButton",
                theme.cardsIcon,
                theme.danger,
                "进入 Boss 挑战",
                boss == null ? "阶段考验" : boss.displayName,
                "已解锁",
                theme.gold,
                position,
                size);
        }

        private static string ResolveActivityDescription(ActivityData activity)
        {
            return activity != null && !string.IsNullOrEmpty(activity.description)
                ? activity.description
                : "完成后消耗时间并推进成长。";
        }

        private string GetActivitySubtitle(ActivityData activity)
        {
            return GetActivityTypeLabel(activity.activityType) + "  " + FormatTimeCost(activity.timeCost);
        }

        private string GetActivityTypeLabel(ActivityType activityType)
        {
            switch (activityType)
            {
                case ActivityType.Event:
                    return "事件";
                case ActivityType.CardBattle:
                    return "挑战";
                case ActivityType.MiniGame:
                    return "小游戏";
                case ActivityType.Rest:
                    return "休息";
                case ActivityType.Shop:
                    return "商店";
                case ActivityType.Training:
                    return "学习";
                default:
                    return "行动";
            }
        }

        private static Sprite ResolveActivityIcon(UIThemeConfig theme, ActivityData activity)
        {
            if (activity != null && activity.icon != null)
            {
                return activity.icon;
            }

            if (activity == null)
            {
                return theme.timeIcon;
            }

            switch (activity.activityType)
            {
                case ActivityType.Event:
                    return theme.relationIcon;
                case ActivityType.CardBattle:
                case ActivityType.MiniGame:
                case ActivityType.Training:
                    return theme.cardsIcon;
                case ActivityType.Rest:
                    return theme.moodIcon;
                case ActivityType.Shop:
                    return theme.inventoryIcon;
                default:
                    return theme.timeIcon;
            }
        }

        private static Color ResolveActivityAccentColor(UIThemeConfig theme, ActivityData activity)
        {
            if (activity == null)
            {
                return theme.hintText;
            }

            switch (activity.activityType)
            {
                case ActivityType.Rest:
                    return theme.success;
                case ActivityType.Event:
                    return theme.gold;
                case ActivityType.Shop:
                    return theme.time;
                case ActivityType.CardBattle:
                    return theme.danger;
                default:
                    return theme.hintText;
            }
        }

        private void BuildInventoryPanel(RectTransform parent)
        {
            CreateHeader(parent, "道具 / 卡组");
            float y = -92f;
            for (int i = 0; i < sessionState.itemInventory.ownedItems.Count; i++)
            {
                ItemData item = sessionState.itemInventory.ownedItems[i];
                if (item == null)
                {
                    continue;
                }

                Button rowButton = CreateButton(parent, string.Empty, Vector2.zero, new Vector2(1f, 1f));
                RectTransform row = rowButton.transform as RectTransform;
                row.name = "ItemRow_" + i;
                SetTop(row, 20f, 20f, -y, 64f);
                rowButton.interactable = item.canUseInStageScreen;
                ItemData captured = item;
                rowButton.onClick.AddListener(() => OnItemClicked(captured));
                if (!string.IsNullOrEmpty(item.imagePath))
                {
                    Image icon = CreateImage("Icon", row, LoadSprite(item.imagePath), Color.white);
                    icon.preserveAspect = true;
                    SetLeftMiddle(icon.rectTransform, new Vector2(16f, 0f), new Vector2(42f, 42f));
                }

                Text label = CreateText("ItemText", row, item.displayName, 18, FontStyle.Normal, TextAnchor.MiddleLeft, text);
                SetStretch(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(72f, 6f), new Vector2(-16f, -6f));
                y -= 78f;
            }

            UIThemeConfig theme = GetRuntimeTheme();
            RectTransform summaryGroup = CreateEmpty("InventorySummaryGroup", parent);
            SetBottom(summaryGroup, 24f, 24f, 24f, 96f);
            CreateRuntimeInfoStrip(
                summaryGroup,
                "DeckCountStrip",
                theme.cardsIcon,
                theme.gold,
                "卡组",
                sessionState.deckState.masterDeck.Count + " 张",
                0f,
                0f,
                0f,
                44f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                summaryGroup,
                "BossWinStrip",
                theme.confirmIcon,
                theme.success,
                "Boss 胜利",
                sessionState.stageState.defeatedBossCount + " 次",
                0f,
                0f,
                52f,
                44f,
                GameUiPanelVariant.Dark);
        }

        private void OnActivityClicked(ActivityData activity)
        {
            LocationActionExecutionResult executionResult = ExecuteActivity(activity, LocationActionSource.LegacyButton);
            if (!executionResult.success)
            {
                SetLogMessage("课堂提示", executionResult.message);
                return;
            }

            ActivityResult result = executionResult.activityResult;
            if (stageController.ShouldStartBoss())
            {
                PlayAvatarActionAnimation(activity.activityType);
                if (IsAvatarAvailable())
                {
                    StartDeferredAvatarAction(ShowBossIntroAfterAvatarAnimation("阶段时间结束，Boss 已解锁。"));
                    return;
                }

                ShowBossIntro("阶段时间结束，Boss 已解锁。");
                return;
            }

            string resultMessage = result == null ? string.Empty : result.message;
            ShowStageScreen("完成活动：" + activity.displayName + "。" + resultMessage + BuildRewardSummary(result == null ? null : result.rewards));
            PlayAvatarActionAnimation(activity.activityType);
        }

        private void OnItemClicked(ItemData item)
        {
            ItemUseResult result = itemEffectResolver.UseItem(item, sessionState);
            if (!result.success)
            {
                SetLogMessage("课堂提示", result.message);
                return;
            }

            string itemName = item == null ? "道具" : item.displayName;
            ShowStageScreen("使用道具：" + itemName + "。");
            PlayItemUseAnimation();
        }

        private IEnumerator ShowBossIntroAfterAvatarAnimation(string message)
        {
            yield return new WaitForSeconds(0.62f);
            ShowBossIntro(message);
        }

        private void StartDeferredAvatarAction(IEnumerator deferredAction)
        {
            StopDeferredAvatarAction();
            avatarDeferredActionCoroutine = StartCoroutine(RunDeferredAvatarAction(deferredAction));
        }

        private IEnumerator RunDeferredAvatarAction(IEnumerator deferredAction)
        {
            yield return deferredAction;
            avatarDeferredActionCoroutine = null;
        }

        private void StopDeferredAvatarAction()
        {
            if (avatarDeferredActionCoroutine == null)
            {
                return;
            }

            StopCoroutine(avatarDeferredActionCoroutine);
            avatarDeferredActionCoroutine = null;
        }

        private void CacheAvatarPortrait(Image portrait)
        {
            avatarImage = portrait;
            avatarRect = portrait == null ? null : portrait.rectTransform;
            if (avatarRect == null)
            {
                return;
            }

            avatarDefaultAnchoredPosition = avatarRect.anchoredPosition;
            avatarDefaultScale = avatarRect.localScale;
            avatarDefaultRotation = avatarRect.localRotation;
            avatarDefaultColor = avatarImage.color;
        }

        private bool IsAvatarAvailable()
        {
            return avatarRect != null && avatarImage != null && avatarRect.gameObject.activeInHierarchy;
        }

        private void PlayAvatarActionAnimation(ActivityType activityType)
        {
            switch (activityType)
            {
                case ActivityType.Rest:
                    PlayRestAnimation();
                    return;
                case ActivityType.Event:
                    PlayChatAnimation();
                    return;
                case ActivityType.CardBattle:
                    PlayBossChallengeAnimation();
                    return;
                case ActivityType.Shop:
                    PlayItemUseAnimation();
                    return;
                default:
                    PlayStudyAnimation();
                    return;
            }
        }

        private void PlayStudyAnimation()
        {
            StartAvatarAnimation(StudyAnimation());
        }

        private void PlayRestAnimation()
        {
            StartAvatarAnimation(RestAnimation());
        }

        private void PlayChatAnimation()
        {
            StartAvatarAnimation(ChatAnimation());
        }

        private void PlayItemUseAnimation()
        {
            StartAvatarAnimation(ItemUseAnimation());
        }

        private void PlayBossChallengeAnimation()
        {
            StartAvatarAnimation(BossChallengeAnimation());
        }

        private void StartAvatarAnimation(IEnumerator animation)
        {
            if (!IsAvatarAvailable())
            {
                return;
            }

            StopAvatarAnimation();
            ResetAvatarTransform();
            avatarAnimationCoroutine = StartCoroutine(RunAvatarAnimation(animation));
        }

        private IEnumerator RunAvatarAnimation(IEnumerator animation)
        {
            yield return animation;
            ClearAvatarEffect();
            ResetAvatarTransform();
            avatarAnimationCoroutine = null;
        }

        private IEnumerator StudyAnimation()
        {
            CreateAvatarEffectText("专注", amber, new Vector2(64f, -8f));
            for (int i = 0; i < 2; i++)
            {
                yield return AnimateAvatarPose(new Vector2(0f, -6f), 1.04f, 0f, 1f, amber, 0.25f, 0.10f);
                yield return AnimateAvatarPose(Vector2.zero, 1f, 0f, 1f, Color.white, 0f, 0.10f);
            }
        }

        private IEnumerator RestAnimation()
        {
            CreateAvatarEffectText("Zzz", muted, new Vector2(62f, -10f));
            yield return AnimateAvatarPose(new Vector2(0f, -8f), 0.98f, -2f, 0.75f, muted, 0.12f, 0.28f);
            yield return AnimateAvatarPose(Vector2.zero, 1f, 0f, 1f, Color.white, 0f, 0.28f);
        }

        private IEnumerator ChatAnimation()
        {
            CreateAvatarEffectText("交流", amber, new Vector2(70f, -18f));
            yield return AnimateAvatarPose(new Vector2(0f, 10f), 1.06f, 0f, 1f, amber, 0.18f, 0.16f);
            yield return AnimateAvatarPose(Vector2.zero, 1f, 0f, 1f, Color.white, 0f, 0.18f);
            yield return AnimateAvatarPose(new Vector2(0f, 5f), 1.03f, 0f, 1f, amber, 0.10f, 0.10f);
            yield return AnimateAvatarPose(Vector2.zero, 1f, 0f, 1f, Color.white, 0f, 0.10f);
        }

        private IEnumerator ItemUseAnimation()
        {
            CreateAvatarEffectText("道具", amber, new Vector2(66f, -12f));
            yield return AnimateAvatarPose(Vector2.zero, 1.12f, 0f, 1f, amber, 0.28f, 0.14f);
            yield return AnimateAvatarPose(Vector2.zero, 1f, 0f, 1f, Color.white, 0f, 0.18f);
        }

        private IEnumerator BossChallengeAnimation()
        {
            CreateAvatarEffectText("!", warning, new Vector2(60f, -14f));
            for (int i = 0; i < 5; i++)
            {
                float x = i % 2 == 0 ? 7f : -7f;
                yield return AnimateAvatarPose(new Vector2(x, 0f), 1.03f, 0f, 1f, warning, 0.30f, 0.045f);
            }

            yield return AnimateAvatarPose(Vector2.zero, 1f, 0f, 1f, Color.white, 0f, 0.08f);
        }

        private IEnumerator AnimateAvatarPose(Vector2 offset, float scaleMultiplier, float rotationZ, float alpha, Color tint, float tintAmount, float duration)
        {
            if (!IsAvatarAvailable())
            {
                yield break;
            }

            Vector2 startPosition = avatarRect.anchoredPosition;
            Vector3 startScale = avatarRect.localScale;
            Quaternion startRotation = avatarRect.localRotation;
            Color startColor = avatarImage.color;
            Vector2 targetPosition = avatarDefaultAnchoredPosition + offset;
            Vector3 targetScale = avatarDefaultScale * scaleMultiplier;
            Quaternion targetRotation = avatarDefaultRotation * Quaternion.Euler(0f, 0f, rotationZ);
            Color targetColor = Color.Lerp(avatarDefaultColor, tint, tintAmount);
            targetColor.a = avatarDefaultColor.a * alpha;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                if (!IsAvatarAvailable())
                {
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 2f);
                avatarRect.anchoredPosition = Vector2.Lerp(startPosition, targetPosition, eased);
                avatarRect.localScale = Vector3.Lerp(startScale, targetScale, eased);
                avatarRect.localRotation = Quaternion.Slerp(startRotation, targetRotation, eased);
                avatarImage.color = Color.Lerp(startColor, targetColor, eased);
                yield return null;
            }

            avatarRect.anchoredPosition = targetPosition;
            avatarRect.localScale = targetScale;
            avatarRect.localRotation = targetRotation;
            avatarImage.color = targetColor;
        }

        private void ResetAvatarTransform()
        {
            if (!IsAvatarAvailable())
            {
                return;
            }

            avatarRect.anchoredPosition = avatarDefaultAnchoredPosition;
            avatarRect.localScale = avatarDefaultScale;
            avatarRect.localRotation = avatarDefaultRotation;
            avatarImage.color = avatarDefaultColor;
        }

        private Text CreateAvatarEffectText(string value, Color color, Vector2 anchoredPosition)
        {
            ClearAvatarEffect();
            if (!IsAvatarAvailable())
            {
                return null;
            }

            RectTransform parent = avatarRect.parent as RectTransform;
            if (parent == null)
            {
                return null;
            }

            Text effect = CreateText("AvatarActionEffect", parent, value, 22, FontStyle.Bold, TextAnchor.MiddleCenter, color);
            SetTopCenter(effect.rectTransform, anchoredPosition, new Vector2(92f, 30f));
            avatarEffectObject = effect.gameObject;
            return effect;
        }

        private void StopAvatarAnimation()
        {
            if (avatarAnimationCoroutine != null)
            {
                StopCoroutine(avatarAnimationCoroutine);
                avatarAnimationCoroutine = null;
            }

            ClearAvatarEffect();
            ResetAvatarTransform();
        }

        private void ClearAvatarEffect()
        {
            if (avatarEffectObject != null)
            {
                Destroy(avatarEffectObject);
                avatarEffectObject = null;
            }
        }

        private void ShowBossIntro(string message)
        {
            ClearContent();
            BattleUIConfig uiConfig = GetBattleUiConfig();
            StageData stage = sessionState.stageState.currentStage;
            ChallengeData boss = stage.bossChallenge;
            UpdateBossIntroTopStatus(boss);
            SetLogMessage("系统提示", message);

            RectTransform mainPanel = CreatePanel("MainChallengePanel", contentRoot, board, amber);
            Stretch(mainPanel);
            BattleUiStyler.ApplyPanel(mainPanel, uiConfig, GameUiPanelVariant.Dark);
            CreateHeader(mainPanel, "阶段 Boss");

            RectTransform centerBossPanel = CreatePanel("CenterBossPanel", mainPanel, new Color(0.055f, 0.095f, 0.082f, 0.98f), amber);
            SetStretch(centerBossPanel, Vector2.zero, new Vector2(0.62f, 1f), new Vector2(28f, 34f), new Vector2(-12f, -78f));
            BattleUiStyler.ApplyPanel(centerBossPanel, uiConfig, GameUiPanelVariant.Dark);
            BuildCenterBossPanel(centerBossPanel, boss);

            RectTransform functionPanel = CreatePanel("BossFunctionPanel", mainPanel, panel, green);
            SetStretch(functionPanel, new Vector2(0.64f, 0f), Vector2.one, new Vector2(12f, 34f), new Vector2(-28f, -78f));
            BattleUiStyler.ApplyPanel(functionPanel, uiConfig, GameUiPanelVariant.Standard);
            BuildBossFunctionPanel(functionPanel, boss);
        }

        private void BuildBossInfoPanel(RectTransform parent, ChallengeData boss)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            UIThemeConfig theme = BattleUiStyler.ResolveTheme(uiConfig);
            CreateHeader(parent, "Boss 信息");

            Text name = CreateText("BossNameText", parent, boss.displayName, 28, FontStyle.Bold, TextAnchor.UpperLeft, amber);
            SetTop(name.rectTransform, 24f, 24f, 82f, 42f);
            BattleUiStyler.ApplyText(name, uiConfig, GameUiTextStyle.Subtitle, true);

            CreateRuntimeInfoStrip(
                parent,
                "BossTypeStrip",
                theme.cardsIcon,
                theme.gold,
                "类型",
                "阶段 Boss",
                24f,
                24f,
                142f,
                52f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                parent,
                "BossStatusStrip",
                theme.confirmIcon,
                theme.success,
                "状态",
                "已解锁，可进入挑战",
                24f,
                24f,
                204f,
                52f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                parent,
                "BossStageStrip",
                theme.timeIcon,
                theme.gold,
                "阶段",
                "适应新学期 / 难度 普通",
                24f,
                24f,
                266f,
                52f,
                GameUiPanelVariant.Dark);

            RectTransform badge = CreatePanel("UnlockStatusBadge", parent, new Color(0.08f, 0.13f, 0.10f, 1f), amber);
            SetBottom(badge, 24f, 24f, 26f, 48f);
            BattleUiStyler.ApplyPanel(badge, uiConfig, GameUiPanelVariant.Dark);
            Text badgeText = CreateText("BadgeText", badge, "Boss 已解锁", 22, FontStyle.Bold, TextAnchor.MiddleCenter, amber);
            Stretch(badgeText.rectTransform, 4f);
            BattleUiStyler.ApplyText(badgeText, uiConfig, GameUiTextStyle.HintText, true);
        }

        private void BuildCenterBossPanel(RectTransform parent, ChallengeData boss)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            Text title = CreateText("CenterTitleText", parent, "课堂小测", 30, FontStyle.Bold, TextAnchor.UpperCenter, text);
            SetTop(title.rectTransform, 20f, 20f, 18f, 44f);
            BattleUiStyler.ApplyText(title, uiConfig, GameUiTextStyle.Title, true);

            RectTransform cardFrame = CreatePanel("BossCardFrame", parent, new Color(0.08f, 0.10f, 0.085f, 1f), amber);
            SetTopCenter(cardFrame, new Vector2(0f, -82f), new Vector2(300f, 372f));
            BattleUiStyler.ApplyPanel(cardFrame, uiConfig, GameUiPanelVariant.Standard);

            if (!string.IsNullOrEmpty(boss.imagePath))
            {
                Image bossImage = CreateImage("BossCardImage", cardFrame, LoadSprite(boss.imagePath), Color.white);
                bossImage.preserveAspect = true;
                Stretch(bossImage.rectTransform, 18f);
            }

            RectTransform statusBadge = CreatePanel("BossStatusBadge", cardFrame, new Color(0.11f, 0.08f, 0.04f, 1f), amber);
            SetBottom(statusBadge, 42f, 42f, 18f, 34f);
            BattleUiStyler.ApplyPanel(statusBadge, uiConfig, GameUiPanelVariant.Dark);
            Text statusText = CreateText("BossStatusText", statusBadge, "可挑战", 18, FontStyle.Bold, TextAnchor.MiddleCenter, amber);
            Stretch(statusText.rectTransform, 2f);
            BattleUiStyler.ApplyText(statusText, uiConfig, GameUiTextStyle.SmallText, true);

            RectTransform progressGroup = CreatePanel("ProgressGroup", parent, new Color(0.045f, 0.075f, 0.065f, 0.98f), green);
            SetBottom(progressGroup, 42f, 42f, 28f, 92f);
            BattleUiStyler.ApplyPanel(progressGroup, uiConfig, GameUiPanelVariant.Standard);
            Text progressTitle = CreateText("ProgressTitleText", progressGroup, "目标进度", 20, FontStyle.Bold, TextAnchor.UpperLeft, text);
            SetTop(progressTitle.rectTransform, 18f, 18f, 10f, 28f);
            BattleUiStyler.ApplyText(progressTitle, uiConfig, GameUiTextStyle.StatText);
            Image bossProgressFill;
            RectTransform progressBar = CreateProgressBar("BossProgressBar", progressGroup, 0f, new Color(0.02f, 0.05f, 0.045f, 1f), amber, out bossProgressFill);
            SetBottom(progressBar, 18f, 116f, 18f, 28f);
            BattleUiStyler.ApplyProgressBar(progressBar, bossProgressFill, uiConfig, BattleUiStyler.GetGoldColor(uiConfig));
            Text progressValue = CreateText("ProgressValueText", progressGroup, "0 / " + boss.targetProgress, 20, FontStyle.Bold, TextAnchor.MiddleRight, amber);
            SetBottom(progressValue.rectTransform, 0f, 18f, 18f, 30f);
            BattleUiStyler.ApplyText(progressValue, uiConfig, GameUiTextStyle.StatText);
        }

        private void BuildBossFunctionPanel(RectTransform parent, ChallengeData boss)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            CreateHeader(parent, "Boss 挑战");

            RectTransform tabBar = CreateEmpty("RightPanelTabBar", parent);
            SetTop(tabBar, 24f, 24f, 78f, 48f);

            bossInfoTabButton = CreateButton(tabBar, "Boss 信息", Vector2.zero, new Vector2(154f, 42f));
            SetTopLeft(bossInfoTabButton.transform as RectTransform, new Vector2(0f, 0f), new Vector2(154f, 42f));
            bossInfoTabButton.onClick.AddListener(ShowBossInfoTab);

            challengeTabButton = CreateButton(tabBar, "开始挑战", Vector2.zero, new Vector2(154f, 42f));
            SetTopLeft(challengeTabButton.transform as RectTransform, new Vector2(166f, 0f), new Vector2(154f, 42f));
            challengeTabButton.onClick.AddListener(ShowChallengeTab);

            bossInfoContent = CreateEmpty("BossInfoContent", parent);
            SetStretch(bossInfoContent, Vector2.zero, Vector2.one, new Vector2(24f, 72f), new Vector2(-24f, -144f));
            BuildBossInfoPanel(bossInfoContent, boss);

            challengeContent = CreateEmpty("ChallengeContent", parent);
            SetStretch(challengeContent, Vector2.zero, Vector2.one, new Vector2(24f, 72f), new Vector2(-24f, -144f));
            BuildChallengeContent(challengeContent, boss);

            Text footer = CreateText("RightPanelFooterText", parent, "胜利次数：" + sessionState.stageState.defeatedBossCount, 18, FontStyle.Normal, TextAnchor.LowerLeft, muted);
            SetBottom(footer.rectTransform, 24f, 24f, 22f, 30f);
            BattleUiStyler.ApplyText(footer, uiConfig, GameUiTextStyle.SmallText);

            ShowBossInfoTab();
        }

        private void BuildChallengeContent(RectTransform parent, ChallengeData boss)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            UIThemeConfig theme = BattleUiStyler.ResolveTheme(uiConfig);
            CreateRuntimeInfoStrip(
                parent,
                "ChallengeGoalStrip",
                theme.cardsIcon,
                theme.gold,
                "挑战目标",
                "在 " + boss.maxTurns + " 回合内达到 " + boss.targetProgress + " 进度",
                0f,
                0f,
                0f,
                64f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                parent,
                "ChallengeEnergyStrip",
                theme.energyIcon,
                theme.success,
                "挑战条件",
                "Boss 已解锁 / 精力 " + sessionState.playerState.energy + "/" + sessionState.playerState.maxEnergy,
                0f,
                0f,
                78f,
                64f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                parent,
                "ChallengeRiskStrip",
                theme.stressIcon,
                theme.danger,
                "风险提示",
                "当前压力 " + sessionState.playerState.pressure + "，失败可能继续增加压力",
                0f,
                0f,
                156f,
                64f,
                GameUiPanelVariant.Dark);

            Button start = CreateButton(parent, "开始挑战", Vector2.zero, new Vector2(230f, 64f));
            SetBottomCenter(start.transform as RectTransform, new Vector2(0f, 10f), new Vector2(230f, 64f));
            start.interactable = stageController.ShouldStartBoss();
            start.onClick.AddListener(StartBossScreen);
            BattleUiStyler.ApplyActionButton(start, uiConfig);
        }

        private void ShowBossInfoTab()
        {
            UpdateRightPanelTabState(true);
        }

        private void ShowChallengeTab()
        {
            UpdateRightPanelTabState(false);
        }

        private void UpdateRightPanelTabState(bool showBossInfo)
        {
            if (bossInfoContent != null)
            {
                bossInfoContent.gameObject.SetActive(showBossInfo);
            }

            if (challengeContent != null)
            {
                challengeContent.gameObject.SetActive(!showBossInfo);
            }

            SetRightPanelTabButtonState(bossInfoTabButton, showBossInfo);
            SetRightPanelTabButtonState(challengeTabButton, !showBossInfo);
        }

        private void SetRightPanelTabButtonState(Button button, bool selected)
        {
            if (selected)
            {
                SetButtonColors(
                    button,
                    new Color(0.18f, 0.17f, 0.08f, 1f),
                    new Color(0.22f, 0.20f, 0.10f, 1f),
                    new Color(0.12f, 0.10f, 0.06f, 1f),
                    new Color(0.07f, 0.08f, 0.07f, 0.65f));
                BattleUiStyler.ApplyTabButton(button, GetBattleUiConfig(), true);
                return;
            }

            SetButtonColors(
                button,
                new Color(0.07f, 0.12f, 0.095f, 0.98f),
                new Color(0.12f, 0.18f, 0.13f, 1f),
                new Color(0.10f, 0.09f, 0.05f, 1f),
                new Color(0.07f, 0.08f, 0.07f, 0.65f));
            BattleUiStyler.ApplyTabButton(button, GetBattleUiConfig(), false);
        }

        private void StartBossScreen()
        {
            if (IsAvatarAvailable())
            {
                PlayBossChallengeAnimation();
                StartDeferredAvatarAction(StartBossScreenAfterAvatarAnimation());
                return;
            }

            StartBossScreenImmediately();
        }

        private IEnumerator StartBossScreenAfterAvatarAnimation()
        {
            yield return new WaitForSeconds(0.48f);
            StartBossScreenImmediately();
        }

        private void StartBossScreenImmediately()
        {
            if(!sessionState.BattleFlow.Start(System.Guid.NewGuid().ToString("N"),sessionState.stageState.currentStage.bossChallenge,
                null,stageController,null,challengeController,BattleReturnContext.Capture(()=>ShowStageScreen("已返回阶段界面。")),
                (uint)System.Environment.TickCount,out var error))SetLogMessage("挑战",error);
        }

        private void ShowChallengeScreen(string message)
        {
            ClearContent();
            BattleUIConfig uiConfig = GetBattleUiConfig();
            ChallengeRuntimeState state = challengeController.runtimeState;
            UpdateChallengeTopStatus(state);
            if (topEndTurnButton != null)
            {
                topEndTurnButton.gameObject.SetActive(false);
            }

            SetLogMessage("行动记录", message);

            if (state.isComplete)
            {
                ShowBossResult(state.isSuccess);
                return;
            }

            RectTransform battleCanvas = CreatePanel("BattleCanvas", contentRoot, board, WithAlpha(green, 0.24f));
            Stretch(battleCanvas);
            BattleUiStyler.ApplyPanel(battleCanvas, uiConfig, GameUiPanelVariant.Dark);

            RectTransform backgroundLayer = CreateEmpty("BackgroundLayer", battleCanvas);
            Stretch(backgroundLayer);
            Image tableWash = CreateImage("BattlefieldWash", backgroundLayer, GetSolidSprite(), new Color(0.025f, 0.075f, 0.06f, 0.72f));
            Stretch(tableWash.rectTransform);
            BattleUiStyler.ApplyBattlefieldWash(tableWash, uiConfig);

            RectTransform actorLayer = CreateEmpty("ActorLayer", battleCanvas);
            SetStretch(actorLayer, Vector2.zero, Vector2.one, new Vector2(26f, uiConfig.handAreaHeight + 18f), new Vector2(-26f, -22f));

            RectTransform hudLayer = CreateEmpty("HudLayer", battleCanvas);
            Stretch(hudLayer);

            RectTransform handLayer = CreateEmpty("HandLayer", battleCanvas);
            SetBottom(handLayer, 0f, 0f, 0f, uiConfig.handAreaHeight);

            RectTransform dragLayer = CreateEmpty("DragLayer", battleCanvas);
            Stretch(dragLayer);

            battleFeedbackLayer = CreateEmpty("FeedbackLayer", battleCanvas);
            Stretch(battleFeedbackLayer);

            RectTransform tooltipLayer = CreateEmpty("TooltipLayer", battleCanvas);
            Stretch(tooltipLayer);
            battleTooltipView = BuildBattleTooltipView(tooltipLayer);

            BuildBattlefieldLayer(actorLayer, state, battleTooltipView);
            BuildBattleHudLayer(hudLayer, state, battleTooltipView);
            BuildHandLayer(handLayer, dragLayer, state, battleTooltipView);
        }

        private void BuildChallengePanel(RectTransform parent, ChallengeRuntimeState state)
        {
            CreateHeader(parent, "卡牌挑战");
            BattleUIConfig uiConfig = GetBattleUiConfig();
            UIThemeConfig theme = BattleUiStyler.ResolveTheme(uiConfig);

            if (!string.IsNullOrEmpty(state.data.imagePath))
            {
                Image bossImage = CreateImage("BossQuizCard", parent, LoadSprite(state.data.imagePath), Color.white);
                bossImage.preserveAspect = true;
                SetTopLeft(bossImage.rectTransform, new Vector2(48f, -94f), new Vector2(270f, 320f));
            }

            RectTransform infoGroup = CreateEmpty("ChallengeInfoGroup", parent);
            SetStretch(infoGroup, Vector2.zero, Vector2.one, new Vector2(360f, 58f), new Vector2(-48f, -36f));

            Text goal = CreateText("GoalText", infoGroup, "目标：达到 " + state.data.targetProgress + " 点进度", 30, FontStyle.Bold, TextAnchor.UpperLeft, text);
            SetTop(goal.rectTransform, 0f, 0f, 0f, 48f);
            BattleUiStyler.ApplyText(goal, uiConfig, GameUiTextStyle.Title, true);

            CreateRuntimeInfoStrip(
                infoGroup,
                "ChallengeCurrentProgressStrip",
                theme.cardsIcon,
                theme.gold,
                "当前进度",
                state.currentProgress + " / " + state.data.targetProgress,
                0f,
                0f,
                62f,
                48f,
                GameUiPanelVariant.Dark);

            CreateRuntimeInfoStrip(
                infoGroup,
                "ChallengeTurnProgressStrip",
                theme.timeIcon,
                theme.gold,
                "回合进度",
                state.currentTurn + " / " + state.data.maxTurns,
                0f,
                0f,
                120f,
                48f,
                GameUiPanelVariant.Dark);

            float progress01 = state.data.targetProgress <= 0 ? 1f : Mathf.Clamp01((float)state.currentProgress / state.data.targetProgress);
            Image challengeProgressFill;
            RectTransform progressBar = CreateProgressBar("ChallengeProgressBar", infoGroup, progress01, new Color(0.02f, 0.05f, 0.045f, 1f), amber, out challengeProgressFill);
            SetTop(progressBar, 0f, 0f, 188f, 42f);

            Text progressLabel = CreateText("ChallengeProgressLabel", progressBar, state.currentProgress + " / " + state.data.targetProgress, 22, FontStyle.Bold, TextAnchor.MiddleCenter, text);
            Stretch(progressLabel.rectTransform);
            BattleUiStyler.ApplyText(progressLabel, uiConfig, GameUiTextStyle.StatText, true);

            CreateRuntimeInfoStrip(
                infoGroup,
                "ChallengeHintStrip",
                theme.confirmIcon,
                theme.success,
                "当前提示",
                "选择合适卡牌推进小测进度",
                0f,
                0f,
                252f,
                48f,
                GameUiPanelVariant.Dark);

            Text chalkLine = CreateText("ChalkDecoration", parent, "粉笔记录：认真听题  ->  选择卡牌  ->  推进进度", 20, FontStyle.Italic, TextAnchor.LowerRight, muted);
            SetBottom(chalkLine.rectTransform, 36f, 42f, 24f, 38f);
            BattleUiStyler.ApplyText(chalkLine, uiConfig, GameUiTextStyle.SmallText, true);
        }

        private void BuildBattlefieldLayer(RectTransform parent, ChallengeRuntimeState state, BattleTooltipView tooltip)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();

            Text objective = CreateText(
                "BattleObjectiveText",
                parent,
                "目标进度 " + state.currentProgress + " / " + state.data.targetProgress + "    回合 " + state.currentTurn + " / " + state.data.maxTurns,
                24,
                FontStyle.Bold,
                TextAnchor.UpperCenter,
                amber);
            SetTop(objective.rectTransform, 260f, 260f, 22f, 34f);
            BattleUiStyler.ApplyText(objective, uiConfig, GameUiTextStyle.StatText, true);

            RectTransform playerRoot = CreatePanel("PlayerRoot", parent, new Color(0.055f, 0.095f, 0.082f, 0.84f), WithAlpha(green, uiConfig.weakBorderAlpha));
            SetStretch(playerRoot, new Vector2(0.04f, 0.06f), new Vector2(0.38f, 0.76f), Vector2.zero, Vector2.zero);
            BattleUiStyler.ApplyPanel(playerRoot, uiConfig, GameUiPanelVariant.Dark);
            BuildPlayerStatusView(playerRoot);

            RectTransform enemyRoot = CreatePanel("EnemyRoot", parent, new Color(0.09f, 0.055f, 0.052f, 0.86f), WithAlpha(amber, uiConfig.weakBorderAlpha));
            SetStretch(enemyRoot, new Vector2(0.60f, 0.08f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);
            BattleUiStyler.ApplyPanel(enemyRoot, uiConfig, GameUiPanelVariant.Dark);
            BuildEnemyStatusView(enemyRoot, state, tooltip);

            battleCenterHintText = CreateText("BattleCenterHintText", parent, "选择手牌推进月考", 22, FontStyle.Bold, TextAnchor.MiddleCenter, WithAlpha(muted, 0.28f));
            SetStretch(battleCenterHintText.rectTransform, new Vector2(0.40f, 0.32f), new Vector2(0.60f, 0.68f), Vector2.zero, Vector2.zero);
            BattleUiStyler.ApplyText(battleCenterHintText, uiConfig, GameUiTextStyle.HintText, true);
        }

        private void BuildPlayerStatusView(RectTransform parent)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            UIThemeConfig theme = BattleUiStyler.ResolveTheme(uiConfig);
            PlayerState player = sessionState.playerState;
            Text name = CreateText("PlayerNameText", parent, "学生", 28, FontStyle.Bold, TextAnchor.UpperLeft, text);
            SetTop(name.rectTransform, 22f, 22f, 20f, 38f);
            BattleUiStyler.ApplyText(name, uiConfig, GameUiTextStyle.Subtitle, true);

            if (GameArtAssetCatalog.CharacterPortraitPaths.TryGetValue("char_player_student", out string portraitPath))
            {
                Image portrait = CreateImage("BattlePlayerPortrait", parent, LoadSprite(portraitPath), Color.white);
                portrait.preserveAspect = true;
                SetStretch(portrait.rectTransform, new Vector2(0.04f, 0.12f), new Vector2(0.44f, 0.80f), Vector2.zero, Vector2.zero);
            }

            CreateRuntimeInfoStrip(
                parent,
                "PlayerHpStrip",
                uiConfig.GetArtwork("icon_battle_stamina"),
                theme.success,
                "耐力",
                challengeController.runtimeState.playerHp + " / " + challengeController.runtimeState.playerMaxHp,
                190f,
                22f,
                86f,
                48f,
                GameUiPanelVariant.Dark, true);
            CreateRuntimeInfoStrip(
                parent,
                "PlayerBlockStrip",
                uiConfig.GetArtwork("icon_block"),
                theme.gold,
                "格挡",
                challengeController.runtimeState.playerBlock.ToString(),
                190f,
                22f,
                144f,
                48f,
                GameUiPanelVariant.Dark, true);
            CreateRuntimeInfoStrip(
                parent,
                "PlayerPressureStrip",
                theme.stressIcon,
                theme.danger,
                "压力",
                player.pressure.ToString(),
                190f,
                22f,
                202f,
                48f,
                GameUiPanelVariant.Dark, true);

            float hp01 = challengeController.runtimeState.playerMaxHp <= 0 ? 0f : (float)challengeController.runtimeState.playerHp / challengeController.runtimeState.playerMaxHp;
            RectTransform energyTrack = CreateProgressBar("PlayerHpBar", parent, hp01, new Color(0.02f, 0.05f, 0.045f, 1f), green, out _);
            SetBottom(energyTrack, 22f, 22f, 20f, 28f);
            BattleUiStyler.ApplyProgressBar(energyTrack, energyTrack.Find("Fill") == null ? null : energyTrack.Find("Fill").GetComponent<Image>(), uiConfig, BattleUiStyler.GetSuccessColor(uiConfig));
            BattleArtStyler.Progress(energyTrack, energyTrack.Find("Fill").GetComponent<Image>(), uiConfig, "hud_stamina", hp01);
        }

        private void BuildEnemyStatusView(RectTransform parent, ChallengeRuntimeState state, BattleTooltipView tooltip)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            UIThemeConfig theme = BattleUiStyler.ResolveTheme(uiConfig);
            battleEnemyTargetRect = parent;
            ChallengeData boss = state.data;
            Text name = CreateText("EnemyNameText", parent, boss.displayName, 28, FontStyle.Bold, TextAnchor.UpperLeft, amber);
            SetTop(name.rectTransform, 22f, 22f, 20f, 38f);
            BattleUiStyler.ApplyText(name, uiConfig, GameUiTextStyle.Subtitle, true);

            if (!string.IsNullOrEmpty(boss.imagePath))
            {
                Image bossImage = CreateImage("EnemyArt", parent, LoadSprite(boss.imagePath), Color.white);
                bossImage.preserveAspect = true;
                SetStretch(bossImage.rectTransform, new Vector2(0.08f, 0.20f), new Vector2(0.52f, 0.78f), Vector2.zero, Vector2.zero);
            }

            float progress01 = boss.targetProgress <= 0 ? 1f : Mathf.Clamp01((float)state.currentProgress / boss.targetProgress);
            RectTransform progressBar = CreateProgressBar("EnemyProgressBar", parent, progress01, new Color(0.02f, 0.05f, 0.045f, 1f), amber, out Image enemyProgressFill);
            SetBottom(progressBar, 22f, 22f, 20f, 34f);
            BattleUiStyler.ApplyProgressBar(progressBar, enemyProgressFill, uiConfig, BattleUiStyler.GetGoldColor(uiConfig));
            BattleArtStyler.Progress(progressBar, enemyProgressFill, uiConfig, "hud_progress", progress01);

            Text progressLabel = CreateText("EnemyProgressLabel", progressBar, state.currentProgress + " / " + boss.targetProgress, 20, FontStyle.Bold, TextAnchor.MiddleCenter, text);
            Stretch(progressLabel.rectTransform);
            BattleUiStyler.ApplyText(progressLabel, uiConfig, GameUiTextStyle.SmallText, true);

            CreateRuntimeInfoStrip(
                parent,
                "EnemyGoalStrip",
                uiConfig.GetArtwork("icon_challenge_progress"),
                theme.gold,
                "目标进度",
                "达到 " + boss.targetProgress,
                230f,
                22f,
                94f,
                48f,
                GameUiPanelVariant.Dark, true);
            CreateRuntimeInfoStrip(
                parent,
                "EnemyMaxTurnStrip",
                uiConfig.GetArtwork("icon_turns"),
                theme.gold,
                "最大回合",
                boss.maxTurns.ToString(),
                230f,
                22f,
                152f,
                48f,
                GameUiPanelVariant.Dark, true);
            CreateRuntimeInfoStrip(
                parent,
                "EnemyCurrentTurnStrip",
                theme.stressIcon,
                theme.danger,
                "当前回合",
                state.currentTurn.ToString(),
                230f,
                22f,
                210f,
                48f,
                GameUiPanelVariant.Dark, true);

            RectTransform intent = CreatePanel("IntentView", parent, new Color(0.13f, 0.08f, 0.05f, 0.96f), warning);
            SetTopCenter(intent, new Vector2(0f, 18f), new Vector2(176f, 66f));
            BattleUiStyler.ApplyPanel(intent, uiConfig, GameUiPanelVariant.Warning);
            SetPanelRaycast(intent, true);
            string intentTitle = BuildIntentTitle(state);
            string intentBody = BuildIntentBody(state);
            Image intentIcon = CreateImage("IntentIcon", intent, theme.stressIcon, theme.danger);
            SetTopLeft(intentIcon.rectTransform, new Vector2(12f, -19f), new Vector2(28f, 28f));
            ApplyRuntimeIcon(intentIcon, theme.stressIcon, theme.danger);
            BattleArtStyler.Icon(intentIcon, uiConfig.GetArtwork("icon_intent_attack"));
            Text intentText = CreateText("IntentText", intent, intentTitle, 18, FontStyle.Bold, TextAnchor.MiddleLeft, text);
            SetStretch(intentText.rectTransform, Vector2.zero, Vector2.one, new Vector2(48f, 6f), new Vector2(-10f, -6f));
            BattleUiStyler.ApplyText(intentText, uiConfig, GameUiTextStyle.SmallText, true);
            intent.gameObject.AddComponent<BattleTooltipTarget>().Initialize(tooltip, intentTitle, intentBody);

            battleEnemyTargetHighlight = CreateImage("EnemyTargetHighlight", parent, GetSolidSprite(), new Color(0.92f, 0.74f, 0.38f, 0.0f));
            Stretch(battleEnemyTargetHighlight.rectTransform);
            battleEnemyTargetHighlight.raycastTarget = false;
            battleEnemyTargetHighlight.gameObject.SetActive(false);
        }

        private void BuildBattleHudLayer(RectTransform parent, ChallengeRuntimeState state, BattleTooltipView tooltip)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            DeckState deckState = challengeController.deckController.deckState;

            RectTransform energy = CreatePanel("EnergyView", parent, new Color(0.07f, 0.10f, 0.08f, 0.96f), amber);
            SetBottomLeft(energy, new Vector2(34f, 32f), new Vector2(132f, 76f));
            BattleUiStyler.ApplyPanel(energy, uiConfig, GameUiPanelVariant.Dark);
            SetPanelRaycast(energy, true);
            UIThemeConfig theme = BattleUiStyler.ResolveTheme(uiConfig);
            Image energyIcon = CreateImage("EnergyIcon", energy, theme.energyIcon, amber);
            SetTopLeft(energyIcon.rectTransform, new Vector2(14f, -20f), new Vector2(28f, 28f));
            ApplyRuntimeIcon(energyIcon, theme.energyIcon, amber);
            BattleArtStyler.Icon(energyIcon, uiConfig.GetArtwork("icon_action_energy"));
            Text energyLabel = CreateText("EnergyLabel", energy, "能量", 14, FontStyle.Bold, TextAnchor.MiddleLeft, amber);
            SetStretch(energyLabel.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(50f, 2f), new Vector2(-10f, -6f));
            BattleUiStyler.ApplyText(energyLabel, uiConfig, GameUiTextStyle.SmallText, true);
            Text energyText = CreateText("EnergyValue", energy, state.currentEnergy + " / " + state.data.startingEnergy, 20, FontStyle.Bold, TextAnchor.MiddleLeft, text);
            SetStretch(energyText.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(50f, 6f), new Vector2(-10f, -2f));
            BattleUiStyler.ApplyText(energyText, uiConfig, GameUiTextStyle.StatText, true);
            energy.gameObject.AddComponent<BattleTooltipTarget>().Initialize(tooltip, "能量", "当前可用于打出卡牌的能量。费用不足的卡牌会变暗。");

            Button endTurn = CreateButton(parent, string.Empty, Vector2.zero, new Vector2(210f, 76f));
            endTurn.gameObject.name = "EndTurnButton";
            SetBottomRight(endTurn.transform as RectTransform, new Vector2(-34f, uiConfig.handAreaHeight + 28f), new Vector2(210f, 76f));
            endTurn.interactable = !state.isComplete;
            endTurn.onClick.AddListener(OnEndTurnClicked);
            RectTransform endTurnRect = endTurn.transform as RectTransform;
            Image endTurnIcon = CreateImage("EndTurnIcon", endTurnRect, theme.timeIcon, amber);
            SetTopLeft(endTurnIcon.rectTransform, new Vector2(18f, -22f), new Vector2(28f, 28f));
            ApplyRuntimeIcon(endTurnIcon, theme.timeIcon, amber);
            BattleArtStyler.Icon(endTurnIcon, uiConfig.GetArtwork("icon_turns"));
            Text endTurnTitle = CreateText("EndTurnTitle", endTurnRect, "结束回合", 20, FontStyle.Bold, TextAnchor.MiddleLeft, text);
            SetStretch(endTurnTitle.rectTransform, Vector2.zero, Vector2.one, new Vector2(56f, 10f), new Vector2(-14f, -10f));
            BattleUiStyler.ApplyText(endTurnTitle, uiConfig, GameUiTextStyle.ButtonText, true);
            SetButtonColors(
                endTurn,
                new Color(0.16f, 0.10f, 0.055f, 1f),
                new Color(0.24f, 0.15f, 0.07f, 1f),
                new Color(0.11f, 0.07f, 0.04f, 1f),
                new Color(0.07f, 0.08f, 0.07f, 0.65f));
            BattleUiStyler.ApplyActionButton(endTurn, uiConfig);
            BattleArtStyler.Button(endTurn, uiConfig);

            RectTransform draw = BuildDeckCounter(parent, "DrawPileCounter", "抽牌堆", deckState.drawPile.Count, tooltip, "剩余可抽卡牌数量");
            SetBottomRight(draw, new Vector2(-184f, 32f), new Vector2(118f, 66f));
            BattleArtStyler.Icon(draw.Find("CounterIcon").GetComponent<Image>(), uiConfig.GetArtwork("icon_pile_draw"));

            RectTransform discard = BuildDeckCounter(parent, "DiscardPileCounter", "弃牌堆", deckState.discardPile.Count, tooltip, "本场战斗已弃置卡牌数量");
            SetBottomRight(discard, new Vector2(-34f, 32f), new Vector2(118f, 66f));
            BattleArtStyler.Icon(discard.Find("CounterIcon").GetComponent<Image>(), uiConfig.GetArtwork("icon_pile_discard"));
        }

        private RectTransform BuildDeckCounter(RectTransform parent, string name, string label, int count, BattleTooltipView tooltip, string tooltipBody)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            RectTransform counter = CreatePanel(name, parent, new Color(0.045f, 0.075f, 0.068f, 0.96f), green);
            BattleUiStyler.ApplyPanel(counter, uiConfig, GameUiPanelVariant.Standard);
            SetPanelRaycast(counter, true);
            UIThemeConfig theme = BattleUiStyler.ResolveTheme(uiConfig);
            Sprite counterIcon = ResolveDeckCounterIcon(theme, label);
            Image icon = CreateImage("CounterIcon", counter, counterIcon, label.Contains("抽") ? theme.gold : theme.hintText);
            SetTopLeft(icon.rectTransform, new Vector2(12f, -20f), new Vector2(24f, 24f));
            ApplyRuntimeIcon(icon, counterIcon, label.Contains("抽") ? theme.gold : theme.hintText);
            Text counterLabel = CreateText("CounterLabel", counter, label, 13, FontStyle.Bold, TextAnchor.MiddleLeft, muted);
            SetStretch(counterLabel.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(44f, 2f), new Vector2(-6f, -4f));
            BattleUiStyler.ApplyText(counterLabel, uiConfig, GameUiTextStyle.SmallText);
            Text counterValue = CreateText("CounterValue", counter, count.ToString(), 20, FontStyle.Bold, TextAnchor.MiddleLeft, text);
            SetStretch(counterValue.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(44f, 4f), new Vector2(-6f, -2f));
            BattleUiStyler.ApplyText(counterValue, uiConfig, GameUiTextStyle.StatText);
            counter.gameObject.AddComponent<BattleTooltipTarget>().Initialize(tooltip, label, tooltipBody);
            return counter;
        }

        private static Sprite ResolveDeckCounterIcon(UIThemeConfig theme, string label)
        {
            if (theme == null)
            {
                return null;
            }

            return label != null && label.Contains("弃") && theme.cancelIcon != null
                ? theme.cancelIcon
                : theme.cardsIcon;
        }

        private void BuildHandLayer(RectTransform handLayer, RectTransform dragLayer, ChallengeRuntimeState state, BattleTooltipView tooltip)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            Image tray = CreateImage("HandBackground", handLayer, GetSolidSprite(), new Color(0.04f, 0.055f, 0.045f, uiConfig.handAreaBackgroundAlpha));
            Stretch(tray.rectTransform);
            tray.raycastTarget = false;
            if (uiConfig.showHandAreaBorder) BattleUiStyler.ApplyImagePanel(tray, uiConfig, GameUiPanelVariant.Dark);
            tray.color = new Color(tray.color.r, tray.color.g, tray.color.b, uiConfig.handAreaBackgroundAlpha);

            Text label = CreateText("HandLabel", handLayer, "手牌", 22, FontStyle.Bold, TextAnchor.UpperLeft, muted);
            SetTop(label.rectTransform, 24f, 24f, 12f, 30f);
            BattleUiStyler.ApplyText(label, uiConfig, GameUiTextStyle.Subtitle, true);

            RectTransform handRoot = CreateEmpty("HandRoot", handLayer);
            SetStretch(handRoot, Vector2.zero, Vector2.one, new Vector2(224f, 6f), new Vector2(-224f, -2f));

            HandLayoutController layout = handRoot.gameObject.AddComponent<HandLayoutController>();
            layout.Initialize(handRoot, dragLayer, gameObject.GetComponent<Canvas>(), GetBattleUiConfig(), tooltip);
            layout.CardDragStarted += OnBattleCardDragStarted;
            layout.CardDragging += OnBattleCardDragging;
            layout.CardDragEnded += OnBattleCardDragEnded;
            layout.TryPlayDraggedCard = TryPlayDraggedBattleCard;

            foreach (CardData card in challengeController.deckController.deckState.hand)
            {
                CreateCardButton(handRoot, card, layout);
            }

            layout.RefreshLayout(true);
        }

        private BattleTooltipView BuildBattleTooltipView(RectTransform tooltipLayer)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            RectTransform tooltipRoot = CreatePanel("TooltipView", tooltipLayer, new Color(0.025f, 0.035f, 0.032f, 0.96f), amber);
            tooltipRoot.sizeDelta = new Vector2(GetBattleUiConfig().tooltipMaxWidth, 150f);
            tooltipRoot.anchorMin = new Vector2(0.5f, 0.5f);
            tooltipRoot.anchorMax = new Vector2(0.5f, 0.5f);
            tooltipRoot.pivot = new Vector2(0.5f, 0.5f);
            BattleUiStyler.ApplyPanel(tooltipRoot, uiConfig, GameUiPanelVariant.Dark);

            Text title = CreateText("TooltipTitle", tooltipRoot, "", 21, FontStyle.Bold, TextAnchor.UpperLeft, amber);
            SetTop(title.rectTransform, 16f, 16f, 12f, 28f);
            BattleUiStyler.ApplyText(title, uiConfig, GameUiTextStyle.Subtitle, true);

            Text body = CreateText("TooltipBody", tooltipRoot, "", 18, FontStyle.Normal, TextAnchor.UpperLeft, text);
            SetStretch(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 14f), new Vector2(-16f, -48f));
            BattleUiStyler.ApplyText(body, uiConfig, GameUiTextStyle.Body, true);

            BattleTooltipView tooltip = tooltipRoot.gameObject.AddComponent<BattleTooltipView>();
            tooltip.Initialize(tooltipRoot, title, body, gameObject.GetComponent<Canvas>(), GetBattleUiConfig());
            return tooltip;
        }

        private BattleCardView CreateCardButton(RectTransform parent, CardData card, HandLayoutController handLayout,
            BattleCardPreview instance = null, System.Action<BattleCardView> instanceClick = null)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            GameObject buttonObject = new GameObject("Card_" + card.cardId, typeof(RectTransform));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.sizeDelta = new Vector2(188f, 228f);

            Image background = buttonObject.AddComponent<Image>();
            background.sprite = GetSolidSprite();
            background.color = cardFill;

            Button button = buttonObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = cardFill;
            colors.highlightedColor = new Color(0.20f, 0.26f, 0.18f, 1f);
            colors.pressedColor = new Color(0.30f, 0.24f, 0.12f, 1f);
            colors.selectedColor = new Color(0.26f, 0.24f, 0.12f, 1f);
            colors.disabledColor = new Color(0.08f, 0.09f, 0.08f, 0.65f);
            button.colors = colors;
            CreateBorder(rect, (instance != null ? instance.Playable : challengeController.CanPlayCard(card)) ? green : warning);
            BattleUiStyler.ApplyCardButton(button, background, uiConfig);

            Sprite suppliedArt = uiConfig.GetCardArtwork(card);
            if (suppliedArt != null || !string.IsNullOrEmpty(card.imagePath))
            {
                Image imageFrame = CreateImage("CardImageFrame", rect, GetSolidSprite(), paper);
                SetTop(imageFrame.rectTransform, 12f, 12f, 12f, 88f);
                BattleUiStyler.ApplyImagePanel(imageFrame, uiConfig, GameUiPanelVariant.Standard);

                Image image = CreateImage("CardImage", rect, suppliedArt != null ? suppliedArt : LoadSprite(card.imagePath), Color.white);
                image.preserveAspect = true; image.raycastTarget = false; imageFrame.raycastTarget = false;
                SetTop(image.rectTransform, 18f, 18f, 18f, 76f);
            }

            RectTransform metaBadge = CreatePanel("CardMetaBadge", rect, new Color(0.06f, 0.085f, 0.072f, 0.98f), GetCardBadgeColor(card));
            SetTop(metaBadge, 14f, 14f, 104f, 22f);
            BattleUiStyler.ApplyPanel(metaBadge, uiConfig, GameUiPanelVariant.Dark);
            Text metaText = CreateText("CardMetaText", metaBadge, GetCardTypeLabel(card.cardType) + " · " + GetCardRarityLabel(card.rarity), 14, FontStyle.Bold, TextAnchor.MiddleCenter, text);
            Stretch(metaText.rectTransform, 2f);
            BattleUiStyler.ApplyCardText(metaText, uiConfig, GameUiTextStyle.SmallText, true);

            Text label = CreateText("CardNameText", rect, instance != null ? instance.Card.Definition.Name : card.displayName, 20, FontStyle.Bold, TextAnchor.MiddleCenter, text);
            SetTop(label.rectTransform, 12f, 12f, 130f, 28f);
            BattleUiStyler.ApplyCardText(label, uiConfig, GameUiTextStyle.Subtitle);

            Text effect = CreateText("CardEffectText", rect, instance != null ? instance.Summary : BuildCardEffectSummary(card), 15, FontStyle.Normal, TextAnchor.UpperCenter, muted);
            SetTop(effect.rectTransform, 12f, 12f, 160f, 32f);
            BattleUiStyler.ApplyCardText(effect, uiConfig, GameUiTextStyle.SmallText);

            RectTransform costBadge = CreatePanel("CostBadge", rect, new Color(0.07f, 0.09f, 0.075f, 1f), amber);
            SetBottomCenter(costBadge, new Vector2(0f, 10f), new Vector2(96f, 28f));
            BattleUiStyler.ApplyPanel(costBadge, uiConfig, GameUiPanelVariant.Dark);
            Color costColor = challengeController.runtimeState.currentEnergy >= card.cost ? amber : warning;
            Text cost = CreateText("CostText", costBadge, "费用 " + card.cost, 16, FontStyle.Bold, TextAnchor.MiddleCenter, costColor);
            Stretch(cost.rectTransform, 2f);
            BattleUiStyler.ApplyCardText(cost, uiConfig, GameUiTextStyle.SmallText, true);

            CardData captured = card;
            BattleCardView cardView = buttonObject.AddComponent<BattleCardView>();
            cardView.Initialize(card, button, background, cost, BuildCardTooltipBody(card), challengeController.CanPlayCard(card), uiConfig);
            if (instance != null)
            {
                cardView.BindInstance(instance);
                button.onClick.AddListener(() => instanceClick?.Invoke(cardView));
            }
            else button.onClick.AddListener(() => OnCardClicked(captured));
            handLayout.RegisterCard(cardView);
            return cardView;
        }

        private Color GetCardBadgeColor(CardData card)
        {
            if (card == null)
            {
                return amber;
            }

            switch (card.cardType)
            {
                case CardType.Rest:
                case CardType.Trait:
                    return green;
                case CardType.Status:
                    return warning;
                case CardType.Family:
                case CardType.Social:
                    return amber;
                default:
                    return muted;
            }
        }

        private void OnCardClicked(CardData card)
        {
            BattleCardFeedbackKind feedbackKind = GetCardFeedbackKind(card);
            if (!challengeController.TryPlayCard(card, true, out string reason))
            {
                SetLogMessage("行动记录", string.IsNullOrEmpty(reason) ? "无法打出：" + card.displayName : reason);
                return;
            }

            CompleteBattleCardPlay(card, feedbackKind);
        }

        private bool TryPlayDraggedBattleCard(BattleCardView cardView, PointerEventData eventData)
        {
            if (cardView == null || cardView.Data == null)
            {
                return false;
            }

            CardData card = cardView.Data;
            CardTargetType targetType = challengeController.GetEffectiveTargetType(card);
            bool hasEnemyTarget = targetType != CardTargetType.SingleEnemy || IsPointerOverEnemyTarget(eventData);
            if (!challengeController.TryPlayCard(card, hasEnemyTarget, out string reason))
            {
                SetEnemyTargetHighlight(false, false);
                SetLogMessage("行动记录", string.IsNullOrEmpty(reason) ? "无法打出：" + card.displayName : reason);
                return false;
            }

            SetEnemyTargetHighlight(false, false);
            CompleteBattleCardPlay(card, GetCardFeedbackKind(card));
            return true;
        }

        private void CompleteBattleCardPlay(CardData card, BattleCardFeedbackKind feedbackKind)
        {
            if (challengeController.runtimeState.isComplete)
            {
                ShowBossResult(challengeController.runtimeState.isSuccess);
                return;
            }

            ShowChallengeScreen("打出卡牌：" + card.displayName);
            PlayBattleCardFeedback(card, feedbackKind);
        }

        private void OnBattleCardDragStarted(BattleCardView cardView, PointerEventData eventData)
        {
            if (cardView == null || cardView.Data == null)
            {
                return;
            }

            CardTargetType targetType = challengeController.GetEffectiveTargetType(cardView.Data);
            bool requiresEnemy = targetType == CardTargetType.SingleEnemy;
            SetEnemyTargetHighlight(requiresEnemy, false);
            if (requiresEnemy)
            {
                SetBattleCenterHint("拖到月考目标以推进进度", true, false);
                SetLogMessage("行动记录", "拖到敌人区域释放以打出：" + cardView.CardName);
                return;
            }

            SetBattleCenterHint(targetType == CardTargetType.Self ? "释放以获得格挡" : "释放以打出卡牌", true, true);
        }

        private void OnBattleCardDragging(BattleCardView cardView, PointerEventData eventData)
        {
            if (cardView == null || cardView.Data == null)
            {
                return;
            }

            bool requiresEnemy = challengeController.GetEffectiveTargetType(cardView.Data) == CardTargetType.SingleEnemy;
            SetEnemyTargetHighlight(requiresEnemy, requiresEnemy && IsPointerOverEnemyTarget(eventData));
            if (requiresEnemy)
            {
                SetBattleCenterHint(IsPointerOverEnemyTarget(eventData) ? "释放卡牌，推进月考进度" : "拖到月考目标以推进进度", true, IsPointerOverEnemyTarget(eventData));
            }
        }

        private void OnBattleCardDragEnded(BattleCardView cardView, PointerEventData eventData)
        {
            SetEnemyTargetHighlight(false, false);
            SetBattleCenterHint("选择手牌推进月考", false, false);
        }

        private bool IsPointerOverEnemyTarget(PointerEventData eventData)
        {
            if (eventData == null || battleEnemyTargetRect == null)
            {
                return false;
            }

            Canvas canvas = gameObject.GetComponent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(battleEnemyTargetRect, eventData.position, eventCamera);
        }

        private void SetEnemyTargetHighlight(bool visible, bool valid)
        {
            if (battleEnemyTargetHighlight == null)
            {
                return;
            }

            battleEnemyTargetHighlight.gameObject.SetActive(visible);
            battleEnemyTargetHighlight.color = valid
                ? new Color(0.43f, 0.65f, 0.52f, 0.22f)
                : new Color(0.92f, 0.74f, 0.38f, 0.14f);
        }

        private void SetBattleCenterHint(string value, bool active, bool positive)
        {
            if (battleCenterHintText == null)
            {
                return;
            }

            battleCenterHintText.text = value;
            Color target = positive ? green : muted;
            battleCenterHintText.color = active ? WithAlpha(target, 0.86f) : WithAlpha(muted, 0.28f);
        }

        private void OnEndTurnClicked()
        {
            challengeController.EndTurn();
            if (challengeController.runtimeState.isComplete)
            {
                ShowBossResult(challengeController.runtimeState.isSuccess);
                return;
            }

            ShowChallengeScreen("进入下一回合。");
            PlayBattleFeedback("回合 " + challengeController.runtimeState.currentTurn, new Vector2(0.50f, 0.58f), muted);
        }

        private BattleCardFeedbackKind GetCardFeedbackKind(CardData card)
        {
            if (card == null || card.effects == null)
            {
                return BattleCardFeedbackKind.Utility;
            }

            for (int i = 0; i < card.effects.Count; i++)
            {
                CardEffectData effect = card.effects[i];
                if (effect == null)
                {
                    continue;
                }

                if (effect.effectType == CardEffectType.AddChallengeProgress || effect.effectType == CardEffectType.AddSubjectProgress)
                {
                    return BattleCardFeedbackKind.Attack;
                }

                if (effect.effectType == CardEffectType.GainEnergy
                    || effect.effectType == CardEffectType.ReducePressure
                    || effect.effectType == CardEffectType.ReducePlayerStat)
                {
                    return BattleCardFeedbackKind.Defense;
                }
            }

            return BattleCardFeedbackKind.Utility;
        }

        private void PlayBattleCardFeedback(CardData card, BattleCardFeedbackKind feedbackKind)
        {
            string value;
            Vector2 anchor;
            Color color;
            switch (feedbackKind)
            {
                case BattleCardFeedbackKind.Attack:
                    value = "+" + Mathf.Max(1, GetChallengeProgressValue(card)) + " 进度";
                    anchor = new Vector2(0.76f, 0.58f);
                    color = amber;
                    break;
                case BattleCardFeedbackKind.Defense:
                    value = "状态提升";
                    anchor = new Vector2(0.24f, 0.50f);
                    color = green;
                    break;
                default:
                    value = "效果生效";
                    anchor = new Vector2(0.50f, 0.50f);
                    color = text;
                    break;
            }

            PlayBattleFeedback(value, anchor, color);
        }

        private int GetChallengeProgressValue(CardData card)
        {
            int value = 0;
            if (card == null || card.effects == null)
            {
                return value;
            }

            for (int i = 0; i < card.effects.Count; i++)
            {
                CardEffectData effect = card.effects[i];
                if (effect != null && effect.effectType == CardEffectType.AddChallengeProgress)
                {
                    value += effect.value;
                }
            }

            return value;
        }

        private void PlayBattleFeedback(string value, Vector2 anchor, Color color)
        {
            if (battleFeedbackLayer == null)
            {
                return;
            }

            if (battleFeedbackCoroutine != null)
            {
                StopCoroutine(battleFeedbackCoroutine);
                battleFeedbackCoroutine = null;
            }

            Text feedback = CreateText("BattleFeedbackText", battleFeedbackLayer, value, 32, FontStyle.Bold, TextAnchor.MiddleCenter, color);
            RectTransform rect = feedback.rectTransform;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(220f, 52f);
            battleFeedbackCoroutine = StartCoroutine(AnimateBattleFeedback(feedback));
        }

        private IEnumerator AnimateBattleFeedback(Text feedback)
        {
            if (feedback == null)
            {
                yield break;
            }

            RectTransform rect = feedback.rectTransform;
            Vector2 start = rect.anchoredPosition;
            Color startColor = feedback.color;
            float duration = GetBattleUiConfig().hudAnimationDuration + 0.45f;
            float elapsed = 0f;
            while (elapsed < duration && feedback != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                rect.anchoredPosition = start + new Vector2(0f, Mathf.Lerp(0f, 42f, t));
                Color color = startColor;
                color.a = 1f - t;
                feedback.color = color;
                yield return null;
            }

            if (feedback != null)
            {
                Destroy(feedback.gameObject);
            }

            battleFeedbackCoroutine = null;
        }

        private void ShowBossResult(bool success)
        {
            var record = challengeController.Result;
            ClearContent();
            titleText.text = record != null && record.Consumption == BattleResultConsumption.Applied && record.Success ? "挑战完成" : "挑战结果";
            statusText.text = BuildStatusText();
            SetTopChallengeControlsVisible(false);
            SetLogMessage("行动记录", record != null && record.Consumption == BattleResultConsumption.Applied ? "本场结果已结算。" : "结算未完成，未自动重试。");
            BattleResultView.Show(contentRoot, GetBattleUiConfig(), runtimeFont, record, sessionState, () =>
            {
                if (sessionState.endingReached) { ShowEndingScreen(); return; }
                stageController.MoveToNextStage();
                ShowStageScreen("进入新阶段。");
            });
        }
        private static string BuildResultIntroText(bool success)
        {
            return success
                ? "完成阶段 Boss，成长路线继续推进。"
                : "未能完成 Boss 目标，游戏继续但需要调整节奏。";
        }

        private string BuildRewardSummary(IReadOnlyList<RewardData> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < rewards.Count && parts.Count < 4; i++)
            {
                string summary = BuildRewardSummary(rewards[i]);
                if (!string.IsNullOrEmpty(summary))
                {
                    parts.Add(summary);
                }
            }

            return parts.Count == 0 ? string.Empty : "\n获得：" + string.Join("，", parts.ToArray());
        }

        private string BuildRewardSummary(RewardData reward)
        {
            if (reward == null)
            {
                return string.Empty;
            }

            switch (reward.rewardType)
            {
                case RewardType.AddCard:
                    return reward.card != null ? "卡牌 " + reward.card.displayName : "新卡牌";
                case RewardType.AddItem:
                    return reward.item != null ? "道具 " + reward.item.displayName : "新道具";
                case RewardType.AddPlayerStat:
                    return GetStatLabel(reward.targetStat) + " +" + reward.value;
                case RewardType.ReducePlayerStat:
                    return GetStatLabel(reward.targetStat) + " -" + reward.value;
                case RewardType.ReducePressure:
                    return "压力 -" + reward.value;
                case RewardType.AddMoney:
                    return "金钱 +" + reward.value;
                case RewardType.IncreaseMaxEnergy:
                    return "最大精力 +" + reward.value;
                case RewardType.AddStageTime:
                    return "阶段时间 +" + reward.value;
                default:
                    return reward.description;
            }
        }

        private void ShowEndingScreen()
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            UIThemeConfig theme = BattleUiStyler.ResolveTheme(uiConfig);
            ClearContent();
            if (endingController == null)
            {
                endingController = gameObject.AddComponent<EndingController>();
            }

            endingController.SetSessionState(sessionState);
            endingController.SetEndingOptions(gameBootstrapper != null ? gameBootstrapper.GetEndingOptions() : MvpSampleDataFactory.CreateDefaultEndings());
            EndingData endingData = endingController.SelectEnding();
            string endingTitle = endingData != null && !string.IsNullOrEmpty(endingData.displayName)
                ? endingData.displayName
                : "普通但完整的成长";

            titleText.text = "结局：" + endingTitle;
            statusText.text = BuildStatusText();
            SetTopChallengeControlsVisible(false);
            SetLogMessage("课堂提示", "三阶段流程已结束。");

            RectTransform ending = CreatePanel("EndingView", contentRoot, panel, amber);
            Stretch(ending);
            BattleUiStyler.ApplyPanel(ending, uiConfig, GameUiPanelVariant.Standard);
            CreateHeader(ending, endingTitle);

            RectTransform illustrationPanel = CreatePanel("EndingIllustrationPanel", ending, new Color(0.055f, 0.082f, 0.074f, 0.98f), amber);
            SetStretch(illustrationPanel, Vector2.zero, new Vector2(0.42f, 1f), new Vector2(72f, 96f), new Vector2(-20f, -152f));
            BattleUiStyler.ApplyPanel(illustrationPanel, uiConfig, GameUiPanelVariant.Dark);
            Sprite endingSprite = ResolveEndingSprite(endingData);
            if (endingSprite != null)
            {
                Image illustration = CreateImage("EndingIllustration", illustrationPanel, endingSprite, Color.white);
                illustration.preserveAspect = true;
                Stretch(illustration.rectTransform, 18f);
            }
            else
            {
                Image fallbackIcon = CreateImage("EndingFallbackIcon", illustrationPanel, theme.confirmIcon, theme.gold);
                SetTopCenter(fallbackIcon.rectTransform, new Vector2(0f, -92f), new Vector2(52f, 52f));
                ApplyRuntimeIcon(fallbackIcon, theme.confirmIcon, theme.gold);
                Text fallbackText = CreateText("EndingFallbackText", illustrationPanel, "成长档案", 24, FontStyle.Bold, TextAnchor.MiddleCenter, text);
                SetTop(fallbackText.rectTransform, 32f, 32f, 164f, 44f);
                BattleUiStyler.ApplyText(fallbackText, uiConfig, GameUiTextStyle.Subtitle, true);
            }

            RectTransform summaryPanel = CreatePanel("EndingSummaryPanel", ending, new Color(0.045f, 0.070f, 0.064f, 0.98f), green);
            SetStretch(summaryPanel, new Vector2(0.44f, 0f), Vector2.one, new Vector2(18f, 96f), new Vector2(-72f, -152f));
            BattleUiStyler.ApplyPanel(summaryPanel, uiConfig, GameUiPanelVariant.Dark);
            Text description = CreateText("EndingDescription", summaryPanel, ResolveEndingDescription(endingData), 18, FontStyle.Bold, TextAnchor.UpperLeft, text);
            SetTop(description.rectTransform, 18f, 18f, 18f, 68f);
            BattleUiStyler.ApplyText(description, uiConfig, GameUiTextStyle.Body, true);

            CreateRuntimeInfoStrip(
                summaryPanel,
                "EndingStageStrip",
                theme.timeIcon,
                theme.gold,
                "阶段记录",
                "完成 " + sessionState.stageState.clearedStageCount + " / Boss " + sessionState.stageState.defeatedBossCount,
                18f,
                18f,
                104f,
                52f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                summaryPanel,
                "EndingSubjectStrip",
                theme.cardsIcon,
                theme.success,
                "三科表现",
                sessionState.playerState.chinese + " / " + sessionState.playerState.math + " / " + sessionState.playerState.english + "，平均 " + EndingController.GetSubjectAverage(sessionState.playerState),
                18f,
                18f,
                166f,
                52f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                summaryPanel,
                "EndingMindStrip",
                theme.stressIcon,
                theme.danger,
                "身心状态",
                "压力 " + sessionState.playerState.pressure + " / 自信 " + sessionState.playerState.confidence,
                18f,
                18f,
                228f,
                52f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                summaryPanel,
                "EndingRelationStrip",
                theme.relationIcon,
                theme.gold,
                "关系与兴趣",
                "亲子 " + sessionState.playerState.parentRelation + " / 兴趣 " + sessionState.playerState.interest,
                18f,
                18f,
                290f,
                52f,
                GameUiPanelVariant.Dark);
            CreateRuntimeInfoStrip(
                summaryPanel,
                "EndingAutonomyStrip",
                theme.confirmIcon,
                theme.success,
                "自主选择",
                "自主 " + sessionState.playerState.autonomy,
                18f,
                18f,
                352f,
                52f,
                GameUiPanelVariant.Dark);

            Button restart = CreateButton(ending, "重新开始", new Vector2(0f, 90f), new Vector2(240f, 70f));
            SetBottomCenter(restart.transform as RectTransform, new Vector2(0f, 48f), new Vector2(240f, 70f));
            restart.onClick.AddListener(() =>
            {
                BuildSession();
                ShowStageScreen("新游戏开始。");
            });
            BattleUiStyler.ApplyActionButton(restart, uiConfig);
        }

        private Sprite ResolveEndingSprite(EndingData endingData)
        {
            if (endingData == null)
            {
                return null;
            }

            return endingData.illustrationSprite != null
                ? endingData.illustrationSprite
                : LoadSprite(endingData.imagePath);
        }

        private static string ResolveEndingDescription(EndingData endingData)
        {
            return endingData != null && !string.IsNullOrEmpty(endingData.description)
                ? endingData.description
                : "这是一段不完美但完整的成长。";
        }

        private string BuildStatusText()
        {
            StageState stage = sessionState.stageState;
            PlayerState player = sessionState.playerState;
            return "剩余时间 " + stage.remainingTime
                + "  精力 " + player.energy + "/" + player.maxEnergy
                + "  压力 " + player.pressure
                + "  金钱 " + player.money;
        }

        private void UpdateChallengeTopStatus(ChallengeRuntimeState state)
        {
            titleText.text = state.data.displayName;
            SetTopStatusActionSpace(false);
            statusText.text = "回合 " + state.currentTurn + " / " + state.data.maxTurns + "    行动能量 " + state.currentEnergy;
            topProgressText.text = "进度 " + state.currentProgress + " / " + state.data.targetProgress;
            float progress01 = state.data.targetProgress <= 0 ? 1f : Mathf.Clamp01((float)state.currentProgress / state.data.targetProgress);
            SetProgressFill(topProgressFill, progress01);
            topEndTurnButton.interactable = !state.isComplete;
            SetTopChallengeControlsVisible(true);
        }

        private void UpdateBossIntroTopStatus(ChallengeData boss)
        {
            titleText.text = "Boss 挑战：" + boss.displayName;
            SetTopChallengeControlsVisible(false);
            SetTopStatusActionSpace(false);
            statusText.text = BuildStatusText();

            if (phaseStateButton != null)
            {
                phaseStateButton.gameObject.SetActive(true);
                phaseStateButton.interactable = false;
            }
        }

        private void SetTopChallengeControlsVisible(bool visible)
        {
            if (!visible)
            {
                SetTopStatusActionSpace(false);
            }

            if (topProgressGroup != null)
            {
                topProgressGroup.gameObject.SetActive(visible);
            }

            if (topEndTurnButton != null)
            {
                topEndTurnButton.gameObject.SetActive(visible);
            }

            if (phaseStateButton != null)
            {
                phaseStateButton.gameObject.SetActive(false);
            }
        }

        private void SetTopStatusActionSpace(bool reserveActionButton)
        {
            float rightOffset = reserveActionButton ? -230f : -28f;
            SetStretch(statusText.rectTransform, new Vector2(0.62f, 0f), Vector2.one, new Vector2(12f, 8f), new Vector2(rightOffset, -8f));
        }

        private void SetTopStatusMapSpace()
        {
            SetStretch(statusText.rectTransform, new Vector2(0.32f, 0f), Vector2.one, new Vector2(12f, 8f), new Vector2(-28f, -8f));
        }

        private void SetLogMessage(string title, string message)
        {
            logTitleText.text = title;
            logText.text = message;
        }

        private BattleUIConfig GetBattleUiConfig()
        {
            if (battleUiConfig == null)
            {
                battleUiConfig = Resources.Load<BattleUIConfig>(BattleUIConfig.ResourcePath);
                if (battleUiConfig == null) battleUiConfig = BattleUIConfig.CreateRuntimeDefault();
            }

            return battleUiConfig;
        }

        private string BuildCardTooltipBody(CardData card)
        {
            if (card == null)
            {
                return "暂无描述";
            }

            List<string> parts = new List<string>();
            if (!string.IsNullOrEmpty(card.description))
            {
                parts.Add(card.description.Trim());
            }

            parts.Add("费用：" + card.cost);
            parts.Add("类型：" + GetCardTypeLabel(card.cardType));
            parts.Add("稀有度：" + GetCardRarityLabel(card.rarity));
            parts.Add("目标：" + GetCardTargetLabel(challengeController.GetEffectiveTargetType(card)));
            string statLine = BuildCardStatLine(card);
            if (!string.IsNullOrEmpty(statLine))
            {
                parts.Add(statLine);
            }

            parts.Add("效果：" + BuildCardEffectSummary(card));
            return string.Join("  |  ", parts.ToArray());
        }

        private string BuildCardStatLine(CardData card)
        {
            if (card == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            int damage = card.damage > 0 ? card.damage : GetChallengeProgressValue(card);
            if (damage > 0)
            {
                parts.Add("伤害 " + damage);
            }

            if (card.block > 0)
            {
                parts.Add("格挡 " + card.block);
            }

            if (card.drawCount > 0)
            {
                parts.Add("抽牌 " + card.drawCount);
            }

            return parts.Count == 0 ? string.Empty : "数值：" + string.Join("，", parts.ToArray());
        }

        private static string GetCardTargetLabel(CardTargetType targetType)
        {
            switch (targetType)
            {
                case CardTargetType.Self:
                    return "自己";
                case CardTargetType.SingleEnemy:
                    return "单个敌人";
                case CardTargetType.AllEnemies:
                    return "所有敌人";
                case CardTargetType.RandomEnemy:
                    return "随机敌人";
                default:
                    return "无";
            }
        }

        private static string GetCardTypeLabel(CardType type)
        {
            switch (type)
            {
                case CardType.Rest:
                    return "休息";
                case CardType.Social:
                    return "社交";
                case CardType.Family:
                    return "家庭";
                case CardType.Interest:
                    return "兴趣";
                case CardType.Trait:
                    return "特质";
                case CardType.Status:
                    return "状态";
                default:
                    return "学习";
            }
        }

        private static string GetCardRarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Uncommon:
                    return "进阶";
                case CardRarity.Rare:
                    return "稀有";
                default:
                    return "普通";
            }
        }

        private string BuildIntentTitle(ChallengeRuntimeState state)
        {
            var intent = challengeController.Intent;
            return intent == null ? "准备中" : (intent.Title == "攻击" ? "攻击 " : intent.Title + " · 攻击 ") + intent.Damage;
        }

        private string BuildIntentBody(ChallengeRuntimeState state)
        {
            var intent = challengeController.Intent;
            if (intent == null) return "敌方行动尚未准备。";
            var parts = new List<string>();
            foreach (var effect in intent.Effects) parts.Add(BuildEffectSummary(effect));
            return "本回合结束后攻击 " + intent.Damage + "。" + (parts.Count == 0 ? "" : "附加效果：" + string.Join("，", parts));
        }

        private string BuildIntentEffectSummary(ChallengeIntentData intent)
        {
            if (intent == null || intent.effects == null || intent.effects.Count == 0)
            {
                return "无";
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < intent.effects.Count && parts.Count < 3; i++)
            {
                string value = BuildEffectSummary(intent.effects[i]);
                if (!string.IsNullOrEmpty(value))
                {
                    parts.Add(value);
                }
            }

            return parts.Count == 0 ? "无" : string.Join("，", parts.ToArray());
        }

        private string BuildCardEffectSummary(CardData card)
        {
            if (card == null || card.effects == null || card.effects.Count == 0)
            {
                return "效果待定";
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < card.effects.Count && lines.Count < 2; i++)
            {
                string value = BuildEffectSummary(card.effects[i]);
                if (!string.IsNullOrEmpty(value))
                {
                    lines.Add(value);
                }
            }

            return lines.Count == 0 ? "效果待定" : string.Join("，", lines.ToArray());
        }

        private string BuildEffectSummary(CardEffectData effect)
        {
            switch (effect.effectType)
            {
                case CardEffectType.AddChallengeProgress:
                    return "进度 +" + effect.value;
                case CardEffectType.GainEnergy:
                    return "精力 +" + effect.value;
                case CardEffectType.LoseEnergy:
                    return "精力 -" + effect.value;
                case CardEffectType.ReducePressure:
                    return "压力 -" + effect.value;
                case CardEffectType.AddPressure:
                    return "压力 +" + effect.value;
                case CardEffectType.DrawCards:
                    return "抽牌 +" + effect.value;
                case CardEffectType.AddPlayerStat:
                    return GetStatLabel(effect.targetStat) + " +" + effect.value;
                case CardEffectType.ReducePlayerStat:
                    return GetStatLabel(effect.targetStat) + " -" + effect.value;
                default:
                    return string.Empty;
            }
        }

        private string GetStatLabel(PlayerStatKey statKey)
        {
            switch (statKey)
            {
                case PlayerStatKey.Chinese:
                    return "语文";
                case PlayerStatKey.Math:
                    return "数学";
                case PlayerStatKey.English:
                    return "英语";
                case PlayerStatKey.Energy:
                case PlayerStatKey.MaxEnergy:
                    return "精力";
                case PlayerStatKey.Pressure:
                    return "压力";
                case PlayerStatKey.Mood:
                    return "心情";
                case PlayerStatKey.ParentRelation:
                    return "亲子";
                case PlayerStatKey.Social:
                    return "社交";
                case PlayerStatKey.Interest:
                    return "兴趣";
                case PlayerStatKey.Confidence:
                    return "自信";
                case PlayerStatKey.Autonomy:
                    return "自主";
                case PlayerStatKey.Money:
                    return "金钱";
                default:
                    return "属性";
            }
        }

        private void ClearContent()
        {
            ResetSceneMapRuntimeState();
            StopAvatarAnimation();
            if (battleFeedbackCoroutine != null)
            {
                StopCoroutine(battleFeedbackCoroutine);
                battleFeedbackCoroutine = null;
            }

            battleFeedbackLayer = null;
            battleEnemyTargetRect = null;
            battleEnemyTargetHighlight = null;
            battleTooltipView = null;
            battleCenterHintText = null;
            avatarRect = null;
            avatarImage = null;
            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Destroy(contentRoot.GetChild(i).gameObject);
            }
        }

        private void ResetSceneMapRuntimeState()
        {
            sceneMapActive = false;
            sceneMapConfirmOpen = false;
            sceneMapDebugVisible = false;
            sceneMapPlayArea = null;
            sceneMapPlayer = null;
            sceneMapPlayerImage = null;
            sceneMapFacingIndicator = null;
            sceneMapConfirmDialog = null;
            sceneMapPromptText = null;
            sceneMapFeedbackText = null;
            sceneMapStatusText = null;
            sceneMapFacingDirection = Vector2.up;
            sceneMapPlayerAnimationTimer = 0f;
            sceneMapPlayerAnimationFrame = 0;
            focusedSceneMapInteraction = null;
            sceneMapInteractions.Clear();
            sceneMapBlockers.Clear();
            sceneMapDebugObjects.Clear();
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null)
            {
                return;
            }

            GameObject eventSystemObject = new GameObject("EventSystem");
            eventSystemObject.AddComponent<EventSystem>();
            if (!TryAddInputSystemUiModule(eventSystemObject))
            {
                eventSystemObject.AddComponent<StandaloneInputModule>();
            }
        }

        private static bool TryAddInputSystemUiModule(GameObject eventSystemObject)
        {
            Type moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType == null)
            {
                return false;
            }

            eventSystemObject.AddComponent(moduleType);
            return true;
        }

        private void CreateHeader(RectTransform parent, string value)
        {
            Text header = CreateText("Header", parent, value, 30, FontStyle.Bold, TextAnchor.UpperLeft, text);
            SetTop(header.rectTransform, 24f, 24f, 22f, 48f);
        }

        private Button CreateButton(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonObject = new GameObject("Button", typeof(RectTransform));
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = size;

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = GetSolidSprite();
            image.color = new Color(0.09f, 0.13f, 0.11f, 0.98f);
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            CreateBorder(rect, amber);

            Text labelText = null;
            if (!string.IsNullOrEmpty(label))
            {
                labelText = CreateText("Label", rect, label, 20, FontStyle.Bold, TextAnchor.MiddleCenter, text);
                Stretch(labelText.rectTransform, 10f);
            }

            ApplyLegacyButtonStyle(button, image, labelText);
            return button;
        }

        private Button CreateLegacyEntryButton(
            RectTransform parent,
            string objectName,
            Sprite iconSprite,
            Color iconColor,
            string title,
            string detail,
            string badge,
            Color badgeColor,
            Vector2 anchoredPosition,
            Vector2 size)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            Button button = CreateButton(parent, string.Empty, anchoredPosition, size);
            button.gameObject.name = objectName;
            RectTransform rect = button.transform as RectTransform;

            Image icon = CreateImage("EntryIcon", rect, iconSprite, iconColor);
            SetTopLeft(icon.rectTransform, new Vector2(18f, -20f), new Vector2(34f, 34f));
            ApplyRuntimeIcon(icon, iconSprite, iconColor);

            float badgeWidth = Mathf.Clamp(size.x * 0.24f, 96f, 132f);
            RectTransform badgeRect = CreatePanel("EntryBadge", rect, new Color(0.045f, 0.070f, 0.064f, 0.96f), badgeColor);
            SetStretch(
                badgeRect,
                Vector2.one,
                Vector2.one,
                new Vector2(-badgeWidth - 12f, -38f),
                new Vector2(-12f, -10f));
            BattleUiStyler.ApplyPanel(badgeRect, uiConfig, GameUiPanelVariant.Dark);

            Text badgeText = CreateText("BadgeText", badgeRect, badge, 14, FontStyle.Bold, TextAnchor.MiddleCenter, text);
            Stretch(badgeText.rectTransform, 2f);
            BattleUiStyler.ApplyText(badgeText, uiConfig, GameUiTextStyle.SmallText, true);

            Text titleText = CreateText("EntryTitle", rect, title, 18, FontStyle.Bold, TextAnchor.MiddleLeft, text);
            SetTop(titleText.rectTransform, 66f, badgeWidth + 22f, 12f, 26f);
            BattleUiStyler.ApplyText(titleText, uiConfig, GameUiTextStyle.Subtitle, true);

            Text detailText = CreateText("EntryDetail", rect, detail, 14, FontStyle.Normal, TextAnchor.UpperLeft, muted);
            SetTop(detailText.rectTransform, 66f, badgeWidth + 22f, 42f, Mathf.Max(24f, size.y - 54f));
            BattleUiStyler.ApplyText(detailText, uiConfig, GameUiTextStyle.SmallText, true);

            return button;
        }

        private void SetButtonColors(Button button, Color normal, Color highlighted, Color pressed, Color disabled)
        {
            if (button == null)
            {
                return;
            }

            ColorBlock colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = highlighted;
            colors.pressedColor = pressed;
            colors.selectedColor = highlighted;
            colors.disabledColor = disabled;
            button.colors = colors;

            if (button.targetGraphic != null)
            {
                button.targetGraphic.color = button.interactable ? normal : disabled;
            }
        }

        private RectTransform CreateProgressBar(string name, RectTransform parent, float value, Color trackColor, Color fillColor, out Image fill)
        {
            RectTransform track = CreateEmpty(name, parent);
            Image trackImage = track.gameObject.AddComponent<Image>();
            trackImage.sprite = GetSolidSprite();
            trackImage.color = trackColor;
            trackImage.raycastTarget = false;
            CreateBorder(track, WithAlpha(green, GetBattleUiConfig().weakBorderAlpha));
            ApplyLegacyPanelStyle(track, trackImage, ResolveLegacyPanelVariant(trackColor));

            Image fillImage = CreateImage("Fill", track, GetSolidSprite(), fillColor);
            fill = fillImage;
            fill.transform.SetAsFirstSibling();
            SetStretch(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            SetProgressFill(fill, value);
            return track;
        }

        private void SetProgressFill(Image fill, float value)
        {
            if (fill == null)
            {
                return;
            }

            RectTransform fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
        }

        private RectTransform CreatePanel(string name, RectTransform parent, Color fill, Color border)
        {
            RectTransform rect = CreateEmpty(name, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = GetSolidSprite();
            image.color = fill;
            image.raycastTarget = false;
            CreateBorder(rect, border);
            ApplyLegacyPanelStyle(rect, image, ResolveLegacyPanelVariant(fill));
            return rect;
        }

        private static void SetPanelRaycast(RectTransform rect, bool enabled)
        {
            Image image = rect == null ? null : rect.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = enabled;
            }
        }

        private void CreateBorder(RectTransform rect, Color color)
        {
            Image top = CreateImage("TopBorder", rect, GetSolidSprite(), color);
            SetStretch(top.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -3f), Vector2.zero);
            Image bottom = CreateImage("BottomBorder", rect, GetSolidSprite(), color);
            SetStretch(bottom.rectTransform, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 3f));
            Image left = CreateImage("LeftBorder", rect, GetSolidSprite(), color);
            SetStretch(left.rectTransform, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
            Image right = CreateImage("RightBorder", rect, GetSolidSprite(), color);
            SetStretch(right.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-3f, 0f), Vector2.zero);
        }

        private RectTransform CreateEmpty(string name, RectTransform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private Image CreateImage(string name, RectTransform parent, Sprite sprite, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Image image = child.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateRuntimeInfoStrip(
            RectTransform parent,
            string name,
            Sprite iconSprite,
            Color iconColor,
            string title,
            string value,
            float left,
            float right,
            float top,
            float height,
            GameUiPanelVariant variant,
            bool compact = false)
        {
            BattleUIConfig uiConfig = GetBattleUiConfig();
            RectTransform strip = CreatePanel(name, parent, new Color(0.040f, 0.065f, 0.058f, 0.98f), iconColor);
            SetTop(strip, left, right, top, height);
            BattleUiStyler.ApplyPanel(strip, uiConfig, variant);

            Image icon = CreateImage("Icon", strip, iconSprite, iconColor);
            SetTopLeft(icon.rectTransform, new Vector2(14f, -14f), new Vector2(28f, 28f));
            ApplyRuntimeIcon(icon, iconSprite, iconColor);

            float textLeft = iconSprite == null ? 18f : 52f;
            Text titleText = CreateText("Title", strip, title, 15, FontStyle.Bold, TextAnchor.MiddleLeft, iconColor);
            SetStretch(titleText.rectTransform, new Vector2(0f, 0.5f), Vector2.one, new Vector2(textLeft, 2f), new Vector2(-16f, -4f));
            BattleUiStyler.ApplyText(titleText, uiConfig, GameUiTextStyle.SmallText, true);

            Text valueText = CreateText("Value", strip, value, 17, FontStyle.Bold, TextAnchor.MiddleLeft, text);
            SetStretch(valueText.rectTransform, Vector2.zero, new Vector2(1f, 0.5f), new Vector2(textLeft, 4f), new Vector2(-16f, -2f));
            BattleUiStyler.ApplyText(valueText, uiConfig, GameUiTextStyle.Body, true);
            if (compact)
            {
                SetStretch(titleText.rectTransform, Vector2.zero, new Vector2(.52f, 1f), new Vector2(textLeft, 4f), new Vector2(0f, -4f));
                SetStretch(valueText.rectTransform, new Vector2(.52f, 0f), Vector2.one, new Vector2(4f, 4f), new Vector2(-14f, -4f));
                titleText.fontSize = 16;
                valueText.fontSize = 18;
                valueText.alignment = TextAnchor.MiddleRight;
                if (iconSprite != null) icon.color = Color.white;
            }
            return valueText;
        }

        private static void ApplyRuntimeIcon(Image image, Sprite sprite, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = sprite == null ? Color.clear : color;
            image.enabled = sprite != null;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private Text CreateText(string name, RectTransform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor anchor, Color color)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Text textComponent = child.AddComponent<Text>();
            textComponent.font = GetRuntimeFont();
            textComponent.text = value;
            textComponent.fontSize = fontSize;
            textComponent.fontStyle = fontStyle;
            textComponent.alignment = anchor;
            textComponent.color = color;
            textComponent.horizontalOverflow = HorizontalWrapMode.Wrap;
            textComponent.verticalOverflow = VerticalWrapMode.Truncate;
            textComponent.raycastTarget = false;
            ApplyLegacyTextStyle(textComponent, fontSize, fontStyle, color);
            return textComponent;
        }

        private Font GetRuntimeFont()
        {
            if (runtimeFont == null)
            {
                runtimeFont = GetRuntimeTheme().ResolveFont();
            }

            return runtimeFont;
        }

        private UIThemeConfig GetRuntimeTheme()
        {
            if (runtimeTheme == null)
            {
                BattleUIConfig config = GetBattleUiConfig();
                runtimeTheme = GameUiStyle.ResolveTheme(config == null ? null : config.theme);
            }

            return runtimeTheme;
        }

        private void ApplyLegacyPanelStyle(RectTransform rect, Image image, GameUiPanelVariant variant)
        {
            if (rect == null || image == null)
            {
                return;
            }

            UIThemeConfig theme = GetRuntimeTheme();
            PixelPanel pixelPanel = rect.GetComponent<PixelPanel>();
            if (pixelPanel == null)
            {
                pixelPanel = rect.gameObject.AddComponent<PixelPanel>();
            }

            pixelPanel.theme = theme;
            pixelPanel.background = image;
            pixelPanel.variant = variant;
            pixelPanel.ApplyStyle();
            RecolorLegacyBorder(rect, ResolveLegacyBorderColor(theme, variant));
        }

        private void ApplyLegacyButtonStyle(Button button, Image image, Text labelText)
        {
            if (button == null)
            {
                return;
            }

            UIThemeConfig theme = GetRuntimeTheme();
            GameUiStyle.ApplyButton(button, image, theme);
            GameUiStyle.ApplyPixelText(labelText, theme, GameUiTextStyle.ButtonText, true);
            RecolorLegacyBorder(button.transform as RectTransform, theme.gold);

            PixelButton pixelButton = button.GetComponent<PixelButton>();
            if (pixelButton == null)
            {
                pixelButton = button.gameObject.AddComponent<PixelButton>();
            }

            pixelButton.theme = theme;
            pixelButton.background = image;
            pixelButton.labelText = labelText;
            pixelButton.animateScale = true;
            pixelButton.ApplyStyle();
        }

        private void ApplyLegacyTextStyle(Text target, int fontSize, FontStyle fontStyle, Color legacyColor)
        {
            if (target == null)
            {
                return;
            }

            UIThemeConfig theme = GetRuntimeTheme();
            GameUiTextStyle style = ResolveLegacyTextStyle(fontSize, fontStyle);
            bool inverse = ShouldUseInverseText(target, legacyColor);
            GameUiStyle.ApplyPixelText(target, theme, style, inverse);
            if (!inverse && IsLegacyAccentText(legacyColor))
            {
                target.color = theme.gold;
            }
        }

        private static GameUiTextStyle ResolveLegacyTextStyle(int fontSize, FontStyle fontStyle)
        {
            if (fontSize >= 30)
            {
                return GameUiTextStyle.Title;
            }

            if (fontSize >= 22 || fontStyle == FontStyle.Bold)
            {
                return GameUiTextStyle.Subtitle;
            }

            return fontSize <= 16 ? GameUiTextStyle.SmallText : GameUiTextStyle.Body;
        }

        private static GameUiPanelVariant ResolveLegacyPanelVariant(Color fill)
        {
            return fill.r < 0.10f && fill.g < 0.13f ? GameUiPanelVariant.Dark : GameUiPanelVariant.Standard;
        }

        private static Color ResolveLegacyBorderColor(UIThemeConfig theme, GameUiPanelVariant variant)
        {
            switch (variant)
            {
                case GameUiPanelVariant.Success:
                    return theme.success;
                case GameUiPanelVariant.Warning:
                    return theme.danger;
                default:
                    return theme.panelBorder;
            }
        }

        private static bool IsLegacyInverseText(Color color)
        {
            return color.g > 0.70f || color.b > 0.70f;
        }

        private static bool ShouldUseInverseText(Text target, Color legacyColor)
        {
            PixelPanel panel = target == null ? null : target.GetComponentInParent<PixelPanel>();
            if (panel == null)
            {
                return IsLegacyInverseText(legacyColor);
            }

            return panel.variant == GameUiPanelVariant.Dark
                || panel.variant == GameUiPanelVariant.Success
                || panel.variant == GameUiPanelVariant.Warning;
        }

        private static bool IsLegacyAccentText(Color color)
        {
            return color.r > 0.80f && color.g > 0.55f && color.b < 0.50f;
        }

        private static void RecolorLegacyBorder(RectTransform rect, Color color)
        {
            if (rect == null)
            {
                return;
            }

            SetLegacyBorderColor(rect, "TopBorder", color);
            SetLegacyBorderColor(rect, "BottomBorder", color);
            SetLegacyBorderColor(rect, "LeftBorder", color);
            SetLegacyBorderColor(rect, "RightBorder", color);
        }

        private static void SetLegacyBorderColor(RectTransform rect, string childName, Color color)
        {
            Transform child = rect.Find(childName);
            Image image = child == null ? null : child.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }

        private Sprite GetSolidSprite()
        {
            if (solidSprite != null)
            {
                return solidSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
            return solidSprite;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            string path = GetFullPath(assetPath);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            byte[] bytes = File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            if (!texture.LoadImage(bytes))
            {
                Destroy(texture);
                return null;
            }

            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.one * 0.5f, 100f);
        }

        private static string GetFullPath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return string.Empty;
            }

            return assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length))
                : assetPath;
        }

        private static void Stretch(RectTransform rect, float padding = 0f)
        {
            SetStretch(rect, Vector2.zero, Vector2.one, new Vector2(padding, padding), new Vector2(-padding, -padding));
        }

        private static void SetStretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        private static string TruncateRuntimeText(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            int safeLength = Mathf.Max(0, maxLength - 3);
            return value.Substring(0, safeLength) + "...";
        }

        private static void SetTop(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void SetBottom(RectTransform rect, float left, float right, float bottom, float height)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, bottom + height);
        }

        private static void SetTopCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetCenter(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(0f, 0f);
            rect.pivot = new Vector2(0f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetBottomRight(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(1f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetRightMiddle(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(1f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void SetLeftMiddle(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private class SceneMapInteractionPoint
        {
            public string displayName;
            public string description;
            public ActivityData activity;
            public bool returnsToMain;
            public Rect interactionRect;
            public Vector2 facingTarget;
            public RectTransform marker;
            public RectTransform debugRect;
            public Color markerFill;
            public Color markerBorder;
        }
    }
}
