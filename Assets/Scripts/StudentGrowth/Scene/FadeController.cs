using System.Collections;
using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DefaultExecutionOrder(-95)]
    public sealed class FadeController : MonoBehaviour
    {
        private const string RootName = "SceneTransitionFadeController";

        // 淡入淡出时长交给调用方（SceneTransitionManager）传，这里只做兜底。
        private static FadeController instance;

        public float defaultDuration = 0.35f;
        public Canvas canvas;
        public CanvasGroup canvasGroup;
        public Image fadeImage;

        // 补间句柄。
        //
        // 重要（2026-10-20 实测纠正）：LitMotion 的核心 `Bind` **不会**自动取消
        // 绑定同一目标的前一条补间（见 MotionBuilder.Bind：只是 SetCallbackData +
        // ScheduleMotion）。早先注释里写的"按目标绑定天然互斥"是错的——
        // 不显式取消的话两条补间会并行 Lerp 同一个 Image.color。
        // 实测表现：`FadeTo(1f,0.4f)` 进行中再 `FadeTo(0f,0.05f)`，第二条跑完 alpha
        // 仍被第一条拉回 0.375（= 第一条在 t=0.15s 的插值），黑场收不住。
        private MotionHandle fadeMotion;

        /// <summary>
        /// 过渡代际。每次 FadeTo 自增，协程在补间结束后比对：
        /// 不是最新一代说明自己已被后一次过渡取代，应立即停止后续步骤，
        /// 否则旧协程会把新过渡的 blocksRaycasts / 状态改掉。
        /// </summary>
        private int fadeGeneration;

        /// <summary>
        /// 补间调度器。留 null 时用忽略时间缩放的默认值（正式运行即如此）。
        ///
        /// 之所以可注入：EditMode 下 LitMotion 的 PlayerLoop 调度器不会被编辑器的帧驱动，
        /// 补间永远停在起点（`MotionBuilder.ScheduleMotion` 只在**编辑器非播放态**且
        /// `DefaultScheduler` 为默认值时才分流到 EditorMotionDispatcher）。
        /// 测试注入 ManualMotionDispatcher 后即可手动推进时间，把"渐变"变成可断言的确定性行为。
        /// </summary>
        [System.NonSerialized] public IMotionScheduler schedulerOverride;

        public static FadeController Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<FadeController>();
                }

                if (instance == null)
                {
                    GameObject root = new GameObject(RootName);
                    instance = root.AddComponent<FadeController>();
                }

                return instance;
            }
        }

        public IEnumerator FadeIn(float duration = -1f)
        {
            yield return FadeTo(1f, duration);
        }

        public IEnumerator FadeOut(float duration = -1f)
        {
            yield return FadeTo(0f, duration);
        }

        public void SetAlpha(float alpha)
        {
            EnsureCanvas();
            SetImageAlpha(alpha);
            canvasGroup.blocksRaycasts = alpha > 0.001f;
        }

        /// <summary>
        /// 确保画布/遮罩已建立，并把 alpha 归零。
        ///
        /// 存在的意义：**EditMode 测试里 AddComponent 不会触发 Awake**，
        /// 于是 fadeImage 始终为 null，后续都在空引用上打转。
        /// 正式运行时这个方法是幂等的（Awake 已经建好了就直接返回），
        /// 所以留成公开 API 比在测试里反射调私有方法干净。
        /// </summary>
        public void EnsureInitialized()
        {
            EnsureCanvas();
            SetAlpha(0f);
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureCanvas();
            SetAlpha(0f);
        }

        /// <summary>
        /// 过渡到指定 alpha。**真正的实现在这里**——FadeIn/FadeOut 只是
        /// `yield return FadeTo(...)` 的薄封装。
        ///
        /// 公开的原因：`yield return 内层 IEnumerator` 的递归执行是 Unity 协程调度器
        /// 负责的；手动 `MoveNext()` 驱动时外层只会把内层枚举器当作 Current 返回，
        /// 内层方法体根本不会执行。测试要能手动步进，就必须直接驱动这一层。
        /// 正式代码仍走 FadeIn/FadeOut，行为不变。
        /// </summary>
        public IEnumerator FadeTo(float targetAlpha, float duration)
        {
            EnsureCanvas();
            duration = duration < 0f ? defaultDuration : duration;
            canvasGroup.blocksRaycasts = true;

            if (duration <= 0f)
            {
                SetImageAlpha(targetAlpha);
                canvasGroup.blocksRaycasts = targetAlpha > 0.001f;
                yield break;
            }

            // 开新过渡前先取消上一条：否则两条补间会并行 Lerp 同一个 Image.color，
        // 最终值被先发起的那条拉走（实测 alpha 停在 0.375）。
        if (fadeMotion.IsActive())
        {
            fadeMotion.Cancel();
        }

        int generation = ++fadeGeneration;

        // 用忽略时间缩放的调度器：暂停时（Time.timeScale = 0）场景过渡仍要走完，
        // 否则从暂停菜单返回主界面会卡在全黑遮罩上。
        // 绑定刻意不用 LitMotion.Extensions 的 BindToColorA——那个程序集因
        // LITMOTION_SUPPORT_TMP 引用了 TMP_Text，会把 Unity.TextMeshPro 变成
        // 本工程的必需依赖（CS0012）。核心库的 Bind 就够。
        IMotionScheduler scheduler = schedulerOverride ?? MotionScheduler.UpdateIgnoreTimeScale;
        fadeMotion = LMotion.Create(fadeImage.color.a, targetAlpha, duration)
            .WithScheduler(scheduler)
            .WithOnComplete(ApplyFinalAlpha)
            .Bind(fadeImage, static (x, target) =>
            {
                Color color = target.color;
                color.a = x;
                target.color = color;
            });

        yield return fadeMotion.ToYieldInstruction();

        // 被后一次过渡取代时就此收手：新过渡自己会负责收尾状态。
        if (generation != fadeGeneration)
        {
            yield break;
        }

        // 收尾**确定地放在这里**，不依赖 WithOnComplete 的触发时序：
        // 补间结束时把 alpha 落到目标值、并按最终 alpha 复位遮挡。
        // （只靠 WithOnComplete 时实测出现过 alpha=0 但 blocksRaycasts 仍为 true，
        //   表现为"全透明却点不动"，所以显式补一次。）
        SetImageAlpha(targetAlpha);
        canvasGroup.blocksRaycasts = targetAlpha > 0.001f;
        }

        /// <summary>
        /// 收尾统一走这里：补间正常完成与中途被新补间取消都不会抛异常，
        /// 但要保证 blocksRaycasts 与最终 alpha 一致，避免出现"全黑但点得动"。
        /// </summary>
        private void ApplyFinalAlpha()
        {
            float alpha = fadeImage == null ? 0f : fadeImage.color.a;
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = alpha > 0.001f;
            }
        }

        private void OnDisable()
        {
            CancelFadeMotion();
        }

        /// <summary>
        /// 销毁时收掉补间。**这是必须的**：补间是按目标绑定的，如果不取消，
        /// 对象（连同 fadeImage）销毁后补间还会继续访问它，抛
        /// `MissingReferenceException: The object of type 'UnityEngine.UI.Image'
        /// has been destroyed but you are still trying to access it`。
        /// 场景切换 / 编辑器停止播放时都会走到这里。
        /// </summary>
        private void OnDestroy()
        {
            CancelFadeMotion();
        }

        private void CancelFadeMotion()
        {
            if (fadeMotion.IsActive())
            {
                fadeMotion.Cancel();
            }

            if (instance == this)
            {
                instance = null;
            }
        }

        private void EnsureCanvas()
        {
            if (canvas != null && canvasGroup != null && fadeImage != null)
            {
                return;
            }

            GameObject canvasObject = new GameObject("GlobalCanvas", typeof(RectTransform));
            canvasObject.transform.SetParent(transform, false);
            canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32760;
            canvasGroup = canvasObject.AddComponent<CanvasGroup>();

            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);

            GameObject imageObject = new GameObject("FadeBlack", typeof(RectTransform));
            RectTransform imageRect = imageObject.transform as RectTransform;
            imageRect.SetParent(canvasObject.transform, false);
            imageRect.anchorMin = Vector2.zero;
            imageRect.anchorMax = Vector2.one;
            imageRect.offsetMin = Vector2.zero;
            imageRect.offsetMax = Vector2.zero;

            fadeImage = imageObject.AddComponent<Image>();
            fadeImage.color = new Color(0f, 0f, 0f, 0f);
            fadeImage.raycastTarget = true;
        }

        private void SetImageAlpha(float alpha)
        {
            if (fadeImage == null)
            {
                return;
            }

            Color color = fadeImage.color;
            color.a = Mathf.Clamp01(alpha);
            fadeImage.color = color;
        }
    }
}
