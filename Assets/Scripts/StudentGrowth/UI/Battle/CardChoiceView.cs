using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 批次 1：战后三选一面板（独立新面板）。
    /// 只负责显示与输入，抽卡逻辑在 CardChoiceService，落账由调用方回调执行。
    /// 键位：←/→ 或 A/D 切换，Enter/E 确认，Esc 跳过。
    /// </summary>
    public sealed class CardChoiceView : MonoBehaviour
    {
        private static readonly Color Ink = new Color(.22f, .14f, .08f);
        private static readonly Color Selected = new Color(1f, .84f, .4f);

        /// <summary>
        /// 工程只启用新 Input System（activeInputHandler=1）时读旧 Input 会抛异常。
        /// Pressed 是静态方法、每次按键都会走，所以标志也必须是静态的；
        /// 否则每帧重抛一次，实测会刷出数十万条报错。
        /// </summary>
        private static bool legacyInputBlocked;

        private BattleUIConfig config;
        private List<CardData> offer = new List<CardData>();
        private readonly List<Image> frames = new List<Image>();
        private int selected;
        private Action<CardData> onPick;
        private Action onSkip;
        private bool closed;

        public static CardChoiceView Open(IList<CardData> offer, Action<CardData> onPick, Action onSkip)
        {
            GameObject go = new GameObject("CardChoiceView", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            CardChoiceView view = go.AddComponent<CardChoiceView>();
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 190;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            view.config = Resources.Load<BattleUIConfig>(BattleUIConfig.ResourcePath) ?? BattleUIConfig.CreateRuntimeDefault();
            view.offer = offer == null ? new List<CardData>() : new List<CardData>(offer);
            view.onPick = onPick;
            view.onSkip = onSkip;
            view.Build();
            return view;
        }

        private void Update()
        {
            if (closed)
            {
                return;
            }

            if (Pressed(KeyCode.LeftArrow) || Pressed(KeyCode.A))
            {
                Move(-1);
            }
            else if (Pressed(KeyCode.RightArrow) || Pressed(KeyCode.D))
            {
                Move(1);
            }
            else if (Pressed(KeyCode.Return) || Pressed(KeyCode.KeypadEnter) || Pressed(KeyCode.E))
            {
                Confirm();
            }
            else if (Pressed(KeyCode.Escape))
            {
                Skip();
            }
        }

        private void Build()
        {
            RectTransform root = Rect("CardChoiceRoot", transform, Vector2.zero, new Vector2(1920, 1080));
            Image backdrop = root.gameObject.AddComponent<Image>();
            backdrop.color = new Color(.06f, .05f, .04f, .86f);
            backdrop.raycastTarget = true;

            RectTransform panel = Panel("CardChoicePanel", root, new Vector2(0, 10), new Vector2(1240, 660), "hud_tooltip_panel");
            Label("Title", panel, "选择一张卡加入牌组", new Vector2(0, 262), new Vector2(1100, 60), 34, Ink);
            Label("Hint", panel, "←/→ 切换　Enter/E 确认　Esc 跳过（跳过没有补偿）",
                new Vector2(0, 214), new Vector2(1100, 40), 21, new Color(.42f, .32f, .2f));

            if (offer.Count == 0)
            {
                Label("Empty", panel, "牌组里没有可选的卡。", new Vector2(0, 20), new Vector2(900, 60), 26, Ink);
            }
            else
            {
                float gap = 40f;
                float width = 320f;
                float startX = -(offer.Count - 1) * (width + gap) * .5f;
                for (int i = 0; i < offer.Count; i++)
                {
                    // y=-10：卡牌行顶边 180，避开上方提示行（底边 194），原来 20 会压住提示文字
                    BuildSlot(panel, offer[i], i, new Vector2(startX + i * (width + gap), -10));
                }
            }

            Button("Skip", "跳过", panel, new Vector2(-180, -268), new Vector2(280, 70), Skip);
            Button("Confirm", "确认", panel, new Vector2(180, -268), new Vector2(280, 70), Confirm);
            RefreshSelection();
        }

        private void BuildSlot(RectTransform parent, CardData card, int index, Vector2 position)
        {
            RectTransform slot = Panel("Card_" + index, parent, position, new Vector2(320, 380), null);
            Image frame = slot.GetComponent<Image>();
            frames.Add(frame);

            Button button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            button.onClick.AddListener(() =>
            {
                if (closed)
                {
                    return;
                }

                selected = index;
                RefreshSelection();
            });

            Sprite art = config != null ? config.GetCardArtwork(card) : null;
            if (art != null)
            {
                Image image = Rect("Art", slot, new Vector2(0, 96), new Vector2(260, 150)).gameObject.AddComponent<Image>();
                image.sprite = art;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            Label("Name", slot, string.IsNullOrEmpty(card.displayName) ? card.name : card.displayName,
                new Vector2(0, -34), new Vector2(290, 44), 24, Color.white);
            Label("Cost", slot, "费用 " + card.cost, new Vector2(0, -76), new Vector2(290, 30), 20, Color.yellow);

            string body = string.IsNullOrEmpty(card.description) ? "（无描述）" : card.description;
            string keywords = (card.exhaust ? "\n消耗：用后移出本场" : "") + (card.retain ? "\n保留" : "");
            Label("Body", slot, body + keywords, new Vector2(0, -150), new Vector2(290, 110), 19, Color.white);
        }

        private void Move(int delta)
        {
            if (offer.Count == 0)
            {
                return;
            }

            selected = (selected + delta + offer.Count) % offer.Count;
            RefreshSelection();
        }

        private void RefreshSelection()
        {
            for (int i = 0; i < frames.Count; i++)
            {
                if (frames[i] != null)
                {
                    frames[i].color = i == selected
                        ? Selected
                        : (frames[i].sprite != null ? Color.white : new Color(.94f, .90f, .77f));
                }
            }
        }

        private void Confirm()
        {
            if (closed || offer.Count == 0)
            {
                return;
            }

            int index = Mathf.Clamp(selected, 0, offer.Count - 1);
            CardData picked = offer[index];
            Close();
            if (onPick != null)
            {
                onPick(picked);
            }
        }

        private void Skip()
        {
            if (closed)
            {
                return;
            }

            Close();
            if (onSkip != null)
            {
                onSkip();
            }
        }

        private void Close()
        {
            closed = true;
            Destroy(gameObject);
        }

        private static bool Pressed(KeyCode key)
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetKeyDown(key))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    // 只启用新输入系统时 Legacy Input 会抛异常；置标志后不再重试，落到下面的通道。
                    legacyInputBlocked = true;
                }
            }

            return ReadInputSystemKey(key);
        }

        private static bool ReadInputSystemKey(KeyCode key)
        {
            string property;
            switch (key)
            {
                case KeyCode.Return:
                case KeyCode.KeypadEnter: property = "enterKey"; break;
                case KeyCode.Escape: property = "escapeKey"; break;
                case KeyCode.E: property = "eKey"; break;
                case KeyCode.A: property = "aKey"; break;
                case KeyCode.D: property = "dKey"; break;
                case KeyCode.LeftArrow: property = "leftArrowKey"; break;
                case KeyCode.RightArrow: property = "rightArrowKey"; break;
                default: return false;
            }

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

            object control = keyboardType.GetProperty(property)?.GetValue(keyboard, null);
            if (control == null)
            {
                return false;
            }

            object value = control.GetType().GetProperty("wasPressedThisFrame")?.GetValue(control, null);
            return value is bool pressed && pressed;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, string key)
        {
            RectTransform rect = Rect(name, parent, position, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = key == null ? null : config.GetArtwork(key);
            image.type = Image.Type.Sliced;
            image.color = image.sprite != null ? Color.white : key == null ? new Color(.94f, .90f, .77f) : new Color(.94f, .90f, .77f);
            return rect;
        }

        private Text Label(string name, Transform parent, string value, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            Text label = Rect(name, parent, position, size).gameObject.AddComponent<Text>();
            label.text = value;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            GameUiStyle.ApplyTextFromFontSpec(label, null, fontSize, FontStyle.Normal);
            PixelText pixel = label.GetComponent<PixelText>();
            if (pixel != null)
            {
                pixel.applyOnAwake = false;
            }

            label.fontSize = fontSize;
            label.color = color;
            return label;
        }

        private Button Button(string name, string label, Transform parent, Vector2 position, Vector2 size, Action action)
        {
            RectTransform rect = Panel(name, parent, position, size, null);
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            if (config != null)
            {
                BattleArtStyler.Button(button, config);
            }

            Text text = Label("Label", rect, label, Vector2.zero, size - Vector2.one * 12, 24, Color.white);
            text.raycastTarget = false;
            button.onClick.AddListener(() => action());
            return button;
        }
    }
}
