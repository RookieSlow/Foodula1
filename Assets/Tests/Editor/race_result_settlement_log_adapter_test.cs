using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NUnit.Framework;

// Shares the inactive participant/HUD and memory-repository fixture in
// race_result_settlement_adapter_test.cs; no second setup or player storage.
public sealed partial class RaceResultSettlementAdapterTests
{
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void SavedCareerResultIsLoggedBeforeOneCompletedClosure(TeamId team)
    {
        CareerRepository repository = CreateSeason(team, out MemoryCareerStore store);
        CareerRaceLaunchRequest launch = CreateLaunch(repository.Load().State, "logged-" + team);
        CreateParticipants(team, launch);
        SetTerminalCareerResults(false);
        using (var trace = new MemoryResultTrace(this))
        {
            SeedResultTrace(trace);
            CompleteCareer(repository);

            string contents = trace.Contents;
            AssertCareerResultBeforeClosure(contents,
                "[CAREER_RESULT] status=saved result_id=" + launch.ResultId);
            Assert.That(contents, Does.Contain("race=1/8 track=" + launch.TrackId));
            Assert.That(contents, Does.Contain("phase=Racing player_points=10 player_rank=1"));
            Assert.That(contents, Does.Contain("champion=" + team));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
            AssertClosedTrace(trace, RaceLogMode.Career, RaceLogTermination.Completed);
            AssertPresented();

            Complete(ForbiddenTechSave, ForbiddenXpSave,
                () => throw new InvalidOperationException("Saved result must not reopen storage."));
            Assert.That(trace.Contents, Is.EqualTo(contents), "Repeated display cannot append to a closed trace.");
            Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
            Assert.That(hud.gameOverText.text, Does.Contain("生涯赛果已保存。返回主菜单"));
        }
    }

    [Test]
    public void RejectedCareerSaveClosesAsCompletedButNeverClaimsSaved()
    {
        CareerRepository repository = CreateSeason(TeamId.UK, out MemoryCareerStore store);
        CareerRaceLaunchRequest launch = CreateLaunch(repository.Load().State, "logged-retry");
        CreateParticipants(TeamId.UK, launch);
        SetTerminalCareerResults(false);
        string originalJson = store.Json;
        store.FailWrites = true;
        using (var trace = new MemoryResultTrace(this))
        {
            SeedResultTrace(trace);
            CompleteCareer(repository);

            string rejectedTrace = trace.Contents;
            AssertCareerResultBeforeClosure(rejectedTrace,
                "[CAREER_RESULT] status=rejected result_id=" + launch.ResultId);
            Assert.That(rejectedTrace, Does.Contain("reason=生涯赛果保存失败，进度未推进"));
            Assert.That(rejectedTrace, Does.Not.Contain("status=saved"));
            Assert.That(store.Json, Is.EqualTo(originalJson));
            Assert.That(GetField<bool>("careerResultRecorded"), Is.False);
            // Completed describes race termination, not successful persistence.
            AssertClosedTrace(trace, RaceLogMode.Career, RaceLogTermination.Completed);
            Assert.That(hud.gameOverText.text, Does.Contain("保存失败，进度未推进"));
            AssertPresented();

            store.FailWrites = false;
            CompleteCareer(repository);
            Assert.That(repository.Load().State.NextTrackIndex, Is.EqualTo(1));
            Assert.That(GetField<bool>("careerResultRecorded"), Is.True);
            Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
            Assert.That(hud.gameOverText.text, Does.Contain("生涯赛果已保存："));
            Assert.That(trace.Contents, Is.EqualTo(rejectedTrace),
                "A later successful retry must not rewrite the rejected, closed trace.");
        }
    }

    [Test]
    public void PreviouslyRecordedCareerUsesAlreadySavedMarkerWithoutOpeningStorage()
    {
        CareerRepository repository = CreateSeason(TeamId.DE, out MemoryCareerStore store);
        CareerRaceLaunchRequest launch = CreateLaunch(repository.Load().State, "logged-existing");
        CreateParticipants(TeamId.DE, launch);
        SetTerminalCareerResults(false);
        Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId, session.Players,
            repository, out _, out _), Is.True);
        SetField("careerResultRecorded", true);
        string json = store.Json;
        using (var trace = new MemoryResultTrace(this))
        {
            SeedResultTrace(trace);
            Complete(ForbiddenTechSave, ForbiddenXpSave,
                () => throw new InvalidOperationException("Recorded result must not open storage."));

            AssertCareerResultBeforeClosure(trace.Contents,
                "[CAREER_RESULT] status=already_saved result_id=" + launch.ResultId);
            Assert.That(trace.Contents, Does.Not.Contain("status=saved"));
            Assert.That(store.Json, Is.EqualTo(json));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(2));
            AssertClosedTrace(trace, RaceLogMode.Career, RaceLogTermination.Completed);
            AssertPresented();
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public void InvalidCareerResultLogsRejectionWithoutProgressionOrNormalRewards(bool wrongTrack)
    {
        CareerRepository repository = CreateSeason(TeamId.IT, out MemoryCareerStore store);
        CareerRaceLaunchRequest launch = CreateLaunch(repository.Load().State, "logged-invalid");
        CreateParticipants(TeamId.IT, launch);
        SetTerminalCareerResults(false);
        if (wrongTrack) SetTrack(new TrackConfig { trackId = "fallback_42" });
        else Assert.That(CareerRaceSettlement.TryRecord(launch, launch.TrackId, session.Players,
            repository, out _, out _), Is.True);
        string json = store.Json;
        int writes = store.SuccessfulWrites;
        using (var trace = new MemoryResultTrace(this))
        {
            SeedResultTrace(trace);
            CompleteCareer(repository);

            AssertCareerResultBeforeClosure(trace.Contents,
                "[CAREER_RESULT] status=rejected result_id=" + launch.ResultId);
            Assert.That(trace.Contents, Does.Contain("reason=" + (wrongTrack
                ? "比赛结果阵容或完赛状态无效" : "生涯存档已变化，未写入本场结果")));
            Assert.That(trace.Contents, Does.Not.Contain("RP 奖励:"));
            Assert.That(store.Json, Is.EqualTo(json));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(writes));
            Assert.That(GetField<bool>("careerResultRecorded"), Is.False);
            AssertClosedTrace(trace, RaceLogMode.Career, RaceLogTermination.Completed);
            AssertPresented();
        }
    }

    [Test]
    public void NormalPersistenceFinishesBeforeRewardsAreLoggedAndTraceCloses()
    {
        CreateParticipants(TeamId.UK);
        SetTerminalCareerResults(false);
        var calls = new List<string>();
        using (var trace = new MemoryResultTrace(this))
        {
            SeedResultTrace(trace);
            Complete(state =>
            {
                AssertOpenResultTrace(trace);
                Assert.That(hud.gameOverPanel.activeSelf, Is.False);
                calls.Add("RP");
            }, (id, xp) =>
            {
                AssertOpenResultTrace(trace);
                Assert.That(calls, Is.EqualTo(new[] { "RP" }));
                calls.Add("XP");
            }, () => throw new InvalidOperationException("Normal race must not open career storage."));

            Assert.That(calls, Is.EqualTo(new[] { "RP", "XP" }));
            Assert.That(trace.Contents, Does.Contain("RP 奖励:"));
            Assert.That(trace.Contents, Does.Contain("XP"));
            Assert.That(trace.Contents, Does.Not.Contain("[CAREER_RESULT]"));
            AssertClosedTrace(trace, RaceLogMode.FreeRace, RaceLogTermination.Completed);
            AssertPresented();
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void NormalSaveFailureLeavesTraceOpenWithoutFalseCompletion(bool failXp)
    {
        CreateParticipants(TeamId.UK);
        SetTerminalCareerResults(false);
        var failure = new InvalidOperationException("injected logged save failure");
        using (var trace = new MemoryResultTrace(this))
        {
            SeedResultTrace(trace);
            TargetInvocationException raised = Assert.Throws<TargetInvocationException>(() => Complete(
                state => { if (!failXp) throw failure; },
                (id, xp) => throw failure,
                () => throw new InvalidOperationException("Wrong storage branch.")));

            Assert.That(raised.InnerException, Is.SameAs(failure));
            AssertOpenResultTrace(trace);
            RaceLogAnalysisResult analysis = RaceLogAnalyzer.Analyze(trace.Contents);
            Assert.That(analysis.IsComplete, Is.False);
            Assert.That(analysis.HasCompletedRaceEvidence, Is.False);
            Assert.That(analysis.Termination, Is.EqualTo(RaceLogTermination.Unknown));
            Assert.That(hud.gameOverPanel.activeSelf, Is.False);
            Assert.That(hand.gearSelectionPanel.activeSelf, Is.True);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TutorialDirectorControlsClosureEvenWithDormantCareer(bool completed)
    {
        CareerRepository repository = CreateSeason(TeamId.UK, out MemoryCareerStore store);
        CareerRaceLaunchRequest launch = CreateLaunch(repository.Load().State, "logged-dormant");
        TutorialScenarioDefinition tutorial = TutorialScenarioDefinition.CreateLeMansUk();
        CreateParticipants(TeamId.UK, launch, tutorial);
        SetTerminalCareerResults(false);
        var director = new TutorialRuntimeDirector(tutorial, true);
        if (completed) Assert.That(director.CompletePracticeLap(out _), Is.True);
        SetField("tutorialDirector", director);
        string json = store.Json;
        using (var trace = new MemoryResultTrace(this))
        {
            SeedResultTrace(trace);
            Complete(ForbiddenTechSave, ForbiddenXpSave,
                () => throw new InvalidOperationException("Tutorial must not open storage."));

            Assert.That(trace.Contents, Does.Not.Contain("[CAREER_SETUP]"));
            Assert.That(trace.Contents, Does.Not.Contain("[CAREER_RESULT]"));
            Assert.That(trace.Contents, Does.Contain("不发放 RP、车手 XP"));
            Assert.That(store.Json, Is.EqualTo(json));
            Assert.That(store.SuccessfulWrites, Is.EqualTo(1));
            AssertClosedTrace(trace, RaceLogMode.Tutorial, completed
                ? RaceLogTermination.Completed : RaceLogTermination.TutorialIncomplete);
            AssertPresented();
        }
    }

    private void SeedResultTrace(MemoryResultTrace trace)
    {
        TutorialRuntimeDirector director = GetField<TutorialRuntimeDirector>("tutorialDirector");
        FreeRaceRosterEntry[] roster = session.Players
            .Select(p => new FreeRaceRosterEntry(p.teamId, p.driverId)).ToArray();
        foreach (string line in RaceLogStartupRules.BuildSetupLines(
            GetField<TutorialScenarioDefinition>("tutorialScenario"),
            director?.Phase ?? TutorialRunPhase.Guided,
            GetField<CareerRaceLaunchRequest>("careerRaceLaunch"), roster))
            trace.Writer.Append(line);
        // Synthetic phase evidence only: result fixtures do not execute GameLoop.
        trace.Writer.Append("[TURN_START] turn=1\n[CARD_PHASE] end\n[MOVE_PHASE] begin\n[MOVE_PHASE] end");
    }

    private static void AssertCareerResultBeforeClosure(string contents, string expectedResult)
    {
        Assert.That(contents, Does.Contain(expectedResult));
        Assert.That(CountResultMarker(contents, "[CAREER_RESULT]"), Is.EqualTo(1));
        Assert.That(contents.IndexOf(expectedResult, StringComparison.Ordinal),
            Is.LessThan(contents.IndexOf("[RACE_TERMINATION]", StringComparison.Ordinal)));
    }

    private static void AssertOpenResultTrace(MemoryResultTrace trace)
    {
        Assert.That(trace.Writer.IsActive, Is.True);
        Assert.That(trace.Contents, Does.Not.Contain("[RACE_TERMINATION]"));
        Assert.That(trace.Contents, Does.Not.Contain("[RACE_END]"));
    }

    private static void AssertClosedTrace(MemoryResultTrace trace, RaceLogMode mode,
        RaceLogTermination termination)
    {
        Assert.That(trace.Writer.IsActive, Is.False);
        Assert.That(trace.Writer.FilePath, Is.Null, "Memory trace must never begin a disk log.");
        string contents = trace.Contents;
        Assert.That(CountResultMarker(contents, "[RACE_TERMINATION]"), Is.EqualTo(1));
        Assert.That(CountResultMarker(contents, "[RACE_END]"), Is.EqualTo(1));
        Assert.That(contents.IndexOf("[RACE_TERMINATION]", StringComparison.Ordinal),
            Is.LessThan(contents.IndexOf("[RACE_END]", StringComparison.Ordinal)));
        RaceLogAnalysisResult analysis = RaceLogAnalyzer.Analyze(contents);
        Assert.That(analysis.IsValid, Is.True, string.Join("; ", analysis.Errors));
        Assert.That(analysis.IsComplete, Is.True);
        Assert.That(analysis.Mode, Is.EqualTo(mode));
        Assert.That(analysis.Termination, Is.EqualTo(termination));
        Assert.That(analysis.HasCompletedRaceEvidence, Is.EqualTo(termination == RaceLogTermination.Completed));
    }

    private static int CountResultMarker(string contents, string marker)
        => contents.Replace("\r", string.Empty).Split('\n')
            .Count(line => line.Contains("\t" + marker));

    private sealed class MemoryResultTrace : IDisposable
    {
        private static readonly FieldInfo TelemetryInstance = typeof(PlaytestTelemetryService)
            .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly RaceResultSettlementAdapterTests fixture;
        private readonly MemoryStream stream = new MemoryStream();
        private readonly object previousTelemetry;
        private readonly RaceTestLogWriter previousWriter;
        public RaceTestLogWriter Writer { get; } = new RaceTestLogWriter("memory-only-not-opened");
        public string Contents => Encoding.UTF8.GetString(stream.ToArray());

        public MemoryResultTrace(RaceResultSettlementAdapterTests fixture)
        {
            this.fixture = fixture;
            previousTelemetry = TelemetryInstance.GetValue(null);
            previousWriter = fixture.GetField<RaceTestLogWriter>("raceLogWriter");
            var sink = new StreamWriter(stream, new UTF8Encoding(false), 1024, true) { AutoFlush = true };
            typeof(RaceTestLogWriter).GetField("writer", PrivateInstance).SetValue(Writer, sink);
            // Append/End also dispatch telemetry: do not append fixture events to a live session.
            TelemetryInstance.SetValue(null, null);
            fixture.SetField("raceLogWriter", Writer);
        }

        public void Dispose()
        {
            try { Writer.Dispose(); }
            finally
            {
                fixture.SetField("raceLogWriter", previousWriter);
                TelemetryInstance.SetValue(null, previousTelemetry);
                stream.Dispose();
            }
        }
    }
}
