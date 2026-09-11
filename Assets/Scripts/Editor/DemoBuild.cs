using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Reproducible command-line entry point for the Windows Demo build.
/// </summary>
public static class DemoBuild
{
    private const string OutputEnvironmentVariable = "FOODULA1_BUILD_OUTPUT";

    public static void BuildWindowsDemo()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .ToArray();

        if (scenes.Length == 0)
            throw new BuildFailedException("No enabled scenes are configured for the Demo build.");

        string outputPath = Environment.GetEnvironmentVariable(OutputEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            outputPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName,
                "Builds",
                $"Foodula1-Demo-v{PlayerSettings.bundleVersion}-Windows",
                "Foodula1.exe");
        }

        outputPath = Path.GetFullPath(outputPath);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.StrictMode | BuildOptions.DetailedBuildReport
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new BuildFailedException(
                $"Foodula1 Demo build failed: {report.summary.result} " +
                $"({report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings).");
        }

        Debug.Log(
            $"[DEMO_BUILD] Success version={PlayerSettings.bundleVersion} " +
            $"size={report.summary.totalSize} output={outputPath}");
    }
}
