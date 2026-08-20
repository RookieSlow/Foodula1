using System.Collections.Generic;
using NUnit.Framework;

public class TrackRulesTests
{
    [Test]
    public void GetCrossedNodeIndices_PreservesOrderedNodesAcrossLapWrap()
    {
        List<int> crossed = TrackRules.GetCrossedNodeIndices(4, 2, 6);

        Assert.That(crossed, Is.EqualTo(new[] { 3, 0, 1, 2 }));
    }

    [Test]
    public void GetCrossedNodeIndices_RejectsEmptyOrNonForwardPaths()
    {
        Assert.That(TrackRules.GetCrossedNodeIndices(0, 0, 4), Is.Empty);
        Assert.That(TrackRules.GetCrossedNodeIndices(4, 4, 4), Is.Empty);
        Assert.That(TrackRules.GetCrossedNodeIndices(4, 5, 3), Is.Empty);
    }

    [Test]
    public void GetTraversalEvents_UsesOneOrderedPathForTrackEvents()
    {
        var nodes = new List<TrackNode>
        {
            new TrackNode(0, 99, "Start", isStartFinish: true),
            new TrackNode(1, 4, "Apex", cornerId: 7, isApex: true),
            new TrackNode(2, 99, "Pit Entry", isPitEntry: true),
            new TrackNode(3, 99, "Pit Exit", isPitExit: true)
        };

        TrackTraversalEvents events = TrackRules.GetTraversalEvents(nodes, 2, 10);

        Assert.That(events.CrossedNodeIndices, Is.EqualTo(new[] { 3, 0, 1, 2, 3, 0, 1, 2 }));
        Assert.That(events.CrossedStartFinishNodeIndices, Is.EqualTo(new[] { 0, 0 }));
        Assert.That(events.UniqueApexCornerIds, Is.EquivalentTo(new[] { 7 }));
        Assert.That(events.CrossedPitEntry, Is.True);
        Assert.That(events.CrossedStartFinishAt(0), Is.True);
    }

    [Test]
    public void GetTraversalEvents_ReturnsEmptyEventsForInvalidTrack()
    {
        TrackTraversalEvents events = TrackRules.GetTraversalEvents(null, 0, 4);

        Assert.That(events.CrossedNodeIndices, Is.Empty);
        Assert.That(events.CrossedStartFinishNodeIndices, Is.Empty);
        Assert.That(events.UniqueApexCornerIds, Is.Empty);
        Assert.That(events.CrossedPitEntry, Is.False);
    }

    [Test]
    public void ConfigToNodes_PreservesApexAndStartFinishFlags()
    {
        var config = new TrackConfig
        {
            cells = new[]
            {
                Cell(0, "start_finish", false),
                Cell(1, "corner", false, "corner_a"),
                Cell(2, "corner", true, "corner_a")
            }
        };

        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(config);

        Assert.That(nodes[0].isStartFinish, Is.True);
        Assert.That(nodes[1].isApex, Is.False);
        Assert.That(nodes[2].isApex, Is.True);
        Assert.That(nodes[1].cornerId, Is.EqualTo(nodes[2].cornerId));
    }

    [Test]
    public void LoadConfig_Nordschleife_UsesStandaloneRealLayout()
    {
        TrackConfig config = TrackDataLoader.LoadConfig("nurburgring_24h_endurance");

        Assert.That(config, Is.Not.Null);
        Assert.That(config.trackNameEn, Is.EqualTo("Nürburgring Nordschleife"));
        Assert.That(config.gameCellCount, Is.EqualTo(219));
        Assert.That(config.cells.Length, Is.EqualTo(config.gameCellCount));
        Assert.That(config.realCircuit.lengthMeters, Is.EqualTo(20832f));

        int startFinishCount = 0;
        float distanceSum = 0f;
        foreach (CellData cell in config.cells)
        {
            if (cell.IsStartFinish)
            {
                startFinishCount++;
            }

            Assert.That(cell.segmentId, Is.Not.EqualTo("mercedes_arena"));
            distanceSum += cell.distanceMeters;
        }

        Assert.That(startFinishCount, Is.EqualTo(1));
        Assert.That(distanceSum, Is.EqualTo(config.realCircuit.lengthMeters));
    }

    [Test]
    public void LoadConfig_Indianapolis_UsesProgressiveLaneLimitsAndStartFinishChange()
    {
        TrackConfig config = TrackDataLoader.LoadConfig("indianapolis_burger");

        Assert.That(config, Is.Not.Null);
        Assert.That(config.laneCornerSpeedLimits, Is.EqualTo(new[] { 4, 5, 6, 7 }));
        Assert.That(config.allowStartFinishLaneChange, Is.True);
    }

    [Test]
    public void GetUniqueApexCornersCrossed_IgnoresNonApexCornerCells()
    {
        var nodes = new List<TrackNode>
        {
            Node(0),
            Node(1, 1, false),
            Node(2, 1, true),
            Node(3, 2, false)
        };

        HashSet<int> corners = TrackRules.GetUniqueApexCornersCrossed(nodes, 0, 3);

        Assert.That(corners, Is.EquivalentTo(new[] { 1 }));
    }

    [Test]
    public void GetUniqueApexCornersCrossed_WrapsAndDeduplicatesCornerIds()
    {
        var nodes = new List<TrackNode>
        {
            Node(0, 2, true),
            Node(1),
            Node(2, 2, true),
            Node(3, 3, true)
        };

        HashSet<int> corners = TrackRules.GetUniqueApexCornersCrossed(nodes, 2, 6);

        Assert.That(corners, Is.EquivalentTo(new[] { 2, 3 }));
    }

    [Test]
    public void AllTrackConfigs_HaveExactlyOneApexPerCorner()
    {
        foreach (string trackId in TrackDataLoader.GetAvailableTrackIds())
        {
            TrackConfig config = TrackDataLoader.LoadConfig(trackId);
            var apexCounts = new Dictionary<string, int>();

            foreach (CellData cell in config.cells)
            {
                if (!cell.IsCorner)
                {
                    continue;
                }

                Assert.That(cell.cornerId, Is.Not.Null.And.Not.Empty, $"{trackId} has a corner cell without cornerId");
                if (!apexCounts.ContainsKey(cell.cornerId))
                {
                    apexCounts[cell.cornerId] = 0;
                }

                if (cell.isApex)
                {
                    apexCounts[cell.cornerId]++;
                }
            }

            foreach (KeyValuePair<string, int> pair in apexCounts)
            {
                Assert.That(pair.Value, Is.EqualTo(1), $"{trackId}/{pair.Key} must have exactly one apex");
            }
        }
    }

    [Test]
    public void CrossesStartFinish_UsesRuntimeFlagAtArbitraryIndex()
    {
        var nodes = new List<TrackNode>
        {
            Node(0),
            new TrackNode(1, 99, isStartFinish: true),
            Node(2)
        };

        bool crossed = TrackRules.CrossesStartFinish(nodes, 2, 4, out int index);

        Assert.That(crossed, Is.True);
        Assert.That(index, Is.EqualTo(1));
        Assert.That(TrackRules.FindStartFinishNodeIndex(nodes), Is.EqualTo(1));
    }

    private static CellData Cell(int index, string type, bool isApex, string cornerId = null)
    {
        return new CellData
        {
            index = index,
            type = type,
            name = type,
            cornerId = cornerId,
            cornerLimit = 3,
            isApex = isApex,
            position = new Vector2Data()
        };
    }

    private static TrackNode Node(int index, int cornerId = 0, bool isApex = false)
    {
        return new TrackNode(index, 3, cornerId: cornerId, isApex: isApex);
    }
}
