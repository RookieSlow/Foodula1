using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RaceTestLogWriterTests
{
    [TestCase(RaceLogTermination.Completed)]
    [TestCase(RaceLogTermination.Restarted)]
    [TestCase(RaceLogTermination.Exited)]
    [TestCase(RaceLogTermination.SceneDestroyed)]
    [TestCase(RaceLogTermination.TutorialIncomplete)]
    [TestCase(RaceLogTermination.Unknown)]
    public void ClosureOutcomeSurvivesRealWriterAndFileAnalyzer(RaceLogTermination cause)
    {
        string directory = Path.Combine(Path.GetTempPath(), "foodula1-race-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var writer = new RaceTestLogWriter(directory))
            {
                writer.BeginRace("silverstone", "Silverstone", "driver", TeamId.US, 1);
                writer.Append("[TURN_START] turn=1\n[CARD_PHASE] end\n[MOVE_PHASE] begin\n[MOVE_PHASE] end");
                string oldPath = writer.FilePath;
                writer.End("多行赛果\n说明文字", cause);
                writer.End("duplicate ignored", RaceLogTermination.Completed);
                var previous = RaceLogFileAnalyzer.AnalyzeFile(oldPath);
                Assert.That(previous.IsValid, Is.True, string.Join("; ", previous.Errors));
                Assert.That(previous.Termination, Is.EqualTo(cause));
                Assert.That(previous.HasCompletedRaceEvidence, Is.EqualTo(cause == RaceLogTermination.Completed));
                Assert.That(File.ReadAllText(oldPath), Does.Not.Contain("duplicate ignored"));

                // Fresh file stays partial and cannot inherit the previous closure.
                writer.BeginRace("suzuka", "Suzuka", "driver", TeamId.JP, 1);
                writer.Append("[TURN_START] turn=1\n[CARD_PHASE] end");
                // Analyze saved evidence, not a file still held by StreamWriter.
                writer.Dispose();
                var restarted = RaceLogFileAnalyzer.AnalyzeFile(writer.FilePath);
                Assert.That(writer.FilePath, Is.Not.EqualTo(oldPath));
                Assert.That(restarted.IsValid, Is.True, string.Join("; ", restarted.Errors));
                Assert.That(restarted.SawRaceEnd, Is.False);
                Assert.That(restarted.HasCompletedRaceEvidence, Is.False);
                Assert.That(restarted.Termination, Is.EqualTo(RaceLogTermination.Unknown));
            }
        }
        finally
        {
            // Only this fixture's unique temporary directory is owned here.
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Test]
    public void WritesMetadataAndStripsHudRichText()
    {
        string directory = Path.Combine(Path.GetTempPath(), "foodula1-race-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new RaceTestLogWriter(directory);
            writer.BeginRace("silverstone", "Silverstone", "driver", TeamId.CN, 1);
            string path = writer.FilePath;
            writer.Append("<color=red>SPINS OUT!</color>");
            Action<string> legacyEnd = writer.End;
            legacyEnd("finished");

            string contents = File.ReadAllText(path);
            Assert.That(contents, Does.Contain("track_id=silverstone"));
            Assert.That(contents, Does.Contain("SPINS OUT!"));
            Assert.That(contents, Does.Not.Contain("<color=red>"));
            Assert.That(contents, Does.Contain("[RACE_END] finished"));
            Assert.That(contents, Does.Contain("[RACE_TERMINATION] outcome=Unknown"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }

    [Test]
    public void RestartingRaceClosesPreviousFileAndUsesFreshPath()
    {
        string directory = Path.Combine(Path.GetTempPath(), "foodula1-race-log-" + Guid.NewGuid().ToString("N"));
        try
        {
            var writer = new RaceTestLogWriter(directory);
            writer.BeginRace("silverstone", "Silverstone", "driver", TeamId.CN, 1);
            string firstPath = writer.FilePath;
            writer.Append("first");

            writer.BeginRace("suzuka", "Suzuka", "driver", TeamId.JP, 1);
            string secondPath = writer.FilePath;
            writer.End("finished");

            Assert.That(firstPath, Is.Not.EqualTo(secondPath));
            Assert.That(File.ReadAllText(firstPath), Does.Contain("first"));
            Assert.That(File.ReadAllText(secondPath), Does.Contain("track_id=suzuka"));
            Assert.That(writer.IsActive, Is.False);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, true);
        }
    }
}

/// <summary>Real coordinator closure paths, with only scene/rebuild I/O recorded.</summary>
public class RaceLogClosureIntegrationTests
{
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;
    private GameObject host;
    private MVPGameManager manager;
    private RaceSession session;
    private RaceTestLogWriter writer;
    private string directory;
    private UnityEngine.Random.State randomState;
    private object telemetryInstance;
    private static FieldInfo TelemetryInstance => typeof(PlaytestTelemetryService)
        .GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);

    [SetUp]
    public void SetUp()
    {
        randomState = UnityEngine.Random.state;
        telemetryInstance = TelemetryInstance.GetValue(null);
        TelemetryInstance.SetValue(null, null); // Never append fixture events to a real playtest session.
        directory = Path.Combine(Path.GetTempPath(), "foodula1-closure-" + Guid.NewGuid().ToString("N"));
        host = new GameObject("Inactive race log closure regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        session = new RaceSession(new SystemRandomSource(31));
        Set("session", session);
        writer = new RaceTestLogWriter(directory);
        Set("raceLogWriter", writer);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (host != null) UnityEngine.Object.DestroyImmediate(host);
            writer?.Dispose();
            // This unique fixture directory is the only filesystem data owned here.
            if (directory != null && Directory.Exists(directory)) Directory.Delete(directory, true);
        }
        finally
        {
            TelemetryInstance.SetValue(null, telemetryInstance);
            UnityEngine.Random.state = randomState;
        }
    }

    [TestCase(TeamId.UK, true)]
    [TestCase(TeamId.DE, true)]
    [TestCase(TeamId.IT, true)]
    [TestCase(TeamId.US, true)]
    [TestCase(TeamId.CN, true)]
    [TestCase(TeamId.JP, true)]
    [TestCase(TeamId.UK, false)]
    [TestCase(TeamId.DE, false)]
    [TestCase(TeamId.IT, false)]
    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.CN, false)]
    [TestCase(TeamId.JP, false)]
    public void GameOverUsesDirectorCompletionNotVehicleFinish(TeamId team, bool completed)
    {
        var player = AddFinisher(team, false);
        TutorialRuntimeDirector director = SetTutorial(team, true);
        if (completed) Assert.That(director.CompletePracticeLap(out _), Is.True);
        BeginClosedTurn(team);
        string path = writer.FilePath;

        Invoke("ShowGameOver");
        AssertClosed(path, completed ? RaceLogTermination.Completed : RaceLogTermination.TutorialIncomplete);
        string resultText = File.ReadAllText(path);
        Assert.That(resultText, Does.Contain(completed
            ? "勒芒教程练习完成！"
            : "本次练习未完成；可以使用教程面板重新开始。"));
        Assert.That(resultText, Does.Contain("教程模式：不发放 RP、车手 XP、解锁或正常赛事进度。"));
        Assert.That(player.techState.rpBalance, Is.EqualTo(77));
        Assert.That(player.driverXp, Is.EqualTo(42));
        // Destruction after a result cannot replace it or append another terminal event.
        DestroyHost();
        AssertClosed(path, completed ? RaceLogTermination.Completed : RaceLogTermination.TutorialIncomplete);
    }

    [TestCase(true, true, true)]
    [TestCase(false, true, true)]
    [TestCase(false, false, true)]
    [TestCase(false, false, false)]
    public void ActualLogAdapterPreservesSetupPrecedenceAndLoadedTrack(bool tutorial, bool career, bool freeRoster)
    {
        PlayerState player = AddFinisher(TeamId.US, false);
        if (tutorial) SetTutorial(TeamId.US, true);
        if (career)
        {
            var season = new CareerSeasonState();
            Assert.That(CareerModeRules.TryStartSeason(season, TeamId.US,
                new[] { TeamId.US, TeamId.UK, TeamId.IT, TeamId.DE }), Is.True);
            Assert.That(CareerRaceLaunchRequest.TryCreate(season, "setup-precedence", out var launch), Is.True);
            Set("careerRaceLaunch", launch);
        }
        if (freeRoster) Set("freeRaceRoster", new[] { new FreeRaceRosterEntry(TeamId.US, player.DriverProfile.Id) });
        TrackManager track = host.AddComponent<TrackManager>();
        typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(track,
            new TrackConfig { trackId = "actual-track", trackName = "实际赛道" });
        manager.trackManager = track;
        Invoke("BeginRaceTestLog", player.DriverProfile, player);
        writer.Dispose();
        string text = File.ReadAllText(writer.FilePath);
        Assert.That(text, Does.Contain("track_id=actual-track"));
        Assert.That(text, Does.Contain("track_name=实际赛道"));
        Assert.That(text.Contains("[TUTORIAL_SETUP]"), Is.EqualTo(tutorial));
        Assert.That(text.Contains("[CAREER_SETUP]"), Is.EqualTo(!tutorial && career));
        Assert.That(text.Contains("[FREE_RACE_SETUP]"), Is.EqualTo(!tutorial && !career && freeRoster));
        if (tutorial)
            Assert.That(text, Does.Contain("phase=Practice"));
        if (!tutorial && career)
            Assert.That(text, Does.Contain("result_id=setup-precedence"));
        if (!tutorial && !career && freeRoster)
            Assert.That(text, Does.Contain("player=US:" + player.DriverProfile.Id));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingOrGuidedDirectorCannotClaimCompletedPractice(bool guidedDirector)
    {
        AddFinisher(TeamId.US, false);
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.US);
        Set("tutorialScenario", scenario);
        if (guidedDirector) Set("tutorialDirector", new TutorialRuntimeDirector(scenario));
        BeginClosedTurn(TeamId.US);
        Invoke("ShowGameOver");
        AssertClosed(writer.FilePath, RaceLogTermination.TutorialIncomplete);
    }

    [Test]
    public void OrdinaryAndAlreadyRecordedCareerResultsKeepDistinctSetupButBothCloseCompleted()
    {
        // AI-only ordinary fixture executes the real result branch without human profile writes.
        AddFinisher(TeamId.DE, true);
        BeginClosedTurn(TeamId.DE);
        string ordinaryPath = writer.FilePath;
        Invoke("ShowGameOver");
        AssertClosed(ordinaryPath, RaceLogTermination.Completed);
        Assert.That(File.ReadAllText(ordinaryPath), Does.Contain("RP 奖励:"));

        var season = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(season, TeamId.DE,
            new[] { TeamId.DE, TeamId.UK, TeamId.IT, TeamId.US }), Is.True);
        Assert.That(CareerRaceLaunchRequest.TryCreate(season, "closure-regression", out var launch), Is.True);
        Set("careerRaceLaunch", launch);
        Set("careerResultRecorded", true); // Never call the real career repository.
        int balance = session.Players[0].techState.rpBalance;
        int xp = session.Players[0].driverXp;
        BeginClosedTurn(TeamId.DE);
        Invoke("ShowGameOver");
        AssertClosed(writer.FilePath, RaceLogTermination.Completed);
        Assert.That(File.ReadAllText(writer.FilePath), Does.Contain("[CAREER_RESULT] status=already_saved"));
        Assert.That(session.Players[0].techState.rpBalance, Is.EqualTo(balance));
        Assert.That(session.Players[0].driverXp, Is.EqualTo(xp));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void BeginRaceLogClosesOldRunAndFreshFileCannotInheritCompletion(bool tutorial)
    {
        PlayerState player = AddFinisher(TeamId.CN, false);
        if (tutorial) SetTutorial(TeamId.CN, false);
        BeginClosedTurn(TeamId.CN);
        string oldPath = writer.FilePath;
        Invoke("BeginRaceTestLog", player.DriverProfile, player);
        AssertClosed(oldPath, RaceLogTermination.Restarted);
        string freshPath = writer.FilePath;
        Assert.That(freshPath, Is.Not.EqualTo(oldPath));
        Assert.That(writer.IsActive, Is.True);
        writer.Dispose();
        RaceLogAnalysisResult fresh = RaceLogFileAnalyzer.AnalyzeFile(freshPath);
        Assert.That(fresh.IsValid, Is.True, string.Join("; ", fresh.Errors));
        Assert.That(fresh.SawRaceEnd, Is.False);
        Assert.That(fresh.Termination, Is.EqualTo(RaceLogTermination.Unknown));
        Assert.That(fresh.HasCompletedRaceEvidence, Is.False);
        if (tutorial) Assert.That(File.ReadAllText(freshPath), Does.Contain("[TUTORIAL_SETUP]"));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TutorialRestartClosesBeforeFlagAndRebuildCallback(bool practice)
    {
        SetTutorial(TeamId.US, false);
        BeginClosedTurn(TeamId.US);
        string path = writer.FilePath;
        int calls = 0;
        Invoke("RestartTutorialRuntime", practice, "closure-regression", (Action)(() =>
        {
            calls++;
            AssertClosed(path, RaceLogTermination.Restarted);
            Assert.That(Get("initializeTutorialInPractice"), Is.EqualTo(practice));
        }));
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(File.ReadAllText(path), Does.Contain("tutorial reset: closure-regression"));
    }

    [Test]
    public void OrdinaryTutorialResetGuardDoesNotCloseOrRebuild()
    {
        BeginClosedTurn(TeamId.UK);
        Invoke("RestartTutorialRuntime", true, "ignored", (Action)(() => Assert.Fail("Not a tutorial.")));
        Assert.That(writer.IsActive, Is.True);
        Assert.That(Get("initializeTutorialInPractice"), Is.False);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void ExitClosesBeforeNavigationEvenWhenNavigationFails(bool failNavigation)
    {
        TutorialRuntimeDirector director = SetTutorial(TeamId.US, true);
        BeginClosedTurn(TeamId.US);
        string path = writer.FilePath;
        int calls = 0;
        var failure = new InvalidOperationException("recorded navigation failure");
        Action navigate = () =>
        {
            calls++;
            Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Exited));
            AssertClosed(path, RaceLogTermination.Exited);
            Assert.That(File.ReadAllText(path), Does.Contain("event=exit_requested"));
            if (failNavigation) throw failure;
        };
        if (failNavigation)
            Assert.That(Assert.Throws<TargetInvocationException>(() => Invoke("ExitTutorialRuntime", navigate))
                .InnerException, Is.SameAs(failure));
        else Invoke("ExitTutorialRuntime", navigate);
        Assert.That(calls, Is.EqualTo(1));
        DestroyHost();
        AssertClosed(path, RaceLogTermination.Exited);
    }

    [Test]
    public void FailedRebuildCannotRelabelAbortedRunAsCompleted()
    {
        SetTutorial(TeamId.CN, false);
        BeginClosedTurn(TeamId.CN);
        var failure = new InvalidOperationException("recorded rebuild failure");
        Assert.That(Assert.Throws<TargetInvocationException>(() => Invoke("RestartTutorialRuntime",
            true, "failed-rebuild", (Action)(() => throw failure))).InnerException, Is.SameAs(failure));
        AssertClosed(writer.FilePath, RaceLogTermination.Restarted);
        Assert.That(Get("initializeTutorialInPractice"), Is.True, "Existing nontransactional order is preserved.");
    }

    [TestCase(false)]
    [TestCase(true)]
    public void DestroyClosesOnlyAnActiveWriter(bool active)
    {
        if (active) BeginClosedTurn(TeamId.UK);
        string path = writer.FilePath;
        Invoke("OnDestroy"); // Exercise the actual callback even though the host never activates.
        Invoke("OnDestroy");
        Assert.That(writer.IsActive, Is.False);
        if (active) AssertClosed(path, RaceLogTermination.SceneDestroyed);
        else Assert.That(writer.FilePath, Is.Null);
    }

    private TutorialRuntimeDirector SetTutorial(TeamId team, bool practice)
    {
        var scenario = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        var director = new TutorialRuntimeDirector(scenario, practice);
        Set("tutorialScenario", scenario);
        Set("tutorialDirector", director);
        return director;
    }

    private PlayerState AddFinisher(TeamId team, bool ai)
    {
        var player = new PlayerState("closure player", ai, 0, 1)
        {
            teamId = team, hasFinished = true, finishOrder = 1,
            techState = new TechTreeState(team, 77), driverXp = 42
        };
        session.Players.Add(player);
        return player;
    }

    private void BeginClosedTurn(TeamId team)
    {
        writer.BeginRace("fixture_track", "Fixture Track", "fixture driver", team, 0);
        Assert.That(writer.IsActive, Is.True);
        writer.Append("[TURN_START] turn=1\n[CARD_PHASE] end\n[MOVE_PHASE] begin\n[MOVE_PHASE] end");
    }

    private void AssertClosed(string path, RaceLogTermination outcome)
    {
        RaceLogAnalysisResult result = RaceLogFileAnalyzer.AnalyzeFile(path);
        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.Termination, Is.EqualTo(outcome));
        Assert.That(result.HasCompletedRaceEvidence, Is.EqualTo(outcome == RaceLogTermination.Completed));
        string contents = File.ReadAllText(path);
        Assert.That(contents.Split(new[] { "[RACE_TERMINATION]" }, StringSplitOptions.None).Length, Is.EqualTo(2));
        Assert.That(contents.Split(new[] { "[RACE_END]" }, StringSplitOptions.None).Length, Is.EqualTo(2));
    }

    private void DestroyHost()
    {
        // Unity does not deliver OnDestroy to a never-active object; invoke that callback explicitly.
        Invoke("OnDestroy");
        UnityEngine.Object.DestroyImmediate(host);
        host = null;
    }

    private void Set(string field, object value) => typeof(MVPGameManager).GetField(field, PrivateInstance).SetValue(manager, value);
    private object Get(string field) => typeof(MVPGameManager).GetField(field, PrivateInstance).GetValue(manager);
    private object Invoke(string method, params object[] arguments)
        => typeof(MVPGameManager).GetMethod(method, PrivateInstance).Invoke(manager, arguments);
}
