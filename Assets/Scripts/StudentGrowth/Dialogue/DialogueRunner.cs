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
        }

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
            view.sessionState = sessionState;
            view.currentNpc = currentNpc;
            view.Show(dialogue, HandleFinished);
            return true;
        }

        public bool PlayById(string dialogueId, Action onComplete = null)
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

            return Play(dialogue, onComplete);
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
                StartCoroutine(OpenGiftPickerNextFrame());
                return;
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
        /// 选中道具后从背包移除，并按价值回一点心情（不给道具本身的使用效果）。
        /// </summary>
        private IEnumerator OpenGiftPickerNextFrame()
        {
            yield return null;

            if (sessionState == null || sessionState.playerState == null)
            {
                yield break;
            }

            InventoryPanelView panel = InventoryPanelView.Ensure();
            panel.OpenForSelection(sessionState, item =>
            {
                if (item == null)
                {
                    return;
                }

                InventoryModel.Drop(sessionState, item);
                int bonus = Mathf.Clamp(2 + item.price / 8, 2, 8);
                sessionState.playerState.AddStat(PlayerStatKey.Mood, bonus);
                // 2026-10-24：送礼同时积累 NPC 好感，曲线与心情一致（价格越贵加得越多）。
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
            });
        }

        private void HandleJumpRequested(string nextDialogueId)
        {
            // PlayById 内部会把 pendingComplete 重新登记给下一段对话。
            // 这里千万不能再把它清空 —— 清了之后整条链路播完不会触发结束回调，
            // 输入闸门永远不释放，表现就是「选完选项后人物不能动」。
            PlayById(nextDialogueId, pendingComplete);
        }

        private void HandleFinished()
        {
            Action callback = pendingComplete;
            pendingComplete = null;
            callback?.Invoke();
        }
    }
}
