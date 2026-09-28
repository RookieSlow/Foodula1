using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class PitApproachChoiceRulesTests
{
    [TestCase(0, true, 10)]
    [TestCase(9, true, 1)]
    [TestCase(10, false, 0)]
    [TestCase(19, false, 11)]
    [TestCase(20, true, 10)]
    [TestCase(-11, true, 1)]
    public void WindowUsesNormalizedDistanceAndExcludesEntry(int position, bool offered, int distance)
    {
        var player = new PlayerState("driver", false, position, 2) { skipNextTurn = true };
        Assert.AreEqual(offered, PitLaneRules.TryGetApproachChoiceDistance(
            player, RacePitStopExecutionTests.Track(), true, out int actual));
        Assert.AreEqual(distance, actual);
        Assert.IsTrue(player.skipNextTurn); // Existing offer gate does not inspect recovery skips.
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
    }

    [TestCase("null")]
    [TestCase("disabled")]
    [TestCase("no-pit")]
    [TestCase("blown")]
    [TestCase("finished")]
    [TestCase("resolved")]
    [TestCase("requested")]
    [TestCase("scheduled")]
    public void IneligibleOfferDoesNotConsumeAnyState(string reason)
    {
        var player = new PlayerState("driver", false, 9, 2)
        {
            isBlown = reason == "blown", hasFinished = reason == "finished",
            pitChoiceResolvedThisLap = reason == "resolved",
            pitStopRequested = reason == "requested", pitStopScheduled = reason == "scheduled"
        };
        Assert.IsFalse(PitLaneRules.TryGetApproachChoiceDistance(reason == "null" ? null : player,
            RacePitStopExecutionTests.Track(reason != "no-pit"), reason != "disabled", out int distance));
        Assert.AreEqual(-1, distance);
        Assert.AreEqual(reason == "resolved", player.pitChoiceResolvedThisLap);
        Assert.AreEqual(reason == "requested", player.pitStopRequested);
        Assert.AreEqual(reason == "scheduled", player.pitStopScheduled);
        Assert.AreEqual(9, player.position);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RecordingChoiceOnlyChangesTheTwoReservationFields(bool enter)
    {
        var player = RacePitStopExecutionTests.Player();
        PitLaneRules.RecordApproachChoice(player, enter);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(enter, player.pitStopRequested);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.IsTrue(player.skipNextTurn);
        Assert.AreEqual(10, player.position);
        Assert.AreEqual(4, player.gear);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
        PitLaneRules.RecordApproachChoice(null, enter);
    }
}

/// <summary>Actual callback and synchronous coroutine paths; no scene startup or GameLoop.</summary>
public class PitApproachChoiceBoundaryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private RaceInputState input;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Pit choice regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.trackManager = host.AddComponent<TrackManager>();
        SetField("tutorialPitRuleNodes", RacePitStopExecutionTests.Track());
        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
        player = new PlayerState("driver", false, 9, 2);
        SetField("pitWaitingPlayer", player);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    private void SetField(string name, object value) =>
        typeof(MVPGameManager).GetField(name, Private).SetValue(manager, value);

    private object Invoke(string name, params object[] args) =>
        typeof(MVPGameManager).GetMethod(name, Private).Invoke(manager, args);

    [TestCase("closed")]
    [TestCase("gear")]
    [TestCase("cards")]
    [TestCase("discard")]
    [TestCase("lane")]
    public void LatePitClickLeavesOtherInputGateAndParticipantUntouched(string gate)
    {
        if (gate == "gear") input.BeginGearSelection(3);
        if (gate == "cards") input.BeginCardSelection();
        if (gate == "discard") input.BeginDiscardSelection();
        if (gate == "lane") input.BeginLaneChangeSelection();
        manager.ChoosePit(true);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.IsFalse(player.pitStopRequested);
        Assert.AreEqual(gate == "gear", input.WaitingForGear);
        Assert.AreEqual(gate == "cards", input.WaitingForCards);
        Assert.AreEqual(gate == "discard", input.WaitingForDiscard);
        Assert.AreEqual(gate == "lane", input.WaitingForLaneChange);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AcceptedClickClosesGateAndDuplicateClickCannotReverseChoice(bool enter)
    {
        input.BeginPitChoice();
        manager.ChoosePit(enter);
        manager.ChoosePit(!enter);
        Assert.IsFalse(input.WaitingForPitChoice);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(enter, player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.AreEqual(9, player.position);
    }

    [Test]
    public void MissingWaitingPlayerStillClosesThePitGate()
    {
        SetField("pitWaitingPlayer", null);
        input.BeginPitChoice();
        manager.ChoosePit(true);
        Assert.IsFalse(input.WaitingForPitChoice);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
    }

    [Test]
    public void MissingPanelDefaultsToContinueWithoutOpeningAnInputGate()
    {
        var routine = (IEnumerator)Invoke("WaitForPitChoice", 1);
        Assert.IsFalse(routine.MoveNext());
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.IsFalse(player.pitStopRequested);
        Assert.IsFalse(input.WaitingForPitChoice);
    }

    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    public void ActualAIOfferRetainsSixtyPercentThreshold(int heatCards, bool enter)
    {
        player.isAI = true;
        var speeds = new CardData[5 - heatCards];
        for (int i = 0; i < speeds.Length; i++) speeds[i] = new CardData(CardType.Speed, 1);
        player.deck.InitializeExactOrder(speeds, new HeatPool(6));
        player.deck.DrawToHand(speeds.Length);
        player.deck.DrawHeatFromPool(heatCards, HeatPaymentDestination.Hand);
        var routine = (IEnumerator)Invoke("ResolvePitApproachChoice", player);
        Assert.IsFalse(routine.MoveNext());
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(enter, player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.AreEqual(9, player.position);
        Assert.IsFalse(input.WaitingForPitChoice);
        Assert.IsFalse(((IEnumerator)Invoke("ResolvePitApproachChoice", player)).MoveNext());
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PassingWithoutReservationReopensTheNextLapOffer(bool ai)
    {
        player.isAI = ai;
        if (ai) Assert.IsFalse(((IEnumerator)Invoke("ResolvePitApproachChoice", player)).MoveNext());
        else { input.BeginPitChoice(); manager.ChoosePit(false); }
        Assert.IsFalse(PitLaneRules.TryGetApproachChoiceDistance(player,
            RacePitStopExecutionTests.Track(), true, out _));
        Invoke("RegisterPitEntryCrossing", player, 9, 12);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.IsFalse(player.pitStopScheduled);
        player.position = 12;
        Assert.IsFalse(PitLaneRules.TryGetApproachChoiceDistance(player,
            RacePitStopExecutionTests.Track(), true, out _));
        player.position = 29; // Next lap, one cell before entry after normalization.
        Assert.IsTrue(PitLaneRules.TryGetApproachChoiceDistance(player,
            RacePitStopExecutionTests.Track(), true, out int distance));
        Assert.AreEqual(1, distance);
    }
}
