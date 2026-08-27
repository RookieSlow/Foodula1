using System;
using System.IO;

/// <summary>
/// File-system adapter for <see cref="RaceLogAnalyzer"/>. The text analyzer
/// remains deterministic and side-effect free; this boundary only loads a
/// saved manual-play log and turns I/O failures into an analysis result.
/// </summary>
public static class RaceLogFileAnalyzer
{
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
