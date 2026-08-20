using System.Collections.Generic;

/// <summary>
/// Immutable result of sampling one raw forward movement path on a closed
/// track. All event consumers use the same ordered node sequence.
/// </summary>
public sealed class TrackTraversalEvents
{
    private readonly List<int> crossedNodeIndices;
    private readonly List<int> crossedStartFinishNodeIndices;
    private readonly HashSet<int> uniqueApexCornerIds;

    internal TrackTraversalEvents(
        List<int> crossedNodeIndices,
        List<int> crossedStartFinishNodeIndices,
        HashSet<int> uniqueApexCornerIds,
        bool crossedPitEntry)
    {
        this.crossedNodeIndices = crossedNodeIndices ?? new List<int>();
        this.crossedStartFinishNodeIndices =
            crossedStartFinishNodeIndices ?? new List<int>();
        this.uniqueApexCornerIds = uniqueApexCornerIds ?? new HashSet<int>();
        CrossedPitEntry = crossedPitEntry;
    }

    public IReadOnlyList<int> CrossedNodeIndices => crossedNodeIndices;
    public IReadOnlyList<int> CrossedStartFinishNodeIndices => crossedStartFinishNodeIndices;
    public IReadOnlyCollection<int> UniqueApexCornerIds => uniqueApexCornerIds;
    public bool CrossedPitEntry { get; }

    /// <summary>
    /// Returns true when the sampled path included this start/finish node.
    /// The ordered list remains available when a move crosses more than one lap.
    /// </summary>
    public bool CrossedStartFinishAt(int nodeIndex)
    {
        return crossedStartFinishNodeIndices.Contains(nodeIndex);
    }
}
