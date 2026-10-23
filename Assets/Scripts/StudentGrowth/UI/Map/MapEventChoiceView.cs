using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 事件选项面板：交互时先让玩家选，再把选择交给调用方结算。
    ///
    /// 背景：<see cref="ActivityController"/> 原来是「自动取第一个满足条件的选项」，
    /// <see cref="MapDialogueView"/> 只是结果卡，玩家没有选择权。这个面板补上前置选择那一步。
    ///
    /// 复用约定：代码搭建、走 UIThemeConfig 主题、打开时占用 GameplayInputGate。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapEventChoiceView : MonoBehaviour
    {
        private const float PanelWidth = 720f;
        private const float PanelHeight = 460f;
        private const float ButtonHeight = 62f;
        private const float ButtonGap = 10f;

        private static MapEventChoiceView instance;

        private UIThemeConfig theme;
        private GameObject root;
        private RectTransform panelRect;
        private Text titleText;
        private Text bodyText;
        private RectTransform choiceRoot;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<EventChoiceData> current = new List<EventChoiceData>();
        private Action<EventChoiceData> onPick;
        private Action onCancel;
        private bool ownsGate;
        private bool legacyInputBlocked;

        public static MapEventChoiceView Instance => instance;

        public bool IsOpen => root != null && root.activeSelf;

        public static MapEventChoiceView Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject host = new GameObject("[UI] EventChoiceView");
            instance = host.AddComponent<MapEventChoiceView>();
            instance.Build();
            return instance;
        }

        public void Open(string title, string body, IReadOnlyList<EventChoiceData> choices,
            Action<EventChoiceData> pick, Action cancel)
        {
            if (root == null)
            {
                return;
            }

            onPick = pick;
            onCancel = cancel;
            titleText.text = string.IsNullOrEmpty(title) ? "选择做法" : title;
            bodyText.text = body ?? string.Empty;

            ClearChoices();
            current.Clear();
            int count = choices == null ? 0 : choices.Count;
            for (int i = 0; i < count; i++)
            {
                EventChoiceData choice = choices[i];
                if (choice == null)
                {
                    continue;
                }

                current.Add(choice);
                CreateChoiceButton(choice, current.Count - 1);
            }

            FitToChoiceCount(current.Count);
            root.SetActive(true);
            AcquireGate();
        }

        /// <summary>
        /// 面板高度跟着选项数走。
        ///
        /// 原来是固定 460 高、选项区固定 292：4 个选项时最后一个按钮会压到右下角的「取消」上
        /// （选项区底边距面板底只有 8px，而取消按钮占底部 20~56px）。改成按数量算，
        /// 2 个选项时也不会留一大片空白。
        /// </summary>
        private void FitToChoiceCount(int count)
        {
            if (panelRect == null)
            {
                return;
            }

            int safeCount = Mathf.Max(1, count);
            float listHeight = safeCount * ButtonHeight + (safeCount - 1) * ButtonGap;
            // 160 = 标题(58) + 正文(92) + 间距；68 = 底部取消按钮区。
            float panelHeight = 160f + listHeight + 68f;
            panelRect.sizeDelta = new Vector2(PanelWidth, panelHeight);
            if (choiceRoot != null)
            {
                choiceRoot.sizeDelta = new Vector2(PanelWidth - 48f, listHeight);
            }
        }

        public void Close()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            onPick = null;
            onCancel = null;
            current.Clear();
            ReleaseGate();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            if (IsKeyPressed(KeyCode.Escape, "escapeKey"))
            {
                Cancel();
                return;
            }

            // 数字键 1~4 直接选，键盘玩家不必切鼠标。
            for (int i = 0; i < current.Count && i < 9; i++)
            {
                if (IsKeyPressed(KeyCode.Alpha1 + i, "digit" + (i + 1) + "Key"))
                {
                    Pick(current[i]);
                    return;
                }
            }
        }

        /// <summary>工程只启用新 Input System，直接读旧 Input 会每帧抛异常（历史上刷过 33 万条）。</summary>
        private bool IsKeyPressed(KeyCode legacyKey, string inputSystemKey)
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
            object key = keyboard == null ? null : keyboardType.GetProperty(keyPropertyName)?.GetValue(keyboard, null);
            object value = key == null ? null : key.GetType().GetProperty(statePropertyName)?.GetValue(key, null);
            return value is bool pressed && pressed;
        }

        private void Cancel()
        {
            Action cancel = onCancel;
            Close();
            cancel?.Invoke();
        }

        private void OnDestroy()
        {
            ReleaseGate();
            if (instance == this)
            {
                instance = null;
            }
        }

        private void Build()
        {
            theme = GameUiStyle.ResolveTheme(null);
            EnsureEventSystem();

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 210;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = CreateRect("ChoiceRoot", transform, Vector2.zero, Vector2.zero);
            Stretch(rootRect);
            root = rootRect.gameObject;

            RectTransform backdrop = CreateRect("Backdrop", root.transform, Vector2.zero, Vector2.zero);
            Stretch(backdrop);
            Image backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.55f);
            Button backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(Cancel);

            RectTransform panel = CreateRect("Window", root.transform, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            Image background = panel.gameObject.AddComponent<Image>();
            PixelPanel style = panel.gameObject.AddComponent<PixelPanel>();
            style.theme = theme;
            style.background = background;
            style.variant = GameUiPanelVariant.Standard;
            style.createBorder = true;
            style.ApplyStyle();
            GameUiStyle.ApplyPanelSprite(background,
                theme.dialoguePanelSprite != null ? theme.dialoguePanelSprite : theme.panelSprite,
                theme.panelBackground);

            titleText = CreateLabel(panel, "Title", "选择做法", GameUiTextStyle.Subtitle, false, TextAnchor.MiddleLeft);
            Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -18f), new Vector2(PanelWidth - 48f, 34f));

            bodyText = CreateLabel(panel, "Body", string.Empty, GameUiTextStyle.Body, false, TextAnchor.UpperLeft);
            Place(bodyText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -58f), new Vector2(PanelWidth - 48f, 92f));

            choiceRoot = CreateRect("Choices", panel, new Vector2(PanelWidth - 48f, 292f), Vector2.zero);
            Place(choiceRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(24f, -160f), new Vector2(PanelWidth - 48f, 292f));

            Button cancel = CreateButton(panel, "Cancel", "取消", new Vector2(-24f, 20f), new Vector2(96f, 36f), Cancel);
            Place(cancel.transform as RectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 20f), new Vector2(96f, 36f));

            root.SetActive(false);
        }

        /// <summary>
        /// 兜底补 EventSystem（多数地图场景没有，只有 StageHub 自带一个）。
        ///
        /// 模块必须优先挂 InputSystemUIInputModule：工程 `activeInputHandler = 1`（只用新 Input System），
        /// StandaloneInputModule 读旧 `UnityEngine.Input`，在这种配置下每帧抛 InvalidOperationException
        /// （历史上刷过 33 万条）。写法照抄 <see cref="SettingsMenuController"/> 的 fallback。
        /// AssignDefaultActions 不能省——不调的话模块没有 actions 资产，UI 按钮全部点不动。
        /// </summary>
        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() != null)
            {
                return;
            }

            GameObject host = new GameObject("[UI] EventSystem");
            host.AddComponent<UnityEngine.EventSystems.EventSystem>();
            Type moduleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (moduleType != null)
            {
                Component module = host.AddComponent(moduleType);
                moduleType.GetMethod("AssignDefaultActions")?.Invoke(module, null);
            }
            else
            {
                host.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
        }

        private void CreateChoiceButton(EventChoiceData choice, int index)
        {
            string label = string.IsNullOrEmpty(choice.choiceText) ? ("选项 " + (index + 1)) : choice.choiceText;
            label = (index + 1) + ". " + label;
            string reward = BuildRewardLine(choice.rewards);
            if (!string.IsNullOrEmpty(reward))
            {
                label += "\n" + reward;
            }

            Button button = CreateButton(choiceRoot, "Choice" + index, label, Vector2.zero,
                new Vector2(PanelWidth - 48f, ButtonHeight), () => Pick(choice));
            Place(button.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, -index * (ButtonHeight + ButtonGap)),
                new Vector2(PanelWidth - 48f, ButtonHeight));

            Text text = button.GetComponentInChildren<Text>(true);
            if (text != null)
            {
                text.alignment = TextAnchor.MiddleLeft;
                text.lineSpacing = 0.95f;
            }

            spawned.Add(button.gameObject);
        }

        private void Pick(EventChoiceData choice)
        {
            Action<EventChoiceData> pick = onPick;
            Close();
            pick?.Invoke(choice);
        }

        private void ClearChoices()
        {
            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    Destroy(spawned[i]);
                }
            }

            spawned.Clear();
        }

        private static string BuildRewardLine(List<RewardData> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                RewardData reward = rewards[i];
                if (reward == null)
                {
                    continue;
                }

                string text = string.IsNullOrEmpty(reward.description)
                    ? DescribeReward(reward)
                    : reward.description;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" · ");
                }

                builder.Append(text);
            }

            return builder.ToString();
        }

        private static string DescribeReward(RewardData reward)
        {
            switch (reward.rewardType)
            {
                case RewardType.AddPlayerStat:
                    return reward.targetStat + " +" + reward.value;
                case RewardType.ReducePlayerStat:
                    return reward.targetStat + " -" + reward.value;
                case RewardType.ReducePressure:
                    return "压力 -" + reward.value;
                case RewardType.IncreaseMaxEnergy:
                    return "精力上限 +" + reward.value;
                case RewardType.AddMoney:
                    return "金钱 +" + reward.value;
                default:
                    return string.Empty;
            }
        }

        private void AcquireGate()
        {
            if (ownsGate)
            {
                return;
            }

            ownsGate = true;
            GameplayInputGate.Acquire(this);
        }

        private void ReleaseGate()
        {
            if (!ownsGate)
            {
                return;
            }

            ownsGate = false;
            GameplayInputGate.Release(this, true);
        }

        private Text CreateLabel(Transform parent, string name, string content, GameUiTextStyle style, bool inverse, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.alignment = alignment;
            text.raycastTarget = false;
            GameUiStyle.ApplyPixelText(text, theme, style, inverse);
            return text;
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Action onClick)
        {
            RectTransform rect = CreateRect(name, parent, size, pos);
            Image image = rect.gameObject.AddComponent<Image>();
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateLabel(rect, "Label", label, GameUiTextStyle.ButtonText, true, TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);

            PixelButton pixel = rect.gameObject.AddComponent<PixelButton>();
            pixel.theme = theme;
            pixel.background = image;
            pixel.labelText = text;
            pixel.ApplyStyle();

            button.onClick.AddListener(() => onClick());
            return button;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = pos;
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
