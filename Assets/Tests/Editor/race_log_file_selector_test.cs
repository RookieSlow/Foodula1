using System;
using System.IO;
using NUnit.Framework;

public class RaceLogFileSelectorTests
{
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

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "foodula1-log-selection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}
