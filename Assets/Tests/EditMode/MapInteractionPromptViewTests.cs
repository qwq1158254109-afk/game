using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 提示栏的显隐契约。核心是这一条：**离开交互范围再靠近同一个目标，提示必须重新出现**。
    ///
    /// 原实现的故障链（2026-10-23）：
    ///   ShowDefault() 在 showDefaultWhenIdle=false 时只调 SetVisible(false)，
    ///   既不置 visibleState=false（导致 FinishHide 提前 return、面板永不 SetActive(false)），
    ///   也不清 lastMessage（导致 ShowMessage 的幂等判断命中、直接 return）。
    ///   结果是第二次靠近同一目标时提示既不更新也不显示。
    ///
    /// 注：EditMode 没有 Update 循环，LitMotion 补间不会推进。这里把 canvasGroup 置空，
    /// 让 BeginShow/BeginHide 走直接 SetActive 的同步分支，才能断言显隐结果。
    /// </summary>
    public class MapInteractionPromptViewTests
    {
        private GameObject root;
        private MapInteractionPromptView view;
        private GameObject targetObject;
        private MapInteractionTarget target;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("PromptRoot", typeof(RectTransform));
            view = root.AddComponent<MapInteractionPromptView>();
            view.panelRoot = root;
            view.showDefaultWhenIdle = false;
            view.canvasGroup = null;

            targetObject = new GameObject("Target");
            target = targetObject.AddComponent<MapInteractionTarget>();
            target.displayName = "书桌";
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null) Object.DestroyImmediate(root);
            if (targetObject != null) Object.DestroyImmediate(targetObject);
        }

        [Test]
        public void PromptShowsWhenApproachingTarget()
        {
            view.ShowTarget(target);
            Assert.IsTrue(view.panelRoot.activeSelf, "靠近可交互目标应显示提示");
        }

        [Test]
        public void PromptHidesWhenLeaving()
        {
            view.ShowTarget(target);
            view.ShowTarget(null);
            Assert.IsFalse(view.panelRoot.activeSelf, "离开交互范围应隐藏提示");
        }

        [Test]
        public void PromptReappearsAfterLeavingAndReturningToSameTarget()
        {
            view.ShowTarget(target);
            view.ShowTarget(null);
            view.ShowTarget(target);

            Assert.IsTrue(view.panelRoot.activeSelf,
                "离开后再靠近同一目标，提示必须重新弹出——不能被上一次的文案缓存挡住");
        }

        [Test]
        public void HideClearsCachedMessageSoNextShowWorks()
        {
            view.ShowTarget(target);
            view.Hide();
            view.ShowTarget(target);

            Assert.IsTrue(view.panelRoot.activeSelf, "显式隐藏后再次显示不应被去重逻辑拦截");
        }

        [Test]
        public void RepeatedShowWithSameTargetDoesNotToggleVisibility()
        {
            view.ShowTarget(target);
            bool first = view.panelRoot.activeSelf;

            // 每帧 RefreshTarget 都会调到，内容没变时不应反复起补间（这里是显隐幂等）
            view.ShowTarget(target);
            view.ShowTarget(target);

            Assert.AreEqual(first, view.panelRoot.activeSelf, "同一目标重复调用应保持稳定");
        }
    }
}
