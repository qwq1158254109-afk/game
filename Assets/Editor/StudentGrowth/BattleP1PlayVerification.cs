using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace StudentGrowth.EditorTools
{
    [InitializeOnLoad]
    public static class BattleP1PlayVerification
    {
        private const string Marker="Temp/BattleP1Play.active";
        private const string SaveMarker="Temp/SettingsPlayVerification.active";
        private static readonly List<string> checks=new List<string>();
        static BattleP1PlayVerification() { EditorApplication.playModeStateChanged += Changed; }
        [MenuItem("Tools/Student Growth/Battle/Verify P1 in Play Mode")]
        public static void Run()
        {
            if(EditorApplication.isPlaying || SceneManager.GetActiveScene().isDirty || File.Exists(SaveMarker)) throw new InvalidOperationException("Editor is busy or scene has unsaved changes.");
            Directory.CreateDirectory("Temp");Directory.CreateDirectory(BattleP1Verification.Output);
            File.WriteAllText(BattleP1Verification.Output+"/play.txt", "RUNNING " + DateTime.Now.ToString("O"));
            string id=Guid.NewGuid().ToString("N");File.WriteAllText(Marker,id);File.WriteAllText(SaveMarker,id);
            SetResolution(1920,1080,true);
            EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!File.Exists(Marker)) return;
            if(state==PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall+=()=>GameApplicationRuntime.Instance.StartCoroutine(Guard());
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                string resultPath=BattleP1Verification.Output+"/play.txt";
                if(File.ReadAllText(resultPath).StartsWith("RUNNING")) File.WriteAllText(resultPath,
                    File.ReadAllText(BattleP1Verification.Output+"/play-progress.txt")+"\nABORTED: Play Mode stopped before verification completed.");
                if(File.Exists(SaveMarker) && File.ReadAllText(SaveMarker)==File.ReadAllText(Marker)) File.Delete(SaveMarker);
                File.Delete(Marker);RestoreResolution();
            }
        }
        private static IEnumerator Guard()
        {
            checks.Clear(); Exception failure=null;
            yield return SaveCoroutineGuard.Run(Verify(),e=>failure=e);
            File.WriteAllText(BattleP1Verification.Output+"/play.txt",string.Join("\n",checks)+"\n"+(failure==null?"PASS ALL":"FAIL "+failure));
            EditorApplication.isPlaying=false;
        }
        private static void Check(bool value,string message)
        { checks.Add((value?"PASS ":"FAIL ")+message);File.WriteAllLines(BattleP1Verification.Output+"/play-progress.txt",checks);if(!value)throw new Exception(message); }
        private static object Call(object target,string method,params object[] args)
        { return target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,args); }
        private static IEnumerator Frames(int n) { for(int i=0;i<n;i++)yield return null; }
        private static IEnumerator Shot(string name)
        { Canvas.ForceUpdateCanvases();yield return Frames(4);ScreenCapture.CaptureScreenshot(BattleP1Verification.Output+"/"+name+".png");yield return Frames(8); }
        private static IEnumerator Verify()
        {
            yield return Frames(20);
            var app=GameApplicationRuntime.Instance;
            Check(app.SaveDirectory.Contains("PausePlayTests"),"isolated save directory");
            var go=new GameObject("BattleP1VerificationUI");var ui=go.AddComponent<StudentGrowthMvpRuntimeUi>();
            yield return Frames(3);
            go.GetComponent<Canvas>().sortingOrder=100;
            var battle=go.GetComponent<ChallengeController>();var stage=go.GetComponent<StageController>();
            if(!app.Lifetime.IsActive) app.BindSession(battle.sessionState);
            Call(ui,"StartThemeAuditChallengePreview");yield return Frames(3);
            Check(battle.runtimeState.data!=null && !battle.runtimeState.isComplete,"existing Boss entry starts playable battle");
            var config=Resources.Load<BattleUIConfig>(BattleUIConfig.ResourcePath);
            Check(config.artwork.Length==70 && config.artwork.All(x=>x.sprite!=null),"all 70 imported Sprite references resolve");
            Check(go.GetComponentsInChildren<Image>().Any(x=>x.sprite!=null && x.sprite.name.StartsWith("card_art_")),"supplied card art bound by stable cardId");
            yield return Shot("battle-1920x1080");

            var card=go.GetComponentsInChildren<BattleCardView>().First(c=>battle.CanPlayCard(c.Data));
            int cost=card.Data.cost;int energy=battle.runtimeState.currentEnergy;int hand=battle.deckController.deckState.hand.Count;
            var point=RectTransformUtility.WorldToScreenPoint(null,card.RectTransform.position);
            var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Any(h=>h.gameObject.transform.IsChildOf(card.transform) || h.gameObject==card.gameObject),"visible card remains raycast accessible through artwork");
            ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.pointerClickHandler);yield return Frames(3);
            Check(battle.runtimeState.currentEnergy==energy-cost,"UI click spends card cost exactly once");
            Check(battle.deckController.deckState.hand.Count==hand-1,"UI click moves one card");

            card=go.GetComponentsInChildren<BattleCardView>().FirstOrDefault(c=>battle.CanPlayCard(c.Data) && battle.GetEffectiveTargetType(c.Data)==CardTargetType.SingleEnemy);
            if(card!=null)
            {
                energy=battle.runtimeState.currentEnergy;cost=card.Data.cost;
                pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,card.RectTransform.position),button=PointerEventData.InputButton.Left};
                ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.beginDragHandler);
                pointer.position=new Vector2(-100,-100);ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.endDragHandler);
                yield return Frames(3);Check(battle.runtimeState.currentEnergy==energy,"cancelled target drag does not spend energy");
                var target=go.GetComponentsInChildren<RectTransform>().First(t=>t.name=="EnemyRoot");
                pointer.position=RectTransformUtility.WorldToScreenPoint(null,card.RectTransform.position);
                ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.beginDragHandler);
                pointer.position=RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center));
                ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(card.gameObject,pointer,ExecuteEvents.endDragHandler);
                yield return Frames(3);Check(battle.runtimeState.currentEnergy==energy-cost,"targeted drag spends card cost exactly once");
            }
            else throw new Exception("No affordable targeted card available for UI drag coverage");
            Call(ui,"OnEndTurnClicked");yield return Frames(2);
            Check(battle.runtimeState.currentTurn==2,"end turn through UI advances one turn");
            SetResolution(1366,768,false);yield return Shot("battle-1366x768");
            for (int i=0;i<16 && !battle.runtimeState.isComplete;i++)
            {
                card=go.GetComponentsInChildren<BattleCardView>().Where(c=>battle.CanPlayCard(c.Data))
                    .OrderByDescending(c=>c.Data.effects.Where(e=>e!=null && e.effectType==CardEffectType.AddChallengeProgress).Sum(e=>e.value)).FirstOrDefault();
                if(card==null) Call(ui,"OnEndTurnClicked");
                else ExecuteEvents.Execute(card.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
                yield return Frames(3);
            }
            Check(battle.runtimeState.isSuccess && battle.runtimeState.currentProgress>=battle.runtimeState.data.targetProgress,"real card plays reach challenge target and settle victory");
            Call(ui,"ShowBossResult",true);yield return Frames(3);
            int money=battle.sessionState.playerState.money;int count=battle.sessionState.stageState.clearedStageCount;
            var earlierContinue=go.GetComponentsInChildren<Button>().First(b=>b.name=="ContinueButton").onClick;
            for(int i=0;i<3;i++){Call(ui,"ShowBossResult",true);yield return null;}
            Check(battle.sessionState.playerState.money==money && battle.sessionState.stageState.clearedStageCount==count,"opening actual result View three times leaves rewards and stage counts unchanged");
            Check(go.GetComponentsInChildren<Image>().Any(x=>x.sprite!=null && x.sprite.name=="window_challenge_complete"),"complete window sprite is used");
            yield return Shot("result-complete-1366x768");
            // A distinct stage tests the failure presentation without undoing a completed result.
            var previousStage=battle.sessionState.stageState.currentStage;
            var continueButton=go.GetComponentsInChildren<Button>().First(b=>b.name=="ContinueButton");
            continueButton.onClick.Invoke();var nextStage=battle.sessionState.stageState.currentStage;
            continueButton.onClick.Invoke();
            earlierContinue.Invoke();
            Check(nextStage!=previousStage && battle.sessionState.stageState.currentStage==nextStage,"result Continue callback advances stage only once");
            Call(ui,"StartThemeAuditChallengePreview");
            for(int i=0;i<10 && !battle.runtimeState.isComplete;i++) { Call(ui,"OnEndTurnClicked");yield return Frames(2); }
            Call(ui,"ShowBossResult",false);
            Check(battle.Result.Consumption==BattleResultConsumption.Applied && !battle.Result.Success,"Boss failure uses the same result consumer");
            SetResolution(1920,1080,false);yield return Shot("result-incomplete-1920x1080");
        }
        private static void SetResolution(int w,int h,bool remember)
        {
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);var prop=type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(remember)SessionState.SetInt("BattleP1.PreviousGameSize",(int)prop.GetValue(view));
            var sizesType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});var gt=group.GetType();
            int count=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null)+(int)gt.GetMethod("GetCustomCount").Invoke(group,null);int index=-1;
            for(int i=0;i<count;i++){var size=gt.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});if((int)size.GetType().GetProperty("width").GetValue(size)==w && (int)size.GetType().GetProperty("height").GetValue(size)==h){index=i;break;}}
            if(index<0){var st=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");var ctor=st.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)[0];var size=ctor.Invoke(new object[]{Enum.ToObject(ctor.GetParameters()[0].ParameterType,1),w,h,"Battle QA "+w+"x"+h});gt.GetMethod("AddCustomSize").Invoke(group,new[]{size});index=count;}
            prop.SetValue(view,index);view.Show();view.Focus();
        }
        private static void RestoreResolution()
        {
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
            type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,SessionState.GetInt("BattleP1.PreviousGameSize",0));
        }
    }
}
