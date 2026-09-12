using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace MemorialArchive.Editor
{
    [InitializeOnLoad]
    public static class NarrativeAuthoringQueue
    {
        private const string RequestPath = "Logs/narrative.request";
        private static TestRunnerApi api;
        static NarrativeAuthoringQueue()
        {
            EditorApplication.update += Poll;
            if (SessionState.GetBool("NarrativeTestsRunning", false)) RegisterResults();
        }
        private static void RegisterResults()
        {
            api = ScriptableObject.CreateInstance<TestRunnerApi>(); api.RegisterCallbacks(new Results());
        }
        private static void Poll()
        {
            if (!File.Exists(RequestPath) || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                DateTime.UtcNow - File.GetLastWriteTimeUtc(RequestPath) < TimeSpan.FromSeconds(10)) return;
            var action = File.ReadAllText(RequestPath).Trim(); File.Delete(RequestPath);
            try
            {
                if (action == "apply") { NarrativeContentBuilder.Apply(); File.WriteAllText("Logs/narrative-authoring.txt", "PASS"); }
                else if (action == "test")
                {
                    SessionState.SetBool("NarrativeTestsRunning", true); RegisterResults();
                    api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = new[] { "NarrativeTests", "NarrativeRuntimeTests" } }));
                }
                else if (action == "build") { RegressionPlayerBuild.Build(); File.WriteAllText("Logs/narrative-build.txt", "PASS"); }
            }
            catch (Exception e) { File.WriteAllText("Logs/narrative-authoring.txt", e.ToString()); Debug.LogException(e); }
        }
        private sealed class Results : ICallbacks
        {
            private readonly StringBuilder output = new StringBuilder();
            public void RunStarted(ITestAdaptor test) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) => output.AppendLine(result.FullName + ": " + result.ResultState + " " + result.Message);
            public void RunFinished(ITestResultAdaptor result)
            {
                SessionState.SetBool("NarrativeTestsRunning", false);
                output.Insert(0, $"{result.ResultState}: passed={result.PassCount}, failed={result.FailCount}\n");
                File.WriteAllText("Logs/narrative-tests.txt", output.ToString());
            }
        }
    }
}
