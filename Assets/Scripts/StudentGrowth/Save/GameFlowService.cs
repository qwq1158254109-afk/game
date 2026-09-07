using System;
using System.Collections;
using System.Collections.Generic;

namespace StudentGrowth
{
    public interface IGameFlowBackend
    { IEnumerator RestoreAsync(LoadedGameState state,Action<CommandResult> completed); }
    public sealed class GameFlowService : IGameFlowService
    {
        private readonly IGameFlowBackend backend;
        private readonly PostLoadPipeline postLoad;
        private readonly Func<bool> navigationBlocked;
        public bool IsBusy { get; private set; }
        public GameFlowService(IGameFlowBackend backend,PostLoadPipeline postLoad,Func<bool> navigationBlocked)
        { this.backend = backend; this.postLoad = postLoad; this.navigationBlocked = navigationBlocked; }
        public IEnumerator RestoreLoadedGameAsync(LoadedGameState state,Action<GameFlowRestoreResult> completed)
        {
            if (state == null || IsBusy || navigationBlocked())
            { completed?.Invoke(new GameFlowRestoreResult(false,"FLOW_NOT_READY")); yield break; }
            IsBusy = true;
            GameFlowRestoreResult result;
            try
            {
                CommandResult restored = null;
                IEnumerator routine = null; try { routine = backend.RestoreAsync(state,r => restored = r); } catch { }
                if (routine != null) yield return SaveCoroutineGuard.Run(routine,_ => restored = null);
                if (restored == null || !restored.Success) result = new GameFlowRestoreResult(false,restored?.ErrorCode ?? "FLOW_RESTORE_FAILED");
                else
                {
                    IReadOnlyList<PostLoadHandlerResult> post = null;
                    yield return postLoad.ExecuteAsync(new LoadedGameContext(state),r => post = r);
                    // Optional platform failure is diagnostic; a restored game remains playable.
                    result = new GameFlowRestoreResult(true,post: post);
                }
            }
            finally { IsBusy = false; }
            completed?.Invoke(result);
        }
    }
}
