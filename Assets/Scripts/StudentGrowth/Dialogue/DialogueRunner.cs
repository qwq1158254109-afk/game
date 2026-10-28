using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 对话播放器：把 <see cref="DialogueView"/>（纯显示）和 <see cref="DialogueData"/>（纯数据）接起来，
    /// 负责发奖励、按 ID 跳转、结束时回调。
    ///
    /// UI 不直接改存档，奖励只在这里发——这样以后加「对话里扣钱」「对话改剧情标记」都只有一处要改。
    /// </summary>
    public sealed class DialogueRunner : MonoBehaviour
    {
        public DialogueView view;
        public GameSessionState sessionState;

        /// <summary>
        /// 当前对话归属的 NPC（2026-10-24）。由交互流程在 Play 时传入，
        /// 「赠送礼物」的好感度记在它头上；没有 NPC 上下文时送礼只回心情（旧行为）。
        /// </summary>
        public NpcData currentNpc { get; private set; }

        /// <summary>对话结束后的待办动作（由选项触发，交互流程在 EndDialogue 里消费）。</summary>
        public bool pendingOpenShop { get; private set; }
        public bool pendingFallbackActivity { get; private set; }

        public bool ConsumePendingOpenShop()
        {
            bool value = pendingOpenShop;
            pendingOpenShop = false;
            return value;
        }

        public bool ConsumePendingFallbackActivity()
        {
            bool value = pendingFallbackActivity;
            pendingFallbackActivity = false;
            return value;
        }

        /// <summary>
        /// 每次玩家重新打开对话前清空待办动作。
        /// 注意不能在 Play 里清——菜单选项跳转子对话（HandleJumpRequested → Play）会再次进 Play，
        /// 那样选项刚设置的标志会被自己吃掉。
        /// </summary>
        public void ClearPendingActions()
        {
            pendingOpenShop = false;
            pendingFallbackActivity = false;
            pendingGiftOption = null;
            giftReturnDialogue = null;
            jumpReturnDialogue = null;
            if (view != null)
            {
                view.suppressAdvance = false;
            }
        }

        /// <summary>送礼等待中的选项（含收礼对话 nextDialogueId）。背包关闭后由 ResumeAfterGift 消费。</summary>
        private DialogueOption pendingGiftOption;
        /// <summary>送礼选项所在的那段对话（通常是菜单），取消送礼时回到它重选。</summary>
        private DialogueData currentDialogueData;
        /// <summary>
        /// 送礼选项所在的那段对话，收礼话说完要回到它（2026-10-27 用户指定）。
        /// 必须单独记一份：Play 播收礼对话时会把 currentDialogueData 换成收礼那段，
        /// 那时再取就找不回菜单了。
        /// </summary>
        private DialogueData giftReturnDialogue;

        /// <summary>
        /// 从当前对话点出的子对话播完要回到的那段（2026-10-27 用户指定：打招呼之后
        /// 也要回到对话栏）。菜单里点「打招呼」「购买参考书」这类只是插一段话，
        /// 播完应该回菜单继续聊，而不是把整段对话结束、把玩家丢回地图。
        /// </summary>
        private DialogueData jumpReturnDialogue;

        private readonly RewardResolver rewardResolver = new RewardResolver();
        private readonly Dictionary<string, DialogueData> registry = new Dictionary<string, DialogueData>();
        private Action pendingComplete;

        private void Awake()
        {
            BindView();
            // 约定：对话资产放 Resources/StudentGrowth/Dialogue，启动时全部登记，
            // 这样 DialogueOption.nextDialogueId 才能按 ID 跳转（否则只会警告"未登记"）。
            RegisterFromResources();
        }

        private void OnDestroy()
        {
            UnbindView();
        }

        /// <summary>登记一段可被 nextDialogueId 跳转到的对话。</summary>
        public void Register(DialogueData dialogue)
        {
            if (dialogue == null || string.IsNullOrEmpty(dialogue.dialogueId))
            {
                return;
            }

            registry[dialogue.dialogueId] = dialogue;
        }

        public void RegisterAll(IEnumerable<DialogueData> dialogues)
        {
            if (dialogues == null)
            {
                return;
            }

            foreach (DialogueData dialogue in dialogues)
            {
                Register(dialogue);
            }
        }

        /// <summary>从 Resources 下批量登记（约定路径 StudentGrowth/Dialogue）。</summary>
        public void RegisterFromResources(string resourceRoot = "StudentGrowth/Dialogue")
        {
            DialogueData[] loaded = Resources.LoadAll<DialogueData>(resourceRoot);
            RegisterAll(loaded);
        }

        public bool Play(DialogueData dialogue, Action onComplete = null, NpcData npc = null)
        {
            if (dialogue == null)
            {
                return false;
            }

            currentNpc = npc;
            BindView();
            if (view == null)
            {
                Debug.LogWarning("[DialogueRunner] 缺少 DialogueView，对话无法播放。");
                onComplete?.Invoke();
                return false;
            }

            pendingComplete = onComplete;
            currentDialogueData = dialogue;
            view.sessionState = sessionState;
            view.currentNpc = currentNpc;
            view.Show(dialogue, HandleFinished);
            return true;
        }

        public bool PlayById(string dialogueId, Action onComplete = null, NpcData npc = null)
        {
            if (string.IsNullOrEmpty(dialogueId))
            {
                return false;
            }

            if (!registry.TryGetValue(dialogueId, out DialogueData dialogue))
            {
                // 跳转目标没登记时不要静默吞掉，否则剧情会「点完选项就没反应」。
                Debug.LogWarning("[DialogueRunner] 未登记的对话 ID：" + dialogueId);
                return false;
            }

            return Play(dialogue, onComplete, npc);
        }

        public bool IsPlaying => view != null && view.gameObject.activeSelf;

        private void BindView()
        {
            if (view == null)
            {
                view = FindAnyObjectByType<DialogueView>();
            }

            if (view == null)
            {
                return;
            }

            view.onOptionChosen -= HandleOptionChosen;
            view.onOptionChosen += HandleOptionChosen;
            view.onJumpRequested -= HandleJumpRequested;
            view.onJumpRequested += HandleJumpRequested;
        }

        private void UnbindView()
        {
            if (view == null)
            {
                return;
            }

            view.onOptionChosen -= HandleOptionChosen;
            view.onJumpRequested -= HandleJumpRequested;
        }

        private void HandleOptionChosen(DialogueOption option)
        {
            if (option == null)
            {
                return;
            }

            if (option.openShop)
            {
                pendingOpenShop = true;
            }

            if (option.useFallbackActivity)
            {
                pendingFallbackActivity = true;
            }

            if (option.chooseItemAsGift)
            {
                pendingGiftOption = option;
                // 菜单记为「送完回来找它」。此刻 currentDialogueData 还是菜单，
                // 一旦 Play 播起收礼对话就会被覆盖成收礼那段。
                giftReturnDialogue = currentDialogueData;
                if (view != null)
                {
                    view.suppressAdvance = true;
                }

                StartCoroutine(OpenGiftPickerNextFrame());
                return;
            }

            // 子对话（打招呼 / 购买参考书这类）播完要回到当前这段，不能把整段结束掉。
            // 这一步必须放在下面几个提前 return 之前：纯分支选项没有奖励，
            // 摆在奖励检查后面就会被漏掉。
            if (!string.IsNullOrEmpty(option.nextDialogueId))
            {
                jumpReturnDialogue = currentDialogueData;
            }

            if (option.rewards == null || option.rewards.Count == 0)
            {
                return;
            }

            if (sessionState == null)
            {
                Debug.LogWarning("[DialogueRunner] 没有 sessionState，选项奖励未发放。");
                return;
            }

            rewardResolver.ApplyRewards(option.rewards, sessionState);
        }
        /// <summary>
        /// 「赠送礼物」：等对话框收起一帧，再打开背包的选择模式。
        /// 2026-10-24 修复顺序：先挑道具、选中并发放效果后才播收礼对话（nextDialogueId）；
        /// 取消则回到送礼前的那段对话（通常是菜单）重选。旧行为是点选项的瞬间就播
        /// 「收下了礼物」，玩家还没送出去 NPC 就先道谢了。
        /// </summary>
        private IEnumerator OpenGiftPickerNextFrame()
        {
            yield return null;

            if (sessionState == null || sessionState.playerState == null)
            {
                ResumeAfterGift(false);
                yield break;
            }

            // 2026-10-26：改用送礼专用选择条（820 宽，贴在对话框上方原位）。
            // 旧版复用 InventoryPanelView（800×560 居中），与 1200×340 的对话框
            // 几何上必然重叠，实测界面糊成一坨。
            GiftPickerView picker = GiftPickerView.Ensure();
            picker.Open(sessionState, item =>
            {
                if (item == null)
                {
                    ResumeAfterGift(false);
                    return;
                }

                InventoryModel.Drop(sessionState, item);
                int bonus = Mathf.Clamp(2 + item.price / 8, 2, 8);
                sessionState.playerState.AddStat(PlayerStatKey.Mood, bonus);
                // 送礼同时积累 NPC 好感，曲线与心情一致（价格越贵加得越多）。
                if (currentNpc != null && !string.IsNullOrEmpty(currentNpc.npcId))
                {
                    sessionState.npcRelationships.Add(currentNpc.npcId, bonus);
                    // 送礼当场涨好感，对话框里的小心心要同步点亮。
                    if (view != null)
                    {
                        view.RefreshNpcHearts();
                    }
                }
                Sfx.Ui.Confirm();
                ResumeAfterGift(true);
            }, () => ResumeAfterGift(false));
        }

        /// <summary>
        /// 背包关闭后的收尾。2026-10-27 用户指定：**送礼完成后回到对话界面**，
        /// 所以送出礼物、取消送礼、以及「送出了但没配收礼话」三种情况统统回菜单，
        /// 玩家能接着打招呼 / 再送 / 告辞，而不是一送完就被踢回地图。
        ///
        /// 三处必须一起改，拆开都会留下另一种卡法：
        /// 1. 播收礼对话要带上 currentNpc —— Play 会把 currentNpc 覆盖成参数值，
        ///    漏传等于「收礼那句一出现，好感度小心心全灭」。
        /// 2. 要回的那段菜单必须提前存进 giftReturnDialogue，不能用 currentDialogueData
        ///    （播收礼话时它已经被换成收礼那段了）。
        /// 3. Play 会重新登记 pendingComplete，回菜单前必须先把原回调抓到局部变量，
        ///    否则菜单播完又会绕回本方法，变成无限循环。
        /// </summary>
        private void ResumeAfterGift(bool giftGiven)
        {
            DialogueOption giftOption = pendingGiftOption;
            pendingGiftOption = null;
            DialogueData menu = giftReturnDialogue;
            giftReturnDialogue = null;
            Action finish = pendingComplete;
            if (view != null)
            {
                view.suppressAdvance = false;
            }

            if (giftGiven && giftOption != null && !string.IsNullOrEmpty(giftOption.nextDialogueId))
            {
                // 收礼话说完由 ReturnToGiftMenu 把菜单接回来；播放失败（ID 未登记）
                // 就落到下面同一个回菜单分支，不会卡住。
                if (PlayById(giftOption.nextDialogueId, () => ReturnToGiftMenu(menu, finish), currentNpc))
                {
                    return;
                }
            }

            ReturnToGiftMenu(menu, finish);
        }

        /// <summary>
        /// 把菜单接回来。没有菜单可回（异常数据）时收起面板并结束，
        /// 绝不能让对话框停在菜单那一句上——那种状态点哪都没反应、人也走不动。
        /// </summary>
        private void ReturnToGiftMenu(DialogueData menu, Action finish)
        {
            if (menu != null)
            {
                // Play 会把 pendingComplete 重新登记成 finish（原 EndDialogue），
                // 菜单再播完就是正常结束，不会再绕回这里。
                Play(menu, finish, currentNpc);
                return;
            }

            if (view != null)
            {
                view.Hide();
            }

            pendingComplete = null;
            finish?.Invoke();
        }

        private void HandleJumpRequested(string nextDialogueId)
        {
            // PlayById 内部会把 pendingComplete 重新登记给下一段对话。
            // 这里千万不能再把它清空 —— 清了之后整条链路播完不会触发结束回调，
            // 输入闸门永远不释放，表现就是「选完选项后人物不能动」。
            // NPC 上下文也要一路带下去，否则子对话里好感度小心心会消失。
            //
            // 2026-10-27：子对话播完要回菜单（用户指定「打招呼之后也回到对话栏」）。
            // 原回调先抓到局部变量——子对话播完时 pendingComplete 已经指向
            // 回菜单的那层包装了，直接传就会自我循环。
            Action finish = pendingComplete;
            DialogueData back = jumpReturnDialogue;
            jumpReturnDialogue = null;

            if (PlayById(nextDialogueId, () => ReturnAfterJump(back, finish), currentNpc))
            {
                return;
            }

            ReturnAfterJump(back, finish);
        }

        /// <summary>
        /// 子对话播完把上一段接回来。没有上一段可回（异常数据）时收起面板并结束，
        /// 不能把对话框留在「点哪都没反应」的状态。
        /// </summary>
        private void ReturnAfterJump(DialogueData back, Action finish)
        {
            if (back != null)
            {
                Play(back, finish, currentNpc);
                return;
            }

            if (view != null)
            {
                view.Hide();
            }

            pendingComplete = null;
            finish?.Invoke();
        }

        private void HandleFinished()
        {
            Action callback = pendingComplete;
            pendingComplete = null;
            callback?.Invoke();
        }
    }
}
