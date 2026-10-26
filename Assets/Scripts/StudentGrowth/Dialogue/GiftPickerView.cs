using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 送礼选择条：对话里点「赠送礼物」后，在对话框上方原位弹出。
    ///
    /// 2026-10-26：替代原来的「打开完整背包窗口」方案。
    /// 旧做法复用的是 InventoryPanelView（800×560 居中，占 y 80~640），
    /// 而对话框是 1200×340 贴底、选项区从 y 424 往上长，两者几何上必然重叠，
    /// 实测界面糊成一坨（背包压对话、选项区冲出屏幕）。
    /// 现在改成宽度对齐原选项卡片（820）的紧凑条，只列可赠送道具，
    /// 一屏一个焦点，不再需要遮罩或跳转。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GiftPickerView : MonoBehaviour
    {
        private const int Columns = 5;
        private const int Rows = 2;
        private const int PageSize = Columns * Rows;

        private const float SlotSize = 84f;
        private const float SlotGap = 14f;
        private const float PanelWidth = 820f;
        private const float PanelHeight = 268f;

        /// <summary>与 DialogueView 的 optionsRoot 同一条底边（PanelBottom 60 + PanelHeight 340 + 24）。</summary>
        private const float PanelBottom = 424f;

        private const float GridLeft = 20f;
        private const float GridTop = 64f;
        private const float DetailLeft = 536f;
        private const float DetailWidth = 264f;

        private static GiftPickerView instance;

        private GameSessionState session;
        private UIThemeConfig theme;
        private Action<ItemData> pickCallback;
        private Action cancelCallback;

        private GameObject root;
        private readonly List<Button> slots = new List<Button>();
        private readonly List<Image> slotBackgrounds = new List<Image>();
        private readonly List<Image> slotIcons = new List<Image>();
        private readonly List<Text> slotCounts = new List<Text>();

        private Text pageLabel;
        private Text detailName;
        private Text detailMeta;
        private Text detailInfo;
        private Text emptyHint;
        private Button prevButton;
        private Button nextButton;
        private Button giftButton;

        private ItemData selected;
        private int page;

        private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 Center = new Vector2(0.5f, 0.5f);
        private static readonly Vector2 BottomRight = new Vector2(1f, 0f);

        public bool IsOpen => root != null && root.activeSelf;

        public static GiftPickerView Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            GameObject host = new GameObject("[UI] GiftPicker");
            instance = host.AddComponent<GiftPickerView>();
            instance.Build();
            return instance;
        }

        /// <summary>打开选择条。onPick 收到选中的道具，onCancel 表示玩家放弃送礼。</summary>
        public void Open(GameSessionState state, Action<ItemData> onPick, Action onCancel)
        {
            session = state;
            pickCallback = onPick;
            cancelCallback = onCancel;
            page = 0;
            selected = null;

            if (root == null)
            {
                Build();
            }

            root.SetActive(true);
            SelectFirstOnPage();
            Refresh();
        }

        public void Close()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            pickCallback = null;
            cancelCallback = null;
            selected = null;
        }

        private void OnDestroy()
        {
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
            // 比背包（200）高一层：送礼时若背包残留也不会被压住。
            canvas.sortingOrder = 210;

            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            // 全屏 root 但不带 Image：不挡射线，只有卡片本身接点击。
            RectTransform rootRect = CreateRect("GiftRoot", transform, Vector2.zero, Vector2.zero);
            Stretch(rootRect);
            root = rootRect.gameObject;

            RectTransform panel = CreateRect("Card", root.transform, new Vector2(PanelWidth, PanelHeight), Vector2.zero);
            panel.anchorMin = new Vector2(0.5f, 0f);
            panel.anchorMax = new Vector2(0.5f, 0f);
            panel.pivot = new Vector2(0.5f, 0f);
            panel.anchoredPosition = new Vector2(0f, PanelBottom);

            Image background = panel.gameObject.AddComponent<Image>();
            PixelPanel panelStyle = panel.gameObject.AddComponent<PixelPanel>();
            panelStyle.theme = theme;
            panelStyle.background = background;
            panelStyle.variant = GameUiPanelVariant.Standard;
            panelStyle.createBorder = true;
            panelStyle.ApplyStyle();
            ApplyPanelSprite(background);

            // PixelPanel 在 ResolveReferences 里会把「面板下第一个 Text」认成自己的标题，
            // 每次 OnEnable 都用 Subtitle 样式刷一遍（还会挂 PixelText 反复刷）。
            // 实测「送点什么？」因此被压成 Subtitle 的 24，比正文还小。
            // 这里先塞一个隐藏的占位给它抓，真标题就不会被碰。
            Text panelTitlePlaceholder = CreateLabel(panel, "PanelTitle", string.Empty, GameUiTextStyle.Subtitle, false, TextAnchor.MiddleLeft);
            panelTitlePlaceholder.gameObject.SetActive(false);
            panelStyle.titleText = panelTitlePlaceholder;

            BuildHeader(panel);
            BuildGrid(panel);
            BuildDetail(panel);
            root.SetActive(false);
        }

        private void BuildHeader(RectTransform panel)
        {
            Text title = CreateLabel(panel, "Title", "送点什么？", GameUiTextStyle.Title, false, TextAnchor.MiddleLeft);
            // 字号显式指定：主题 Title 在运行时被 PixelPanel 的标题逻辑压成 Body 的 24。
            // rect 高度必须大于「字号 × 行高」——之前 34 高装 28 号，文字被 Truncate
            // 裁得只剩几道横线（实测截图里标题看着像下划线）。
            title.fontSize = 26;
            Place(title.rectTransform, TopLeft, TopLeft, new Vector2(GridLeft, -20f), new Vector2(300f, 40f));

            // 翻页放标题右侧的空位（标题文字实际只占 134 宽），
            // 不能放卡片右端——那里是详情区，会压住道具名。
            // 文案用「上页/下页」而不是「‹ ›」：像素字体缺这两个字形，会渲染成几道横线。
            prevButton = CreateButton(panel, "PrevPage", "上页", Vector2.zero, new Vector2(76f, 46f), () => ChangePage(-1));
            Place(prevButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(330f, -16f), new Vector2(76f, 46f));

            pageLabel = CreateLabel(panel, "PageLabel", string.Empty, GameUiTextStyle.SmallText, false, TextAnchor.MiddleCenter);
            Place(pageLabel.rectTransform, TopLeft, TopLeft, new Vector2(410f, -16f), new Vector2(40f, 46f));

            nextButton = CreateButton(panel, "NextPage", "下页", Vector2.zero, new Vector2(76f, 46f), () => ChangePage(1));
            Place(nextButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(454f, -16f), new Vector2(76f, 46f));


        }

        private void BuildGrid(RectTransform panel)
        {
            for (int i = 0; i < PageSize; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                Vector2 pos = new Vector2(
                    GridLeft + col * (SlotSize + SlotGap),
                    -GridTop - row * (SlotSize + SlotGap));

                RectTransform slot = CreateRect("Slot" + i, panel, new Vector2(SlotSize, SlotSize), Vector2.zero);
                Place(slot, TopLeft, TopLeft, pos, new Vector2(SlotSize, SlotSize));

                Image slotBackground = slot.gameObject.AddComponent<Image>();
                slotBackgrounds.Add(slotBackground);

                Button button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slotBackground;
                // Button 默认 ColorTint 会把 targetGraphic.color 刷成 normalColor（白），
                // 手动设的格子底色会被抹掉——背包那边同样显式关掉（InventoryPanelView:259）。
                button.transition = Selectable.Transition.None;
                int captured = i;
                button.onClick.AddListener(() => OnSlotClicked(captured));
                slots.Add(button);

                Image icon = CreateImage("Icon", slot);
                Place(icon.rectTransform, Center, Center, Vector2.zero, new Vector2(SlotSize - 24f, SlotSize - 24f));
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                slotIcons.Add(icon);

                Text count = CreateLabel(slot, "Count", string.Empty, GameUiTextStyle.SmallText, false, TextAnchor.LowerRight);
                Place(count.rectTransform, BottomRight, BottomRight, new Vector2(-4f, 2f), new Vector2(40f, 18f));
                slotCounts.Add(count);
            }

            emptyHint = CreateLabel(panel, "EmptyHint", "身上没有可以送出的东西。", GameUiTextStyle.Body, false, TextAnchor.MiddleCenter);
            Place(emptyHint.rectTransform, TopLeft, TopLeft, new Vector2(GridLeft, -GridTop), new Vector2(Columns * SlotSize + (Columns - 1) * SlotGap, Rows * SlotSize + SlotGap));
            emptyHint.gameObject.SetActive(false);
        }

        private void BuildDetail(RectTransform panel)
        {
            detailName = CreateLabel(panel, "DetailName", string.Empty, GameUiTextStyle.Subtitle, false, TextAnchor.UpperLeft);
            detailName.fontSize = 22;
            Place(detailName.rectTransform, TopLeft, TopLeft, new Vector2(DetailLeft, -22f), new Vector2(DetailWidth, 36f));

            detailMeta = CreateLabel(panel, "DetailMeta", string.Empty, GameUiTextStyle.SmallText, false, TextAnchor.UpperLeft);
            detailMeta.fontSize = 13;
            detailMeta.lineSpacing = 1.1f;
            Place(detailMeta.rectTransform, TopLeft, TopLeft, new Vector2(DetailLeft, -62f), new Vector2(DetailWidth, 66f));

            detailInfo = CreateLabel(panel, "DetailInfo", string.Empty, GameUiTextStyle.Body, false, TextAnchor.UpperLeft);
            // 正文压到 16 并收紧行距：276 宽里 24 号会挤成一坨。
            detailInfo.fontSize = 16;
            detailInfo.lineSpacing = 1.1f;
            detailInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailInfo.verticalOverflow = VerticalWrapMode.Truncate;
            Place(detailInfo.rectTransform, TopLeft, TopLeft, new Vector2(DetailLeft, -130f), new Vector2(DetailWidth, 94f));

            giftButton = CreateButton(panel, "Gift", "赠送", Vector2.zero, new Vector2(100f, 36f), OnGift);
            Place(giftButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(DetailLeft, -228f), new Vector2(100f, 36f));

            Button cancel = CreateButton(panel, "Cancel", "取消", Vector2.zero, new Vector2(100f, 36f), OnCancel);
            Place(cancel.transform as RectTransform, TopLeft, TopLeft, new Vector2(DetailLeft + 112f, -228f), new Vector2(100f, 36f));
        }

        // ── 数据 ────────────────────────────────────────────────

        private List<InventoryEntry> CollectGiftable()
        {
            List<InventoryEntry> result = new List<InventoryEntry>();
            if (session == null)
            {
                return result;
            }

            IReadOnlyList<InventoryEntry> entries = InventoryModel.Entries(session);
            for (int i = 0; i < entries.Count; i++)
            {
                InventoryEntry entry = entries[i];
                // StageOnly 是关卡内专用道具，送出去没有意义。
                if (!entry.IsValid || entry.item.itemType == ItemType.StageOnly)
                {
                    continue;
                }

                result.Add(entry);
            }

            return result;
        }

        private int PageCount(List<InventoryEntry> entries)
        {
            return Mathf.Max(1, (entries.Count + PageSize - 1) / PageSize);
        }

        private void SelectFirstOnPage()
        {
            List<InventoryEntry> entries = CollectGiftable();
            int start = page * PageSize;
            selected = start < entries.Count ? entries[start].item : null;
        }

        private void ChangePage(int delta)
        {
            List<InventoryEntry> entries = CollectGiftable();
            int pageCount = PageCount(entries);
            int next = Mathf.Clamp(page + delta, 0, pageCount - 1);
            if (next == page)
            {
                return;
            }

            page = next;
            SelectFirstOnPage();
            Sfx.World.PageFlip();
            Refresh();
        }

        private void OnSlotClicked(int index)
        {
            List<InventoryEntry> entries = CollectGiftable();
            int global = page * PageSize + index;
            if (global < 0 || global >= entries.Count)
            {
                return;
            }

            selected = entries[global].item;
            Sfx.Ui.Click();
            Refresh();
        }

        private void OnGift()
        {
            if (selected == null)
            {
                Sfx.Ui.Error();
                return;
            }

            Action<ItemData> callback = pickCallback;
            ItemData picked = selected;
            Close();
            callback?.Invoke(picked);
        }

        private void OnCancel()
        {
            Action callback = cancelCallback;
            Close();
            Sfx.Ui.Back();
            callback?.Invoke();
        }

        // ── 刷新 ────────────────────────────────────────────────

        private void Refresh()
        {
            List<InventoryEntry> entries = CollectGiftable();
            int pageCount = PageCount(entries);
            page = Mathf.Clamp(page, 0, pageCount - 1);

            for (int i = 0; i < slots.Count; i++)
            {
                int global = page * PageSize + i;
                bool filled = global < entries.Count;
                InventoryEntry entry = filled ? entries[global] : default;

                slots[i].gameObject.SetActive(filled);
                if (!filled)
                {
                    continue;
                }

                slotIcons[i].sprite = entry.item.icon;
                slotIcons[i].color = entry.item.icon != null ? Color.white : FallbackColor(entry.item);
                slotIcons[i].enabled = true;

                bool showCount = entry.count > 1;
                slotCounts[i].text = showCount ? entry.count.ToString() : string.Empty;
                slotCounts[i].gameObject.SetActive(showCount);

                bool isSelected = selected == entry.item;
                ApplySlotStyle(slotBackgrounds[i], slots[i], isSelected);
            }

            bool any = entries.Count > 0;
            emptyHint.gameObject.SetActive(!any);
            giftButton.interactable = selected != null;

            pageLabel.text = pageCount > 1 ? (page + 1) + "/" + pageCount : string.Empty;
            prevButton.gameObject.SetActive(pageCount > 1);
            nextButton.gameObject.SetActive(pageCount > 1);
            prevButton.interactable = page > 0;
            nextButton.interactable = page < pageCount - 1;

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            if (selected == null)
            {
                detailName.text = string.Empty;
                detailMeta.text = string.Empty;
                detailInfo.text = string.Empty;
                return;
            }

            detailName.text = DisplayName(selected);
            detailMeta.text = "类型：" + TypeName(selected.itemType)
                + "\n价格：" + selected.price
                + "\n送出后：心情 +" + GiftMoodBonus(selected);
            detailInfo.text = string.IsNullOrEmpty(selected.description) ? "（没有描述）" : selected.description;
        }

        // ── 小工具 ────────────────────────────────────────────────

        /// <summary>与 DialogueRunner 的送礼结算保持一致，玩家选之前就知道能涨多少。</summary>
        private static int GiftMoodBonus(ItemData item)
        {
            return Mathf.Clamp(2 + item.price / 8, 2, 8);
        }

        private static string DisplayName(ItemData item)
        {
            if (item == null)
            {
                return "未知道具";
            }

            return string.IsNullOrEmpty(item.displayName) ? item.itemId : item.displayName;
        }

        private static string TypeName(ItemType type)
        {
            switch (type)
            {
                case ItemType.Consumable: return "消耗品";
                case ItemType.Passive: return "被动";
                case ItemType.StageOnly: return "关卡专用";
                default: return type.ToString();
            }
        }

        private void ApplySlotStyle(Image background, Button button, bool isSelected)
        {
            // 照背包 ApplySlotVisual 的做法用 quickbarSlotSprite（专用格子贴图）。
            // 之前用 SetSelectedButtonVisual 拿的是 buttonSprite（按钮贴图），
            // Sliced 模式下实测整格不渲染，格子区只剩图标、看着像飘在空中。
            // 直接用纯色块：卡片底色是米黄纸纹，格子再用同色系贴图会糊成一片。
            // 深棕底 + 选中暖黄，边界最清楚。
            background.sprite = GameUiStyle.GetSolidSprite();
            background.type = Image.Type.Simple;
            background.color = isSelected
                ? new Color(0.96f, 0.76f, 0.36f, 1f)
                : new Color(0.33f, 0.28f, 0.23f, 1f);
        }

        private static Color FallbackColor(ItemData item)
        {
            if (item == null || string.IsNullOrEmpty(item.itemId))
            {
                return new Color(0.7f, 0.7f, 0.7f, 1f);
            }

            int hash = 0;
            for (int i = 0; i < item.itemId.Length; i++)
            {
                hash = (hash * 31 + item.itemId[i]) & 0x7fffffff;
            }

            float hue = Mathf.Abs(hash % 360) / 360f;
            return Color.HSVToRGB(hue, 0.45f, 0.95f);
        }

        private void ApplyPanelSprite(Image background)
        {
            Sprite sprite = theme.inventoryPanelSprite != null ? theme.inventoryPanelSprite : theme.panelSprite;
            GameUiStyle.ApplyPanelSprite(background, sprite, theme.panelBackground);
        }

        private Text CreateLabel(Transform parent, string name, string content, GameUiTextStyle style, bool inverse, TextAnchor alignment)
        {
            RectTransform rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            Text text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.alignment = alignment;
            text.raycastTarget = false;
            // 2026-10-26：这里不用 ApplyPixelText。挂上 PixelText 组件后，OnEnable 会用
            // 组件自身的 textStyle 再刷一次字号，实测标题被刷成 Body 的 24（主题 Title 是 36），
            // 表现为「标题比正文还小」。ApplyText 只设字体/字号/颜色，不挂组件，字号稳定。
            GameUiStyle.ApplyText(text, theme, style, inverse);
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
