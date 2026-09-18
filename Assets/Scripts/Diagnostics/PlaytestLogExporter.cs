using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

/// <summary>Creates a tester-controlled ZIP containing local operation and race logs.</summary>
public static class PlaytestLogExporter
{
    [Serializable]
    private sealed class ExportManifest
    {
        public int schemaVersion = 1;
        public string exportedUtc;
        public string appVersion;
        public int playtestFiles;
        public int raceLogFiles;
        public int totalFiles;
        public string privacy = "Tester-controlled local export; review before sending.";
    }

    public static string ExportDefaultBundle()
    {
        PlaytestTelemetryService.Record("diagnostics", "export_requested");
        PlaytestTelemetryService.Flush();
        return Export(
            PlaytestTelemetryService.DefaultLogRoot,
            RaceTestLogWriter.GetDefaultDirectory(),
            PlaytestTelemetryService.DefaultExportRoot,
            Application.version);
    }

    public static string Export(
        string playtestLogRoot,
        string raceLogRoot,
        string exportRoot,
        string appVersion)
    {
        Directory.CreateDirectory(exportRoot);
        string stamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff", CultureInfo.InvariantCulture);
        string safeVersion = SanitizeFilePart(string.IsNullOrWhiteSpace(appVersion) ? "unknown" : appVersion);
        string destination = Path.Combine(exportRoot, $"Foodula1-playtest-v{safeVersion}-{stamp}.zip");

        using (var stream = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
        {
            int playtestFiles = AddDirectory(archive, playtestLogRoot, "playtest-logs");
            int raceLogFiles = AddDirectory(archive, raceLogRoot, "race-logs");
            ZipArchiveEntry readme = archive.CreateEntry(
                "README.txt",
                System.IO.Compression.CompressionLevel.Optimal);
            using (var writer = new StreamWriter(readme.Open(), new UTF8Encoding(false)))
            {
                writer.WriteLine("Foodula1 playtest evidence bundle");
                writer.WriteLine($"Exported UTC: {DateTime.UtcNow:O}");
                writer.WriteLine($"App version: {appVersion}");
                writer.WriteLine();
                writer.WriteLine("This bundle was created only after the tester selected Export Test Logs.");
                writer.WriteLine("It contains local gameplay operations, structured race traces, warnings and errors.");
                writer.WriteLine("F8 issue markers may include screenshots of the game window and anonymous performance samples.");
                writer.WriteLine("It contains broad technical compatibility information (OS, CPU/GPU name, memory and resolution).");
                writer.WriteLine("It does not intentionally contain typed text, player identity, device identifiers, accounts, or automatic uploads.");
                writer.WriteLine("Please review the files before sending them to the developer.");
            }

            var manifest = new ExportManifest
            {
                exportedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                appVersion = appVersion,
                playtestFiles = playtestFiles,
                raceLogFiles = raceLogFiles,
                totalFiles = playtestFiles + raceLogFiles + 2
            };
            ZipArchiveEntry manifestEntry = archive.CreateEntry(
                "manifest.json",
                System.IO.Compression.CompressionLevel.Optimal);
            using (var writer = new StreamWriter(manifestEntry.Open(), new UTF8Encoding(false)))
                writer.Write(JsonUtility.ToJson(manifest, true));
        }

        PlaytestTelemetryService.Record("diagnostics", "export_completed", details: Path.GetFileName(destination));
        return destination;
    }

    public static void RevealInFileBrowser(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;
        string directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
            Application.OpenURL("file:///" + directory.Replace('\\', '/'));
    }

    private static int AddDirectory(ZipArchive archive, string sourceRoot, string entryRoot)
    {
        if (string.IsNullOrWhiteSpace(sourceRoot) || !Directory.Exists(sourceRoot))
            return 0;

        string[] files = Directory.GetFiles(sourceRoot, "*", SearchOption.AllDirectories);
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < files.Length; i++)
        {
            string relative = files[i].Substring(sourceRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string entryName = entryRoot + "/" + relative.Replace('\\', '/');
            ZipArchiveEntry entry = archive.CreateEntry(
                entryName,
                System.IO.Compression.CompressionLevel.Optimal);
            using (Stream source = new FileStream(files[i], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (Stream destination = entry.Open())
                source.CopyTo(destination);
        }
        return files.Length;
    }

    private static string SanitizeFilePart(string value)
    {
        string safe = value.Trim();
        foreach (char invalid in Path.GetInvalidFileNameChars())
            safe = safe.Replace(invalid, '_');
        return safe;
    }
}
