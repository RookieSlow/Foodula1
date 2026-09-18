using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class PlaytestLoggingTests
{
    [Test]
    public void writer_creates_jsonl_metadata_and_summary_without_multiline_values()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            var metadata = new PlaytestSessionMetadata
            {
                sessionId = "test-session",
                appVersion = "0.1.1-test",
                platform = "Test"
            };

            string eventsPath;
            string metadataPath;
            string summaryPath;
            using (var writer = new PlaytestLogWriter(root, metadata))
            {
                eventsPath = writer.EventsPath;
                metadataPath = writer.MetadataPath;
                summaryPath = writer.SummaryPath;
                Assert.That(writer.Record(1.25f, "MainMenu", "pointer", "up",
                    "Canvas/Settings", "label=导出\n测试日志", 0.5f, 0.25f), Is.True);
                writer.Complete("test_complete");
            }

            string[] lines = File.ReadAllLines(eventsPath);
            Assert.That(lines, Has.Length.EqualTo(2));
            Assert.That(lines[0], Does.Contain("\"category\":\"pointer\""));
            Assert.That(lines[0], Does.Contain("label=导出 测试日志"));
            Assert.That(lines[0], Does.Not.Contain("\\n"));
            Assert.That(lines[1], Does.Contain("session_end"));

            PlaytestSessionMetadata savedMetadata = JsonUtility.FromJson<PlaytestSessionMetadata>(File.ReadAllText(metadataPath));
            PlaytestSessionSummary savedSummary = JsonUtility.FromJson<PlaytestSessionSummary>(File.ReadAllText(summaryPath));
            Assert.That(savedMetadata.endReason, Is.EqualTo("test_complete"));
            Assert.That(savedSummary.totalEvents, Is.EqualTo(2));
                Assert.That(savedSummary.pointerEvents, Is.EqualTo(1));
                Assert.That(savedSummary.lifecycleEvents, Is.EqualTo(1));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public void writer_truncates_oversized_values_and_prunes_old_sessions()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            for (int i = 0; i < 4; i++)
            {
                string path = Path.Combine(root, "session-old-" + i);
                Directory.CreateDirectory(path);
                Directory.SetCreationTimeUtc(path, DateTime.UtcNow.AddMinutes(-20 + i));
            }

            PlaytestLogWriter.PruneSessions(root, 2);
            Assert.That(Directory.GetDirectories(root, "session-*").Length, Is.EqualTo(2));
            Assert.That(PlaytestLogWriter.SanitizeValue(new string('x', 50), 12),
                Is.EqualTo(new string('x', 12) + "…"));
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    [Test]
    public void exporter_bundles_playtest_and_race_logs_with_privacy_readme()
    {
        string root = CreateTemporaryDirectory();
        try
        {
            string playtestRoot = Path.Combine(root, "playtest-logs");
            string raceRoot = Path.Combine(root, "race-logs");
            string exportRoot = Path.Combine(root, "exports");
            Directory.CreateDirectory(Path.Combine(playtestRoot, "session-example"));
            Directory.CreateDirectory(raceRoot);
            File.WriteAllText(Path.Combine(playtestRoot, "session-example", "events.jsonl"), "{}\n");
            File.WriteAllText(Path.Combine(raceRoot, "race-example.log"), "[RACE_END] finished\n");

            string zipPath = PlaytestLogExporter.Export(playtestRoot, raceRoot, exportRoot, "0.1.1-test");

            Assert.That(File.Exists(zipPath), Is.True);
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                string[] names = archive.Entries.Select(entry => entry.FullName).ToArray();
                Assert.That(names, Does.Contain("playtest-logs/session-example/events.jsonl"));
                Assert.That(names, Does.Contain("race-logs/race-example.log"));
                Assert.That(names, Does.Contain("README.txt"));
                Assert.That(names, Does.Contain("manifest.json"));
                using (var reader = new StreamReader(archive.GetEntry("README.txt").Open()))
                {
                    string readme = reader.ReadToEnd();
                    Assert.That(readme, Does.Contain("does not intentionally contain typed text"));
                    Assert.That(readme, Does.Contain("F8 issue markers"));
                }
                using (var reader = new StreamReader(archive.GetEntry("manifest.json").Open()))
                {
                    string manifest = reader.ReadToEnd();
                    Assert.That(manifest, Does.Contain("\"playtestFiles\": 1"));
                    Assert.That(manifest, Does.Contain("\"raceLogFiles\": 1"));
                    Assert.That(manifest, Does.Contain("\"totalFiles\": 4"));
                }
            }
        }
        finally
        {
            DeleteDirectory(root);
        }
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "foodula1-playtest-log-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        if (Directory.Exists(path))
            Directory.Delete(path, true);
    }
}
