using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth.EditorTools
{
    [InitializeOnLoad]
    public static class BattleP3Verification
    {
        public const string Output="Docs/Battle/P3-v03";
        private const string Marker="Temp/BattleP3Play.active",Isolation="Temp/SettingsPlayVerification.active";
        static BattleP3Verification(){EditorApplication.playModeStateChanged+=Changed;}
        [MenuItem("Tools/Student Growth/Battle/P3/Run transaction tests")]
        public static void Tests()=>BattleP1Verification.RunTests("rules",Output,new[]{"StudentGrowth.Tests.BattleEngineTests","StudentGrowth.Tests.ItemEffectResolverTests","StudentGrowth.Tests.BattleP3Tests"});
        [MenuItem("Tools/Student Growth/Battle/P3/Verify main menu and map loop")]
        public static void Play()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty||File.Exists(Isolation))throw new InvalidOperationException("Finish existing play tests and save scene edits first.");
            Directory.CreateDirectory(Output);File.WriteAllText(Output+"/play.txt","RUNNING");
            string id=Guid.NewGuid().ToString("N");File.WriteAllText(Marker,id);File.WriteAllText(Isolation,id);
            SessionState.SetString("P3.StartScene",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(GameApplicationRuntime.MainMenuPath);
            BattleP2PlayVerification.Resolution(1920,1080,true);
            EditorApplication.isPlaying=true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if(!File.Exists(Marker))return;
            if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>GameApplicationRuntime.Instance.StartCoroutine(BattleP3Smoke.Run(Output,ok=>EditorApplication.isPlaying=false));
            if(state==PlayModeStateChange.EnteredEditMode)
            {
                if(File.Exists(Isolation)&&File.ReadAllText(Isolation)==File.ReadAllText(Marker))File.Delete(Isolation);
                File.Delete(Marker);EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("P3.StartScene",""));
                BattleP2PlayVerification.RestoreResolution();
                if(File.ReadAllText(Output+"/play.txt")=="RUNNING")File.WriteAllText(Output+"/play.txt","ABORTED");
            }
        }
        [MenuItem("Tools/Student Growth/Battle/P3/Build normal Windows player")]
        public static void Build()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode||SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Finish play mode and save scene changes first.");
            Directory.CreateDirectory("Builds/P3");
            var report=BuildPipeline.BuildPlayer(EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(),
                "Builds/P3/StudentGrowth.exe",BuildTarget.StandaloneWindows64,BuildOptions.None);
            File.WriteAllText(Output+"/build.txt",report.summary.result+"\nErrors="+report.summary.totalErrors+" Warnings="+report.summary.totalWarnings+"\n"+report.summary.outputPath);
        }
    }
}
