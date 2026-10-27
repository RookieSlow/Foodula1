using System.Collections.Generic;

/// <summary>
/// Pure traversal rules for data-driven tracks.
/// </summary>
public static class TrackRules
{
    public static HashSet<int> GetUniqueApexCornersCrossed(
        IReadOnlyList<TrackNode> nodes,
        int fromPosition,
        int toPosition)
    {
        var corners = new HashSet<int>();
        if (nodes == null || nodes.Count == 0 || toPosition <= fromPosition)
            return corners;

        for (int position = fromPosition + 1; position <= toPosition; position++)
        {
            TrackNode node = nodes[NormalizeIndex(position, nodes.Count)];
            if (node.cornerId > 0 && node.isApex)
                corners.Add(node.cornerId);
        }

        return corners;
    }

    /// <summary>New physical apex passes only; a later lap is a new pass of the same corner.</summary>
    public static List<int> GetAdditionalApexCornersCrossed(IReadOnlyList<TrackNode> nodes,
        int originalStart, int originalEnd, int extraStart, int extraEnd)
    {
        var result = new List<int>();
        if (nodes == null || nodes.Count == 0 || extraEnd <= extraStart) return result;
        var seen = new HashSet<(int corner, int segmentStart)>();
        for (int position = originalStart + 1; position <= originalEnd; position++)
        {
            TrackNode node = nodes[NormalizeIndex(position, nodes.Count)];
            if (node.cornerId > 0 && node.isApex)
                seen.Add((node.cornerId, ApexSegmentStart(nodes, position, node.cornerId)));
        }
        for (int position = extraStart + 1; position <= extraEnd; position++)
        {
            TrackNode node = nodes[NormalizeIndex(position, nodes.Count)];
            if (node.cornerId > 0 && node.isApex &&
                seen.Add((node.cornerId, ApexSegmentStart(nodes, position, node.cornerId))))
                result.Add(node.cornerId);
        }
        return result;
    }

    private static int ApexSegmentStart(IReadOnlyList<TrackNode> nodes, int position, int corner)
    {
        int start = position;
        for (int count = 1; count < nodes.Count; count++)
        {
            if (nodes[NormalizeIndex(start - 1, nodes.Count)].cornerId != corner) return start;
            start--;
        }
        // A track made entirely of one corner has no entry boundary; one array
        // cycle is its pass. This also keeps the search bounded for malformed data.
        return position - NormalizeIndex(position, nodes.Count);
    }

    /// <summary>End of a newly added path before the crossing that finishes this racer.</summary>
    public static int EndBeforeFinalFinish(IReadOnlyList<TrackNode> nodes, int from, int to, int lapsRemaining)
    {
        if (lapsRemaining <= 0) return from;
        if (nodes == null || nodes.Count == 0) return to;
        for (int position = from + 1; position <= to; position++)
            if (nodes[NormalizeIndex(position, nodes.Count)].isStartFinish && --lapsRemaining == 0)
                return position - 1;
        return to;
    }

    public static bool CrossesStartFinish(
        IReadOnlyList<TrackNode> nodes,
        int fromPosition,
        int toPosition,
        out int startFinishIndex)
    {
        startFinishIndex = -1;
        if (nodes == null || nodes.Count == 0 || toPosition <= fromPosition)
            return false;

        for (int position = fromPosition + 1; position <= toPosition; position++)
        {
            int index = NormalizeIndex(position, nodes.Count);
            if (!nodes[index].isStartFinish)
                continue;

            startFinishIndex = index;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Counts every start/finish node crossed by a forward movement. This is
    /// used by instant bonus movement that does not traverse visual nodes.
    /// </summary>
    public static int CountStartFinishCrossings(
        IReadOnlyList<TrackNode> nodes,
        int fromPosition,
        int toPosition)
    {
        if (nodes == null || nodes.Count == 0 || toPosition <= fromPosition)
            return 0;

        int crossings = 0;
        for (int position = fromPosition + 1; position <= toPosition; position++)
        {
            if (nodes[NormalizeIndex(position, nodes.Count)].isStartFinish)
                crossings++;
        }

        return crossings;
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
