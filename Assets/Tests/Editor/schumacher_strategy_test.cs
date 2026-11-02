using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>D03 actual preparation/whole fixed-cost transactions and independent owner-input windows; no UI claims.</summary>
public class SchumacherStrategyTests
{
    private RaceSession race;
    private PlayerState owner, other;
    private CardData cost, equal;
    private int turn;
    private SchumacherStrategyRuntime Strategy => owner.driverSkill.Strategy;
    private DriverSkillActivationContext Context => new DriverSkillActivationContext(true, owner.lap + 1, 99,
        owner.deck?.heatPool?.remaining ?? 0, 20, 0);

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(71));
        owner = NewOwner(7); other = NewOwner(7); other.name = "Other D03";
        race.Players.Add(owner); race.Players.Add(other); Begin();
        cost = owner.deck.Hand[0]; equal = owner.deck.Hand[1];
    }

    private static PlayerState NewOwner(int level)
    {
        var p = new PlayerState("Schumacher", false, 0, 1) { teamId = TeamId.DE, driverId = "de_michael_schumacher", lap = 1 };
        p.driverSkill.Initialize(p.DriverProfile, level, true);
        p.deck.InitializeExactOrder(Enumerable.Range(0, 16).Select(_ => new CardData(CardType.Speed, 2)).ToArray(), new HeatPool(20));
        p.deck.DrawToHand(8); return p;
    }
    private void Begin() { turn++; owner.driverSkill.BeginGlobalTurn(); race.BeginTurn(owner); Assert.That(Strategy.BeginInput(race, owner), Is.True); }
    private SchumacherStrategyActivationPlan Plan(int gear = 1, int request = 7)
    {
        Assert.That(SchumacherStrategyActivationPlan.TryCreate(race, owner, turn, gear, request, Context,
            out var plan, out string reason), Is.True, reason); return plan;
    }
    private bool Commit(SchumacherStrategyActivationPlan plan, CardData card = null, int gear = 1, int request = 7)
        => plan.TryCommit(race, owner, turn, gear, request, Context, card, out _);
    private void Prepare()
    { var plan = Plan(); Assert.That(Commit(plan, plan.Choices[0]), Is.True); }
    private void ExecutionInput() { Prepare(); Begin(); Assert.That(Strategy.IsExecutionAvailable, Is.True); }

    [TestCase(3, 2)] [TestCase(5, 3)] [TestCase(7, 3)]
    public void PreparationAndTwoLaterExecutionsPayExactSourcesAndOneRaceUse(int level, int bonus)
    {
        owner.driverSkill.Initialize(owner.DriverProfile, level, true); Begin();
        int uses = owner.driverSkill.UsesRemaining;
        var prep = Plan(2); CardData chosen = prep.Choices[0], sameValue = prep.Choices[1];
        Assert.That(prep.IsExecution, Is.False); Assert.That(prep.MovementBonus, Is.Zero);
        Assert.That(Commit(prep, chosen, 2), Is.True);
        Assert.That(owner.deck.ContainsInHand(chosen), Is.False);
        Assert.That(owner.deck.DiscardPile.Count(c => ReferenceEquals(c, chosen)), Is.EqualTo(1));
        Assert.That(owner.deck.ContainsInHand(sameValue), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20)); Assert.That(prep.PaidHeat, Is.Null);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        Assert.That(Strategy.ManualPath, Is.True); Assert.That(Strategy.MovementBonusThisInput, Is.Zero);
        var paid = new List<CardData>();
        for (int segment = 1; segment <= 2; segment++)
        {
            Begin(); Assert.That(Strategy.ExecutionNumber, Is.EqualTo(segment));
            var plan = Plan(3); Assert.That(plan.Choices, Is.Empty); Assert.That(plan.MovementBonus, Is.EqualTo(bonus));
            Assert.That(Commit(plan, null, 3), Is.True);
            Assert.That(Strategy.MovementBonusThisInput, Is.EqualTo(bonus));
            Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20 - segment));
            Assert.That(owner.deck.ContainsInHand(plan.PaidHeat), Is.True);
            Assert.That(owner.deck.DiscardPile.Contains(plan.PaidHeat), Is.False);
            Assert.That(owner.heatPaidCardsThisTurn.Single(), Is.SameAs(plan.PaidHeat));
            Assert.That(plan.PaidHeat.isTemp, Is.False); paid.Add(plan.PaidHeat);
            Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
            Assert.That(Commit(plan, null, 3), Is.False);
        }
        Assert.That(paid[0], Is.Not.SameAs(paid[1]));
        Begin(); Assert.That(Strategy.IsExecutionAvailable, Is.False);
        Assert.That(Strategy.MovementBonusThisInput, Is.Zero); Assert.That(Strategy.ManualPath, Is.True);
        Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(20));
    }

    [Test]
    public void PreviewCancellationAndUnknownEqualValueCostCannotSpendPreparation()
    {
        var p = Plan(); p.Cancel(); Assert.That(Commit(p, cost), Is.False);
        var q = Plan(); Assert.That(Commit(q, new CardData(CardType.Speed, cost.value)), Is.False);
        Assert.That(owner.deck.HandCount, Is.EqualTo(8)); Assert.That(owner.deck.DiscardPile, Is.Empty);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
        Assert.That(Strategy.ManualPath, Is.False);
    }

    [TestCase("turn")] [TestCase("request")] [TestCase("gear")] [TestCase("lap")] [TestCase("position")]
    [TestCase("driver")] [TestCase("pool")] [TestCase("deck")] [TestCase("engine")] [TestCase("capacity")]
    [TestCase("hand-value")] [TestCase("hand-temp")] [TestCase("hand-trick")] [TestCase("draw-value")]
    [TestCase("discard")] [TestCase("receipts")] [TestCase("initialization")] [TestCase("session")]
    public void EveryStalePreparationSnapshotRejectsWithoutAdditionalResourceMutation(string change)
    {
        var plan = Plan(); CardDeck originalDeck = owner.deck;
        if (change == "turn") turn++;
        if (change == "lap") owner.lap++;
        if (change == "position") owner.position++;
        if (change == "driver") owner.driverId = "de_sebastian_vettel";
        if (change == "pool") owner.deck.heatPool = new HeatPool(20);
        if (change == "deck") owner.deck = originalDeck.CreatePresentationCopy();
        if (change == "engine") owner.deck.heatPool.remaining--;
        if (change == "capacity") owner.deck.SetRegionalCapacityBonus(1);
        if (change == "hand-value") cost.value++;
        if (change == "hand-temp") cost.isTemp = true;
        if (change == "hand-trick") cost.trickId = "changed";
        if (change == "draw-value") owner.deck.DrawPile[0].value++;
        if (change == "discard") { owner.deck.DrawHeatFromPool(1, HeatPaymentDestination.Discard); owner.deck.heatPool.remaining++; }
        if (change == "receipts") owner.heatPaidCardsThisTurn.Add(new CardData(CardType.Heat, 0));
        if (change == "initialization") owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        if (change == "session") race = new RaceSession(new SystemRandomSource(8)) { Players = race.Players };
        int heat = originalDeck.heatPool.remaining, hand = originalDeck.HandCount, discard = originalDeck.DiscardPileCount;
        int uses = owner.driverSkill.UsesRemaining;
        Assert.That(Commit(plan, cost, change == "gear" ? 2 : 1, change == "request" ? 8 : 7), Is.False);
        Assert.That(originalDeck.heatPool.remaining, Is.EqualTo(heat)); Assert.That(originalDeck.HandCount, Is.EqualTo(hand));
        Assert.That(originalDeck.DiscardPileCount, Is.EqualTo(discard)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [TestCase(0)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
    public void PreparationRequiresLowGearWithoutSpending(int gear)
    {
        Assert.That(SchumacherStrategyActivationPlan.TryCreate(race, owner, turn, gear, 7, Context, out _, out _), Is.False);
        Assert.That(owner.deck.DiscardPile, Is.Empty); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [Test]
    public void PreparationCanDiscardWithNoEngineHeatButNeedsRemainingOrdinarySpeedCards()
    {
        owner.deck.heatPool.remaining = 0; var p = Plan(); Assert.That(Commit(p, cost), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.Zero); Assert.That(owner.deck.ContainsInHand(cost), Is.False);
        var fresh = NewOwner(7); race.Players.Add(fresh); owner = fresh; Begin();
        foreach (CardData c in owner.deck.Hand.Skip(1).ToArray()) owner.deck.RemoveFromHand(new List<CardData> { c });
        Assert.That(SchumacherStrategyActivationPlan.TryCreate(race, owner, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.deck.HandCount, Is.EqualTo(1));
    }

    [TestCase("ai")] [TestCase("disabled")] [TestCase("tier")] [TestCase("foreign")] [TestCase("pit")]
    [TestCase("skip")] [TestCase("kanto")] [TestCase("finish")] [TestCase("blown")]
    public void BlockedOwnerInputsNeitherOpenNorAdvanceOrDiscard(string blocked)
    {
        if (blocked == "ai") owner.isAI = true;
        if (blocked == "disabled") owner.driverSkill.Initialize(owner.DriverProfile, 7, false);
        if (blocked == "tier") owner.driverSkill.Initialize(owner.DriverProfile, 2, true);
        if (blocked == "foreign") race.Players.Remove(owner);
        if (blocked == "pit") owner.pitStopScheduled = true;
        if (blocked == "skip") owner.skipNextTurn = true;
        if (blocked == "kanto") owner.kantoOdenSkipThisTurn = true;
        if (blocked == "finish") owner.hasFinished = true;
        if (blocked == "blown") owner.isBlown = true;
        int input = Strategy.InputsStarted;
        Assert.That(Strategy.BeginInput(race, owner), Is.False);
        Assert.That(Strategy.InputsStarted, Is.EqualTo(input));
        Assert.That(SchumacherStrategyActivationPlan.TryCreate(race, owner, turn, 1, 7, Context, out _, out _), Is.False);
        Assert.That(owner.deck.HandCount, Is.EqualTo(8)); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20));
    }

    [Test]
    public void SameInputAndOtherCarsOrGlobalRoundsCannotCreateExtraExecution()
    {
        Prepare(); int input = Strategy.InputsStarted;
        Assert.That(Strategy.BeginInput(race, owner), Is.False);
        Assert.That(SchumacherStrategyActivationPlan.CanPrepare(race, owner, 3, Context, out _), Is.False);
        race.BeginTurn(other); Assert.That(other.driverSkill.Strategy.BeginInput(race, other), Is.True);
        Assert.That(Strategy.InputsStarted, Is.EqualTo(input));
        Strategy.BeginGlobalTurn(); Strategy.BeginGlobalTurn(); Assert.That(Strategy.InputsStarted, Is.EqualTo(input));
        Assert.That(Strategy.BeginInput(race, owner), Is.True); Assert.That(Strategy.ExecutionNumber, Is.EqualTo(1));
    }

    [TestCase(1)] [TestCase(2)] [TestCase(0)] [TestCase(5)]
    public void LowOrInvalidExecutionChoiceLosesRestWithoutHeatOrUseRefund(int gear)
    {
        ExecutionInput(); int uses = owner.driverSkill.UsesRemaining;
        Assert.That(SchumacherStrategyActivationPlan.TryCreate(race, owner, turn, gear, 7, Context, out _, out _), Is.False);
        Assert.That(Strategy.IsExecutionAvailable, Is.False); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses)); Assert.That(Strategy.ManualPath, Is.True);
        Begin(); Assert.That(Strategy.IsExecutionAvailable, Is.False);
    }

    [TestCase("heat")] [TestCase("cards")]
    public void InsufficientExecutionCostCannotPartiallyPayOrKeepRemainingPlan(string missing)
    {
        ExecutionInput();
        if (missing == "heat") owner.deck.heatPool.remaining = 0;
        else foreach (CardData c in owner.deck.Hand.Skip(2).ToArray()) owner.deck.RemoveFromHand(new List<CardData> { c });
        int heat = owner.deck.heatPool.remaining, uses = owner.driverSkill.UsesRemaining;
        Assert.That(SchumacherStrategyActivationPlan.TryCreate(race, owner, turn, 3, 7, Context, out _, out _), Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat)); Assert.That(owner.heatPaidCardsThisTurn, Is.Empty);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses)); Assert.That(Strategy.IsExecutionAvailable, Is.False);
    }

    [Test]
    public void SupersededAndStaleCancelCannotCloseNewExecutionButExplicitCancelDoes()
    {
        ExecutionInput(); var old = Plan(3); var latest = Plan(3, 8); old.Cancel();
        Assert.That(Strategy.IsExecutionAvailable, Is.True); latest.Cancel(false);
        Assert.That(Strategy.IsExecutionAvailable, Is.True); var next = Plan(3, 9); next.Cancel();
        Assert.That(Strategy.IsExecutionAvailable, Is.False); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(1));
    }

    [Test]
    public void GenericDiscountBreadAndBurstDoNotMakeExecutionFreeOrReplaceItsActualReceipt()
    {
        ExecutionInput(); owner.trickState.schwarzbrotActive = true; owner.trickState.schwarzbrotRemaining = 2;
        owner.techState = new TechTreeState(TeamId.DE); owner.techState.activeNodeIds.Add("de-l3-grill-spezial");
        owner.techState.germanBurst.BeginTurn(8);
        Assert.That(race.TryDeclareGermanBurst(owner, true, 8), Is.True);
        var plan = Plan(3); Assert.That(Commit(plan, null, 3), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19)); Assert.That(owner.trickState.schwarzbrotRemaining, Is.EqualTo(2));
        Assert.That(owner.techState.germanBurst.Locked, Is.True);
        Assert.That(owner.techState.germanBurst.Receipts.Contains(plan.PaidHeat), Is.True);
        Assert.That(owner.deck.ContainsInHand(plan.PaidHeat), Is.True);
    }

    [Test]
    public void PrepAndExecutionGearLocksCannotBeBypassedAfterCostCommit()
    {
        Prepare(); Assert.That(Strategy.IsGearAllowed(1) && Strategy.IsGearAllowed(2), Is.True);
        Assert.That(Strategy.IsGearAllowed(3), Is.False); Assert.That(Strategy.SealGear(3), Is.False);
        Assert.That(owner.deck.DiscardPile.Contains(cost), Is.True); Assert.That(Strategy.ManualPath, Is.True);
        Begin(); Assert.That(Strategy.IsExecutionAvailable, Is.False);
    }

    [Test]
    public void UnpaidExecutionGearConfirmationDeclinesButDoesNotCharge()
    {
        ExecutionInput(); Assert.That(Strategy.SealGear(4), Is.True);
        Assert.That(Strategy.IsExecutionAvailable, Is.False); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20));
        Begin(); Assert.That(Strategy.IsExecutionAvailable, Is.False);
    }

    [TestCase("short")] [TestCase("gear")] [TestCase("skip")] [TestCase("duplicate")]
    public void InvalidFormalMovementInputForfeitsCurrentBonusAndFutureWithoutRefundingActualPayment(string invalid)
    {
        ExecutionInput(); var plan = Plan(3); Assert.That(Commit(plan, null, 3), Is.True); owner.gear = 3;
        CardData[] cards = owner.deck.Hand.Where(c => c.IsSpeed).Take(3).ToArray();
        Assert.That(CardPlayRules.CommitSpeedCards(owner, cards, 3), Is.EqualTo(SpeedCardCommitResult.Success));
        if (invalid == "short") owner.playedSpeedCardsThisTurn.RemoveAt(0);
        if (invalid == "duplicate") owner.playedSpeedCardsThisTurn[2] = owner.playedSpeedCardsThisTurn[0];
        if (invalid == "gear") owner.gear = 2;
        if (invalid == "skip") owner.skipNextTurn = true;
        Assert.That(Strategy.ValidateMovementInput(race, owner, 3), Is.False);
        Assert.That(Strategy.MovementBonusThisInput, Is.Zero); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
        Assert.That(owner.heatPaidCardsThisTurn.Single(), Is.SameAs(plan.PaidHeat));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(1));
    }

    [Test]
    public void NormalCloseRetainsOnlyCommittedFutureAndInterruptKeepsMigrationWithoutRefund()
    {
        Prepare(); Strategy.CloseInput(); Begin(); Assert.That(Strategy.IsExecutionAvailable, Is.True);
        Strategy.CloseInput(); Begin(); Assert.That(Strategy.IsExecutionAvailable, Is.False);
        var plan = Plan(); Assert.That(Commit(plan, plan.Choices[0]), Is.True);
        int uses = owner.driverSkill.UsesRemaining, heat = owner.deck.heatPool.remaining;
        Strategy.Interrupt(); Assert.That(Strategy.ManualPath, Is.True); Assert.That(Strategy.HasSequence, Is.True);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses)); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat));
        owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        Assert.That(owner.driverSkill.Strategy.ManualPath, Is.False); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [Test]
    public void PresentationCopyRebindAndInterruptionCannotChangeRealSequenceOrResources()
    {
        ExecutionInput(); var plan = Plan(3); Assert.That(Commit(plan, null, 3), Is.True); owner.gear = 3;
        var copy = new PlayerState("forecast", false, owner.position, owner.gear) { driverId = owner.driverId, teamId = owner.teamId,
            driverSkill = owner.driverSkill.CreatePresentationCopy(), deck = owner.deck.CreatePresentationCopy(), lap = owner.lap };
        Assert.That(copy.driverSkill.Strategy.RebindPresentationCopy(race, owner, copy), Is.True);
        copy.driverSkill.Strategy.Interrupt();
        Assert.That(Strategy.MovementBonusThisInput, Is.EqualTo(3)); Assert.That(Strategy.ManualPath, Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19)); Assert.That(copy.deck.heatPool, Is.Not.SameAs(owner.deck.heatPool));
    }
}
