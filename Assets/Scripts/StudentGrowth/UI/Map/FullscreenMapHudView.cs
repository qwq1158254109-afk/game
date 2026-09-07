using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class FullscreenMapHudView : MonoBehaviour
    {
        public UIThemeConfig theme;
        public PixelPanel locationPanel;
        public PixelPanel statsPanel;
        public PixelPanel questPanel;
        public PixelPanel quickbarPanel;
        public PixelPanel actionPanel;
        public Text locationNameText;
        public Text stageText;
        public Text questTitleText;
        public Text questMainText;
        public Text questSubText;
        public Text timeText;
        public Text energyText;
        public Text pressureText;
        public Text moodText;
        public Text parentRelationText;
        public Text moneyText;
        public Image energyBarBack;
        public Image pressureBarBack;
        public Image moodBarBack;
        public Image energyBarFill;
        public Image pressureBarFill;
        public Image moodBarFill;
        public Text[] quickbarSlotTexts = new Text[0];
        public Image timeIcon;
        public Image energyIcon;
        public Image pressureIcon;
        public Image moodIcon;
        public Image parentRelationIcon;
        public Image moneyIcon;
        public Image[] quickbarSlotIcons = new Image[0];
        public Image statusButtonIcon;
        public Image inventoryButtonIcon;
        public Image deckButtonIcon;
        public Image settingsButtonIcon;
        public Image returnButtonIcon;
        public Button statusButton;
        public Button inventoryButton;
        public Button deckButton;
        public Button settingsButton;
        public Button returnButton;
        public GameSessionState sessionState;
        public SceneTransitionController sceneTransitionController;
        public string fallbackLocationName = "地图";
        public bool showResourcePanel = true;
        public bool showActionPanel;
        public bool applyStyleOnEnable = true;
        public float refreshInterval = 0.2f;
        public bool useSchoolPresentation;
        public bool showSchoolStageObjective;
        public RpgPlayerController playerController;
        public PlayerStatusView playerStatusView;
        public ResourceHudView resourceHudView;
        public StageObjectiveView stageObjectiveView;
        public ActionHintBarView actionHintBarView;

        private const float SimplifiedHudWidth = 432f;
        private const float SimplifiedHudHeight = 156f;
        private const float HudLeftPadding = 18f;
        private const float HudRightPadding = 18f;
        private const float HudIconSize = 22f;
        private const float HudIconSpacing = 8f;
        private const float HudStatLabelX = 18f;
        private const float HudStatBarX = 128f;
        private const float HudStatBarWidth = 270f;
        private const float HudStatBarHeight = 10f;
        private const float HudStatFirstY = -82f;
        private const float HudStatRowGap = 24f;
        private const float ResourcePanelWidth = 264f;
        private const float ResourcePanelHeight = 42f;
        private const string MapExplorationActivityLabel = "地图探索";
        private const int MaxLocationTitleCharacters = 10;
        private const int MaxStageTitleCharacters = 12;

        private static readonly string[] QuickbarNumbers =
        {
            "1", "2", "3", "4", "5", "6", "7", "8", "9", "0"
        };

        private static readonly string[] QuickbarTooltips =
        {
            "1 休息",
            "2 制定计划",
            "3 学习",
            "4 家人互动",
            "5 出口 / 地图切换",
            "6 背包",
            "7 卡组",
            "8 设置",
            "9 返回",
            "0 备用"
        };

        private string currentLocationName;
        private float nextRefreshTime;

        private void OnEnable()
        {
            if (applyStyleOnEnable)
            {
                ApplyStyle();
            }

            Refresh();
        }

        private void Update()
        {
            if (refreshInterval > 0f && Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
            Refresh();
        }

        public void Configure(GameSessionState state, SceneTransitionController sceneTransition)
        {
            sessionState = state;
            sceneTransitionController = sceneTransition;
        }

        public void ConfigureForMap(
            GameSessionState state,
            SceneTransitionController sceneTransition,
            RpgPlayerController mapPlayerController,
            UIThemeConfig mapTheme,
            bool schoolPresentation)
        {
            sessionState = state;
            sceneTransitionController = sceneTransition;
            playerController = mapPlayerController;
            theme = mapTheme;
            useSchoolPresentation = schoolPresentation;
            ApplyStyle();
            Refresh();
        }

        public void SetLocationName(string locationName)
        {
            currentLocationName = locationName;
            SetText(locationNameText, BuildLocationTitle(currentLocationName, fallbackLocationName));
        }

        public void ApplyStyle()
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            ApplyCompactLayout();
            ApplyPanel(locationPanel, GameUiPanelVariant.Dark, resolved.hudPlateSprite);
            ApplyPanel(statsPanel, GameUiPanelVariant.Dark, resolved.hudPlateSprite);
            ApplyPanel(questPanel, GameUiPanelVariant.Dark, resolved.taskPanelSprite != null ? resolved.taskPanelSprite : resolved.hudPlateSprite);
            ApplyPanel(quickbarPanel, GameUiPanelVariant.Dark, resolved.quickbarSprite != null ? resolved.quickbarSprite : resolved.hudPlateSprite);
            ApplyPanel(actionPanel, GameUiPanelVariant.Dark, resolved.quickbarSprite != null ? resolved.quickbarSprite : resolved.hudPlateSprite);
            ApplyQuickbarPanelSprites(resolved);

            GameUiStyle.ApplyPixelText(locationNameText, resolved, GameUiTextStyle.ButtonText, true);
            GameUiStyle.ApplyPixelText(stageText, resolved, GameUiTextStyle.SmallText, true);
            GameUiStyle.ApplyPixelText(questTitleText, resolved, GameUiTextStyle.ButtonText, true);
            GameUiStyle.ApplyPixelText(questMainText, resolved, GameUiTextStyle.SmallText, true);
            GameUiStyle.ApplyPixelText(questSubText, resolved, GameUiTextStyle.SmallText, true);
            GameUiStyle.ApplyPixelText(timeText, resolved, GameUiTextStyle.StatText, true);
            GameUiStyle.ApplyPixelText(energyText, resolved, GameUiTextStyle.SmallText, true);
            GameUiStyle.ApplyPixelText(pressureText, resolved, GameUiTextStyle.SmallText, true);
            GameUiStyle.ApplyPixelText(moodText, resolved, GameUiTextStyle.SmallText, true);
            GameUiStyle.ApplyPixelText(parentRelationText, resolved, GameUiTextStyle.SmallText, true);
            GameUiStyle.ApplyPixelText(moneyText, resolved, GameUiTextStyle.StatText, true);
            ConfigureSimplifiedHudText();
            ApplyQuickbarTextStyle(resolved);
            ApplyProgressBarStyle(resolved);

            ApplyButton(statusButton);
            ApplyButton(inventoryButton);
            ApplyButton(deckButton);
            ApplyButton(settingsButton);
            ApplyButton(returnButton);

            ApplyIcon(timeIcon, resolved.timeIcon, resolved.time);
            ApplyIcon(energyIcon, resolved.energyIcon, resolved.bodyText);
            ApplyIcon(pressureIcon, resolved.stressIcon, resolved.bodyText);
            ApplyIcon(moodIcon, resolved.moodIcon, resolved.bodyText);
            ApplyIcon(parentRelationIcon, resolved.relationIcon, resolved.bodyText);
            ApplyIcon(moneyIcon, resolved.moneyIcon, resolved.gold);
            ApplyQuickbarIcons(resolved);
            ApplyIcon(statusButtonIcon, resolved.moodIcon, resolved.inverseText);
            ApplyIcon(inventoryButtonIcon, resolved.inventoryIcon, resolved.inverseText);
            ApplyIcon(deckButtonIcon, resolved.cardsIcon, resolved.inverseText);
            ApplyIcon(settingsButtonIcon, resolved.settingsIcon, resolved.inverseText);
            ApplyIcon(returnButtonIcon, resolved.backIcon, resolved.inverseText);
            ApplySimplifiedHudVisibility();
            if (useSchoolPresentation)
            {
                ApplySchoolPresentation();
            }
        }

        public void SetOptionalButtonsInteractable(bool interactable)
        {
            SetInteractable(statusButton, interactable);
            SetInteractable(inventoryButton, interactable);
            SetInteractable(deckButton, interactable);
            SetInteractable(settingsButton, interactable);
            ApplyStyle();
        }

        public void Refresh()
        {
            GameSessionState state = ResolveSessionState();
            PlayerState player = state == null ? null : state.playerState;
            StageState stageState = state == null ? null : state.stageState;
            StageData stage = stageState == null ? null : stageState.currentStage;

            if (useSchoolPresentation)
            {
                RefreshSchoolPresentation(player, stageState);
                return;
            }

            SetText(locationNameText, BuildLocationTitle(currentLocationName, fallbackLocationName));
            SetText(stageText, BuildStageActivityText(stage));
            SetText(timeText, stageState == null ? "-" : stageState.remainingTime.ToString());
            SetText(energyText, player == null ? "精力 -" : "精力 " + player.energy + "/" + player.maxEnergy);
            SetText(pressureText, player == null ? "压力 -" : "压力 " + player.pressure);
            SetText(moodText, player == null ? "心情 -" : "心情 " + player.mood);
            SetText(parentRelationText, player == null ? "亲子 -" : "亲子 " + player.parentRelation);
            SetText(moneyText, player == null ? "-" : player.money.ToString());
            RefreshQuestPanel(stageState, stage);
            RefreshQuickbarLabels();
            RefreshProgressBars(player);
            ApplyDynamicTextColors(player);
        }

        private void ApplySchoolPresentation()
        {
            EnsureSchoolViews();
            Graphic[] obsoleteStatusGraphics =
            {
                locationNameText,
                stageText,
                energyIcon,
                pressureIcon,
                moodIcon,
                parentRelationIcon,
                parentRelationText
            };

            playerStatusView.Configure(
                locationPanel,
                energyText,
                pressureText,
                moodText,
                energyBarBack,
                pressureBarBack,
                moodBarBack,
                energyBarFill,
                pressureBarFill,
                moodBarFill,
                obsoleteStatusGraphics,
                theme);
            resourceHudView.Configure(statsPanel, moneyIcon, moneyText, timeIcon, timeText, theme);
            stageObjectiveView.Configure(
                questPanel,
                questTitleText,
                questMainText,
                questSubText,
                theme,
                showSchoolStageObjective);
            actionHintBarView.Configure(quickbarPanel, quickbarSlotTexts, quickbarSlotIcons, theme);
            SetPanelVisible(statsPanel, showResourcePanel);
            SetPanelVisible(actionPanel, false);
        }

        private void RefreshSchoolPresentation(PlayerState player, StageState stageState)
        {
            EnsureSchoolViews();
            playerStatusView.Refresh(player);
            resourceHudView.Refresh(player, stageState);
            stageObjectiveView.Refresh(stageState);
            actionHintBarView.RefreshBindings(playerController);
        }

        private void EnsureSchoolViews()
        {
            if (playerStatusView == null && locationPanel != null)
            {
                playerStatusView = locationPanel.GetComponent<PlayerStatusView>();
                if (playerStatusView == null)
                {
                    playerStatusView = locationPanel.gameObject.AddComponent<PlayerStatusView>();
                }
            }

            if (resourceHudView == null && statsPanel != null)
            {
                resourceHudView = statsPanel.GetComponent<ResourceHudView>();
                if (resourceHudView == null)
                {
                    resourceHudView = statsPanel.gameObject.AddComponent<ResourceHudView>();
                }
            }

            if (stageObjectiveView == null && questPanel != null)
            {
                stageObjectiveView = questPanel.GetComponent<StageObjectiveView>();
                if (stageObjectiveView == null)
                {
                    stageObjectiveView = questPanel.gameObject.AddComponent<StageObjectiveView>();
                }
            }

            if (actionHintBarView == null && quickbarPanel != null)
            {
                actionHintBarView = quickbarPanel.GetComponent<ActionHintBarView>();
                if (actionHintBarView == null)
                {
                    actionHintBarView = quickbarPanel.gameObject.AddComponent<ActionHintBarView>();
                }
            }
        }

        private GameSessionState ResolveSessionState()
        {
            if (sessionState == null && sceneTransitionController != null)
            {
                sessionState = sceneTransitionController.sessionState;
            }

            return sessionState;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static string BuildLocationTitle(string locationName, string fallback)
        {
            string resolved = IsNullOrWhiteSpace(locationName) ? fallback : locationName;
            return "地点  " + CompactLabel(resolved, MaxLocationTitleCharacters);
        }

        private static string BuildStageActivityText(StageData stage)
        {
            string stageName = stage == null || IsNullOrWhiteSpace(stage.displayName)
                ? "-"
                : CompactLabel(stage.displayName, MaxStageTitleCharacters);
            return "阶段  " + stageName + "\n当前活动  " + MapExplorationActivityLabel;
        }

        private static string CompactLabel(string value, int maxCharacters)
        {
            if (IsNullOrWhiteSpace(value))
            {
                return "-";
            }

            string compact = value.Trim();
            int separatorIndex = compact.IndexOf('|');
            if (separatorIndex < 0)
            {
                separatorIndex = compact.IndexOf('｜');
            }

            if (separatorIndex > 0)
            {
                compact = compact.Substring(0, separatorIndex).Trim();
            }

            if (maxCharacters > 0 && compact.Length > maxCharacters)
            {
                compact = compact.Substring(0, maxCharacters);
            }

            return compact;
        }

        private static bool IsNullOrWhiteSpace(string value)
        {
            return string.IsNullOrEmpty(value) || value.Trim().Length == 0;
        }

        private void ApplyPanel(PixelPanel panel, GameUiPanelVariant variant, Sprite sprite)
        {
            if (panel == null)
            {
                return;
            }

            panel.theme = theme;
            panel.variant = variant;
            panel.titleText = null;
            panel.ApplyStyle();
            if (panel.background != null)
            {
                GameUiStyle.ApplyPanelSprite(panel.background, sprite, GameUiStyle.ResolvePanelColor(theme, variant));
            }
        }

        private void ApplyCompactLayout()
        {
            SetPanelVisible(statsPanel, showResourcePanel);
            SetPanelVisible(actionPanel, showActionPanel);
            SetGraphicVisible(parentRelationIcon, false);
            SetGraphicVisible(parentRelationText, false);
            SetChildActive(locationPanel, "PortraitPlate", false);

            SetRect(locationPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(SimplifiedHudWidth, SimplifiedHudHeight));
            SetTextRect(locationNameText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HudLeftPadding, -14f), new Vector2(280f, 24f));
            SetHudIconStripLayout();
            SetTextRect(stageText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(HudLeftPadding, -42f), new Vector2(-(HudLeftPadding + HudRightPadding), 34f));
            SetTextRect(energyText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HudStatLabelX, HudStatFirstY), new Vector2(98f, 20f));
            SetTextRect(pressureText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HudStatLabelX, HudStatFirstY - HudStatRowGap), new Vector2(98f, 20f));
            SetTextRect(moodText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(HudStatLabelX, HudStatFirstY - HudStatRowGap * 2f), new Vector2(98f, 20f));
            SetBarRect(energyBarBack, new Vector2(HudStatBarX, HudStatFirstY - 6f), new Vector2(HudStatBarWidth, HudStatBarHeight));
            SetBarRect(energyBarFill, new Vector2(HudStatBarX, HudStatFirstY - 6f), new Vector2(HudStatBarWidth, HudStatBarHeight));
            SetBarRect(pressureBarBack, new Vector2(HudStatBarX, HudStatFirstY - HudStatRowGap - 6f), new Vector2(HudStatBarWidth, HudStatBarHeight));
            SetBarRect(pressureBarFill, new Vector2(HudStatBarX, HudStatFirstY - HudStatRowGap - 6f), new Vector2(HudStatBarWidth, HudStatBarHeight));
            SetBarRect(moodBarBack, new Vector2(HudStatBarX, HudStatFirstY - HudStatRowGap * 2f - 6f), new Vector2(HudStatBarWidth, HudStatBarHeight));
            SetBarRect(moodBarFill, new Vector2(HudStatBarX, HudStatFirstY - HudStatRowGap * 2f - 6f), new Vector2(HudStatBarWidth, HudStatBarHeight));

            SetRect(questPanel, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -178f), new Vector2(300f, 108f));
            SetTextRect(questTitleText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -14f), new Vector2(-40f, 22f));
            SetTextRect(questMainText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -42f), new Vector2(-40f, 26f));
            SetTextRect(questSubText, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(20f, -72f), new Vector2(-40f, 28f));

            SetRect(statsPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -182f), new Vector2(ResourcePanelWidth, ResourcePanelHeight));
            SetIconRect(moneyIcon, new Vector2(16f, -12f), new Vector2(18f, 18f));
            SetTextRect(moneyText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -12f), new Vector2(78f, 18f));
            SetIconRect(timeIcon, new Vector2(136f, -12f), new Vector2(18f, 18f));
            SetTextRect(timeText, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(160f, -12f), new Vector2(78f, 18f));

            SetRect(quickbarPanel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(572f, 62f));
            ApplyQuickbarSlotLayout();
        }

        private void ApplySimplifiedHudVisibility()
        {
            SetGraphicVisible(energyIcon, energyIcon != null && energyIcon.sprite != null);
            SetGraphicVisible(pressureIcon, pressureIcon != null && pressureIcon.sprite != null);
            SetGraphicVisible(moodIcon, moodIcon != null && moodIcon.sprite != null);
            SetGraphicVisible(parentRelationIcon, false);
            SetGraphicVisible(parentRelationText, false);
            SetChildActive(locationPanel, "PortraitPlate", false);
        }

        private void ConfigureSimplifiedHudText()
        {
            ConfigureHudText(locationNameText, 18, FontStyle.Bold, TextAnchor.MiddleLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Truncate, 1f);
            ConfigureHudText(stageText, 12, FontStyle.Normal, TextAnchor.UpperLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Truncate, 0.95f);
            ConfigureHudText(energyText, 13, FontStyle.Bold, TextAnchor.MiddleLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Truncate, 1f);
            ConfigureHudText(pressureText, 13, FontStyle.Bold, TextAnchor.MiddleLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Truncate, 1f);
            ConfigureHudText(moodText, 13, FontStyle.Bold, TextAnchor.MiddleLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Truncate, 1f);
            ConfigureHudText(moneyText, 14, FontStyle.Bold, TextAnchor.MiddleLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Truncate, 1f);
            ConfigureHudText(timeText, 14, FontStyle.Bold, TextAnchor.MiddleLeft, HorizontalWrapMode.Wrap, VerticalWrapMode.Truncate, 1f);
        }

        private static void ConfigureHudText(
            Text text,
            int fontSize,
            FontStyle fontStyle,
            TextAnchor alignment,
            HorizontalWrapMode horizontalOverflow,
            VerticalWrapMode verticalOverflow,
            float lineSpacing)
        {
            if (text == null)
            {
                return;
            }

            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.alignment = alignment;
            text.horizontalOverflow = horizontalOverflow;
            text.verticalOverflow = verticalOverflow;
            text.lineSpacing = lineSpacing;
        }

        private void SetHudIconStripLayout()
        {
            float startX = SimplifiedHudWidth - HudRightPadding - HudIconSize * 3f - HudIconSpacing * 2f;
            SetIconRect(energyIcon, new Vector2(startX, -14f), new Vector2(HudIconSize, HudIconSize));
            SetIconRect(pressureIcon, new Vector2(startX + HudIconSize + HudIconSpacing, -14f), new Vector2(HudIconSize, HudIconSize));
            SetIconRect(moodIcon, new Vector2(startX + (HudIconSize + HudIconSpacing) * 2f, -14f), new Vector2(HudIconSize, HudIconSize));
        }

        private void ApplyButton(Button button)
        {
            if (button == null)
            {
                return;
            }

            PixelButton pixelButton = button.GetComponent<PixelButton>();
            if (pixelButton != null)
            {
                pixelButton.theme = theme;
                pixelButton.ApplyStyle();
                return;
            }

            GameUiStyle.ApplyButton(button, button.targetGraphic as Image, theme);
            Text label = button.GetComponentInChildren<Text>(true);
            GameUiStyle.ApplyPixelText(label, theme, GameUiTextStyle.ButtonText, true);
        }

        private static void ApplyIcon(Image image, Sprite sprite, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.color = color;
            image.enabled = sprite != null;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private void ApplyQuickbarTextStyle(UIThemeConfig resolved)
        {
            if (quickbarSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < quickbarSlotTexts.Length; i++)
            {
                Text text = quickbarSlotTexts[i];
                if (text == null)
                {
                    continue;
                }

                GameUiStyle.ApplyPixelText(text, resolved, GameUiTextStyle.SmallText, true);
                text.alignment = TextAnchor.LowerLeft;
                text.fontSize = 13;
                ConfigureQuickbarTooltip(text, i);
            }
        }

        private void ApplyQuickbarPanelSprites(UIThemeConfig resolved)
        {
            if (resolved.quickbarSlotSprite == null)
            {
                return;
            }

            ApplyChildSlotSprites(quickbarPanel, resolved.quickbarSlotSprite);
            ApplyChildSlotSprites(actionPanel, resolved.quickbarSlotSprite);
        }

        private static void ApplyChildSlotSprites(PixelPanel rootPanel, Sprite slotSprite)
        {
            if (rootPanel == null || slotSprite == null)
            {
                return;
            }

            PixelPanel[] childPanels = rootPanel.GetComponentsInChildren<PixelPanel>(true);
            for (int i = 0; i < childPanels.Length; i++)
            {
                PixelPanel child = childPanels[i];
                if (child == null || child == rootPanel || child.background == null)
                {
                    continue;
                }

                GameUiStyle.ApplyPanelSprite(child.background, slotSprite, Color.white);
            }
        }

        private void ApplyQuickbarIcons(UIThemeConfig resolved)
        {
            if (quickbarSlotIcons == null)
            {
                return;
            }

            Sprite[] sprites =
            {
                resolved.energyIcon,
                resolved.timeIcon,
                resolved.cardsIcon,
                resolved.relationIcon,
                resolved.backIcon,
                resolved.inventoryIcon,
                resolved.cardsIcon,
                resolved.settingsIcon,
                resolved.backIcon,
                null
            };

            for (int i = 0; i < quickbarSlotIcons.Length; i++)
            {
                Sprite sprite = i < sprites.Length ? sprites[i] : null;
                ApplyIcon(quickbarSlotIcons[i], sprite, resolved.inverseText);
            }
        }

        private void RefreshQuestPanel(StageState stageState, StageData stage)
        {
            string stageName = stage == null || string.IsNullOrEmpty(stage.displayName)
                ? "当前阶段"
                : stage.displayName;
            string remaining = stageState == null ? "-" : stageState.remainingTime.ToString();

            SetText(questTitleText, "任务");
            SetText(questMainText, "主线\n推进 " + stageName);
            SetText(questSubText, "支线  完成家中活动\n剩余时间：" + remaining);
        }

        private void RefreshQuickbarLabels()
        {
            if (quickbarSlotTexts == null)
            {
                return;
            }

            for (int i = 0; i < quickbarSlotTexts.Length; i++)
            {
                SetText(quickbarSlotTexts[i], i < QuickbarNumbers.Length ? QuickbarNumbers[i] : string.Empty);
            }
        }

        private void ApplyProgressBarStyle(UIThemeConfig resolved)
        {
            ConfigureBarBackground(energyBarBack);
            ConfigureBarBackground(pressureBarBack);
            ConfigureBarBackground(moodBarBack);
            ConfigureProgressBar(energyBarFill, resolved.success);
            ConfigureProgressBar(pressureBarFill, resolved.danger);
            ConfigureProgressBar(moodBarFill, resolved.gold);
        }

        private void RefreshProgressBars(PlayerState player)
        {
            SetFill(energyBarFill, player == null || player.maxEnergy <= 0 ? 0f : (float)player.energy / player.maxEnergy);
            SetFill(pressureBarFill, player == null ? 0f : player.pressure / 100f);
            SetFill(moodBarFill, player == null ? 0f : player.mood / 100f);
        }

        private static void SetInteractable(Button button, bool interactable)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }

        private void ApplyDynamicTextColors(PlayerState player)
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (timeText != null)
            {
                timeText.color = resolved.time;
            }

            if (timeIcon != null)
            {
                timeIcon.color = resolved.time;
            }

            if (moneyText != null)
            {
                moneyText.color = resolved.gold;
            }

            if (moneyIcon != null)
            {
                moneyIcon.color = resolved.gold;
            }

            if (pressureText != null)
            {
                pressureText.color = player != null && player.pressure >= 60 ? resolved.danger : resolved.inverseText;
            }

            if (pressureIcon != null)
            {
                pressureIcon.color = player != null && player.pressure >= 60 ? resolved.danger : resolved.inverseText;
            }

            if (moodText != null)
            {
                moodText.color = player != null && player.mood >= 60 ? resolved.success : resolved.inverseText;
            }

            if (moodIcon != null)
            {
                moodIcon.color = player != null && player.mood >= 60 ? resolved.success : resolved.inverseText;
            }
        }

        private void ApplyQuickbarSlotLayout()
        {
            if (quickbarSlotTexts == null)
            {
                return;
            }

            const float slotSize = 48f;
            const float spacing = 7f;
            float startX = -((slotSize * quickbarSlotTexts.Length) + (spacing * (quickbarSlotTexts.Length - 1))) * 0.5f;

            for (int i = 0; i < quickbarSlotTexts.Length; i++)
            {
                Text label = quickbarSlotTexts[i];
                Transform slot = label == null ? null : label.transform.parent;
                RectTransform slotRect = slot as RectTransform;
                if (slotRect == null)
                {
                    continue;
                }

                SetRect(slotRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 0.5f), new Vector2(startX + i * (slotSize + spacing), 0f), new Vector2(slotSize, slotSize));
                SetTextRect(label, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(6f, 4f), new Vector2(-12f, -8f));

                if (quickbarSlotIcons != null && i < quickbarSlotIcons.Length)
                {
                    SetRect(quickbarSlotIcons[i].rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2f, 3f), new Vector2(24f, 24f));
                }
            }
        }

        private static void ConfigureQuickbarTooltip(Text text, int index)
        {
            Transform slot = text == null ? null : text.transform.parent;
            if (slot == null)
            {
                return;
            }

            Image image = slot.GetComponent<Image>();
            if (image != null)
            {
                image.raycastTarget = true;
            }

            QuickbarTooltipTarget tooltip = slot.GetComponent<QuickbarTooltipTarget>();
            if (tooltip == null)
            {
                tooltip = slot.gameObject.AddComponent<QuickbarTooltipTarget>();
            }

            tooltip.tooltipText = index >= 0 && index < QuickbarTooltips.Length ? QuickbarTooltips[index] : string.Empty;
        }

        private static void ConfigureProgressBar(Image image, Color color)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = GameUiStyle.GetSolidSprite();
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Horizontal;
            image.fillOrigin = 0;
            image.color = color;
            image.raycastTarget = false;
        }

        private static void ConfigureBarBackground(Image image)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = GameUiStyle.GetSolidSprite();
            image.type = Image.Type.Simple;
            image.color = new Color(0.05f, 0.04f, 0.03f, 0.62f);
            image.raycastTarget = false;
        }

        private static void SetFill(Image image, float value)
        {
            if (image != null)
            {
                image.fillAmount = Mathf.Clamp01(value);
            }
        }

        private static void SetPanelVisible(PixelPanel panel, bool visible)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(visible);
            }
        }

        private static void SetGraphicVisible(Graphic graphic, bool visible)
        {
            if (graphic != null)
            {
                graphic.gameObject.SetActive(visible);
            }
        }

        private static void SetChildActive(PixelPanel panel, string childName, bool active)
        {
            Transform root = panel == null ? null : panel.transform;
            Transform child = root == null ? null : root.Find(childName);
            if (child != null)
            {
                child.gameObject.SetActive(active);
            }
        }

        private static void SetIconRect(Image image, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (image != null)
            {
                SetRect(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, sizeDelta);
            }
        }

        private static void SetBarRect(Image image, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (image != null)
            {
                SetRect(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), anchoredPosition, sizeDelta);
            }
        }

        private static void SetTextRect(Text text, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (text != null)
            {
                SetRect(text.rectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            }
        }

        private static void SetRect(PixelPanel panel, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (panel != null)
            {
                SetRect(panel.transform as RectTransform, anchorMin, anchorMax, pivot, anchoredPosition, sizeDelta);
            }
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}
