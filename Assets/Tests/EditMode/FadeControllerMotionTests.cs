using System.Collections;
using LitMotion;
using NUnit.Framework;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// FadeController 接入 LitMotion 后的行为固定测试（2026-10-20）。
    ///
    /// 为什么要这么测：淡入淡出是"看不清就说不清"的效果，靠截图判断不了
    /// 「补间有没有真的走到终点」。这里用 LitMotion 的 ManualMotionDispatcher
    /// 手动推进时间，把过程变成确定性断言——不依赖帧率、不进 Play 模式。
    ///
    /// 四个必须知道的写法约束（全部是实测踩出来的）：
    ///
    /// 1. **必须驱动 `FadeTo`，不要驱动 `FadeIn`/`FadeOut`**
    ///    FadeIn/FadeOut 是 `yield return FadeTo(...)` 的薄封装，而
    ///    「递归执行内层 IEnumerator」是 Unity 协程调度器干的活。
    ///    手动 MoveNext 时外层只会把内层枚举器当作 Current 返回，
    ///    内层方法体**根本不会执行**（表现为 blocksRaycasts 没置位、alpha 不动）。
    ///    FadeTo 已提升为 public，正式代码仍走 FadeIn/FadeOut。
    ///
    /// 2. **必须注入调度器，不能靠 `MotionScheduler.DefaultScheduler`**
    ///    LitMotion 的 `MotionBuilder.ScheduleMotion()` 在**编辑器非播放态**下，
    ///    只要 `buffer.Scheduler == null` 就直接分流到 `EditorMotionDispatcher`，
    ///    把 `DefaultScheduler` 完全忽略；而 FadeController 里写死的
    ///    `UpdateIgnoreTimeScale` 属于 PlayerLoop 调度器，EditMode 下不被驱动。
    ///    所以测试通过 `schedulerOverride` 注入 ManualMotionDispatcher。
    ///
    /// 3. **`IEnumerator` 是惰性的**
    ///    方法体要等第一次 MoveNext 才执行，"呼出后立刻置位"的断言必须在其后。
    ///
    /// 4. **断言前先 `MoveNext` 到底**
    ///    循环退出时补间可能刚好完成但协程还没被推到最后一行，
    ///    补一次 MoveNext 让 `SetImageAlpha(target)` 与 `blocksRaycasts` 收尾。
    ///
    /// 覆盖三件事：
    /// 1. 淡入：alpha 从 0 渐进到 1（中途必须是中间值，证明是渐变而非跳变）
    /// 2. 淡出：alpha 回到 0
    /// 3. 重叠：后发起的过渡取消前一条，最终值只由最后一条决定
    /// </summary>
    public sealed class FadeControllerMotionTests
    {
        private GameObject root;
        private FadeController controller;
        private ManualMotionDispatcher dispatcher;

        [SetUp]
        public void SetUp()
        {
            // 清掉可能残留的单例，保证每个用例拿到干净的 FadeController。
            FadeController existing = Object.FindAnyObjectByType<FadeController>();
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            dispatcher = new ManualMotionDispatcher();

            root = new GameObject("FadeControllerTestRoot");
            controller = root.AddComponent<FadeController>();
            // EditMode 下 AddComponent 不触发 Awake，必须显式初始化，
            // 否则 fadeImage 为 null，后续断言全变成 NullReferenceException。
            controller.EnsureInitialized();
            controller.schedulerOverride = dispatcher.Scheduler;
        }

        [TearDown]
        public void TearDown()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FadeIn_AnimatesAlphaFromZeroToOne()
        {
            Assert.IsNotNull(controller.fadeImage, "初始化后 fadeImage 必须存在");
            Assert.AreEqual(0f, controller.fadeImage.color.a, 1e-4f, "初始应为全透明");
            Assert.IsFalse(controller.canvasGroup.blocksRaycasts, "全透明时不应挡点击");

            IEnumerator routine = controller.FadeTo(1f, 0.2f);
            routine.MoveNext();   // 惰性求值：让 FadeTo 方法体执行（首行置 blocksRaycasts=true）
            Assert.IsTrue(controller.canvasGroup.blocksRaycasts,
                "过渡开始后应挡住点击，避免连点穿透");

            float midAlpha = -1f;
            for (int i = 0; i < 10; i++)
            {
                dispatcher.Update(0.02);
                routine.MoveNext();
                if (i == 4)
                {
                    midAlpha = controller.fadeImage.color.a;
                }
            }

            Assert.Greater(midAlpha, 0f, $"第 5 步 alpha={midAlpha}——为 0 说明补间没有推进");
            Assert.Less(midAlpha, 1f, $"第 5 步 alpha={midAlpha}——为 1 说明是瞬间跳变而非渐变");
            Assert.AreEqual(1f, controller.fadeImage.color.a, 0.01f, "最终应完全变黑");
            Assert.IsTrue(controller.canvasGroup.blocksRaycasts, "全黑后应挡住点击");
        }

        [Test]
        public void FadeOut_AnimatesAlphaBackToZero()
        {
            controller.SetAlpha(1f);
            Assert.IsNotNull(controller.fadeImage, "初始化后 fadeImage 必须存在");
            Assert.AreEqual(1f, controller.fadeImage.color.a, 1e-4f, "前置状态应为全黑");

            IEnumerator routine = controller.FadeTo(0f, 0.2f);
            routine.MoveNext();
            for (int i = 0; i < 10; i++)
            {
                dispatcher.Update(0.02);
                routine.MoveNext();
            }

            // 补间在循环内完成，但协程还要再走一步才会执行收尾的
            // SetImageAlpha(target) / blocksRaycasts，所以必须补一次。
            routine.MoveNext();

            Assert.AreEqual(0f, controller.fadeImage.color.a, 0.01f, "最终应恢复全透明");
            Assert.IsFalse(controller.canvasGroup.blocksRaycasts, "透明后不应再挡点击");
        }

        [Test]
        public void SecondFade_CancelsPreviousTweenWithoutStalling()
        {
            // 这条是本次改动的核心动机：旧的协程实现下两次过渡会并行推进，
            // 各自从不同起点 Lerp 同一个 fadeImage.color，场景连切时会出现中间态闪跳。
            // 改成按目标绑定的补间后，第二条会取消第一条，最终值只由最后一条决定。
            IEnumerator first = controller.FadeTo(1f, 0.4f);
            first.MoveNext();
            dispatcher.Update(0.05);
            first.MoveNext();
            Assert.Greater(controller.fadeImage.color.a, 0f, "第一条应已开始变黑，重叠局面成立");

            IEnumerator second = controller.FadeTo(0f, 0.05f);
            second.MoveNext();
            for (int i = 0; i < 5; i++)
            {
                dispatcher.Update(0.02);
                second.MoveNext();
            }
            second.MoveNext();   // 补一步让协程收尾

            Assert.AreEqual(0f, controller.fadeImage.color.a, 0.01f,
                "后发起的淡出应胜出：说明旧补间已被显式取消，没有与新补间互相拉扯");
        }
    }
}