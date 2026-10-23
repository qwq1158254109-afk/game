using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 一段对话。由剧情组在 Inspector 里填，代码不关心具体文案。
    ///
    /// 设计约定（给写剧情的人看）：
    ///   · lines 按顺序播放，一条 line = 一屏。
    ///   · line.options 为空 → 普通对白，玩家点一下继续。
    ///   · line.options 非空 → 显示选项按钮，玩家选完按 nextDialogueId 跳转或结束。
    ///   · 每条 line 都可以覆盖 speakerName / portrait，方便一段对话里多人对谈。
    ///   · 奖励写在 option 上，选择后才发放。
    /// </summary>
    [CreateAssetMenu(menuName = "Game Data/Dialogue Data", fileName = "NewDialogue")]
    public class DialogueData : ScriptableObject
    {
        [Tooltip("稳定 ID，存档与跳转都靠它。改名不会破坏存档。")]
        public string dialogueId;

        [Tooltip("默认说话人。单人对白只填这里即可。")]
        public string speakerName;

        [Tooltip("默认头像。缺省时对话框左侧留空，不影响布局。")]
        public Sprite portrait;

        [Tooltip("按顺序播放的对白行。")]
        public List<DialogueLine> lines = new List<DialogueLine>();
    }

    [Serializable]
    public class DialogueLine
    {
        [TextArea(2, 6)]
        [Tooltip("这一屏的正文。留空会被跳过。")]
        public string text;

        [Tooltip("留空则沿用 DialogueData.speakerName。")]
        public string speakerOverride;

        [Tooltip("留空则沿用 DialogueData.portrait。")]
        public Sprite portraitOverride;

        [Tooltip("非空时显示选项按钮；为空表示普通对白，点一下继续。")]
        public List<DialogueOption> options = new List<DialogueOption>();

        public string ResolveSpeaker(string fallback)
        {
            return string.IsNullOrEmpty(speakerOverride) ? fallback : speakerOverride;
        }

        public Sprite ResolvePortrait(Sprite fallback)
        {
            return portraitOverride != null ? portraitOverride : fallback;
        }

        public bool HasOptions()
        {
            return options != null && options.Count > 0;
        }
    }

    [Serializable]
    public class DialogueOption
    {
        [Tooltip("按钮上的文字。")]
        public string text;

        [Tooltip("选中后发放的奖励。为空表示纯分支。")]
        public List<RewardData> rewards = new List<RewardData>();

        [Tooltip("选中后跳转到的对话 ID。留空表示这段对话到此结束。")]
        public string nextDialogueId;

        [Tooltip("不满足条件的选项会置灰。留空表示永远可选。")]
        public List<EventConditionData> requirements = new List<EventConditionData>();
    }
}
