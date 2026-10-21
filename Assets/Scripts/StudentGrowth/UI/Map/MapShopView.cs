using System.Collections.Generic;
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
        private const float StockRowHeight = 58f;
        private const float StockRowSpacing = 8f;
        private const int MaxVisibleStockItems = 3;

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
        public List<ItemData> stockItems = new List<ItemData>();

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
            panelRect.sizeDelta = new Vector2(620f, 424f);

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
            SetRect(moneyIcon.rectTransform, new Vector2(388f, -26f), new Vector2(24f, 24f), new Vector2(0f, 1f));

            moneyText = CreateText("Money", panelRect, string.Empty, 18, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(moneyText.rectTransform, new Vector2(418f, -22f), new Vector2(92f, 30f), new Vector2(0f, 1f));

            closeButton = CreateButton("CloseButton", panelRect, "关闭", new Vector2(516f, -20f), new Vector2(80f, 34f), new Vector2(0f, 1f));
            closeButton.onClick.AddListener(Hide);

            descriptionText = CreateText("Description", panelRect, string.Empty, 17, FontStyle.Normal, TextAnchor.UpperLeft);
            descriptionText.supportRichText = true;
            descriptionText.lineSpacing = 1.05f;
            SetRect(descriptionText.rectTransform, new Vector2(24f, -68f), new Vector2(572f, 56f), new Vector2(0f, 1f));

            GameObject stockListObject = new GameObject("StockList", typeof(RectTransform));
            stockListRoot = stockListObject.transform as RectTransform;
            stockListRoot.SetParent(panelRect, false);
            SetRect(stockListRoot, new Vector2(24f, -132f), new Vector2(572f, 226f), new Vector2(0f, 1f));

            feedbackText = CreateText("Feedback", panelRect, string.Empty, 17, FontStyle.Bold, TextAnchor.MiddleLeft);
            feedbackText.supportRichText = true;
            SetRect(feedbackText.rectTransform, new Vector2(24f, 22f), new Vector2(572f, 34f), Vector2.zero);

            ApplyStyle();
            Hide();
        }

        public void Show()
        {
            ResolveReferences();
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
            ApplyStyle();
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

            int count = Mathf.Min(stockItems.Count, MaxVisibleStockItems);
            for (int i = 0; i < count; i++)
            {
                Button itemButton = CreateStockButton(stockItems[i], i);
                RectTransform rect = itemButton.transform as RectTransform;
                SetRect(rect, new Vector2(0f, -i * (StockRowHeight + StockRowSpacing)), new Vector2(572f, StockRowHeight), new Vector2(0f, 1f));
            }

            if (stockItems.Count > count)
            {
                Text overflow = CreateText("StockOverflow", stockListRoot, "还有 " + (stockItems.Count - count) + " 件商品未显示。", 14, FontStyle.Normal, TextAnchor.MiddleLeft);
                SetRect(overflow.rectTransform, new Vector2(0f, -count * (StockRowHeight + StockRowSpacing)), new Vector2(560f, 24f), new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(overflow, theme, GameUiTextStyle.SmallText);
            }
        }

        private Button CreateStockButton(ItemData item, int index)
        {
            Button button = CreateButton("ShopItem_" + index, stockListRoot, ResolveItemName(item), Vector2.zero, new Vector2(572f, StockRowHeight), new Vector2(0f, 1f));
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
            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = ResolveItemName(item);
                label.alignment = TextAnchor.MiddleLeft;
                SetRect(label.rectTransform, new Vector2(54f, -8f), new Vector2(220f, 20f), new Vector2(0f, 1f));
                GameUiStyle.ApplyPixelText(label, resolved, GameUiTextStyle.ButtonText, true);
            }

            CreateStockIcon("ItemIcon", rect, item == null || item.icon == null ? resolved.inventoryIcon : item.icon, new Vector2(14f, -14f), new Vector2(30f, 30f), resolved.inverseText);

            Text priceText = CreateText("Price", rect, "$" + ShopPurchaseService.ResolvePrice(item), 15, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(priceText.rectTransform, new Vector2(452f, -8f), new Vector2(84f, 20f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(priceText, resolved, GameUiTextStyle.StatText, true);
            priceText.color = resolved.gold;

            Text detailText = CreateText("Detail", rect, BuildStockDetail(item), 14, FontStyle.Normal, TextAnchor.MiddleLeft);
            SetRect(detailText.rectTransform, new Vector2(54f, -32f), new Vector2(360f, 18f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(detailText, resolved, GameUiTextStyle.SmallText, true);

            bool affordable = CanAfford(item);
            Text statusText = CreateText("Status", rect, affordable ? "可购买" : "金钱不足", 14, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(statusText.rectTransform, new Vector2(434f, -32f), new Vector2(102f, 18f), new Vector2(0f, 1f));
            GameUiStyle.ApplyPixelText(statusText, resolved, GameUiTextStyle.SmallText, true);
            statusText.color = affordable ? resolved.success : resolved.danger;
        }

        private void Purchase(ItemData item)
        {
            ShopPurchaseResult result = purchaseService.TryPurchase(item, sessionState);
            SetFeedback(result);
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
            return Section("购买道具")
                + Row("说明", "商品会直接进入背包。消耗品可在背包中使用，被动道具会在对应战斗时机生效。");
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
            GameUiStyle.ApplyPixelText(moneyText, resolved, GameUiTextStyle.StatText);
            GameUiStyle.ApplyPixelText(descriptionText, resolved, GameUiTextStyle.Body);
            GameUiStyle.ApplyPixelText(feedbackText, resolved, GameUiTextStyle.Body);
            ApplyButton(closeButton);

            if (stockListRoot != null)
            {
                Button[] buttons = stockListRoot.GetComponentsInChildren<Button>(true);
                for (int i = 0; i < buttons.Length; i++)
                {
                    ApplyButton(buttons[i]);
                }
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

        private static Image CreateStockIcon(string name, RectTransform parent, Sprite sprite, Vector2 anchoredPosition, Vector2 sizeDelta, Color color)
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
