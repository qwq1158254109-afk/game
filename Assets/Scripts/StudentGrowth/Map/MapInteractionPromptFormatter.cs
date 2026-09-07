using System;
using System.Collections.Generic;

namespace StudentGrowth
{
    public static class MapInteractionPromptFormatter
    {
        public static string BuildDefaultPrompt(bool debugVisible)
        {
            return debugVisible
                ? "靠近可互动位置后按 E 交互｜调试范围已显示"
                : "靠近可互动位置后按 E 交互";
        }

        public static string BuildExitPrompt()
        {
            return "按 E 返回阶段地图";
        }

        public static string BuildUnavailablePrompt(string displayName)
        {
            return NormalizeName(displayName) + " 当前没有可执行行动";
        }

        public static string BuildActivityPrompt(string displayName, int timeCost, string rewardPreview)
        {
            return "按 E 与 " + NormalizeName(displayName) + " 交互";
        }

        public static string FormatTimeCost(int timeCost)
        {
            int safeCost = Math.Max(0, timeCost);
            return safeCost == 0 ? "不消耗时间" : "消耗 " + safeCost + " 小时";
        }

        public static string CompactRewardPreview(string rewardPreview)
        {
            if (string.IsNullOrEmpty(rewardPreview))
            {
                return string.Empty;
            }

            string value = rewardPreview.Replace("\r", string.Empty).Replace("\n", string.Empty).Trim();
            return string.IsNullOrEmpty(value) ? string.Empty : value;
        }

        public static string BuildRewardPreview(ActivityData activity)
        {
            if (activity == null)
            {
                return string.Empty;
            }

            return BuildRewardPreview(activity.fixedRewards);
        }

        public static string BuildRewardPreview(LocationActionData action)
        {
            if (action == null || !action.showRewardPreview)
            {
                return string.Empty;
            }

            string fixedPreview = BuildRewardPreview(action.fixedRewards);
            if (!string.IsNullOrEmpty(fixedPreview))
            {
                return fixedPreview;
            }

            return BuildRewardPreview(action.possibleRewards);
        }

        public static string BuildRewardPreview(IList<RewardData> rewards)
        {
            if (rewards == null || rewards.Count == 0)
            {
                return string.Empty;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                RewardData reward = rewards[i];
                if (reward == null)
                {
                    continue;
                }

                string preview = FormatReward(reward);
                if (string.IsNullOrEmpty(preview))
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append("，");
                }

                builder.Append(preview);
            }

            return builder.ToString();
        }

        public static string FormatReward(RewardData reward)
        {
            if (reward == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(reward.description))
            {
                return reward.description;
            }

            switch (reward.rewardType)
            {
                case RewardType.AddPlayerStat:
                    return FormatStatName(reward.targetStat) + SignedValue(reward.value);
                case RewardType.ReducePlayerStat:
                    return FormatStatName(reward.targetStat) + SignedValue(-Math.Abs(reward.value));
                case RewardType.AddMoney:
                    return "金钱" + SignedValue(reward.value);
                case RewardType.ReducePressure:
                    return "压力" + SignedValue(-Math.Abs(reward.value));
                case RewardType.IncreaseMaxEnergy:
                    return "精力上限" + SignedValue(reward.value);
                case RewardType.AddStageTime:
                    return "时间" + SignedValue(reward.value);
                case RewardType.AddCard:
                    return reward.card == null ? "获得卡牌" : "获得卡牌 " + reward.card.displayName;
                case RewardType.UpgradeCard:
                    return reward.card == null ? "升级卡牌" : "升级卡牌 " + reward.card.displayName;
                case RewardType.RemoveCard:
                    return reward.card == null ? "移除卡牌" : "移除卡牌 " + reward.card.displayName;
                case RewardType.AddItem:
                    return reward.item == null ? "获得道具" : "获得道具 " + reward.item.displayName;
                case RewardType.RemoveItem:
                    return reward.item == null ? "移除道具" : "移除道具 " + reward.item.displayName;
                case RewardType.UnlockActivity:
                    return reward.activity == null ? "解锁活动" : "解锁活动 " + reward.activity.displayName;
                case RewardType.UnlockStage:
                    return reward.stage == null ? "解锁阶段" : "解锁阶段 " + reward.stage.displayName;
                default:
                    return string.Empty;
            }
        }

        public static string FormatStatName(PlayerStatKey key)
        {
            switch (key)
            {
                case PlayerStatKey.Chinese:
                    return "语文";
                case PlayerStatKey.Math:
                    return "数学";
                case PlayerStatKey.English:
                    return "英语";
                case PlayerStatKey.Energy:
                    return "精力";
                case PlayerStatKey.MaxEnergy:
                    return "精力上限";
                case PlayerStatKey.Pressure:
                    return "压力";
                case PlayerStatKey.Mood:
                    return "心情";
                case PlayerStatKey.ParentRelation:
                    return "亲子";
                case PlayerStatKey.Social:
                    return "社交";
                case PlayerStatKey.Interest:
                    return "兴趣";
                case PlayerStatKey.Confidence:
                    return "自信";
                case PlayerStatKey.Autonomy:
                    return "自主";
                case PlayerStatKey.Money:
                    return "金钱";
                default:
                    return key.ToString();
            }
        }

        public static string SignedValue(int value)
        {
            return value >= 0 ? "+" + value : value.ToString();
        }

        private static string NormalizeName(string displayName)
        {
            return string.IsNullOrEmpty(displayName) ? "互动" : displayName;
        }
    }
}
