using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object=UnityEngine.Object;

namespace StudentGrowth
{
    // Explicit, isolated verification through the same map entry and UI used by players.
    public static class BattleP4Smoke
    {
        public static bool Requested=>Array.IndexOf(Environment.GetCommandLineArgs(),"-battleP4Smoke")>=0;
        public static string PlayerOutput=>Path.GetFullPath(Path.Combine(Application.dataPath,"../BattleP4Smoke"));
        private static string output;
        private static int errors;
        private static readonly List<string> checks=new List<string>();
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRequested(){if(Requested)GameApplicationRuntime.Instance.StartCoroutine(Run(PlayerOutput,ok=>Application.Quit(ok?0:1)));}
        public static IEnumerator Run(string folder,Action<bool> complete)
        {
            output=folder;Directory.CreateDirectory(output);checks.Clear();errors=0;File.WriteAllText(output+"/play.txt","RUNNING");
            Exception failure=null;Application.logMessageReceived+=OnLog;
            yield return SaveCoroutineGuard.Run(Verify(),ex=>failure=ex);
            Application.logMessageReceived-=OnLog;bool okay=failure==null&&errors==0;
            File.WriteAllText(output+"/play.txt",string.Join("\n",checks)+"\n"+(okay?"PASS ALL":"FAIL "+failure+" Errors="+errors));complete(okay);
        }
        private static void OnLog(string message,string stack,LogType type){if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors++;}
        private static void Check(bool okay,string label){checks.Add((okay?"PASS ":"FAIL ")+label);File.WriteAllLines(output+"/progress.txt",checks);if(!okay)throw new Exception(label);}
        private static IEnumerator Frames(int count=4){for(int i=0;i<count;i++)yield return null;}
        private static IEnumerator Until(Func<bool> condition,string label)
        {float deadline=Time.realtimeSinceStartup+35;while(!condition()){if(Time.realtimeSinceStartup>deadline)throw new Exception("Timeout: "+label);yield return null;}}
        private static Button Named(string name)=>Object.FindObjectsByType<Button>().First(b=>b.name==name);
        private static void Click(Button b)=>ExecuteEvents.Execute(b.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        private static BattleCardView[] Cards()=>Object.FindObjectsByType<BattleCardView>().Where(c=>c.InstancePreview!=null).ToArray();
        private static BattleCardView Find(string suffix)=>Cards().FirstOrDefault(c=>c.InstancePreview.Card.DefinitionId=="card_p4_"+suffix&&c.Playable);
        private static IEnumerator Shot(string name){yield return Frames();Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot(output+"/"+name+".png");yield return Frames(12);}
        private static bool HasFocus(BattleSnapshot s)=>s.Statuses.Any(t=>t.Kind==BattleStatusKind.Focus);
        private static int Focus(BattleSnapshot s)=>s.Statuses.FirstOrDefault(t=>t.Kind==BattleStatusKind.Focus)?.Intensity??0;
        private static IEnumerator Verify()
        {
            yield return Until(()=>Object.FindAnyObjectByType<MainMenuController>()?.IsReady==true,"main menu");yield return Frames(15);
            var app=GameApplicationRuntime.Instance;
            string storage=Path.GetFullPath(app.SaveDirectory).TrimEnd(Path.DirectorySeparatorChar);
            string expected=Path.GetFullPath(Requested?Path.Combine(PlayerOutput,"Saves"):Path.Combine(Application.dataPath,"../Temp/PausePlayTests")).TrimEnd(Path.DirectorySeparatorChar);
            Check(string.Equals(Path.GetDirectoryName(storage),expected,StringComparison.OrdinalIgnoreCase)&&Guid.TryParseExact(Path.GetFileName(storage),"N",out _)
                &&(!Directory.Exists(storage)||Directory.GetFileSystemEntries(storage).Length==0),"fresh GUID test-only save directory");
            File.WriteAllText(output+"/isolation.txt",storage);
            Click(Object.FindObjectsByType<Button>().First(b=>b.GetComponentsInChildren<Text>().Any(t=>t.text=="开始游戏")));
            yield return Until(()=>SceneManager.GetActiveScene().path==GameApplicationRuntime.HomePath&&!app.Backend.IsBusy,"home");yield return Frames(20);
            if(Requested){Screen.SetResolution(1366,768,FullScreenMode.Windowed);yield return Until(()=>Screen.width==1366&&Screen.height==768,"player resolution");}
            var map=Object.FindAnyObjectByType<FullscreenMapSceneController>();var session=map.sessionState;var flow=session.BattleFlow;
            var interaction=Object.FindAnyObjectByType<MapInteractionFlowController>();var desk=map.interactionTargets.First(t=>t.interactionId=="activity_home_desk_plan");
            map.playerController.TeleportTo((Vector2)desk.transform.position+Vector2.down*.55f);map.playerController.SetFacingDirection(Vector2.up);yield return Frames();
            var permanent=session.deckState.masterDeck.ToArray();int time=session.stageState.remainingTime,ap=session.timeActionState.RemainingDailyActionPoints,math=session.playerState.math;
            Vector2 position=map.playerController.transform.position;
            Check(interaction.TryBeginInteraction(desk),"normal home desk confirmation opens");yield return Frames();
            Check(Named("DeskBattle").interactable&&Named("DeskStrategyBattle").interactable,"original challenge and strategy entry coexist");yield return Shot("desk-strategy-entry");
            Click(Named("DeskStrategyBattle"));yield return Frames();
            var engine=map.challengeController.SandboxEngine;
            Check(engine!=null&&flow.Phase==BattleFlowPhase.Active,"real strategy button starts formal engine");
            Check(engine.Snapshot().Definition.Id=="challenge_strategy_practice"&&engine.Snapshot().Cards.Count==10,"ten loan instances and six card definitions");
            Check(session.deckState.masterDeck.SequenceEqual(permanent),"permanent deck unchanged at entry");
            Check(session.stageState.remainingTime==time-1&&session.timeActionState.RemainingDailyActionPoints==ap-1,"one stage time and one daily action charged");
            var energy=Find("adjust_rhythm");if(energy!=null){int before=engine.Snapshot().Energy;string id=energy.InstancePreview.Card.InstanceId;Click(energy.GetComponent<Button>());yield return Frames();Check(engine.Snapshot().Energy==before+1&&engine.Snapshot().InPile(BattlePile.Exhaust).Any(c=>c.InstanceId==id),"energy conversion exhausts the exact instance");}
            var focus=Find("enter_focus");if(focus!=null){Click(focus.GetComponent<Button>());yield return Frames();}
            var intentBefore=engine.GetIntentPreview();Check(intentBefore.Statuses.Any(s=>s.Kind==BattleStatusKind.Distraction&&s.ActiveFromPlayerTurn==2),"intent previews next player turn without fake damage");
            Check(engine.Snapshot().Hp==intentBefore.Hp,"auxiliary intent deals zero damage");
            // Use remaining first-turn actions to keep the strategy run representative.
            yield return SpendTurn(map.challengeController);
            Click(Named("EndTurn"));yield return Frames();
            Check(engine.Snapshot().Turn==2&&engine.Snapshot().Statuses.Count(s=>!s.IsPersistent)==2,"both enemy statuses cover player turn two");
            if(!HasFocus(engine.Snapshot()))
            {
                focus=Find("enter_focus");Check(focus!=null,"focus available by second opening hand");Click(focus.GetComponent<Button>());yield return Frames();
            }
            Check(Object.FindObjectsByType<RectTransform>().Count(r=>r.name=="Status_Focus"||r.name=="Status_Distraction"||r.name=="Status_WeakDefense")==3,"all three status rows render");
            Check(Object.FindObjectsByType<Image>().Where(i=>i.transform.parent!=null&&i.transform.parent.name.StartsWith("Status_")&&i.name=="Icon").All(i=>i.sprite!=null),"status icons use supplied artwork");
            var statusTarget=Object.FindObjectsByType<BattleTooltipTarget>().First(t=>t.name=="Status_Distraction");
            ExecuteEvents.Execute(statusTarget.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerEnterHandler);yield return Frames();
            Check(Object.FindObjectsByType<Text>().Any(t=>t.name=="TooltipBody"&&t.text.Contains("有效玩家回合：2—2")),"status tooltip shows exact effective interval");
            yield return Shot("strategy-status-tooltip");ExecuteEvents.Execute(statusTarget.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerExitHandler);
            yield return Shot("strategy-statuses");
            var damageCard=Cards().Where(c=>c.Playable&&c.InstancePreview.ProgressChange>0).OrderByDescending(c=>c.InstancePreview.ProgressChange).FirstOrDefault();
            if(damageCard!=null)
            {
                var binding=damageCard.InstancePreview;int before=engine.Snapshot().Progress;
                var target=Object.FindObjectsByType<RectTransform>().First(t=>t.name=="BattleTarget");
                var pointer=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=RectTransformUtility.WorldToScreenPoint(null,damageCard.RectTransform.position)};
                ExecuteEvents.Execute(damageCard.gameObject,pointer,ExecuteEvents.beginDragHandler);pointer.position=RectTransformUtility.WorldToScreenPoint(null,target.position);
                ExecuteEvents.Execute(damageCard.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(damageCard.gameObject,pointer,ExecuteEvents.endDragHandler);yield return Frames();
                Check(engine.Snapshot().Progress==before+binding.ProgressChange,"real drag executes the displayed status-modified progress");
            }
            yield return SpendTurn(map.challengeController);int expectedHp=engine.GetIntentPreview().Hp;
            Click(Named("EndTurn"));yield return Frames();
            Check(engine.Snapshot().Turn==3&&engine.Snapshot().Statuses.All(s=>s.IsPersistent),"timed debuffs expire at turn two end; focus persists");
            Check(engine.Snapshot().Hp==expectedHp,"multi-hit intent preview equals execution after actual block");
            int guard=0;
            while(engine.Result==null&&guard++<8){yield return SpendTurn(map.challengeController);if(engine.Result==null){Click(Named("EndTurn"));yield return Frames();}}
            Check(engine.Result?.Outcome==BattleOutcome.Victory,"strategy card combination reaches victory");
            Check(engine.Snapshot().Statuses.Count==0&&flow.Result.Consumption==BattleResultConsumption.Applied,"statuses removed and fixed result committed");
            Check(session.playerState.math==math+2&&session.stageState.clearedStageCount==0,"strategy reward once without stage advancement");
            bool blocked=false;try{app.Source.Capture();}catch(InvalidOperationException){blocked=true;}Check(blocked,"result window retains save gate");
            yield return Shot("strategy-result");Click(Named("Return"));yield return Frames();
            Check(!flow.BlocksSave&&Vector2.Distance(position,map.playerController.transform.position)<.01f,"return restores original map position");
            Check(session.deckState.masterDeck.SequenceEqual(permanent),"loan cards never enter permanent deck after result");
            Check(app.Source.Capture()!=null,"save permitted after return");
            Check(!Directory.Exists(Path.Combine(storage,"Slot01"))&&app.SaveService.GetSlotSummaries()[1].IsEmpty,"save slot empty before first write");
            SaveOperationResult saved=null;yield return app.SaveService.SaveAsync(new SaveSlotId(1),r=>saved=r);Check(saved?.Success==true,"post-strategy map save succeeds");
            LoadOperationResult loaded=null;yield return app.SaveService.LoadAsync(new SaveSlotId(1),r=>loaded=r);Check(loaded?.Success==true,"post-strategy save decodes");
            Check(errors==0,"no runtime Console errors");
        }
        private static IEnumerator SpendTurn(ChallengeController controller)
        {
            int guard=0;
            while(controller.SandboxEngine.Result==null&&guard++<20)
            {
                var s=controller.SandboxEngine.Snapshot();var cards=Cards().Where(c=>c.Playable).ToArray();
                var next=cards.FirstOrDefault(c=>c.InstancePreview.Card.DefinitionId.EndsWith("adjust_rhythm"))
                    ??(s.Turn<=3&&Focus(s)<4?cards.FirstOrDefault(c=>c.InstancePreview.Card.DefinitionId.EndsWith("enter_focus")):null)
                    ??cards.Where(c=>c.InstancePreview.ProgressChange>0).OrderByDescending(c=>c.InstancePreview.ProgressChange).FirstOrDefault()
                    ??cards.FirstOrDefault(c=>c.InstancePreview.Card.DefinitionId.EndsWith("browse_notes"))
                    ??cards.FirstOrDefault(c=>c.InstancePreview.BlockChange>0);
                if(next==null)break;
                var preview=next.InstancePreview;int progress=s.Progress,block=s.Block;
                Click(next.GetComponent<Button>());yield return Frames();
                Check(controller.SandboxEngine.Snapshot().Progress==progress+preview.ProgressChange&&controller.SandboxEngine.Snapshot().Block==block+preview.BlockChange,"card click matches current preview: "+preview.Card.Definition.Name);
            }
        }
    }
}
