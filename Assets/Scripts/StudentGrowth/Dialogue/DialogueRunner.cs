using System;
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

        private readonly RewardResolver rewardResolver = new RewardResolver();
        private readonly Dictionary<string, DialogueData> registry = new Dictionary<string, DialogueData>();
        private Action pendingComplete;

        private void Awake()
        {
            BindView();
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

        public bool Play(DialogueData dialogue, Action onComplete = null)
        {
            if (dialogue == null)
            {
                return false;
            }

            BindView();
            if (view == null)
            {
                Debug.LogWarning("[DialogueRunner] 缺少 DialogueView，对话无法播放。");
                onComplete?.Invoke();
                return false;
            }

            pendingComplete = onComplete;
            view.sessionState = sessionState;
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
            if (option == null || option.rewards == null || option.rewards.Count == 0)
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

        private void HandleJumpRequested(string nextDialogueId)
        {
            // 跳转不触发「播放结束」回调：整条链路（含跳转后的对话）播完才算完。
            if (PlayById(nextDialogueId, pendingComplete))
            {
                pendingComplete = null;
            }
        }

        private void HandleFinished()
        {
            Action callback = pendingComplete;
            pendingComplete = null;
            callback?.Invoke();
        }
    }
}
