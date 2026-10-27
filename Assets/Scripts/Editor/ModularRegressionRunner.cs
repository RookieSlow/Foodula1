using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Native TestRunner wrapper, with exact test selection and reload-safe RunFinished evidence.</summary>
[InitializeOnLoad]
public static class ModularRegressionRunner
{
    private const string Prefix = "Foodula1.ModularRegression.";
    private static TestRunnerApi api;
    private static Capture capture;
    private static readonly MethodInfo IsRunActive = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.NonPublic);
    public static bool IsRunning => SessionState.GetBool(Prefix + "armed", false);
    public static bool IsPreparing => SessionState.GetBool(Prefix + "preparing", false);
    public static bool IsBusy => IsRunning || IsPreparing;
    public static string LastResultPath => SessionState.GetString(Prefix + "lastResult", "");
    public static string CurrentTest => SessionState.GetString(Prefix + "current", "");
    public static int Completed => SessionState.GetInt(Prefix + "completed", 0);
    public static int SelectedCount => SessionState.GetInt(Prefix + "selected", 0);
    public static string Error => SessionState.GetString(Prefix + "error", "");
    public static ModularRegressionCatalog.Plan CurrentPlan => JsonUtility.FromJson<ModularRegressionCatalog.Plan>(SessionState.GetString(Prefix + "plan", "{}"));

    [Serializable] public sealed class CaseResult
    {
        public string name, state, message, stackTrace;
        public double seconds;
    }
    [Serializable] public sealed class Result
    {
        public string startedUtc, endedUtc, state, nativeState;
        public double seconds;
        public int total, passed, failed, skipped, inconclusive;
        public bool selectionMatches;
        public ModularRegressionCatalog.Plan plan;
        public List<CaseResult> cases = new List<CaseResult>();
        public string[] unexpected, missing;
    }

    static ModularRegressionRunner()
    {
        if (IsRunning) Register();
        else if (SessionState.GetBool(Prefix + "preparing", false))
        {
            SessionState.SetBool(Prefix + "preparing", false);
            SessionState.SetString(Prefix + "error", "Discovery interrupted by reload; retry after the editor is idle.");
        }
    }

    public static bool NativeTestsRunning()
    {
        // Pinned Test Framework 1.1.33 exposes this only internally. Fail closed if that API changes.
        if (IsRunActive == null) throw new NotSupportedException("Cannot verify native runner idle state; use the native Test Runner.");
        return (bool)IsRunActive.Invoke(null, null);
    }

    private static void EnsureSafe()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || NativeTestsRunning())
            throw new InvalidOperationException("Requires idle EditMode, with no native test already running.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("A loaded scene is dirty; save it yourself before running tests.");
    }

    public static void Discover(Action<string[]> completed)
    {
        EnsureApi();
        api.RetrieveTestList(TestMode.EditMode, tree =>
        {
            var names = new List<string>();
            Visit(tree, names);
            completed(names.OrderBy(n => n, StringComparer.Ordinal).ToArray());
        });
    }
    private static void Visit(ITestAdaptor test, List<string> names)
    {
        if (!test.IsSuite) names.Add(test.FullName);
        foreach (var child in test.Children) Visit(child, names);
    }

    public static void Run(string moduleCsv, bool includeCore = false)
    {
        if (IsBusy) throw new InvalidOperationException("A module request is already active.");
        EnsureSafe();
        var requested = moduleCsv.Split(',').Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        if (includeCore && !requested.Contains("full") && !requested.Contains("fast-all")) requested.Add("core");
        SessionState.SetString(Prefix + "error", "");
        // Invalidate the old run immediately, before asynchronous discovery. A pending/failed
        // request must never expose an earlier Passed result as its own completion.
        SessionState.SetString(Prefix + "lastResult", "");
        SessionState.SetString(Prefix + "plan", "{}");
        SessionState.SetInt(Prefix + "completed", 0);
        SessionState.SetInt(Prefix + "selected", 0);
        SessionState.SetString(Prefix + "current", "");
        SessionState.SetBool(Prefix + "preparing", true);
        try
        {
            Discover(names =>
            {
                try
                {
                    EnsureSafe();
                    var plan = ModularRegressionCatalog.Select(ModularRegressionCatalog.Load(), names, requested);
                    string directory = Path.GetFullPath(Path.Combine("Library/Foodula1/RegressionRuns", DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8)));
                    Directory.CreateDirectory(directory);
                    string planJson = JsonUtility.ToJson(plan, true);
                    File.WriteAllText(Path.Combine(directory, "plan.json"), planJson);
                    SessionState.SetString(Prefix + "plan", planJson);
                    SessionState.SetString(Prefix + "directory", directory);
                    SessionState.SetString(Prefix + "started", DateTime.UtcNow.ToString("o"));
                    SessionState.SetString(Prefix + "lastResult", "");
                    SessionState.SetInt(Prefix + "completed", 0);
                    SessionState.SetInt(Prefix + "selected", plan.selected);
                    SessionState.SetString(Prefix + "current", "");
                    SessionState.SetBool(Prefix + "armed", true);
                    Register();
                    var filter = new Filter { testMode = TestMode.EditMode };
                    if (!plan.full) filter.testNames = plan.testNames;
                    // Never set runSynchronously: it silently excludes coroutine/native UI tests.
                    api.Execute(new ExecutionSettings(filter));
                }
                catch (Exception exception) { FailRequest(exception); }
                finally { SessionState.SetBool(Prefix + "preparing", false); }
            });
        }
        catch (Exception exception) { SessionState.SetBool(Prefix + "preparing", false); FailRequest(exception); throw; }
    }

    private static void Register()
    {
        EnsureApi();
        if (capture != null) return;
        capture = new Capture();
        api.RegisterCallbacks(capture);
    }
    private static void EnsureApi()
    {
        if (api != null) return;
        // Recover only this tool's API after a domain reload instead of leaking a hidden object each time.
        api = Resources.FindObjectsOfTypeAll<TestRunnerApi>().FirstOrDefault(x => x.name == "Foodula1 Modular Regression API");
        if (api == null) api = ScriptableObject.CreateInstance<TestRunnerApi>();
        api.name = "Foodula1 Modular Regression API";
        api.hideFlags = HideFlags.HideAndDontSave;
    }
    private static void Release()
    {
        if (api != null && capture != null) api.UnregisterCallbacks(capture);
        capture = null;
        if (api != null) UnityEngine.Object.DestroyImmediate(api);
        api = null;
    }
    private static void FailRequest(Exception exception)
    {
        SessionState.SetBool(Prefix + "armed", false);
        SessionState.SetString(Prefix + "error", exception.ToString());
        Release();
        Debug.LogError("[ModularRegression] Request failed: " + exception.Message);
    }

    private sealed class Capture : ICallbacks
    {
        public void RunStarted(ITestAdaptor tests) { }
        public void TestStarted(ITestAdaptor test)
        {
            if (IsRunning && !test.IsSuite) SessionState.SetString(Prefix + "current", test.FullName);
        }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (IsRunning && !result.Test.IsSuite) SessionState.SetInt(Prefix + "completed", Completed + 1);
        }
        public void RunFinished(ITestResultAdaptor native)
        {
            if (!IsRunning) return;
            try
            {
                var result = new Result { startedUtc = SessionState.GetString(Prefix + "started", ""),
                    endedUtc = native.EndTime.ToUniversalTime().ToString("o"), state = native.ResultState, nativeState = native.ResultState,
                    seconds = native.Duration, passed = native.PassCount, failed = native.FailCount,
                    skipped = native.SkipCount, inconclusive = native.InconclusiveCount, plan = CurrentPlan };
                result.total = result.passed + result.failed + result.skipped + result.inconclusive;
                Collect(native, result.cases);
                var actual = new HashSet<string>(result.cases.Select(c => c.name), StringComparer.Ordinal);
                var expected = new HashSet<string>(result.plan.testNames, StringComparer.Ordinal);
                result.unexpected = actual.Except(expected).ToArray();
                result.missing = expected.Except(actual).ToArray();
                result.selectionMatches = result.unexpected.Length == 0 && result.missing.Length == 0 && result.total == result.plan.selected;
                if (!result.selectionMatches) result.state = "InvalidSelection";
                string path = Path.Combine(SessionState.GetString(Prefix + "directory", ""), "result.json");
                File.WriteAllText(path, JsonUtility.ToJson(result, true));
                SessionState.SetString(Prefix + "lastResult", path);
                if (!result.selectionMatches) SessionState.SetString(Prefix + "error", "Executed test selection differs from the preview; inspect result.json.");
                Debug.Log("[ModularRegression] " + result.state + " " + result.passed + "/" + result.total + " in " + result.seconds.ToString("F3") + "s; " + path);
            }
            catch (Exception exception) { SessionState.SetString(Prefix + "error", exception.ToString()); Debug.LogException(exception); }
            finally { SessionState.SetBool(Prefix + "armed", false); SessionState.SetString(Prefix + "current", ""); Release(); }
        }
        private static void Collect(ITestResultAdaptor node, List<CaseResult> cases)
        {
            if (!node.Test.IsSuite) cases.Add(new CaseResult { name = node.FullName, state = node.ResultState,
                seconds = node.Duration, message = node.Message, stackTrace = node.StackTrace });
            foreach (var child in node.Children) Collect(child, cases);
        }
    }
}
