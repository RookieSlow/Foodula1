using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class ThunderstormGridRulesTests
{
    [TestCase(2)]
    [TestCase(4)]
    public void TwelveRacersHaveDistinctSlotsAndWrapBehindStart(int laneCount)
    {
        var slots = new HashSet<string>();
        for (int index = 0; index < 12; index++)
        {
            Assert.IsTrue(TrackPresentationRules.TryGetThunderstormGridSlot(
                0, 12, index, 0, 77, laneCount, out int position, out int lane));
            Assert.AreEqual((77 - index / laneCount) % 77, position);
            Assert.AreEqual(index % laneCount, lane);
            Assert.IsTrue(slots.Add(position + ":" + lane));
        }
        Assert.AreEqual(12, slots.Count);
    }

    [TestCase(1, 12, 0, 77)]
    [TestCase(10, 12, 0, 77)]
    [TestCase(0, 4, 0, 77)]
    [TestCase(0, 0, 0, 77)]
    [TestCase(0, 12, -1, 77)]
    [TestCase(0, 12, 0, 0)]
    [TestCase(0, 12, 0, -1)]
    public void IneligibleContextLeavesNoGridOverride(int turn, int roster, int index, int nodes)
    {
        Assert.IsFalse(TrackPresentationRules.TryGetThunderstormGridSlot(
            turn, roster, index, 5, nodes, 2, out int position, out int lane));
        Assert.AreEqual(0, position);
        Assert.AreEqual(0, lane);
    }

    [Test]
    public void NonZeroStartAndLastRowUseAuthoredStartIndex()
    {
        Assert.IsTrue(TrackPresentationRules.TryGetThunderstormGridSlot(
            0, 12, 11, 3, 60, 2, out int position, out int lane));
        Assert.AreEqual(58, position);
        Assert.AreEqual(1, lane);
    }

    [TestCase(0)]
    [TestCase(-3)]
    public void InvalidLaneCountRetainsSingleLaneFallback(int laneCount)
    {
        Assert.IsTrue(TrackPresentationRules.TryGetThunderstormGridSlot(
            0, 12, 11, 0, 60, laneCount, out int position, out int lane));
        Assert.AreEqual(49, position);
        Assert.AreEqual(0, lane);
    }
}

/// <summary>Verifies the real manager supplies roster, track and turn state to the display-only rule.</summary>
public class ThunderstormGridManagerAdapterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private TrackManager track;
    private RaceSession session;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Thunderstorm grid adapter regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        track = host.AddComponent<TrackManager>();
        manager.trackManager = track;
        session = new RaceSession(new SystemRandomSource(1));
        SetField("session", session);
        SetField("freeRaceRoster", new FreeRaceRosterEntry[12]);

        var nodes = new List<TrackNode>();
        for (int i = 0; i < 60; i++)
            nodes.Add(new TrackNode(i, 99, isStartFinish: i == 3));
        typeof(TrackManager).GetField("nodes", Private).SetValue(track, nodes);
        typeof(TrackManager).GetField("laneOffsets", Private).SetValue(track, new[] { -0.2f, 0.2f });
        for (int i = 0; i < 12; i++)
            session.Players.Add(new PlayerState("driver " + i, i != 0, 3, 1));
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

    private void SetField(string name, object value) =>
        typeof(MVPGameManager).GetField(name, Private).SetValue(manager, value);

    private bool TrySlot(PlayerState player, out int position, out int lane)
    {
        object[] args = { player, 0, 0 };
        bool result = (bool)typeof(MVPGameManager)
            .GetMethod("TryGetThunderstormGridSlot", Private).Invoke(manager, args);
        position = (int)args[1];
        lane = (int)args[2];
        return result;
    }

    [Test]
    public void AllTwelveRealParticipantsReceiveDistinctDisplaySlotsWithoutMovingRulePositions()
    {
        var displaySlots = new HashSet<string>();
        foreach (PlayerState player in session.Players)
        {
            int index = session.Players.IndexOf(player);
            Assert.IsTrue(TrySlot(player, out int position, out int lane));
            Assert.AreEqual((3 - index / 2 + 60) % 60, position);
            Assert.AreEqual(index % 2, lane);
            Assert.IsTrue(displaySlots.Add(position + ":" + lane));
            Assert.AreEqual(3, player.position);
        }
        Assert.AreEqual(12, displaySlots.Count);
    }

    [Test]
    public void FirstTurnRetiresTheDisplayOverrideWithoutChangingRulePosition()
    {
        PlayerState player = session.Players[11];
        Assert.IsTrue(TrySlot(player, out _, out _));
        SetField("raceTurnNumber", 1);
        Assert.IsFalse(TrySlot(player, out int position, out int lane));
        Assert.AreEqual(0, position);
        Assert.AreEqual(0, lane);
        Assert.AreEqual(3, player.position);
    }

    [TestCase(0)]
    [TestCase(4)]
    public void MissingOrOrdinaryRosterHasNoGridOverride(int rosterSize)
    {
        SetField("freeRaceRoster", rosterSize == 0 ? null : new FreeRaceRosterEntry[rosterSize]);
        Assert.IsFalse(TrySlot(session.Players[0], out int position, out int lane));
        Assert.AreEqual(0, position);
        Assert.AreEqual(0, lane);
    }

    [Test]
    public void UnregisteredParticipantCannotReceiveAGridSlot()
    {
        var outsider = new PlayerState("outsider", true, 3, 1);
        Assert.IsFalse(TrySlot(outsider, out int position, out int lane));
        Assert.AreEqual(0, position);
        Assert.AreEqual(0, lane);
    }

    [Test]
    public void MissingSessionOrTrackHasNoGridOverride()
    {
        PlayerState player = session.Players[0];
        SetField("session", null);
        Assert.IsFalse(TrySlot(player, out _, out _));
        SetField("session", session);
        manager.trackManager = null;
        Assert.IsFalse(TrySlot(player, out _, out _));
    }
}
