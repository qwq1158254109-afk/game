using System;
using System.Collections.Generic;

namespace StudentGrowth
{
    public enum BattleResultConsumption { Pending, Applying, Applied, Faulted }

    public sealed class BattleResultRecord
    {
        public string BattleId { get; internal set; }
        public string RuntimeSessionId { get; internal set; }
        public ChallengeData Challenge { get; internal set; }
        public StageData Stage { get; internal set; }
        public bool IsComplete { get; internal set; }
        public bool Success { get; internal set; }
        public int FinalProgress { get; internal set; }
        public int TargetProgress { get; internal set; }
        public int TurnsUsed { get; internal set; }
        public string OutcomeReason { get; internal set; }
        public string Failure { get; internal set; }
        public BattleResultConsumption Consumption { get; internal set; }
        public bool ReturnRequested { get; internal set; }
        public BattleSettlementPlan Plan { get; internal set; }
        public bool EngineOwned { get; internal set; }
        internal StageData NextStage;
    }

    // One consumer per session. Formal battles use CompleteEngine; legacy bridges remain for old callers.
    public sealed class BattleResultConsumer
    {
        private readonly GameSessionState session;
        private readonly RewardResolver rewards = new RewardResolver();
        private readonly Dictionary<string, BattleResultRecord> directStageResults = new Dictionary<string, BattleResultRecord>();
        private readonly HashSet<string> completedStages = new HashSet<string>();
        public BattleResultRecord Current { get; private set; }
        public event Action<BattleResultRecord> Applied;
        internal Action<string> CommitProbe;

        public bool CompleteEngine(BattleResultRecord record, EngineBattleResult result,
            BattleCheckpoint baseline, ActivityData activity)
        {
            if(record==null||record!=Current||result==null||result.Outcome==BattleOutcome.Faulted
                ||record.RuntimeSessionId!=GameApplicationRuntime.CurrentRuntimeSessionId
                ||record.BattleId!=result.Final.BattleId)return false;
            if(record.Consumption==BattleResultConsumption.Applied)return true;
            if(record.Stage!=session.stageState.currentStage)return false;
            if(record.Consumption==BattleResultConsumption.Applying)return false;
            if(!baseline.Matches(session)){record.Failure="战斗外状态已改变，无法安全提交。";return false;}
            try
            {
                if(record.Plan==null)record.Plan=BattleSettlementPlan.Prepare(session,result,record.Challenge,activity,IsStageCleared(record.Stage),record.NextStage);
            }
            catch(Exception ex){record.Failure=ex.ToString();record.Consumption=BattleResultConsumption.Faulted;return false;}
            var backup=new BattleCheckpoint(session);
            record.Consumption=BattleResultConsumption.Applying;
            try
            {
                CommitProbe?.Invoke("BeforeWrite");
                record.Plan.Final.Restore(session);
                CommitProbe?.Invoke("AfterWrite");
                record.Success=result.Outcome==BattleOutcome.Victory;record.IsComplete=true;
                record.FinalProgress=result.Final.Progress;record.TargetProgress=result.Final.Definition.Target;
                record.TurnsUsed=result.Final.Turn;record.OutcomeReason=result.Reason.ToString();
                record.Consumption=BattleResultConsumption.Applied;record.Failure=null;
            }
            catch(Exception ex)
            {
                try { backup.Restore(session);record.Consumption=BattleResultConsumption.Faulted;record.Failure=ex.ToString(); }
                catch(Exception restore){record.Failure=ex+"\nROLLBACK FAILED: "+restore;}
                return false;
            }
            if(Applied!=null)foreach(Action<BattleResultRecord> listener in Applied.GetInvocationList())
                try{listener(record);}catch(Exception ex){UnityEngine.Debug.LogWarning("Battle committed; notification failed: "+ex);}
            return true;
        }

        public BattleResultConsumer(GameSessionState session) { this.session = session; }

        public BattleResultRecord Begin(ChallengeData challenge)
        {
            Current = new BattleResultRecord
            {
                BattleId = Guid.NewGuid().ToString("N"),
                RuntimeSessionId = GameApplicationRuntime.CurrentRuntimeSessionId,
                Challenge = challenge,
                Stage = session.stageState.currentStage
            };
            return Current;
        }

        public bool Complete(BattleResultRecord record, bool success)
        {
            if(record?.EngineOwned==true)return record.Consumption==BattleResultConsumption.Applied&&record.Success==success;
            if (record == null || record != Current) return false;
            if (record.RuntimeSessionId != GameApplicationRuntime.CurrentRuntimeSessionId) return false;
            if (record.Stage != session.stageState.currentStage) return false;
            if (record.Consumption != BattleResultConsumption.Pending)
                return record.Consumption == BattleResultConsumption.Applied && record.Success == success;

            record.IsComplete = true;
            record.Success = success;
            record.Consumption = BattleResultConsumption.Applying;
            try
            {
                var challenge = record.Challenge;
                bool stageBoss = challenge != null && challenge.isBoss && record.Stage != null
                    && record.Stage.bossChallenge == challenge;
                // A restored/completed stage must not issue another challenge or stage reward.
                if (stageBoss && IsStageCleared(record.Stage))
                {
                    record.Consumption = BattleResultConsumption.Applied;
                    return true;
                }
                if (challenge != null)
                    rewards.ApplyRewards(challenge.isBoss
                        ? (success ? challenge.bossClearRewards : challenge.bossFailureRewards)
                        : (success ? challenge.successRewards : challenge.failureRewards), session);
                if (stageBoss) ApplyStage(record.Stage, success);
                record.Consumption = BattleResultConsumption.Applied;
                return true;
            }
            catch (Exception ex)
            {
                record.Failure = ex.GetType().Name + ": " + ex.Message;
                record.Consumption = BattleResultConsumption.Faulted;
                return false;
            }
        }

        public bool CompleteStage(StageData stage, bool success)
        {
            if (stage == null || stage != session.stageState.currentStage) return false;
            if (Current != null && Current.Stage == stage && Current.Challenge == stage.bossChallenge)
                return Complete(Current, success);
            string key = StageKey(stage);
            if (directStageResults.TryGetValue(key, out var previous))
                return previous.Consumption == BattleResultConsumption.Applied && previous.Success == success;
            var record = Begin(stage.bossChallenge);
            directStageResults.Add(key, record);
            if (stage.bossChallenge != null) return Complete(record, success);
            record.Success = success;
            record.IsComplete = true;
            record.Consumption = BattleResultConsumption.Applying;
            try { ApplyStage(stage, success); record.Consumption = BattleResultConsumption.Applied; return true; }
            catch (Exception ex) { record.Failure = ex.Message; record.Consumption = BattleResultConsumption.Faulted; return false; }
        }

        public bool TryRequestReturn(BattleResultRecord record)
        {
            if (record == null || record != Current || record.ReturnRequested
                || record.Consumption != BattleResultConsumption.Applied
                || record.Stage != session.stageState.currentStage
                || record.RuntimeSessionId != GameApplicationRuntime.CurrentRuntimeSessionId) return false;
            record.ReturnRequested = true;
            return true;
        }

        public bool IsStageCleared(StageData stage)
        {
            return stage != null && (completedStages.Contains(StageKey(stage))
                || (!string.IsNullOrEmpty(stage.stageId) && session.clearedStageIds.Contains(stage.stageId)));
        }

        private void ApplyStage(StageData stage, bool success)
        {
            if (IsStageCleared(stage)) return;
            session.stageState.bossDefeated = success;
            if (success)
            {
                session.stageState.defeatedBossCount++;
                session.stageState.clearedStageCount++;
                if (!string.IsNullOrEmpty(stage.stageId) && !session.clearedStageIds.Contains(stage.stageId)) session.clearedStageIds.Add(stage.stageId);
                if (stage.bossChallenge != null && !string.IsNullOrEmpty(stage.bossChallenge.challengeId)
                    && !session.defeatedBossIds.Contains(stage.bossChallenge.challengeId)) session.defeatedBossIds.Add(stage.bossChallenge.challengeId);
                rewards.ApplyRewards(stage.clearStageRewards, session);
                completedStages.Add(StageKey(stage));
                // 阶段通关后回地图弹成长报告。最终阶段不弹——那边有结局界面，两份全屏 UI 会打架。
                if (!stage.isFinalStage)
                {
                    StageReportQueue.Enqueue(stage);
                }
            }
            if (stage.isFinalStage) session.endingReached = true;
        }

        private static string StageKey(StageData stage)
        {
            return string.IsNullOrEmpty(stage.stageId) ? "runtime:" + stage.GetEntityId() : stage.stageId;
        }
    }
}
