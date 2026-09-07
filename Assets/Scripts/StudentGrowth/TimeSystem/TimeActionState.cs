using System;

namespace StudentGrowth
{
    public enum TimePeriod
    {
        Morning = 0,
        Afternoon = 1,
        Evening = 2,
        Night = 3
    }

    public enum SleepLocation
    {
        Home,
        BusStop
    }

    [Serializable]
    public sealed class TimeActionState
    {
        public const int ActionPointsPerPeriod = 3;
        public const int PeriodCount = 4;
        public const int MaxDailyActionPoints = ActionPointsPerPeriod * PeriodCount;

        public int currentDay = 1;
        public TimePeriod currentPeriod = TimePeriod.Morning;
        public int currentActionPoints = ActionPointsPerPeriod;

        public int RemainingDailyActionPoints
        {
            get
            {
                return currentActionPoints
                    + ActionPointsPerPeriod * ((int)TimePeriod.Night - (int)currentPeriod);
            }
        }

        public bool RequiresSleep
        {
            get { return currentPeriod == TimePeriod.Night && currentActionPoints <= 0; }
        }

        public void ResetToDefaults()
        {
            currentDay = 1;
            currentPeriod = TimePeriod.Morning;
            currentActionPoints = ActionPointsPerPeriod;
        }

        public void Normalize()
        {
            currentDay = Math.Max(1, currentDay);
            int periodValue = Math.Max(0, Math.Min((int)TimePeriod.Night, (int)currentPeriod));
            currentPeriod = (TimePeriod)periodValue;
            currentActionPoints = Math.Max(0, Math.Min(ActionPointsPerPeriod, currentActionPoints));
        }
    }

    public sealed class TimeActionPreview
    {
        public bool canConsume;
        public int cost;
        public int remainingBefore;
        public int remainingAfter;
        public TimePeriod periodBefore;
        public TimePeriod periodAfter;
        public int actionPointsBefore;
        public int actionPointsAfter;
        public bool periodChanged;
        public bool requiresSleep;
        public string message;
    }

    public sealed class SleepResult
    {
        public bool success;
        public SleepLocation location;
        public int dayBefore;
        public int dayAfter;
        public TimePeriod periodBefore;
        public TimePeriod periodAfter;
        public int discardedActionPoints;
        public int moodChange;
        public bool startedNewDay;
        public string message;
    }

    public static class TimeActionRules
    {
        public static TimeActionPreview Preview(TimeActionState state, int cost)
        {
            TimeActionState resolved = state ?? new TimeActionState();
            resolved.Normalize();

            TimeActionPreview preview = new TimeActionPreview
            {
                cost = cost,
                remainingBefore = resolved.RemainingDailyActionPoints,
                periodBefore = resolved.currentPeriod,
                actionPointsBefore = resolved.currentActionPoints,
                periodAfter = resolved.currentPeriod,
                actionPointsAfter = resolved.currentActionPoints,
                remainingAfter = resolved.RemainingDailyActionPoints,
                requiresSleep = resolved.RequiresSleep
            };

            if (cost < 0)
            {
                preview.message = "行动值消耗不能为负数。";
                return preview;
            }

            if (cost > preview.remainingBefore)
            {
                preview.message = "今日剩余行动值不足。";
                return preview;
            }

            preview.canConsume = true;
            preview.remainingAfter = preview.remainingBefore - cost;
            ResolveRemaining(preview.remainingAfter, out preview.periodAfter, out preview.actionPointsAfter);
            preview.periodChanged = preview.periodAfter != preview.periodBefore;
            preview.requiresSleep = preview.periodAfter == TimePeriod.Night && preview.actionPointsAfter == 0;
            preview.message = cost == 0 ? "该行动不消耗行动值。" : "行动值消耗成功。";
            return preview;
        }

        public static void Apply(TimeActionState state, TimeActionPreview preview)
        {
            if (state == null || preview == null || !preview.canConsume)
            {
                return;
            }

            state.currentPeriod = preview.periodAfter;
            state.currentActionPoints = preview.actionPointsAfter;
            state.Normalize();
        }

        public static string GetPeriodDisplayName(TimePeriod period)
        {
            switch (period)
            {
                case TimePeriod.Morning:
                    return "早晨";
                case TimePeriod.Afternoon:
                    return "下午";
                case TimePeriod.Evening:
                    return "傍晚";
                case TimePeriod.Night:
                    return "夜晚";
                default:
                    return "未知";
            }
        }

        private static void ResolveRemaining(int remaining, out TimePeriod period, out int actionPoints)
        {
            int clamped = Math.Max(0, Math.Min(TimeActionState.MaxDailyActionPoints, remaining));
            if (clamped >= 10)
            {
                period = TimePeriod.Morning;
                actionPoints = clamped - 9;
                return;
            }

            if (clamped >= 7)
            {
                period = TimePeriod.Afternoon;
                actionPoints = clamped - 6;
                return;
            }

            if (clamped >= 4)
            {
                period = TimePeriod.Evening;
                actionPoints = clamped - 3;
                return;
            }

            period = TimePeriod.Night;
            actionPoints = clamped;
        }
    }
}
