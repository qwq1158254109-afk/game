using System.Collections.Generic;

namespace StudentGrowth
{
    public static class EventConditionResolver
    {
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

            int currentValue = playerState.GetStat(condition.statKey);
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
