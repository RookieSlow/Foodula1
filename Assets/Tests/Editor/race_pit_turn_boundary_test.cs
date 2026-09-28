using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Composes the real entry/exit adapters and session reset on inactive objects.
/// This protects synchronous turn boundaries, not the GameLoop coroutine or UI.
/// </summary>
public class RacePitTurnBoundaryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private GameConfigSO config;
    private MVPGameManager manager;
    private RaceSession session;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Pit turn boundary regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.trackManager = host.AddComponent<TrackManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.minGear = 2;
        config.pitExitMoveBonus = 1;
        config.enableTechTree = false;
        manager.config = config;
        session = new RaceSession(new SystemRandomSource(1));
        SetField("session", session);
        SetField("tutorialPitRuleNodes", RacePitStopExecutionTests.Track());
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(config);
    }

    private void SetField(string name, object value) =>
        typeof(MVPGameManager).GetField(name, Private).SetValue(manager, value);

    private void Invoke(string name, params object[] args) =>
        typeof(MVPGameManager).GetMethod(name, Private).Invoke(manager, args);

    [TestCase(TeamId.CN, false)]
    [TestCase(TeamId.CN, true)]
    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.US, true)]
    [TestCase(TeamId.UK, false)]
    [TestCase(TeamId.UK, true)]
    [TestCase(TeamId.DE, false)]
    [TestCase(TeamId.DE, true)]
    [TestCase(TeamId.IT, false)]
    [TestCase(TeamId.IT, true)]
    [TestCase(TeamId.JP, false)]
    [TestCase(TeamId.JP, true)]
    public void ReservationCrossingStopAndNextPlayableTurnKeepTheirBoundaries(TeamId team, bool ai)
    {
        var player = RacePitStopExecutionTests.Player(team);
        player.isAI = ai;
        player.position = 12; // Already settled movement: entry must not teleport it.
        player.pitStopScheduled = false;
        player.skipNextTurn = false;
        session.Players.Add(player);
        var handBefore = new List<CardData>(player.deck.Hand);
        Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));

        Invoke("RegisterPitEntryCrossing", player, 8, 9);
        Assert.IsTrue(player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Invoke("RegisterPitEntryCrossing", player, 8, 12);
        Assert.IsFalse(player.pitStopRequested);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.AreEqual(12, player.position);
        Assert.AreEqual(8, player.totalMovementThisTurn);
        CollectionAssert.AreEqual(handBefore, player.deck.Hand);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);

        session.BeginTurn(player);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(0, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(RaceTurnStartAction.ExecuteScheduledPitStop, RaceTurnRules.GetStartAction(player));
        Invoke("ExecuteScheduledPitStop", player);
        Assert.AreEqual(0, player.position);
        Assert.AreEqual(team == TeamId.CN ? 1 : 2, player.gear);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(0, player.chinaConsecutiveGearCount);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(2, player.lap);

        // A1 retains exclusion after the stop has consumed its scheduling flag.
        var skipped = new HashSet<PlayerState> { player };
        var order = new List<PlayerState> { player };
        Assert.IsTrue(RaceTurnRules.IsInactive(player, skipped));
        player.totalMovementThisTurn = 7;
        player.cornerTotalThisTurn = 7;
        Invoke("ComputeMovements", order, skipped);
        Invoke("ResolveSlipstreamsAtTurnEnd", order, skipped);
        Assert.AreEqual(0, player.totalMovementThisTurn);
        Assert.AreEqual(0, player.cornerTotalThisTurn);
        Assert.AreEqual(0, player.position);

        session.BeginTurn(player);
        Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));
        Assert.IsFalse(RaceTurnRules.IsInactive(player, new HashSet<PlayerState>()));
        Assert.AreEqual(0, player.positionAtTurnStart);
        Assert.AreEqual(player.gear, player.selectedGearThisTurn);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
    }

    [TestCase(false, false, false)]
    [TestCase(true, true, false)]
    [TestCase(true, false, true)]
    public void CrossingWithoutEligibleReservationOnlyReopensChoice(bool requested, bool finished, bool blown)
    {
        var player = RacePitStopExecutionTests.Player();
        player.pitStopScheduled = false;
        player.skipNextTurn = false;
        player.pitStopRequested = requested;
        player.hasFinished = finished;
        player.isBlown = blown;
        Invoke("RegisterPitEntryCrossing", player, 8, 12);
        Assert.IsFalse(player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(10, player.position);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        Assert.AreEqual(4, player.deck.CountPermanentHeatOutsideEngine());
    }

    [TestCase("missing-track")]
    [TestCase("missing-pit")]
    [TestCase("disabled")]
    [TestCase("before-entry")]
    [TestCase("reverse")]
    [TestCase("standing-on-entry")]
    public void EntryGuardKeepsReservationAndPaidHeatUntouched(string reason)
    {
        var player = RacePitStopExecutionTests.Player();
        player.pitStopScheduled = false;
        if (reason == "missing-track") manager.trackManager = null;
        if (reason == "missing-pit") SetField("tutorialPitRuleNodes", RacePitStopExecutionTests.Track(false));
        if (reason == "disabled")
        {
            SetField("tutorialPitRuleNodes", null);
            config.enablePitLane = false;
        }
        int start = reason == "standing-on-entry" ? 10 : 8;
        int end = reason == "before-entry" ? 9 : reason == "reverse" ? 7 : 12;
        Invoke("RegisterPitEntryCrossing", player, start, end);
        Assert.IsTrue(player.pitStopRequested);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsTrue(player.skipNextTurn);
        Assert.AreEqual(10, player.position);
        Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
    }
}
