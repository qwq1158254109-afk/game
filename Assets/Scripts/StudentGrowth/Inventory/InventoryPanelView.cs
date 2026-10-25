using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 背包面板：5×4 网格（20 格/页）+ 翻页 + 右侧详情 + 使用 / 丢弃。
    ///
    /// 2026-10-22 第二版：
    /// ① 视觉统一走 UIThemeConfig —— PixelPanel 底板与描边、PixelButton 按钮、PixelText 字体，
    ///    不再用内置 LegacyRuntime.ttf + 手写色值。
    /// ② 网格支持翻页（每页 20 格），页脚显示「第 n / m 页」。
    /// ③ 打开面板时占用 GameplayInputGate：旧版只拦按键不拦移动，
    ///    面板开着时角色仍会在后面走（RpgPlayerController 只认闸门）。
    /// ④ 丢弃改二次确认（2.5 秒内再点一次才真丢），避免误点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryPanelView : MonoBehaviour
    {
        private const int Columns = 5;
        private const int Rows = 4;
        private const int PageSize = Columns * Rows;

        private const float SlotSize = 92f;
        private const float SlotGap = 10f;
        private const float PanelWidth = 800f;
        private const float PanelHeight = 560f;
        private const float GridLeft = 28f;
        private const float GridTop = 76f;
        private const float DetailLeft = 548f;
        private const float DetailWidth = 224f;
        private const float DetailHeight = 398f;
        private const float FooterTop = 500f;
        private const float DropConfirmSeconds = 2.5f;

        private static InventoryPanelView instance;

        private GameSessionState session;
        private UIThemeConfig theme;

        private GameObject root;
        private readonly List<Button> slots = new List<Button>();
        private readonly List<Image> slotBackgrounds = new List<Image>();
        private readonly List<Image> slotIcons = new List<Image>();
        private readonly List<Text> slotCounts = new List<Text>();

        private Text pageLabel;
        private Text detailName;
        private Text detailMeta;
        private Text detailInfo;
        private Text feedback;
        private Text emptyHint;
        private Button prevButton;
        private Button nextButton;
        private Button useButton;
        private Button dropButton;
        private Text dropLabel;

        private ItemData selected;
        private ItemData dropArmedItem;
        private float dropArmedUntil;
        private int page;
        private bool ownsGate;
        private Action<ItemData> selectionCallback;

        public static InventoryPanelView Instance => instance;

        public bool IsOpen => root != null && root.activeSelf;

        public static InventoryPanelView Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject host = new GameObject("[UI] InventoryPanel");
            instance = host.AddComponent<InventoryPanelView>();
            instance.Build();
            return instance;
        }

        public void Open(GameSessionState state)
        {
            session = state;
            if (root != null)
            {
                root.SetActive(true);
            }

            page = 0;
            selected = null;
            ClearDropArm();
            ClearFeedback();
            AcquireGate();
            Refresh();
        }

        /// <summary>
        /// 选择模式：点任意格子把道具交给回调，而不是「使用」。
        /// 用于对话里的「赠送礼物」——不消耗道具的使用效果，只把物品本身送出去。
        /// </summary>
        public void OpenForSelection(GameSessionState state, Action<ItemData> onPick)
        {
            Open(state);
            selectionCallback = onPick;
        }

        public void Close()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            ClearDropArm();

            // 2026-10-24：还挂着选择回调时，关闭面板 = 玩家取消选择，必须以 null 回调
            // 让调用方收尾（对话送礼流程靠它复位输入锁并回到菜单）。之前直接丢弃回调，
            // 取消送礼后对话面板被 suppressAdvance 锁死，永远点不动。
            // 选中道具的路径在 Close 之前已清空回调，不会双重触发。
            Action<ItemData> pending = selectionCallback;
            selectionCallback = null;
            ReleaseGate();
            pending?.Invoke(null);
        }

        public void Toggle(GameSessionState state)
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open(state);
            }
        }

        private void Update()
        {
            if (root == null || !root.activeSelf || dropArmedItem == null)
            {
                return;
            }

            if (Time.unscaledTime > dropArmedUntil)
            {
                ClearDropArm();
                UpdateDropLabel();
            }
        }

        private void OnDisable()
        {
            ReleaseGate();
        }

        private void OnDestroy()
        {
            InventoryModel.Changed -= Refresh;
            ReleaseGate();
            if (instance == this)
            {
                instance = null;
            }
        }

        // ── 构建 ────────────────────────────────────────────────

        private void Build()
        {
            theme = GameUiStyle.ResolveTheme(null);

            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            RectTransform rootRect = CreateRect("InventoryRoot", transform, Vector2.zero, Vector2.zero);
            Stretch(rootRect);
            root = rootRect.gameObject;

            // 遮罩：挡住 HUD 点击，同时压暗背景；点空白处关闭
            RectTransform backdrop = CreateRect("Backdrop", root.transform, Vector2.zero, Vector2.zero);
            Stretch(backdrop);
            Image backdropImage = backdrop.gameObject.AddComponent<Image>();
            backdropImage.color = new Color(0f, 0f, 0f, 0.55f);
            Button backdropButton = backdrop.gameObject.AddComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;
            backdropButton.onClick.AddListener(Close);

            RectTransform panel = CreateRect("Window", root.transform, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            Image background = panel.gameObject.AddComponent<Image>();
            PixelPanel panelStyle = panel.gameObject.AddComponent<PixelPanel>();
            panelStyle.theme = theme;
            panelStyle.background = background;
            panelStyle.variant = GameUiPanelVariant.Standard;
            panelStyle.createBorder = true;
            panelStyle.ApplyStyle();
            ApplyInventoryPanelSprite(background);

            BuildHeader(panel);
            BuildGrid(panel);
            BuildDetail(panel);
            BuildFooter(panel);

            root.SetActive(false);
            InventoryModel.Changed += Refresh;
        }

        private void BuildHeader(RectTransform panel)
        {
            Image icon = CreateImage("InventoryIcon", panel);
            Place(icon.rectTransform, TopLeft, TopLeft, new Vector2(GridLeft, -20f), new Vector2(26f, 26f));
            icon.sprite = theme.inventoryIcon;
            icon.enabled = theme.inventoryIcon != null;
            icon.raycastTarget = false;

            Text title = CreateLabel(panel, "Title", "背包", GameUiTextStyle.Subtitle, false, TextAnchor.MiddleLeft);
            Place(title.rectTransform, TopLeft, TopLeft, new Vector2(GridLeft + 36f, -16f), new Vector2(220f, 34f));

            Button close = CreateButton(panel, "Close", "关闭", new Vector2(-24f, -18f), new Vector2(76f, 32f), Close);
            Place(close.transform as RectTransform, TopRight, TopRight, new Vector2(-24f, -18f), new Vector2(76f, 32f));
        }

        private void BuildGrid(RectTransform panel)
        {
            float gridWidth = Columns * SlotSize + (Columns - 1) * SlotGap;
            float gridHeight = Rows * SlotSize + (Rows - 1) * SlotGap;

            RectTransform grid = CreateRect("Grid", panel, new Vector2(gridWidth, gridHeight), Vector2.zero);
            Place(grid, TopLeft, TopLeft, new Vector2(GridLeft, -GridTop), new Vector2(gridWidth, gridHeight));

            for (int i = 0; i < PageSize; i++)
            {
                int index = i;
                int col = i % Columns;
                int row = i / Columns;

                RectTransform slot = CreateRect("Slot" + i, grid, new Vector2(SlotSize, SlotSize), Vector2.zero);
                Place(slot, TopLeft, TopLeft,
                    new Vector2(col * (SlotSize + SlotGap), -row * (SlotSize + SlotGap)),
                    new Vector2(SlotSize, SlotSize));

                Image image = slot.gameObject.AddComponent<Image>();
                image.color = theme.panelDarkBackground;

                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => SelectSlot(index));

                Image slotIcon = CreateImage("Icon", slot);
                Place(slotIcon.rectTransform, Center, Center, Vector2.zero, new Vector2(SlotSize - 30f, SlotSize - 30f));
                slotIcon.enabled = false;
                slotIcon.raycastTarget = false;
                slotIcon.preserveAspect = true;

                Text count = CreateLabel(slot, "Count", string.Empty, GameUiTextStyle.SmallText, true, TextAnchor.LowerRight);
                Place(count.rectTransform, BottomRight, BottomRight, new Vector2(-6f, 4f), new Vector2(48f, 20f));

                slots.Add(button);
                slotBackgrounds.Add(image);
                slotIcons.Add(slotIcon);
                slotCounts.Add(count);
            }

            emptyHint = CreateLabel(grid, "Empty", "背包是空的。", GameUiTextStyle.Body, false, TextAnchor.MiddleCenter);
            Place(emptyHint.rectTransform, Center, Center, Vector2.zero, new Vector2(gridWidth - 40f, 40f));
        }

        private void BuildDetail(RectTransform panel)
        {
            RectTransform detail = CreateRect("Detail", panel, new Vector2(DetailWidth, DetailHeight), Vector2.zero);
            Place(detail, TopLeft, TopLeft, new Vector2(DetailLeft, -GridTop), new Vector2(DetailWidth, DetailHeight));

            Image background = detail.gameObject.AddComponent<Image>();
            GameUiStyle.ApplyPanelSprite(background, null, new Color(0f, 0f, 0f, 0.26f));
            background.raycastTarget = false;

            detailName = CreateLabel(detail, "Name", "未选择", GameUiTextStyle.Subtitle, true, TextAnchor.UpperLeft);
            Place(detailName.rectTransform, TopLeft, TopLeft, new Vector2(12f, -10f), new Vector2(DetailWidth - 24f, 32f));

            detailMeta = CreateLabel(detail, "Meta", string.Empty, GameUiTextStyle.SmallText, true, TextAnchor.UpperLeft);
            Place(detailMeta.rectTransform, TopLeft, TopLeft, new Vector2(12f, -48f), new Vector2(DetailWidth - 24f, 62f));

            detailInfo = CreateLabel(detail, "Info", "点上面的格子查看道具。", GameUiTextStyle.SmallText, true, TextAnchor.UpperLeft);
            Place(detailInfo.rectTransform, TopLeft, TopLeft, new Vector2(12f, -118f), new Vector2(DetailWidth - 24f, 150f));

            feedback = CreateLabel(detail, "Feedback", string.Empty, GameUiTextStyle.SmallText, true, TextAnchor.UpperLeft);
            Place(feedback.rectTransform, TopLeft, TopLeft, new Vector2(12f, -272f), new Vector2(DetailWidth - 24f, 30f));

            useButton = CreateButton(detail, "Use", "使用", Vector2.zero, new Vector2(96f, 38f), OnUse);
            Place(useButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(12f, -320f), new Vector2(96f, 38f));

            dropButton = CreateButton(detail, "Drop", "丢弃", Vector2.zero, new Vector2(100f, 38f), OnDrop);
            Place(dropButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(112f, -320f), new Vector2(100f, 38f));
            dropLabel = dropButton.GetComponentInChildren<Text>(true);
        }

        private void BuildFooter(RectTransform panel)
        {
            prevButton = CreateButton(panel, "PrevPage", "‹", Vector2.zero, new Vector2(40f, 32f), () => ChangePage(-1));
            Place(prevButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(GridLeft, -FooterTop), new Vector2(40f, 32f));

            pageLabel = CreateLabel(panel, "PageLabel", "第 1 / 1 页", GameUiTextStyle.SmallText, false, TextAnchor.MiddleCenter);
            Place(pageLabel.rectTransform, TopLeft, TopLeft, new Vector2(GridLeft + 48f, -FooterTop), new Vector2(120f, 32f));

            nextButton = CreateButton(panel, "NextPage", "›", Vector2.zero, new Vector2(40f, 32f), () => ChangePage(1));
            Place(nextButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(GridLeft + 176f, -FooterTop), new Vector2(40f, 32f));

            Text hint = CreateLabel(panel, "FooterHint", "点击道具使用 · 6 / B 开关 · Esc 关闭", GameUiTextStyle.SmallText, false, TextAnchor.MiddleRight);
            Place(hint.rectTransform, TopRight, TopRight, new Vector2(-24f, -FooterTop), new Vector2(420f, 32f));
        }

        // ── 交互 ────────────────────────────────────────────────

        private void SelectSlot(int index)
        {
            IReadOnlyList<InventoryEntry> entries = InventoryModel.Entries(session);
            int global = page * PageSize + index;
            if (global < 0 || global >= entries.Count)
            {
                return;
            }

            if (selectionCallback != null)
            {
                Action<ItemData> callback = selectionCallback;
                selectionCallback = null;
                ItemData picked = entries[global].item;
                Close();
                callback?.Invoke(picked);
                return;
            }

            selected = entries[global].item;
            ClearDropArm();
            ClearFeedback();
            Sfx.Ui.Click();
            Refresh();
        }

        private void ChangePage(int delta)
        {
            IReadOnlyList<InventoryEntry> entries = InventoryModel.Entries(session);
            int pageCount = Mathf.Max(1, (entries.Count + PageSize - 1) / PageSize);
            int next = Mathf.Clamp(page + delta, 0, pageCount - 1);
            if (next == page)
            {
                return;
            }

            page = next;
            selected = FirstEntryOnPage(entries, page);
            ClearDropArm();
            ClearFeedback();
            Sfx.World.PageFlip();
            Refresh();
        }

        private void OnUse()
        {
            if (selected == null)
            {
                return;
            }

            bool ok = InventoryModel.TryUse(session, selected, out string message);
            if (ok)
            {
                Sfx.Ui.Confirm();
            }
            else
            {
                Sfx.Ui.Error();
            }

            ShowFeedback(message);
            Refresh();
        }

        private void OnDrop()
        {
            if (selected == null)
            {
                return;
            }

            if (dropArmedItem == selected && Time.unscaledTime <= dropArmedUntil)
            {
                bool removed = InventoryModel.Drop(session, selected);
                Sfx.Ui.Back();
                ClearDropArm();
                if (removed)
                {
                    selected = null;
                }

                ShowFeedback(removed ? "已丢弃一件。" : "丢弃失败。");
                Refresh();
                return;
            }

            dropArmedItem = selected;
            dropArmedUntil = Time.unscaledTime + DropConfirmSeconds;
            UpdateDropLabel();
            Sfx.Ui.Click();
        }

        private void UpdateDropLabel()
        {
            if (dropLabel != null)
            {
                dropLabel.text = dropArmedItem != null ? "确认丢弃" : "丢弃";
            }
        }

        private void ClearDropArm()
        {
            dropArmedItem = null;
            dropArmedUntil = 0f;
            UpdateDropLabel();
        }

        private void ShowFeedback(string message)
        {
            if (feedback != null)
            {
                feedback.text = message ?? string.Empty;
            }
        }

        private void ClearFeedback()
        {
            ShowFeedback(string.Empty);
        }

        // ── 刷新 ────────────────────────────────────────────────

        private void Refresh()
        {
            if (root == null || !root.activeSelf)
            {
                return;
            }

            IReadOnlyList<InventoryEntry> entries = InventoryModel.Entries(session);
            int pageCount = Mathf.Max(1, (entries.Count + PageSize - 1) / PageSize);
            page = Mathf.Clamp(page, 0, pageCount - 1);

            if (selected != null && InventoryModel.CountOf(session, selected) == 0)
            {
                selected = null;
            }

            if (selected == null && entries.Count > 0)
            {
                selected = FirstEntryOnPage(entries, page);
                if (selected == null)
                {
                    selected = entries[0].item;
                }
            }

            for (int i = 0; i < PageSize; i++)
            {
                int global = page * PageSize + i;
                bool hasItem = global < entries.Count;
                InventoryEntry entry = hasItem ? entries[global] : default;
                ItemData entryItem = hasItem ? entry.item : null;
                Sprite icon = ResolveIcon(entryItem);
                bool fallback = entryItem != null && entryItem.icon == null;

                slotIcons[i].enabled = icon != null;
                slotIcons[i].sprite = icon;
                slotIcons[i].color = fallback ? TintFor(entryItem) : Color.white;
                slotCounts[i].text = hasItem && entry.count > 1 ? entry.count.ToString() : string.Empty;
                slots[i].interactable = hasItem;
                ApplySlotVisual(i, hasItem, hasItem && entry.item == selected);
            }

            emptyHint.enabled = entries.Count == 0;
            pageLabel.text = "第 " + (page + 1) + " / " + pageCount + " 页";
            prevButton.interactable = page > 0;
            nextButton.interactable = page < pageCount - 1;

            bool hasSelection = selected != null;
            detailName.text = hasSelection ? DisplayName(selected) : "未选择";
            detailMeta.text = hasSelection ? BuildMeta(selected) : string.Empty;
            detailInfo.text = hasSelection ? BuildDetail(selected) : "点上面的格子查看道具。";
            useButton.interactable = hasSelection && CanUseOnMap(selected);
            dropButton.interactable = hasSelection;
        }

        private static ItemData FirstEntryOnPage(IReadOnlyList<InventoryEntry> entries, int pageIndex)
        {
            int start = Mathf.Max(0, pageIndex) * PageSize;
            int end = Mathf.Min(entries.Count, start + PageSize);
            for (int i = start; i < end; i++)
            {
                if (entries[i].IsValid)
                {
                    return entries[i].item;
                }
            }

            return null;
        }

        private void ApplySlotVisual(int index, bool hasItem, bool isSelected)
        {
            Image background = slotBackgrounds[index];
            if (theme.quickbarSlotSprite != null)
            {
                background.sprite = theme.quickbarSlotSprite;
                background.type = Image.Type.Sliced;
                background.color = isSelected
                    ? theme.buttonHover
                    : hasItem ? Color.white : new Color(1f, 1f, 1f, 0.55f);
            }
            else
            {
                background.sprite = GameUiStyle.GetSolidSprite();
                background.type = Image.Type.Simple;
                background.color = isSelected
                    ? theme.buttonSelected
                    : hasItem ? theme.panelDarkBackground : new Color(0f, 0f, 0f, 0.35f);
            }
        }

        private string BuildMeta(ItemData item)
        {
            int count = InventoryModel.CountOf(session, item);
            return "类型：" + TypeName(item.itemType) + "\n数量：" + count + "\n价格：" + item.price;
        }

        private string BuildDetail(ItemData item)
        {
            return string.IsNullOrEmpty(item.description) ? "（无描述）" : item.description;
        }

        private static string TypeName(ItemType type)
        {
            switch (type)
            {
                case ItemType.Consumable:
                    return "消耗品";
                case ItemType.Passive:
                    return "被动";
                case ItemType.StageOnly:
                    return "阶段限定";
                default:
                    return type.ToString();
            }
        }

        /// <summary>
        /// 运行时生成的道具（商店默认 4 件）没有 icon 资产，只有 imagePath。
        /// 不兜底的话背包格子全是空的 —— 这里按类型借主题图标，再用稳定色相区分同款。
        /// </summary>
        private Sprite ResolveIcon(ItemData item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.icon != null)
            {
                return item.icon;
            }

            return item.itemType == ItemType.Passive ? theme.cardsIcon : theme.inventoryIcon;
        }

        /// <summary>同一道具永远得到同一个颜色，让共用的兜底图标也能一眼区分。</summary>
        private static Color TintFor(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId))
            {
                return Color.white;
            }

            int hash = 0;
            string key = item.itemId;
            for (int i = 0; i < key.Length; i++)
            {
                hash = unchecked(hash * 31 + key[i]);
            }

            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.95f);
        }

        private static bool CanUseOnMap(ItemData item)
        {
            return item != null && (item.itemType == ItemType.Consumable || item.canUseInStageScreen);
        }

        private static string DisplayName(ItemData item)
        {
            if (item == null)
            {
                return "未知道具";
            }

            return string.IsNullOrEmpty(item.displayName) ? item.itemId : item.displayName;
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

        private void ApplyInventoryPanelSprite(Image background)
        {
            Sprite sprite = theme.inventoryPanelSprite != null ? theme.inventoryPanelSprite : theme.panelSprite;
            GameUiStyle.ApplyPanelSprite(background, sprite, theme.panelBackground);
        }

        // ── 小工具 ────────────────────────────────────────────────

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

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

        private static Image CreateImage(string name, Transform parent)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            return rect.gameObject.AddComponent<Image>();
        }

        private Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
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

            button.onClick.AddListener(onClick);
            return button;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rect = go.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = Center;
            rect.anchorMax = Center;
            rect.pivot = Center;
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
            rect.pivot = Center;
        }
    }
}
