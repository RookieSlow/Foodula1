using System.Collections.Generic;
using NUnit.Framework;

public class HunterSkillActivationPlanTests
{
    private RaceSession session;
    private PlayerState owner, target;
    private CardData first, second;

    [SetUp]
    public void SetUp()
    {
        session = new RaceSession(new SystemRandomSource(7));
        owner = new PlayerState("Hart", false, 10, 2) { teamId = TeamId.UK, driverId = "uk_hunter_hart" };
        target = new PlayerState("Target", true, 13, 2) { teamId = TeamId.DE };
        session.Players.Add(owner);
        session.Players.Add(target);
        owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        owner.driverSkill.BeginGlobalTurn();
        session.BeginDriverSkillInput(owner);
        first = new CardData(CardType.Speed, 3);
        second = new CardData(CardType.Speed, 3);
        owner.deck.InitializeExactOrder(new[] { first, second }, new HeatPool(6));
        owner.deck.DrawToHand(2);
    }

    private DriverSkillActivationContext Context(bool canInput = true) =>
        DriverSkillRaceContextRules.Build(owner, canInput, 3, session.Players, 60);

    private HunterSkillActivationPlan Prepare()
    {
        Assert.That(HunterSkillActivationPlan.TryCreate(session, owner, 60, 4, 1, 10, Context(),
            out HunterSkillActivationPlan plan, out string reason), Is.True, reason);
        return plan;
    }

    private bool Commit(HunterSkillActivationPlan plan, CardData cost) =>
        plan.TryCommit(session, owner, 4, 1, 10, Context(), cost, out _);

    [TestCase(3, 2, 2)]
    [TestCase(5, 3, 2)]
    [TestCase(7, 3, 3)]
    public void CommitUsesChosenExactInstanceOnceAtEveryTier(int level, int bonus, int uses)
    {
        owner.driverSkill.Initialize(owner.DriverProfile, level, true);
        owner.driverSkill.BeginGlobalTurn();
        session.BeginDriverSkillInput(owner);
        HunterSkillActivationPlan plan = Prepare();
        Assert.That(plan.MovementBonus, Is.EqualTo(bonus));
        Assert.That(plan.Target, Is.SameAs(target));
        Assert.That(plan.TargetDistance, Is.EqualTo(3));
        Assert.That(owner.deck.Hand, Has.Count.EqualTo(2), "Preparation cannot spend a card.");
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses));

        Assert.That(Commit(plan, second), Is.True);
        Assert.That(owner.deck.Hand, Is.EqualTo(new[] { first }));
        Assert.That(owner.deck.DiscardPile, Is.EqualTo(new[] { second }));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(owner.driverSkill.IsActive, Is.True);
        Assert.That(Commit(plan, first), Is.False);
        Assert.That(owner.deck.Hand, Is.EqualTo(new[] { first }));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        Assert.That(plan.IsClosed, Is.True);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    [TestCase(5, true)]
    [TestCase(6, false)]
    [TestCase(-1, false)]
    public void TargetRangeUsesActualProgress(int gap, bool eligible)
    {
        target.position = owner.position + gap;
        Assert.That(HunterSkillActivationPlan.CanPrepare(session, owner, 60, 1, Context(), out _), Is.EqualTo(eligible));
        Assert.That(owner.deck.HandCount, Is.EqualTo(2));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
    }

    [Test]
    public void NearestAcrossStartLineIsFoundButALappedCarIsNotAhead()
    {
        owner.position = 58;
        target.position = 1;
        target.lap = 1;
        Assert.That(Prepare().TargetDistance, Is.EqualTo(3));
        target.lap = 0;
        Assert.That(HunterSkillActivationPlan.CanPrepare(session, owner, 60, 1, Context(), out _), Is.False);
    }

    [Test]
    public void CancelCostsNothingAndCannotBeCommittedLater()
    {
        HunterSkillActivationPlan plan = Prepare();
        plan.Cancel();
        plan.Cancel();
        Assert.That(Commit(plan, first), Is.False);
        AssertNoPayment();
    }

    [TestCase("turn")]
    [TestCase("gear")]
    [TestCase("version")]
    [TestCase("session")]
    [TestCase("owner")]
    [TestCase("phase")]
    public void ChangedCommitBoundaryRejectsWithoutPayment(string boundary)
    {
        HunterSkillActivationPlan plan = Prepare();
        Assert.That(plan.TryCommit(boundary == "session" ? new RaceSession() : session,
            boundary == "owner" ? target : owner, boundary == "turn" ? 5 : 4,
            boundary == "gear" ? 2 : 1, boundary == "version" ? 11 : 10,
            Context(boundary != "phase"), first, out _), Is.False);
        AssertNoPayment();
    }

    [TestCase("target moved")]
    [TestCase("target finished")]
    [TestCase("target blown")]
    [TestCase("new nearest")]
    [TestCase("target removed")]
    [TestCase("target identity")]
    [TestCase("owner finished")]
    [TestCase("pit")]
    [TestCase("skip")]
    [TestCase("oden skip")]
    [TestCase("owner input")]
    [TestCase("interrupt")]
    [TestCase("global reset")]
    [TestCase("initialize same object")]
    [TestCase("runtime replaced")]
    [TestCase("driver identity")]
    public void ChangedParticipantOrClockRejectsWithoutSpending(string change)
    {
        HunterSkillActivationPlan plan = Prepare();
        switch (change)
        {
            case "target moved": target.position++; break;
            case "target finished": target.hasFinished = true; break;
            case "target blown": target.isBlown = true; break;
            case "new nearest": session.Players.Add(new PlayerState("nearer", true, 11, 1)); break;
            case "target removed": session.Players.Remove(target); break;
            case "target identity": target.driverId = "de_sebastian_vettel"; break;
            case "owner finished": owner.hasFinished = true; break;
            case "pit": owner.pitStopScheduled = true; break;
            case "skip": owner.skipNextTurn = true; break;
            case "oden skip": owner.kantoOdenSkipThisTurn = true; break;
            case "owner input": owner.driverSkill.BeginGlobalTurn(); session.BeginDriverSkillInput(owner); break;
            case "interrupt": owner.driverSkill.InterruptDrivingInput(); break;
            case "global reset": owner.driverSkill.BeginGlobalTurn(); break;
            case "initialize same object":
                owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
                owner.driverSkill.BeginDrivingInput();
                break;
            case "runtime replaced":
                owner.driverSkill = new DriverSkillRuntimeState();
                owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
                owner.driverSkill.BeginDrivingInput();
                break;
            case "driver identity": owner.driverId = "uk_nigel_mansell"; break;
        }
        Assert.That(Commit(plan, first), Is.False);
        AssertNoPayment();
    }

    [TestCase("value")]
    [TestCase("type")]
    [TestCase("temporary")]
    [TestCase("trick")]
    [TestCase("hand replaced")]
    public void ChangedCardSourceIsNotSpent(string change)
    {
        HunterSkillActivationPlan plan = Prepare();
        switch (change)
        {
            case "value": second.value++; break;
            case "type": second.type = CardType.Heat; break;
            case "temporary": second.isTemp = true; break;
            case "trick": second.trickId = "foreign"; break;
            case "hand replaced":
                owner.deck.RemoveFromHand(new List<CardData> { second });
                owner.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 3) });
                break;
        }
        Assert.That(Commit(plan, first), Is.False);
        Assert.That(owner.deck.HandCount, Is.EqualTo(2));
        Assert.That(owner.deck.DiscardPileCount, Is.Zero);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
    }

    [TestCase(CardType.Heat, false)]
    [TestCase(CardType.Trick, false)]
    [TestCase(CardType.Speed, true)]
    [TestCase(CardType.Speed, false)]
    public void ForeignOrIneligibleInstancesCannotPay(CardType type, bool temporary)
    {
        var foreign = new CardData(type, 3) { isTemp = temporary };
        HunterSkillActivationPlan plan = Prepare();
        Assert.That(Commit(plan, foreign), Is.False);
        AssertNoPayment();
    }

    [TestCase(CardType.Heat, false)]
    [TestCase(CardType.Trick, false)]
    [TestCase(CardType.Speed, true)]
    public void IneligibleCardsCannotStandInForSecondOrdinarySpeed(CardType type, bool temporary)
    {
        owner.deck.RemoveFromHand(new List<CardData> { second });
        owner.deck.AddCardsToHand(new[] { new CardData(type, 3) { isTemp = temporary } });
        Assert.That(HunterSkillActivationPlan.CanPrepare(session, owner, 60, 1, Context(), out _), Is.False);
        Assert.That(owner.deck.DiscardPileCount, Is.Zero);
    }

    [Test]
    public void BonusIsOneMovementContributionAndForecastCannotSpendRealCost()
    {
        Assert.That(Commit(Prepare(), second), Is.True);
        owner.playedSpeedCardsThisTurn.Add(first);
        Assert.That(RaceMovementRules.ComputeCornerSpeed(owner, null, false), Is.EqualTo(3));
        Assert.That(RaceMovementRules.ComputeCardAndDriverMovementBonus(owner, true), Is.EqualTo(3));
        var preview = owner.driverSkill.CreatePresentationCopy();
        preview.BeginGlobalTurn();
        preview.BeginDrivingInput();
        Assert.That(preview.IsActive, Is.False);
        Assert.That(owner.driverSkill.IsActive, Is.True);
        Assert.That(owner.deck.DiscardPile, Is.EqualTo(new[] { second }));
        Assert.That(first.value, Is.EqualTo(3));
        Assert.That(second.value, Is.EqualTo(3));
        Assert.That(DriverSkillRules.ApplyHeatMultiplier(owner.driverSkill, 2), Is.EqualTo(2));
    }

    [Test]
    public void TwoSameTeamParticipantsDoNotSharePlansOrUses()
    {
        target.teamId = TeamId.UK;
        target.driverId = owner.driverId;
        target.driverSkill.Initialize(target.DriverProfile, 7, true);
        Assert.That(Commit(Prepare(), second), Is.True);
        Assert.That(target.driverSkill.UsesRemaining, Is.EqualTo(3));
        Assert.That(target.driverSkill.IsActive, Is.False);
    }

    [TestCase(1, 2, true)]
    [TestCase(2, 2, false)]
    [TestCase(2, 3, true)]
    [TestCase(4, 4, false)]
    [TestCase(4, 5, true)]
    public void FixedDiscardPreservesPendingGearRequirement(int pending, int count, bool eligible)
    {
        for (int i = 2; i < count; i++) owner.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 1) });
        Assert.That(HunterSkillActivationPlan.CanPrepare(session, owner, 60, pending, Context(), out _), Is.EqualTo(eligible));
        Assert.That(owner.deck.HandCount, Is.EqualTo(count));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
    }

    [TestCase(5, true)]
    [TestCase(6, false)]
    public void ExistingBorrowedBbqHeatCanMeetGearButCannotPayDiscard(int position, bool eligible)
    {
        owner.position = position;
        target.position = position + 3;
        owner.techState = new TechTreeState(TeamId.UK);
        owner.techState.activeNodeIds.Add("uk-l3-sun-never-sets");
        owner.techState.sunNeverSetsTarget = TeamId.US;
        owner.deck.DrawHeatFromPoolToHand(1);
        CardData heat = owner.deck.Hand[2];
        Assert.That(HunterSkillActivationPlan.TryCreate(session, owner, 60, 4, 2, 10, Context(),
            out HunterSkillActivationPlan plan, out _), Is.EqualTo(eligible));
        if (!eligible) return;
        Assert.That(plan.Choices, Has.No.Member(heat));
        Assert.That(plan.TryCommit(session, owner, 4, 2, 10, Context(), heat, out _), Is.False);
        Assert.That(plan.TryCommit(session, owner, 4, 2, 10, Context(), second, out _), Is.True);
        Assert.That(owner.deck.ContainsInHand(heat), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(5));
    }

    private void AssertNoPayment()
    {
        Assert.That(owner.deck.Hand, Is.EqualTo(new[] { first, second }));
        Assert.That(owner.deck.DiscardPileCount, Is.Zero);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
        Assert.That(owner.driverSkill.IsActive, Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6));
    }
}
