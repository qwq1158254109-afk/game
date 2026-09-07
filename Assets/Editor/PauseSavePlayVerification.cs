using System;
using System.Collections;
using System.IO;
using StudentGrowth;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Opt-in integration test; Play mode restores the user's unsaved Editor scene on exit.
[InitializeOnLoad]
public static class PauseSavePlayVerification
{
    private const string Active = "Temp/PauseSaveVerification.active";
    private const string Isolation = "Temp/SettingsPlayVerification.active";
    private const string Report = "Docs/SaveLoad/2026-09-05/Unity-Play.txt";
    private static double started;
    static PauseSavePlayVerification()
    { EditorApplication.update += Tick; EditorApplication.playModeStateChanged += Changed; }
    private static void Tick()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (!EditorApplication.isPlayingOrWillChangePlaymode && File.Exists("Temp/PauseSaveVerification.request"))
        {
            File.Delete("Temp/PauseSaveVerification.request");
            string id = Guid.NewGuid().ToString("N"); File.WriteAllText(Active,id); File.WriteAllText(Isolation,id);
            EditorApplication.isPlaying = true;
        }
        if (EditorApplication.isPlaying && File.Exists(Active) && started > 0 && EditorApplication.timeSinceStartup-started > 90)
            Finish(new TimeoutException("Save/load play verification timed out"));
    }
    private static void Changed(PlayModeStateChange state)
    {
        if (!File.Exists(Active)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            started = EditorApplication.timeSinceStartup;
            Application.runInBackground=true;
            PauseMenuRuntime.Instance.StartCoroutine(Guard());
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        { File.Delete(Active); if (File.Exists(Isolation)) File.Delete(Isolation); started=0; }
    }
    private static IEnumerator Guard()
    {
        Exception error=null;
        yield return SaveCoroutineGuard.Run(Verify(),ex=>error=ex);
        Finish(error);
    }
    private static IEnumerator Verify()
    {
        yield return null; yield return null;
        var runtime=PauseMenuRuntime.Instance;
        Check(runtime.SaveDirectory.Contains("PausePlayTests"),"Save isolation");
        var session=UnityGameSaveSource.CurrentSession(); Check(session!=null,"Session initialized");
        var player=UnityEngine.Object.FindAnyObjectByType<RpgPlayerController>(); Check(player!=null,"Player initialized");
        session.playerState.money=731; session.timeActionState.currentDay=7; session.timeActionState.currentActionPoints=2;
        session.locationState.locationFlags["save_play_probe"]=true;
        var position=(Vector2)player.transform.position; string scene=SceneManager.GetActiveScene().path;
        int cards=session.deckState.masterDeck.Count; float scale=Time.timeScale;
        runtime.Coordinator.OpenPause(); Check(Time.timeScale==0 && GameplayInputGate.IsBlocked,"Pause locks gameplay");
        runtime.Coordinator.OpenSave(); runtime.Coordinator.SelectSlot(new SaveSlotId(1));
        Check(runtime.SaveService.GetSlotSummaries()[1].IsEmpty,"Save requires confirmation");
        yield return runtime.Coordinator.ConfirmAsync();
        Check(runtime.SaveService.GetSlotSummaries()[1].IsValid,"Save committed: "+runtime.Coordinator.Notice);
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        yield return null;
        ScreenCapture.CaptureScreenshot(Path.GetDirectoryName(Report)+"/save-menu.png");
        yield return null; yield return null;
        session.playerState.money=3; session.timeActionState.currentActionPoints=0;
        runtime.Coordinator.Back(); runtime.Coordinator.OpenLoad(); runtime.Coordinator.SelectSlot(new SaveSlotId(1));
        yield return runtime.Coordinator.ConfirmAsync();
        Check(runtime.Coordinator.State==PauseMenuState.Gameplay,"Load succeeded: "+runtime.Coordinator.Notice);
        var restored=UnityGameSaveSource.CurrentSession();
        Check(restored.playerState.money==731 && restored.timeActionState.currentDay==7 && restored.timeActionState.currentActionPoints==2,"Stats/time restored without rewards or costs");
        Check(restored.locationState.locationFlags["save_play_probe"] && restored.deckState.masterDeck.Count==cards,"Flags/deck restored");
        player=UnityEngine.Object.FindAnyObjectByType<RpgPlayerController>();
        Check(SceneManager.GetActiveScene().path==scene && Vector2.Distance(player.transform.position,position)<0.05f,"Scene and player restored");
        yield return null; yield return null;
        Check(Time.timeScale==scale && !GameplayInputGate.IsBlocked,"Input/time released, scale="+Time.timeScale+" blocked="+GameplayInputGate.IsBlocked);
        runtime.Coordinator.OpenPause(); runtime.Coordinator.OpenSettings(); SettingsMenuController.Instance.Close();
        Check(runtime.Coordinator.State==PauseMenuState.Paused && Time.timeScale==0,"Settings close returns to paused menu");
        runtime.Coordinator.ContinueGame(); Check(Time.timeScale==scale,"Continue resumes");
        runtime.Coordinator.OpenPause(); yield return runtime.Coordinator.ReturnToTitleAsync(null);
        runtime.Coordinator.RequestConfirmation(PauseConfirmation.NewGame); yield return runtime.Coordinator.ConfirmAsync();
        Check(runtime.Coordinator.State==PauseMenuState.Gameplay && SceneManager.GetActiveScene().name=="StageHubMapScene","Title starts new game");
        Check(UnityGameSaveSource.CurrentSession().timeActionState.currentDay==1,"New game resets session");
        yield return null; yield return null;
        var clock=UnityEngine.Object.FindAnyObjectByType<TimeActionController>();
        clock.State.currentPeriod=TimePeriod.Night; clock.Sleep(default(SleepLocation));
        yield return null; yield return null;
        Check(runtime.SaveService.GetSlotSummaries()[0].Day==2,"New day triggers automatic slot");
        runtime.Coordinator.OpenPause(); runtime.Coordinator.OpenLoad();
        UnityEngine.Object.Destroy(runtime.SaveView.gameObject); yield return null;
        runtime.TextService.SetLanguage("en"); runtime.Coordinator.Back(); runtime.Coordinator.ContinueGame();
    }
    private static void Check(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
    private static void Finish(Exception error)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Report));
        File.WriteAllText(Report,(error==null ? "PASS: real scene save/load; confirmation; stats/day/action points/flags/deck/player restoration; pause/settings/continue; title/new game; new-day automatic save; destroyed view language event.\n" : "FAIL: "+error+"\n")+"UTC="+DateTime.UtcNow.ToString("O"));
        started=0; EditorApplication.isPlaying=false;
    }
}
