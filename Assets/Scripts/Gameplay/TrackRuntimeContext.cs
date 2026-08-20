using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Immutable-at-the-boundary snapshot of the loaded track.
/// TrackManager builds this once after loading JSON (or the fallback layout),
/// then exposes the snapshot to gameplay and presentation adapters without
/// making them depend on the loader's mutable staging state.
/// </summary>
public sealed class TrackRuntimeContext
{
    private readonly List<TrackNode> nodes;
    private readonly Vector2[] worldPathCoordinates;
    private readonly float[] laneOffsets;
    private readonly Dictionary<int, int> cornerSpeedLimits;
    private readonly Dictionary<int, string> cornerNames;
    private readonly string[] weatherPool;
    private readonly int[] laneCornerSpeedLimits;
    private readonly bool allowsStartFinishLaneChange;
    private readonly string trackName;
    private readonly string country;
    private readonly string defaultWeather;

    public TrackRuntimeContext(
        string trackId,
        TrackConfig trackConfig,
        IReadOnlyList<TrackNode> trackNodes,
        Vector2[] pathCoordinates,
        float[] offsets,
        IReadOnlyDictionary<int, int> speedLimits,
        IReadOnlyDictionary<int, string> names,
        int configuredTotalLaps)
    {
        TrackId = trackId ?? string.Empty;
        nodes = CopyNodes(trackNodes);
        worldPathCoordinates = pathCoordinates != null
            ? (Vector2[])pathCoordinates.Clone()
            : new Vector2[0];
        laneOffsets = offsets != null
            ? (float[])offsets.Clone()
            : new float[0];
        cornerSpeedLimits = CopyDictionary(speedLimits);
        cornerNames = CopyDictionary(names);
        laneCornerSpeedLimits = trackConfig != null && trackConfig.laneCornerSpeedLimits != null
            ? (int[])trackConfig.laneCornerSpeedLimits.Clone()
            : new int[0];
        weatherPool = trackConfig != null && trackConfig.weatherPool != null
            ? (string[])trackConfig.weatherPool.Clone()
            : new string[0];
        trackName = trackConfig != null ? trackConfig.trackName : TrackId;
        country = trackConfig != null ? trackConfig.country : string.Empty;
        defaultWeather = trackConfig != null ? trackConfig.defaultWeather : string.Empty;
        allowsStartFinishLaneChange = trackConfig != null
            ? trackConfig.allowStartFinishLaneChange
            : TrackPresentationRules.AllowsStartFinishLaneChange(TrackId);
        TotalLaps = trackConfig != null && trackConfig.laps > 0
            ? trackConfig.laps
            : Mathf.Max(1, configuredTotalLaps);
    }

    public string TrackId { get; }
    public int TotalLaps { get; }
    public string TrackName => trackName;
    public string Country => country;
    public string DefaultWeather => defaultWeather;
    /// <summary>Returns a copy so session state cannot mutate the track snapshot.</summary>
    public string[] WeatherPool => (string[])weatherPool.Clone();
    public IReadOnlyList<TrackNode> Nodes => nodes;
    public IReadOnlyList<Vector2> WorldPathCoordinates => worldPathCoordinates;
    public IReadOnlyList<float> LaneOffsets => laneOffsets;
    public int TotalNodes => nodes.Count;
    public int LaneCount => laneOffsets.Length > 0 ? laneOffsets.Length : 1;
    public int StartFinishNodeIndex => TrackRules.FindStartFinishNodeIndex(nodes);

    public bool AllowsStartFinishLaneChange => allowsStartFinishLaneChange;

    /// <summary>Ordered, normalized nodes visited by a raw forward movement path.</summary>
    public List<int> GetCrossedNodeIndices(int fromPosition, int toPosition)
    {
        return TrackRules.GetCrossedNodeIndices(TotalNodes, fromPosition, toPosition);
    }

    /// <summary>Returns one shared event snapshot for a raw forward movement path.</summary>
    public TrackTraversalEvents GetTraversalEvents(int fromPosition, int toPosition)
    {
        return TrackRules.GetTraversalEvents(nodes, fromPosition, toPosition);
    }

    public HashSet<int> GetUniqueCornersCrossed(int fromPosition, int toPosition)
    {
        return TrackRules.GetUniqueApexCornersCrossed(nodes, fromPosition, toPosition);
    }

    public bool CrossesStartFinish(int fromPosition, int toPosition, out int startFinishIndex)
    {
        return TrackRules.CrossesStartFinish(nodes, fromPosition, toPosition, out startFinishIndex);
    }

    public TrackNode GetNode(int index)
    {
        if (nodes.Count == 0)
            return null;

        int normalized = NormalizeIndex(index, nodes.Count);
        return nodes[normalized];
    }

    public bool IsInCorner(int position)
    {
        TrackNode node = GetNode(position);
        return node != null && node.cornerId > 0;
    }

    public int GetApexNodeIndex(int cornerId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].cornerId == cornerId && nodes[i].isApex)
                return i;
        }

        return -1;
    }

    public int GetCornerSpeedLimit(int cornerId)
    {
        int limit;
        return cornerSpeedLimits.TryGetValue(cornerId, out limit) ? limit : 99;
    }

    public int GetCornerSpeedLimit(int cornerId, int laneIndex)
    {
        int baseLimit = GetCornerSpeedLimit(cornerId);
        if (baseLimit >= 99)
            return baseLimit;

        if (laneCornerSpeedLimits.Length >= LaneCount)
        {
            int innerToOuterIndex = TrackPresentationRules.GetLaneRankFromInside(
                TrackId,
                laneIndex);
            return laneCornerSpeedLimits[innerToOuterIndex];
        }

        return TrackPresentationRules.GetLaneAdjustedCornerSpeedLimit(
            TrackId,
            baseLimit,
            laneIndex);
    }

    public string GetCornerName(int cornerId)
    {
        string name;
        return cornerNames.TryGetValue(cornerId, out name) ? name : "Unknown";
    }

    public Vector3 GetNodePosition(int index)
    {
        if (worldPathCoordinates.Length == 0)
            return Vector3.zero;

        int normalized = NormalizeIndex(index, worldPathCoordinates.Length);
        Vector2 position = worldPathCoordinates[normalized];
        return new Vector3(position.x, position.y, 0f);
    }

    public Vector3 GetNodePosition(int index, int laneIndex)
    {
        if (worldPathCoordinates.Length == 0)
            return Vector3.zero;

        int normalized = NormalizeIndex(index, worldPathCoordinates.Length);
        int safeLane = Mathf.Clamp(laneIndex, 0, LaneCount - 1);
        float laneOffset = laneOffsets.Length > 0 ? laneOffsets[safeLane] : 0f;
        Vector2 position = worldPathCoordinates[normalized] + GetPathNormal(normalized) * laneOffset;
        return new Vector3(position.x, position.y, 0f);
    }

    public int GetDefaultLaneIndex(bool isAi)
    {
        if (LaneCount <= 1)
            return 0;
        if (!TrackPresentationRules.IsIndianapolis(TrackId))
            return TrackPresentationRules.GetInnerLaneIndex(TrackId);

        int leftMiddle = (LaneCount - 1) / 2;
        return isAi ? Mathf.Min(LaneCount - 1, leftMiddle + 1) : leftMiddle;
    }

    public int GetLaneTowardsInside(int laneIndex)
    {
        return LaneCount <= 1 ? 0 : Mathf.Max(0, laneIndex - 1);
    }

    public int GetLaneTowardsOutside(int laneIndex)
    {
        return LaneCount <= 1 ? 0 : Mathf.Min(LaneCount - 1, laneIndex + 1);
    }

    private Vector2 GetPathNormal(int index)
    {
        if (worldPathCoordinates.Length < 2)
            return Vector2.zero;

        int previous = NormalizeIndex(index - 1, worldPathCoordinates.Length);
        int next = NormalizeIndex(index + 1, worldPathCoordinates.Length);
        Vector2 tangent = (worldPathCoordinates[next] - worldPathCoordinates[previous]).normalized;
        return new Vector2(-tangent.y, tangent.x);
    }

    private static int NormalizeIndex(int index, int count)
    {
        int normalized = index % count;
        return normalized < 0 ? normalized + count : normalized;
    }

    private static List<TrackNode> CopyNodes(IReadOnlyList<TrackNode> source)
    {
        var copy = new List<TrackNode>();
        if (source == null)
            return copy;

        foreach (TrackNode node in source)
        {
            if (node == null)
            {
                copy.Add(null);
                continue;
            }

            copy.Add(new TrackNode(
                node.nodeIndex,
                node.speedLimit,
                node.nodeName,
                node.cornerId,
                node.isStartFinish,
                node.isApex,
                node.isPitEntry,
                node.isPitExit));
        }

        return copy;
    }

    private static Dictionary<TKey, TValue> CopyDictionary<TKey, TValue>(
        IReadOnlyDictionary<TKey, TValue> source)
    {
        var copy = new Dictionary<TKey, TValue>();
        if (source == null)
            return copy;

        foreach (KeyValuePair<TKey, TValue> pair in source)
            copy[pair.Key] = pair.Value;
        return copy;
    }
}
