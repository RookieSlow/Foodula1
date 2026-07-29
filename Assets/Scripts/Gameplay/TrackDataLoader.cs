using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Loads track JSON configs from Resources/Configs/Tracks/ and converts them
/// to the runtime TrackNode format used by TrackManager.
/// </summary>
public static class TrackDataLoader
{
    private const string TRACKS_RESOURCES_PATH = "Configs/Tracks";

    /// <summary>
    /// Load a track config by its trackId (filename without .json).
    /// Returns null if the file doesn't exist.
    /// </summary>
    public static TrackConfig LoadConfig(string trackId)
    {
        TextAsset jsonAsset = Resources.Load<TextAsset>($"{TRACKS_RESOURCES_PATH}/{trackId}");
        if (jsonAsset == null)
        {
            Debug.LogError($"[TrackDataLoader] Track config not found: Resources/{TRACKS_RESOURCES_PATH}/{trackId}.json");
            return null;
        }

        TrackConfig config = JsonUtility.FromJson<TrackConfig>(jsonAsset.text);

        if (config == null || config.cells == null || config.cells.Length == 0)
        {
            Debug.LogError($"[TrackDataLoader] Failed to deserialize track: {trackId}");
            return null;
        }

        if (config.gameCellCount != config.cells.Length)
        {
            Debug.LogWarning(
                $"[TrackDataLoader] {trackId}: gameCellCount={config.gameCellCount} but cells.Length={config.cells.Length}. Using cells.Length.");
        }

        Debug.Log($"[TrackDataLoader] Loaded track: {config.trackName} ({config.gameCellCount} cells, {config.laps} laps)");
        return config;
    }

    /// <summary>
    /// List all available track IDs in the Resources/Configs/Tracks/ folder.
    /// </summary>
    public static string[] GetAvailableTrackIds()
    {
        TextAsset[] assets = Resources.LoadAll<TextAsset>(TRACKS_RESOURCES_PATH);
        return assets.Select(a => a.name).ToArray();
    }

    /// <summary>
    /// Convert a TrackConfig's cells into a list of TrackNode objects for runtime use.
    /// Each corner segment gets a unique positive integer cornerId.
    /// Straight/start_finish/pit cells get cornerId=0.
    /// </summary>
    public static List<TrackNode> ConfigToNodes(TrackConfig config)
    {
        var nodes = new List<TrackNode>();
        var cornerIdMap = new Dictionary<string, int>();
        int nextCornerId = 1;

        foreach (var cell in config.cells)
        {
            int cornerId = 0;
            int speedLimit = 99; // no limit for non-corner cells

            if (cell.IsCorner && !string.IsNullOrEmpty(cell.cornerId))
            {
                // Assign a consistent numeric ID for each unique corner segmentId
                if (!cornerIdMap.TryGetValue(cell.cornerId, out cornerId))
                {
                    cornerId = nextCornerId++;
                    cornerIdMap[cell.cornerId] = cornerId;
                }
                speedLimit = cell.cornerLimit;
            }

            var node = new TrackNode(
                cell.index,
                speedLimit,
                cell.name,
                cornerId,
                cell.IsStartFinish
            );

            nodes.Add(node);
        }

        return nodes;
    }

    /// <summary>
    /// Extract world-space positions from a TrackConfig's cells.
    /// Normalized coordinates (0-1) are mapped to world space:
    ///   center (0.5, 0.5) → worldOrigin
    ///   size = worldWidth x worldHeight units
    /// </summary>
    public static Vector2[] ConfigToWorldPositions(TrackConfig config, float worldSize = 30f, Vector2? worldOrigin = null)
    {
        return ConfigToWorldPositions(config, worldSize, worldSize, worldOrigin);
    }

    /// <summary>
    /// Extract world-space positions using independent width and height values.
    /// This preserves non-square source layouts without changing their normalized coordinates.
    /// </summary>
    public static Vector2[] ConfigToWorldPositions(
        TrackConfig config,
        float worldWidth,
        float worldHeight,
        Vector2? worldOrigin = null)
    {
        Vector2 origin = worldOrigin ?? Vector2.zero;

        Vector2[] positions = new Vector2[config.cells.Length];
        for (int i = 0; i < config.cells.Length; i++)
        {
            var cell = config.cells[i];
            float wx = (cell.position.x - 0.5f) * worldWidth + origin.x;
            float wy = (cell.position.y - 0.5f) * worldHeight + origin.y;
            positions[i] = new Vector2(wx, wy);
        }

        return positions;
    }

    /// <summary>
    /// Build corner metadata dictionaries from a TrackConfig.
    /// Returns (cornerSpeedLimits: cornerId→limit, cornerNames: cornerId→name).
    /// </summary>
    public static void BuildCornerMaps(
        TrackConfig config,
        List<TrackNode> nodes,
        out Dictionary<int, int> cornerSpeedLimits,
        out Dictionary<int, string> cornerNames)
    {
        cornerSpeedLimits = new Dictionary<int, int>();
        cornerNames = new Dictionary<int, string>();

        foreach (var node in nodes)
        {
            if (node.cornerId > 0 && !cornerSpeedLimits.ContainsKey(node.cornerId))
            {
                cornerSpeedLimits[node.cornerId] = node.speedLimit;
                cornerNames[node.cornerId] = node.nodeName;
            }
        }
    }
}
