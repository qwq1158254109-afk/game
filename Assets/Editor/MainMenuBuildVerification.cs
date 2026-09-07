using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using StudentGrowth;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
public static class MainMenuBuildVerification
{
    public static void Build()
    {
        EditorApplication.delayCall+=()=> {
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes=EditorBuildSettings.scenes.Where(s=>s.enabled).Select(s=>s.path).ToArray(), locationPathName="Builds/MainMenu-v1.1/StudentGrowth.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development });
            Directory.CreateDirectory("Docs/SaveLoad/MainMenu-v1.1");
            File.WriteAllText("Docs/SaveLoad/MainMenu-v1.1/build.txt",report.summary.result+"\nErrors="+report.summary.totalErrors+"\nWarnings="+report.summary.totalWarnings+"\nDuration="+report.summary.totalTime);
        };
    }
    public static IEnumerator CaptureResolutions()
    {
        var tool=typeof(StudentGrowth.Editor.StageHubGameViewCaptureTool);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;
        tool.GetMethod("PrepareGameView",flags).Invoke(null,null);
        int old=(int)tool.GetMethod("GetSelectedSizeIndex",flags).Invoke(null,null);
        var group=tool.GetMethod("GetStandaloneSizeGroup",flags).Invoke(null,null);
        var sizeType=typeof(EditorWindow).Assembly.GetType("UnityEditor.GameViewSize");
        var constructor=sizeType.GetConstructors(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)[0];
        try {
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(1600,1200)}) {
                int index=(int)tool.GetMethod("FindFixedResolutionIndex",flags).Invoke(null,new object[]{size.x,size.y});
                if(index<0) { var preset=constructor.Invoke(new object[]{Enum.ToObject(constructor.GetParameters()[0].ParameterType,1),size.x,size.y,"MainMenu "+size.x+"x"+size.y}); group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{preset}); index=(int)tool.GetMethod("FindFixedResolutionIndex",flags).Invoke(null,new object[]{size.x,size.y}); }
                tool.GetMethod("SetSelectedSizeIndex",flags).Invoke(null,new object[]{index});
                for(int i=0;i<12;i++) yield return null;
                if(Screen.width!=size.x || Screen.height!=size.y) throw new Exception("Resolution mismatch: "+Screen.width+"x"+Screen.height);
                ScreenCapture.CaptureScreenshot("Docs/SaveLoad/MainMenu-v1.1/menu-"+size.x+"x"+size.y+".png"); yield return null; yield return null;
            }
        } finally { tool.GetMethod("SetSelectedSizeIndex",flags).Invoke(null,new object[]{old}); }
    }
}
