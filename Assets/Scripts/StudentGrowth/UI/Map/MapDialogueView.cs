using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class MapDialogueView : MonoBehaviour
    {
        private const string HintColor = "#73E7C1";

        public UIThemeConfig theme;
        public GameObject panelRoot;
        public PixelPanel panelStyle;
        public CanvasGroup canvasGroup;
        public Image eventIcon;
        public PixelPanel choicePanelStyle;
        public PixelPanel rewardPanelStyle;
        public Image choiceIcon;
        public Image rewardIcon;
        public Text titleText;
        public Text bodyText;
        public Text choiceText;
        public Text rewardText;
        public Button closeButton;

        private void Awake()
        {
            ResolveReferences();
            Hide();
        }

        private void OnEnable()
        {
            ApplyStyle();
        }

        public void BuildDefaultLayout(RectTransform parent)
        {
            if (parent == null || titleText != null)
            {
                return;
            }

            panelRoot = gameObject;
            panelRoot.name = "MapDialogueView";

            RectTransform panelRect = panelRoot.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 88f);
            panelRect.sizeDelta = new Vector2(760f, 286f);

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

            eventIcon = CreateIcon("EventIcon", panelRect);
            SetRect(eventIcon.rectTransform, new Vector2(24f, 218f), new Vector2(42f, 42f), Vector2.zero);

            titleText = CreateText("Title", panelRect, string.Empty, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(titleText.rectTransform, new Vector2(78f, 226f), new Vector2(520f, 34f), Vector2.zero);

            bodyText = CreateText("Body", panelRect, string.Empty, 18, FontStyle.Normal, TextAnchor.UpperLeft);
            bodyText.supportRichText = true;
            bodyText.lineSpacing = 1.08f;
            SetRect(bodyText.rectTransform, new Vector2(28f, 132f), new Vector2(704f, 78f), Vector2.zero);

            RectTransform choicePanel = CreateInfoPanel("ChoicePanel", panelRect, out choicePanelStyle);
            SetRect(choicePanel, new Vector2(28f, 78f), new Vector2(704f, 42f), Vector2.zero);
            choiceIcon = CreateIcon("ChoiceIcon", choicePanel);
            SetRect(choiceIcon.rectTransform, new Vector2(14f, 8f), new Vector2(24f, 24f), Vector2.zero);

            choiceText = CreateText("Choice", choicePanel, string.Empty, 17, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(choiceText.rectTransform, new Vector2(50f, 7f), new Vector2(628f, 26f), Vector2.zero);

            RectTransform rewardPanel = CreateInfoPanel("RewardPanel", panelRect, out rewardPanelStyle);
            SetRect(rewardPanel, new Vector2(28f, 30f), new Vector2(560f, 40f), Vector2.zero);
            rewardIcon = CreateIcon("RewardIcon", rewardPanel);
            SetRect(rewardIcon.rectTransform, new Vector2(14f, 8f), new Vector2(22f, 22f), Vector2.zero);

            rewardText = CreateText("Reward", rewardPanel, string.Empty, 16, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(rewardText.rectTransform, new Vector2(50f, 7f), new Vector2(484f, 24f), Vector2.zero);

            closeButton = CreateButton("CloseButton", panelRect, "继续");
            SetRect(closeButton.transform as RectTransform, new Vector2(626f, 28f), new Vector2(104f, 38f), Vector2.zero);
            closeButton.onClick.AddListener(Hide);

            ApplyStyle();
            Hide();
        }

        public void Show(ActivityResult result)
        {
            if (result == null || result.linkedEvent == null)
            {
                Hide();
                return;
            }

            ResolveReferences();
            SetVisible(true);

            GameEventData eventData = result.linkedEvent;
            SetText(titleText, string.IsNullOrEmpty(eventData.title) ? ResolveActivityName(result) : eventData.title);
            SetText(bodyText, FormatBody(eventData));
            SetText(choiceText, FormatChoice(result.selectedChoice));
            SetText(rewardText, FormatRewards(result));
            ApplyEventIcon(eventData);
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
                ApplyDialogueSprite(resolved);
            }

            ApplyInfoPanel(choicePanelStyle, resolved);
            ApplyInfoPanel(rewardPanelStyle, resolved);
            GameUiStyle.ApplyPixelText(titleText, resolved, GameUiTextStyle.Subtitle);
            GameUiStyle.ApplyPixelText(bodyText, resolved, GameUiTextStyle.Body);
            GameUiStyle.ApplyPixelText(choiceText, resolved, GameUiTextStyle.ButtonText, true);
            GameUiStyle.ApplyPixelText(rewardText, resolved, GameUiTextStyle.ButtonText, true);
            ApplyIcon(choiceIcon, resolved.confirmIcon, resolved.success);
            ApplyIcon(rewardIcon, resolved.inventoryIcon == null ? resolved.moneyIcon : resolved.inventoryIcon, resolved.gold);
            ApplyButton(closeButton);
        }

        private void ApplyDialogueSprite(UIThemeConfig resolved)
        {
            Image background = panelStyle == null ? null : panelStyle.background;
            if (background == null || resolved.dialoguePanelSprite == null)
            {
                return;
            }

            background.sprite = resolved.dialoguePanelSprite;
            background.type = Image.Type.Sliced;
            GameUiStyle.ApplyPanelSprite(background, resolved.dialoguePanelSprite, resolved.panelBackground);
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

        private void ApplyEventIcon(GameEventData eventData)
        {
            if (eventIcon == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            ApplyIcon(eventIcon, eventData.icon != null ? eventData.icon : resolved.relationIcon, resolved.bodyText);
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

        private static string FormatBody(GameEventData eventData)
        {
            string body = eventData == null || string.IsNullOrEmpty(eventData.description)
                ? "事件已经触发。"
                : eventData.description;
            return WrapValue(body);
        }

        private static string FormatChoice(EventChoiceData selectedChoice)
        {
            string choice = selectedChoice == null || string.IsNullOrEmpty(selectedChoice.choiceText)
                ? "系统已完成默认处理。"
                : selectedChoice.choiceText;
            return "行动  " + choice;
        }

        private static string FormatRewards(ActivityResult result)
        {
            string rewards = result == null ? string.Empty : MapInteractionPromptFormatter.BuildRewardPreview(result.rewards);
            return "获得  " + (string.IsNullOrEmpty(rewards) ? "无额外奖励" : rewards);
        }

        private static string ResolveActivityName(ActivityResult result)
        {
            return result == null || result.activityData == null || string.IsNullOrEmpty(result.activityData.displayName)
                ? "事件"
                : result.activityData.displayName;
        }

        private static string WrapValue(string value)
        {
            return "<color=" + HintColor + ">" + value + "</color>";
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
            rect.pivot = new Vector2(0f, 0f);
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
