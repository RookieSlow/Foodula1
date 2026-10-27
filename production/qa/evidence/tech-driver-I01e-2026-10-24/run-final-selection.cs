if (ModularRegressionRunner.IsBusy || ModularRegressionRunner.NativeTestsRunning() || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode || UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating) throw new System.InvalidOperationException("Editor or runner busy");
ModularRegressionRunner.Discover(names => {
    var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
    typeof(ModularRegressionRunner).GetMethod("EnsureSafe",flags).Invoke(null,null);
    var plan = ModularRegressionCatalog.Select(ModularRegressionCatalog.Load(),names,new[]{"tech","drivers","cards-heat","core","tracks-weather-pit"});
    var exact = new[]{
        "RaceGameLoopCoroutineTests.HartActiveNewApexSettlesBeforeFinishOrPitReservation",
        "RaceGameLoopCoroutineTests.HartNativeTwoLegsThenOneCellPaysOnlyTheNewApexAndKeepsPartialPaymentSpin",
        "RaceGameLoopCoroutineTests.InterruptedHartFinalCrossingDoesNotCommitOrLeakIntoNextRace",
        "RaceGameLoopCoroutineTests.UkBreakfastThenPreparationUsesOldHeatAtRealA2A4AndClosesOnFormalCard",
        "RaceGameLoopCoroutineTests.RealGameLoopSchedulesTwoTurnsAcrossTeamsAndFinalSprintRecoveryOrRetirement"
    };
    if(exact.Any(n=>!names.Contains(n))) throw new System.InvalidOperationException("A selected native case is not loaded");
    plan.testNames=plan.testNames.Concat(exact).Distinct().OrderBy(n=>n,System.StringComparer.Ordinal).ToArray();
    plan.modules=plan.modules.Concat(new[]{"long-races"}).ToArray();
    plan.selected=plan.testNames.Length; plan.longSelected=exact.Length; plan.excluded=plan.discovered-plan.selected;
    string dir=System.IO.Path.GetFullPath("Library/Foodula1/RegressionRuns/"+System.DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ")+"-I01e-exact");
    System.IO.Directory.CreateDirectory(dir);
    string prefix="Foodula1.ModularRegression.";
    string json=UnityEngine.JsonUtility.ToJson(plan,true);
    System.IO.File.WriteAllText(System.IO.Path.Combine(dir,"plan.json"),json);
    UnityEditor.SessionState.SetString(prefix+"plan",json);
    UnityEditor.SessionState.SetString(prefix+"directory",dir);
    UnityEditor.SessionState.SetString(prefix+"started",System.DateTime.UtcNow.ToString("o"));
    UnityEditor.SessionState.SetString(prefix+"lastResult",""); UnityEditor.SessionState.SetString(prefix+"error","");
    UnityEditor.SessionState.SetInt(prefix+"completed",0); UnityEditor.SessionState.SetInt(prefix+"selected",plan.selected);
    UnityEditor.SessionState.SetBool(prefix+"armed",true);
    typeof(ModularRegressionRunner).GetMethod("Register",flags).Invoke(null,null);
    var api=(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi)typeof(ModularRegressionRunner).GetField("api",flags).GetValue(null);
    api.Execute(new UnityEditor.TestTools.TestRunner.Api.ExecutionSettings(new UnityEditor.TestTools.TestRunner.Api.Filter{
        testMode=UnityEditor.TestTools.TestRunner.Api.TestMode.EditMode,testNames=plan.testNames}));
});
return "Five module fixtures plus five exact native cases; native RunFinished remains authoritative.";
