using System;
using System.IO;
using NUnit.Framework;

public class RaceLogFileSelectorTests
{
    private const string CompletedLog =
        "[TURN_START] turn=1\n" +
        "[CARD_PHASE] end\n" +
        "[MOVE_PHASE] begin\n" +
        "[MOVE_PHASE] end\n" +
        "[RACE_TERMINATION] outcome=Completed\n" +
        "[RACE_END] finished";

    [Test]
    public void NewestLogWinsRegardlessOfDirectoryOrder()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string older = Path.Combine(directory, "z-older.log");
            string newer = Path.Combine(directory, "a-newer.log");
            string ignored = Path.Combine(directory, "other.txt");
            File.WriteAllText(older, "older");
            File.WriteAllText(newer, "newer");
            File.WriteAllText(ignored, "ignored");
            DateTime timestamp = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(older, timestamp);
            File.SetLastWriteTimeUtc(newer, timestamp.AddMinutes(1));
            File.SetLastWriteTimeUtc(ignored, timestamp.AddMinutes(2));

            Assert.That(RaceLogFileAnalyzer.FindLatestLogPath(directory), Is.EqualTo(newer));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void EqualTimestampsUseStableOrdinalPathOrder()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string first = Path.Combine(directory, "a.log");
            string second = Path.Combine(directory, "b.log");
            File.WriteAllText(second, "second");
            File.WriteAllText(first, "first");
            DateTime timestamp = new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc);
            File.SetLastWriteTimeUtc(first, timestamp);
            File.SetLastWriteTimeUtc(second, timestamp);

            Assert.That(RaceLogFileAnalyzer.FindLatestLogPath(directory), Is.EqualTo(first));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void EmptyDirectoryHasNoLatestLog()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            Assert.That(RaceLogFileAnalyzer.FindLatestLogPath(directory), Is.Null);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void CompletedSelectorSkipsNewerAbortedAndLegacyLogs()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string completed = WriteLog(directory, "completed.log", CompletedLog, 0);
            WriteLog(directory, "aborted.log",
                CompletedLog.Replace("outcome=Completed", "outcome=Exited"), 1);
            string legacy = WriteLog(directory, "legacy.log",
                CompletedLog.Replace("[RACE_TERMINATION] outcome=Completed\n", ""), 2);

            Assert.That(RaceLogFileAnalyzer.FindLatestLogPath(directory), Is.EqualTo(legacy));
            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(directory), Is.EqualTo(completed));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void CompletedSelectorSkipsNewerMalformedLog()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string completed = WriteLog(directory, "completed.log", CompletedLog, 0);
            WriteLog(directory, "invalid.log",
                CompletedLog.Replace("[MOVE_PHASE] end\n", ""), 1);

            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(directory), Is.EqualTo(completed));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void CompletedSelectorUsesStableOrdinalTieBreak()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string first = WriteLog(directory, "a.log", CompletedLog, 0);
            WriteLog(directory, "b.log", CompletedLog, 0);

            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(directory), Is.EqualTo(first));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void CompletedSelectorReturnsNullWhenNoRunQualifies()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(directory), Is.Null);
            WriteLog(directory, "aborted.log",
                CompletedLog.Replace("outcome=Completed", "outcome=Restarted"), 0);
            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(directory), Is.Null);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void ModeSelectorKeepsFreeAndCareerCompletionSeparate()
    {
        string directory = CreateTemporaryDirectory();
        try
        {
            string free = WriteLog(directory, "free.log",
                "[FREE_RACE_SETUP] field=6\n" + CompletedLog, 0);
            string career = WriteLog(directory, "career.log",
                "[CAREER_SETUP] race=1/8\n" + CompletedLog, 1);
            WriteLog(directory, "newer-aborted-free.log",
                "[FREE_RACE_SETUP] field=6\n" +
                CompletedLog.Replace("outcome=Completed", "outcome=Exited"), 2);
            string unknownMode = WriteLog(directory, "newer-unknown-mode.log", CompletedLog, 3);

            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(directory),
                Is.EqualTo(unknownMode));
            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(
                directory, RaceLogMode.FreeRace), Is.EqualTo(free));
            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(
                directory, RaceLogMode.Career), Is.EqualTo(career));
            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(
                directory, RaceLogMode.Unknown), Is.Null);
            Assert.That(RaceLogFileAnalyzer.FindLatestCompletedLogPath(
                directory, RaceLogMode.Tutorial), Is.Null);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string WriteLog(string directory, string name, string contents, int minuteOffset)
    {
        string path = Path.Combine(directory, name);
        File.WriteAllText(path, contents);
        File.SetLastWriteTimeUtc(path,
            new DateTime(2026, 9, 27, 0, 0, 0, DateTimeKind.Utc).AddMinutes(minuteOffset));
        return path;
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "foodula1-log-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
