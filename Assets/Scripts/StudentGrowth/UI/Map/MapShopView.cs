using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class MapShopView : MonoBehaviour
    {
        private const string HeaderColor = "#42D6A4";
        private const string LabelColor = "#9DA7B3";
        private const string ValueColor = "#E9EDF2";
        private const string HintColor = "#73E7C1";
        // 2026-10-26 商店改版：3 列 x 3 行 = 9 个格子，正好装下全部商品
        // （原来单列 3 行，9 件只显示 3 件，其余永远看不到）。
        private const float StockRowHeight = 100f;
        private const float StockRowSpacing = 10f;
        // 2026-10-26 二次改版：3 列 372 宽塞不下「图标+名称+价格+详情+状态」
        // 五个元素（第一行就占 352，只剩 20px 呼吸），改为 2 列 570 宽。
        // 2*570 + 20 = 1160 正好填满列表区；每页 2x3 = 6 件，靠翻页看剩余。
        private const float StockColumnWidth = 570f;
        private const float StockColumnGap = 20f;
        private const int StockColumns = 2;
        private const int StockPageRows = 3;
        private const int MaxVisibleStockItems = StockColumns * StockPageRows;

        // 卡牌区在道具区下方。面板原高 424 放不下第二块列表，扩到 660；
        // 场景里已序列化的实例不会重跑 BuildDefaultLayout，靠 EnsureLayoutExpanded 幂等修正。
        private const float ShopPanelHeight = 480f;
        private const float CardSectionTitleTop = -362f;
        private const float CardListTop = -386f;
        private const float CardListHeight = 198f;

        public UIThemeConfig theme;
        public GameSessionState sessionState;
        public FullscreenMapHudView hudView;
        public GameObject panelRoot;
        public PixelPanel panelStyle;
        public CanvasGroup canvasGroup;
        public Image shopIcon;
        public Image moneyIcon;
        public Text titleText;
        public Text moneyText;
        public Text descriptionText;
        public Text feedbackText;
        public RectTransform stockListRoot;
        public Button closeButton;

        // 2026-10-26 商店改版：道具与卡牌改用页签切换，共用同一块列表区域。
        private Button tabItemButton;
        private Button tabCardButton;
        private int currentTab;

        // 2026-10-26：每页 2x3 = 6 件，超出靠翻页。道具与卡牌各自记页码。
        private Button prevPageButton;
        private Button nextPageButton;
        private Text pageLabel;
        private int stockPage;
        private int cardPage;
        private bool stockDiagLogged;
        // 2026-10-26：道具/卡牌的图标走 imagePath 按需读盘。Refresh 每次
        // 买卖都会重跑，不缓存就是反复 IO。
        private static readonly Dictionary<string, Sprite> iconCache = new Dictionary<string, Sprite>();

        public List<ItemData> stockItems = new List<ItemData>();
        public List<CardData> shopCards = new List<CardData>();
        public RectTransform cardListRoot;
        public Text cardSectionTitle;

        private readonly ShopPurchaseService purchaseService = new ShopPurchaseService();

        private void Awake()
        {
            ResolveReferences();
            // 不在此 Hide：首次 Show() 激活时会同步触发 Awake，Hide 会吞掉这次显示。
            // 初始不可见由场景序列化 inactive + 工厂 BuildDefaultLayout 末尾的 Hide() 保证。
        }

        private void OnEnable()
        {
            ApplyStyle();
            EnsureCloseButtonBound();
        }

        /// <summary>
        /// 修复（2026-10-16）：与 MapActivityResultView 同源问题——场景序列化实例
        /// 从未经过 BuildDefaultLayout，closeButton.onClick 为空导致按钮点不动。
        /// 每次显示时幂等补绑。
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

        public void Configure(GameSessionState state, FullscreenMapHudView hud)
        {
            sessionState = state;
            hudView = hud;
            EnsureDefaultStock();
            ApplyStyle();
            Refresh();
        }

        public void SetCatalog(IReadOnlyList<ItemData> items)
        {
            stockItems.Clear();
            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i] != null)
                    {
                        stockItems.Add(items[i]);
                    }
                }
            }

            Refresh();
        }

        public void BuildDefaultLayout(RectTransform parent)
        {
            if (parent == null || titleText != null && stockListRoot != null)
            {
                return;
            }

            panelRoot = panelRoot == null ? gameObject : panelRoot;
            panelRoot.name = "MapShopView";

            RectTransform panelRect = panelRoot.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1200f, ShopPanelHeight);

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

            shopIcon = CreateIcon("ShopIcon", panelRect);
            SetRect(shopIcon.rectTransform, new Vector2(24f, -22f), new Vector2(34f, 34f), new Vector2(0f, 1f));

            titleText = CreateText("Title", panelRect, "商店", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(titleText.rectTransform, new Vector2(68f, -20f), new Vector2(250f, 36f), new Vector2(0f, 1f));

            moneyIcon = CreateIcon("MoneyIcon", panelRect);
            SetRect(moneyIcon.rectTransform, new Vector2(958f, -26f), new Vector2(24f, 24f), new Vector2(0f, 1f));

            moneyText = CreateText("Money", panelRect, string.Empty, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(moneyText.rectTransform, new Vector2(988f, -22f), new Vector2(92f, 30f), new Vector2(0f, 1f));

            closeButton = CreateButton("CloseButton", panelRect, "关闭", new Vector2(1096f, -20f), new Vector2(80f, 34f), new Vector2(0f, 1f));
            closeButton.onClick.AddListener(Hide);

            descriptionText = CreateText("Description", panelRect, string.Empty, 17, FontStyle.Normal, TextAnchor.UpperLeft);
            descriptionText.supportRichText = true;
            descriptionText.lineSpacing = 1.05f;
            SetRect(descriptionText.rectTransform, new Vector2(24f, -60f), new Vector2(700f, 32f), new Vector2(0f, 1f));

            tabItemButton = CreateButton("TabItem", panelRect, "道具", new Vector2(740f, -56f), new Vector2(120f, 36f), new Vector2(0f, 1f));
            tabItemButton.onClick.AddListener(() => SetTab(0));
            tabCardButton = CreateButton("TabCard", panelRect, "卡牌", new Vector2(872f, -56f), new Vector2(120f, 36f), new Vector2(0f, 1f));
            tabCardButton.onClick.AddListener(() => SetTab(1));

            GameObject stockListObject = new GameObject("StockList", typeof(RectTransform));
            stockListRoot = stockListObject.transform as RectTransform;
            stockListRoot.SetParent(panelRect, false);
            SetRect(stockListRoot, new Vector2(20f, -98f), new Vector2(1160f, 320f), new Vector2(0f, 1f));

            cardSectionTitle = CreateText("CardSectionTitle", panelRect, "卡牌", 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(cardSectionTitle.rectTransform, new Vector2(24f, CardSectionTitleTop), new Vector2(572f, 22f), new Vector2(0f, 1f));
            cardSectionTitle.gameObject.SetActive(false); // 页签替代了这个标题

            GameObject cardListObject = new GameObject("CardList", typeof(RectTransform));
            cardListRoot = cardListObject.transform as RectTransform;
            cardListRoot.SetParent(panelRect, false);
            SetRect(cardListRoot, new Vector2(20f, -98f), new Vector2(1160f, 320f), new Vector2(0f, 1f));

            feedbackText = CreateText("Feedback", panelRect, string.Empty, 17, FontStyle.Bold, TextAnchor.MiddleLeft);
            feedbackText.supportRichText = true;
            SetRect(feedbackText.rectTransform, new Vector2(20f, 14f), new Vector2(780f, 30f), Vector2.zero);

            // 翻页组固定在反馈行右侧，不占列表空间
            prevPageButton = CreateButton("PrevPage", panelRect, "上页", new Vector2(830f, 12f), new Vector2(76f, 32f), new Vector2(0f, 0f));
            prevPageButton.onClick.AddListener(() => ChangePage(-1));

            pageLabel = CreateText("PageLabel", panelRect, string.Empty, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(pageLabel.rectTransform, new Vector2(916f, 12f), new Vector2(48f, 32f), new Vector2(0f, 0f));

            nextPageButton = CreateButton("NextPage", panelRect, "下页", new Vector2(974f, 12f), new Vector2(76f, 32f), new Vector2(0f, 0f));
            nextPageButton.onClick.AddListener(() => ChangePage(1));

            ApplyStyle();
            SetTab(0);
            Hide();
        }

        public void Show()
        {
            ResolveReferences();
            EnsureLayoutExpanded();
            EnsureDefaultStock();
            SetVisible(true);
            Refresh();
        }

        public void Hide()
        {
            SetVisible(false);
        }

        public void Refresh()
        {
            if (panelRoot == null || !panelRoot.activeSelf)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            SetText(titleText, "商店");
            SetText(moneyText, ResolveMoneyText());
            SetText(descriptionText, BuildDescriptionText());
            ApplyIcon(shopIcon, resolved.inventoryIcon, resolved.bodyText);
            ApplyIcon(moneyIcon, resolved.moneyIcon, resolved.gold);
            RefreshStockList();
            RefreshCardList();
            RefreshTabState();
            RefreshPageState();
            ApplyStyle();
        }

        /// <summary>
        /// 切换道具 / 卡牌页签。两个列表挂在同一块区域（y -110 起、1160x252），
        /// 靠 SetActive 切换，不再像旧版那样上下堆叠。
        /// </summary>
        /// <summary>翻页。页码在各自 Refresh 里做 Clamp，越界会自动收敛。</summary>
        private void ChangePage(int delta)
        {
            if (currentTab == 0)
            {
                stockPage += delta;
            }
            else
            {
                cardPage += delta;
            }

            Refresh();
        }

        /// <summary>按当前页签与商品数决定翻页组的显隐和页码文案。</summary>
        private void RefreshPageState()
        {
            int total = currentTab == 0 ? stockItems.Count : shopCards.Count;
            int pageCount = Mathf.Max(1, (total + MaxVisibleStockItems - 1) / MaxVisibleStockItems);
            int current = Mathf.Clamp(currentTab == 0 ? stockPage : cardPage, 0, pageCount - 1);

            bool needsPaging = pageCount > 1;
            if (prevPageButton != null)
            {
                prevPageButton.gameObject.SetActive(needsPaging);
                prevPageButton.interactable = current > 0;
            }

            if (nextPageButton != null)
            {
                nextPageButton.gameObject.SetActive(needsPaging);
                nextPageButton.interactable = current < pageCount - 1;
            }

            if (pageLabel != null)
            {
                pageLabel.gameObject.SetActive(needsPaging);
                pageLabel.text = needsPaging ? (current + 1) + "/" + pageCount : string.Empty;
            }
        }

        private void SetTab(int tab)
        {
            currentTab = tab;
            if (stockListRoot != null)
            {
                stockListRoot.gameObject.SetActive(tab == 0);
            }

            if (cardListRoot != null)
            {
                cardListRoot.gameObject.SetActive(tab == 1);
            }

            RefreshTabState();
        }

        private void RefreshTabState()
        {
            // 选中态用暖金底 + 深棕字，未选中用深褐底 + 米黄字。
            // 不用 theme 字段是怕主题里没有对应的 button 配色项。
            StyleTab(tabItemButton, currentTab == 0);
            StyleTab(tabCardButton, currentTab == 1);
        }

        private static void StyleTab(Button button, bool active)
        {
            if (button == null)
            {
                return;
            }

            Image background = button.targetGraphic as Image;
            if (background != null)
            {
                background.color = active
                    ? new Color(0.886f, 0.698f, 0.298f, 1f)
                    : new Color(0.427f, 0.243f, 0.141f, 1f);
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = active
                    ? new Color(0.204f, 0.133f, 0.086f, 1f)
                    : new Color(0.941f, 0.851f, 0.647f, 1f);
            }
        }

        private void RefreshStockList()
        {
            ClearStockList();
            if (stockListRoot == null)
            {
                return;
            }

            EnsureDefaultStock();
            if (stockItems.Count == 0)
            {
                Text emptyText = CreateText("Empty", stockListRoot, "今日暂无商品。", 17, FontStyle.Normal, TextAnchor.UpperLeft);
                SetRect(emptyText.rectTransform, Vector2.zero, new Vector2(560f, 30f), new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(emptyText, theme, GameUiTextStyle.Body);
                return;
            }

            // 一次性诊断：只在需要定位图标问题时留下痕迹
            if (!stockDiagLogged && stockItems.Count > 0)
            {
                stockDiagLogged = true;
                ItemData probe = stockItems[0];
                Debug.Log("[MapShop] 诊断 item=" + (probe == null ? "NULL" : probe.itemId)
                    + " path=" + (probe == null ? "-" : (string.IsNullOrEmpty(probe.imagePath) ? "EMPTY" : probe.imagePath))
                    + " icon=" + (probe == null || probe.icon == null ? "NULL" : probe.icon.name)
                    + " 总数=" + stockItems.Count);
            }

            int pageCount = Mathf.Max(1, (stockItems.Count + MaxVisibleStockItems - 1) / MaxVisibleStockItems);
            stockPage = Mathf.Clamp(stockPage, 0, pageCount - 1);
            int start = stockPage * MaxVisibleStockItems;
            int count = Mathf.Min(MaxVisibleStockItems, stockItems.Count - start);
            for (int i = 0; i < count; i++)
            {
                int col = i % StockColumns;
                int row = i / StockColumns;
                Button itemButton = CreateStockButton(stockItems[start + i], start + i);
                RectTransform rect = itemButton.transform as RectTransform;
                SetRect(rect,
                    new Vector2(col * (StockColumnWidth + StockColumnGap), -row * (StockRowHeight + StockRowSpacing)),
                    new Vector2(StockColumnWidth, StockRowHeight),
                    new Vector2(0f, 1f));
            }

            // 翻页后不再需要「还有 N 件未显示」提示
        }

        private Button CreateStockButton(ItemData item, int index)
        {
            Button button = CreateButton("ShopItem_" + index, stockListRoot, ResolveItemName(item), Vector2.zero, new Vector2(StockColumnWidth, StockRowHeight), new Vector2(0f, 1f));
            // 买不起时保持不可点，让 SetSelectedButtonVisual 走 disabled 灰态，
            // 玩家一眼能看出哪些买不了（横条右下也写着「金钱不足」）。
            // 点击反馈交给 feedbackText——下方仍会写原因。
            button.interactable = CanAfford(item);
            ItemData captured = item;
            button.onClick.AddListener(() => Purchase(captured));
            ConfigureStockButton(button, item);
            return button;
        }

        private void ConfigureStockButton(Button button, ItemData item)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);

            // 2026-10-26：所有商品横条强制使用同一张底图。
            // 之前左右两列出现不同外观（右边有内嵌亮边、左边没有），
            // 说明某条分支改过 background.sprite（selected / disabled 都会换图）。
            // 这里统一钉死，不依赖按钮状态。
            ForceStockBackground(button, resolved);

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = ResolveItemName(item);
                label.alignment = TextAnchor.MiddleLeft;
                SetRect(label.rectTransform, new Vector2(76f, -16f), new Vector2(300f, 24f), new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(label, resolved, GameUiTextStyle.ButtonText, true);
            }

            // 第三个参数是染色：原来传 inverseText（深色），图标被整体压暗，
            // 在深棕横条上等于看不见。图标应该保持原色。
            CreateStockIcon("ItemIcon", rect, ResolveItemSprite(item, resolved), new Vector2(16f, -26f), new Vector2(48f, 48f), Color.white);

            Text priceText = CreateText("Price", rect, "$" + ShopPurchaseService.ResolvePrice(item), 15, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(priceText.rectTransform, new Vector2(440f, -16f), new Vector2(100f, 24f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(priceText, resolved, GameUiTextStyle.StatText, true);
            priceText.color = resolved.gold;

            Text detailText = CreateText("Detail", rect, BuildStockDetail(item), 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(detailText.rectTransform, new Vector2(76f, -56f), new Vector2(344f, 20f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(detailText, resolved, GameUiTextStyle.SmallText, true);

            bool affordable = CanAfford(item);
            Text statusText = CreateText("Status", rect, affordable ? "可购买" : "金钱不足", 14, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(statusText.rectTransform, new Vector2(440f, -56f), new Vector2(100f, 20f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(statusText, resolved, GameUiTextStyle.SmallText, true);
            statusText.color = affordable ? resolved.success : resolved.danger;
        }

        private void RefreshCardList()
        {
            ClearCardList();
            if (cardListRoot == null)
            {
                return;
            }

            EnsureDefaultStock();
            if (shopCards.Count == 0)
            {
                Text emptyText = CreateText("CardEmpty", cardListRoot, "今日暂无卡牌出售。", 16, FontStyle.Normal, TextAnchor.UpperLeft);
                SetRect(emptyText.rectTransform, Vector2.zero, new Vector2(560f, 28f), new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(emptyText, theme, GameUiTextStyle.Body);
                return;
            }

            int pageCount = Mathf.Max(1, (shopCards.Count + MaxVisibleStockItems - 1) / MaxVisibleStockItems);
            cardPage = Mathf.Clamp(cardPage, 0, pageCount - 1);
            int start = cardPage * MaxVisibleStockItems;
            int count = Mathf.Min(MaxVisibleStockItems, shopCards.Count - start);
            for (int i = 0; i < count; i++)
            {
                int col = i % StockColumns;
                int row = i / StockColumns;
                Button cardButton = CreateCardButton(shopCards[start + i], start + i);
                RectTransform rect = cardButton.transform as RectTransform;
                SetRect(rect,
                    new Vector2(col * (StockColumnWidth + StockColumnGap), -row * (StockRowHeight + StockRowSpacing)),
                    new Vector2(StockColumnWidth, StockRowHeight),
                    new Vector2(0f, 1f));
            }

            // 翻页后不再需要「还有 N 张未显示」提示
        }

        private Button CreateCardButton(CardData card, int index)
        {
            Button button = CreateButton("ShopCard_" + index, cardListRoot, ResolveCardName(card), Vector2.zero, new Vector2(572f, StockRowHeight), new Vector2(0f, 1f));
            button.interactable = CanAffordCard(card);
            CardData captured = card;
            button.onClick.AddListener(() => PurchaseCard(captured));
            ConfigureCardButton(button, card);
            return button;
        }

        private void ConfigureCardButton(Button button, CardData card)
        {
            if (button == null)
            {
                return;
            }

            RectTransform rect = button.transform as RectTransform;
            if (rect == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            ForceStockBackground(button, resolved);

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = ResolveCardName(card);
                label.alignment = TextAnchor.MiddleLeft;
                SetRect(label.rectTransform, new Vector2(76f, -16f), new Vector2(300f, 24f), new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(label, resolved, GameUiTextStyle.ButtonText, true);
            }

            CreateStockIcon("CardIcon", rect, ResolveCardSprite(card, resolved), new Vector2(16f, -26f), new Vector2(48f, 48f), Color.white);

            Text priceText = CreateText("Price", rect, "$" + ShopPurchaseService.ResolveCardPrice(card), 15, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(priceText.rectTransform, new Vector2(440f, -16f), new Vector2(100f, 24f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(priceText, resolved, GameUiTextStyle.StatText, true);
            priceText.color = resolved.gold;

            Text detailText = CreateText("Detail", rect, BuildCardDetail(card), 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(detailText.rectTransform, new Vector2(76f, -56f), new Vector2(344f, 20f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(detailText, resolved, GameUiTextStyle.SmallText, true);

            bool affordable = CanAffordCard(card);
            Text statusText = CreateText("Status", rect, affordable ? "可购买" : "金钱不足", 14, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(statusText.rectTransform, new Vector2(440f, -56f), new Vector2(100f, 20f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(statusText, resolved, GameUiTextStyle.SmallText, true);
            statusText.color = affordable ? resolved.success : resolved.danger;
        }

        private void PurchaseCard(CardData card)
        {
            ShopCardPurchaseResult result = purchaseService.TryPurchaseCard(card, sessionState);
            SetCardFeedback(result);
            if (result.success)
            {
                Sfx.World.Coin();
            }

            if (result.success && hudView != null)
            {
                hudView.Refresh();
            }

            Refresh();
        }

        private void SetCardFeedback(ShopCardPurchaseResult result)
        {
            if (feedbackText == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            Color color = result != null && result.success ? resolved.success : resolved.danger;
            string hex = ColorUtility.ToHtmlStringRGB(color);
            string message = result == null || string.IsNullOrEmpty(result.message) ? "购买处理失败。" : result.message;
            feedbackText.text = "<color=#" + hex + ">" + message + "</color>";
        }

        private bool CanAffordCard(CardData card)
        {
            if (sessionState == null || sessionState.playerState == null)
            {
                return false;
            }

            return sessionState.playerState.money >= ShopPurchaseService.ResolveCardPrice(card);
        }

        private static string ResolveCardName(CardData card)
        {
            if (card == null)
            {
                return "未命名卡牌";
            }

            if (!string.IsNullOrEmpty(card.displayName))
            {
                return card.displayName;
            }

            return string.IsNullOrEmpty(card.cardId) ? "未命名卡牌" : card.cardId;
        }

        private static string BuildCardDetail(CardData card)
        {
            if (card == null)
            {
                return "卡牌数据缺失";
            }

            string rarity = GetRarityLabel(card.rarity);
            string description = string.IsNullOrEmpty(card.description) ? "暂无说明" : card.description;
            return rarity + " · 费用 " + card.cost + " · " + Truncate(description, 22);
        }

        private static string GetRarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Rare:
                    return "稀有";
                case CardRarity.Uncommon:
                    return "罕见";
                default:
                    return "普通";
            }
        }

        private void ClearCardList()
        {
            if (cardListRoot == null)
            {
                return;
            }

            for (int i = cardListRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = cardListRoot.GetChild(i);
                child.SetParent(null, false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>
        /// 拆掉旧布局并按当前代码重建。用于场景里序列化的历史实例——
        /// BuildDefaultLayout 有「已有 titleText/stockListRoot 就直接返回」的保护，
        /// 不清空引用它永远不会重排。
        /// </summary>
        private void RebuildLayout(RectTransform panelRect)
        {
            // 记住原来的父节点。BuildDefaultLayout(parent) 内部会执行
            // panelRect.SetParent(parent)，如果把它自己传进去就是「自己当自己的父」，
            // Unity 直接报错。
            Transform originalParent = panelRect.parent;

            // 必须用 DestroyImmediate：Destroy 是延迟到帧末销毁，而紧接着就要
            // 重建一套新子节点，同一帧内两套 UI 并存。旧字段仍指向「待销毁」的
            // 组件，任何一次访问都会抛 NullReference。UI 重建开销很小，立即销毁。
            for (int i = panelRect.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(panelRect.GetChild(i).gameObject);
            }

            // 清空全部子节点引用，绕过 BuildDefaultLayout 的 early return，
            // 也避免后续代码拿着已销毁对象继续用。
            titleText = null;
            moneyText = null;
            descriptionText = null;
            feedbackText = null;
            stockListRoot = null;
            cardListRoot = null;
            cardSectionTitle = null;
            tabItemButton = null;
            tabCardButton = null;
            prevPageButton = null;
            nextPageButton = null;
            pageLabel = null;
            closeButton = null;
            shopIcon = null;
            moneyIcon = null;

            panelRect.sizeDelta = new Vector2(1200f, ShopPanelHeight);
            BuildDefaultLayout(originalParent as RectTransform);
        }

        /// <summary>
        /// 幂等把面板撑到含卡牌区的高度，并补齐缺失的卡牌列表节点。
        /// 场景里已序列化的 MapShopView 实例不会再走 BuildDefaultLayout，
        /// 没有这一步就会出现「加了卡牌但界面上看不到」。
        /// </summary>
        private void EnsureLayoutExpanded()
        {
            if (panelRoot == null)
            {
                return;
            }

            RectTransform panelRect = panelRoot.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            // 2026-10-26：三列改版后，场景里序列化的旧实例（620x660 单列布局）
            // 不会走 BuildDefaultLayout，只补高度是修不好的——旧高度 660 反而
            // 比新高度 400 大，连高度判断都过不了。这里按面板宽度判定版本，
            // 不是 1200 就拆掉子节点整体重建。
            if (!Mathf.Approximately(panelRect.sizeDelta.x, 1200f))
            {
                RebuildLayout(panelRect);
                return;
            }

            if (panelRect != null && panelRect.sizeDelta.y < ShopPanelHeight)
            {
                panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, ShopPanelHeight);
            }

            if (cardSectionTitle == null && panelRect != null)
            {
                cardSectionTitle = CreateText("CardSectionTitle", panelRect, "卡牌", 18, FontStyle.Bold, TextAnchor.MiddleLeft);
                SetRect(cardSectionTitle.rectTransform, new Vector2(24f, CardSectionTitleTop), new Vector2(572f, 22f), new Vector2(0f, 1f));
            }

            if (cardListRoot == null && panelRect != null)
            {
                GameObject cardListObject = new GameObject("CardList", typeof(RectTransform));
                cardListRoot = cardListObject.transform as RectTransform;
                cardListRoot.SetParent(panelRect, false);
                SetRect(cardListRoot, new Vector2(24f, CardListTop), new Vector2(572f, CardListHeight), new Vector2(0f, 1f));
            }

            // 关闭按钮也归位：场景里序列化的是旧版小尺寸，右边缘会被面板裁掉一半。
            if (closeButton != null)
            {
                RectTransform closeRect = closeButton.transform as RectTransform;
                SetRect(closeRect, new Vector2(516f, -20f), new Vector2(80f, 34f), new Vector2(0f, 1f));
            }
        }

        private void Purchase(ItemData item)
        {
            Debug.Log("[MapShop] 购买请求 item=" + (item == null ? "NULL" : item.itemId)
                + " 金钱=" + (sessionState != null && sessionState.playerState != null
                    ? sessionState.playerState.GetStat(PlayerStatKey.Money).ToString() : "?"));
            ShopPurchaseResult result = purchaseService.TryPurchase(item, sessionState);
            SetFeedback(result);
            if (result.success)
            {
                Sfx.World.Coin();
            }

            if (result.success && hudView != null)
            {
                hudView.Refresh();
            }

            Refresh();
        }

        private void SetFeedback(ShopPurchaseResult result)
        {
            if (feedbackText == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            Color color = result != null && result.success ? resolved.success : resolved.danger;
            string hex = ColorUtility.ToHtmlStringRGB(color);
            string message = result == null || string.IsNullOrEmpty(result.message) ? "购买处理失败。" : result.message;
            feedbackText.text = "<color=#" + hex + ">" + message + "</color>";
        }

        private void EnsureDefaultStock()
        {
            if (stockItems.Count == 0)
            {
                stockItems.AddRange(MvpSampleDataFactory.CreateDefaultShopItems());
            }

            if (shopCards.Count == 0)
            {
                shopCards.AddRange(MvpSampleDataFactory.CreateDefaultShopCards());
            }
        }

        private bool CanAfford(ItemData item)
        {
            if (sessionState == null || sessionState.playerState == null)
            {
                return false;
            }

            return sessionState.playerState.money >= ShopPurchaseService.ResolvePrice(item);
        }

        private string ResolveMoneyText()
        {
            int money = sessionState == null || sessionState.playerState == null ? 0 : sessionState.playerState.money;
            return money.ToString();
        }

        private string BuildDescriptionText()
        {
            return Section("购买道具与卡牌")
                + Row("说明", "道具进背包，卡牌进主卡组。消耗品在背包中使用，被动道具在战斗时生效。");
        }

        private static string ResolveItemName(ItemData item)
        {
            return item == null || string.IsNullOrEmpty(item.displayName) ? "未命名道具" : item.displayName;
        }

        private static string BuildStockDetail(ItemData item)
        {
            if (item == null)
            {
                return "商品数据缺失";
            }

            string description = string.IsNullOrEmpty(item.description) ? "暂无说明" : item.description;
            return GetItemTypeLabel(item.itemType) + " · " + Truncate(description, 28);
        }

        private static string GetItemTypeLabel(ItemType type)
        {
            switch (type)
            {
                case ItemType.Passive:
                    return "被动";
                case ItemType.StageOnly:
                    return "地图";
                default:
                    return "消耗";
            }
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, Mathf.Max(0, maxLength - 1)) + "…";
        }

        public void ApplyStyle()
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (panelStyle != null)
            {
                panelStyle.theme = resolved;
                panelStyle.variant = GameUiPanelVariant.Standard;
                panelStyle.ApplyStyle();
                ApplyShopSprite(resolved);
            }

            GameUiStyle.ApplyPixelText(titleText, resolved, GameUiTextStyle.Subtitle);
            GameUiStyle.ApplyPixelText(cardSectionTitle, resolved, GameUiTextStyle.Subtitle);
            GameUiStyle.ApplyPixelText(moneyText, resolved, GameUiTextStyle.StatText);
            GameUiStyle.ApplyPixelText(descriptionText, resolved, GameUiTextStyle.Body);
            GameUiStyle.ApplyPixelText(feedbackText, resolved, GameUiTextStyle.Body);
            ApplyButton(closeButton);
            // 关闭按钮同样走纯色底：贴图版在浅色面板上只剩细边，用户反馈「关闭按钮不明显」。
            EnsureRowReadable(closeButton);

            if (stockListRoot != null)
            {
                Button[] buttons = stockListRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    ApplyButton(buttons[i]);
                    EnsureRowReadable(buttons[i]);
                }
            }

            if (cardListRoot != null)
            {
                Button[] cardButtons = cardListRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < cardButtons.Length; i++)
                {
                    ApplyButton(cardButtons[i]);
                    EnsureRowReadable(cardButtons[i]);
                }
            }
        }

        /// <summary>
        /// 商品行按钮的可读性兜底。
        ///
        /// 2026-10-23 用户截图反馈「商品没有图标和名字」。原因：按钮贴图在浅色面板上
        /// Sliced 之后只剩一条细边，而行标题用的是 ButtonText + inverse 的浅色字，
        /// 落在浅底上几乎不可见。这里把行按钮换成纯色底 + 高对比前景，不再依赖贴图。
        /// </summary>
        private void EnsureRowReadable(Button button)
        {
            if (button == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);

            Image background = button.targetGraphic as Image;
            if (background != null)
            {
                background.sprite = null;
                background.type = Image.Type.Simple;
                background.color = resolved.buttonNormal;
            }

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.color = resolved.inverseText;
            }
        }

        private void ApplyShopSprite(UIThemeConfig resolved)
        {
            Image background = panelStyle == null ? null : panelStyle.background;
            if (background == null || resolved.shopPanelSprite == null)
            {
                return;
            }

            background.sprite = resolved.shopPanelSprite;
            background.type = Image.Type.Sliced;
            GameUiStyle.ApplyPanelSprite(background, resolved.shopPanelSprite, resolved.panelBackground);
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

        private void ClearStockList()
        {
            if (stockListRoot == null)
            {
                return;
            }

            for (int i = stockListRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = stockListRoot.GetChild(i);
                child.SetParent(null, false);
                if (Application.isPlaying)
                {
                    Destroy(child.gameObject);
                }
                else
                {
                    DestroyImmediate(child.gameObject);
                }
            }
        }

        private static string Section(string value)
        {
            return "<color=" + HeaderColor + ">■ " + value + "</color>\n";
        }

        private static string Row(string label, string value)
        {
            return "<color=" + LabelColor + ">" + label + "</color>  <color=" + ValueColor + ">" + value + "</color>";
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

        /// <summary>
        /// 解析道具图标：优先 imagePath 读盘，其次 ItemData.icon，再退到
        /// GameArtAssetCatalog 的约定路径，最后用主题通用图标兜底。
        /// 运行时 CreateInstance 出来的道具 icon 字段是 null，只查它必然全空。
        /// </summary>
        private static Sprite ResolveItemSprite(ItemData item, UIThemeConfig resolved)
        {
            if (item == null)
            {
                return resolved.inventoryIcon;
            }

            Sprite loaded = LoadIconSprite(item.imagePath);
            if (loaded != null)
            {
                return loaded;
            }

            if (item.icon != null)
            {
                return item.icon;
            }

            if (!string.IsNullOrEmpty(item.itemId)
                && GameArtAssetCatalog.TryGetItemIconPath(item.itemId, out string catalogPath))
            {
                loaded = LoadIconSprite(catalogPath);
                if (loaded != null)
                {
                    return loaded;
                }
            }

            return resolved.inventoryIcon;
        }

        /// <summary>解析卡牌图标，顺序同道具。</summary>
        private static Sprite ResolveCardSprite(CardData card, UIThemeConfig resolved)
        {
            if (card == null)
            {
                return resolved.inventoryIcon;
            }

            Sprite loaded = LoadIconSprite(card.imagePath);
            if (loaded != null)
            {
                return loaded;
            }

            if (card.icon != null)
            {
                return card.icon;
            }

            if (!string.IsNullOrEmpty(card.cardId)
                && GameArtAssetCatalog.TryGetCardImagePath(card.cardId, out string catalogPath))
            {
                loaded = LoadIconSprite(catalogPath);
                if (loaded != null)
                {
                    return loaded;
                }
            }

            return resolved.inventoryIcon;
        }

        /// <summary>
        /// 从 Assets 目录读 PNG 并建 Sprite，结果进静态缓存。
        /// 与 GiftPickerView.LoadIconSprite 同一套做法；打包后 Assets 不随包
        /// 分发，会回退到通用图标，与项目内既有约定一致。
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
                // Application.dataPath 末尾可能带斜杠，直接用 Path.Combine 会拼出
                // 「Assets\/Art/...」这种混合分隔符，File.Exists 在某些情况下判假。
                // 这里统一成目录分隔符再拼。
                string dataPath = Application.dataPath.TrimEnd('/', '\\');
                string fullPath = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                    ? Path.Combine(dataPath,
                        assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar))
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
                        sprite = Sprite.Create(texture,
                            new Rect(0f, 0f, texture.width, texture.height),
                            Vector2.one * 0.5f, 100f);
                    }
                    else
                    {
                        Destroy(texture);
                    }
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("[MapShop] 读取图标失败：" + assetPath + " / " + exception.Message);
                sprite = null;
            }

            if (sprite == null)
            {
                Debug.LogWarning("[MapShop] 图标为空：" + assetPath);
            }
            else
            {
                // 只缓存成功结果。原实现连 null 一起缓存，一旦某次读取失败
                // （比如面板在资源就绪前就 Refresh 过一次），这个路径就永久坏掉。
                iconCache[assetPath] = sprite;
            }

            return sprite;
        }

        /// <summary>
        /// 把商品横条的底图钉死成主题的 buttonSprite（Sliced 九宫格），
        /// 不随 selected / interactable 状态换图。买不起用整体压暗表示，
        /// 不改底图本身，保证 6 个横条外观一致。
        /// </summary>
        private static void ForceStockBackground(Button button, UIThemeConfig resolved)
        {
            if (button == null)
            {
                return;
            }

            Image background = button.targetGraphic as Image;
            if (background == null)
            {
                return;
            }

            // 优先用 buttonSelectedSprite：用户截图里「带内嵌亮边」的那种外观
            // 来自选中底图，默认的 buttonSprite 少了那圈内框。没有选中底图时
            // 才退回 buttonSprite。
            Sprite skin = resolved.buttonSelectedSprite != null
                ? resolved.buttonSelectedSprite
                : resolved.buttonSprite;
            background.sprite = skin != null ? skin : GameUiStyle.GetSolidSprite();
            background.type = skin != null ? Image.Type.Sliced : Image.Type.Simple;
            background.color = button.interactable
                ? Color.white
                : new Color(0.62f, 0.58f, 0.54f, 1f);
        }

        private static Image CreateStockIcon(string name, RectTransform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            GameObject iconObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = iconObject.transform as RectTransform;
            rect.SetParent(parent, false);
            SetRect(rect, anchoredPosition, sizeDelta, new Vector2(0f, 1f));

            Image image = iconObject.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            if (sprite == null)
            {
                // 商品没有 icon 资源时，原实现直接 Color.clear + enabled=false，
                // 整行看起来像「没有图标」（2026-10-23 用户截图反馈）。
                // 改成半透明占位块：既保留排版位置，也让玩家看出这里本该有个图标。
                image.color = new Color(1f, 1f, 1f, 0.22f);
                image.enabled = true;
            }
            else
            {
                image.color = color;
                image.enabled = true;
            }

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

        private Button CreateButton(string name, RectTransform parent, string label, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchor)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = buttonObject.transform as RectTransform;
            rect.SetParent(parent, false);
            SetRect(rect, anchoredPosition, sizeDelta, anchor);

            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;
            // Button 默认 ColorTint 会接管 image.color：鼠标移开后把颜色刷成
            // normalColor（白），而 PixelButton 设的底色被覆盖，看起来就像
            // 「选中后再也不恢复」。送礼面板同样显式关掉（GiftPickerView:248）。
            button.transition = Selectable.Transition.None;

            Text text = CreateText("Text", rect, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.background = image;
            pixelButton.labelText = text;
            pixelButton.theme = theme;
            // 商品横条只在鼠标悬停时高亮，移开立即恢复。
            // 不加这句会走 selected 语义，点过的横条会一直亮着（2026-10-26 用户反馈）。
            // 2026-10-26 用户确认：商品横条就用主题的 buttonSprite 底图
            // （深棕木牌 + 内嵌亮边，即用户截图里的「图1」）。
            // 不用 solidBackground —— 那会变成纯色块，不是要的样子。
            // 也不用 highlightOnHoverOnly —— 悬停只保留 PixelButton 自带的缩放，
            // 不再切换底图/颜色，避免「悬停后变成另一个样式」。
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
