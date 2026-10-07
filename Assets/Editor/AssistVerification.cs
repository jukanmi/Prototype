using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Prototype.Editor
{
    /// <summary>Run focused regression tests in the already-open editor, without reopening a scene.</summary>
    public static class AssistVerification
    {
        private static TestRunnerApi api;
        private const string RequestFile = "Logs/assist-tests.request";

        [InitializeOnLoadMethod]
        private static void AfterReload() => EditorApplication.delayCall += CheckRequest;

        private static void CheckRequest()
        {
            if (!File.Exists(RequestFile) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            File.Delete(RequestFile);
            Run();
        }

        [MenuItem("Tools/Assist/Run regression tests")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            Directory.CreateDirectory("Logs");
            api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Results());
            api.Execute(new ExecutionSettings(new Filter
            {
                testMode = TestMode.EditMode,
                groupNames = new[]
                {
                    "^Prototype.Tests.AssistSystemTests", "^Prototype.Tests.PlayerPilotIntentTests",
                    "^Prototype.Tests.InputActionAssetTests", "^Prototype.Tests.InputRebindRulesTests",
                    "^Prototype.Tests.InputMapSwitcherTests", "^Prototype.Tests.DeckRulesTests",
                    "^Prototype.Tests.ComboExecutor", "^Prototype.Tests.SkillStepTests",
                    "^Prototype.Tests.SkillHitboxPrefabTests", "^Prototype.Tests.SkillClipOverrideTests",
                },
            }));
        }

        private class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                TestRunnerApi.SaveResultToFile(result, Path.GetFullPath("Logs/assist-editmode-results.xml"));
                File.WriteAllText("Logs/assist-tests-summary.txt", $"Passed: {result.PassCount}\nFailed: {result.FailCount}\nState: {result.ResultState}\n");
                Debug.Log($"Assist regression: {result.PassCount} passed, {result.FailCount} failed.");
            }
        }
    }
}
