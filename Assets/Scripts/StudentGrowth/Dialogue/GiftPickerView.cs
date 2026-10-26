using System;
using System.Collections.Generic;
using System.IO;
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
    ///
    /// 2026-10-26 二次修订（尺寸对齐对话选择栏）：
    /// 尺寸改为 820×432，与 DialogueView 的 optionsRoot 完全一致（OptionCardWidth 820，
    /// 4 项时高度 4×(92+12)+16=432），底边同为 y=424，取代原先的 820×268。
    /// **前提修正：CanvasScaler 参考分辨率必须是 1920×1080。**
    /// 之前这里用的是 1280×720，而对话 UI 挂在 1920×1080 的 Canvas 下——
    /// 同一个「424」在两种参考分辨率下对应的屏幕位置差了 20% 屏高，
    /// 所谓「贴在对话框上方原位」实际是悬在屏幕中部偏上，这是「布局很乱」的另一半根因。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GiftPickerView : MonoBehaviour
    {
        private const int Columns = 7;
        private const int Rows = 2;
        private const int PageSize = Columns * Rows;

        private const float SlotSize = 112f;
        private const float SlotGap = 16f;
        private const float PanelWidth = 1200f;
        private const float PanelHeight = 400f;

        /// <summary>
        /// 与 DialogueView 的 optionsRoot 同一条底边（PanelBottom 60 + PanelHeight 340 + 24）。
        /// 注意：只有在两边都是 1920×1080 参考分辨率时这个数值才等价。
        /// </summary>
        private const float PanelBottom = 424f;

        /// <summary>
        /// 图标显示边长。必须是源图 48 的整数倍——48x48 缩放到 64（1.33 倍）
        /// 会出现半像素，边缘发糊；96 = 2 倍，正好铺进 112 的格子并留 8px 边。
        /// </summary>
        private const float IconSize = 96f;

        private const float Padding = 20f;
        private const float GridTop = 64f;
        private const float DetailLeft = 920f;
        private const float DetailWidth = 260f;

        private static GiftPickerView instance;

        /// <summary>
        /// 道具图标缓存。运行时道具由 MvpSampleDataFactory 用代码建，
        /// 只填了 imagePath（磁盘路径），icon（Sprite）恒为 null，
        /// 所以这里要从 PNG 现读，且必须缓存——Refresh 每次选格/翻页都会调。
        /// </summary>
        private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

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
            // 2026-10-26：1280×720 → 1920×1080，与对话 UI 所在 Canvas 统一。
            // 不统一的话，同一套坐标在屏幕上会被放大 1.5 倍（字被裁、位置悬空）。
            scaler.referenceResolution = new Vector2(1920f, 1080f);
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
            // 实测「送点什么？」因此被压成 Subtitle 的字号，比正文还小。
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
            // 字号显式指定：主题 Title 在运行时被 PixelPanel 的标题逻辑压成 Body 的字号。
            // rect 高度必须大于「字号 × 行高」——Unity Text 实际渲染高度约 fontSize×1.3，
            // 26 号至少要 34 高，这里给 44 留余量（之前 40 高装 26 号会裁掉底部）。
            title.fontSize = 26;
            Place(title.rectTransform, TopLeft, TopLeft, new Vector2(Padding, -18f), new Vector2(300f, 44f));

            // 翻页组右对齐到内容右边界（820-20=800），与标题分列两端。
            // 文案用「上页/下页」而不是「‹ ›」：像素字体缺这两个字形，会渲染成几道横线。
            prevButton = CreateButton(panel, "PrevPage", "上页", Vector2.zero, new Vector2(76f, 44f), () => ChangePage(-1));
            Place(prevButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(900f, -16f), new Vector2(76f, 44f));

            pageLabel = CreateLabel(panel, "PageLabel", string.Empty, GameUiTextStyle.SmallText, false, TextAnchor.MiddleCenter);
            // SmallText 是 12 号，在 1920 参考下比正文小一圈，页码容易看不清，提到 18。
            pageLabel.fontSize = 18;
            Place(pageLabel.rectTransform, TopLeft, TopLeft, new Vector2(988f, -16f), new Vector2(44f, 44f));

            nextButton = CreateButton(panel, "NextPage", "下页", Vector2.zero, new Vector2(76f, 44f), () => ChangePage(1));
            Place(nextButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(1040f, -16f), new Vector2(76f, 44f));

            // 曾加过一条分隔线（y=-76），实测距详情标题顶边只有 8px（屏幕像素），
            // 视觉上和标题挤在一起，且纸纹底本身就有横线，加它没有信息价值，已删。
        }

        private void BuildGrid(RectTransform panel)
        {
            float gridWidth = Columns * SlotSize + (Columns - 1) * SlotGap;
            float gridHeight = Rows * SlotSize + (Rows - 1) * SlotGap;

            for (int i = 0; i < PageSize; i++)
            {
                int col = i % Columns;
                int row = i / Columns;
                Vector2 pos = new Vector2(
                    Padding + col * (SlotSize + SlotGap),
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
                Place(icon.rectTransform, Center, Center, Vector2.zero, new Vector2(IconSize, IconSize));
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                // 初始关闭：Image 的 sprite 为 null 且 enabled=true 时会渲染一块白色方块，
                // 在 Refresh 之前会看到 15 个白格。背包同样先关后开（InventoryPanelView:264）。
                icon.enabled = false;
                slotIcons.Add(icon);

                Text count = CreateLabel(slot, "Count", string.Empty, GameUiTextStyle.SmallText, false, TextAnchor.LowerRight);
                Place(count.rectTransform, BottomRight, BottomRight, new Vector2(-6f, 4f), new Vector2(48f, 22f));
                slotCounts.Add(count);
            }

            emptyHint = CreateLabel(panel, "EmptyHint", "身上没有可以送出的东西。", GameUiTextStyle.Body, false, TextAnchor.MiddleCenter);
            Place(emptyHint.rectTransform, TopLeft, TopLeft, new Vector2(Padding, -GridTop), new Vector2(gridWidth, gridHeight));
            emptyHint.gameObject.SetActive(false);
        }

        private void BuildDetail(RectTransform panel)
        {
            detailName = CreateLabel(panel, "DetailName", string.Empty, GameUiTextStyle.Subtitle, false, TextAnchor.UpperLeft);
            detailName.fontSize = 24;
            Place(detailName.rectTransform, TopLeft, TopLeft, new Vector2(DetailLeft, -64f), new Vector2(DetailWidth, 44f));

            detailMeta = CreateLabel(panel, "DetailMeta", string.Empty, GameUiTextStyle.SmallText, false, TextAnchor.UpperLeft);
            detailMeta.fontSize = 18;
            detailMeta.lineSpacing = 1.15f;
            Place(detailMeta.rectTransform, TopLeft, TopLeft, new Vector2(DetailLeft, -116f), new Vector2(DetailWidth, 84f));

            detailInfo = CreateLabel(panel, "DetailInfo", string.Empty, GameUiTextStyle.Body, false, TextAnchor.UpperLeft);
            // 描述做成可换行 + Truncate，高度按「18 号 ≈ 每行 21，5 行」给足 104，
            // 短描述标题不会被裁，长描述也有余量（旧版 94 高装 16 号只够 5 行，实测被裁）。
            detailInfo.fontSize = 18;
            detailInfo.lineSpacing = 1.15f;
            detailInfo.horizontalOverflow = HorizontalWrapMode.Wrap;
            detailInfo.verticalOverflow = VerticalWrapMode.Truncate;
            Place(detailInfo.rectTransform, TopLeft, TopLeft, new Vector2(DetailLeft, -208f), new Vector2(DetailWidth, 80f));

            // 按钮底边 -398，距面板底 432 还有 34：不会再被 3px 边框压住。
            giftButton = CreateButton(panel, "Gift", "赠送", Vector2.zero, new Vector2(124f, 46f), OnGift);
            Place(giftButton.transform as RectTransform, TopLeft, TopLeft, new Vector2(DetailLeft, -330f), new Vector2(124f, 46f));

            Button cancel = CreateButton(panel, "Cancel", "取消", Vector2.zero, new Vector2(124f, 46f), OnCancel);
            Place(cancel.transform as RectTransform, TopLeft, TopLeft, new Vector2(DetailLeft + 132f, -330f), new Vector2(124f, 46f));
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

                Sprite icon = ResolveIcon(entry.item, out Color tint);
                slotIcons[i].sprite = icon;
                slotIcons[i].color = tint;
                // sprite 为 null 时必须关掉，否则会渲染成纯色方块（旧版就是这里恒为 true）。
                slotIcons[i].enabled = icon != null;

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

        // ── 图标 ────────────────────────────────────────────────

        /// <summary>
        /// 取道具图标，并给出应当乘上去的着色。
        ///
        /// 运行时道具（MvpSampleDataFactory 建的）只有 imagePath，icon 恒为 null，
        /// 所以这里按 icon → imagePath → 目录表 → 通用图标 的顺序回退。
        /// 回退到通用图标时按 itemId 加色相，至少能靠颜色区分不同道具。
        /// </summary>
        private Sprite ResolveIcon(ItemData item, out Color tint)
        {
            tint = Color.white;
            if (item == null)
            {
                return null;
            }

            if (item.icon != null)
            {
                return item.icon;
            }

            Sprite loaded = LoadIconSprite(item.imagePath);
            if (loaded == null
                && !string.IsNullOrEmpty(item.itemId)
                && GameArtAssetCatalog.TryGetItemIconPath(item.itemId, out string catalogPath))
            {
                loaded = LoadIconSprite(catalogPath);
            }

            if (loaded != null)
            {
                return loaded;
            }

            tint = FallbackColor(item);
            return theme != null ? theme.inventoryIcon : null;
        }

        /// <summary>
        /// 从 Assets 下的 PNG 现读图标（与 StudentGrowthMvpRuntimeUi.LoadSprite 同一做法）。
        /// 结果进静态缓存：Refresh 在每次选格/翻页都会调用，不缓存会反复读盘。
        /// 注意这是 Editor / 开发期的读法（依赖 Application.dataPath 下真实存在的 Assets 目录），
        /// 打包后 Assets 不随包分发，届时会回退到通用图标——与项目内既有做法保持一致。
        /// </summary>
        private static Sprite LoadIconSprite(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
            {
                return null;
            }

            if (iconCache.TryGetValue(assetPath, out Sprite cached))
            {
                return cached;
            }

            Sprite sprite = null;
            try
            {
                string fullPath = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                    ? Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length))
                    : assetPath;

                if (File.Exists(fullPath))
                {
                    byte[] bytes = File.ReadAllBytes(fullPath);
                    Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point,
                        wrapMode = TextureWrapMode.Clamp
                    };

                    if (texture.LoadImage(bytes))
                    {
                        texture.filterMode = FilterMode.Point;
                        texture.wrapMode = TextureWrapMode.Clamp;
                        sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), Vector2.one * 0.5f, 100f);
                    }
                    else
                    {
                        Destroy(texture);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[GiftPicker] 读取图标失败：" + assetPath + " / " + exception.Message);
                sprite = null;
            }

            iconCache[assetPath] = sprite;
            return sprite;
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

        /// <summary>同一道具永远得到同一个颜色，让兜底图标也能一眼区分（与背包 TintFor 同算法）。</summary>
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
            // 组件自身的 textStyle 再刷一次字号，实测标题被刷成 Body 的字号（主题 Title 更大），
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
