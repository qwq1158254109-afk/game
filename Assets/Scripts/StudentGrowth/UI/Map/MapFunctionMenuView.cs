using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public enum MapFunctionMenuTab
    {
        Status,
        Inventory,
        Deck,
        Settings
    }

    public sealed class MapFunctionMenuView : MonoBehaviour
    {
        private const string HeaderColor = "#42D6A4";
        private const string LabelColor = "#9DA7B3";
        private const string ValueColor = "#E9EDF2";
        private const string HintColor = "#73E7C1";
        private const float ListRowHeight = 44f;
        private const float ListRowSpacing = 4f;
        private const int MaxVisibleListItems = 4;

        public UIThemeConfig theme;
        public FullscreenMapHudView hudView;
        public GameSessionState sessionState;
        public SceneTransitionController sceneTransitionController;
        public GameObject panelRoot;
        public PixelPanel panelStyle;
        public CanvasGroup canvasGroup;
        public Text titleText;
        public Text bodyText;
        public Button statusTabButton;
        public Button inventoryTabButton;
        public Button deckTabButton;
        public Button settingsTabButton;
        public Button closeButton;
        public RectTransform inventoryListRoot;
        public RectTransform deckListRoot;

        private readonly ItemEffectResolver itemEffectResolver = new ItemEffectResolver();
        private MapFunctionMenuTab currentTab;
        private FullscreenMapHudView wiredHudView;
        private string inventoryFeedbackMessage;

        private void Awake()
        {
            ResolveReferences();
            ApplyStyle();
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

        public void Configure(GameSessionState state, SceneTransitionController sceneTransition, FullscreenMapHudView hud)
        {
            sessionState = state;
            sceneTransitionController = sceneTransition;
            hudView = hud;
            WireHudButtons();
            ApplyStyle();
            Refresh();
        }

        public void ShowStatus()
        {
            Show(MapFunctionMenuTab.Status);
        }

        public void ShowInventory()
        {
            Show(MapFunctionMenuTab.Inventory);
        }

        public void ShowDeck()
        {
            Show(MapFunctionMenuTab.Deck);
        }

        public void ShowSettings()
        {
            Hide();
            SettingsMenuController.Instance.Open();
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

            SetText(titleText, GetTitle(currentTab));
            ApplyBodyLayout();
            SetText(bodyText, BuildBody(currentTab));
            ApplyTabSelection();
            RefreshInventoryList();
            RefreshDeckList();
        }

        public void BuildDefaultLayout(RectTransform parent)
        {
            if (parent == null || titleText != null && bodyText != null)
            {
                return;
            }

            GameObject panel = panelRoot == null ? gameObject : panelRoot;
            panel.name = "MapFunctionMenuPanel";
            RectTransform panelRect = panel.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            panelRect.SetParent(parent, false);
            panelRect.anchorMin = new Vector2(1f, 0.5f);
            panelRect.anchorMax = new Vector2(1f, 0.5f);
            panelRect.pivot = new Vector2(1f, 0.5f);
            panelRect.anchoredPosition = new Vector2(-18f, -4f);
            panelRect.sizeDelta = new Vector2(430f, 392f);

            Image image = panel.GetComponent<Image>();
            if (image == null)
            {
                image = panel.AddComponent<Image>();
            }

            image.raycastTarget = true;
            panelStyle = panel.GetComponent<PixelPanel>();
            if (panelStyle == null)
            {
                panelStyle = panel.AddComponent<PixelPanel>();
            }

            panelStyle.background = image;
            panelStyle.variant = GameUiPanelVariant.Dark;
            canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panel);
            panelRoot = panel;

            titleText = CreateText("Title", panelRect, "状态", 22, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(titleText.rectTransform, new Vector2(20f, -16f), new Vector2(250f, 30f), new Vector2(0f, 1f));

            closeButton = CreateButton("CloseButton", panelRect, "X", new Vector2(378f, -16f), new Vector2(32f, 30f), new Vector2(0f, 1f));
            closeButton.onClick.AddListener(Hide);

            statusTabButton = CreateButton("StatusTab", panelRect, "状态", new Vector2(20f, -58f), new Vector2(88f, 32f), new Vector2(0f, 1f));
            inventoryTabButton = CreateButton("InventoryTab", panelRect, "背包", new Vector2(116f, -58f), new Vector2(88f, 32f), new Vector2(0f, 1f));
            deckTabButton = CreateButton("DeckTab", panelRect, "卡组", new Vector2(212f, -58f), new Vector2(88f, 32f), new Vector2(0f, 1f));
            settingsTabButton = CreateButton("SettingsTab", panelRect, "设置", new Vector2(308f, -58f), new Vector2(88f, 32f), new Vector2(0f, 1f));
            statusTabButton.onClick.AddListener(ShowStatus);
            inventoryTabButton.onClick.AddListener(ShowInventory);
            deckTabButton.onClick.AddListener(ShowDeck);
            settingsTabButton.onClick.AddListener(ShowSettings);

            bodyText = CreateText("Body", panelRect, string.Empty, 17, FontStyle.Normal, TextAnchor.UpperLeft);
            SetRect(bodyText.rectTransform, new Vector2(22f, -104f), new Vector2(386f, 246f), new Vector2(0f, 1f));

            GameObject inventoryListObject = new GameObject("InventoryItemList", typeof(RectTransform));
            inventoryListRoot = inventoryListObject.transform as RectTransform;
            inventoryListRoot.SetParent(panelRect, false);
            SetRect(inventoryListRoot, new Vector2(22f, -176f), new Vector2(386f, 170f), new Vector2(0f, 1f));
            inventoryListRoot.gameObject.SetActive(false);

            GameObject deckListObject = new GameObject("DeckCardList", typeof(RectTransform));
            deckListRoot = deckListObject.transform as RectTransform;
            deckListRoot.SetParent(panelRect, false);
            SetRect(deckListRoot, new Vector2(22f, -176f), new Vector2(386f, 170f), new Vector2(0f, 1f));
            deckListRoot.gameObject.SetActive(false);

            ApplyStyle();
            Hide();
        }

        private void Show(MapFunctionMenuTab tab)
        {
            ResolveReferences();
            currentTab = tab;
            SetVisible(true);
            ApplyStyle();
            Refresh();
        }

        private void WireHudButtons()
        {
            if (hudView == null)
            {
                return;
            }

            if (wiredHudView != null && wiredHudView != hudView)
            {
                UnwireHudButtons(wiredHudView);
            }

            wiredHudView = hudView;
            WireButton(hudView.statusButton, ShowStatus);
            WireButton(hudView.inventoryButton, ShowInventory);
            WireButton(hudView.deckButton, ShowDeck);
            WireButton(hudView.settingsButton, ShowSettings);
            hudView.SetOptionalButtonsInteractable(true);
        }

        private void UnwireHudButtons(FullscreenMapHudView hud)
        {
            if (hud == null)
            {
                return;
            }

            UnwireButton(hud.statusButton, ShowStatus);
            UnwireButton(hud.inventoryButton, ShowInventory);
            UnwireButton(hud.deckButton, ShowDeck);
            UnwireButton(hud.settingsButton, ShowSettings);
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
            {
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void UnwireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button != null)
            {
                button.onClick.RemoveListener(action);
            }
        }

        private string BuildBody(MapFunctionMenuTab tab)
        {
            ResolveSessionState();
            switch (tab)
            {
                case MapFunctionMenuTab.Inventory:
                    return BuildInventoryBody();
                case MapFunctionMenuTab.Deck:
                    return BuildDeckBody();
                case MapFunctionMenuTab.Settings:
                    return BuildSettingsBody();
                default:
                    return BuildStatusBody();
            }
        }

        private string BuildStatusBody()
        {
            PlayerState player = sessionState == null ? null : sessionState.playerState;
            StageState stage = sessionState == null ? null : sessionState.stageState;
            StageData stageData = stage == null ? null : stage.currentStage;
            if (player == null)
            {
                return "暂无学生状态数据。";
            }

            StringBuilder builder = new StringBuilder();
            AppendSection(builder, "阶段进度");
            AppendRow(builder, "当前阶段", stageData == null ? "-" : stageData.displayName);
            AppendRow(builder, "剩余时间", stage == null ? "-" : stage.remainingTime.ToString());
            AppendSpacer(builder);

            AppendSection(builder, "学习能力");
            AppendStatLine(builder, ("语文", player.chinese), ("数学", player.math), ("英语", player.english));
            AppendSpacer(builder);

            AppendSection(builder, "生活状态");
            AppendStatLine(builder, ("精力", player.energy + "/" + player.maxEnergy), ("压力", player.pressure), ("心情", player.mood));
            AppendStatLine(builder, ("亲子", player.parentRelation), ("社交", player.social), ("金钱", player.money));
            AppendSpacer(builder);

            AppendSection(builder, "成长倾向");
            AppendStatLine(builder, ("兴趣", player.interest), ("自信", player.confidence), ("自主", player.autonomy));
            return builder.ToString();
        }

        private string BuildInventoryBody()
        {
            IReadOnlyList<ItemData> items = sessionState == null || sessionState.itemInventory == null
                ? null
                : sessionState.itemInventory.ownedItems;
            if (items == null || items.Count == 0)
            {
                return "背包为空。";
            }

            StringBuilder builder = new StringBuilder();
            AppendSection(builder, "持有道具");
            AppendRow(builder, "数量", items.Count.ToString());
            AppendHint(builder, "选择可用道具可直接在地图中使用。");
            if (!string.IsNullOrEmpty(inventoryFeedbackMessage))
            {
                AppendHint(builder, inventoryFeedbackMessage);
            }

            return builder.ToString();
        }

        private string BuildDeckBody()
        {
            IReadOnlyList<CardData> cards = sessionState == null || sessionState.deckState == null
                ? null
                : sessionState.deckState.masterDeck;
            if (cards == null || cards.Count == 0)
            {
                return "卡组为空。";
            }

            StringBuilder builder = new StringBuilder();
            AppendSection(builder, "当前卡组");
            AppendRow(builder, "数量", cards.Count.ToString());
            AppendHint(builder, "卡牌详情会在战斗界面中生效，地图菜单仅作预览。");
            return builder.ToString();
        }

        private string BuildSettingsBody()
        {
            StringBuilder builder = new StringBuilder();
            AppendSection(builder, "基础设置");
            AppendRow(builder, "音量", "默认");
            AppendRow(builder, "画面", "自适应");
            AppendRow(builder, "交互提示", "开启");
            AppendRow(builder, "调试显示", "关闭");
            AppendSpacer(builder);
            AppendHint(builder, "设置页目前只展示状态，后续接入真实设置控制。");
            return builder.ToString();
        }

        private void RefreshInventoryList()
        {
            if (inventoryListRoot == null)
            {
                return;
            }

            bool showInventoryList = currentTab == MapFunctionMenuTab.Inventory
                && panelRoot != null
                && panelRoot.activeSelf;
            inventoryListRoot.gameObject.SetActive(showInventoryList);
            ClearInventoryList();

            if (!showInventoryList)
            {
                return;
            }

            // 旧的道具列表已移除：背包统一由 InventoryPanelView 承载（网格 + 详情 + 使用/丢弃）。
            // 这里只留一个入口，避免两处都能用道具导致状态不同步。
            Button open = CreateButton(
                "OpenInventory",
                inventoryListRoot,
                "打开背包（6）",
                Vector2.zero,
                new Vector2(374f, ListRowHeight),
                new Vector2(0f, 1f));
            ApplyButton(open);
            open.onClick.AddListener(() => InventoryPanelView.Ensure().Open(sessionState));
        }

        private void ConfigureListButton(Button button, string title, string status, string detail, Sprite icon, bool positiveStatus)
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
            Text titleLabel = button.GetComponentInChildren<Text>(true);
            if (titleLabel != null)
            {
                titleLabel.text = title;
                titleLabel.alignment = TextAnchor.MiddleLeft;
                SetRect(titleLabel.rectTransform, new Vector2(46f, -5f), new Vector2(184f, 18f), new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(titleLabel, resolved, GameUiTextStyle.ButtonText, true);
            }

            CreateListIcon("ItemIcon", rect, icon == null ? resolved.inventoryIcon : icon, new Vector2(10f, -8f), new Vector2(28f, 28f), resolved.inverseText);

            Text statusText = CreateText("Status", rect, status, 14, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(statusText.rectTransform, new Vector2(258f, -5f), new Vector2(96f, 18f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(statusText, resolved, GameUiTextStyle.SmallText, true);
            statusText.color = positiveStatus ? resolved.success : resolved.danger;

            Text detailText = CreateText("Detail", rect, detail, 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(detailText.rectTransform, new Vector2(46f, -24f), new Vector2(308f, 16f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(detailText, resolved, GameUiTextStyle.SmallText, true);
        }

        private void RefreshDeckList()
        {
            if (deckListRoot == null)
            {
                return;
            }

            bool showDeckList = currentTab == MapFunctionMenuTab.Deck
                && panelRoot != null
                && panelRoot.activeSelf;
            deckListRoot.gameObject.SetActive(showDeckList);
            ClearDeckList();

            if (!showDeckList)
            {
                return;
            }

            IReadOnlyList<CardData> cards = sessionState == null || sessionState.deckState == null
                ? null
                : sessionState.deckState.masterDeck;
            if (cards == null || cards.Count == 0)
            {
                Text empty = CreateText("DeckEmpty", deckListRoot, "当前没有卡牌。", 16, FontStyle.Normal, TextAnchor.MiddleCenter);
                SetRect(empty.rectTransform, Vector2.zero, deckListRoot.sizeDelta, new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(empty, GameUiStyle.ResolveTheme(theme), GameUiTextStyle.Body);
                return;
            }

            int count = Mathf.Min(cards.Count, MaxVisibleListItems);
            for (int i = 0; i < count; i++)
            {
                CreateDeckCardPanel(cards[i], i);
            }

            if (cards.Count > count)
            {
                Text overflow = CreateText("DeckOverflow", deckListRoot, "还有 " + (cards.Count - count) + " 张卡牌未显示。", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
                SetRect(overflow.rectTransform, new Vector2(0f, -count * (ListRowHeight + ListRowSpacing)), new Vector2(374f, 24f), new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(overflow, GameUiStyle.ResolveTheme(theme), GameUiTextStyle.SmallText);
            }
        }

        private void CreateDeckCardPanel(CardData card, int index)
        {
            GameObject row = new GameObject("DeckCard_" + index, typeof(RectTransform));
            RectTransform rect = row.transform as RectTransform;
            rect.SetParent(deckListRoot, false);
            SetRect(rect, new Vector2(0f, -index * (ListRowHeight + ListRowSpacing)), new Vector2(374f, ListRowHeight), new Vector2(0f, 1f));

            Image background = row.AddComponent<Image>();
            background.raycastTarget = false;
            PixelPanel panel = row.AddComponent<PixelPanel>();
            panel.background = background;
            panel.theme = theme;
            panel.variant = GameUiPanelVariant.Dark;
            panel.ApplyStyle();

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            CreateListIcon("CardIcon", rect, GetCardIcon(card, resolved), new Vector2(10f, -8f), new Vector2(28f, 28f), resolved.inverseText);

            string cardName = card == null || string.IsNullOrEmpty(card.displayName) ? "未知卡牌" : card.displayName;
            Text nameText = CreateText("Name", rect, cardName, 15, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(nameText.rectTransform, new Vector2(46f, -5f), new Vector2(170f, 18f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(nameText, resolved, GameUiTextStyle.ButtonText, true);

            Text costText = CreateText("Cost", rect, "费用 " + (card == null ? 0 : card.cost), 14, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(costText.rectTransform, new Vector2(258f, -5f), new Vector2(96f, 18f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(costText, resolved, GameUiTextStyle.SmallText, true);
            costText.color = resolved.gold;

            Text detailText = CreateText("Detail", rect, BuildCardDetail(card), 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(detailText.rectTransform, new Vector2(46f, -24f), new Vector2(308f, 16f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(detailText, resolved, GameUiTextStyle.SmallText, true);
        }

        private void UseInventoryItem(ItemData item)
        {
            ResolveSessionState();
            if (item == null || sessionState == null)
            {
                inventoryFeedbackMessage = "道具数据不可用。";
                Refresh();
                return;
            }

            ItemUseResult result = itemEffectResolver.UseItem(item, sessionState);
            string itemName = string.IsNullOrEmpty(item.displayName) ? item.itemId : item.displayName;
            if (result.success)
            {
                inventoryFeedbackMessage = "已使用：" + itemName + (result.consumed ? "，已消耗。" : "。");
                if (hudView != null)
                {
                    hudView.Refresh();
                }
            }
            else
            {
                inventoryFeedbackMessage = "无法使用：" + (string.IsNullOrEmpty(result.message) ? itemName : result.message);
            }

            Refresh();
        }

        private Sprite GetInventoryItemIcon(ItemData item)
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (item != null && item.icon != null)
            {
                return item.icon;
            }

            return resolved.inventoryIcon;
        }

        private static Sprite GetCardIcon(CardData card, UIThemeConfig resolved)
        {
            if (card != null)
            {
                if (card.icon != null)
                {
                    return card.icon;
                }

                if (card.cardSprite != null)
                {
                    return card.cardSprite;
                }
            }

            return resolved == null ? null : resolved.cardsIcon;
        }

        private static string BuildInventoryItemDetail(ItemData item)
        {
            if (item == null)
            {
                return "道具数据缺失";
            }

            string description = string.IsNullOrEmpty(item.description)
                ? GetTriggerLabel(item.triggerTiming)
                : item.description;
            return GetItemTypeLabel(item.itemType) + " · " + Truncate(description, 24);
        }

        private static string BuildCardDetail(CardData card)
        {
            if (card == null)
            {
                return "卡牌数据缺失";
            }

            string description = string.IsNullOrEmpty(card.description)
                ? GetRarityLabel(card.rarity)
                : card.description;
            return GetCardTypeLabel(card.cardType) + " · " + GetRarityLabel(card.rarity) + " · " + Truncate(description, 18);
        }

        private static bool CanUseItemOnMap(ItemData item)
        {
            return item != null
                && item.canUseInStageScreen
                && item.triggerTiming == ItemTriggerTiming.OnUse;
        }

        private static string GetInventoryItemStatus(ItemData item)
        {
            if (item == null)
            {
                return "不可用";
            }

            if (!CanUseItemOnMap(item))
            {
                return "地图不可用";
            }

            return item.consumedOnUse || item.itemType == ItemType.Consumable ? "使用" : "触发";
        }

        private void ClearInventoryList()
        {
            ClearList(inventoryListRoot);
        }

        private void ClearDeckList()
        {
            ClearList(deckListRoot);
        }

        private void ClearList(RectTransform root)
        {
            if (root == null)
            {
                return;
            }

            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
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

        private void ApplyBodyLayout()
        {
            if (bodyText == null)
            {
                return;
            }

            RectTransform bodyRect = bodyText.rectTransform;
            if (currentTab == MapFunctionMenuTab.Inventory || currentTab == MapFunctionMenuTab.Deck)
            {
                SetRect(bodyRect, new Vector2(22f, -104f), new Vector2(386f, 58f), new Vector2(0f, 1f));
                return;
            }

            SetRect(bodyRect, new Vector2(22f, -104f), new Vector2(386f, 246f), new Vector2(0f, 1f));
        }

        private static void AppendSection(StringBuilder builder, string title)
        {
            builder.Append("<color=");
            builder.Append(HeaderColor);
            builder.Append(">■ ");
            builder.Append(title);
            builder.AppendLine("</color>");
        }

        private static void AppendRow(StringBuilder builder, string label, string value)
        {
            builder.Append("<color=");
            builder.Append(LabelColor);
            builder.Append(">");
            builder.Append(label);
            builder.Append("</color>  <color=");
            builder.Append(ValueColor);
            builder.Append(">");
            builder.Append(value);
            builder.AppendLine("</color>");
        }

        private static void AppendStatLine(StringBuilder builder, (string Label, object Value) first, (string Label, object Value) second, (string Label, object Value) third)
        {
            AppendStat(builder, first);
            builder.Append("    ");
            AppendStat(builder, second);
            builder.Append("    ");
            AppendStat(builder, third);
            builder.AppendLine();
        }

        private static void AppendStat(StringBuilder builder, (string Label, object Value) stat)
        {
            builder.Append("<color=");
            builder.Append(LabelColor);
            builder.Append(">");
            builder.Append(stat.Label);
            builder.Append("</color> <color=");
            builder.Append(ValueColor);
            builder.Append(">");
            builder.Append(stat.Value);
            builder.Append("</color>");
        }

        private static void AppendHint(StringBuilder builder, string value)
        {
            builder.Append("<color=");
            builder.Append(HintColor);
            builder.Append(">");
            builder.Append(value);
            builder.AppendLine("</color>");
        }

        private static void AppendSpacer(StringBuilder builder)
        {
            builder.AppendLine();
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

        private static string GetTriggerLabel(ItemTriggerTiming timing)
        {
            switch (timing)
            {
                case ItemTriggerTiming.OnAcquire:
                    return "获得时生效";
                case ItemTriggerTiming.OnStageStart:
                    return "阶段开始";
                case ItemTriggerTiming.OnBattleStart:
                    return "战斗开始";
                case ItemTriggerTiming.OnCardPlayed:
                    return "出牌时机";
                case ItemTriggerTiming.OnBossStart:
                    return "Boss 开始";
                case ItemTriggerTiming.OnStageEnd:
                    return "阶段结束";
                default:
                    return "主动使用";
            }
        }

        private static string GetCardTypeLabel(CardType type)
        {
            switch (type)
            {
                case CardType.Rest:
                    return "休息";
                case CardType.Social:
                    return "社交";
                case CardType.Family:
                    return "家庭";
                case CardType.Interest:
                    return "兴趣";
                case CardType.Trait:
                    return "特质";
                case CardType.Status:
                    return "状态";
                default:
                    return "学习";
            }
        }

        private static string GetRarityLabel(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Uncommon:
                    return "进阶";
                case CardRarity.Rare:
                    return "稀有";
                default:
                    return "普通";
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

        private static Image CreateListIcon(string name, RectTransform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
        {
            GameObject iconObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = iconObject.transform as RectTransform;
            rect.SetParent(parent, false);
            SetRect(rect, anchoredPosition, sizeDelta, new Vector2(0f, 1f));

            Image image = iconObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = sprite == null ? Color.clear : color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.enabled = sprite != null;
            return image;
        }

        public void ApplyStyle()
        {
            ResolveReferences();
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (panelStyle != null)
            {
                panelStyle.theme = resolved;
                panelStyle.variant = GameUiPanelVariant.Dark;
                panelStyle.ApplyStyle();
                if (panelStyle.background != null)
                {
                    GameUiStyle.ApplyPanelSprite(panelStyle.background, resolved.menuWindowSprite, GameUiStyle.ResolvePanelColor(resolved, GameUiPanelVariant.Dark));
                }
            }

            GameUiStyle.ApplyPixelText(titleText, resolved, GameUiTextStyle.Subtitle, true);
            GameUiStyle.ApplyPixelText(bodyText, resolved, GameUiTextStyle.Body, true);
            ApplyButton(statusTabButton);
            ApplyButton(inventoryTabButton);
            ApplyButton(deckTabButton);
            ApplyButton(settingsTabButton);
            ApplyButton(closeButton);
            ApplyTabSelection();
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

        private void ApplyTabSelection()
        {
            SetSelected(statusTabButton, currentTab == MapFunctionMenuTab.Status);
            SetSelected(inventoryTabButton, currentTab == MapFunctionMenuTab.Inventory);
            SetSelected(deckTabButton, currentTab == MapFunctionMenuTab.Deck);
            SetSelected(settingsTabButton, currentTab == MapFunctionMenuTab.Settings);
        }

        private void SetSelected(Button button, bool selected)
        {
            if (button == null)
            {
                return;
            }

            PixelButton pixelButton = button.GetComponent<PixelButton>();
            if (pixelButton != null)
            {
                pixelButton.SetSelected(selected);
            }
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

            RectTransform panelRect = panelRoot == null ? null : panelRoot.transform as RectTransform;
            if (panelRect == null)
            {
                return;
            }

            if (inventoryListRoot == null)
            {
                inventoryListRoot = FindChildRect(panelRect, "InventoryItemList");
            }

            if (deckListRoot == null)
            {
                deckListRoot = FindChildRect(panelRect, "DeckCardList");
            }

            bool canPatchExistingLayout = titleText != null && bodyText != null;
            if (canPatchExistingLayout && inventoryListRoot == null)
            {
                inventoryListRoot = CreateListRoot("InventoryItemList", panelRect, new Vector2(22f, -176f), new Vector2(386f, 170f));
                inventoryListRoot.gameObject.SetActive(false);
            }

            if (canPatchExistingLayout && deckListRoot == null)
            {
                deckListRoot = CreateListRoot("DeckCardList", panelRect, new Vector2(22f, -176f), new Vector2(386f, 170f));
                deckListRoot.gameObject.SetActive(false);
            }

            if (canPatchExistingLayout && inventoryListRoot != null)
            {
                SetRect(inventoryListRoot, new Vector2(22f, -176f), new Vector2(386f, 170f), new Vector2(0f, 1f));
            }

            if (canPatchExistingLayout && deckListRoot != null)
            {
                SetRect(deckListRoot, new Vector2(22f, -176f), new Vector2(386f, 170f), new Vector2(0f, 1f));
            }
        }

        private void ResolveSessionState()
        {
            if (sessionState == null && sceneTransitionController != null)
            {
                sessionState = sceneTransitionController.sessionState;
            }
        }

        private static string GetTitle(MapFunctionMenuTab tab)
        {
            switch (tab)
            {
                case MapFunctionMenuTab.Inventory:
                    return "背包";
                case MapFunctionMenuTab.Deck:
                    return "卡组";
                case MapFunctionMenuTab.Settings:
                    return "设置";
                default:
                    return "学生状态";
            }
        }

        private static Text CreateText(string name, RectTransform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = textObject.transform as RectTransform;
            rect.SetParent(parent, false);
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = anchor;
            text.lineSpacing = 1.05f;
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

            Text text = CreateText("Text", rect, label, 16, FontStyle.Bold, TextAnchor.MiddleCenter);
            RectTransform textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.background = image;
            pixelButton.labelText = text;
            pixelButton.theme = theme;
            pixelButton.ApplyStyle();
            return button;
        }

        private static RectTransform FindChildRect(RectTransform parent, string childName)
        {
            if (parent == null)
            {
                return null;
            }

            Transform child = parent.Find(childName);
            return child == null ? null : child as RectTransform;
        }

        private static RectTransform CreateListRoot(string name, RectTransform parent, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject listObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = listObject.transform as RectTransform;
            rect.SetParent(parent, false);
            SetRect(rect, anchoredPosition, sizeDelta, new Vector2(0f, 1f));
            return rect;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchor)
        {
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
