using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Real manager lane lookup on inactive objects; does not prove rendered Play Mode spacing.</summary>
public sealed class RaceVisualLaneAdapterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private TrackManager track;
    private RaceSession session;
    private List<int> assignedLanes;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Visual lane adapter regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        track = host.AddComponent<TrackManager>();
        manager.trackManager = track;
        session = new RaceSession(new SystemRandomSource(1));
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
        assignedLanes = (List<int>)typeof(MVPGameManager)
            .GetField("laneIndices", Private).GetValue(manager);
        SetTrack("silverstone_afternoon_tea", 2);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [Test]
    public void OrdinaryTrackUsesOutsideLaneForLaterCarsInSameLapAndCell()
    {
        var first = AddPlayer("first", false, 8, 1);
        var second = AddPlayer("second", true, 8, 0);
        var third = AddPlayer("third", true, 8, 0);

        Assert.That(manager.GetLaneIndexForPlayer(first), Is.Zero);
        Assert.That(manager.GetLaneIndexForPlayer(second), Is.EqualTo(1));
        Assert.That(manager.GetLaneIndexForPlayer(third), Is.EqualTo(1));
        Assert.That(assignedLanes, Is.EqualTo(new[] { 1, 0, 0 }));
        Assert.That(first.position, Is.EqualTo(8));
        Assert.That(second.position, Is.EqualTo(8));
    }

    [TestCase(9, 1, false, false)]
    [TestCase(8, 2, false, false)]
    [TestCase(8, 1, true, false)]
    [TestCase(8, 1, false, true)]
    public void OrdinaryTrackDoesNotTreatOtherCellLapOrInactiveLeaderAsParallel(
        int candidateCell, int candidateLap, bool leaderFinished, bool leaderBlown)
    {
        var leader = AddPlayer("leader", false, 8, 1);
        leader.lap = 1;
        leader.hasFinished = leaderFinished;
        leader.isBlown = leaderBlown;
        var candidate = AddPlayer("candidate", true, candidateCell, 1);
        candidate.lap = candidateLap;

        Assert.That(manager.GetLaneIndexForPlayer(candidate), Is.Zero);
        Assert.That(assignedLanes[1], Is.EqualTo(1));
    }

    [TestCase(-2, 0)]
    [TestCase(2, 2)]
    [TestCase(7, 3)]
    public void IndianapolisUsesClampedAssignedLaneEvenWhenCarsShareCell(int assigned, int expected)
    {
        SetTrack(TrackPresentationRules.IndianapolisTrackId, 4);
        AddPlayer("leader", false, 8, 0);
        var candidate = AddPlayer("candidate", true, 8, assigned);

        Assert.That(manager.GetLaneIndexForPlayer(candidate), Is.EqualTo(expected));
        Assert.That(assignedLanes[1], Is.EqualTo(assigned));
        Assert.That(candidate.position, Is.EqualTo(8));
    }

    [TestCase("silverstone_afternoon_tea", false, 0)]
    [TestCase("silverstone_afternoon_tea", true, 0)]
    [TestCase(TrackPresentationRules.IndianapolisTrackId, false, 1)]
    [TestCase(TrackPresentationRules.IndianapolisTrackId, true, 2)]
    public void MissingAssignedLaneUsesTrackDefaultRatherThanParallelLane(
        string trackId, bool isAi, int expected)
    {
        SetTrack(trackId, trackId == TrackPresentationRules.IndianapolisTrackId ? 4 : 2);
        AddPlayer("leader", false, 8, 0);
        var candidate = AddPlayer("candidate", isAi, 8, 0);
        assignedLanes.Clear();

        Assert.That(manager.GetLaneIndexForPlayer(candidate), Is.EqualTo(expected));
        Assert.That(assignedLanes, Is.Empty);
    }

    [Test]
    public void UnregisteredOrMissingPlayerUsesFallbackWithoutMutatingSession()
    {
        var registered = AddPlayer("registered", false, 8, 0);
        var unknownAi = new PlayerState("unknown", true, 8, 2);

        Assert.That(manager.GetLaneIndexForPlayer(unknownAi), Is.Zero);
        Assert.That(manager.GetLaneIndexForPlayer(null), Is.Zero);
        Assert.That(session.Players, Is.EqualTo(new[] { registered }));
        Assert.That(assignedLanes, Is.EqualTo(new[] { 0 }));
    }

    [Test]
    public void MissingTrackReturnsZeroEvenForRegisteredAi()
    {
        var ai = AddPlayer("ai", true, 8, 1);
        manager.trackManager = null;

        Assert.That(manager.GetLaneIndexForPlayer(ai), Is.Zero);
        Assert.That(assignedLanes, Is.EqualTo(new[] { 1 }));
    }

    private PlayerState AddPlayer(string name, bool isAi, int position, int lane)
    {
        var player = new PlayerState(name, isAi, position, 2);
        session.Players.Add(player);
        assignedLanes.Add(lane);
        return player;
    }

    private void SetTrack(string trackId, int laneCount)
    {
        typeof(TrackManager).GetField("laneOffsets", Private).SetValue(track, new float[laneCount]);
        typeof(TrackManager).GetField("<LoadedTrackConfig>k__BackingField", Private)
            .SetValue(track, new TrackConfig { trackId = trackId });
    }
}
