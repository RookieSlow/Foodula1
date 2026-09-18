using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

[Serializable]
public sealed class PlaytestSessionMetadata
{
    public int schemaVersion = 1;
    public string sessionId;
    public string startedUtc;
    public string endedUtc;
    public string endReason;
    public string appVersion;
    public string unityVersion;
    public string platform;
    public string language;
    public int screenWidth;
    public int screenHeight;
    public bool fullscreen;
    public string operatingSystem;
    public string processorType;
    public string graphicsDeviceName;
    public int systemMemoryMb;
    public int graphicsMemoryMb;
    public string privacy = "Local only; no typed text, player identity, device identifier, or automatic upload.";
}

[Serializable]
public sealed class PlaytestOperationEvent
{
    public int schemaVersion = 1;
    public long sequence;
    public string utc;
    public float realtime;
    public string scene;
    public string category;
    public string action;
    public string target;
    public string details;
    public float pointerX = -1f;
    public float pointerY = -1f;
    public float timeScale = 1f;
}

[Serializable]
public sealed class PlaytestSessionSummary
{
    public int schemaVersion = 1;
    public string sessionId;
    public string updatedUtc;
    public long totalEvents;
    public long lifecycleEvents;
    public long sceneEvents;
    public long pointerEvents;
    public long keyEvents;
    public long raceEvents;
    public long performanceEvents;
    public long issueMarkerEvents;
    public long warningEvents;
    public long errorEvents;
}

/// <summary>
/// Thread-safe JSONL writer used by the distributed Demo playtest recorder.
/// File failures disable only diagnostics and never interrupt gameplay.
/// </summary>
public sealed class PlaytestLogWriter : IDisposable
{
    public const int DefaultRetainedSessions = 30;
    public const int MaximumValueLength = 2000;

    private readonly object sync = new object();
    private readonly PlaytestSessionMetadata metadata;
    private readonly PlaytestSessionSummary summary;
    private StreamWriter eventsWriter;
    private bool disposed;

    public PlaytestLogWriter(
        string rootDirectory,
        PlaytestSessionMetadata sessionMetadata,
        int retainedSessions = DefaultRetainedSessions)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory))
            throw new ArgumentException("A playtest log root directory is required.", nameof(rootDirectory));

        RootDirectory = rootDirectory;
        metadata = sessionMetadata ?? new PlaytestSessionMetadata();
        metadata.sessionId = string.IsNullOrWhiteSpace(metadata.sessionId)
            ? Guid.NewGuid().ToString("N")
            : SanitizeValue(metadata.sessionId, 80);
        metadata.startedUtc = string.IsNullOrWhiteSpace(metadata.startedUtc)
            ? DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)
            : SanitizeValue(metadata.startedUtc, 80);

        summary = new PlaytestSessionSummary { sessionId = metadata.sessionId };
        Directory.CreateDirectory(RootDirectory);
        PruneSessions(RootDirectory, Mathf.Max(1, retainedSessions - 1));

        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        SessionDirectory = Path.Combine(RootDirectory, $"session-{stamp}-{metadata.sessionId}");
        Directory.CreateDirectory(SessionDirectory);
        EventsPath = Path.Combine(SessionDirectory, "events.jsonl");
        MetadataPath = Path.Combine(SessionDirectory, "session.json");
        SummaryPath = Path.Combine(SessionDirectory, "summary.json");

        var stream = new FileStream(EventsPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        eventsWriter = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = false };
        WriteMetadata();
        WriteSummary();
    }

    public string RootDirectory { get; }
    public string SessionDirectory { get; }
    public string EventsPath { get; }
    public string MetadataPath { get; }
    public string SummaryPath { get; }
    public bool IsActive => !disposed && eventsWriter != null;

    public bool Record(
        float realtime,
        string scene,
        string category,
        string action,
        string target = "",
        string details = "",
        float pointerX = -1f,
        float pointerY = -1f,
        float timeScale = 1f)
    {
        lock (sync)
        {
            if (!IsActive)
                return false;

            try
            {
                var entry = new PlaytestOperationEvent
                {
                    sequence = ++summary.totalEvents,
                    utc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                    realtime = realtime,
                    scene = SanitizeValue(scene, 160),
                    category = SanitizeValue(category, 80),
                    action = SanitizeValue(action, 120),
                    target = SanitizeValue(target, 512),
                    details = SanitizeValue(details, MaximumValueLength),
                    pointerX = pointerX,
                    pointerY = pointerY,
                    timeScale = timeScale
                };

                IncrementCategory(entry.category, entry.action);
                eventsWriter.WriteLine(JsonUtility.ToJson(entry));
                bool critical = entry.category == "console"
                    || entry.category == "lifecycle"
                    || entry.action == "race_log_finished";
                if (critical || summary.totalEvents % 10 == 0)
                    eventsWriter.Flush();
                if (summary.totalEvents % 25 == 0)
                    WriteSummary();
                return true;
            }
            catch
            {
                CloseWriter();
                return false;
            }
        }
    }

    public void Flush()
    {
        lock (sync)
        {
            try
            {
                eventsWriter?.Flush();
                WriteSummary();
            }
            catch
            {
                CloseWriter();
            }
        }
    }

    public void Complete(string reason)
    {
        lock (sync)
        {
            if (disposed)
                return;

            Record(-1f, string.Empty, "lifecycle", "session_end", details: reason);
            metadata.endedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            metadata.endReason = SanitizeValue(reason, 160);
            WriteMetadata();
            WriteSummary();
            disposed = true;
            CloseWriter();
        }
    }

    public void Dispose()
    {
        Complete("disposed");
    }

    public static string SanitizeValue(string value, int maximumLength = MaximumValueLength)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        string sanitized = value
            .Replace("\r", " ")
            .Replace("\n", " ")
            .Replace("\t", " ")
            .Trim();
        if (sanitized.Length > maximumLength)
            sanitized = sanitized.Substring(0, maximumLength) + "…";
        return sanitized;
    }

    public static void PruneSessions(string rootDirectory, int retainedSessions)
    {
        if (!Directory.Exists(rootDirectory))
            return;

        try
        {
            DirectoryInfo[] sessions = new DirectoryInfo(rootDirectory)
                .GetDirectories("session-*");
            Array.Sort(sessions, (left, right) => right.CreationTimeUtc.CompareTo(left.CreationTimeUtc));
            for (int i = Mathf.Max(0, retainedSessions); i < sessions.Length; i++)
                sessions[i].Delete(true);
        }
        catch
        {
            // Retention is best effort. A locked old log must not disable new logging.
        }
    }

    private void IncrementCategory(string category, string action)
    {
        switch (category)
        {
            case "lifecycle": summary.lifecycleEvents++; break;
            case "scene": summary.sceneEvents++; break;
            case "pointer": summary.pointerEvents++; break;
            case "key": summary.keyEvents++; break;
            case "race": summary.raceEvents++; break;
            case "performance": summary.performanceEvents++; break;
            case "diagnostics":
                if (action == "issue_marked") summary.issueMarkerEvents++;
                break;
            case "console":
                if (action == "warning") summary.warningEvents++;
                else if (action == "error" || action == "exception" || action == "assert") summary.errorEvents++;
                break;
        }
    }

    private void WriteMetadata()
    {
        try
        {
            File.WriteAllText(MetadataPath, JsonUtility.ToJson(metadata, true), new UTF8Encoding(false));
        }
        catch
        {
            // Metadata is secondary to the append-only event stream.
        }
    }

    private void WriteSummary()
    {
        summary.updatedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        try
        {
            File.WriteAllText(SummaryPath, JsonUtility.ToJson(summary, true), new UTF8Encoding(false));
        }
        catch
        {
            // Summary is best effort; events remain authoritative.
        }
    }

    private void CloseWriter()
    {
        StreamWriter current = eventsWriter;
        eventsWriter = null;
        if (current == null)
            return;
        try
        {
            current.Flush();
            current.Dispose();
        }
        catch
        {
            // Never throw from diagnostics shutdown.
        }
    }
}
