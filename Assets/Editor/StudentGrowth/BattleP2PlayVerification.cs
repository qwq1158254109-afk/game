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
    public static class BattleP2PlayVerification
    {
        private const string Marker="Temp/BattleP2Play.active";
        private const string Isolation="Temp/SettingsPlayVerification.active";
        private const string Manual="Temp/BattleP2Manual.active";
        private static readonly List<string> checks=new List<string>();
        static BattleP2PlayVerification(){EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("Tools/Student Growth/Battle/P2/Verify instance UI in Play Mode")]
        public static void Run()=>Start(false);
        [MenuItem("Tools/Student Growth/Battle/P2/Open isolated interactive sandbox")]
        public static void Open()=>Start(true);
        private static void Start(bool manual)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty||File.Exists(Isolation))throw new InvalidOperationException("Save the current scene and finish other tests first.");
            if(AssetDatabase.LoadAssetAtPath<BattleChallengeAsset>(BattleP2ContentBuilder.Root+"/boss_class_quiz.asset")==null
                ||AssetDatabase.LoadAssetAtPath<BattleCardAsset>(BattleP2ContentBuilder.Root+"/card_study_math_practice.asset")==null)
                BattleP2ContentBuilder.Build();
            Directory.CreateDirectory(BattleP2ContentBuilder.Output);
            File.WriteAllText(BattleP2ContentBuilder.Output+"/play.txt","RUNNING");
            string id=Guid.NewGuid().ToString("N");File.WriteAllText(Marker,id);File.WriteAllText(Isolation,id);
            if(manual)File.WriteAllText(Manual,id);
            Resolution(1920,1080,true);EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!File.Exists(Marker))return;
            if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>GameApplicationRuntime.Instance.StartCoroutine(Guard());
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(File.Exists(Isolation)&&File.ReadAllText(Isolation)==File.ReadAllText(Marker))File.Delete(Isolation);
                if(File.Exists(Manual))File.Delete(Manual);File.Delete(Marker);RestoreResolution();
                string report=BattleP2ContentBuilder.Output+"/play.txt";
                if(File.ReadAllText(report)=="RUNNING")File.WriteAllText(report,"ABORTED before completion");
            }
        }
        private static IEnumerator Guard()
        {
            checks.Clear();Exception failure=null;
            yield return SaveCoroutineGuard.Run(Verify(),e=>failure=e);
            bool manual=File.Exists(Manual);
            File.WriteAllText(BattleP2ContentBuilder.Output+"/play.txt",string.Join("\n",checks)+"\n"+(failure!=null?"FAIL "+failure:manual?"MANUAL SANDBOX READY":"PASS ALL"));
            if(!manual||failure!=null)EditorApplication.isPlaying=false;
        }
        private static void Check(bool okay,string text)
        {checks.Add((okay?"PASS ":"FAIL ")+text);File.WriteAllLines(BattleP2ContentBuilder.Output+"/play-progress.txt",checks);if(!okay)throw new Exception(text);}
        private static IEnumerator Frames(int n){for(int i=0;i<n;i++)yield return null;}
        private static IEnumerator Shot(string name)
        {Canvas.ForceUpdateCanvases();yield return Frames(4);ScreenCapture.CaptureScreenshot(BattleP2ContentBuilder.Output+"/"+name+".png");yield return Frames(8);}
        private static BattleRequest Request(BattleSnapshot s,string id=null)=>new BattleRequest(s.RuntimeSessionId,s.BattleId,s.Revision,Guid.NewGuid().ToString("N"),id);
        private static void Click(BattleCardView view)=>ExecuteEvents.Execute(view.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
        private static IEnumerator Verify()
        {
            yield return Frames(20);var app=GameApplicationRuntime.Instance;
            Check(app.SaveDirectory.Contains("PausePlayTests"),"save isolation active");
            var root=new GameObject("BattleP2SandboxUI");var ui=root.AddComponent<StudentGrowthMvpRuntimeUi>();yield return Frames(3);
            root.GetComponent<Canvas>().sortingOrder=100;
            var controller=root.GetComponent<ChallengeController>();if(!app.Lifetime.IsActive)app.BindSession(controller.sessionState);
            var math=Resources.Load<BattleCardAsset>("StudentGrowth/Battle/P2/card_study_math_practice");
            var challenge=Resources.Load<BattleChallengeAsset>("StudentGrowth/Battle/P2/boss_class_quiz");
            Check(math!=null&&challenge!=null&&MonoScript.FromScriptableObject(math)!=null,"migrated assets survive domain reload with script references");
            string baseline=JsonUtility.ToJson(controller.sessionState.playerState),source=JsonUtility.ToJson(math);
            var permanent=controller.sessionState.deckState.masterDeck.ToArray();
            CardData[] cards=File.Exists(Manual)?Resources.LoadAll<BattleCardAsset>("StudentGrowth/Battle/P2").Cast<CardData>().Concat(new[]{math}).ToArray():new CardData[]{math,math};
            Check(ui.OpenEngineSandbox(challenge,cards,2468),"existing Controller opens isolated Engine with frozen definitions");yield return Frames(2);
            if(File.Exists(Manual))yield break;
            var engine=controller.SandboxEngine;var hand=engine.Snapshot().InPile(BattlePile.Hand);
            string a=hand[0].InstanceId,b=hand[1].InstanceId;
            controller.SetEngineTurnCost(Request(engine.Snapshot(),a),-1);
            controller.SetEngineTurnCost(Request(engine.Snapshot(),b),3);
            ui.RefreshEngineSandbox("同定义实例：一张免费，另一张费用不足。",true);yield return Frames(3);
            var views=root.GetComponentsInChildren<BattleCardView>();var va=views.Single(v=>v.InstanceId==a);var vb=views.Single(v=>v.InstanceId==b);
            Check(va.Data==vb.Data&&a!=b,"same CardData is bound to distinct instance IDs after reversed rebuild");
            Check(va.Cost==0&&va.Playable&&vb.Cost==4&&!vb.Playable,"per-instance costs and availability match Engine preview");
            Check(va.GetComponentsInChildren<Text>().Any(t=>t.text=="费用 0")&&vb.GetComponentsInChildren<Text>().Any(t=>t.text=="费用 4"),"actual cost labels show independent prices");
            yield return Shot("duplicates-1920x1080");
            int energy=engine.Snapshot().Energy,revision=engine.Snapshot().Revision;
            Click(vb);yield return Frames(2);
            Check(engine.Snapshot().Energy==energy&&engine.Snapshot().Revision==revision,"unaffordable real UI click changes nothing");
            var oldCallback=vb.GetComponent<Button>().onClick;
            controller.PresentationBusy=true;Click(va);yield return Frames(2);
            Check(engine.Snapshot().Energy==energy&&engine.Snapshot().Revision==revision,"presentation busy rejects input without advancing rules");
            controller.PresentationBusy=false;
            var point=RectTransformUtility.WorldToScreenPoint(null,va.RectTransform.position);
            var pointer=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Any(h=>h.gameObject==va.gameObject||h.gameObject.transform.IsChildOf(va.transform)),"supplied artwork leaves card raycast accessible");
            Click(va);yield return Frames(3);
            Check(engine.Snapshot().Energy==energy&&engine.Snapshot().InPile(BattlePile.Discard).Single().InstanceId==a,"real click plays only free instance A");
            revision=engine.Snapshot().Revision;oldCallback.Invoke();yield return Frames(2);
            Check(engine.Snapshot().Revision==revision&&engine.Snapshot().InPile(BattlePile.Hand).Single().InstanceId==b,"stale pre-rebuild callback cannot play B");
            controller.SetEngineTurnCost(Request(engine.Snapshot(),b),1);ui.RefreshEngineSandbox();yield return Frames(3);
            vb=root.GetComponentsInChildren<BattleCardView>().Single(v=>v.InstanceId==b);Check(vb.Cost==2&&vb.Playable,"rebuilt B shows current cost 2");
            pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,vb.RectTransform.position),button=PointerEventData.InputButton.Left};
            ExecuteEvents.Execute(vb.gameObject,pointer,ExecuteEvents.beginDragHandler);pointer.position=new Vector2(-50,-50);
            ExecuteEvents.Execute(vb.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(vb.gameObject,pointer,ExecuteEvents.endDragHandler);yield return Frames(3);
            Check(engine.Snapshot().Energy==energy,"cancelled real drag does not spend energy");
            var target=root.GetComponentsInChildren<RectTransform>().Single(t=>t.name=="EngineTarget");
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,vb.RectTransform.position);ExecuteEvents.Execute(vb.gameObject,pointer,ExecuteEvents.beginDragHandler);
            pointer.position=RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center));
            ExecuteEvents.Execute(vb.gameObject,pointer,ExecuteEvents.dragHandler);ExecuteEvents.Execute(vb.gameObject,pointer,ExecuteEvents.endDragHandler);yield return Frames(3);
            Check(engine.Snapshot().Energy==energy-2&&engine.Snapshot().InPile(BattlePile.Discard).Any(c=>c.InstanceId==b),"targeted real drag spends B's cost and moves B");
            var end=root.GetComponentsInChildren<Button>().Single(x=>x.name=="EngineEndTurn");end.onClick.Invoke();yield return Frames(3);
            Check(engine.Snapshot().Turn==2&&engine.Snapshot().InPile(BattlePile.Hand).All(c=>c.Cost==1),"real end-turn control resets both temporary costs");
            ui.RefreshEngineSandbox("重排后仍按实例绑定。",true);Resolution(1366,768,false);yield return Shot("rebound-1366x768");
            Check(JsonUtility.ToJson(math)==source&&JsonUtility.ToJson(controller.sessionState.playerState)==baseline
                &&permanent.SequenceEqual(controller.sessionState.deckState.masterDeck),"source asset, permanent deck and real long-term state unchanged");
            bool saveRejected=false;try{new UnityGameSaveSource(null).Capture();}catch(InvalidOperationException ex){saveRejected=ex.Message=="SAVE_BATTLE_SANDBOX_UNCOMMITTED";}
            Check(saveRejected,"uncommitted P2 sandbox cannot be saved");
            var stale=root.GetComponentsInChildren<BattleCardView>().First().GetComponent<Button>().onClick;revision=engine.Snapshot().Revision;
            app.BindSession(new GameSessionState());stale.Invoke();yield return Frames(2);
            Check(engine.Snapshot().Revision==revision,"existing GameSessionLifetime rejects callback after session replacement");
            controller.DisposeEngineSandbox();Check(controller.SandboxEngine==null,"disposal releases only this sandbox");
        }
        public static void Resolution(int w,int h,bool remember)
        {
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
            var selected=type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
            if(remember)SessionState.SetInt("BattleP2.PreviousResolution",(int)selected.GetValue(view));
            var sizesType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSizes");
            var sizes=typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
            var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new object[]{0});var gt=group.GetType();
            int count=(int)gt.GetMethod("GetBuiltinCount").Invoke(group,null)+(int)gt.GetMethod("GetCustomCount").Invoke(group,null),index=-1;
            for(int i=0;i<count;i++){var size=gt.GetMethod("GetGameViewSize").Invoke(group,new object[]{i});if((int)size.GetType().GetProperty("width").GetValue(size)==w&&(int)size.GetType().GetProperty("height").GetValue(size)==h){index=i;break;}}
            if(index<0){var st=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");var ctor=st.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)[0];gt.GetMethod("AddCustomSize").Invoke(group,new[]{ctor.Invoke(new object[]{Enum.ToObject(ctor.GetParameters()[0].ParameterType,1),w,h,"P2 "+w+"x"+h})});index=count;}
            selected.SetValue(view,index);view.Show();view.Focus();
        }
        public static void RestoreResolution()
        {
            var type=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");var view=EditorWindow.GetWindow(type);
            type.GetProperty("selectedSizeIndex",BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).SetValue(view,SessionState.GetInt("BattleP2.PreviousResolution",0));
        }
    }
}
