using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>Production fixed payments and owner-clock continuations; no player UI acceptance.</summary>
public class MansellChargePlanTests
{
    private RaceSession race;
    private PlayerState owner, target;
    private CardData cost, equal;
    private int turn;
    private const int Nodes = 60;
    private DriverSkillActivationContext Context => new DriverSkillActivationContext(true, owner.lap + 1, 99,
        owner.deck?.heatPool?.remaining ?? 0, 6, 0);

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(2));
        owner = new PlayerState("Mansell", false, 0, 1) { teamId = TeamId.UK, driverId = "uk_nigel_mansell" };
        target = new PlayerState("target", true, 6, 1) { driverId = "de_michael_schumacher" };
        race.Players.Add(owner); race.Players.Add(target);
        owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        cost = new CardData(CardType.Speed, 3); equal = new CardData(CardType.Speed, 3);
        owner.deck.InitializeExactOrder(new[] { cost, equal, new CardData(CardType.Speed, 1) }, new HeatPool(6));
        owner.deck.DrawToHand(3); Begin();
    }

    private void Begin(int finish = 0)
    { turn++; race.BeginTurn(owner); race.BeginDriverSkillInput(owner, Nodes, finish); }

    private MansellChargeActivationPlan Plan(int gear = 1, int version = 7, int finish = 0)
    {
        Assert.That(race.TryPrepareMansellCharge(owner, Nodes, finish, turn, gear, version, Context,
            out var plan, out string reason), Is.True, reason);
        return plan;
    }

    private bool Commit(MansellChargeActivationPlan plan, CardData speed = null, int gear = 1, int version = 7, int finish = 0)
        => plan.TryCommit(race, owner, Nodes, finish, turn, gear, version, Context, speed, out _);

    private void First() { var plan = Plan(); Assert.That(Commit(plan, plan.Choices[0]), Is.True); }

    [TestCase(3, 1, 2)] [TestCase(5, 2, 3)] [TestCase(7, 2, 3)]
    public void TwoLegsPayExactInstancesAndOnlyOneRaceUse(int level, int firstBonus, int secondBonus)
    {
        owner.driverSkill.Initialize(owner.DriverProfile, level, true); Begin();
        int uses = owner.driverSkill.UsesRemaining;
        var first = Plan();
        Assert.That(first.IsSecondLeg, Is.False); Assert.That(first.MovementBonus, Is.EqualTo(firstBonus));
        Assert.That(Commit(first, equal), Is.True);
        Assert.That(owner.deck.ContainsInHand(cost), Is.True); Assert.That(owner.deck.ContainsInHand(equal), Is.False);
        Assert.That(owner.deck.DiscardPile.Contains(equal), Is.True);
        Assert.That(owner.deck.DiscardPile.Contains(first.PaidHeat), Is.True);
        Assert.That(first.PaidHeat.IsHeat && !first.PaidHeat.isTemp, Is.True);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        Assert.That(DriverSkillRules.GetMovementBonus(owner.driverSkill, false), Is.EqualTo(firstBonus));
        Assert.That(DriverSkillRules.GetOvertakeBonus(owner.driverSkill, 9), Is.Zero, "No legacy overtake supplement.");
        Assert.That(Commit(first, cost), Is.False);

        target.position = 4; Begin();
        var second = Plan(); Assert.That(second.IsSecondLeg, Is.True); Assert.That(second.Choices, Is.Empty);
        int hand = owner.deck.HandCount;
        Assert.That(Commit(second), Is.True);
        Assert.That(owner.deck.HandCount, Is.EqualTo(hand));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(4));
        Assert.That(owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(2));
        Assert.That(DriverSkillRules.GetMovementBonus(owner.driverSkill, true), Is.EqualTo(secondBonus));
        Assert.That(Commit(second), Is.False);
        Begin(); Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        Assert.That(DriverSkillRules.GetMovementBonus(owner.driverSkill, false), Is.Zero);
    }

    [TestCase(1, true)] [TestCase(6, true)] [TestCase(7, false)] [TestCase(0, false)]
    public void FirstLegDistanceIsPositiveAndIncludesSix(int gap, bool expected)
    {
        target.position = gap;
        Assert.That(MansellChargeActivationPlan.CanPrepare(race, owner, Nodes, 0, 1, Context, out _), Is.EqualTo(expected));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
    }

    [Test]
    public void QueriesCancelAndSameValueForeignCardNeverPay()
    {
        for (int i = 0; i < 3; i++)
            Assert.That(MansellChargeActivationPlan.CanPrepare(race, owner, Nodes, 0, 1, Context, out _), Is.True);
        var plan = Plan(); Assert.That(Commit(plan, new CardData(CardType.Speed, 3)), Is.False);
        var canceled = Plan(); canceled.Cancel(); Assert.That(Commit(canceled, cost), Is.False);
        Assert.That(owner.deck.HandCount, Is.EqualTo(3)); Assert.That(owner.deck.DiscardPile, Is.Empty);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
    }

    [TestCase("turn")] [TestCase("gear")] [TestCase("version")] [TestCase("position")]
    [TestCase("target")] [TestCase("pool")] [TestCase("engine")] [TestCase("hand-value")]
    [TestCase("deck")] [TestCase("session")] [TestCase("initialization")]
    public void StaleFirstSnapshotsRejectAllFees(string change)
    {
        var plan = Plan(); var originalDeck = owner.deck; int originalUses = owner.driverSkill.UsesRemaining;
        if (change == "turn") turn++;
        if (change == "position") owner.position++;
        if (change == "target") target.position++;
        if (change == "pool") owner.deck.heatPool = new HeatPool(6);
        if (change == "engine") owner.deck.DrawHeatFromPool(1);
        if (change == "hand-value") cost.value++;
        if (change == "deck") owner.deck = new CardDeck();
        if (change == "session") race = new RaceSession(new SystemRandomSource(2)) { Players = race.Players };
        if (change == "initialization") owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        int heat = originalDeck.heatPool.remaining, hand = originalDeck.HandCount, discard = originalDeck.DiscardPileCount;
        Assert.That(Commit(plan, cost, change == "gear" ? 2 : 1, change == "version" ? 8 : 7), Is.False);
        Assert.That(originalDeck.heatPool.remaining, Is.EqualTo(heat)); Assert.That(originalDeck.HandCount, Is.EqualTo(hand));
        Assert.That(originalDeck.DiscardPileCount, Is.EqualTo(discard));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(originalUses));
    }

    [TestCase(0)] [TestCase(1)]
    public void FirstLegInsufficientHeatOrPostDiscardSpeedDoesNotPartiallyCommit(int available)
    {
        owner.deck.heatPool.remaining = available;
        if (available == 1) owner.deck.RemoveFromHand(new List<CardData> { equal, owner.deck.Hand.Last() });
        Assert.That(race.TryPrepareMansellCharge(owner, Nodes, 0, turn, 1, 1, Context, out _, out _), Is.False);
        Assert.That(owner.deck.ContainsInHand(cost), Is.True); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(available));
    }

    [TestCase("same")] [TestCase("wider")] [TestCase("passed")] [TestCase("tied")]
    [TestCase("nearer")] [TestCase("replaced")] [TestCase("finished")] [TestCase("retired")]
    public void SecondObservationRequiresSameLiveNearestTargetAndStrictNetPursuit(string change)
    {
        First(); target.position = 4;
        if (change == "same") target.position = 6;
        if (change == "wider") target.position = 7;
        if (change == "passed") { owner.position = 10; target.position = 9; }
        if (change == "tied") target.position = owner.position;
        if (change == "nearer") race.Players.Add(new PlayerState("other", true, 2, 1));
        if (change == "replaced")
        { race.Players.Remove(target); race.Players.Add(new PlayerState("replacement", true, 4, 1) { driverId = target.driverId }); }
        if (change == "finished") target.hasFinished = true;
        if (change == "retired") target.isBlown = true;
        Begin();
        Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        Assert.That(race.TryPrepareMansellCharge(owner, Nodes, 0, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(5)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [TestCase("skip")] [TestCase("pit")] [TestCase("terminal")]
    public void InterruptedNextInputNeverDefersOrRefundsFirstLeg(string change)
    {
        First(); target.position = 4;
        if (change == "skip") owner.skipNextTurn = true;
        if (change == "pit") owner.pitStopScheduled = true;
        if (change == "terminal") owner.hasFinished = true;
        Begin(); Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        owner.skipNextTurn = owner.pitStopScheduled = owner.hasFinished = false;
        Begin(); Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2)); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(5));
    }

    [Test]
    public void ZeroUsesAfterLastFirstLegStillAllowsItsPaidContinuation()
    {
        for (int i = 0; i < 3; i++)
        {
            target.position = 6;
            if (i > 0) { owner.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 1) }); Begin(); Begin(); }
            First(); target.position = 4; Begin();
            Assert.That(Commit(Plan()), Is.True);
        }
        Assert.That(owner.driverSkill.UsesRemaining, Is.Zero);
        Assert.That(owner.deck.heatPool.remaining, Is.Zero);
    }

    [Test]
    public void NextInputWithoutHeatExpiresBeforeAnyLaterA2Recovery()
    {
        First(); owner.deck.DrawHeatFromPool(5); target.position = 4; Begin();
        Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        owner.deck.CoolHeat(1);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(1));
        Assert.That(race.TryPrepareMansellCharge(owner, Nodes, 0, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [TestCase("cancel")] [TestCase("version")] [TestCase("engine")]
    public void DeclinedOrStaleSecondWindowExpiresWithoutChargingOrNewFirstLeg(string change)
    {
        First(); target.position = 4; Begin(); var second = Plan();
        if (change == "engine") owner.deck.DrawHeatFromPool(1);
        int heat = owner.deck.heatPool.remaining;
        if (change == "cancel") second.Cancel();
        Assert.That(Commit(second, version: change == "version" ? 8 : 7), Is.False);
        Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat));
        Assert.That(race.TryPrepareMansellCharge(owner, Nodes, 0, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [Test]
    public void UnusedSecondInputCannotDelayItsDiscountIntoAnotherInput()
    {
        First(); target.position = 4; Begin(); Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.True);
        Begin(); Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        var next = Plan(); Assert.That(next.IsSecondLeg, Is.False);
        Assert.That(next.Choices, Is.Not.Empty);
    }

    [Test]
    public void LegacyPathCannotInterleavePaidFirstOrSecondButRemainsUnmigratedOutsideSequence()
    {
        First(); Assert.That(owner.driverSkill.TryActivate(owner.DriverProfile, Context, out _), Is.False);
        target.position = 4; Begin();
        Assert.That(owner.driverSkill.TryActivate(owner.DriverProfile, Context, out _), Is.False);
        Assert.That(Commit(Plan()), Is.True);
        Assert.That(DriverSkillRules.GetOvertakeBonus(owner.driverSkill, 3), Is.Zero);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [Test]
    public void ForecastRuntimeCannotClearRealPendingSequenceOrSpendRealResources()
    {
        First(); var copy = owner.driverSkill.CreatePresentationCopy();
        Assert.That(copy.Charge, Is.Not.SameAs(owner.driverSkill.Charge)); copy.Charge.Interrupt();
        Assert.That(owner.driverSkill.Charge.HasSequence, Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(5));
        target.position = 4; Begin(); Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.True);
    }

    [TestCase(true)] [TestCase(false)]
    public void DisabledTutorialAndOrdinaryAiDoNotEnterPaidSequence(bool ai)
    {
        owner.isAI = ai; owner.driverSkill.Initialize(owner.DriverProfile, 7, ai); Begin();
        Assert.That(race.TryPrepareMansellCharge(owner, Nodes, 0, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6));
    }

    [Test]
    public void FixedCostPrimitiveDoesNotChangeOrdinaryPartialHeatPayment()
    {
        owner.deck.heatPool.remaining = 0;
        Assert.That(owner.deck.TryCommitMansellChargeCost(cost, false, out _), Is.False);
        Assert.That(owner.deck.ContainsInHand(cost), Is.True);
        owner.deck.heatPool.remaining = 1;
        Assert.That(owner.deck.DrawHeatFromPool(3), Is.EqualTo(1), "Ordinary penalties still pay partially.");
        Assert.That(owner.deck.heatPool.remaining, Is.Zero);
    }

    [Test]
    public void NewChargeMovementOnlyExtendsTailAndKeepsCommittedCornerSpeed()
    {
        First(); owner.totalMovementThisTurn = 6; owner.cornerTotalThisTurn = 4;
        var tail = RaceMovementRules.GetHunterActiveCornerTail(owner, 10);
        Assert.That(tail.start, Is.EqualTo(14)); Assert.That(tail.end, Is.EqualTo(16));
        Assert.That(owner.cornerTotalThisTurn, Is.EqualTo(4));
        Assert.That(RaceMovementRules.ComputeCardAndDriverMovementBonus(owner, false), Is.EqualTo(2));
    }

    [TestCase("cancel")] [TestCase("stale-commit")]
    public void ReissuedSecondPlanCannotBeClosedOrChargedByItsOldDelegate(string action)
    {
        First(); target.position = 4; Begin();
        var old = Plan(version: 7); var latest = Plan(version: 8);
        if (action == "cancel") old.Cancel();
        else Assert.That(Commit(old, version: 8), Is.False);
        Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.True);
        Assert.That(Commit(latest, version: 8), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(4));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
        Assert.That(Commit(old), Is.False); Assert.That(Commit(latest, version: 8), Is.False);
    }

    [Test]
    public void LatestSecondCancellationClosesWindowAndCannotBeReopenedByOldPlan()
    {
        First(); target.position = 4; Begin(); var old = Plan(); var latest = Plan(version: 8);
        latest.Cancel();
        Assert.That(Commit(old), Is.False);
        Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(5));
    }

    [Test]
    public void ReissuedFirstPlanInvalidatesOldCommitWithoutSpendingAnything()
    {
        var old = Plan(); var latest = Plan(version: 8);
        Assert.That(Commit(old, cost), Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(Commit(latest, equal, version: 8), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(5));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [TestCase("pool")] [TestCase("deck")] [TestCase("target-id")]
    [TestCase("owner-id")] [TestCase("target-position")]
    public void ChangedFirstLegSourceAfterSecondObservationCannotMintFreshContinuation(string change)
    {
        First(); target.position = 4; Begin();
        var originalDeck = owner.deck;
        if (change == "pool") owner.deck.heatPool = new HeatPool(6);
        if (change == "deck")
        {
            owner.deck = new CardDeck();
            owner.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(6));
            owner.deck.DrawToHand(1);
        }
        if (change == "target-id") target.driverId = "de_sebastian_vettel";
        if (change == "owner-id") owner.driverId = "uk_hunter_hart";
        if (change == "target-position") target.position = 5;
        int heat = owner.deck.heatPool.remaining, hand = owner.deck.HandCount;
        Assert.That(race.TryPrepareMansellCharge(owner, Nodes, 0, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat)); Assert.That(owner.deck.HandCount, Is.EqualTo(hand));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
        Assert.That(originalDeck.CountHeatInDiscardPile(), Is.EqualTo(1));
    }

    [TestCase(false)] [TestCase(true)]
    public void RealManagerDisableClearsFirstAndSecondPlansWithoutRefund(bool second)
    {
        if (second) { First(); target.position = 4; Begin(); }
        var plan = Plan(); int uses = owner.driverSkill.UsesRemaining, heat = owner.deck.heatPool.remaining;
        var host = new UnityEngine.GameObject("Inactive Mansell lifecycle fixture"); host.SetActive(false);
        try
        {
            var manager = host.AddComponent<MVPGameManager>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(MVPGameManager).GetField("session", flags).SetValue(manager, race);
            typeof(MVPGameManager).GetMethod("OnDisable", flags).Invoke(manager, null);
            Assert.That(Commit(plan, second ? null : cost), Is.False);
            Assert.That(owner.driverSkill.Charge.HasSequence, Is.False);
            Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat));
            Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }

    [Test]
    public void LegacyActivationAlreadyInThisInputCannotAlsoUsePaidEntry()
    {
        Assert.That(owner.driverSkill.TryActivate(owner.DriverProfile, Context, out _), Is.True);
        Assert.That(race.TryPrepareMansellCharge(owner, Nodes, 0, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [Test]
    public void WrongSessionFailedPreparationCannotCloseRealSecondWindow()
    {
        First(); target.position = 4; Begin();
        var foreign = new RaceSession(new SystemRandomSource(77));
        Assert.That(foreign.TryPrepareMansellCharge(owner, Nodes, 0, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.True);
        Assert.That(Commit(Plan()), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(4));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [TestCase(false, false)] [TestCase(true, false)]
    [TestCase(false, true)] [TestCase(true, true)]
    public void RealManagerDisableAfterPaidCommitCannotFallBackToLegacyBonus(bool second, bool globalCleanup)
    {
        First();
        if (second) { target.position = 4; Begin(); Assert.That(Commit(Plan()), Is.True); }
        int uses = owner.driverSkill.UsesRemaining, heat = owner.deck.heatPool.remaining;
        if (globalCleanup) owner.driverSkill.BeginGlobalTurn();
        var host = new UnityEngine.GameObject("Paid Mansell lifecycle fixture"); host.SetActive(false);
        try
        {
            var manager = host.AddComponent<MVPGameManager>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(MVPGameManager).GetField("session", flags).SetValue(manager, race);
            typeof(MVPGameManager).GetMethod("OnDisable", flags).Invoke(manager, null);
            Assert.That(owner.driverSkill.Charge.HasSequence, Is.False);
            Assert.That(owner.driverSkill.IsActive, Is.False);
            Assert.That(DriverSkillRules.GetMovementBonus(owner.driverSkill, false), Is.Zero);
            Assert.That(DriverSkillRules.GetOvertakeBonus(owner.driverSkill, 9), Is.Zero);
            Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat));
            Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses));
        }
        finally { UnityEngine.Object.DestroyImmediate(host); }
    }
}
