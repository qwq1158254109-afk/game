using System;
using System.Collections.Generic;

namespace StudentGrowth
{
    public partial class ChallengeController
    {
        public BattleEngine SandboxEngine { get; private set; }
        public bool PresentationBusy { get; set; }
        // P2 intentionally has no call to the P1 result consumer or real player mutation.
        public BattlePreparation StartEngineSandbox(ChallengeData challenge,IEnumerable<CardData> deck,PlayerState baseline,
            uint seed,IEnumerable<BattleEffect> opening=null)
        {
            var prepared=BattleCompiler.Prepare(challenge,deck,opening,sessionState.itemInventory.ownedItems);
            if(!prepared.Success)return prepared;
            string session=GameApplicationRuntime.CurrentRuntimeSessionId;
            if(string.IsNullOrEmpty(session)){prepared.Errors.Add("RuntimeSessionId: no active existing game session");return prepared;}
            if(SandboxEngine!=null){prepared.Errors.Add("A P2 sandbox is already active");return prepared;}
            if(runtimeState?.data!=null&&!runtimeState.isComplete){prepared.Errors.Add("An existing legacy battle is still active");return prepared;}
            try { SandboxEngine=new BattleEngine(prepared.Definition,baseline,session,Guid.NewGuid().ToString("N"),new BattleRandom(seed)); }
            catch(Exception ex){prepared.Errors.Add("Baseline: "+ex.Message);}
            return prepared;
        }
        public BattleActionReceipt PlayEngineCard(BattleRequest request)=>EngineAction(request,()=>SandboxEngine.TryPlayCard(request));
        public BattleActionReceipt EndEngineTurn(BattleRequest request)=>EngineAction(request,()=>SandboxEngine.TryEndTurn(request));
        public BattleActionReceipt SetEngineTurnCost(BattleRequest request,int delta)=>EngineAction(request,()=>SandboxEngine.TrySetTurnCostDelta(request,delta));
        private BattleActionReceipt EngineAction(BattleRequest request,Func<BattleActionReceipt> action)
        {
            if(SandboxEngine==null)return new BattleActionReceipt(false,BattleFailure.WrongBattle,null,null);
            if(request==null||request.RuntimeSessionId!=GameApplicationRuntime.CurrentRuntimeSessionId||!GameApplicationRuntime.Instance.Lifetime.Matches(request.RuntimeSessionId))
                return new BattleActionReceipt(false,BattleFailure.WrongSession,SandboxEngine.Snapshot(),SandboxEngine.Result);
            if(PresentationBusy)return new BattleActionReceipt(false,BattleFailure.Busy,SandboxEngine.Snapshot(),SandboxEngine.Result);
            if(sessionState.BattleFlow.BlocksSave && sessionState.BattleFlow.Phase!=BattleFlowPhase.Active)
                return new BattleActionReceipt(false,BattleFailure.Busy,SandboxEngine.Snapshot(),SandboxEngine.Result);
            return action();
        }
        public void DisposeEngineSandbox(){SandboxEngine?.Dispose();SandboxEngine=null;PresentationBusy=false;}
        private void OnDestroy(){DisposeEngineSandbox();}
    }
}
