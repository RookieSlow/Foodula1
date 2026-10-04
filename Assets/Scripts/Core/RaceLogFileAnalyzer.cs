using System;
using System.IO;

/// <summary>
/// File-system adapter for <see cref="RaceLogAnalyzer"/>. The text analyzer
/// remains deterministic and side-effect free; this boundary only loads a
/// saved manual-play log and turns I/O failures into an analysis result.
/// </summary>
public static class RaceLogFileAnalyzer
{
    /// <summary>
    /// Finds the newest saved log. Equal timestamps are resolved by ordinal
    /// path order so the editor menu selects the same file on every run.
    /// Returns null when the directory contains no race logs.
    /// </summary>
    public static string FindLatestLogPath(string directory)
    {
        return FindLatestLogPath(directory, _ => true);
    }

    /// <summary>
    /// Finds the newest log with explicit, valid completed-race evidence.
    /// Aborted, legacy and malformed logs remain available through the
    /// unfiltered selector but cannot be mistaken for a finished run.
    /// </summary>
    public static string FindLatestCompletedLogPath(string directory)
    {
        return FindLatestLogPath(directory, path => AnalyzeFile(path).HasCompletedRaceEvidence);
    }

    /// <summary>
    /// Selects completed evidence for one explicitly recorded mode. Logs with
    /// no setup marker never inherit a mode from their filename or prose.
    /// </summary>
    public static string FindLatestCompletedLogPath(string directory, RaceLogMode mode)
    {
        if (mode == RaceLogMode.Unknown) return null;
        return FindLatestLogPath(directory, path =>
        {
            RaceLogAnalysisResult analysis = AnalyzeFile(path);
            return analysis.HasCompletedRaceEvidence && analysis.Mode == mode;
        });
    }

    private static string FindLatestLogPath(string directory, Func<string, bool> accepts)
    {
        string[] paths = Directory.GetFiles(directory, "*.log");
        if (paths.Length == 0)
            return null;

        Array.Sort(paths, StringComparer.Ordinal);
        string latestPath = null;
        DateTime latestWrite = DateTime.MinValue;
        for (int i = 0; i < paths.Length; i++)
        {
            if (!accepts(paths[i]))
                continue;

            DateTime candidateWrite = File.GetLastWriteTimeUtc(paths[i]);
            if (latestPath == null || candidateWrite > latestWrite)
            {
                latestPath = paths[i];
                latestWrite = candidateWrite;
            }
        }

        return latestPath;
    }

    /// <summary>Analyzes one saved race log without throwing to the caller.</summary>
    public static RaceLogAnalysisResult AnalyzeFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return Failure("Log path is empty.");

        if (!File.Exists(path))
            return Failure("Log file not found: " + path);

        try
        {
            return RaceLogAnalyzer.Analyze(File.ReadAllText(path));
        }
        catch (Exception exception)
        {
            return Failure("Unable to read log file: " + exception.Message);
        }
    }

    private static RaceLogAnalysisResult Failure(string message)
    {
        var result = new RaceLogAnalysisResult();
        result.AddError(message);
        return result;
    }
}
