using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace StudentGrowth
{
    public sealed class UnityGameFlowBackend : IGameFlowBackend
    {
        private readonly UnityGameSaveSource source;
        private readonly GameApplicationRuntime app;
        public bool IsBusy { get; private set; }
        public float LoadTimeout=30, BindTimeout=10;
        private int operationId;
        public UnityGameFlowBackend(UnityGameSaveSource source, GameApplicationRuntime app=null) { this.source=source; this.app=app; }
        public IEnumerator RestoreAsync(LoadedGameState state,Action<CommandResult> completed) => Enter(state.Record.scenePath,state,completed);
        public IEnumerator StartNewAsync(string path,Action<CommandResult> completed) => Enter(path,null,completed);
        public IEnumerator ReturnToMenuAsync(Action<CommandResult> completed) => Enter(GameApplicationRuntime.MainMenuPath,null,completed);
        private IEnumerator Enter(string path,LoadedGameState loaded,Action<CommandResult> completed)
        {
            GameFlowDiagnostics.Trace("Enter "+path);
            if(IsBusy || AudioSettingsController.DisplayTransitionBlocked) { completed(new CommandResult(false,"FLOW_NOT_READY")); yield break; }
            bool menu=path==GameApplicationRuntime.MainMenuPath;
            if(SceneUtility.GetBuildIndexByScenePath(path)<0) { if(menu) app?.Recovery.Show("主菜单资源不可用。"); completed(new CommandResult(false,"FLOW_SCENE_UNAVAILABLE")); yield break; }
            if(app!=null && (app.SaveService.IsBusy || app.SaveService.CommitUncertain)) { completed(new CommandResult(false,"SAVE_COMMIT_UNCERTAIN")); yield break; }
            IsBusy=true; int token=++operationId; bool success=false;
            GameplayInputGate.Acquire(this); PlayerManager.Instance.SetInputLocked(true);
            GameFlowDiagnostics.Trace("Input locked");
            try
            {
                if(app!=null && app.Lifetime.IsActive) {
                    var ended=app.EndSession(app.Lifetime.RuntimeSessionId);
                    if(!ended.Success) { completed(ended); yield break; }
                } else { PlayerManager.Instance.EndSession(); GameBootstrapper.ClearSharedSession(); }
                // Destroy is deferred until the end of the frame. Starting an async scene load
                // before SpriteRenderer cleanup finishes can deadlock Unity's PersistentManager
                // against the preload thread (observed on Unity 6000.4.6f1).
                float cleanupDeadline=Time.realtimeSinceStartup+BindTimeout;
                while(PlayerManager.Instance.HasPendingPlayerCleanup)
                {
                    if(Time.realtimeSinceStartup>cleanupDeadline)
                    { completed(new CommandResult(false,"FLOW_PLAYER_CLEANUP_TIMEOUT")); yield break; }
                    yield return null;
                }
                GameFlowDiagnostics.Trace("Deferred player cleanup completed");
                if(loaded!=null) { GameBootstrapper.BeginRestore(loaded.Session); app?.BindSession(loaded.Session); }
                GameFlowDiagnostics.Trace("Session ended; requesting LoadSceneAsync");
                float deadline=Time.realtimeSinceStartup+LoadTimeout;
                AsyncOperation operation=null;
                try { operation=SceneManager.LoadSceneAsync(path,LoadSceneMode.Single); } catch(Exception ex) { Debug.LogWarning(ex.Message); }
                GameFlowDiagnostics.Trace("LoadSceneAsync returned");
                if(operation==null) { completed(new CommandResult(false,"FLOW_SCENE_LOAD_FAILED")); yield break; }
                bool expired=false;
                while(!operation.isDone) {
                    if(!expired && Time.realtimeSinceStartup>deadline) { expired=true; ++operationId; app?.Recovery.Show("场景加载超时，正在等待底层加载结束。"); }
                    yield return null;
                }
                if(expired || token!=operationId) { completed(new CommandResult(false,"FLOW_LOAD_TIMEOUT")); yield break; }
                GameFlowDiagnostics.Trace("Scene load completed");
                deadline=Time.realtimeSinceStartup+BindTimeout;
                string failure="FLOW_BIND_TIMEOUT";
                while(Time.realtimeSinceStartup<deadline)
                {
                    yield return null;
                    if(menu) {
                        var view=UnityEngine.Object.FindAnyObjectByType<MainMenuController>();
                        if(view!=null && view.IsReady && (app==null || !app.Lifetime.IsActive) && PlayerManager.Instance.CurrentPlayer==null) { success=true; break; }
                        continue;
                    }
                    var map=UnityEngine.Object.FindAnyObjectByType<FullscreenMapSceneController>();
                    var hub=UnityEngine.Object.FindAnyObjectByType<StageHubMapSceneController>();
                    var player=PlayerManager.Instance.ResolvePlayerAfterSceneLoad();
                    var session=loaded?.Session ?? UnityGameSaveSource.CurrentSession();
                    if(SceneManager.GetActiveScene().path!=path || session==null || player==null || (map==null && hub==null)) continue;
                    if(map!=null && (map.timeActionController==null || map.activityController==null || map.facingInteractor==null || map.hudView==null || map.interactionController==null)) continue;
                    if(app!=null && !app.Lifetime.IsActive) app.BindSession(session);
                    if(loaded==null && map!=null) session.locationState.currentLocationId=map.mapId;
                    foreach(var stage in UnityEngine.Object.FindObjectsByType<StageController>(FindObjectsInactive.Exclude)) stage.SetSessionState(session);
                    foreach(var time in UnityEngine.Object.FindObjectsByType<TimeActionController>(FindObjectsInactive.Exclude)) time.SetSessionState(session);
                    foreach(var transition in UnityEngine.Object.FindObjectsByType<SceneTransitionController>(FindObjectsInactive.Exclude)) transition.SetSessionState(session);
                    if(loaded!=null && loaded.Record.hasPlayerPosition) {
                        player.TeleportTo(new Vector2(loaded.Record.playerX,loaded.Record.playerY));
                        player.SetFacingDirection(new Vector2(loaded.Record.facingX,loaded.Record.facingY));
                    }
                    if(map!=null) { map.sessionState=session; map.RebindPlayer(player); map.RefreshHud(); }
                    if(hub!=null) { hub.sessionState=session; hub.RefreshPortals(); }
                    source.PlayedSeconds=loaded?.Record.playedSeconds ?? 0;
                    if(loaded!=null && app!=null) app.Lifetime.Saved(app.Lifetime.RuntimeSessionId,source.Capture());
                    success=true; break;
                }
                if(!success) { completed(new CommandResult(false,failure)); yield break; }
                app?.Recovery.Hide(); completed(new CommandResult(true));
                GameFlowDiagnostics.Trace("Ready published");
            }
            finally
            {
                GameBootstrapper.EndRestore(); IsBusy=false;
                if(success) { PlayerManager.Instance.SetInputLocked(false); GameplayInputGate.Release(this,true); }
                else {
                    if(app!=null && app.Lifetime.IsActive) app.EndSession(app.Lifetime.RuntimeSessionId);
                    PlayerManager.Instance.EndSession();
                    app?.Recovery.Show("场景未能就绪。进度未写入磁盘，请重试返回主菜单。");
                }
            }
        }
    }
}
