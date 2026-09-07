using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class MapInteractionConfirmView : MonoBehaviour
    {
        public UIThemeConfig theme;
        public GameObject panelRoot;
        public PixelPanel panelStyle;
        public CanvasGroup canvasGroup;
        public Image actionIcon;
        public Text titleText;
        public Text bodyText;
        public Button confirmButton;
        public Button cancelButton;
        public Image confirmButtonIcon;
        public Image cancelButtonIcon;

        private UnityAction confirmAction;
        private UnityAction cancelAction;
        private Button battleButton;
        private Button strategyButton;
        private float basePanelHeight,baseBodyHeight;
        public void SetStrategyOption(UnityAction action)
        {
            if(strategyButton==null&&action!=null)
            {
                basePanelHeight=(panelRoot.transform as RectTransform).sizeDelta.y;
                baseBodyHeight=bodyText==null?140:bodyText.rectTransform.sizeDelta.y;
                strategyButton=CreateButton("DeskStrategyBattle",panelRoot.transform as RectTransform,"策略练习 · 1 时间",out var icon);
                SetRect(strategyButton.transform as RectTransform,new Vector2(28,66),new Vector2(260,36),Vector2.zero);
            }
            if(strategyButton==null)return;
            strategyButton.gameObject.SetActive(action!=null);strategyButton.onClick.RemoveAllListeners();
            if(action!=null)strategyButton.onClick.AddListener(action);
            var panel=panelRoot.transform as RectTransform;
            panel.sizeDelta=new Vector2(panel.sizeDelta.x,basePanelHeight+(action==null?0:140));
            if(bodyText!=null)bodyText.rectTransform.sizeDelta=new Vector2(bodyText.rectTransform.sizeDelta.x,baseBodyHeight+(action==null?0:96));
        }
        public void SetBattleOption(UnityAction action)
        {
            if(battleButton==null && action!=null)
            {
                battleButton=CreateButton("DeskBattle",panelRoot.transform as RectTransform,"习题挑战 · 1 时间",out var icon);
                SetRect(battleButton.transform as RectTransform,new Vector2(28,24),new Vector2(260,36),Vector2.zero);
            }
            if(battleButton==null)return;
            battleButton.gameObject.SetActive(action!=null);battleButton.onClick.RemoveAllListeners();
            if(action!=null)battleButton.onClick.AddListener(action);
        }

        private void Awake()
        {
            ResolveReferences();
            // The owning flow/factory sets initial visibility. Awake may run inside Show()
            // when a serialized inactive panel is first activated; hiding here cancels that Show.
        }

        private void OnEnable()
        {
            ApplyStyle();
        }

        public void BuildDefaultLayout(RectTransform parent)
        {
            if (parent == null || titleText != null && bodyText != null)
            {
                return;
            }

            panelRoot = panelRoot == null ? gameObject : panelRoot;
            panelRoot.name = "MapInteractionConfirmView";

            RectTransform panelRect = panelRoot.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = new Vector2(0f, -8f);
            panelRect.sizeDelta = new Vector2(560f, 286f);

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

            actionIcon = CreateIcon("ActionIcon", panelRect);
            SetRect(actionIcon.rectTransform, new Vector2(24f, -24f), new Vector2(32f, 32f), new Vector2(0f, 1f));

            titleText = CreateText("Title", panelRect, string.Empty, 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(titleText.rectTransform, new Vector2(68f, -22f), new Vector2(464f, 36f), new Vector2(0f, 1f));

            bodyText = CreateText("Body", panelRect, string.Empty, 18, FontStyle.Normal, TextAnchor.UpperLeft);
            bodyText.supportRichText = true;
            bodyText.lineSpacing = 1.05f;
            SetRect(bodyText.rectTransform, new Vector2(28f, -70f), new Vector2(504f, 140f), new Vector2(0f, 1f));

            confirmButton = CreateButton("ConfirmButton", panelRect, "确认", out confirmButtonIcon);
            cancelButton = CreateButton("CancelButton", panelRect, "取消", out cancelButtonIcon);
            SetRect(confirmButton.transform as RectTransform, new Vector2(344f, 24f), new Vector2(88f, 36f), Vector2.zero);
            SetRect(cancelButton.transform as RectTransform, new Vector2(444f, 24f), new Vector2(88f, 36f), Vector2.zero);

            ApplyStyle();
            Hide();
        }

        public void BindActions(UnityAction onConfirm, UnityAction onCancel)
        {
            BindButtonAction(confirmButton, ref confirmAction, onConfirm);
            BindButtonAction(cancelButton, ref cancelAction, onCancel);
        }

        public void Show(string title, string body)
        {
            ResolveReferences();
            SetText(titleText, title);
            SetText(bodyText, body);
            SetVisible(true);
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
                if (panelStyle.background != null)
                {
                    GameUiStyle.ApplyPanelSprite(panelStyle.background, resolved.menuWindowSprite, GameUiStyle.ResolvePanelColor(resolved, GameUiPanelVariant.Standard));
                }
            }

            GameUiStyle.ApplyPixelText(titleText, resolved, GameUiTextStyle.Subtitle);
            GameUiStyle.ApplyPixelText(bodyText, resolved, GameUiTextStyle.Body);
            ApplyButton(confirmButton, confirmButtonIcon, resolved.confirmIcon);
            ApplyButton(cancelButton, cancelButtonIcon, resolved.cancelIcon);
            ApplyIcon(actionIcon, resolved.confirmIcon, resolved.success);
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

        private void ApplyButton(Button button, Image icon, Sprite iconSprite)
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
            }
            else
            {
                GameUiStyle.ApplyButton(button, button.targetGraphic as Image, theme);
                GameUiStyle.ApplyPixelText(button.GetComponentInChildren<Text>(true), theme, GameUiTextStyle.ButtonText, true);
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            ApplyIcon(icon, iconSprite, resolved.inverseText);
        }

        private static void BindButtonAction(Button button, ref UnityAction previousAction, UnityAction nextAction)
        {
            if (button == null)
            {
                previousAction = nextAction;
                return;
            }

            if (previousAction != null)
            {
                button.onClick.RemoveListener(previousAction);
            }

            previousAction = nextAction;
            if (previousAction != null)
            {
                button.onClick.AddListener(previousAction);
            }
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

        private Button CreateButton(string name, RectTransform parent, string label, out Image icon)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = buttonObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            icon = CreateIcon("Icon", rect);
            RectTransform iconRect = icon.transform as RectTransform;
            iconRect.anchorMin = new Vector2(0f, 0.5f);
            iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(10f, 0f);
            iconRect.sizeDelta = new Vector2(16f, 16f);

            Text text = CreateText("Text", rect, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(28f, 0f);
            text.rectTransform.offsetMax = new Vector2(-8f, 0f);

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
