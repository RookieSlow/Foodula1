using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Persists a readable race trace for manual playtest analysis.
/// Gameplay treats this adapter as optional: file failures never stop a race.
/// Architecture boundary: ADR-002 keeps this Unity/file-system adapter outside
/// the pure race rules layer.
/// </summary>
public sealed class RaceTestLogWriter : IDisposable
{
    private static readonly Regex RichTextTag = new Regex("<.*?>", RegexOptions.Compiled);

    private readonly string directory;
    private StreamWriter writer;

    public RaceTestLogWriter(string outputDirectory = null)
    {
        directory = string.IsNullOrEmpty(outputDirectory)
            ? GetDefaultDirectory()
            : outputDirectory;
    }

    /// <summary>Absolute path of the current or most recently closed log.</summary>
    public string FilePath { get; private set; }

    /// <summary>Directory used for the current writer, useful for test tooling.</summary>
    public string OutputDirectory => directory;

    /// <summary>Whether a race log is currently accepting events.</summary>
    public bool IsActive => writer != null;

    /// <summary>Returns the default persistent directory used by race logs.</summary>
    public static string GetDefaultDirectory()
    {
        return Path.Combine(Application.persistentDataPath, "race-logs");
    }

    /// <summary>Starts a fresh log file for one race.</summary>
    public void BeginRace(string trackId, string trackName, string playerName, TeamId playerTeam, int opponentCount)
    {
        Close();
        FilePath = null;

        try
        {
            Directory.CreateDirectory(directory);
            string safeTrack = SanitizeFilePart(string.IsNullOrEmpty(trackId) ? "track" : trackId);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            FilePath = Path.Combine(directory, $"race-{stamp}-{safeTrack}-{Guid.NewGuid():N}.log");
            writer = new StreamWriter(FilePath, false, new UTF8Encoding(false))
            {
                AutoFlush = true
            };

        writer.WriteLine("# Foodula1 Race Test Log");
            writer.WriteLine($"started_utc={DateTime.UtcNow:O}");
            writer.WriteLine($"track_id={SanitizeValue(trackId)}");
            writer.WriteLine($"track_name={SanitizeValue(trackName)}");
            writer.WriteLine($"player={SanitizeValue(playerName)}");
            writer.WriteLine($"player_team={playerTeam}");
            writer.WriteLine($"opponents={opponentCount}");
            writer.WriteLine();
        }
        catch (Exception exception)
        {
            Close();
            Debug.LogWarning($"[RaceTestLog] Logging disabled: {exception.Message}");
        }
    }

    /// <summary>Appends one or more readable, timestamped event lines.</summary>
    public void Append(string message)
    {
        if (writer == null || string.IsNullOrEmpty(message))
            return;

        try
        {
            string plain = StripRichText(message).Replace("\r", string.Empty);
            string[] lines = plain.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                writer.WriteLine($"{DateTime.UtcNow:O}\t{lines[i]}");
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"[RaceTestLog] Write failed; logging disabled: {exception.Message}");
            Close();
        }
    }

    /// <summary>Writes a final marker and closes the current file.</summary>
    public void End(string result)
    {
        if (writer == null)
            return;

        Append("[RACE_END] " + result);
        Close();
    }

    public void Dispose()
    {
        Close();
    }

    internal static string StripRichText(string message)
    {
        return string.IsNullOrEmpty(message)
            ? string.Empty
            : RichTextTag.Replace(message, string.Empty);
    }

    private static string SanitizeFilePart(string value)
    {
        string safe = SanitizeValue(value);
        foreach (char invalid in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalid, '_');
        return string.IsNullOrEmpty(safe) ? "track" : safe;
    }

    private static string SanitizeValue(string value)
    {
        return (value ?? string.Empty).Replace("\r", " ").Replace("\n", " ").Trim();
    }

    private void Close()
    {
        if (writer == null)
            return;

        StreamWriter current = writer;
        writer = null;
        try
        {
            current.Flush();
            current.Dispose();
        }
        catch (Exception exception)
        {
            // Logging is best effort and must never abort scene shutdown.
            Debug.LogWarning($"[RaceTestLog] Close failed: {exception.Message}");
        }
    }
}
