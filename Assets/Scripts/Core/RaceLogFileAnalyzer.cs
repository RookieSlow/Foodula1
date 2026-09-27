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
        string[] paths = Directory.GetFiles(directory, "*.log");
        if (paths.Length == 0)
            return null;

        Array.Sort(paths, StringComparer.Ordinal);
        string latestPath = paths[0];
        DateTime latestWrite = File.GetLastWriteTimeUtc(latestPath);
        for (int i = 1; i < paths.Length; i++)
        {
            DateTime candidateWrite = File.GetLastWriteTimeUtc(paths[i]);
            if (candidateWrite > latestWrite)
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
