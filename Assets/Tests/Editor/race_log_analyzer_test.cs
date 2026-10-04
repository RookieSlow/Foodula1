using NUnit.Framework;
using System;
using System.IO;

public class RaceLogAnalyzerTests
{
    private const string ValidLog =
        "[TURN_START] turn=1\n" +
        "[CARD_PHASE] end\n" +
        "[MOVE_PHASE] begin\n" +
        "[MOVE_PHASE] end\n" +
        "[SLIPSTREAM_PHASE] begin events=1\n" +
        "[SLIPSTREAM_PHASE] end\n" +
        "[DISCARD] 你 selected=2 discarded=2\n" +
        "[RACE_END] finished";

    [TestCase("[TUTORIAL_SETUP] event=runtime_ready", RaceLogMode.Tutorial)]
    [TestCase("[FREE_RACE_SETUP] field=6", RaceLogMode.FreeRace)]
    [TestCase("[CAREER_SETUP] race=1/8", RaceLogMode.Career)]
    public void SetupMarkerIdentifiesModeWithoutGuessingFromResultText(
        string setup, RaceLogMode expected)
    {
        string log = setup + "\n" + ValidLog.Replace("[RACE_END]",
            "[RACE_TERMINATION] outcome=Completed\n[RACE_END]");
        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(log);

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.HasCompletedRaceEvidence, Is.True);
        Assert.That(result.Mode, Is.EqualTo(expected));
    }

    [Test]
    public void LegacyCompletedLogWithoutSetupKeepsUnknownMode()
    {
        string log = "track_name=[CAREER_SETUP] race=1/8\n" +
            ValidLog.Replace("[RACE_END]", "[RACE_TERMINATION] outcome=Completed\n[RACE_END]");
        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(log);

        Assert.That(result.HasCompletedRaceEvidence, Is.True);
        Assert.That(result.Mode, Is.EqualTo(RaceLogMode.Unknown));
    }

    [TestCase("[CAREER_SETUP] race=1/8\n[FREE_RACE_SETUP] field=6\n",
        "Conflicting race setup modes.")]
    [TestCase("[TURN_START] turn=0\n[CAREER_SETUP] race=1/8\n",
        "Race setup appeared after the first turn")]
    public void ConflictingOrLateModeCannotQualifyAsCompletedEvidence(
        string prefix, string expectedError)
    {
        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(prefix +
            ValidLog.Replace("[RACE_END]", "[RACE_TERMINATION] outcome=Completed\n[RACE_END]"));

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.HasCompletedRaceEvidence, Is.False);
        Assert.That(result.Errors, Has.Some.Contains(expectedError));
    }

    [TestCase(RaceLogTermination.Completed, true)]
    [TestCase(RaceLogTermination.Restarted, false)]
    [TestCase(RaceLogTermination.Exited, false)]
    [TestCase(RaceLogTermination.SceneDestroyed, false)]
    [TestCase(RaceLogTermination.TutorialIncomplete, false)]
    [TestCase(RaceLogTermination.Unknown, false)]
    public void ClosureCauseIsSeparateFromStructuralCompleteness(
        RaceLogTermination cause, bool completedEvidence)
    {
        string log = ValidLog.Replace("[RACE_END]", "[RACE_TERMINATION] outcome=" + cause + "\n[RACE_END]");
        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(log);
        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.Termination, Is.EqualTo(cause));
        Assert.That(result.HasCompletedRaceEvidence, Is.EqualTo(completedEvidence));
    }

    [TestCase("finished")]
    [TestCase("race reset")]
    [TestCase("scene destroyed")]
    [TestCase("生涯赛果已保存：总分 10")]
    public void LegacyEndProseDoesNotInventCompletedEvidence(string prose)
    {
        var result = RaceLogAnalyzer.Analyze(ValidLog.Replace("finished", prose));
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.Termination, Is.EqualTo(RaceLogTermination.Unknown));
        Assert.That(result.HasCompletedRaceEvidence, Is.False);
    }

    [TestCase("CompletedMore")]
    [TestCase("1")]
    [TestCase("completed")]
    [TestCase("")]
    public void InvalidClosureNamesCannotPassAcceptance(string outcome)
    {
        var result = RaceLogAnalyzer.Analyze(ValidLog.Replace("[RACE_END]",
            "[RACE_TERMINATION] outcome=" + outcome + "\n[RACE_END]"));
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.HasCompletedRaceEvidence, Is.False);
        Assert.That(result.Errors, Has.Some.Contains("Invalid [RACE_TERMINATION]"));
    }

    [TestCase("[TURN_START] turn=2")]
    [TestCase("[RACE_END] race reset")]
    [TestCase("# Foodula1 Race Test Log\ntrack_id=suzuka")]
    public void EventsOrAnotherFileAfterEndCannotBorrowEarlierCompletion(string suffix)
    {
        var result = RaceLogAnalyzer.Analyze(ValidLog.Replace("[RACE_END]",
            "[RACE_TERMINATION] outcome=Completed\n[RACE_END]") + "\n" + suffix);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.HasCompletedRaceEvidence, Is.False);
        Assert.That(result.TurnCount, Is.EqualTo(1));
    }

    [TestCase("[RACE_TERMINATION] outcome=Completed")]
    [TestCase("[TURN_START] turn=2")]
    public void ClosureMustBeUniqueAndImmediatelyPrecedeEnd(string between)
    {
        var result = RaceLogAnalyzer.Analyze(ValidLog.Replace("[RACE_END]",
            "[RACE_TERMINATION] outcome=Completed\n" + between + "\n[RACE_END]"));
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.HasCompletedRaceEvidence, Is.False);
    }

    [TestCase("")]
    [TestCase("[TURN_START] turn=1\n[CARD_PHASE] end\n[MOVE_PHASE] begin\n")]
    public void CompletedClosureWithoutCompleteTurnCannotPassAcceptance(string prefix)
    {
        var result = RaceLogAnalyzer.Analyze(prefix +
            "[RACE_TERMINATION] outcome=Completed\n[RACE_END] finished");
        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsComplete, Is.False);
        Assert.That(result.HasCompletedRaceEvidence, Is.False);
    }

    [Test]
    public void MetadataAndMultilineResultProseDoNotBecomePhaseMarkers()
    {
        string log = "# Foodula1 Race Test Log\nplayer=[TURN_START]\ntrack_name=[RACE_END]\n" +
            ValidLog.Replace("[RACE_END]", "[RACE_TERMINATION] outcome=Completed\n[RACE_END]") +
            "\n车手 [TURN_START] 已完赛\n说明：[RACE_END] 不代表视觉验收";
        log = log.Replace("[CARD_PHASE] end\n", "2026-09-29T11:38:20.930Z\t[CARD_PHASE] end\n");
        var result = RaceLogAnalyzer.Analyze(log);
        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.HasCompletedRaceEvidence, Is.True);
        Assert.That(result.TurnCount, Is.EqualTo(1));
    }

    [Test]
    public void CompletedMarkerDoesNotHideInvalidDiscardOrPhaseOrder()
    {
        var result = RaceLogAnalyzer.Analyze(ValidLog.Replace("discarded=2", "discarded=3")
            .Replace("[RACE_END]", "[RACE_TERMINATION] outcome=Completed\n[RACE_END]"));
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.IsValid, Is.False);
        Assert.That(result.HasCompletedRaceEvidence, Is.False);
    }

    [Test]
    public void ValidTurnWithTailwindAndDiscardHasExpectedOrder()
    {
        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(ValidLog);

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.TurnCount, Is.EqualTo(1));
        Assert.That(result.CompletedTurnCount, Is.EqualTo(1));
        Assert.That(result.IncompleteTurnCount, Is.EqualTo(0));
        Assert.That(result.SlipstreamPhaseCount, Is.EqualTo(1));
        Assert.That(result.DiscardEventCount, Is.EqualTo(1));
    }

    [Test]
    public void LegacyTailwindBeforeMovementIsReported()
    {
        const string log =
            "[TURN_START] turn=2\n" +
            "[CARD_PHASE] end\n" +
            "[SLIPSTREAM_PHASE] begin events=1\n" +
            "[MOVE_PHASE] begin\n" +
            "[MOVE_PHASE] end\n" +
            "[SLIPSTREAM_PHASE] end\n" +
            "[RACE_END] stopped";

        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(log);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contains("[SLIPSTREAM_PHASE] begin must follow [MOVE_PHASE] end"));
    }

    [Test]
    public void DiscardCountCannotExceedSelectedCount()
    {
        const string log =
            "[TURN_START] turn=1\n" +
            "[CARD_PHASE] end\n" +
            "[MOVE_PHASE] begin\n" +
            "[MOVE_PHASE] end\n" +
            "[DISCARD] 你 selected=1 discarded=2";

        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(log);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contains("discarded count 2 exceeds selected count 1"));
        Assert.That(result.IsComplete, Is.False);
    }

    [TestCase("selected=2147483648 discarded=0")]
    [TestCase("selected=1 discarded=2147483648")]
    public void OverflowingDiscardCountsAreReportedAsAnalysisErrors(string counts)
    {
        string log = ValidLog.Replace("selected=2 discarded=2", counts);

        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(log);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.Errors, Has.Some.Contains("invalid selected/discarded counts"));
    }

    [Test]
    public void MaximumIntDiscardCountsRemainValid()
    {
        const string counts = "selected=2147483647 discarded=2147483647";
        string log = ValidLog.Replace("selected=2 discarded=2", counts);

        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(log);

        Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
        Assert.That(result.IsComplete, Is.True);
        Assert.That(result.DiscardEventCount, Is.EqualTo(1));
    }

    [Test]
    public void IncompleteManualLogIsDistinguishedFromPhaseOrderError()
    {
        const string log =
            "[TURN_START] turn=1\n" +
            "[CARD_PHASE] end\n" +
            "[MOVE_PHASE] begin";

        RaceLogAnalysisResult result = RaceLogAnalyzer.Analyze(log);

        Assert.That(result.IsValid, Is.True);
        Assert.That(result.IsComplete, Is.False);
        Assert.That(result.IncompleteTurnCount, Is.EqualTo(1));
    }

    [Test]
    public void FileAdapterReadsSavedRaceLog()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "foodula1-race-log-" + Guid.NewGuid().ToString("N") + ".log");
        try
        {
            File.WriteAllText(path, ValidLog);

            RaceLogAnalysisResult result = RaceLogFileAnalyzer.AnalyzeFile(path);

            Assert.That(result.IsValid, Is.True, string.Join("; ", result.Errors));
            Assert.That(result.IsComplete, Is.True);
            Assert.That(result.TurnCount, Is.EqualTo(1));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Test]
    public void FileAdapterReportsMissingLogWithoutThrowing()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            "foodula1-missing-" + Guid.NewGuid().ToString("N") + ".log");

        RaceLogAnalysisResult result = RaceLogFileAnalyzer.AnalyzeFile(path);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors, Has.Some.Contains("Log file not found"));
    }
}
