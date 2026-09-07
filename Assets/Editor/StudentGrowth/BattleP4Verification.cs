using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace StudentGrowth.EditorTools
{
    [InitializeOnLoad]
    public static class BattleP4Verification
    {
        private const string Output=BattleP4ContentBuilder.Output;
        private const string Marker="Temp/BattleP4Play.active",Isolation="Temp/SettingsPlayVerification.active";
        static BattleP4Verification(){EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("Tools/Student Growth/Battle/P4/Verify strategy map loop")]
        public static void Play()=>Begin(false);
        [MenuItem("Tools/Student Growth/Battle/P4/Verify previous map and boss loop")]
        public static void Previous()=>Begin(true);
        private static string PlayOutput=>SessionState.GetBool("P4.Previous",false)?Output+"/p3-regression":Output;
        private static void Begin(bool previous)
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty||File.Exists(Isolation))throw new InvalidOperationException("Finish play tests and save scene changes first.");
            SessionState.SetBool("P4.Previous",previous);
            Directory.CreateDirectory(PlayOutput);File.WriteAllText(PlayOutput+"/play.txt","RUNNING");
            string id=Guid.NewGuid().ToString("N");File.WriteAllText(Marker,id);File.WriteAllText(Isolation,id);
            SessionState.SetString("P4.StartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(GameApplicationRuntime.MainMenuPath);
            BattleP2PlayVerification.Resolution(1920,1080,true);EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!File.Exists(Marker))return;
            if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>GameApplicationRuntime.Instance.StartCoroutine(
                SessionState.GetBool("P4.Previous",false)?BattleP3Smoke.Run(PlayOutput,ok=>EditorApplication.isPlaying=false):BattleP4Smoke.Run(PlayOutput,ok=>EditorApplication.isPlaying=false));
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(File.Exists(Isolation)&&File.ReadAllText(Isolation)==File.ReadAllText(Marker))File.Delete(Isolation);
                File.Delete(Marker);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("P4.StartScene",""));
                BattleP2PlayVerification.RestoreResolution();
                if(File.ReadAllText(PlayOutput+"/play.txt")=="RUNNING")File.WriteAllText(PlayOutput+"/play.txt","ABORTED");
            }
        }
        [MenuItem("Tools/Student Growth/Battle/P4/Build normal Windows player")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Finish play mode and save changes first.");
            Directory.CreateDirectory("Builds/P4");Directory.CreateDirectory(Output);
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),"Builds/P4/StudentGrowth.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
            File.WriteAllText(Output+"/build.txt",report.summary.result+"\nErrors="+report.summary.totalErrors+" Warnings="+report.summary.totalWarnings+"\n"+report.summary.outputPath);
        }
    }
}
