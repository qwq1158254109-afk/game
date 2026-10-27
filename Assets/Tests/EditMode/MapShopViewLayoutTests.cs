using System.Linq;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 商店面板布局与金钱显示。锁四条被用户截图报过的契约：
    /// 1. 面板宽度必须窄于画布参考宽度 1280（曾做到 1400，屏幕上左右被裁）；
    /// 2. 金钱文本不得压住关闭按钮（1400 宽下曾重叠 12px）；
    /// 3. 关闭按钮必须贴面板右边缘、且完全落在面板内（旧版迁移分支曾把它挪到 x=516 的顶栏中间）；
    /// 4. 初始金钱 100 时，第一页道具必须至少有一件显示为可购买，不能整页「金钱不足」。
    /// </summary>
    public class MapShopViewLayoutTests
    {
        private GameObject canvasOwner;
        private MapShopView view;

        [SetUp]
        public void Setup()
        {
            canvasOwner = new GameObject("ShopLayoutTestCanvas", typeof(RectTransform));
            Canvas canvas = canvasOwner.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            view = MapUiFactory.CreateShopView(canvasOwner.transform as RectTransform);
        }

        [TearDown]
        public void Cleanup()
        {
            if (canvasOwner != null)
            {
                Object.DestroyImmediate(canvasOwner);
            }
        }

        private static RectTransform RectOf(Component component)
        {
            return component == null ? null : component.transform as RectTransform;
        }

        /// <summary>anchor/pivot 都是 (0,1)，所以左边缘 = anchoredPosition.x，右边缘 = 左 + width。</summary>
        private static float Left(RectTransform rect)
        {
            return rect.anchoredPosition.x;
        }

        private static float Right(RectTransform rect)
        {
            return rect.anchoredPosition.x + rect.sizeDelta.x;
        }

        [Test]
        public void MoneyTextDoesNotOverlapCloseButton()
        {
            view.Show();

            RectTransform moneyRect = RectOf(view.moneyText);
            RectTransform closeRect = RectOf(view.closeButton);
            Assert.IsNotNull(moneyRect, "金钱文本必须存在");
            Assert.IsNotNull(closeRect, "关闭按钮必须存在");
            Assert.LessOrEqual(Right(moneyRect), Left(closeRect),
                "金钱文本右边缘 (" + Right(moneyRect) + ") 不能超过关闭按钮左边缘 (" + Left(closeRect) + ")");
        }

        [Test]
        public void CloseButtonSitsOnPanelRightEdge()
        {
            view.Show();

            RectTransform panelRect = view.panelRoot == null ? null : view.panelRoot.GetComponent<RectTransform>();
            RectTransform closeRect = RectOf(view.closeButton);
            Assert.IsNotNull(panelRect);
            Assert.IsNotNull(closeRect);
            Assert.AreEqual(1200f, panelRect.sizeDelta.x, 0.5f, "商店面板宽度应为 1200");
            Assert.AreEqual(1180f, Right(closeRect), 0.5f, "关闭按钮右边缘应留 20px 边距");
            Assert.Less(Left(closeRect), 1180f, "关闭按钮必须整块落在面板内");
        }

        [Test]
        public void PanelFitsInsideCanvasReferenceWidth()
        {
            view.Show();

            RectTransform panelRect = view.panelRoot == null ? null : view.panelRoot.GetComponent<RectTransform>();
            Assert.IsNotNull(panelRect);
            // MapUiFactory.EnsureRuntimeCanvas 的 CanvasScaler.referenceResolution 是 1280x720，
            // 面板宽于 1280 就会被屏幕左右裁掉（2026-10-27 用户报「太大了装不下」）。
            Assert.LessOrEqual(panelRect.sizeDelta.x, 1280f,
                "商店面板宽度 " + panelRect.sizeDelta.x + " 超过了画布参考宽度 1280");
        }

        [Test]
        public void StockColumnsFitInsideListArea()
        {
            view.Show();

            RectTransform listRect = view.stockListRoot;
            Assert.IsNotNull(listRect, "商品列表区必须存在");

            Button[] stockButtons = listRect.GetComponentsInChildren<Button>(true);
            Assert.Greater(stockButtons.Length, 1, "第一页至少要有两件商品才能验列宽");

            RectTransform first = stockButtons[0].transform as RectTransform;
            RectTransform second = stockButtons[1].transform as RectTransform;
            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.LessOrEqual(Right(second), listRect.sizeDelta.x + 0.5f,
                "第二列右边缘 " + Right(second) + " 溢出了列表区宽度 " + listRect.sizeDelta.x);
        }

        [Test]
        public void MoneyIconSitsLeftOfMoneyText()
        {
            view.Show();

            RectTransform iconRect = RectOf(view.moneyIcon);
            RectTransform moneyRect = RectOf(view.moneyText);
            Assert.IsNotNull(iconRect);
            Assert.IsNotNull(moneyRect);
            Assert.LessOrEqual(Right(iconRect), Left(moneyRect), "金钱图标不能压住金钱文本");
        }

        [Test]
        public void StockRowHighlightsOnHoverButNotOnClick()
        {
            view.Show();

            PixelButton[] rows = view.stockListRoot == null
                ? new PixelButton[0]
                : view.stockListRoot.GetComponentsInChildren<PixelButton>(true);
            Assert.Greater(rows.Length, 0, "第一页必须有商品横条");

            PixelButton row = rows[0];
            // 关掉缩放动画：EditMode 下 StartCoroutine 不会 tick，测颜色就够了。
            row.animateScale = false;
            Image background = row.GetComponent<Image>();
            Assert.IsNotNull(background);
            Assert.IsTrue(row.hoverOnlyVisual, "商品横条必须走 hoverOnlyVisual（用户要求只要悬停变化）");

            Sprite restingSkin = background.sprite;
            Color restingColor = background.color;

            row.OnPointerEnter(null);
            bool hoverChanged = background.sprite != restingSkin || background.color != restingColor;
            Assert.IsTrue(hoverChanged, "悬停必须有可见变化（换暖金底图或提亮）");
            Sprite hoverSkin = background.sprite;
            Color hoverColor = background.color;

            // 点击：按下 / 选中 / 抬起都不该改变外观，尤其不能换成绿色的 button_selected
            row.OnPointerDown(null);
            row.OnSelect(null);
            row.OnPointerUp(null);
            Assert.AreEqual(hoverSkin, background.sprite, "点击不能换底图（会变成绿色选中图）");
            Assert.AreEqual(hoverColor, background.color, "点击不能改颜色");

            // 鼠标移开：必须回到常态，且不能因为「点过」而留在高亮
            row.OnPointerExit(null);
            Assert.AreEqual(restingSkin, background.sprite, "鼠标移开后必须回到常态底图");
            Assert.AreEqual(restingColor, background.color, "鼠标移开后必须回到常态颜色");

            // 鼠标不在上面时的选中（键盘 / 点击残留选中态）也不能点亮
            row.OnSelect(null);
            Assert.AreEqual(restingSkin, background.sprite, "点击选中不能留下高亮底图");
            Assert.AreEqual(restingColor, background.color, "点击选中不能留下高亮颜色");
        }

        [Test]
        public void TabsNeverTurnGreenOnClick()
        {
            view.Show();

            // 页签点击后若走 selected 分支会换上 button_selected.png（绿色），
            // 再被 StyleTab 的 tint 染成暗绿——用户报的「变绿」就是这个。
            Button[] tabs = view.panelRoot.GetComponentsInChildren<Button>(true)
                .Where(b => b != null && b.name.StartsWith("Tab"))
                .ToArray();
            Assert.AreEqual(2, tabs.Length, "商店应有道具 / 卡牌两个页签");

            for (int i = 0; i < tabs.Length; i++)
            {
                PixelButton pixelButton = tabs[i].GetComponent<PixelButton>();
                Assert.IsNotNull(pixelButton);
                pixelButton.animateScale = false;

                Image background = tabs[i].GetComponent<Image>();
                Sprite before = background.sprite;
                Color colorBefore = background.color;

                // 完整走一遍真实鼠标路径：移上去 -> 按下 -> 选中 -> 抬起 -> 移开。
                // 旧实现在「移开」这一步才刷新视觉，selected 还是 true，
                // 于是换上绿色的 button_selected —— 用户看到的「点击之后变绿」。
                pixelButton.OnPointerEnter(null);
                pixelButton.OnPointerDown(null);
                pixelButton.OnSelect(null);
                pixelButton.OnPointerUp(null);
                pixelButton.OnPointerExit(null);

                Assert.AreEqual(before, background.sprite, "页签点击后移开鼠标不能换底图（会变绿）：" + tabs[i].name);
                Assert.AreEqual(colorBefore, background.color, "页签点击后移开鼠标不能改颜色：" + tabs[i].name);
            }
        }

        [Test]
        public void PagingIsIndependentPerTab()
        {
            view.Show();

            Button tabItem = FindButton("TabItem");
            Button tabCard = FindButton("TabCard");
            Button nextPage = FindButton("NextPage");
            Text pageLabel = FindText("PageLabel");
            Assert.IsNotNull(tabItem);
            Assert.IsNotNull(tabCard);
            Assert.IsNotNull(nextPage);
            Assert.IsNotNull(pageLabel);

            // 道具 9 件 / 每页 6 件 = 2 页
            Assert.AreEqual("1/2", pageLabel.text, "道具页应显示 1/2");

            nextPage.onClick.Invoke();
            Assert.AreEqual("2/2", pageLabel.text, "翻到第 2 页应显示 2/2");
            Assert.IsTrue(nextPage.gameObject.activeSelf, "第 2 页翻页按钮不能消失");

            // 卡牌只有 4 件 = 1 页：切过去按钮应隐藏，页码不能沿用道具页的 2/2
            tabCard.onClick.Invoke();
            Assert.IsFalse(nextPage.gameObject.activeSelf, "卡牌页只有 1 页，翻页按钮应隐藏");
            Assert.IsFalse(pageLabel.gameObject.activeSelf, "卡牌页只有 1 页，页码应隐藏");

            // 切回道具页，页码要还是 2/2（两个页签各自记页码）
            tabItem.onClick.Invoke();
            Assert.IsTrue(nextPage.gameObject.activeSelf, "切回道具页翻页按钮应恢复");
            Assert.AreEqual("2/2", pageLabel.text, "切回道具页应保留原来的 2/2");
        }

        private Button FindButton(string name)
        {
            Button[] buttons = view.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] != null && buttons[i].name == name)
                {
                    return buttons[i];
                }
            }
            return null;
        }

        private Text FindText(string name)
        {
            Text[] texts = view.GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] != null && texts[i].name == name)
                {
                    return texts[i];
                }
            }
            return null;
        }

        [Test]
        public void InitialMoneyIsShownAndFirstPageHasAffordableItem()
        {
            GameSessionState sessionState = new GameSessionState();
            Assert.AreEqual(100, sessionState.playerState.money, "新会话初始金钱应为 100");

            view.Configure(sessionState, null);
            view.Show();

            Assert.AreEqual("100", view.moneyText.text, "顶栏金钱文本应显示当前金钱");

            Button[] stockButtons = view.stockListRoot == null
                ? new Button[0]
                : view.stockListRoot.GetComponentsInChildren<Button>(true);
            Assert.Greater(stockButtons.Length, 0, "第一页必须有商品");

            int affordable = 0;
            for (int i = 0; i < stockButtons.Length; i++)
            {
                if (stockButtons[i] != null && stockButtons[i].interactable)
                {
                    affordable++;
                }
            }

            Assert.Greater(affordable, 0, "初始金钱 100 时第一页至少有一件可购买，不能整页「金钱不足」");
        }
    }
}
