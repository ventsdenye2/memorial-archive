using System;
using System.IO;
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
            var action = File.ReadAllText(RequestPath).Trim();
            if (action == "test" || action == "feedback-test")
            {
                // Do not run cached assemblies after an external source edit.
                var runtimeAssembly = "Library/ScriptAssemblies/Assembly-CSharp.dll";
                var testAssembly = "Library/ScriptAssemblies/Assembly-CSharp-Editor.dll";
                var compiledAt = File.Exists(runtimeAssembly) && File.Exists(testAssembly)
                    ? new DateTime(Math.Min(File.GetLastWriteTimeUtc(runtimeAssembly).Ticks, File.GetLastWriteTimeUtc(testAssembly).Ticks), DateTimeKind.Utc)
                    : DateTime.MinValue;
                if (Array.Exists(Directory.GetFiles("Assets/Scripts", "*.cs", SearchOption.AllDirectories), path => File.GetLastWriteTimeUtc(path) > compiledAt) ||
                    Array.Exists(Directory.GetFiles("Assets/Tests", "*.cs", SearchOption.AllDirectories), path => File.GetLastWriteTimeUtc(path) > File.GetLastWriteTimeUtc(testAssembly)))
                {
                    AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                    return;
                }
            }
            File.Delete(RequestPath);
            try
            {
                if (action == "apply") { NarrativeContentBuilder.Apply(); File.WriteAllText("Logs/narrative-authoring.txt", "PASS"); }
                else if (action == "test" || action == "feedback-test")
                {
                    SessionState.SetBool("NarrativeTestsRunning", true); RegisterResults();
                    var groups = action == "feedback-test"
                        ? new[] { "Feedback0913Tests", "Feedback0913RuntimeTests", "OpeningDialogueRegressionTests", "DialogueSystemTests", "NarrativeTests", "NarrativeRuntimeTests", "GuideProgressTests", "LightingSystemTests", "LightRenderSelectionTests", "InventorySystemTests", "CharacterStateMachineTests", "SavePersistenceTests" }
                        : new[] { "NarrativeTests", "NarrativeRuntimeTests" };
                    api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, groupNames = groups }));
                }
                else if (action == "build") { RegressionPlayerBuild.Build(); File.WriteAllText("Logs/narrative-build.txt", "PASS"); }
            }
            catch (Exception e) { File.WriteAllText("Logs/narrative-authoring.txt", e.ToString()); Debug.LogException(e); }
        }
        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor test) => File.WriteAllText("Logs/narrative-tests.txt", "RUNNING\n");
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) => File.AppendAllText(
                "Logs/narrative-tests.txt", result.FullName + ": " + result.ResultState + " " + result.Message +
                (string.IsNullOrEmpty(result.StackTrace) ? string.Empty : Environment.NewLine + result.StackTrace) + Environment.NewLine);
            public void RunFinished(ITestResultAdaptor result)
            {
                SessionState.SetBool("NarrativeTestsRunning", false);
                var path = "Logs/narrative-tests.txt";
                var details = File.Exists(path) ? File.ReadAllText(path) : string.Empty;
                File.WriteAllText(path, $"{result.ResultState}: passed={result.PassCount}, failed={result.FailCount}\n" + details);
            }
        }
    }
}
