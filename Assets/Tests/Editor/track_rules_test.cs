using System.Collections.Generic;
using NUnit.Framework;

public class TrackRulesTests
{
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
