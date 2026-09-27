using NUnit.Framework;

public class TechTreeProfileCodecTests
{
    private TechTreeDatabase database;

    [SetUp]
    public void SetUp()
    {
        database = TechTreeDatabaseFactory.CreateDefault();
    }

    [Test]
    public void EncodeDecode_PreservesProfileAndOmitsRaceOnlyState()
    {
        var original = new TechTreeState(TeamId.US, 1350);
        original.unlockedNodeIds.Add("common-l1-heat-coating");
        original.unlockedNodeIds.Add("us-l1-drive-thru");
        original.activeNodeIds.Add("common-l1-heat-coating");
        original.landmark1PassCount = 3;
        original.landmark1UltUsed = true;
        original.fishAndChipsUsed = true;

        string encoded = TechTreeProfileCodec.Encode(original);
        TechTreeState decoded = TechTreeProfileCodec.Decode(encoded, TeamId.US, database);

        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded.teamId, Is.EqualTo(TeamId.US));
        Assert.That(decoded.rpBalance, Is.EqualTo(1350));
        Assert.That(decoded.unlockedNodeIds, Is.EquivalentTo(original.unlockedNodeIds));
        Assert.That(decoded.activeNodeIds, Is.EquivalentTo(original.activeNodeIds));
        Assert.That(decoded.landmark1PassCount, Is.Zero);
        Assert.That(decoded.landmark1UltUsed, Is.False);
        Assert.That(decoded.fishAndChipsUsed, Is.False);
    }

    [Test]
    public void Decode_FiltersUnknownNodesAndActiveNodesThatAreNotUnlocked()
    {
        const string serialized = "{\"teamId\":0,\"rpBalance\":900,\"unlocked\":[\"common-l1-heat-coating\",\"removed-node\"],\"active\":[\"common-l1-heat-coating\",\"common-l1-track-memory\",\"removed-node\"]}";

        TechTreeState decoded = TechTreeProfileCodec.Decode(serialized, TeamId.UK, database);

        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded.teamId, Is.EqualTo(TeamId.UK), "Storage key team remains authoritative.");
        Assert.That(decoded.rpBalance, Is.EqualTo(900));
        Assert.That(decoded.unlockedNodeIds, Is.EquivalentTo(new[] { "common-l1-heat-coating" }));
        Assert.That(decoded.activeNodeIds, Is.EquivalentTo(new[] { "common-l1-heat-coating" }));
    }

    [TestCase(null)]
    [TestCase("")]
    public void Decode_RejectsMissingPayload(string serialized)
    {
        Assert.That(TechTreeProfileCodec.Decode(serialized, TeamId.CN, database), Is.Null);
    }

    [Test]
    public void Decode_DefaultsMissingNodeCollectionsToEmptySets()
    {
        const string serialized = "{\"teamId\":0,\"rpBalance\":900}";

        TechTreeState decoded = TechTreeProfileCodec.Decode(serialized, TeamId.CN, database);

        Assert.That(decoded, Is.Not.Null);
        Assert.That(decoded.rpBalance, Is.EqualTo(900));
        Assert.That(decoded.unlockedNodeIds, Is.Empty);
        Assert.That(decoded.activeNodeIds, Is.Empty);
    }
}
