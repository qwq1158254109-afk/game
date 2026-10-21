using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class MapActivityResultView : MonoBehaviour
    {
        public UIThemeConfig theme;
        public GameObject panelRoot;
        public PixelPanel panelStyle;
        public CanvasGroup canvasGroup;
        public Image resultIcon;
        public PixelPanel summaryPanelStyle;
        public PixelPanel detailPanelStyle;
        public Image actionIcon;
        public Image timeIcon;
        public Image rewardIcon;
        public Text titleText;
        public Text summaryText;
        public Text detailText;
        public Button closeButton;

        private void Awake()
        {
            ResolveReferences();
            // 不在此 Hide：该面板在场景中序列化为 inactive，首次 Show() 的 SetActive(true)
            // 会在 Show 内部同步触发 Awake，此时 Hide() 会立刻取消这次显示（首次弹出看不到）。
            // 初始不可见由场景序列化的 inactive 状态 + 工厂 BuildDefaultLayout 末尾的 Hide() 保证。
        }

        private void OnEnable()
        {
            ApplyStyle();
            EnsureCloseButtonBound();
        }

        /// <summary>
        /// 修复（2026-10-16）：「行动完成」弹窗的关闭按钮点不动。
        /// 场景里序列化的弹窗实例从未经过 BuildDefaultLayout —— closeButton.onClick 是空的，
        /// 按下有反馈但不触发任何动作。每次显示时幂等补绑（RemoveListener 对未注册委托是 no-op）。
        /// </summary>
        private void EnsureCloseButtonBound()
        {
            if (closeButton == null && panelRoot != null)
            {
                Button[] buttons = panelRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    if (buttons[i] != null && buttons[i].gameObject.name.Contains("Close"))
                    {
                        closeButton = buttons[i];
                        break;
                    }
                }
            }

            if (closeButton == null)
            {
                return;
            }

            closeButton.onClick.RemoveListener(Hide);
            closeButton.onClick.AddListener(Hide);
        }

        public void BuildDefaultLayout(RectTransform parent)
        {
            if (parent == null || titleText != null && detailText != null)
            {
                return;
            }

            panelRoot = panelRoot == null ? gameObject : panelRoot;
            panelRoot.name = "MapActivityResultView";

            RectTransform panelRect = panelRoot.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -12f);
            panelRect.sizeDelta = new Vector2(560f, 300f);

            Image background = panelRoot.GetComponent<Image>();
            if (background == null)
            {
                background = panelRoot.AddComponent<Image>();
            }

            background.raycastTarget = true;
            panelStyle = panelRoot.GetComponent<PixelPanel>();
            if (panelStyle == null)
            {
                panelStyle = panelRoot.AddComponent<PixelPanel>();
            }

            panelStyle.background = background;
            panelStyle.variant = GameUiPanelVariant.Standard;
            canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panelRoot);

            resultIcon = CreateIcon("ResultIcon", panelRect);
            SetRect(resultIcon.rectTransform, new Vector2(24f, -24f), new Vector2(36f, 36f), new Vector2(0f, 1f));

            titleText = CreateText("Title", panelRect, "行动完成", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(titleText.rectTransform, new Vector2(72f, -22f), new Vector2(380f, 36f), new Vector2(0f, 1f));

            RectTransform summaryPanel = CreateInfoPanel("SummaryPanel", panelRect, out summaryPanelStyle);
            SetRect(summaryPanel, new Vector2(28f, -74f), new Vector2(504f, 76f), new Vector2(0f, 1f));
            actionIcon = CreateIcon("ActionIcon", summaryPanel);
            SetRect(actionIcon.rectTransform, new Vector2(16f, -18f), new Vector2(26f, 26f), new Vector2(0f, 1f));

            summaryText = CreateText("Summary", summaryPanel, string.Empty, 17, FontStyle.Bold, TextAnchor.UpperLeft);
            summaryText.lineSpacing = 1.12f;
            SetRect(summaryText.rectTransform, new Vector2(54f, -14f), new Vector2(426f, 50f), new Vector2(0f, 1f));

            RectTransform detailPanel = CreateInfoPanel("DetailPanel", panelRect, out detailPanelStyle);
            SetRect(detailPanel, new Vector2(28f, -164f), new Vector2(504f, 82f), new Vector2(0f, 1f));
            timeIcon = CreateIcon("TimeIcon", detailPanel);
            SetRect(timeIcon.rectTransform, new Vector2(16f, -16f), new Vector2(22f, 22f), new Vector2(0f, 1f));
            rewardIcon = CreateIcon("RewardIcon", detailPanel);
            SetRect(rewardIcon.rectTransform, new Vector2(16f, -48f), new Vector2(22f, 22f), new Vector2(0f, 1f));

            detailText = CreateText("Detail", detailPanel, string.Empty, 16, FontStyle.Bold, TextAnchor.UpperLeft);
            detailText.lineSpacing = 1.08f;
            SetRect(detailText.rectTransform, new Vector2(54f, -12f), new Vector2(426f, 58f), new Vector2(0f, 1f));

            closeButton = CreateButton("CloseButton", panelRect, "知道了");
            SetRect(closeButton.transform as RectTransform, new Vector2(436f, 24f), new Vector2(96f, 38f), Vector2.zero);
            closeButton.onClick.AddListener(Hide);

            ApplyStyle();
            Hide();
        }

        public void Show(LocationActionExecutionResult result)
        {
            if (result == null || result.activity == null)
            {
                Hide();
                return;
            }

            ResolveReferences();
            SetVisible(true);
            SetText(titleText, "行动完成");
            SetText(summaryText, BuildSummary(result));
            SetText(detailText, BuildDetail(result));
            ApplyResultIcon(result);
            ApplyStyle();
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void ApplyStyle()
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (panelStyle != null)
            {
                panelStyle.theme = resolved;
                panelStyle.variant = GameUiPanelVariant.Standard;
                panelStyle.ApplyStyle();
                ApplyResultSprite(resolved);
            }

            ApplyInfoPanel(summaryPanelStyle, resolved);
            ApplyInfoPanel(detailPanelStyle, resolved);
            GameUiStyle.ApplyPixelText(titleText, resolved, GameUiTextStyle.Subtitle);
            GameUiStyle.ApplyPixelText(summaryText, resolved, GameUiTextStyle.ButtonText, true);
            GameUiStyle.ApplyPixelText(detailText, resolved, GameUiTextStyle.ButtonText, true);
            ApplyIcon(timeIcon, resolved.timeIcon, resolved.time);
            ApplyIcon(rewardIcon, resolved.inventoryIcon == null ? resolved.moneyIcon : resolved.inventoryIcon, resolved.gold);
            ApplyButton(closeButton);
        }

        private void ApplyResultSprite(UIThemeConfig resolved)
        {
            Image background = panelStyle == null ? null : panelStyle.background;
            if (background == null || resolved.resultPanelSprite == null)
            {
                return;
            }

            background.sprite = resolved.resultPanelSprite;
            background.type = Image.Type.Sliced;
            GameUiStyle.ApplyPanelSprite(background, resolved.resultPanelSprite, resolved.panelBackground);
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
            GameUiStyle.ApplyPixelText(button.GetComponentInChildren<Text>(true), theme, GameUiTextStyle.ButtonText, true);
        }

        private void ApplyResultIcon(LocationActionExecutionResult result)
        {
            if (resultIcon == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            Sprite icon = result.activity != null && result.activity.icon != null
                ? result.activity.icon
                : resolved.confirmIcon;
            ApplyIcon(resultIcon, icon, resolved.success);
            ApplyIcon(actionIcon, icon, resolved.success);
        }

        private void ResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panelStyle == null && panelRoot != null)
            {
                panelStyle = panelRoot.GetComponent<PixelPanel>();
            }

            if (canvasGroup == null && panelRoot != null)
            {
                canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panelRoot);
            }
        }

        private static string BuildSummary(LocationActionExecutionResult result)
        {
            ActivityData activity = result.activity;
            string activityName = activity == null || string.IsNullOrEmpty(activity.displayName)
                ? "活动"
                : activity.displayName;
            string resultMessage = result.activityResult == null ? string.Empty : result.activityResult.message;
            return "行动  " + activityName
                + "\n反馈  " + (string.IsNullOrEmpty(resultMessage) ? "行动已经结算。" : resultMessage);
        }

        private static string BuildDetail(LocationActionExecutionResult result)
        {
            string rewards = result.activityResult == null
                ? string.Empty
                : MapInteractionPromptFormatter.BuildRewardPreview(result.activityResult.rewards);
            string time = result.timeAdvanced
                ? result.remainingTimeBefore + " -> " + result.remainingTimeAfter
                : "未消耗";
            return "时间  " + time
                + "\n获得  " + (string.IsNullOrEmpty(rewards) ? "无额外奖励" : rewards);
        }

        private static void ApplyInfoPanel(PixelPanel panel, UIThemeConfig theme)
        {
            if (panel == null)
            {
                return;
            }

            panel.theme = theme;
            panel.variant = GameUiPanelVariant.Dark;
            panel.ApplyStyle();
            if (panel.background != null)
            {
                GameUiStyle.ApplyPanelSprite(panel.background, theme.taskPanelSprite, GameUiStyle.ResolvePanelColor(theme, GameUiPanelVariant.Dark));
            }
        }

        private static void ApplyIcon(Image icon, Sprite sprite, Color color)
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = sprite;
            icon.color = sprite == null ? Color.clear : color;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        private static Image CreateIcon(string name, RectTransform parent)
        {
            GameObject iconObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = iconObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Image image = iconObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.enabled = false;
            return image;
        }

        private static RectTransform CreateInfoPanel(string name, RectTransform parent, out PixelPanel panel)
        {
            GameObject panelObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = panelObject.transform as RectTransform;
            rect.SetParent(parent, false);

            Image background = panelObject.AddComponent<Image>();
            background.raycastTarget = false;
            panel = panelObject.AddComponent<PixelPanel>();
            panel.background = background;
            panel.variant = GameUiPanelVariant.Dark;
            panel.ApplyStyle();
            return rect;
        }

        private static Text CreateText(string name, RectTransform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = textObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = anchor;
            text.raycastTarget = false;
            GameUiStyle.ApplyTextFromFontSpec(text, null, fontSize, fontStyle);
            return text;
        }

        private Button CreateButton(string name, RectTransform parent, string label)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = buttonObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Text", rect, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.background = image;
            pixelButton.labelText = text;
            pixelButton.theme = theme;
            pixelButton.ApplyStyle();
            return button;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchor)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, anchor.y > 0.5f ? 1f : 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
