using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class TimeActionHudView : MonoBehaviour
    {
        private const string ResourceRoot = "StudentGrowth/TimeAction/";

        public Image periodBackground;
        public Image periodIcon;
        public Image dateLogoBackground;
        public Image[] actionPointIcons = new Image[TimeActionState.ActionPointsPerPeriod];
        public Text dayText;
        public Text periodText;
        public Text actionPointText;
        public Text remainingDailyText;
        public Text sleepWarningText;
        public RectTransform periodIconArea;
        public RectTransform timeInfoArea;
        public RectTransform dayPeriodRow;
        public RectTransform actionPointRow;
        public GameSessionState sessionState;
        public TimeActionController timeActionController;
        public float refreshInterval = 0.2f;
        public UIThemeConfig theme;
        public PixelPanel compactPanel;
        public bool compactSchoolLayout;
        public bool useEventOnlyRefresh;

        private Sprite morningBackground;
        private Sprite afternoonBackground;
        private Sprite eveningBackground;
        private Sprite nightBackground;
        private Sprite morningIcon;
        private Sprite afternoonIcon;
        private Sprite eveningIcon;
        private Sprite nightIcon;
        private Sprite dateLogo;
        private Sprite actionPointFilled;
        private Sprite actionPointEmpty;
        private Material dateFrameMaterial;
        private float nextRefreshTime;
        private bool resourcesLoaded;

        private void OnEnable()
        {
            LoadResourcesIfNeeded();
            ApplyPresentation();
            Subscribe();
            Refresh();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (useEventOnlyRefresh)
            {
                return;
            }

            if (refreshInterval > 0f && Time.unscaledTime < nextRefreshTime)
            {
                return;
            }

            nextRefreshTime = Time.unscaledTime + Mathf.Max(0f, refreshInterval);
            Refresh();
        }

        public void Configure(GameSessionState state, TimeActionController controller)
        {
            Configure(state, controller, theme, compactSchoolLayout, useEventOnlyRefresh);
        }

        public void Configure(
            GameSessionState state,
            TimeActionController controller,
            UIThemeConfig config,
            bool useCompactSchoolLayout,
            bool eventOnly)
        {
            Unsubscribe();
            sessionState = state;
            timeActionController = controller;
            theme = config;
            compactSchoolLayout = useCompactSchoolLayout;
            useEventOnlyRefresh = eventOnly;
            resourcesLoaded = false;
            LoadResourcesIfNeeded();
            ApplyPresentation();
            Subscribe();
            Refresh();
        }

        public void ConfigurePresentation(UIThemeConfig config, bool useCompactSchoolLayout, bool eventOnly)
        {
            theme = config;
            compactSchoolLayout = useCompactSchoolLayout;
            useEventOnlyRefresh = eventOnly;
            resourcesLoaded = false;
            LoadResourcesIfNeeded();
            ApplyPresentation();
            Refresh();
        }

        public void SetTopLeftLayout()
        {
            compactSchoolLayout = false;
            RectTransform rect = transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            // 贴顶改回距顶 8px（2026-10-17 用户反馈「地图选取界面左上角溢出」）。
            // 历史：2026-10-14 曾拍板「完全贴顶」把 y 设为 0，但贴顶后日期图标的描边/投影
            // 会被屏幕边缘裁掉，看着像面板溢出去了；改回 8px 留白，x 维持 32。
            rect.anchoredPosition = new Vector2(32f, -8f);
            rect.sizeDelta = new Vector2(340f, 116f);

            SetTopLeftRect(periodIconArea, Vector2.zero, new Vector2(116f, 116f));
            SetTopLeftRect(timeInfoArea, new Vector2(116f, 0f), new Vector2(224f, 116f));
            SetTopLeftRect(dayPeriodRow, new Vector2(12f, -12f), new Vector2(200f, 32f));
            SetTopLeftRect(actionPointRow, new Vector2(12f, -54f), new Vector2(200f, 42f));

            SetTopLeftRect(dateLogoBackground == null ? null : dateLogoBackground.rectTransform, Vector2.zero, new Vector2(112f, 112f));
            SetTopLeftRect(periodBackground == null ? null : periodBackground.rectTransform, new Vector2(8f, -8f), new Vector2(100f, 100f));
            SetTopLeftRect(periodIcon == null ? null : periodIcon.rectTransform, new Vector2(34f, -30f), new Vector2(44f, 44f));
            SetTopLeftRect(dayText == null ? null : dayText.rectTransform, Vector2.zero, new Vector2(80f, 32f));
            SetTopLeftRect(periodText == null ? null : periodText.rectTransform, new Vector2(82f, 0f), new Vector2(112f, 32f));
            SetTopLeftRect(actionPointText == null ? null : actionPointText.rectTransform, Vector2.zero, new Vector2(104f, 36f));
            SetTopLeftRect(sleepWarningText == null ? null : sleepWarningText.rectTransform, new Vector2(12f, -92f), new Vector2(200f, 20f));

            if (actionPointText != null)
            {
                actionPointText.alignment = TextAnchor.MiddleLeft;
            }

            for (int i = 0; i < actionPointIcons.Length; i++)
            {
                Image point = actionPointIcons[i];
                SetTopLeftRect(point == null ? null : point.rectTransform, new Vector2(104f + i * 32f, -3f), new Vector2(30f, 30f));
            }

            if (dayText != null)
            {
                dayText.gameObject.SetActive(true);
            }

            if (remainingDailyText != null)
            {
                remainingDailyText.gameObject.SetActive(false);
            }

            if (dateLogoBackground != null)
            {
                dateLogoBackground.transform.SetSiblingIndex(0);
                ApplyTransparentDateFrameMaterial();
            }

            if (periodBackground != null)
            {
                periodBackground.transform.SetAsLastSibling();
            }

            if (periodIcon != null)
            {
                periodIcon.transform.SetAsLastSibling();
            }

            if (periodText != null)
            {
                periodText.gameObject.SetActive(true);
            }

            if (compactPanel != null && compactPanel.background != null)
            {
                compactPanel.background.enabled = false;
            }
        }

        private void ApplyPresentation()
        {
            if (!compactSchoolLayout)
            {
                ApplySprite(dateLogoBackground, dateLogo);
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            EnsureCompactPanel(resolved);
            RectTransform rect = transform as RectTransform;
            SetRect(rect, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                new Vector2(-18f, -18f), new Vector2(330f, 76f));

            SetGraphicActive(dateLogoBackground, false);
            SetGraphicActive(periodBackground, false);
            SetGraphicActive(remainingDailyText, false);
            SetGraphicActive(dayText, true);
            SetGraphicActive(periodIcon, true);
            SetGraphicActive(periodText, true);
            SetGraphicActive(actionPointText, true);
            SetGraphicActive(sleepWarningText, true);

            SetRect(dayText == null ? null : dayText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -10f), new Vector2(108f, 24f));
            SetRect(periodIcon == null ? null : periodIcon.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(126f, -10f), new Vector2(24f, 24f));
            SetRect(periodText == null ? null : periodText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(156f, -10f), new Vector2(70f, 24f));
            SetRect(actionPointText == null ? null : actionPointText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(14f, -40f), new Vector2(104f, 22f));
            SetRect(sleepWarningText == null ? null : sleepWarningText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(228f, -10f), new Vector2(88f, 50f));

            for (int i = 0; i < actionPointIcons.Length; i++)
            {
                Image point = actionPointIcons[i];
                SetGraphicActive(point, true);
                SetRect(point == null ? null : point.rectTransform,
                    new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(120f + i * 30f, -39f), new Vector2(24f, 24f));
            }

            ApplyTextStyle(dayText, resolved, GameUiTextStyle.ButtonText, TextAnchor.MiddleLeft, resolved.inverseText, 16);
            ApplyTextStyle(periodText, resolved, GameUiTextStyle.SmallText, TextAnchor.MiddleLeft, resolved.gold, 14);
            ApplyTextStyle(actionPointText, resolved, GameUiTextStyle.SmallText, TextAnchor.MiddleLeft, resolved.inverseText, 13);
            ApplyTextStyle(sleepWarningText, resolved, GameUiTextStyle.SmallText, TextAnchor.UpperLeft, resolved.danger, 11);
        }

        private void EnsureCompactPanel(UIThemeConfig resolved)
        {
            Image background = GetComponent<Image>();
            if (background == null)
            {
                background = gameObject.AddComponent<Image>();
            }

            compactPanel = GetComponent<PixelPanel>();
            if (compactPanel == null)
            {
                compactPanel = gameObject.AddComponent<PixelPanel>();
            }

            compactPanel.background = background;
            compactPanel.theme = resolved;
            compactPanel.variant = GameUiPanelVariant.Dark;
            compactPanel.ApplyStyle();
            GameUiStyle.ApplyPanelSprite(background, resolved.hudPlateSprite, resolved.panelDarkBackground);
            background.enabled = true;
            background.raycastTarget = false;
        }

        private static void ApplyTextStyle(Text text, UIThemeConfig resolved, GameUiTextStyle style,
            TextAnchor alignment, Color color, int fontSize)
        {
            if (text == null)
            {
                return;
            }

            GameUiStyle.ApplyPixelText(text, resolved, style, true);
            text.alignment = alignment;
            text.fontSize = fontSize;
            text.color = color;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
        }

        private void OnDestroy()
        {
            if (dateFrameMaterial != null)
            {
                Destroy(dateFrameMaterial);
            }
        }

        public void Refresh()
        {
            LoadResourcesIfNeeded();
            TimeActionState state = ResolveState();
            if (state == null)
            {
                SetText(dayText, "-");
                SetText(periodText, "-");
                SetText(actionPointText, "行动值 -/3");
                SetText(remainingDailyText, "今日剩余 -/12");
                SetText(sleepWarningText, string.Empty);
                return;
            }

            state.Normalize();
            if (!compactSchoolLayout)
            {
                ApplySprite(dateLogoBackground, dateLogo);
            }
            else
            {
                SetGraphicActive(dateLogoBackground, false);
            }
            ApplyPeriodVisual(state.currentPeriod);
            SetText(dayText, BuildStageProgressLabel());
            SetText(periodText, TimeActionRules.GetPeriodDisplayName(state.currentPeriod));
            SetText(actionPointText, "行动值 " + state.currentActionPoints + "/" + TimeActionState.ActionPointsPerPeriod);
            SetText(remainingDailyText, "今日剩余 " + state.RemainingDailyActionPoints + "/" + TimeActionState.MaxDailyActionPoints);
            SetText(sleepWarningText, state.RequiresSleep ? "行动已用完，请回家睡觉" : string.Empty);
            bool depleted = state.currentActionPoints <= 0;
            if (actionPointText != null)
            {
                UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
                actionPointText.color = depleted
                    ? resolved.actionPointEmptyColor
                    : resolved.inverseText;
            }

            for (int i = 0; i < actionPointIcons.Length; i++)
            {
                Image image = actionPointIcons[i];
                if (image == null)
                {
                    continue;
                }

                image.sprite = i < state.currentActionPoints ? actionPointFilled : actionPointEmpty;
                image.enabled = image.sprite != null;
                UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
                image.color = i < state.currentActionPoints
                    ? resolved.actionPointFilledColor
                    : resolved.actionPointEmptyColor;
                image.preserveAspect = true;
            }
        }

        /// <summary>
        /// 宏观时钟标签。唯一权威来源是阶段剩余回合数（StageState.remainingTime）——
        /// 日内时段/行动点只管幕内节奏，不参与日期（原因：一天 12 行动点，一天会跨 12 个阶段回合，
        /// 显示「第 N 天」会与阶段进度矛盾）。
        /// ★ 长度硬约束：dayText 槽位只有 80x32px（SetTopLeftLayout 设定）配 20px 粗体，
        /// 最多容纳 4 个汉字；超长会折行并被裁切。所以回退文案刻意取短。
        /// dateMilestones 长度为 timeLimit + 1 且该项非空时优先显示；空表是合法状态。
        /// </summary>
        private string BuildStageProgressLabel()
        {
            StageState stageState = sessionState == null ? null : sessionState.stageState;
            StageData stage = stageState == null ? null : stageState.currentStage;
            if (stage == null)
            {
                return "剩 - 次";
            }

            int remaining = Mathf.Max(0, stageState.remainingTime);
            int index = Mathf.Clamp(stage.timeLimit - remaining, 0, Mathf.Max(0, stage.timeLimit));
            string[] milestones = stage.dateMilestones;
            if (milestones != null
                && milestones.Length == stage.timeLimit + 1
                && index < milestones.Length
                && !string.IsNullOrEmpty(milestones[index]))
            {
                return milestones[index];
            }

            return remaining > 0 ? "剩 " + remaining + " 次" : "已走完";
        }

        private TimeActionState ResolveState()
        {
            if (timeActionController != null)
            {
                if (sessionState == null)
                {
                    sessionState = timeActionController.SessionState;
                }

                return timeActionController.State;
            }

            return sessionState == null ? null : sessionState.timeActionState;
        }

        private void LoadResourcesIfNeeded()
        {
            if (resourcesLoaded && dateLogo != null)
            {
                return;
            }

            resourcesLoaded = true;
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            morningBackground = LoadSprite("morning-background");
            afternoonBackground = LoadSprite("afternoon-background");
            eveningBackground = LoadSprite("evening-background");
            nightBackground = LoadSprite("night-background");
            morningIcon = resolved.morningPeriodIcon != null ? resolved.morningPeriodIcon : LoadSprite("morning-icon");
            afternoonIcon = resolved.afternoonPeriodIcon != null ? resolved.afternoonPeriodIcon : LoadSprite("afternoon-icon");
            eveningIcon = resolved.eveningPeriodIcon != null ? resolved.eveningPeriodIcon : LoadSprite("evening-icon");
            nightIcon = resolved.nightPeriodIcon != null ? resolved.nightPeriodIcon : LoadSprite("night-icon");
            dateLogo = LoadSprite("date-logo-background");
            actionPointFilled = resolved.actionPointFilledIcon != null
                ? resolved.actionPointFilledIcon
                : LoadSprite("action-point-filled");
            actionPointEmpty = resolved.actionPointEmptyIcon != null
                ? resolved.actionPointEmptyIcon
                : LoadSprite("action-point-empty");
            if (!compactSchoolLayout)
            {
                ApplySprite(dateLogoBackground, dateLogo);
            }
        }

        private void ApplyPeriodVisual(TimePeriod period)
        {
            Sprite background;
            Sprite icon;
            switch (period)
            {
                case TimePeriod.Morning:
                    background = morningBackground;
                    icon = morningIcon;
                    break;
                case TimePeriod.Afternoon:
                    background = afternoonBackground;
                    icon = afternoonIcon;
                    break;
                case TimePeriod.Evening:
                    background = eveningBackground;
                    icon = eveningIcon;
                    break;
                default:
                    background = nightBackground;
                    icon = nightIcon;
                    break;
            }

            if (!compactSchoolLayout)
            {
                ApplySprite(periodBackground, background);
            }
            else
            {
                SetGraphicActive(periodBackground, false);
            }

            ApplySprite(periodIcon, icon);
            if (compactSchoolLayout && periodIcon != null)
            {
                periodIcon.color = GameUiStyle.ResolveTheme(theme).gold;
            }
        }

        private void Subscribe()
        {
            if (timeActionController != null)
            {
                timeActionController.StateChanged -= Refresh;
                timeActionController.StateChanged += Refresh;
            }
        }

        private void Unsubscribe()
        {
            if (timeActionController != null)
            {
                timeActionController.StateChanged -= Refresh;
            }
        }

        private static Sprite LoadSprite(string name)
        {
            return Resources.Load<Sprite>(ResourceRoot + name);
        }

        private static void ApplySprite(Image target, Sprite sprite)
        {
            if (target == null)
            {
                return;
            }

            target.sprite = sprite;
            target.enabled = sprite != null;
            target.color = Color.white;
            target.preserveAspect = true;
            target.raycastTarget = false;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetTopLeftRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
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

        private static void SetGraphicActive(Graphic graphic, bool active)
        {
            if (graphic != null)
            {
                graphic.gameObject.SetActive(active);
            }
        }

        private void ApplyTransparentDateFrameMaterial()
        {
            if (dateLogoBackground == null)
            {
                return;
            }

            if (dateFrameMaterial == null)
            {
                Shader shader = Shader.Find("StudentGrowth/UI/DarkPixelsTransparent");
                if (shader == null)
                {
                    return;
                }

                dateFrameMaterial = new Material(shader)
                {
                    name = "TimeActionDateFrameMaterial",
                    hideFlags = HideFlags.HideAndDontSave
                };
                dateFrameMaterial.SetFloat("_DarkCutoff", 0.08f);
            }

            dateLogoBackground.material = dateFrameMaterial;
        }

    }
}
