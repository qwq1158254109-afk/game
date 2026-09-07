using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace StudentGrowth.EditorTools
{
    public static class BattleP1Verification
    {
        public const string Output = "Docs/Battle/P0-P1-v03";

        public static void RunTests(string label, string outputDirectory = null, string[] additionalTests = null)
        {
            string output = outputDirectory ?? Output;
            Directory.CreateDirectory(output);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            var callback = new Results(label, output);
            api.RegisterCallbacks(callback);
            try
            {
                api.Execute(new ExecutionSettings(new Filter
                {
                    testMode = TestMode.EditMode,
                    testNames = new[] { "StudentGrowth.Tests.ChallengeControllerTests", "StudentGrowth.Tests.CardEffectResolverTests",
                        "StudentGrowth.Tests.DeckControllerTests", "StudentGrowth.Tests.StageControllerTests",
                        "StudentGrowth.Tests.RewardResolverTests", "StudentGrowth.Tests.LocationActionExecutorTests",
                        "StudentGrowth.Tests.BattleP1Tests" }.Concat(additionalTests ?? new string[0]).ToArray()
                }) { runSynchronously = true });
            }
            finally { api.UnregisterCallbacks(callback); UnityEngine.Object.DestroyImmediate(api); }
        }

        private sealed class Results : ICallbacks
        {
            private readonly string label;
            private readonly string output;
            public Results(string label, string output) { this.label = label; this.output = output; }
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor test) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath(output + "/" + label + ".xml"));
                File.WriteAllText(output + "/" + label + ".txt", "Passed=" + result.PassCount + " Failed=" + result.FailCount
                    + " Skipped=" + result.SkipCount + "\n" + result.ResultState + "\n" + result.Message);
                Debug.Log("Battle " + label + ": " + result.PassCount + " passed / " + result.FailCount + " failed");
            }
        }
    }
}
