using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public enum BattleFlowPhase { Idle, Preparing, Starting, Active, Settling, ResultOpen, Returning, RecoveryRequired }
    public sealed class BattleSessionFlow
    {
        private readonly GameSessionState session;
        private readonly HashSet<string> paidRequests=new HashSet<string>();
        private ChallengeController controller;
        private StageController stage;
        private ActivityData activity;
        private BattleCheckpoint baseline;
        private BattleReturnContext returnContext;
        private BattleView view;
        private string sessionId;
        private bool returning;
        private bool faultExit;
        public BattleFlowPhase Phase { get; private set; }
        public string Failure { get; private set; }
        public string CostReceipt { get; private set; }
        public bool PendingBoss { get; private set; }
        public bool BlocksSave => Phase!=BattleFlowPhase.Idle;
        public BattleResultRecord Result { get; private set; }
        internal Action<string> StartupProbe;
        public BattleSessionFlow(GameSessionState session){this.session=session;}
        public bool Start(string requestId,ChallengeData challenge,ActivityData source,StageController stageController,
            TimeActionController time,ChallengeController battleController,BattleReturnContext origin,uint seed,out string error,
            bool createView=true)
        {
            error=null;
            if(string.IsNullOrEmpty(requestId)){error="缺少开战请求凭证。";return false;}
            if(paidRequests.Contains(requestId)){error="该请求已处理。";return false;}
            if(BlocksSave){error="已有战斗流程正在处理。";return false;}
            if(Application.isPlaying&&GameBootstrapper.SharedSession!=session){error="开战来源会话已失效。";return false;}
            if(battleController!=null&&battleController.SandboxEngine!=null){error="已有规则演练或战斗尚未关闭。";return false;}
            sessionId=GameApplicationRuntime.CurrentRuntimeSessionId;
            if(string.IsNullOrEmpty(sessionId)||origin==null||battleController==null||stageController==null)
            {error="会话、战斗控制器或返回凭证不可用。";return false;}
            controller=battleController;stage=stageController;activity=source;returnContext=origin;
            Phase=BattleFlowPhase.Preparing;GameplayInputGate.Acquire(this);
            var before=new BattleCheckpoint(session);TimeActionController.NotificationScope batch=null;bool committed=false;
            try
            {
                var battleDeck=challenge!=null&&challenge.loanDeck!=null&&challenge.loanDeck.Count>0?challenge.loanDeck:session.deckState.masterDeck;
                // Freeze list membership for preparation, engine and view without touching ownership.
                battleDeck=new List<CardData>(battleDeck);
                var prepared=BattleCompiler.Prepare(challenge,battleDeck,null,session.itemInventory.ownedItems);
                if(!prepared.Success)throw new InvalidOperationException(string.Join("\n",prepared.Errors));
                if(challenge.isBoss && (stage.sessionState!=session||session.stageState.currentStage?.bossChallenge!=challenge
                    ||session.BattleResults.IsStageCleared(session.stageState.currentStage)
                    ||!(session.stageState.bossUnlocked||session.stageState.remainingTime<=0)))
                    throw new InvalidOperationException("阶段挑战尚未解锁或已完成。");
                int cost=challenge.isBoss?0:Math.Max(0,source?.timeCost??0);
                if(session.stageState.remainingTime<cost||time!=null&&!time.PreviewAction(cost).canConsume)
                    throw new InvalidOperationException("时间不足。");
                StartupProbe?.Invoke("Prepared");
                Phase=BattleFlowPhase.Starting;batch=time?.DeferNotifications();
                if(time!=null && !time.TryConsumeAction(cost,out var preview))throw new InvalidOperationException(preview.message);
                session.stageState.remainingTime-=cost;
                PendingBoss=!challenge.isBoss&&(session.stageState.bossUnlocked||session.stageState.remainingTime<=0);
                if(PendingBoss)session.stageState.bossUnlocked=true;
                StartupProbe?.Invoke("CostConsumed");
                controller.SetSessionState(session);
                var started=controller.StartEngineSandbox(challenge,battleDeck,session.playerState,seed);
                if(!started.Success||controller.SandboxEngine.Result?.Outcome==BattleOutcome.Faulted)
                    throw new InvalidOperationException(string.Join("\n",started.Errors)+controller.SandboxEngine?.Result?.FailureDetail);
                if(createView)view=BattleView.Open(controller,this,battleDeck);
                StartupProbe?.Invoke("ViewReady");
                if(sessionId!=GameApplicationRuntime.CurrentRuntimeSessionId)throw new InvalidOperationException("会话已失效。");
                baseline=new BattleCheckpoint(session);
                Result=session.BattleResults.Begin(challenge);Result.EngineOwned=true;Result.BattleId=controller.SandboxEngine.Snapshot().BattleId;
                if(challenge.isBoss && stage.stageSequence!=null)
                    Result.NextStage=stage.stageSequence.Find(s=>s!=null&&s.stageId==Result.Stage.nextStageId);
                CostReceipt=requestId;paidRequests.Add(requestId);faultExit=false;Failure=null;Phase=BattleFlowPhase.Active;
                committed=true;
                batch?.Commit();batch=null;
                ProcessResult();view?.Refresh();return true;
            }
            catch(Exception ex)
            {
                if(committed)
                {
                    Failure=ex.Message;error=Failure;
                    if(IsCurrent)Phase=BattleFlowPhase.RecoveryRequired;
                    Debug.LogWarning("Battle started; presentation failed: "+ex);return true;
                }
                batch?.Dispose();controller.DisposeEngineSandbox();if(view!=null)UnityEngine.Object.Destroy(view.gameObject);view=null;
                PendingBoss=false;error=Failure=ex.Message;
                try{before.Restore(session);Phase=BattleFlowPhase.Idle;GameplayInputGate.Release(this,true);}
                catch(Exception restore){Failure+="\n恢复失败："+restore;Phase=BattleFlowPhase.RecoveryRequired;}
                return false;
            }
        }
        public bool IsCurrent => !string.IsNullOrEmpty(sessionId)&&sessionId==GameApplicationRuntime.CurrentRuntimeSessionId;
        public void ProcessResult()
        {
            if(!IsCurrent||controller==null||Phase!=BattleFlowPhase.Active)return;
            var result=controller.SandboxEngine?.Result;if(result==null)return;
            if(result.Outcome==BattleOutcome.Faulted){Failure=result.FailureDetail;Phase=BattleFlowPhase.RecoveryRequired;return;}
            RetrySettlement();
        }
        public bool RetrySettlement()
        {
            if(!IsCurrent||controller?.SandboxEngine?.Result==null||returning||faultExit)return false;
            Phase=BattleFlowPhase.Settling;
            bool applied=session.BattleResults.CompleteEngine(Result,controller.SandboxEngine.Result,baseline,activity);
            if(!IsCurrent)return false;
            Phase=applied?BattleFlowPhase.ResultOpen:BattleFlowPhase.RecoveryRequired;Failure=Result.Failure;
            return applied;
        }
        public bool ExitFault()
        {
            if(!IsCurrent||Phase!=BattleFlowPhase.RecoveryRequired||Result?.Consumption==BattleResultConsumption.Applied)return false;
            // Nothing from the engine touched the live player. Never overwrite unrelated live changes.
            if(baseline==null||!baseline.Matches(session)){Failure="受控检查点不一致，不能安全退出。";return false;}
            faultExit=true;return TryReturn();
        }
        public bool TryReturn()
        {
            if(!IsCurrent||returning||Phase==BattleFlowPhase.Idle
                ||!faultExit&&Result?.Consumption!=BattleResultConsumption.Applied)return false;
            returning=true;Phase=BattleFlowPhase.Returning;
            try
            {
                if(!returnContext.TryRestore())throw new InvalidOperationException("返回位置不可用，请重试返回。");
                if(Result!=null)Result.ReturnRequested=true;
                controller.DisposeEngineSandbox();var previousView=view;view=null;
                // Keep the gate while handing off a pending stage challenge. No saveable frame between battles.
                bool handoff=PendingBoss;PendingBoss=false;
                if(handoff)
                {
                    Phase=BattleFlowPhase.Idle;
                    if(!Start(Guid.NewGuid().ToString("N"),session.stageState.currentStage.bossChallenge,null,stage,null,controller,
                        returnContext,(uint)Environment.TickCount,out var startError))
                    {Phase=BattleFlowPhase.RecoveryRequired;PendingBoss=true;Failure=startError;view=previousView;GameplayInputGate.Acquire(this);return false;}
                }
                else{Phase=BattleFlowPhase.Idle;GameplayInputGate.Release(this,true);}
                if(previousView!=null)UnityEngine.Object.Destroy(previousView.gameObject);
                return true;
            }
            catch(Exception ex){Failure=ex.Message;Phase=BattleFlowPhase.RecoveryRequired;return false;}
            finally{returning=false;}
        }
        public void CancelSession()
        {
            controller?.DisposeEngineSandbox();if(view!=null)UnityEngine.Object.Destroy(view.gameObject);view=null;
            Phase=BattleFlowPhase.Idle;sessionId=null;PendingBoss=false;paidRequests.Clear();GameplayInputGate.Release(this,true);
        }
    }
}
