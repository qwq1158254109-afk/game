using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudentGrowth
{
    // Explicit opt-in only. The same smoke runs through production UI in Editor and a normal player.
    public static class BattleP3Smoke
    {
        public static bool Requested => Array.IndexOf(Environment.GetCommandLineArgs(),"-battleP3Smoke")>=0;
        public static string PlayerOutput => Path.GetFullPath(Path.Combine(Application.dataPath,"../BattleSmoke"));
        private static string output;
        private static readonly List<string> checks=new List<string>();
        private static int errors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRequested()
        {if(Requested)GameApplicationRuntime.Instance.StartCoroutine(Run(PlayerOutput,ok=>Application.Quit(ok?0:1)));}
        public static IEnumerator Run(string folder,Action<bool> complete)
        {
            output=folder;Directory.CreateDirectory(output);checks.Clear();errors=0;File.WriteAllText(output+"/play.txt","RUNNING");
            Application.logMessageReceived+=OnLog;Exception failure=null;
            yield return SaveCoroutineGuard.Run(Verify(),ex=>failure=ex);
            Application.logMessageReceived-=OnLog;
            bool success=failure==null&&errors==0;
            File.WriteAllText(output+"/play.txt",string.Join("\n",checks)+"\n"+(success?"PASS ALL":"FAIL "+failure+" Errors="+errors));
            complete(success);
        }
        private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static void Check(bool okay,string label)
        {checks.Add((okay?"PASS ":"FAIL ")+label);File.WriteAllLines(output+"/progress.txt",checks);if(!okay)throw new Exception(label);}
        private static IEnumerator Frames(int count=4){for(int i=0;i<count;i++)yield return null;}
        private static IEnumerator Until(Func<bool> condition,string label)
        {float deadline=Time.realtimeSinceStartup+35;while(!condition()){if(Time.realtimeSinceStartup>deadline)throw new Exception("Timeout: "+label);yield return null;}}
        private static Button Named(string name)=>UnityEngine.Object.FindObjectsByType<Button>().First(b=>b.name==name);
        private static void Click(Button button)=>ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        private static IEnumerator Shot(string name){yield return Frames();Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(output+"/"+name+".png");yield return Frames(12);}
        private static void SaveBlocked(GameApplicationRuntime app,string label)
        {bool blocked=false;try{app.Source.Capture();}catch(InvalidOperationException e){blocked=e.Message=="SAVE_BATTLE_FLOW_BUSY";}Check(blocked,label);}
        private static void OpenDesk(MapInteractionFlowController interaction,MapInteractionTarget desk)
        {
            Check(interaction.TryBeginInteraction(desk),"existing desk interaction opens");
            File.WriteAllText(output+"/desk-debug.txt","Flow="+interaction.CurrentState+" ConfirmView="+interaction.confirmView+" Panel="+interaction.confirmPanel
                +"\n"+string.Join("\n",UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include).Select(b=>b.name+" active="+b.gameObject.activeInHierarchy)));
            Click(Named("DeskBattle"));
        }
        private static IEnumerator PlayToWin(ChallengeController controller)
        {
            int actions=0;
            while(controller.SandboxEngine.Result==null && actions++<40)
            {
                yield return Frames();
                var card=UnityEngine.Object.FindObjectsByType<BattleCardView>().Where(v=>v.InstancePreview!=null&&v.Playable)
                    .OrderByDescending(v=>v.InstancePreview.ProgressChange).FirstOrDefault();
                if(card!=null)Click(card.GetComponent<Button>());else Click(Named("EndTurn"));
            }
            Check(controller.SandboxEngine.Result?.Outcome==BattleOutcome.Victory,"real card buttons reach victory");yield return Frames();
        }
        private static IEnumerator Lose(ChallengeController controller)
        {for(int i=0;i<15&&controller.SandboxEngine.Result==null;i++){Click(Named("EndTurn"));yield return Frames();}Check(controller.SandboxEngine.Result?.Outcome==BattleOutcome.Defeat,"real end-turn button reaches defeat");}
        private static IEnumerator Verify()
        {
            yield return Until(()=>UnityEngine.Object.FindAnyObjectByType<MainMenuController>()?.IsReady==true,"main menu");yield return Frames(15);
            var app=GameApplicationRuntime.Instance;
            string storage=Path.GetFullPath(app.SaveDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string expectedParent=Path.GetFullPath(Requested?Path.Combine(PlayerOutput,"Saves"):Path.Combine(Application.dataPath,"../Temp/PausePlayTests")).TrimEnd(Path.DirectorySeparatorChar);
            Check(string.Equals(Path.GetDirectoryName(storage),expectedParent,StringComparison.OrdinalIgnoreCase)
                &&Guid.TryParseExact(Path.GetFileName(storage),"N",out _)
                &&(!Directory.Exists(storage)||Directory.GetFileSystemEntries(storage).Length==0),"save isolation: fresh GUID directory under test-only root");
            File.WriteAllText(output+"/isolation.txt",storage);
            var start=UnityEngine.Object.FindObjectsByType<Button>().First(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text=="开始游戏"));
            Click(start);
            yield return Until(()=>SceneManager.GetActiveScene().path==GameApplicationRuntime.HomePath && !app.Backend.IsBusy,"main menu -> home");yield return Frames(20);
            if(Requested)
            {
                Screen.SetResolution(1366,768,FullScreenMode.Windowed);
                yield return Until(()=>Screen.width==1366&&Screen.height==768,"1366x768 player resolution");yield return Frames();
                Check(Screen.width==1366&&Screen.height==768,"player UI verified at 1366x768");
            }
            var map=UnityEngine.Object.FindAnyObjectByType<FullscreenMapSceneController>();var state=map.sessionState;var flow=state.BattleFlow;
            var controller=map.challengeController;var interaction=UnityEngine.Object.FindAnyObjectByType<MapInteractionFlowController>();
            var desk=map.interactionTargets.First(t=>t.interactionId=="activity_home_desk_plan");
            map.playerController.TeleportTo((Vector2)desk.transform.position+Vector2.down*.55f);map.playerController.SetFacingDirection(Vector2.up);yield return Frames();
            Check(desk.CanSelect(map.playerController.transform.position,map.playerController.FacingDirection),"desk is reachable and facing-selectable");
            int planTime=state.stageState.remainingTime;
            Check(interaction.TryBeginInteraction(desk),"original desk planning option remains available");yield return Frames();
            Click(interaction.confirmView.confirmButton);yield return Frames();
            if(interaction.resultView?.closeButton!=null){Click(interaction.resultView.closeButton);yield return Frames();}
            Check(state.stageState.remainingTime==planTime-Math.Max(0,desk.fallbackActivity.timeCost),"original planning activity still consumes its configured time");
            var afterPlan=new BattleCheckpoint(state);
            interaction.TryBeginInteraction(desk);yield return Frames();
            Check(!interaction.confirmView.confirmButton.interactable,"completed planning is disabled while battle option remains available");
            interaction.ConfirmPending();yield return Frames();
            Check(afterPlan.Matches(state),"keyboard confirmation cannot repeat completed planning");
            int time=state.stageState.remainingTime,ap=state.timeActionState.RemainingDailyActionPoints,math=state.playerState.math;
            var player=state.playerState;var baseline=new BattleCheckpoint(state);int notifications=0;Action changed=()=>notifications++;map.timeActionController.StateChanged+=changed;
            var oldSaveSnapshot=app.Source.Capture();
            flow.StartupProbe=p=>{if(p=="ViewReady")throw new Exception("P3 injected view startup failure");};
            OpenDesk(interaction,desk);yield return Frames();
            Check(baseline.Matches(state)&&notifications==0&&!flow.BlocksSave&&!flow.PendingBoss,"startup failure restores cost and discards next-frame notifications");
            flow.StartupProbe=null;OpenDesk(interaction,desk);yield return Frames();map.timeActionController.StateChanged-=changed;
            Check(flow.Phase==BattleFlowPhase.Active&&notifications==1,"next legitimate action publishes exactly one notification");
            Check(state.stageState.remainingTime==time-1 && state.timeActionState.RemainingDailyActionPoints==ap-1,"each time budget charged once");
            Check(ReferenceEquals(player,state.playerState),"player identity preserved at entry");
            SaveBlocked(app,"active battle blocks all save capture");
            SaveOperationResult blockedSave=null;
            yield return app.SaveService.SaveSnapshotAsync(oldSaveSnapshot,app.Lifetime.RuntimeSessionId,r=>blockedSave=r);
            Check(blockedSave?.Success==false&&blockedSave.ErrorCode=="SAVE_BATTLE_FLOW_BUSY","pre-captured automatic snapshot cannot bypass save gate");
            Check(!map.interactionController.TryInteract(desk).success,"map input cannot re-enter while battle active");
            yield return Shot("map-battle");
            var dragCard=UnityEngine.Object.FindObjectsByType<BattleCardView>().First(v=>v.Playable&&v.InstancePreview.Card.Definition.Target==CardTargetType.SingleEnemy);
            int revision=controller.SandboxEngine.Snapshot().Revision,energy=controller.SandboxEngine.Snapshot().Energy,cost=dragCard.Cost;
            var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,dragCard.RectTransform.position)};
            ExecuteEvents.Execute(dragCard.gameObject,pointer,ExecuteEvents.beginDragHandler);pointer.position=new Vector2(-50,-50);
            ExecuteEvents.Execute(dragCard.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(dragCard.gameObject,pointer,ExecuteEvents.endDragHandler);yield return Frames();
            Check(controller.SandboxEngine.Snapshot().Revision==revision&&controller.SandboxEngine.Snapshot().Energy==energy,"cancelled drag leaves rules and energy unchanged");
            dragCard=UnityEngine.Object.FindObjectsByType<BattleCardView>().First(v=>v.Playable&&v.InstancePreview.Card.Definition.Target==CardTargetType.SingleEnemy);
            cost=dragCard.Cost;var target=UnityEngine.Object.FindObjectsByType<RectTransform>().First(t=>t.name=="BattleTarget");
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,dragCard.RectTransform.position);ExecuteEvents.Execute(dragCard.gameObject,pointer,ExecuteEvents.beginDragHandler);
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,target.position);ExecuteEvents.Execute(dragCard.gameObject,pointer,ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(dragCard.gameObject,pointer,ExecuteEvents.endDragHandler);yield return Frames();
            Check(controller.SandboxEngine.Snapshot().Energy==energy-cost&&controller.SandboxEngine.Snapshot().Revision==revision+1,"targeted drag plays exactly its bound instance");
            yield return PlayToWin(controller);
            Check(flow.Result.Consumption==BattleResultConsumption.Applied&&state.playerState.math==math+2,"normal victory awards once at terminal");
            Check(state.stageState.clearedStageCount==0,"normal victory does not advance stage");SaveBlocked(app,"result display stays unsaveable");
            var resultBaseline=new BattleCheckpoint(state);var oldReturn=Named("Return").onClick;
            var origin=(BattleReturnContext)typeof(BattleSessionFlow).GetField("returnContext",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(flow);
            origin.RestoreProbe=()=>false;Click(Named("Return"));yield return Frames();
            Check(flow.Phase==BattleFlowPhase.RecoveryRequired&&resultBaseline.Matches(state),"return failure preserves committed result");SaveBlocked(app,"failed return blocks save");
            origin.RestoreProbe=null;Click(Named("Return"));yield return Frames();
            Check(!flow.BlocksSave&&resultBaseline.Matches(state),"retry returns without a second cost or reward");
            Check(app.Source.Capture()!=null,"save capture permitted after restored map");
            int secondTime=state.stageState.remainingTime;OpenDesk(interaction,desk);yield return Frames();yield return Lose(controller);
            Check(!flow.Result.Success&&state.stageState.remainingTime==secondTime-1&&state.stageState.clearedStageCount==0,"normal defeat keeps entry cost without stage completion");
            oldReturn.Invoke();Check(flow.Phase==BattleFlowPhase.ResultOpen,"old result callback cannot return a newer battle");
            Click(Named("Return"));yield return Frames();
            state.stageState.remainingTime=1;OpenDesk(interaction,desk);yield return Frames();
            Check(flow.PendingBoss&&state.stageState.bossUnlocked,"last ordinary activity records pending boss");
            yield return PlayToWin(controller);int bossAp=state.timeActionState.RemainingDailyActionPoints;
            Click(Named("Return"));yield return Frames();
            Check(controller.SandboxEngine?.Snapshot().Definition.IsBoss==true&&flow.BlocksSave,"return hands off directly to boss with save gate held");
            Check(state.timeActionState.RemainingDailyActionPoints==bossAp,"automatic boss does not charge activity time");
            yield return Lose(controller);Check(state.stageState.clearedStageCount==0&&!state.endingReached,"boss defeat leaves stage retryable");
            Click(Named("Return"));yield return Frames();Click(Named("StageBossEntry"));yield return Frames();
            Check(state.timeActionState.RemainingDailyActionPoints==bossAp,"boss retry does not charge activity time");
            yield return PlayToWin(controller);Check(state.stageState.clearedStageCount==1&&state.stageState.defeatedBossCount==1,"boss retry victory claims completion once");
            yield return Shot("boss-result");Click(Named("Return"));yield return Frames();
            Check(state.stageState.currentStage.stageId=="stage_02_month_exam"&&!flow.BlocksSave,"boss victory proceeds to next stage and restores map");
            Check(app.Source.Capture()!=null,"post-stage map can be saved");
            Check(!Directory.Exists(Path.Combine(storage,"Slot01"))&&app.SaveService.GetSlotSummaries()[1].IsEmpty,"test slot is empty before first write; existing slots are never overwritten");
            SaveOperationResult saved=null;yield return app.SaveService.SaveAsync(new SaveSlotId(1),r=>saved=r);
            Check(saved?.Success==true,"manual save writes restored map state");
            LoadOperationResult loaded=null;yield return app.SaveService.LoadAsync(new SaveSlotId(1),r=>loaded=r);
            Check(loaded?.Success==true,"saved post-battle state decodes through existing codec");
            OpenDesk(interaction,desk);yield return Frames();var oldController=controller;var oldSnapshot=controller.SandboxEngine.Snapshot();
            var ended=app.EndSession(app.Lifetime.RuntimeSessionId);
            Check(ended.Success&&!flow.BlocksSave&&controller.SandboxEngine==null,"existing session termination cancels battle and releases ownership");
            Check(!oldController.EndEngineTurn(new BattleRequest(oldSnapshot.RuntimeSessionId,oldSnapshot.BattleId,oldSnapshot.Revision,"stale")).Accepted,"old callbacks rejected after session end");
            Check(errors==0,"no runtime Console errors");
        }
    }
}
