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
