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
