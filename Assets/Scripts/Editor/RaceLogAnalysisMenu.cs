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

        string latestPath;
        try
        {
            latestPath = RaceLogFileAnalyzer.FindLatestLogPath(directory);
        }
        catch (Exception exception)
        {
            Debug.LogError("[RaceLogAnalyzer] Cannot enumerate logs: " + exception.Message);
            return;
        }

        if (latestPath == null)
        {
            Debug.LogWarning("[RaceLogAnalyzer] No .log files found in " + directory);
            return;
        }

        AnalyzePath(latestPath);
    }

    [MenuItem("Foodula1/Tools/Analyze Latest Completed Race Log")]
    public static void AnalyzeLatestCompletedRaceLog()
    {
        AnalyzeLatestCompletedRaceLog(null);
    }

    [MenuItem("Foodula1/Tools/Analyze Latest Completed Free Race Log")]
    public static void AnalyzeLatestCompletedFreeRaceLog()
    {
        AnalyzeLatestCompletedRaceLog(RaceLogMode.FreeRace);
    }

    [MenuItem("Foodula1/Tools/Analyze Latest Completed Career Race Log")]
    public static void AnalyzeLatestCompletedCareerRaceLog()
    {
        AnalyzeLatestCompletedRaceLog(RaceLogMode.Career);
    }

    private static void AnalyzeLatestCompletedRaceLog(RaceLogMode? mode)
    {
        string directory = RaceTestLogWriter.GetDefaultDirectory();
        if (!Directory.Exists(directory))
        {
            Debug.LogWarning("[RaceLogAnalyzer] Log directory not found: " + directory);
            return;
        }

        string latestPath;
        try
        {
            latestPath = mode.HasValue
                ? RaceLogFileAnalyzer.FindLatestCompletedLogPath(directory, mode.Value)
                : RaceLogFileAnalyzer.FindLatestCompletedLogPath(directory);
        }
        catch (Exception exception)
        {
            Debug.LogError("[RaceLogAnalyzer] Cannot enumerate logs: " + exception.Message);
            return;
        }

        if (latestPath == null)
        {
            Debug.LogWarning("[RaceLogAnalyzer] No completed " +
                (mode.HasValue ? mode.Value + " " : string.Empty) +
                "race logs found in " + directory);
            return;
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
            "slipstreamPhases={4}, discardEvents={5}, termination={6}, mode={7}",
            Path.GetFileName(path),
            result.TurnCount,
            result.CompletedTurnCount,
            result.IncompleteTurnCount,
            result.SlipstreamPhaseCount,
            result.DiscardEventCount,
            result.Termination,
            result.Mode);

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

        if (!result.HasCompletedRaceEvidence)
        {
            Debug.LogWarning(summary + "；阶段结构已闭合，但结束原因是中止或未记录，不能作为完赛证据。");
            return;
        }

        Debug.Log(summary + "；完赛日志阶段顺序与弃牌数量检查通过，不代表视觉、存档或完整 Play Mode 验收。");
    }
}
