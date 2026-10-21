using System;
using System.Reflection;
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
        private float fitBasePanelHeight, fitBaseBodyHeight;

        // 键盘接管（2026-10-17 修「用按键确认时面板不消失」）：
        // 面板自己读写按键，并把玩法输入闸门占住（GameplayInputGate.Acquire），
        // 这样同一次 E / Enter 不会被玩家控制器再读一遍去重新打开确认面板。
        private int shownFrame = -1;
        private bool visible;

        /// <summary>
        /// 工程只启用新 Input System（activeInputHandler=1）时读旧 Input 会抛异常。
        /// Pressed 是静态方法且每次按键都会走，标志必须是静态的，
        /// 否则只启用新输入系统时会逐帧重抛、刷出大量报错。
        /// </summary>
        private static bool legacyInputBlocked;
        public void SetStrategyOption(UnityAction action)
        {
            if(strategyButton==null&&action!=null)
            {
                basePanelHeight=(panelRoot.transform as RectTransform).sizeDelta.y;
                baseBodyHeight=bodyText==null?140:bodyText.rectTransform.sizeDelta.y;
                strategyButton=CreateButton("DeskStrategyBattle",panelRoot.transform as RectTransform,"策略练习",out var icon);
                SetRect(strategyButton.transform as RectTransform,new Vector2(28,66),new Vector2(260,36),Vector2.zero);
            }
            if(strategyButton==null)return;
            strategyButton.gameObject.SetActive(action!=null);strategyButton.onClick.RemoveAllListeners();
            if(action!=null)strategyButton.onClick.AddListener(action);
            // 无策略选项时直接返回：不得套用「书桌会话」的尺寸补偿（+140/+96），
            // 否则会把其它活动（如母亲对话）面板的正文区强行缩回书桌的缓存值——正文被截断。
            if(action==null)return;
            var panel=panelRoot.transform as RectTransform;
            panel.sizeDelta=new Vector2(panel.sizeDelta.x,basePanelHeight+(action==null?0:140));
            if(bodyText!=null)bodyText.rectTransform.sizeDelta=new Vector2(bodyText.rectTransform.sizeDelta.x,baseBodyHeight+(action==null?0:96));
        }
        public void SetBattleOption(UnityAction action)
        {
            if(battleButton==null && action!=null)
            {
                battleButton=CreateButton("DeskBattle",panelRoot.transform as RectTransform,"习题挑战",out var icon);
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

        /// <summary>
        /// 兜底释放输入闸门：面板被外部直接关掉（场景卸载 / SetActive(false)）而没走 Hide() 时，
        /// 闸门不能一直占着，否则整个地图的键盘输入都会失效。
        /// </summary>
        private void OnDisable()
        {
            if (!visible)
            {
                return;
            }

            visible = false;
            GameplayInputGate.Release(this, false);
        }

        /// <summary>
        /// 面板可见时自己处理确认 / 取消按键。
        /// 原来的按键判定只在 MapInteractionFlowController.Update 里，且受状态机与输入闸门影响；
        /// 面板一旦因为状态已切走、闸门被占、或玩家控制器同帧再次读键而失去响应，
        /// 就会出现「鼠标点确认能关、键盘确认关不掉（或关了又弹回来）」。
        /// 面板自己持有时，只要它还显示着，按键就一定落到它身上。
        /// </summary>
        private void Update()
        {
            if (!visible)
            {
                return;
            }

            // 打开面板的那一次按键（E）不能在同一帧又当成「确认」。
            if (Time.frameCount <= shownFrame)
            {
                return;
            }

            if (Pressed(KeyCode.Escape, "escapeKey"))
            {
                Invoke(cancelAction);
            }
            else if (Pressed(KeyCode.Return, "enterKey")
                || Pressed(KeyCode.KeypadEnter, "numpadEnterKey")
                || Pressed(KeyCode.E, "eKey"))
            {
                Invoke(confirmAction);
            }
        }

        private static void Invoke(UnityAction action)
        {
            if (action != null)
            {
                action.Invoke();
            }
        }

        private static bool Pressed(KeyCode legacyKey, string inputSystemKey)
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
                    // 工程若只启用新输入系统，Legacy Input 会抛异常；置标志后不再重试，
                    // 直接落到下面的 Input System 通道。
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
            return value is bool pressed && pressed;
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
            FitBodyHeight();
            SetVisible(true);
            ApplyStyle();

            // 占住玩法输入：面板开着时不该再被玩家移动/交互读键。
            visible = true;
            shownFrame = Time.frameCount;
            GameplayInputGate.Acquire(this);
        }

        /// <summary>
        /// 正文区高度随文案自适应：原先固定 140px，行数多时（行动说明+消耗+预期结果+提示）
        /// 下半截被 Truncate 裁掉。这里连同面板一起长高，底边位置不变。
        /// 基线用独立字段记录（不碰 SetStrategyOption 的 base* 字段，避免两者互相覆盖）。
        /// </summary>
        private void FitBodyHeight()
        {
            if (bodyText == null || panelRoot == null)
            {
                return;
            }

            RectTransform panelRect = panelRoot.transform as RectTransform;
            RectTransform bodyRect = bodyText.rectTransform;
            if (panelRect == null || bodyRect == null)
            {
                return;
            }

            if (fitBasePanelHeight <= 0f)
            {
                fitBasePanelHeight = panelRect.sizeDelta.y;
                fitBaseBodyHeight = bodyRect.sizeDelta.y;
            }

            float needed = Mathf.Max(fitBaseBodyHeight, bodyText.preferredHeight + 8f);
            float delta = Mathf.Min(needed - fitBaseBodyHeight, 260f);
            bodyRect.sizeDelta = new Vector2(bodyRect.sizeDelta.x, fitBaseBodyHeight + delta);
            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, fitBasePanelHeight + delta);
        }

        public void Hide()
        {
            bool wasVisible = visible;
            visible = false;
            SetVisible(false);
            // wasVisible 时才 consume：关面板的那次按键不再传给玩家控制器 / 流程，
            // 避免同一次 E 既确认又触发一次交互、把面板立刻重新打开。
            GameplayInputGate.Release(this, wasVisible);
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
