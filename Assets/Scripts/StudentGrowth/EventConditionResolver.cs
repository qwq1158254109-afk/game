using System.Collections.Generic;

namespace StudentGrowth
{
    public static class EventConditionResolver
    {
        /// <summary>
        /// 全量判定：支持属性条件与好感度条件（EventConditionData.subject）。
        /// 旧重载（只传 PlayerState）保留给事件系统等存量调用方，行为不变。
        /// </summary>
        public static bool AreConditionsMet(IEnumerable<EventConditionData> conditions, GameSessionState sessionState)
        {
            if (conditions == null)
            {
                return true;
            }

            foreach (EventConditionData condition in conditions)
            {
                if (!IsConditionMet(condition, sessionState))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsConditionMet(EventConditionData condition, GameSessionState sessionState)
        {
            if (condition == null || sessionState == null)
            {
                return true;
            }

            if (condition.subject == ConditionSubject.NpcRelationship)
            {
                if (string.IsNullOrEmpty(condition.npcId))
                {
                    // 没填 npcId 的好感度条件视为永远满足，避免剧情被配置笔误卡死。
                    return true;
                }

                return Compare(sessionState.npcRelationships.Get(condition.npcId), condition);
            }

            return IsConditionMet(condition, sessionState.playerState);
        }

        public static bool AreConditionsMet(IEnumerable<EventConditionData> conditions, PlayerState playerState)
        {
            if (conditions == null)
            {
                return true;
            }

            foreach (EventConditionData condition in conditions)
            {
                if (!IsConditionMet(condition, playerState))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool IsConditionMet(EventConditionData condition, PlayerState playerState)
        {
            if (condition == null || playerState == null)
            {
                return true;
            }

            return Compare(playerState.GetStat(condition.statKey), condition);
        }

        private static bool Compare(int currentValue, EventConditionData condition)
        {
            switch (condition.comparison)
            {
                case ComparisonType.GreaterOrEqual:
                    return currentValue >= condition.value;
                case ComparisonType.LessOrEqual:
                    return currentValue <= condition.value;
                case ComparisonType.Equal:
                    return currentValue == condition.value;
                case ComparisonType.NotEqual:
                    return currentValue != condition.value;
                default:
                    return false;
            }
        }
    }
}
