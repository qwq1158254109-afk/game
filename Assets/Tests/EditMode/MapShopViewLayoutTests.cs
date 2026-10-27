using NUnit.Framework;
using StudentGrowth;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 商店顶栏布局与金钱显示。锁三条曾经被用户截图报过的契约：
    /// 1. 金钱文本不得压住关闭按钮（1400 宽下曾重叠 12px）；
    /// 2. 关闭按钮必须贴面板右边缘、且完全落在面板内（旧版迁移分支曾把它挪到 x=516 的顶栏中间）；
    /// 3. 初始金钱 100 时，第一页道具必须至少有一件显示为可购买，不能整页「金钱不足」。
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
            Assert.AreEqual(1400f, panelRect.sizeDelta.x, 0.5f, "商店面板宽度应为 1400");
            Assert.AreEqual(1380f, Right(closeRect), 0.5f, "关闭按钮右边缘应留 20px 边距");
            Assert.Less(Left(closeRect), 1380f, "关闭按钮必须整块落在面板内");
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
