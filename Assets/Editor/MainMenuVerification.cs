using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using StudentGrowth;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class MainMenuVerification
{
    private const string Marker="Temp/MainMenuVerification.active";
    private const string Output="Docs/SaveLoad/MainMenu-v1.1";
    private static readonly StringBuilder log=new StringBuilder();
    static MainMenuVerification() { EditorApplication.playModeStateChanged+=Changed; }
    public static void RunLogic()
    {
        Directory.CreateDirectory(Output); var report=new StringBuilder();
        foreach(string typeName in new[]{"StudentGrowth.Tests.MainMenuContractTests","StudentGrowth.Tests.PauseSaveTests"}) {
            var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType(typeName)).FirstOrDefault(t=>t!=null);
            if(type==null) { report.AppendLine("MISSING "+typeName); continue; }
            foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttributes(false).Any(a=>a.GetType().Name=="TestAttribute"))) {
                var test=Activator.CreateInstance(type);
                try { type.GetMethod("Setup").Invoke(test,null); method.Invoke(test,null); report.AppendLine("PASS "+method.Name); }
                catch(Exception ex) { report.AppendLine("FAIL "+method.Name+" "+(ex.InnerException ?? ex)); }
                finally { type.GetMethod("Cleanup").Invoke(test,null); }
            }
        }
        File.WriteAllText(Output+"/logic.txt",report.ToString());
    }
    public static void RunPlay()
    {
        Directory.CreateDirectory("Temp"); string id=Guid.NewGuid().ToString("N"); File.WriteAllText(Marker,id); File.WriteAllText("Temp/SettingsPlayVerification.active",id);
        EditorApplication.delayCall+=()=> { Application.runInBackground=true; EditorApplication.isPlaying=true; };
    }
    private static void Changed(PlayModeStateChange change)
    {
        if(!File.Exists(Marker)) return;
        if(change==PlayModeStateChange.EnteredPlayMode) { EditorApplication.delayCall+=StartAfterTransition; }
        if(change==PlayModeStateChange.EnteredEditMode) { File.Delete(Marker); File.Delete("Temp/SettingsPlayVerification.active"); }
    }
    private static void StartAfterTransition()
    { if(EditorApplication.isPlaying && File.Exists(Marker)) { log.Clear(); Directory.CreateDirectory(Output); File.WriteAllText(Output+"/play-progress.txt","Started "+DateTime.UtcNow.ToString("O")+"\n"); GameApplicationRuntime.Instance.StartCoroutine(Run()); } }
    private static IEnumerator Run()
    {
        Application.runInBackground=true; Exception failure=null;
        yield return SaveCoroutineGuard.Run(Verify(),e=>failure=e);
        Directory.CreateDirectory(Output); File.WriteAllText(Output+"/play.txt",log+"\n"+(failure==null ? "PASS ALL" : "FAIL "+failure));
        EditorApplication.isPlaying=false;
    }
    private static void Check(bool value,string message) { string line=(value ? "PASS " : "FAIL ")+message; log.AppendLine(line); File.AppendAllText(Output+"/play-progress.txt",line+"\n"); if(!value) throw new Exception(message); }
    private static IEnumerator Verify()
    {
        yield return null; yield return null;
        var app=GameApplicationRuntime.Instance;
        Check(app.SaveDirectory.Contains("PausePlayTests"),"isolated save directory");
        yield return app.Backend.ReturnToMenuAsync(r=>Check(r.Success,"initial menu: "+r.ErrorCode)); app.Coordinator.EnterTitle();
        yield return null;
        Check(GameBootstrapper.SharedSession==null && PlayerManager.Instance.CurrentPlayer==null,"menu has no session/player");
        app.Coordinator.OpenLoad(); Check(app.Coordinator.Slots.Count==10,"ten slots without session"); app.Coordinator.Back();
        app.Coordinator.OpenSettings(); Check(SettingsMenuController.Instance.IsOpen,"settings without session"); SettingsMenuController.Instance.Close(); yield return null;
        Check(app.Coordinator.State==PauseMenuState.Title,"settings returns to menu");
        ScreenCapture.CaptureScreenshot(Output+"/main-menu.png"); yield return null;
        for(int i=0;i<5;i++) {
            app.Coordinator.BeginNewGame(); yield return app.Coordinator.ConfirmAsync();
            Check(SceneManager.GetActiveScene().path==GameApplicationRuntime.HomePath && app.Coordinator.State==PauseMenuState.Gameplay,"new game home "+i+" notice="+app.Coordinator.Notice);
            yield return null;
            Check(UnityEngine.Object.FindObjectsByType<RpgPlayerController>(FindObjectsInactive.Exclude).Length==1,"one player "+i);
            yield return null; yield return null;
            var blockers=(System.Collections.IEnumerable)typeof(GameplayInputGate).GetField("Blockers",BindingFlags.Static|BindingFlags.NonPublic).GetValue(null);
            foreach(var blocker in blockers) log.AppendLine("BLOCKER "+blocker);
            Check(!GameplayInputGate.IsBlocked && Time.timeScale==1,"input/time resumed "+i+" scale="+Time.timeScale);
            var session=UnityGameSaveSource.CurrentSession(); session.playerState.money=432+i;
            app.Coordinator.OpenPause(); app.Coordinator.OpenSave(); app.Coordinator.SelectSlot(new SaveSlotId(1));
            Check(app.Coordinator.Confirmation==PauseConfirmation.Save,"empty/occupied save confirmation"); yield return app.Coordinator.ConfirmAsync();
            Check(app.SaveService.GetSlotSummaries()[1].IsValid && !app.SaveService.GetSlotSummaries()[1].IsEmpty,"save committed "+app.Coordinator.Notice);
            Check(!app.Lifetime.IsDirty(app.Source.Capture()),"saved snapshot baseline");
            app.Coordinator.Back();
            session.playerState.money++;
            app.Coordinator.RequestConfirmation(PauseConfirmation.ReturnToTitle); yield return app.Coordinator.ConfirmAsync(); Check(app.Coordinator.HasUnsavedChoice,"direct mutation detected"); app.Coordinator.Back();
            app.Coordinator.OpenLoad(); app.Coordinator.SelectSlot(new SaveSlotId(1)); yield return app.Coordinator.ConfirmAsync();
            if(app.Coordinator.HasUnsavedChoice) yield return app.Coordinator.DiscardAndLeaveAsync();
            Check(app.Coordinator.State==PauseMenuState.Gameplay,"restore playable "+app.Coordinator.Notice);
            Check(UnityGameSaveSource.CurrentSession().playerState.money==432+i,"restored money");
            yield return null;
            Check(!app.Lifetime.IsDirty(app.Source.Capture()),"load baseline after ready");
            app.Coordinator.OpenPause(); app.Coordinator.RequestConfirmation(PauseConfirmation.ReturnToTitle); yield return app.Coordinator.ConfirmAsync();
            yield return null;
            Check(SceneManager.GetActiveScene().path==GameApplicationRuntime.MainMenuPath && !app.Lifetime.IsActive,"menu unloaded game "+i);
            Check(UnityEngine.Object.FindObjectsByType<RpgPlayerController>(FindObjectsInactive.Include).Length==0,"no stale players "+i);
            Check(UnityEngine.Object.FindObjectsByType<GameApplicationRuntime>(FindObjectsInactive.Include).Length==1,"unique application host "+i);
        }
    }
}
