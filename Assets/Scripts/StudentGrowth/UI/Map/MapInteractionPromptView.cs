using LitMotion;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public sealed class MapInteractionPromptView : MonoBehaviour
    {
        // 提示栏视觉参数集中在常量里：后续调高低/字号/留白只改这里，不再散落魔法数字。
        // 高度推导：单行文字行高 ≈ 字号 × 1.2（26 × 1.2 ≈ 31），上下留白各 32，得 95 → 取 8 的倍数 96。
        // 左右留白同为 32：横竖留白一致，条内的"呼吸感"才不会一边紧一边松。
        private const float PromptBarWidth = 640f;
        private const float PromptBarMinHeight = 96f;
        private const float PromptBarMaxHeight = 136f;
        private const float PromptBarPadding = 32f;
        private const int PromptFontSize = 26;
        private const float PromptLineSpacing = 1.1f;

        // ---- 入场 / 出场动效（LitMotion，2026-10-20 接入） ----
        // 时长刻意压到 0.15s 内：提示栏在移动中会随目标切换反复显隐，
        // 任何"慢动作"都会让操作反馈显得迟钝。
        private const float ShowDuration = 0.14f;
        private const float HideDuration = 0.09f;
        // 位移取偶数 12px：Canvas 缩放 1.1523 下 12 落在整数像素上，
        // 不会让像素字体在动画过程中出现半像素发虚。
        private const float SlideOffsetY = 12f;

        public UIThemeConfig theme;
        public GameObject panelRoot;
        public PixelPanel panel;
        public CanvasGroup canvasGroup;
        public Text promptText;
        public bool showDefaultWhenIdle;
        public bool debugVisible;
        public bool applyStyleOnAwake = true;

        [Tooltip("用主题里的像素字体（与游戏其它 UI 一致）。关掉则退回内置字体——" +
                 "像素字体在 Canvas 非整数缩放下发虚时的应急开关。")]
        public bool usePixelFont = true;

        // 动效状态：既有 MotionHandle 用来在对象停用时收尾，
        // 也有"当前显示内容"的缓存——RefreshTarget 由玩家控制器每帧调用，
        // 不做这层缓存的话同一条消息会每帧重启一次补间，动效永远播不完。
        private MotionHandle showMotion;
        private MotionHandle slideMotion;
        private float baseAnchoredY;
        private bool visibleState;
        private bool hasState;
        private string lastMessage;

        private void Awake()
        {
            ResolveReferences();
            if (applyStyleOnAwake)
            {
                ApplyStyle();
            }

            ShowDefault();
        }

        private void OnEnable()
        {
            if (applyStyleOnAwake)
            {
                ApplyStyle();
            }
        }

        /// <summary>
        /// 禁用 / 销毁时收掉补间。补间是按目标绑定的（同一 CanvasGroup.alpha 上只有一条存活），
        /// 若不显式取消，对象池复用或场景切换时会留下"半透明 + 偏移 12px"的残留状态。
        /// </summary>
        private void OnDisable()
        {
            CancelActiveMotions();
            // 下次启用时重新开始，避免带着上次的显示状态跳过入场动画。
            hasState = false;
        }

        public void ApplyStyle()
        {
            ResolveReferences();
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            ApplyCompactLayout();
            if (panel != null)
            {
                panel.theme = resolved;
                panel.variant = GameUiPanelVariant.Dark;
                // 样式归本视图所有：PixelPanel 若保留 applyOnAwake，会在地图 UI 激活时二次
                // 把 PromptText 当标题重刷成 Subtitle/浅色、并把底板换回 panelSprite——
                // 那是「提示栏字色/底板漂移」的另一半根因。置为被动，防再犯。
                panel.applyOnAwake = false;
                // 2026-10-17 保留 hud_plate 贴图，但把九宫格边框压回贴图原始像素宽（方案A）。
                // 可算的原因：贴图 72×28、spriteBorder 4px、PPU 16，画布 referencePPU 100 →
                // 默认边宽 = 4 × 100/16 = 25px，96 高的条子上下各被吃掉 25px，且顶描边落在贴图
                // 第 4 行、底描边落在倒数第 3 行，可见纸面中心比容器中心低约 6px —— 字看着偏上、
                // 下边发空。把 pixelsPerUnitMultiplier 设成 referencePPU/spritePPU（=6.25）后，
                // 边宽回到贴图原始像素宽，上下描边等厚。
                // 注意：这里刻意沿用贴图自带的 4px 边框（不改 spriteBorder），所以贴图中段那两道
                // 内嵌装饰线（金 #E2B24C / 深棕 #5B3B24）会随纵向拉伸被拉开 —— 这是方案A 的预期外观。
                panel.createBorder = false;
                panel.ApplyStyle();
                if (panel.background != null)
                {
                    GameUiStyle.ApplyPanelSprite(panel.background, resolved.promptBarSprite, GameUiStyle.ResolvePanelColor(resolved, GameUiPanelVariant.Dark));
                    ApplyPixelPerfectSlice(panel.background, resolved.promptBarSprite);
                }
            }

            // promptBarSprite 接入 UI_New 后是浅色纸面。沿用原来的浅色字（inverseText）
            // 会变成浅底浅字——整条提示栏看上去就是一条空白横条。以面板是否使用贴图
            // 判断底色深浅，贴图在时改用深色正文色。
            bool lightPanel = resolved.promptBarSprite != null;
            GameUiStyle.ApplyPixelText(promptText, resolved, GameUiTextStyle.HintText, !lightPanel);
            if (promptText != null)
            {
                promptText.alignment = TextAnchor.MiddleCenter;
                // 2026-10-17 用户要求「提示栏字体与游戏其它 UI 一致」→ 改回主题像素字体（FusionPixel）。
                // 2026-10-16 那次改用内置字体，是因为像素字体在 Canvas 非整数缩放（1.1523）下
                // 笔画被双线性采样抽掉、看着像横条纹。这次保留字体一致性，同时把字体图集纹理
                // 设成 Point 过滤——最近邻重采样不会再丢笔画，最多是像素块宽窄略不均。
                // 若实机仍嫌糊：Inspector 里把 usePixelFont 关掉即回到内置字体，不用改代码。
                Font themeFont = usePixelFont && resolved.font != null ? resolved.ResolveFont() : null;
                if (themeFont != null)
                {
                    promptText.font = themeFont;
                    EnsurePixelFontFilter(themeFont);
                }
                else
                {
                    promptText.font = ResolvePromptFont(promptText.font);
                }
                promptText.fontSize = PromptFontSize;
                promptText.lineSpacing = PromptLineSpacing;
                // 两行时不允许被裁：撑高由 FitPanelHeightToText 负责。
                promptText.verticalOverflow = VerticalWrapMode.Overflow;
                promptText.color = lightPanel ? resolved.bodyText : resolved.inverseText;

                // 样式归本视图所有：PromptText 上的 PixelText 若保留 applyOnAwake，会在地图 UI
                // 被激活时（OnEnable）二次应用它自己的 inverse 样式，把这里刚设好的深色文字
                // 覆盖回浅色（浅底浅字 → 看不清）。置为被动，双保险（prefab 里也已关闭）。
                PixelText pixelText = promptText.GetComponent<PixelText>();
                if (pixelText != null)
                {
                    pixelText.applyOnAwake = false;
                }
            }
        }

        public void ShowDefault()
        {
            if (!showDefaultWhenIdle)
            {
                Hide();
                return;
            }

            ShowMessage(MapInteractionPromptFormatter.BuildDefaultPrompt(debugVisible));
        }

        /// <summary>
        /// 隐藏提示，并清掉「当前显示内容」缓存。
        ///
        /// 只调 SetVisible(false) 不清缓存会连锁出两个故障（2026-10-23 实测）：
        /// 1) visibleState 仍为 true → <see cref="FinishHide"/> 开头就 return，
        ///    面板淡出后不会被 SetActive(false)，留一条 alpha=0 的隐形横条；
        /// 2) lastMessage 还是上一个目标 → 再靠近同一目标时 <see cref="ShowMessage"/>
        ///    的幂等判断命中，直接 return，既不更新文字也不重新显示。
        /// 两者叠加，表现就是「离开后再靠近，提示再也不弹」。
        /// </summary>
        public void Hide()
        {
            hasState = false;
            lastMessage = null;
            SetVisible(false);
        }

        public void ShowTarget(MapInteractionTarget target)
        {
            if (target == null)
            {
                ShowDefault();
                return;
            }

            ShowMessage(BuildPromptForTarget(target));
        }

        public void ShowMessage(string message)
        {
            bool visible = !string.IsNullOrEmpty(message);

            // 同一状态重复调用直接返回：RefreshTarget 每帧都会走到这里，
            // 内容没变就不该重播动效，否则提示栏会一直"抖动"。
            if (hasState && visibleState == visible && lastMessage == message)
            {
                return;
            }

            hasState = true;
            visibleState = visible;
            lastMessage = message;

            if (promptText != null)
            {
                promptText.text = message;
                FitPanelHeightToText();
            }

            SetVisible(visible);
        }

        /// <summary>
        /// 横条高度随文字行数自适应：按「文字行高 + 上下留白」算，再夹到 [96, 136]。
        /// 高度始终是偶数像素，避免在 Canvas 非整数缩放（1.1523）下出现半像素导致的字体发虚。
        /// </summary>
        private void FitPanelHeightToText()
        {
            RectTransform rect = panelRoot == null ? transform as RectTransform : panelRoot.transform as RectTransform;
            if (rect == null || promptText == null)
            {
                return;
            }

            float needed = promptText.preferredHeight + PromptBarPadding * 2f;
            needed = Mathf.Clamp(needed, PromptBarMinHeight, PromptBarMaxHeight);
            // 取偶：半像素会让内置字体的边缘在非整数缩放下发灰。
            needed = Mathf.Round(needed * 0.5f) * 2f;
            rect.sizeDelta = new Vector2(rect.sizeDelta.x, needed);
        }

        public static string BuildPromptForTarget(MapInteractionTarget target)
        {
            if (target == null)
            {
                return MapInteractionPromptFormatter.BuildDefaultPrompt(false);
            }

            if (!string.IsNullOrEmpty(target.promptText))
            {
                return target.promptText;
            }

            if (target.isExitPortal)
            {
                return MapInteractionPromptFormatter.BuildExitPrompt();
            }

            // 剧情对话优先：不消耗时间，提示里也不该出现「消耗 N 小时」。
            if (target.dialogue != null)
            {
                string npcName = string.IsNullOrEmpty(target.displayName) ? "对方" : target.displayName;
                return "按 E：与 " + npcName + " 交谈";
            }

            ActivityData activity = ResolveActivity(target);
            if (activity != null)
            {
                return MapInteractionPromptFormatter.BuildActivityPrompt(
                    ResolveDisplayName(target, activity.displayName),
                    activity.timeCost,
                    MapInteractionPromptFormatter.BuildRewardPreview(activity));
            }

            LocationActionData action = target.locationAction;
            if (action != null)
            {
                return MapInteractionPromptFormatter.BuildActivityPrompt(
                    ResolveDisplayName(target, action.displayName),
                    action.timeCost,
                    MapInteractionPromptFormatter.BuildRewardPreview(action));
            }

            return MapInteractionPromptFormatter.BuildUnavailablePrompt(target.displayName);
        }

        private void SetVisible(bool visible)
        {
            // 状态维护收敛到这里：FinishHide 靠 visibleState 判断「淡出途中是否又被显示」，
            // ShowMessage 的幂等判断也读它。谁调 SetVisible 谁就不必再自己记一遍。
            visibleState = visible;

            if (canvasGroup != null)
            {
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }

            if (visible)
            {
                BeginShow();
            }
            else
            {
                BeginHide();
            }
        }

        // ------------------------------------------------------------------
        // 入场 / 出场动效
        // 三个要点：
        // 1) 用 MotionScheduler.UpdateIgnoreTimeScale。本工程暂停/设置菜单会把
        //    Time.timeScale 置 0，默认调度器走 scaled time，提示栏在暂停界面里
        //    会"卡在半透明"永远播不完。
        // 2) **每次起新补间前必须先取消旧的**。LitMotion 的核心 `Bind` 不会自动
        //    取消绑定同一目标的前一条（见 MotionBuilder.Bind：只是 SetCallbackData
        //    + ScheduleMotion）。不取消的话，往返切换目标时两条补间会并行 Lerp
        //    同一个 alpha / anchoredPosition，画面表现为抖动或停在中间值。
        //    （早先注释写的"按目标绑定天然互斥"是错的，2026-10-20 实测纠正。）
        // 3) 因此句柄不是"记录"而是"责任"：谁起补间谁负责取消。
        //
        // 注意：这里刻意**不使用 LitMotion.Extensions** 的 BindToAlpha /
        // BindToAnchoredPositionY。原因：LitMotion.Extensions 通过 versionDefines
        // 判定本工程满足「com.unity.ugui >= 2.0.0」→ 定义 LITMOTION_SUPPORT_TMP，
        // 其内部引用了 Unity.TextMeshPro 的 TMP_Text。一旦引用该程序集，
        // StudentGrowth.Runtime 就必须连带引用 Unity.TextMeshPro，否则报
        // CS0012: The type 'TMP_Text' is defined in an assembly that is not referenced。
        // 本工程用 uGUI 的 Text、并没有用 TMP，所以直接在核心库上 Bind，
        // 少一层无关依赖（代价只是下面这几个 lambda）。
        // ------------------------------------------------------------------

        private void BeginShow()
        {
            CancelActiveMotions();
            EnsureVisibleRoot(true);

            if (canvasGroup != null)
            {
                showMotion = LMotion.Create(canvasGroup.alpha, 1f, ShowDuration)
                    .WithEase(Ease.OutQuad)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(canvasGroup, static (x, target) => target.alpha = x);
            }

            RectTransform rect = ResolvePanelRect();
            if (rect != null)
            {
                Vector2 start = new Vector2(rect.anchoredPosition.x, baseAnchoredY - SlideOffsetY);
                Vector2 end = new Vector2(rect.anchoredPosition.x, baseAnchoredY);
                rect.anchoredPosition = start;
                slideMotion = LMotion.Create(start, end, ShowDuration)
                    .WithEase(Ease.OutQuad)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(rect, static (x, target) => target.anchoredPosition = x);
            }
        }

        private void BeginHide()
        {
            // 原实现是直接 SetActive(false)，这里必须留到补间结束后再关，否则动画不可见。
            if (canvasGroup == null)
            {
                EnsureVisibleRoot(false);
                return;
            }

            CancelActiveMotions();
            EnsureVisibleRoot(true);
            showMotion = LMotion.Create(canvasGroup.alpha, 0f, HideDuration)
                .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                .WithOnComplete(FinishHide)
                .Bind(canvasGroup, static (x, target) => target.alpha = x);

            RectTransform rect = ResolvePanelRect();
            if (rect != null)
            {
                Vector2 start = new Vector2(rect.anchoredPosition.x, rect.anchoredPosition.y);
                Vector2 end = new Vector2(rect.anchoredPosition.x, baseAnchoredY - SlideOffsetY);
                slideMotion = LMotion.Create(start, end, HideDuration)
                    .WithEase(Ease.InQuad)
                    .WithScheduler(MotionScheduler.UpdateIgnoreTimeScale)
                    .Bind(rect, static (x, target) => target.anchoredPosition = x);
            }
        }

        /// <summary>
        /// 淡出结束才真正藏起来。回调里要防"淡出途中又被显示"：
        /// 那时 visibleState 已被翻成 true，不能再关掉，否则提示栏会整条消失。
        /// </summary>
        private void FinishHide()
        {
            if (visibleState || panelRoot == null)
            {
                return;
            }

            panelRoot.SetActive(false);
        }

        private void EnsureVisibleRoot(bool visible)
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
            else if (promptText != null)
            {
                promptText.gameObject.SetActive(visible);
            }
        }

        private RectTransform ResolvePanelRect()
        {
            if (panelRoot != null)
            {
                return panelRoot.transform as RectTransform;
            }

            return transform as RectTransform;
        }

        private void CancelActiveMotions()
        {
            // 只取消、不归位：接续的补间要**从当前值**起步（中途切目标时表现为平滑衔接）。
            // 归位交给各条补间自己的终值——比如隐藏态的位置由 BeginHide 的终点负责。
            if (showMotion.IsActive())
            {
                showMotion.Cancel();
            }

            if (slideMotion.IsActive())
            {
                slideMotion.Cancel();
            }
        }

        private void ResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panel == null && panelRoot != null)
            {
                panel = panelRoot.GetComponent<PixelPanel>();
                if (panel == null)
                {
                    Image image = panelRoot.GetComponent<Image>();
                    if (image == null)
                    {
                        image = panelRoot.AddComponent<Image>();
                        image.raycastTarget = false;
                    }

                    panel = panelRoot.AddComponent<PixelPanel>();
                    panel.background = image;
                }
            }

            if (canvasGroup == null && panelRoot != null)
            {
                canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panelRoot);
            }

            if (promptText == null && panelRoot != null)
            {
                // 兜底：引用万一丢失，从子节点找回唯一的 Text（提示栏只有一条文字），
                // 保证「横条在、字不在」这类空白提示栏不会再出现。
                promptText = panelRoot.GetComponentInChildren<Text>(true);
            }
        }

        private static Font cachedPromptFont;

        private static Font filteredPixelFont;

        /// <summary>
        /// 像素字体的图集纹理用 Point 过滤：非整数缩放下双线性会把 1px 笔画抽淡，
        /// 最近邻只是像素块宽窄不均，不会丢笔画。动态字体图集重建后会换纹理，
        /// 所以订阅 Font.textureRebuilt 重新贴一次设置。
        /// </summary>
        private static void EnsurePixelFontFilter(Font font)
        {
            if (font == null)
            {
                return;
            }

            if (filteredPixelFont != font)
            {
                filteredPixelFont = font;
                Font.textureRebuilt -= OnPixelFontTextureRebuilt;
                Font.textureRebuilt += OnPixelFontTextureRebuilt;
            }

            ApplyPixelFontFilter(font);
        }

        private static void OnPixelFontTextureRebuilt(Font font)
        {
            if (font == filteredPixelFont)
            {
                ApplyPixelFontFilter(font);
            }
        }

        private static void ApplyPixelFontFilter(Font font)
        {
            Material material = font == null ? null : font.material;
            if (material != null && material.mainTexture != null)
            {
                material.mainTexture.filterMode = FilterMode.Point;
            }
        }

        /// <summary>
        /// 提示栏专用字体：内置字体（Unity 2022+ 为 LegacyRuntime.ttf，旧版为 Arial.ttf）。
        /// 存在的意义见 ApplyStyle 中的说明——像素字体在非整数缩放下的渲染残破问题。
        /// </summary>
        private static Font ResolvePromptFont(Font fallback)
        {
            if (cachedPromptFont != null)
            {
                return cachedPromptFont;
            }

            Font builtin = null;
            try
            {
                builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            catch
            {
            }

            if (builtin == null)
            {
                try
                {
                    builtin = Resources.GetBuiltinResource<Font>("Arial.ttf");
                }
                catch
                {
                }
            }

            cachedPromptFont = builtin != null ? builtin : fallback;
            return cachedPromptFont;
        }

        private void ApplyCompactLayout()
        {
            RectTransform rootRect = panelRoot == null ? transform as RectTransform : panelRoot.transform as RectTransform;
            if (rootRect != null)
            {
                rootRect.anchorMin = new Vector2(0.5f, 0f);
                rootRect.anchorMax = new Vector2(0.5f, 0f);
                rootRect.pivot = new Vector2(0.5f, 0f);
                // 底边距屏底 64（2026-10-17 用户反馈「遮挡视野」下调，原 92）。
                // 下限由底部按键提示条决定：ActionHintBarView 运行时实测 rect = 360×44，
                // anchoredPosition(18,18)、pivot(0,0) → 顶边在 y=62。提示条宽 360 靠左，
                // 与本条（640 居中）在 x 上只有右侧一小段相邻，留 2px 竖向间隙即可不相撞。
                rootRect.anchoredPosition = new Vector2(0f, 64f);
                rootRect.sizeDelta = new Vector2(PromptBarWidth, PromptBarMinHeight);
                // 布局定的 Y 就是动效基准：出入场补间一律以它为终点，
                // 不读取运行中的 anchoredPosition（那会把动画途中值当基准，逐次累积偏移）。
                baseAnchoredY = rootRect.anchoredPosition.y;
            }

            if (promptText != null)
            {
                // 上下留白与左右留白取同一数值（32），并让文字区域严格居中：
                // 先整块按下内边距内缩，再靠 MiddleCenter 垂直居中，
                // 这样单行 / 两行都能保持「上留白 = 下留白」。
                RectTransform textRect = promptText.rectTransform;
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.offsetMin = new Vector2(PromptBarPadding, PromptBarPadding);
                textRect.offsetMax = new Vector2(-PromptBarPadding, -PromptBarPadding);
            }
        }

        /// <summary>
        /// 把九宫格边框恢复成贴图原始像素宽。Unity 的 UI 边宽 = spriteBorder /
        /// (pixelsPerUnitMultiplier × spritePPU / canvas.referencePixelsPerUnit)；
        /// 像素贴图 PPU 通常是 16 而画布是 100，不修正的话边框会被放大 6.25 倍。
        /// </summary>
        private static void ApplyPixelPerfectSlice(Image target, Sprite sprite)
        {
            if (target == null)
            {
                return;
            }

            if (sprite == null)
            {
                target.pixelsPerUnitMultiplier = 1f;
                return;
            }

            Canvas canvas = target.canvas;
            float referencePPU = canvas != null && canvas.referencePixelsPerUnit > 0f
                ? canvas.referencePixelsPerUnit
                : 100f;
            float spritePPU = sprite.pixelsPerUnit > 0f ? sprite.pixelsPerUnit : 100f;
            target.pixelsPerUnitMultiplier = Mathf.Max(0.01f, referencePPU / spritePPU);
        }

        private static ActivityData ResolveActivity(MapInteractionTarget target)
        {
            if (target == null)
            {
                return null;
            }

            if (target.fallbackActivity != null)
            {
                return target.fallbackActivity;
            }

            return target.locationAction == null ? null : target.locationAction.linkedActivity;
        }

        private static string ResolveDisplayName(MapInteractionTarget target, string fallback)
        {
            if (target != null && !string.IsNullOrEmpty(target.displayName))
            {
                return target.displayName;
            }

            return string.IsNullOrEmpty(fallback) ? "互动" : fallback;
        }

    }
}
