using System.Collections.Generic;

/// <summary>
/// Pure traversal rules for data-driven tracks.
/// </summary>
public static class TrackRules
{
    /// <summary>
    /// Returns the normalized node indices visited by a forward movement path.
    /// The positions remain raw at the call site so a move can cross one or
    /// more lap boundaries without losing event order.
    /// </summary>
    public static List<int> GetCrossedNodeIndices(int nodeCount, int fromPosition, int toPosition)
    {
        var crossed = new List<int>();
        if (nodeCount <= 0 || toPosition <= fromPosition)
            return crossed;

        for (int position = fromPosition + 1; position <= toPosition; position++)
            crossed.Add(NormalizeIndex(position, nodeCount));

        return crossed;
    }

    /// <summary>
    /// Samples all track-owned events from one raw forward path. The raw
    /// target is intentionally retained by the caller so multi-lap movement
    /// cannot lose event order or skip a repeated landmark node.
    /// </summary>
    public static TrackTraversalEvents GetTraversalEvents(
        IReadOnlyList<TrackNode> nodes,
        int fromPosition,
        int toPosition)
    {
        var crossed = GetCrossedNodeIndices(nodes != null ? nodes.Count : 0, fromPosition, toPosition);
        var startFinishNodes = new List<int>();
        var corners = new HashSet<int>();
        bool crossedPitEntry = false;

        foreach (int index in crossed)
        {
            TrackNode node = nodes[index];
            if (node == null)
                continue;

            if (node.isStartFinish)
                startFinishNodes.Add(index);
            if (node.cornerId > 0 && node.isApex)
                corners.Add(node.cornerId);
            if (node.isPitEntry)
                crossedPitEntry = true;
        }

        return new TrackTraversalEvents(crossed, startFinishNodes, corners, crossedPitEntry);
    }

    public static HashSet<int> GetUniqueApexCornersCrossed(
        IReadOnlyList<TrackNode> nodes,
        int fromPosition,
        int toPosition)
    {
        return new HashSet<int>(GetTraversalEvents(nodes, fromPosition, toPosition).UniqueApexCornerIds);
    }

    public static bool CrossesStartFinish(
        IReadOnlyList<TrackNode> nodes,
        int fromPosition,
        int toPosition,
        out int startFinishIndex)
    {
        startFinishIndex = -1;
        IReadOnlyList<int> crossedStartFinishNodes =
            GetTraversalEvents(nodes, fromPosition, toPosition).CrossedStartFinishNodeIndices;
        if (crossedStartFinishNodes.Count == 0)
            return false;

        startFinishIndex = crossedStartFinishNodes[0];
        return true;
    }

    public static int FindStartFinishNodeIndex(IReadOnlyList<TrackNode> nodes)
    {
        if (nodes == null)
            return 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].isStartFinish)
                return i;
        }

        return 0;
    }

    private static int NormalizeIndex(int position, int nodeCount)
    {
        int index = position % nodeCount;
        return index < 0 ? index + nodeCount : index;
    }
}
