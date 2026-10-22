using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class TimeActionController : MonoBehaviour
    {
        public const int BusStopMoodPenalty = 5;

        [SerializeField] private GameSessionState sessionState;

        public event Action<TimeActionPreview> ActionConsumed;
        public event Action<TimePeriod, TimePeriod> PeriodChanged;
        public event Action<int> DayStarted;
        public event Action StateChanged;
        private NotificationScope notificationScope;
        public NotificationScope DeferNotifications()
        {
            if(notificationScope!=null)throw new InvalidOperationException("Time transaction already active");
            return notificationScope=new NotificationScope(this);
        }
        public sealed class NotificationScope : IDisposable
        {
            private readonly TimeActionController owner;
            internal readonly List<Action> pending=new List<Action>();
            private bool closed;
            internal NotificationScope(TimeActionController owner){this.owner=owner;}
            public void Commit()
            {
                if(closed)return;closed=true;owner.notificationScope=null;
                foreach(var call in pending)try{call();}catch(Exception ex){Debug.LogWarning("Time committed; notification failed: "+ex);}
                pending.Clear();
            }
            public void Dispose(){if(closed)return;closed=true;pending.Clear();owner.notificationScope=null;}
        }
        private void Emit(Action call){if(notificationScope!=null)notificationScope.pending.Add(call);else call();}
        private void Publish<T>(Action<T> listeners,T value)
        {if(listeners!=null)foreach(Action<T> listener in listeners.GetInvocationList())Emit(()=>listener(value));}
        private void PublishPeriod(TimePeriod before,TimePeriod after)
        {if(PeriodChanged!=null)foreach(Action<TimePeriod,TimePeriod> listener in PeriodChanged.GetInvocationList())Emit(()=>listener(before,after));}

        public GameSessionState SessionState
        {
            get { return sessionState; }
        }

        public TimeActionState State
        {
            get
            {
                EnsureState();
                return sessionState.timeActionState;
            }
        }

        public void SetSessionState(GameSessionState state)
        {
            sessionState = state ?? new GameSessionState();
            EnsureState();
            NotifyStateChanged();
        }

        public TimeActionPreview PreviewAction(int cost)
        {
            return TimeActionRules.Preview(State, cost);
        }

        public bool TryConsumeAction(int cost, out TimeActionPreview result)
        {
            result = PreviewAction(cost);
            if (!result.canConsume)
            {
                return false;
            }

            TimeActionRules.Apply(State, result);
            Publish(ActionConsumed,result);
            if (result.periodChanged)
            {
                PublishPeriod(result.periodBefore, result.periodAfter);
            }

            NotifyStateChanged();
            return true;
        }

        public bool ConsumeRemainingDay(out TimeActionPreview result)
        {
            return TryConsumeAction(State.RemainingDailyActionPoints, out result);
        }

        public SleepResult Sleep(SleepLocation location)
        {
            TimeActionState state = State;
            TimePeriod periodBefore = state.currentPeriod;
            int dayBefore = state.currentDay;
            int discarded = state.currentActionPoints;
            bool startsNewDay = periodBefore == TimePeriod.Night;

            if (startsNewDay)
            {
                state.currentDay++;
                state.currentPeriod = TimePeriod.Morning;
            }
            else
            {
                state.currentPeriod = (TimePeriod)((int)periodBefore + 1);
            }

            // 夜晚是结算段：不给行动点，避免出现「第 4 次行动」。
            state.currentActionPoints = state.currentPeriod == TimePeriod.Night
                ? 0
                : TimeActionState.ActionPointsPerPeriod;
            int moodChange = 0;
            if (location == SleepLocation.BusStop && sessionState.playerState != null)
            {
                moodChange = -BusStopMoodPenalty;
                sessionState.playerState.AddStat(PlayerStatKey.Mood, moodChange);
            }

            PublishPeriod(periodBefore, state.currentPeriod);
            if (startsNewDay)
            {
                Publish(DayStarted,state.currentDay);
            }

            SleepResult result = new SleepResult
            {
                success = true,
                location = location,
                dayBefore = dayBefore,
                dayAfter = state.currentDay,
                periodBefore = periodBefore,
                periodAfter = state.currentPeriod,
                discardedActionPoints = discarded,
                moodChange = moodChange,
                startedNewDay = startsNewDay,
                message = startsNewDay
                    ? "睡眠结束，进入第 " + state.currentDay + " 天早晨。"
                    : "休息结束，进入" + TimeActionRules.GetPeriodDisplayName(state.currentPeriod) + "。"
            };

            NotifyStateChanged();
            return result;
        }

        private void EnsureState()
        {
            if (sessionState == null)
            {
                sessionState = new GameSessionState();
            }

            if (sessionState.timeActionState == null)
            {
                sessionState.timeActionState = new TimeActionState();
            }

            sessionState.timeActionState.Normalize();
        }

        private void NotifyStateChanged()
        {
            if(StateChanged!=null)foreach(Action listener in StateChanged.GetInvocationList())Emit(listener);
        }
    }
}
