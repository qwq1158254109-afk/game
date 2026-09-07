using System;
using System.IO;
using UnityEngine;
namespace StudentGrowth
{
    public static class GameFlowDiagnostics
    {
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        public static void Trace(string step)
        {
            try { File.AppendAllText(Path.Combine(Application.dataPath,"../Temp/MainMenuFlow.log"),DateTime.UtcNow.ToString("O")+" frame="+Time.frameCount+" "+step+"\n"); }
            catch(IOException) { }
        }
    }
}
