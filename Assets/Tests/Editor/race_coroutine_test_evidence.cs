using System;
using System.Linq;
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
    internal const string StartupDeckCaseResultKey = "Foodula1.Tests.GameLoop.StartupDeckCaseResult";
    internal const string CustomRosterDeckCaseResultKey = "Foodula1.Tests.GameLoop.CustomRosterDeckCaseResult";
    internal const string ThunderstormDeckCaseResultKey = "Foodula1.Tests.GameLoop.ThunderstormDeckCaseResult";
    internal const string WeatherDeckCaseResultKey = "Foodula1.Tests.GameLoop.WeatherDeckCaseResult";
    internal const string PitCaseResultKey = "Foodula1.Tests.GameLoop.PitCaseResult";
    internal const string StartupPitCaseResultKey = "Foodula1.Tests.GameLoop.StartupPitCaseResult";
    internal const string WeatherPitCaseResultKey = "Foodula1.Tests.GameLoop.WeatherPitCaseResult";
    internal const string CrossingCaseResultKey = "Foodula1.Tests.GameLoop.CrossingCaseResult";
    internal const string CareerCaseResultKey = "Foodula1.Tests.GameLoop.CareerCaseResult";
    internal const string CareerRecoveryCaseResultKey = "Foodula1.Tests.GameLoop.CareerRecoveryCaseResult";
    internal const string CareerStartupGateCaseResultKey = "Foodula1.Tests.GameLoop.CareerStartupGateCaseResult";
    internal const string CareerSummerCaseResultKey = "Foodula1.Tests.GameLoop.CareerSummerCaseResult";
    internal const string CareerFifthCaseResultKey = "Foodula1.Tests.GameLoop.CareerFifthCaseResult";
    internal const string CareerFinaleCaseResultKey = "Foodula1.Tests.GameLoop.CareerFinaleCaseResult";
    internal const string LiveTelemetryCaseResultKey = "Foodula1.Tests.GameLoop.LiveTelemetryCaseResult";
    private static TestRunnerApi api;

    // One registry owns both exact-name capture and stale-result cleanup.
    private static readonly System.Collections.Generic.Dictionary<string, string> caseKeys =
        new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "LiveTurnTelemetryRuntimeTests.AuthoredHudRefreshesBeforeConfirmAndRestoresRealHeatAfterInputCloses", LiveTelemetryCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealGameLoopSchedulesTwoTurnsAcrossTeamsAndFinalSprintRecoveryOrRetirement", CaseResultKey },
            { "RaceGameLoopCoroutineTests.RealStartupRunsAuthoredTrackToFinishAndIsolatedSettlement", CompletionCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealStartupDeckCompletesAcrossTeamsWithoutReplacingCards", StartupDeckCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealStartupDeckCompletesCustomRostersAndPreservesParticipantOwnership", CustomRosterDeckCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealStartupDeckCompletesThunderstormWithIndependentTeammates", ThunderstormDeckCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealStartupDeckCompletesWithAuthoredWeatherAndSharedLapGate", WeatherDeckCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealAuthoredPitChoiceSuspendsResumesAndCompletesShanghaiRace", PitCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealStartupDeckCompletesShanghaiPitWithoutReplacingCards", StartupPitCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealStartupDeckCompletesShanghaiWithWeatherAndPit", WeatherPitCaseResultKey },
            { "RaceGameLoopCoroutineTests.MissingCarsPreserveVisualTraversalCrossingOrder", CrossingCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealCareerStartupRunsAuthoredTrackToFinishAndIsolatedSettlement", CareerCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealCareerCompletionRejectsFailedOrStaleSavesAndRetriesWithoutNormalRewards", CareerRecoveryCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealCareerStartRejectsSummerBreakAndCompletedAuthorityBeforeAssembly", CareerStartupGateCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealCareerFourthRaceCompletesSummerDraftAndUnlocksFifthLaunchOnce", CareerSummerCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealCareerFifthRaceConsumesSummerTechnologyAndCompletesOnce", CareerFifthCaseResultKey },
            { "RaceGameLoopCoroutineTests.RealCareerFinaleCompletesStandingsAndRejectsNinthRace", CareerFinaleCaseResultKey },
        };

    internal static System.Collections.Generic.IEnumerable<string> CaseResultKeys => caseKeys.Values;

    internal static string ResolveCaseKey(string fullName) =>
        fullName != null && caseKeys.TryGetValue(fullName, out string key) ? key : null;

    // Lazily serialize recognized cases only. Preserve failures as failures;
    // this boundary neither interprets nor invents a result payload.
    internal static void CaptureCaseResult(string fullName, Func<string> serialize, Action<string, string> write)
    {
        string key = ResolveCaseKey(fullName);
        if (key != null)
            write(key, serialize());
    }


    static RaceCoroutineTestEvidence()
    {
        if (SessionState.GetBool(ArmedKey, false)) Register();
    }

    internal static void Arm()
    {
        ClearStaleResults(SessionState.GetBool(ArmedKey, false), SessionState.EraseString);
        SessionState.SetBool(ArmedKey, true);
        Register();
    }

    // Re-arming after a domain reload must retain evidence from this run.
    // Inject only the erase operation so guards never touch live SessionState.
    internal static void ClearStaleResults(bool alreadyArmed, Action<string> erase)
    {
        if (alreadyArmed) return;
        erase(RunResultKey);
        foreach (string key in CaseResultKeys) erase(key);
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
        public string stackTrace;

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
            stackTrace = includeOutput ? result.StackTrace : string.Empty;
        }
    }

    internal static string SerializeResult(ITestResultAdaptor result, bool includeOutput) =>
        JsonUtility.ToJson(new Summary(result, includeOutput));

    private sealed class Capture : ICallbacks
    {
        public void RunStarted(ITestAdaptor testsToRun) { }
        public void TestStarted(ITestAdaptor test) { }

        public void TestFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            CaptureCaseResult(result.FullName,
                () => SerializeResult(result, true), SessionState.SetString);
        }

        public void RunFinished(ITestResultAdaptor result)
        {
            if (!SessionState.GetBool(ArmedKey, false)) return;
            try
            {
                SessionState.SetString(RunResultKey, SerializeResult(result, false));
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

/// <summary>Pure registry guards; never arm callbacks or mutate stored evidence.</summary>
public class RaceCoroutineEvidenceRegistryTests
{
    [Serializable]
    private sealed class SummarySnapshot
    {
        public string fullName, resultState, endedUtc, message, output;
        public int total, passed, failed, skipped, inconclusive;
    }

    // Implement the installed Unity Test Framework result interface, without
    // arming callbacks or creating/writing a real runner result.
    private sealed class ResultFixture : ITestResultAdaptor
    {
        public string ResultState { get; set; }
        public string FullName => "RaceGameLoopCoroutineTests.RealCareerFinaleCompletesStandingsAndRejectsNinthRace";
        public string Name => "fixture";
        public DateTime EndTime => new DateTime(2026, 10, 12, 0, 35, 0, DateTimeKind.Utc);
        public DateTime StartTime => EndTime.AddSeconds(-1);
        public int PassCount => 1;
        public int FailCount => 2;
        public int SkipCount => 3;
        public int InconclusiveCount => 4;
        public string Message => "message \"quoted\"\n第二行";
        public string Output => "raw output\nnot a success verdict";
        public ITestAdaptor Test => throw new NotSupportedException();
        public UnityEditor.TestTools.TestRunner.Api.TestStatus TestStatus => throw new NotSupportedException();
        public double Duration => 1;
        public string StackTrace => "fixture stack";
        public int AssertCount => 0;
        public bool HasChildren => false;
        public System.Collections.Generic.IEnumerable<ITestResultAdaptor> Children => Array.Empty<ITestResultAdaptor>();
        public NUnit.Framework.Interfaces.TNode ToXml() => throw new NotSupportedException();
    }

    [NUnit.Framework.TestCase("Passed", true)]
    [NUnit.Framework.TestCase("Failed:Error", true)]
    [NUnit.Framework.TestCase("Skipped:Ignored", true)]
    [NUnit.Framework.TestCase("Inconclusive", true)]
    [NUnit.Framework.TestCase("Passed", false)]
    [NUnit.Framework.TestCase("Failed:Cancelled", false)]
    public void SummarySerializationPreservesNativeFactsAndIncludesOutputOnlyForCases(string state, bool includeOutput)
    {
        var result = new ResultFixture { ResultState = state };
        string json = RaceCoroutineTestEvidence.SerializeResult(result, includeOutput);
        SummarySnapshot summary = JsonUtility.FromJson<SummarySnapshot>(json);
        NUnit.Framework.Assert.That(summary.fullName, NUnit.Framework.Is.EqualTo(result.FullName));
        NUnit.Framework.Assert.That(summary.resultState, NUnit.Framework.Is.EqualTo(state));
        NUnit.Framework.Assert.That(summary.endedUtc, NUnit.Framework.Is.EqualTo("2026-10-12T00:35:00.0000000Z"));
        NUnit.Framework.Assert.That(summary.total, NUnit.Framework.Is.EqualTo(10));
        NUnit.Framework.Assert.That(new[] { summary.passed, summary.failed, summary.skipped, summary.inconclusive },
            NUnit.Framework.Is.EqualTo(new[] { 1, 2, 3, 4 }));
        NUnit.Framework.Assert.That(summary.message, NUnit.Framework.Is.EqualTo(result.Message));
        NUnit.Framework.Assert.That(summary.output, NUnit.Framework.Is.EqualTo(includeOutput ? result.Output : string.Empty));
    }

    [NUnit.Framework.TestCase("Passed")]
    [NUnit.Framework.TestCase("Failed")]
    [NUnit.Framework.TestCase("Skipped")]
    public void CapturePreservesPayloadAndSerializesOnlyBeforeOneWrite(string state)
    {
        const string name = "RaceGameLoopCoroutineTests.RealStartupRunsAuthoredTrackToFinishAndIsolatedSettlement";
        string payload = "{\"resultState\":\"" + state + "\",\"output\":\"raw evidence\"}";
        var trace = new System.Collections.Generic.List<string>();
        RaceCoroutineTestEvidence.CaptureCaseResult(name, () =>
        {
            trace.Add("serialize");
            return payload;
        }, (key, value) =>
        {
            trace.Add("write");
            NUnit.Framework.Assert.That(key, NUnit.Framework.Is.EqualTo(RaceCoroutineTestEvidence.CompletionCaseResultKey));
            NUnit.Framework.Assert.That(value, NUnit.Framework.Is.EqualTo(payload));
        });
        NUnit.Framework.CollectionAssert.AreEqual(new[] { "serialize", "write" }, trace);
    }

    [NUnit.Framework.TestCase(null)]
    [NUnit.Framework.TestCase("")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupRunsAuthoredTrackToFinishAndIsolatedSettlement ")]
    public void UnknownCaptureNeverSerializesOrWrites(string name)
    {
        RaceCoroutineTestEvidence.CaptureCaseResult(name,
            () => throw new InvalidOperationException("Unknown test was serialized."),
            (key, value) => NUnit.Framework.Assert.Fail("Unknown test wrote " + key));
    }

    [NUnit.Framework.Test]
    public void SerializationFailureDoesNotReplaceExistingEvidence()
    {
        const string name = "RaceGameLoopCoroutineTests.RealCareerFinaleCompletesStandingsAndRejectsNinthRace";
        string previous = "existing evidence";
        var failure = new InvalidOperationException("serialization failed");
        var actual = NUnit.Framework.Assert.Throws<InvalidOperationException>(() =>
            RaceCoroutineTestEvidence.CaptureCaseResult(name, () => throw failure, (key, value) => previous = value));
        NUnit.Framework.Assert.That(actual, NUnit.Framework.Is.SameAs(failure));
        NUnit.Framework.Assert.That(previous, NUnit.Framework.Is.EqualTo("existing evidence"));
    }

    [NUnit.Framework.Test]
    public void StorageFailurePropagatesOnceWithoutWritingAFallbackSuccess()
    {
        const string name = "RaceGameLoopCoroutineTests.RealCareerFinaleCompletesStandingsAndRejectsNinthRace";
        var failure = new InvalidOperationException("storage failed");
        int writes = 0;
        var actual = NUnit.Framework.Assert.Throws<InvalidOperationException>(() =>
            RaceCoroutineTestEvidence.CaptureCaseResult(name, () => "failed payload", (key, value) =>
            { writes++; throw failure; }));
        NUnit.Framework.Assert.That(actual, NUnit.Framework.Is.SameAs(failure));
        NUnit.Framework.Assert.That(writes, NUnit.Framework.Is.EqualTo(1));
    }

    [NUnit.Framework.Test]
    public void RepeatedCaptureReplacesOnlyTheExactCaseAndNeverTheRunSummary()
    {
        const string name = "RaceGameLoopCoroutineTests.RealCareerFinaleCompletesStandingsAndRejectsNinthRace";
        var storage = new System.Collections.Generic.Dictionary<string, string>
        { { RaceCoroutineTestEvidence.RunResultKey, "run" }, { "unrelated", "keep" } };
        foreach (string payload in new[] { "failed", "passed" })
            RaceCoroutineTestEvidence.CaptureCaseResult(name, () => payload, (key, value) => storage[key] = value);
        NUnit.Framework.Assert.That(storage.Count, NUnit.Framework.Is.EqualTo(3));
        NUnit.Framework.Assert.That(storage[RaceCoroutineTestEvidence.CareerFinaleCaseResultKey], NUnit.Framework.Is.EqualTo("passed"));
        NUnit.Framework.Assert.That(storage[RaceCoroutineTestEvidence.RunResultKey], NUnit.Framework.Is.EqualTo("run"));
        NUnit.Framework.Assert.That(storage["unrelated"], NUnit.Framework.Is.EqualTo("keep"));
    }

    [NUnit.Framework.Test]
    public void FirstArmClearsEveryStaleResultOnceButPreservesOtherStorage()
    {
        var storage = RaceCoroutineTestEvidence.CaseResultKeys.ToDictionary(key => key, key => "stale");
        storage.Add(RaceCoroutineTestEvidence.RunResultKey, "stale run");
        storage.Add("unrelated", "keep");
        var erased = new System.Collections.Generic.List<string>();

        RaceCoroutineTestEvidence.ClearStaleResults(false, key => { erased.Add(key); storage.Remove(key); });

        int expectedCount = RaceCoroutineTestEvidence.CaseResultKeys.Count() + 1;
        NUnit.Framework.Assert.That(erased.Count, NUnit.Framework.Is.EqualTo(expectedCount));
        NUnit.Framework.Assert.That(erased.Distinct().Count(), NUnit.Framework.Is.EqualTo(expectedCount));
        NUnit.Framework.Assert.That(erased[0], NUnit.Framework.Is.EqualTo(RaceCoroutineTestEvidence.RunResultKey));
        NUnit.Framework.CollectionAssert.AreEquivalent(RaceCoroutineTestEvidence.CaseResultKeys, erased.Skip(1));
        NUnit.Framework.Assert.That(storage.Count, NUnit.Framework.Is.EqualTo(1));
        NUnit.Framework.Assert.That(storage["unrelated"], NUnit.Framework.Is.EqualTo("keep"));
    }

    [NUnit.Framework.Test]
    public void RepeatedArmNeverInvokesErase()
    {
        RaceCoroutineTestEvidence.ClearStaleResults(true, key => NUnit.Framework.Assert.Fail("Erased live result: " + key));
    }

    [NUnit.Framework.Test]
    public void RepeatedArmRetainsFreshResultsUntilTheNextRun()
    {
        var storage = new System.Collections.Generic.Dictionary<string, string>();
        Action<string> erase = key => storage.Remove(key);
        RaceCoroutineTestEvidence.ClearStaleResults(false, erase);
        storage.Add(RaceCoroutineTestEvidence.RunResultKey, "fresh run");
        foreach (string key in RaceCoroutineTestEvidence.CaseResultKeys) storage.Add(key, "fresh case");
        var fresh = new System.Collections.Generic.Dictionary<string, string>(storage);

        RaceCoroutineTestEvidence.ClearStaleResults(true, erase);
        NUnit.Framework.CollectionAssert.AreEquivalent(fresh, storage);
        RaceCoroutineTestEvidence.ClearStaleResults(false, erase);
        NUnit.Framework.Assert.That(storage, NUnit.Framework.Is.Empty);
    }

    [NUnit.Framework.Test]
    public void EraseFailurePropagatesBeforeAnyCaseIsCleared()
    {
        var failure = new InvalidOperationException("erase failed");
        var erased = new System.Collections.Generic.List<string>();
        var actual = NUnit.Framework.Assert.Throws<InvalidOperationException>(() =>
            RaceCoroutineTestEvidence.ClearStaleResults(false, key => { erased.Add(key); throw failure; }));

        NUnit.Framework.Assert.That(actual, NUnit.Framework.Is.SameAs(failure));
        NUnit.Framework.CollectionAssert.AreEqual(new[] { RaceCoroutineTestEvidence.RunResultKey }, erased);
    }

    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealGameLoopSchedulesTwoTurnsAcrossTeamsAndFinalSprintRecoveryOrRetirement", "Foodula1.Tests.GameLoop.CaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupRunsAuthoredTrackToFinishAndIsolatedSettlement", "Foodula1.Tests.GameLoop.CompletionCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupDeckCompletesAcrossTeamsWithoutReplacingCards", "Foodula1.Tests.GameLoop.StartupDeckCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupDeckCompletesCustomRostersAndPreservesParticipantOwnership", "Foodula1.Tests.GameLoop.CustomRosterDeckCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupDeckCompletesThunderstormWithIndependentTeammates", "Foodula1.Tests.GameLoop.ThunderstormDeckCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupDeckCompletesWithAuthoredWeatherAndSharedLapGate", "Foodula1.Tests.GameLoop.WeatherDeckCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealAuthoredPitChoiceSuspendsResumesAndCompletesShanghaiRace", "Foodula1.Tests.GameLoop.PitCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupDeckCompletesShanghaiPitWithoutReplacingCards", "Foodula1.Tests.GameLoop.StartupPitCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupDeckCompletesShanghaiWithWeatherAndPit", "Foodula1.Tests.GameLoop.WeatherPitCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.MissingCarsPreserveVisualTraversalCrossingOrder", "Foodula1.Tests.GameLoop.CrossingCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealCareerStartupRunsAuthoredTrackToFinishAndIsolatedSettlement", "Foodula1.Tests.GameLoop.CareerCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealCareerCompletionRejectsFailedOrStaleSavesAndRetriesWithoutNormalRewards", "Foodula1.Tests.GameLoop.CareerRecoveryCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealCareerStartRejectsSummerBreakAndCompletedAuthorityBeforeAssembly", "Foodula1.Tests.GameLoop.CareerStartupGateCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealCareerFourthRaceCompletesSummerDraftAndUnlocksFifthLaunchOnce", "Foodula1.Tests.GameLoop.CareerSummerCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealCareerFifthRaceConsumesSummerTechnologyAndCompletesOnce", "Foodula1.Tests.GameLoop.CareerFifthCaseResult")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealCareerFinaleCompletesStandingsAndRejectsNinthRace", "Foodula1.Tests.GameLoop.CareerFinaleCaseResult")]
    public void ExactNamesRetainTheirOriginalStorageKey(string fullName, string expectedKey)
    {
        NUnit.Framework.Assert.That(RaceCoroutineTestEvidence.ResolveCaseKey(fullName), NUnit.Framework.Is.EqualTo(expectedKey));
    }

    [NUnit.Framework.TestCase(null)]
    [NUnit.Framework.TestCase("")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests")]
    [NUnit.Framework.TestCase("racegameloopcoroutinetests.RealStartupDeckCompletesThunderstormWithIndependentTeammates")]
    [NUnit.Framework.TestCase("RaceGameLoopCoroutineTests.RealStartupDeckCompletesThunderstormWithIndependentTeammates ")]
    public void UnknownOrInexactNamesCannotCapture(string fullName)
    {
        NUnit.Framework.Assert.That(RaceCoroutineTestEvidence.ResolveCaseKey(fullName), NUnit.Framework.Is.Null);
    }

    [NUnit.Framework.Test]
    public void CleanupIncludesEveryCaseConstantOnceAndExcludesRunSummary()
    {
        var expected = System.Linq.Enumerable.Where(typeof(RaceCoroutineTestEvidence).GetFields(
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic),
            field => field.IsLiteral && field.Name.EndsWith("CaseResultKey", StringComparison.Ordinal)
                || field.IsLiteral && field.Name == "CaseResultKey");
        var keys = new System.Collections.Generic.List<string>(RaceCoroutineTestEvidence.CaseResultKeys);
        NUnit.Framework.CollectionAssert.AreEquivalent(System.Linq.Enumerable.Select(expected,
            field => (string)field.GetRawConstantValue()), keys);
        NUnit.Framework.Assert.That(System.Linq.Enumerable.Distinct(keys).Count(), NUnit.Framework.Is.EqualTo(keys.Count));
        NUnit.Framework.Assert.That(keys, NUnit.Framework.Does.Not.Contain(RaceCoroutineTestEvidence.RunResultKey));
    }

    [NUnit.Framework.Test]
    public void EveryMappedNameStillNamesOneRealUnityTest()
    {
        var names = new System.Collections.Generic.HashSet<string>();
        foreach (var method in typeof(RaceGameLoopCoroutineTests).GetMethods())
            if (Attribute.IsDefined(method, typeof(UnityEngine.TestTools.UnityTestAttribute)))
                names.Add(typeof(RaceGameLoopCoroutineTests).FullName + "." + method.Name);
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealGameLoopSchedulesTwoTurnsAcrossTeamsAndFinalSprintRecoveryOrRetirement"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealStartupRunsAuthoredTrackToFinishAndIsolatedSettlement"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealStartupDeckCompletesAcrossTeamsWithoutReplacingCards"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealStartupDeckCompletesCustomRostersAndPreservesParticipantOwnership"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealStartupDeckCompletesThunderstormWithIndependentTeammates"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealStartupDeckCompletesWithAuthoredWeatherAndSharedLapGate"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.MissingCarsPreserveVisualTraversalCrossingOrder"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealCareerStartupRunsAuthoredTrackToFinishAndIsolatedSettlement"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealCareerCompletionRejectsFailedOrStaleSavesAndRetriesWithoutNormalRewards"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealCareerStartRejectsSummerBreakAndCompletedAuthorityBeforeAssembly"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealCareerFourthRaceCompletesSummerDraftAndUnlocksFifthLaunchOnce"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealCareerFifthRaceConsumesSummerTechnologyAndCompletesOnce"));
        NUnit.Framework.Assert.That(names, NUnit.Framework.Does.Contain("RaceGameLoopCoroutineTests.RealCareerFinaleCompletesStandingsAndRejectsNinthRace"));
    }
}
