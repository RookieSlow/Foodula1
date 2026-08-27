using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only entry points for reviewing the timestamped manual race logs.
/// This tool reads existing files only and never changes gameplay or project
/// assets.
/// </summary>
public static class RaceLogAnalysisMenu
{
    [MenuItem("Foodula1/Tools/Analyze Latest Race Log")]
    public static void AnalyzeLatestRaceLog()
    {
        string directory = RaceTestLogWriter.GetDefaultDirectory();
        if (!Directory.Exists(directory))
        {
            Debug.LogWarning("[RaceLogAnalyzer] Log directory not found: " + directory);
            return;
        }

        string[] paths;
        try
        {
            paths = Directory.GetFiles(directory, "*.log");
        }
        catch (Exception exception)
        {
            Debug.LogError("[RaceLogAnalyzer] Cannot enumerate logs: " + exception.Message);
            return;
        }

        if (paths.Length == 0)
        {
            Debug.LogWarning("[RaceLogAnalyzer] No .log files found in " + directory);
            return;
        }

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

        AnalyzePath(latestPath);
    }

    [MenuItem("Foodula1/Tools/Analyze Selected Race Log")]
    public static void AnalyzeSelectedRaceLog()
    {
        string path = EditorUtility.OpenFilePanel(
            "选择 Foodula1 Race Log",
            RaceTestLogWriter.GetDefaultDirectory(),
            "log");
        if (!string.IsNullOrEmpty(path))
            AnalyzePath(path);
    }

    private static void AnalyzePath(string path)
    {
        RaceLogAnalysisResult result = RaceLogFileAnalyzer.AnalyzeFile(path);
        string summary = string.Format(
            "[RaceLogAnalyzer] {0}: turns={1}, completed={2}, incomplete={3}, " +
            "slipstreamPhases={4}, discardEvents={5}",
            Path.GetFileName(path),
            result.TurnCount,
            result.CompletedTurnCount,
            result.IncompleteTurnCount,
            result.SlipstreamPhaseCount,
            result.DiscardEventCount);

        if (!result.IsValid)
        {
            Debug.LogError(summary + "\n - " + string.Join("\n - ", result.Errors));
            return;
        }

        if (!result.IsComplete)
        {
            Debug.LogWarning(summary + "；日志结构有效，但未形成完整 RACE_END 对局。");
            return;
        }

        Debug.Log(summary + "；阶段顺序与弃牌数量检查通过。");
    }
}
