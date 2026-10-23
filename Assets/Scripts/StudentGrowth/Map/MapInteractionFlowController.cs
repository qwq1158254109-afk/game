using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public enum MapInteractionFlowState
    {
        Idle,
        Nearby,
        Confirming,
        Executing,
        Completed,
        Locked,
        Debug
    }

    public sealed class MapInteractionFlowController : MonoBehaviour
    {
        private const string HeaderColor = "#5B3B24";
        private const string LabelColor = "#8F7650";
        private const string ValueColor = "#342216";
        private const string HintColor = "#6D3E24";

        public MapInteractionController interactionController;
        public MapInteractionPromptView promptView;
        public MapDebugOverlayView debugOverlay;
        public SceneTransitionController sceneTransitionController;
        public GameSessionState sessionState;
        public MapDialogueView dialogueView;
        public MapShopView shopView;
        public MapInteractionConfirmView confirmView;
        public MapActivityResultView resultView;
        public bool requireConfirmForActivities = true;
        public bool debugVisible;
        public MapInteractionStateVisual[] stateVisuals = new MapInteractionStateVisual[0];

        public UIThemeConfig theme;
        public GameObject confirmPanel;
        public PixelPanel confirmPanelStyle;
        public CanvasGroup confirmCanvasGroup;
        public Text confirmTitleText;
        public Text confirmBodyText;
        public Button confirmButton;
        public Button cancelButton;
        public Image confirmButtonIcon;
        public Image cancelButtonIcon;

        public MapInteractionFlowState CurrentState { get; private set; } = MapInteractionFlowState.Idle;

        private readonly List<string> completedInteractionKeys = new List<string>();
        private MapInteractionTarget currentTarget;
        private MapInteractionTarget pendingTarget;
        private float promptHoldUntil;
        // 「本阶段已完成」提示的保持期。
        private MapInteractionTarget completedHoldTarget;
        private float completedHoldUntil;
        /// <summary>
        /// 刚交互过的目标 + 抑制截止时刻。
        ///
        /// 2026-10-21（用户第二次报"交互之后提示栏一直存在"）：
        ///   只藏 2.2 秒是不够的 —— 交互后目标进入 Nearby（可重复交互的点位
        ///   `IsCompleted` 恒为 false），下一帧 `ShowTarget()` 就把行动提示**又**显示出来，
        ///   于是看起来"提示栏一直在"。
        ///   现在改成：交互过的目标，在玩家**离开它之前**提示栏保持隐藏；
        ///   走开再回来才重新显示。这才是"交互后跟着淡化消失"的语义。
        /// </summary>
        private MapInteractionTarget suppressPromptTarget;
        private bool promptSuppressed;
        private bool legacyInputBlocked;
        private int confirmStartedFrame = -1;
        /// <summary>选项面板里选中的分支；执行完立即清空，避免影响下一次交互。</summary>
        private EventChoiceData pendingChoice;

        private void Awake()
        {
            ResolveReferences();
            BindConfirmViewActions();
            HideConfirm();
            ApplyConfirmStyle();
            ApplyVisuals();
        }

        private void Start()
        {
            ResolveReferences();
            CreateConfirmUiIfNeeded();
            BindConfirmViewActions();
            HideConfirm();
            ApplyConfirmStyle();
            ApplyVisuals();
        }

        private void Update()
        {
            if (GameplayInputGate.IsBlocked)
            {
                return;
            }

            if (IsKeyPressed(KeyCode.F3, "f3Key"))
            {
                ToggleDebug();
            }

            if (CurrentState != MapInteractionFlowState.Confirming || Time.frameCount <= confirmStartedFrame)
            {
                return;
            }

            if (IsKeyPressed(KeyCode.Escape, "escapeKey"))
            {
                CancelPending();
            }
            else if (IsKeyPressed(KeyCode.Return, "enterKey")
                || IsKeyPressed(KeyCode.KeypadEnter, "numpadEnterKey")
                || IsKeyPressed(KeyCode.E, "eKey"))
            {
                ExecutePending();
            }
        }

        public void RefreshTarget(MapInteractionTarget target)
        {
            if (CurrentState == MapInteractionFlowState.Confirming
                || CurrentState == MapInteractionFlowState.Executing)
            {
                return;
            }

            currentTarget = target;
            if (target == null)
            {
                SetState(debugVisible ? MapInteractionFlowState.Debug : MapInteractionFlowState.Idle);
                // 临时提示保持期内不覆盖（“次数已用完/执行结果”等消息需要 2 秒可读窗口，
                // 而本方法由玩家控制器每帧调用，不设保持期会把消息一帧冲掉）。
                if (promptView != null && Time.unscaledTime >= promptHoldUntil)
                {
                    promptView.ShowDefault();
                }

                // 玩家已离开交互物 → 解除抑制，下次靠近重新显示提示。
                ClearPromptSuppression();
                ApplyVisuals();
                return;
            }

            if (IsCompleted(target) && BattleMapBridge.GetDeskActivity(target)==null)
            {
                SetState(MapInteractionFlowState.Completed);

                if (completedHoldTarget == target && Time.unscaledTime >= completedHoldUntil)
                {
                    // 保持期已过且仍在原地 → 隐藏（保持期本身不重置，因此只淡出一次）。
                    SuppressPromptFor(target);
                    ApplyVisuals();
                    return;
                }

                if (Time.unscaledTime >= promptHoldUntil && completedHoldTarget != target)
                {
                    completedHoldTarget = target;
                    completedHoldUntil = Time.unscaledTime + 2.2f;
                    ShowPrompt("本阶段已完成：" + ResolveDisplayName(target) + "。");
                }

                ApplyVisuals();
                return;
            }

            // 换了目标 / 走开 / 它又能交互了 → 重置「已完成」保持期，下次靠近还能看到一次提醒。
            completedHoldTarget = null;
            SetState(debugVisible ? MapInteractionFlowState.Debug : MapInteractionFlowState.Nearby);

            // 刚交互过、人还没走 → 提示栏保持隐藏（只淡出一次，之后每帧提前返回）。
            if (suppressPromptTarget == target)
            {
                SuppressPromptFor(target);
                ApplyVisuals();
                return;
            }

            if (promptView != null && Time.unscaledTime >= promptHoldUntil)
            {
                promptView.ShowTarget(target);
            }

            ApplyVisuals();
        }

        /// <summary>把提示栏藏起来（清空文案 → SetVisible(false) → BeginHide 淡出）。只做一次。</summary>
        private void SuppressPromptFor(MapInteractionTarget target)
        {
            if (promptSuppressed && suppressPromptTarget == target)
            {
                return;
            }

            suppressPromptTarget = target;
            promptSuppressed = true;
            if (promptView != null)
            {
                promptView.ShowMessage(string.Empty);
            }
        }

        private void ClearPromptSuppression()
        {
            suppressPromptTarget = null;
            promptSuppressed = false;
        }

        public bool TryBeginInteraction(MapInteractionTarget target)
        {
            ResolveReferences();
            if (target == null)
            {
                return false;
            }

            if (CurrentState == MapInteractionFlowState.Executing)
            {
                return true;
            }

            if (CurrentState == MapInteractionFlowState.Confirming)
            {
                return true;
            }

            if (IsCompleted(target) && BattleMapBridge.GetDeskActivity(target)==null)
            {
                SetState(MapInteractionFlowState.Completed);

                if (completedHoldTarget == target && Time.unscaledTime >= completedHoldUntil)
                {
                    // 保持期已过且仍在原地 → 隐藏。
                    SuppressPromptFor(target);
                    ApplyVisuals();
                    return true;
                }

                if (Time.unscaledTime >= promptHoldUntil && completedHoldTarget != target)
                {
                    completedHoldTarget = target;
                    completedHoldUntil = Time.unscaledTime + 2.2f;
                    ShowPrompt("本阶段已完成：" + ResolveDisplayName(target) + "。");
                }

                ApplyVisuals();
                return true;
            }

            if (target.isExitPortal || IsExitPoint(target))
            {
                ExecuteTarget(target);
                return true;
            }

            // 事件类活动且可选项 ≥2 时，先弹选项面板；单选事件仍走原来的确认/执行。
            if (TryBeginEventChoice(target))
            {
                return true;
            }

            if (requireConfirmForActivities && IsActivityTarget(target))
            {
                ShowConfirm(target);
                return true;
            }

            ExecuteTarget(target);
            return true;
        }

        public void ConfirmPending()
        {
            if (Time.frameCount <= confirmStartedFrame)
            {
                return;
            }

            ExecutePending();
        }

        public void CancelPending()
        {
            pendingTarget = null;
            pendingChoice = null;
            if (MapEventChoiceView.Instance != null && MapEventChoiceView.Instance.IsOpen)
            {
                MapEventChoiceView.Instance.Close();
            }
            HideConfirm();
            SetState(currentTarget == null ? MapInteractionFlowState.Idle : MapInteractionFlowState.Nearby);
            // 取消 = 放弃这次确认，提示栏要恢复（ShowConfirm 里为了收起它做过抑制）。
            ClearPromptSuppression();
            if (currentTarget != null && promptView != null)
            {
                promptView.ShowTarget(currentTarget);
            }
            else if (promptView != null)
            {
                promptView.ShowDefault();
            }

            ApplyVisuals();
        }

        public void ToggleDebug()
        {
            debugVisible = !debugVisible;
            if (debugOverlay != null)
            {
                debugOverlay.SetVisible(debugVisible);
            }

            if (promptView != null)
            {
                promptView.debugVisible = debugVisible;
            }

            if (CurrentState != MapInteractionFlowState.Confirming
                && CurrentState != MapInteractionFlowState.Executing)
            {
                SetState(debugVisible ? MapInteractionFlowState.Debug : currentTarget == null ? MapInteractionFlowState.Idle : MapInteractionFlowState.Nearby);
            }

            ApplyVisuals();
        }

        /// <summary>
        /// 事件活动如果有 2 个以上满足条件的选项，先弹选项面板让玩家选。
        /// 返回 true 表示已经接管这次交互（面板已打开）。
        /// </summary>
        private bool TryBeginEventChoice(MapInteractionTarget target)
        {
            ActivityData activity = ResolveActivity(target);
            if (activity == null || activity.activityType != ActivityType.Event || activity.linkedEvent == null)
            {
                return false;
            }

            GameSessionState state = ResolveSessionState();
            if (state == null || state.playerState == null)
            {
                return false;
            }

            List<EventChoiceData> eligible = new List<EventChoiceData>();
            foreach (EventChoiceData choice in activity.linkedEvent.choices)
            {
                if (choice != null
                    && EventConditionResolver.AreConditionsMet(choice.requirements, state.playerState))
                {
                    eligible.Add(choice);
                }
            }

            if (eligible.Count < 2)
            {
                return false;
            }

            pendingTarget = target;
            confirmStartedFrame = Time.frameCount;
            SetState(MapInteractionFlowState.Confirming);
            if (resultView != null)
            {
                resultView.Hide();
            }

            // 面板带全屏遮罩，底部行动提示栏必须收起，否则「按 E：询问图书管理员」会从遮罩下透出来。
            SuppressPromptFor(target);

            // 面板本身替代了确认框，所以「消耗多少行动」得写进面板，不然玩家是盲选。
            string body = activity.linkedEvent.description;
            string costText = MapInteractionPromptFormatter.FormatTimeCost(
                ResolveTimeCost(target.locationAction, activity));
            if (!string.IsNullOrEmpty(costText))
            {
                body = string.IsNullOrEmpty(body) ? costText : body + "\n" + costText;
            }

            MapEventChoiceView view = MapEventChoiceView.Ensure();
            view.Open(ResolveDisplayName(target), body, eligible,
                OnEventChoicePicked, CancelPending);
            return true;
        }

        private void OnEventChoicePicked(EventChoiceData choice)
        {
            MapInteractionTarget target = pendingTarget;
            pendingTarget = null;
            if (target == null)
            {
                pendingChoice = null;
                CancelPending();
                return;
            }

            // 与 ExecutePending 走同一道门禁：面板打开期间次数可能已被别的入口用掉，
            // 不能等玩家选完分支才发现「本阶段已用完」。
            if (IsCompleted(target))
            {
                pendingChoice = null;
                promptHoldUntil = Time.unscaledTime + 2.2f;
                CancelPending();
                ShowPrompt("该活动本阶段次数已用完，请选择其他行动。");
                return;
            }

            pendingChoice = choice;
            ExecuteTarget(target);
        }

        private void ShowConfirm(MapInteractionTarget target)
        {
            pendingTarget = target;
            confirmStartedFrame = Time.frameCount;
            SetState(MapInteractionFlowState.Confirming);
            CreateConfirmUiIfNeeded();
            // 互斥：打开确认面板时收起「行动完成」结果面板，避免两层面板同屏叠加（2026-10-14 用户截图：书桌+行动完成同屏）。
            if (resultView != null)
            {
                resultView.Hide();
            }
            if (confirmView != null)
            {
                confirmView.Show(ResolveDisplayName(target), BuildConfirmBody(target));
                var battleOption=BattleMapBridge.GetDeskActivity(target);
                var battleRequestId=Guid.NewGuid().ToString("N");
                var battleSessionId=GameApplicationRuntime.CurrentRuntimeSessionId;
                // 「确认」不再按次数禁用：用完时保持可点，由 ExecutePending 弹提示
                // （原先禁用会让按钮静默失效——「点了没反应」的主要来源）；此处显式恢复可点，
                // 防止上一次打开留下的禁用态残留。
                if(confirmView.confirmButton!=null)confirmView.confirmButton.interactable=true;
                var strategyOption=BattleMapBridge.GetStrategyActivity(target);
                var strategyRequestId=Guid.NewGuid().ToString("N");
                if(strategyOption!=null)
                {
                    var original=ResolveActivity(target);
                    string cost=MapInteractionPromptFormatter.FormatTimeCost(ResolveTimeCost(target.locationAction,original));
                    string rewards=ResolveRewardPreview(target.locationAction,original);
                    // 文案重设放在选项按钮布局之前；正文用主题默认正文色（深棕）——
                    // 原先的 <color=#F4E9D0> 是为深色底设计的，浅纸面板下「浅底浅字」看不清。
                    confirmView.Show("书桌 · 选择行动","确认：整理计划\n"+cost+"；获得："+rewards
                        +"\n\n策略练习：借用 10 张练习牌（6 种）\n消耗 1 时间；胜利数学 +2。练习牌不带出。"
                        +"\n\n习题挑战：使用当前永久牌组\n消耗 1 时间；胜利数学 +2。");
                }
                confirmView.SetBattleOption(battleOption==null?null:()=>
                {
                    if(battleSessionId!=GameApplicationRuntime.CurrentRuntimeSessionId)return;
                    var request=target.interactionPoint.CreateExecutionRequest(LocationActionSource.Map);
                    request.activity=battleOption;request.requestId=battleRequestId;
                    CancelPending();
                    var result=interactionController.locationActionExecutor.TryExecute(request);
                    ShowPrompt(result.message);
                });
                // SetStrategyOption 放到最后：它对面板/正文做按钮区（+140/+96）的高度补偿，
                // 若之后再有 Show（内部 FitBodyHeight）会覆盖补偿，导致正文压住按钮。
                confirmView.SetStrategyOption(strategyOption==null?null:()=>
                {
                    if(battleSessionId!=GameApplicationRuntime.CurrentRuntimeSessionId)return;
                    var request=target.interactionPoint.CreateExecutionRequest(LocationActionSource.Map);
                    request.activity=strategyOption;request.requestId=strategyRequestId;
                    CancelPending();
                    var result=interactionController.locationActionExecutor.TryExecute(request);
                    ShowPrompt(result.message);
                });
            }
            else
            {
                if (confirmPanel != null)
                {
                    confirmPanel.SetActive(true);
                }

                if (confirmCanvasGroup != null)
                {
                    confirmCanvasGroup.alpha = 1f;
                    confirmCanvasGroup.interactable = true;
                    confirmCanvasGroup.blocksRaycasts = true;
                }

                SetText(confirmTitleText, ResolveDisplayName(target));
                SetText(confirmBodyText, BuildConfirmBody(target));
            }

            ApplyConfirmStyle();
            // 2026-10-21 用户报「交互之后底部的提示栏还在」：
            //   原先这里写 ShowPrompt("确认执行：…")，等于把提示栏**重新点亮**。
            //   而确认面板自己已经把「确认执行：xxx」写在面板正文里了（见 confirmView.Show），
            //   底部再来一条是重复信息，还会一直挂到玩家确认/取消为止。
            //   现在：确认面板打开 = 提示栏收起，信息只在面板里出现。
            SuppressPromptFor(target);
            ApplyVisuals();
        }

        private void ExecutePending()
        {
            if (pendingTarget == null)
            {
                CancelPending();
                return;
            }

            // The desk can reopen for its repeatable battle option. Keyboard confirmation must
            // still respect the original planning activity's per-stage use limit (GDD §5.2).
            if(IsCompleted(pendingTarget))
            {
                promptHoldUntil = Time.unscaledTime + 2.2f;
                CancelPending();
                ShowPrompt("该活动本阶段次数已用完，请选择其他行动。");
                return;
            }

            MapInteractionTarget target = pendingTarget;
            pendingTarget = null;
            ExecuteTarget(target);
        }

        private void ExecuteTarget(MapInteractionTarget target)
        {
            HideConfirm();
            if (Application.isPlaying && TryPlayDoorAnimationBeforeExecution(target))
            {
                return;
            }

            ExecuteTargetImmediate(target);
        }

        private bool TryPlayDoorAnimationBeforeExecution(MapInteractionTarget target)
        {
            if (target == null || (!target.isExitPortal && !IsExitPoint(target)))
            {
                return false;
            }

            MapDoorAnimator doorAnimator = target.GetComponent<MapDoorAnimator>();
            if (doorAnimator == null)
            {
                doorAnimator = target.GetComponentInChildren<MapDoorAnimator>(true);
            }

            if (doorAnimator == null || !doorAnimator.HasOpenAnimation)
            {
                return false;
            }

            if (doorAnimator.IsPlaying)
            {
                return true;
            }

            SetState(MapInteractionFlowState.Executing);
            ShowPrompt(string.IsNullOrEmpty(doorAnimator.openingPromptText) ? "开门中……" : doorAnimator.openingPromptText);
            ApplyVisuals();
            return doorAnimator.TryPlayOpenThen(() =>
            {
                ExecuteTargetImmediate(target);
                if (doorAnimator != null)
                {
                    doorAnimator.ResetToClosed();
                }
            });
        }

        private void ExecuteTargetImmediate(MapInteractionTarget target)
        {
            if (interactionController == null)
            {
                pendingChoice = null;
                ShowPrompt("互动控制器未初始化。");
                SetState(MapInteractionFlowState.Locked);
                ApplyVisuals();
                return;
            }

            SetState(MapInteractionFlowState.Executing);
            LocationActionExecutionResult result = interactionController.TryInteract(target, pendingChoice);
            pendingChoice = null;
            bool exit = target != null && (target.isExitPortal || target.unlimitedUses || IsExitPoint(target));
            if (result != null && result.success)
            {
                if (!exit && !result.shouldOpenBattle)
                {
                    MarkCompleted(target);
                    // 交互过的目标：结果提示淡出后**保持隐藏**，直到玩家离开它。
                    // （可重复交互的点位 IsCompleted 恒为 false，不抑制的话下一帧就会
                    //  重新显示行动提示，表现为"提示栏一直在"。）
                    suppressPromptTarget = target;
                    promptSuppressed = false;
                }

                SetState(exit ? MapInteractionFlowState.Idle : MapInteractionFlowState.Completed);
                // 执行结果提示给 2.2 秒保持期，不被下一帧的面向刷新覆盖。
                // 注意：抑制（suppress）只影响**下一帧起**的显示，这句结果提示必须照常出现，
                // 否则玩家看不到"做了什么/得到什么"。
                promptHoldUntil = Time.unscaledTime + 2.2f;
                ShowPrompt(result.message);
                RefreshHudIfNeeded(result);
                ShowShopIfNeeded(result);
                ShowDialogueIfNeeded(result);
                ShowActivityResultIfNeeded(result, exit);
            }
            else
            {
                SetState(MapInteractionFlowState.Locked);
                ShowPrompt(result == null || string.IsNullOrEmpty(result.message) ? "互动无法执行。" : result.message);
            }

            ApplyVisuals();
        }

        private void MarkCompleted(MapInteractionTarget target)
        {
            if (!ShouldTrackCompletion(target))
            {
                return;
            }

            string key = GetInteractionKey(target);
            if (!string.IsNullOrEmpty(key) && !completedInteractionKeys.Contains(key))
            {
                completedInteractionKeys.Add(key);
            }

            GameSessionState state = ResolveSessionState();
            if (state == null || state.locationState == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            if (!state.locationState.actionUseCount.ContainsKey(key))
            {
                state.locationState.actionUseCount[key] = 0;
            }

            state.locationState.actionUseCount[key]++;
        }

        /// <summary>
        /// 本阶段是否已用尽次数（= 不可再执行）。GDD §5.2：执行门禁看「本阶段次数」——
        /// 计数由 StageController.StartStage 在进阶段时清空，上限取 maxUsesPerStage
        /// （负数 = 不限次数，如睡觉）。
        /// </summary>
        private bool IsCompleted(MapInteractionTarget target)
        {
            if (!ShouldTrackCompletion(target))
            {
                return false;
            }

            string key = GetInteractionKey(target);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            int maxUses = ResolveMaxUsesPerStage(target);
            if (maxUses < 0)
            {
                return false;
            }

            GameSessionState state = ResolveSessionState();
            if (state != null
                && state.locationState != null
                && state.locationState.actionUseCount.TryGetValue(key, out int count))
            {
                return count >= maxUses;
            }

            // 计数不可用（sessionState 未挂载）时的兜底：本次运行内记录过即视为完成。
            return completedInteractionKeys.Contains(key);
        }

        /// <summary>
        /// 读取该互动的每阶段次数上限：优先 LocationActionData（GDD §5.2 指定），
        /// 其次 ActivityData。0 按 1 处理（旧资产兼容）；负数 = 不限次数。
        /// </summary>
        private static int ResolveMaxUsesPerStage(MapInteractionTarget target)
        {
            // 门 / 传送点：不限次数（不写计数，也就谈不上用完）。
            if (target != null && target.unlimitedUses)
            {
                return -1;
            }

            int configured = 0;
            if (target != null && target.locationAction != null)
            {
                configured = target.locationAction.maxUsesPerStage;
            }
            else
            {
                ActivityData activity = ResolveActivity(target);
                if (activity != null)
                {
                    configured = activity.maxUsesPerStage;
                }
            }

            return configured == 0 ? 1 : configured;
        }

        private static bool ShouldTrackCompletion(MapInteractionTarget target)
        {
            if (target == null || target.isExitPortal || target.unlimitedUses || IsExitPoint(target))
            {
                return false;
            }

            ActivityData activity = ResolveActivity(target);
            if (activity == null && target.locationAction == null)
            {
                // 既没有活动也没有 LocationAction 的目标只是占位（例如早期未做完的家具）。
                // 不计数，避免「点一次就提示本阶段已完成」。
                return false;
            }

            return activity == null || activity.activityType != ActivityType.Shop;
        }

        private void ApplyVisuals()
        {
            if (stateVisuals == null)
            {
                return;
            }

            for (int i = 0; i < stateVisuals.Length; i++)
            {
                MapInteractionStateVisual visual = stateVisuals[i];
                if (visual == null)
                {
                    continue;
                }

                MapInteractionTarget target = visual.target;
                bool isCurrent = target != null && target == currentTarget;
                visual.Apply(isCurrent, debugVisible, IsCompleted(target), CurrentState == MapInteractionFlowState.Locked && isCurrent);
            }
        }

        private void ResolveReferences()
        {
            if (interactionController == null)
            {
                interactionController = FindObjectOfType<MapInteractionController>();
            }

            if (promptView == null)
            {
                promptView = FindObjectOfType<MapInteractionPromptView>();
            }

            if (debugOverlay == null)
            {
                debugOverlay = FindObjectOfType<MapDebugOverlayView>();
            }

            if (sceneTransitionController == null)
            {
                sceneTransitionController = FindObjectOfType<SceneTransitionController>();
            }

            if (dialogueView == null)
            {
                dialogueView = FindObjectOfType<MapDialogueView>();
            }

            if (shopView == null)
            {
                shopView = FindObjectOfType<MapShopView>();
            }

            if (confirmView == null)
            {
                confirmView = FindObjectOfType<MapInteractionConfirmView>();
            }

            SyncLegacyConfirmFields();

            if (resultView == null)
            {
                resultView = FindObjectOfType<MapActivityResultView>();
            }

            if (sessionState == null && sceneTransitionController != null)
            {
                sessionState = sceneTransitionController.sessionState;
            }

            if (stateVisuals == null || stateVisuals.Length == 0)
            {
                stateVisuals = FindObjectsOfType<MapInteractionStateVisual>();
            }
        }

        private GameSessionState ResolveSessionState()
        {
            if (sessionState == null && sceneTransitionController != null)
            {
                sessionState = sceneTransitionController.sessionState;
            }

            return sessionState;
        }

        private void BindConfirmViewActions()
        {
            if (confirmView == null)
            {
                return;
            }

            confirmView.BindActions(ConfirmPending, CancelPending);
        }

        private void SyncLegacyConfirmFields()
        {
            if (confirmView == null)
            {
                return;
            }

            confirmPanel = confirmView.panelRoot;
            confirmPanelStyle = confirmView.panelStyle;
            confirmCanvasGroup = confirmView.canvasGroup;
            confirmTitleText = confirmView.titleText;
            confirmBodyText = confirmView.bodyText;
            confirmButton = confirmView.confirmButton;
            cancelButton = confirmView.cancelButton;
            confirmButtonIcon = confirmView.confirmButtonIcon;
            cancelButtonIcon = confirmView.cancelButtonIcon;
        }

        private void BindLegacyConfirmButtons()
        {
            if (confirmButton != null)
            {
                confirmButton.onClick.RemoveListener(ConfirmPending);
                confirmButton.onClick.AddListener(ConfirmPending);
            }

            if (cancelButton != null)
            {
                cancelButton.onClick.RemoveListener(CancelPending);
                cancelButton.onClick.AddListener(CancelPending);
            }
        }

        private void CreateConfirmUiIfNeeded()
        {
            if (confirmView != null)
            {
                confirmView.theme = theme;
                BindConfirmViewActions();
                SyncLegacyConfirmFields();
                ApplyConfirmStyle();
                return;
            }

            if (confirmPanel != null)
            {
                confirmView=confirmPanel.GetComponent<MapInteractionConfirmView>();
                if(confirmView==null)confirmView=confirmPanel.AddComponent<MapInteractionConfirmView>();
                confirmView.panelRoot=confirmPanel;confirmView.panelStyle=confirmPanelStyle;confirmView.canvasGroup=confirmCanvasGroup;
                confirmView.titleText=confirmTitleText;confirmView.bodyText=confirmBodyText;
                confirmView.confirmButton=confirmButton;confirmView.cancelButton=cancelButton;
                confirmView.confirmButtonIcon=confirmButtonIcon;confirmView.cancelButtonIcon=cancelButtonIcon;confirmView.theme=theme;
                BindConfirmViewActions();
                ApplyConfirmStyle();
                return;
            }

            Canvas canvas = MapUiFactory.EnsureRuntimeCanvas();
            RectTransform canvasRect = canvas == null ? null : canvas.transform as RectTransform;
            if (canvasRect == null)
            {
                return;
            }

            confirmView = MapUiFactory.CreateInteractionConfirmView(canvasRect);
            confirmView.theme = theme;
            BindConfirmViewActions();
            SyncLegacyConfirmFields();
            ApplyConfirmStyle();
        }

        private void ShowDialogueIfNeeded(LocationActionExecutionResult result)
        {
            if (result == null || !result.shouldOpenDialogue || result.activityResult == null)
            {
                return;
            }

            CreateDialogueUiIfNeeded();
            if (dialogueView != null)
            {
                dialogueView.theme = theme;
                dialogueView.Show(result.activityResult);
            }
        }

        private void ShowShopIfNeeded(LocationActionExecutionResult result)
        {
            if (result == null || !result.shouldOpenShop)
            {
                return;
            }

            CreateShopUiIfNeeded();
            if (shopView != null)
            {
                shopView.theme = theme;
                shopView.Configure(ResolveSessionState(), FindObjectOfType<FullscreenMapHudView>());
                shopView.Show();
            }
        }

        private void ShowActivityResultIfNeeded(LocationActionExecutionResult result, bool exit)
        {
            if (result == null
                || exit
                || result.activity == null
                || result.shouldOpenShop
                || result.shouldOpenDialogue
                || result.shouldOpenBattle)
            {
                return;
            }

            CreateActivityResultUiIfNeeded();
            if (resultView != null)
            {
                resultView.theme = theme;
                resultView.Show(result);
            }
        }

        private void CreateDialogueUiIfNeeded()
        {
            if (dialogueView != null)
            {
                return;
            }

            RectTransform canvasRect = ResolveCanvasRect();
            if (canvasRect == null)
            {
                return;
            }

            dialogueView = MapUiFactory.CreateDialogueView(canvasRect);
            dialogueView.theme = theme;
            dialogueView.ApplyStyle();
        }

        private void CreateShopUiIfNeeded()
        {
            if (shopView != null)
            {
                return;
            }

            RectTransform canvasRect = ResolveCanvasRect();
            if (canvasRect == null)
            {
                return;
            }

            shopView = MapUiFactory.CreateShopView(canvasRect);
            shopView.theme = theme;
            shopView.ApplyStyle();
        }

        private void CreateActivityResultUiIfNeeded()
        {
            if (resultView != null)
            {
                return;
            }

            RectTransform canvasRect = ResolveCanvasRect();
            if (canvasRect == null)
            {
                return;
            }

            resultView = MapUiFactory.CreateActivityResultView(canvasRect);
            resultView.theme = theme;
            resultView.ApplyStyle();
        }

        private static RectTransform ResolveCanvasRect()
        {
            Canvas canvas = MapUiFactory.EnsureRuntimeCanvas();
            return canvas == null ? null : canvas.transform as RectTransform;
        }

        private void ApplyConfirmStyle()
        {
            if (confirmView != null)
            {
                confirmView.theme = theme;
                confirmView.ApplyStyle();
                SyncLegacyConfirmFields();
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (confirmPanel != null)
            {
                if (confirmPanelStyle == null)
                {
                    confirmPanelStyle = confirmPanel.GetComponent<PixelPanel>();
                    if (confirmPanelStyle == null)
                    {
                        Image image = confirmPanel.GetComponent<Image>();
                        if (image == null)
                        {
                            image = confirmPanel.AddComponent<Image>();
                        }

                        confirmPanelStyle = confirmPanel.AddComponent<PixelPanel>();
                        confirmPanelStyle.background = image;
                    }
                }

                confirmPanelStyle.theme = resolved;
                confirmPanelStyle.variant = GameUiPanelVariant.Standard;
                confirmPanelStyle.ApplyStyle();

                if (confirmCanvasGroup == null)
                {
                    confirmCanvasGroup = UiAnimationHelper.EnsureCanvasGroup(confirmPanel);
                }
            }

            GameUiStyle.ApplyPixelText(confirmTitleText, resolved, GameUiTextStyle.Subtitle);
            GameUiStyle.ApplyPixelText(confirmBodyText, resolved, GameUiTextStyle.Body);
            ApplyConfirmButton(confirmButton, confirmButtonIcon, resolved.confirmIcon);
            ApplyConfirmButton(cancelButton, cancelButtonIcon, resolved.cancelIcon);
        }

        private void ApplyConfirmButton(Button button, Image icon, Sprite iconSprite)
        {
            if (button == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            PixelButton pixelButton = button.GetComponent<PixelButton>();
            if (pixelButton != null)
            {
                pixelButton.theme = theme;
                pixelButton.ApplyStyle();
            }
            else
            {
                GameUiStyle.ApplyButton(button, button.targetGraphic as Image, theme);
                GameUiStyle.ApplyPixelText(button.GetComponentInChildren<Text>(true), theme, GameUiTextStyle.ButtonText, true);
            }

            ApplyIcon(icon, iconSprite, resolved.inverseText);
        }

        private static void ApplyIcon(Image icon, Sprite sprite, Color color)
        {
            if (icon == null)
            {
                return;
            }

            icon.sprite = sprite;
            icon.color = color;
            icon.enabled = sprite != null;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }

        private void HideConfirm()
        {
            if (confirmView != null)
            {
                confirmView.Hide();
                SyncLegacyConfirmFields();
                return;
            }

            if (confirmCanvasGroup != null)
            {
                confirmCanvasGroup.alpha = 0f;
                confirmCanvasGroup.interactable = false;
                confirmCanvasGroup.blocksRaycasts = false;
            }

            if (confirmPanel != null)
            {
                confirmPanel.SetActive(false);
            }
        }

        private void SetState(MapInteractionFlowState state)
        {
            CurrentState = state;
        }

        private void ShowPrompt(string message)
        {
            if (promptView != null)
            {
                promptView.ShowMessage(message);
            }
        }

        private static void RefreshHudIfNeeded(LocationActionExecutionResult result)
        {
            if (result == null || !result.shouldRefreshUi)
            {
                return;
            }

            FullscreenMapHudView hud = FindObjectOfType<FullscreenMapHudView>();
            if (hud != null)
            {
                hud.Refresh();
            }
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static bool IsActivityTarget(MapInteractionTarget target)
        {
            // 房间门虽然也标成 LocationAction（这样能进统一交互系统），但它不是"活动"：
            // 不耗时间、没有次数限制、不需要确认面板。按 E 直接开门。
            if (target != null && target.GetComponent<RoomDoor>() != null)
            {
                return false;
            }

            return target != null
                && !target.isExitPortal
                && (target.fallbackActivity != null
                    || target.locationAction != null
                    || target.interactionPoint == null
                    || target.interactionPoint.interactionType == MapInteractionType.LocationAction);
        }

        private static bool IsExitPoint(MapInteractionTarget target)
        {
            return target != null
                && target.interactionPoint != null
                && target.interactionPoint.interactionType == MapInteractionType.ReturnToMainUi;
        }

        private static string ResolveDisplayName(MapInteractionTarget target)
        {
            if (target == null)
            {
                return "互动";
            }

            if (!string.IsNullOrEmpty(target.displayName))
            {
                return target.displayName;
            }

            return target.interactionPoint == null || string.IsNullOrEmpty(target.interactionPoint.displayName)
                ? "互动"
                : target.interactionPoint.displayName;
        }

        private string BuildConfirmBody(MapInteractionTarget target)
        {
            if (target == null)
            {
                return "确认执行该互动？";
            }

            ActivityData activity = ResolveActivity(target);
            LocationActionData action = target.locationAction;
            StringBuilder builder = new StringBuilder();

            string description = ResolveConfirmDescription(target, action, activity);
            if (!string.IsNullOrEmpty(description))
            {
                AppendSection(builder, "行动说明");
                builder.Append("<color=");
                builder.Append(ValueColor);
                builder.Append(">");
                builder.Append(description);
                builder.AppendLine("</color>");
                builder.AppendLine();
            }

            AppendSection(builder, "消耗");
            int timeCost = ResolveTimeCost(action, activity);
            AppendRow(builder, "行动值", MapInteractionPromptFormatter.FormatTimeCost(timeCost));
            TimeActionState timeState = ResolveSessionState() == null ? null : ResolveSessionState().timeActionState;
            if (timeState != null)
            {
                if (IsHomeSleepActivity(activity))
                {
                    string sleepDestination = timeState.currentPeriod == TimePeriod.Night
                        ? "次日早晨"
                        : TimeActionRules.GetPeriodDisplayName((TimePeriod)((int)timeState.currentPeriod + 1));
                    AppendRow(builder, "预计", "睡眠后进入" + sleepDestination);
                }
                else
                {
                    TimeActionPreview preview = TimeActionRules.Preview(timeState, Mathf.Max(0, timeCost));
                    string previewText = preview.canConsume
                        ? TimeActionRules.GetPeriodDisplayName(preview.periodAfter) + " · " + preview.actionPointsAfter + "/3"
                        : preview.message;
                    AppendRow(builder, "预计", previewText);
                }
            }
            if (action != null && action.energyCost > 0)
            {
                AppendRow(builder, "精力", "-" + action.energyCost);
            }

            if (action != null && action.moneyCost > 0)
            {
                AppendRow(builder, "金钱", "-" + action.moneyCost);
            }

            builder.AppendLine();

            string rewardPreview = ResolveRewardPreview(action, activity);
            AppendSection(builder, "预期结果");
            AppendRow(builder, "获得", string.IsNullOrEmpty(rewardPreview) ? "无明确奖励预览" : rewardPreview);
            builder.AppendLine();

            AppendHint(builder, "Enter / E 确认，Esc 取消。");
            return builder.ToString();
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

        private static int ResolveTimeCost(LocationActionData action, ActivityData activity)
        {
            if (action != null)
            {
                return action.timeCost;
            }

            return activity == null ? 0 : activity.timeCost;
        }

        private static bool IsHomeSleepActivity(ActivityData activity)
        {
            return activity != null
                && activity.activityType == ActivityType.Rest
                && !string.IsNullOrEmpty(activity.activityId)
                && activity.activityId.IndexOf("sleep", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveRewardPreview(LocationActionData action, ActivityData activity)
        {
            string actionPreview = MapInteractionPromptFormatter.BuildRewardPreview(action);
            if (!string.IsNullOrEmpty(actionPreview))
            {
                return actionPreview;
            }

            return MapInteractionPromptFormatter.BuildRewardPreview(activity);
        }

        private static string ResolveConfirmDescription(MapInteractionTarget target, LocationActionData action, ActivityData activity)
        {
            if (action != null)
            {
                if (!string.IsNullOrEmpty(action.confirmMessage))
                {
                    return action.confirmMessage;
                }

                if (!string.IsNullOrEmpty(action.description))
                {
                    return action.description;
                }
            }

            if (activity != null && !string.IsNullOrEmpty(activity.description))
            {
                return activity.description;
            }

            if (target != null && target.interactionPoint != null && !string.IsNullOrEmpty(target.interactionPoint.description))
            {
                return target.interactionPoint.description;
            }

            return target == null ? string.Empty : target.promptText;
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

        private static void AppendHint(StringBuilder builder, string value)
        {
            builder.Append("<color=");
            builder.Append(HintColor);
            builder.Append(">");
            builder.Append(value);
            builder.AppendLine("</color>");
        }

        private static string GetInteractionKey(MapInteractionTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            MapInteractionPoint point = target.interactionPoint;
            if (point != null && !string.IsNullOrEmpty(point.actionId))
            {
                return point.locationId + ":" + point.actionId;
            }

            return string.IsNullOrEmpty(target.interactionId) ? target.name : target.interactionId;
        }

        private bool IsKeyPressed(KeyCode legacyKey, string inputSystemKey)
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetKeyDown(legacyKey))
                    {
                        return true;
                    }
                }
                catch (InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            return ReadInputSystemKey(inputSystemKey, "wasPressedThisFrame");
        }

        private static bool ReadInputSystemKey(string keyPropertyName, string statePropertyName)
        {
            Type keyboardType = Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType == null)
            {
                return false;
            }

            object keyboard = keyboardType.GetProperty("current")?.GetValue(null, null);
            if (keyboard == null)
            {
                return false;
            }

            object key = keyboardType.GetProperty(keyPropertyName)?.GetValue(keyboard, null);
            if (key == null)
            {
                return false;
            }

            object value = key.GetType().GetProperty(statePropertyName)?.GetValue(key, null);
            return value is bool pressed && pressed;
        }
    }
}
