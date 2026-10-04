using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/// <summary>
/// Scoped native Test Runner evidence. MCP callbacks can be lost across the
/// Enter/ExitPlayMode domain reload; re-register while this fixture is armed.
/// Results stay in Editor SessionState, never player preferences or project settings.
/// </summary>
[InitializeOnLoad]
internal static class RaceCoroutineTestEvidence
{
    private const string ArmedKey = "Foodula1.Tests.GameLoop.EvidenceArmed";
    internal const string RunResultKey = "Foodula1.Tests.GameLoop.RunResult";
    internal const string CaseResultKey = "Foodula1.Tests.GameLoop.CaseResult";
    internal const string CompletionCaseResultKey = "Foodula1.Tests.GameLoop.CompletionCaseResult";
    internal const string CrossingCaseResultKey = "Foodula1.Tests.GameLoop.CrossingCaseResult";
    private static TestRunnerApi api;

    static RaceCoroutineTestEvidence()
    {
        if (SessionState.GetBool(ArmedKey, false)) Register();
    }

    internal static void Arm()
    {
        if (!SessionState.GetBool(ArmedKey, false))
        {
            SessionState.EraseString(RunResultKey);
            SessionState.EraseString(CaseResultKey);
            SessionState.EraseString(CompletionCaseResultKey);
            SessionState.EraseString(CrossingCaseResultKey);
        }
        SessionState.SetBool(ArmedKey, true);
        Register();
    }

    private static void Register()
    {
        if (api != null) return;
        api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.hideFlags = HideFlags.HideAndDontSave;
        api.RegisterCallbacks(new Capture());
    }

    [Serializable]
    private sealed class Summary
    {
        public string fullName;
        public string resultState;
        public string endedUtc;
        public int total;
        public int passed;
        public int failed;
        public int skipped;
        public int inconclusive;
        public string message;
        public string output;

        internal Summary(ITestResultAdaptor result, bool includeOutput)
        {
            fullName = result.FullName;
            resultState = result.ResultState;
            endedUtc = result.EndTime.ToUniversalTime().ToString("O");
            passed = result.PassCount;
            failed = result.FailCount;
            skipped = result.SkipCount;
            inconclusive = result.InconclusiveCount;
            total = passed + failed + skipped + inconclusive;
            message = result.Message;
            output = includeOutput ? result.Output : string.Empty;
        }
    }

    private sealed class Capture : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (SessionState.GetBool(ArmedKey, false) &&
                result.FullName == "RaceGameLoopCoroutineTests.RealGameLoopSchedulesTwoTurnsAcrossTeamsAndFinalSprintRecoveryOrRetirement")
                SessionState.SetString(CaseResultKey, JsonUtility.ToJson(new Summary(result, true)));
            if (SessionState.GetBool(ArmedKey, false) &&
                result.FullName == "RaceGameLoopCoroutineTests.RealStartupRunsAuthoredTrackToFinishAndIsolatedSettlement")
                SessionState.SetString(CompletionCaseResultKey, JsonUtility.ToJson(new Summary(result, true)));
            if (SessionState.GetBool(ArmedKey, false) &&
                result.FullName == "RaceGameLoopCoroutineTests.MissingCarsPreserveVisualTraversalCrossingOrder")
                SessionState.SetString(CrossingCaseResultKey, JsonUtility.ToJson(new Summary(result, true)));
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            try
            {
                SessionState.SetString(RunResultKey, JsonUtility.ToJson(new Summary(result, false)));
            }
            finally
            {
                SessionState.EraseBool(ArmedKey);
                api.UnregisterCallbacks(this);
                UnityEngine.Object.DestroyImmediate(api);
                api = null;
            }
        }
    }
}
