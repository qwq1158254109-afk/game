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
        // 2026-10-22 每日循环：一天 3 次行动（早晨 / 下午 / 傍晚），夜晚只做结算、不产出行动。
        // 原值是一天 4 段 × 3 点 = 12 点，而一个阶段只要 6/6/8 次行动 —— 玩家不睡觉就能把阶段做完，
        // currentDay 永远停在 1，「一天」在实机里不存在。详见 Docs/DayLoop-Design-2026-10-22.md。
        public const int ActionPointsPerPeriod = 1;
        public const int PeriodCount = 4;
        public const int UsablePeriodCount = 3;
        public const int MaxDailyActionPoints = ActionPointsPerPeriod * UsablePeriodCount;

        public int currentDay = 1;
        public TimePeriod currentPeriod = TimePeriod.Morning;
        public int currentActionPoints = ActionPointsPerPeriod;

        public int RemainingDailyActionPoints
        {
            get
            {
                if (currentPeriod >= TimePeriod.Night)
                {
                    return 0;
                }

                // 本段剩余 + 之后还有几个可用时段（夜晚不算）
                int periodsAfterCurrent = (int)TimePeriod.Night - (int)currentPeriod - 1;
                return currentActionPoints + periodsAfterCurrent * ActionPointsPerPeriod;
            }
        }

        public bool RequiresSleep
        {
            get { return currentPeriod == TimePeriod.Night && currentActionPoints <= 0; }
        }

        /// <summary>一个阶段按「每天 MaxDailyActionPoints 次」折算需要几天（阶段 6/6/8 → 2/2/3 天）。</summary>
        public static int DaysForStage(int timeLimit)
        {
            int limit = Math.Max(0, timeLimit);
            return Math.Max(1, (limit + MaxDailyActionPoints - 1) / MaxDailyActionPoints);
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
            if (currentPeriod == TimePeriod.Night)
            {
                // 夜晚是结算时段：不保留行动点，否则会留出「第 4 次行动」的口子。
                currentActionPoints = 0;
            }
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

            if (resolved.currentPeriod == TimePeriod.Night)
            {
                preview.message = "夜里做不了事了，回卧室睡觉吧。";
                return preview;
            }

            if (cost > preview.remainingBefore)
            {
                preview.message = "今天的行动次数用完了，回卧室睡觉开始下一天。";
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
            if (clamped <= 0)
            {
                period = TimePeriod.Night;
                actionPoints = 0;
                return;
            }

            // 每天 3 格：3 → 早晨，2 → 下午，1 → 傍晚。夜晚不参与行动分配。
            period = (TimePeriod)((int)TimePeriod.Night - clamped);
            actionPoints = TimeActionState.ActionPointsPerPeriod;
        }
    }
}
