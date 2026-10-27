using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

/// <summary>D02 production owner-input observations and exact old-hand heat settlement.</summary>
public class MansellPursuitTests
{
    private RaceSession race;
    private PlayerState owner, target;
    private const int Nodes = 60;

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(12));
        owner = new PlayerState("Mansell", false, 0, 1)
            { teamId = TeamId.UK, driverId = "uk_nigel_mansell" };
        target = new PlayerState("target", true, 8, 1)
            { teamId = TeamId.DE, driverId = "de_michael_schumacher" };
        race.Players.Add(owner); race.Players.Add(target);
        owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        owner.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(6));
        owner.deck.DrawToHand(1);
    }

    private bool Begin(int finish = 0)
    {
        race.BeginTurn(owner);
        return race.BeginDriverSkillInput(owner, Nodes, finish);
    }

    private void Qualify(int gap = 2)
    {
        target.position = 8; Begin(); target.position = gap; Begin();
    }

    private CardData OldHeat()
    {
        owner.deck.DrawHeatFromPoolToHand(1);
        return owner.deck.Hand.Last();
    }

    [TestCase(2, 1, true)] [TestCase(2, 2, true)] [TestCase(2, 3, false)]
    [TestCase(4, 3, true)] [TestCase(4, 4, false)]
    [TestCase(6, 4, true)] [TestCase(6, 5, false)] [TestCase(1, 1, false)]
    public void FinalPositiveGapUsesPassiveTierAndInitialObservationHasNoActiveSixCellCap(int level, int gap, bool expected)
    {
        owner.driverSkill.Initialize(owner.DriverProfile, level, true);
        CardData heat = OldHeat(); Qualify(gap);
        var result = race.ResolveMansellPursuitAfterDraw(owner);
        Assert.That(result.Triggered, Is.EqualTo(expected));
        Assert.That(result.Restored, Is.EqualTo(expected ? 1 : 0));
        Assert.That(owner.deck.ContainsInHand(heat), Is.EqualTo(!expected));
        Assert.That(owner.driverSkill.PassiveMovementBonusThisTurn, Is.Zero);
        Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
    }

    [TestCase(8)] [TestCase(9)] [TestCase(0)] [TestCase(-1)]
    public void UnchangedWiderTiedOrPassedGapCannotTrigger(int nextPosition)
    {
        CardData heat = OldHeat(); Begin(); target.position = nextPosition; Begin();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        Assert.That(owner.deck.ContainsInHand(heat), Is.True);
    }

    [Test]
    public void ActuallyPassedTargetHasNegativeProgressGapAndCannotReward()
    {
        CardData heat = OldHeat(); Begin(); owner.position = 10; target.position = 9; Begin();
        Assert.That(HunterSlipstreamRuntime.Progress(target, Nodes) - HunterSlipstreamRuntime.Progress(owner, Nodes), Is.EqualTo(-1));
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        Assert.That(owner.deck.ContainsInHand(heat), Is.True);
    }

    [Test]
    public void OwnerMovementAloneNeverResolvesBeforeNextInputObservation()
    {
        CardData heat = OldHeat(); target.position = 5; Begin(); owner.position = 3;
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        target.position = 8; Begin(); // Other racers moved too; net gap did not shrink.
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        Assert.That(owner.deck.ContainsInHand(heat), Is.True);
    }

    [Test]
    public void OtherParticipantsGlobalCleanupDoesNotBecomeAnOwnerObservation()
    {
        OldHeat(); Begin();
        for (int i = 0; i < 3; i++) race.BeginTurn(target);
        target.position = 2; Begin();
        Assert.That(owner.driverSkill.DrivingInputsStarted, Is.EqualTo(2));
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Restored, Is.EqualTo(1));
    }

    [TestCase("nearer")] [TestCase("same-id-replacement")] [TestCase("changed-id")]
    [TestCase("finished")] [TestCase("retired")] [TestCase("removed")]
    public void TargetMustRemainTheSameLiveNearestOriginalParticipant(string change)
    {
        CardData heat = OldHeat(); Begin(); target.position = 3;
        if (change == "nearer") race.Players.Add(new PlayerState("new nearest", true, 1, 1));
        if (change == "same-id-replacement")
        {
            race.Players.Remove(target);
            race.Players.Add(new PlayerState("replacement", true, 2, 1) { driverId = target.driverId });
        }
        if (change == "changed-id") target.driverId = "de_sebastian_vettel";
        if (change == "finished") target.hasFinished = true;
        if (change == "retired") target.isBlown = true;
        if (change == "removed") race.Players.Remove(target);
        Begin();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        Assert.That(owner.deck.ContainsInHand(heat), Is.True);
    }

    [Test]
    public void PendingPitReservationDoesNotHideTheActualNearestCar()
    {
        OldHeat(); target.position = 4; target.pitStopScheduled = true;
        var farther = new PlayerState("farther", true, 6, 1); race.Players.Add(farther);
        Begin(); Assert.That(owner.driverSkill.Pursuit.Target, Is.SameAs(target));
        target.position = 2; farther.position = 3; Begin();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Restored, Is.EqualTo(1));
    }

    [TestCase("skip")] [TestCase("pit")] [TestCase("oden")]
    [TestCase("finished")] [TestCase("retired")]
    public void IneligibleOwnerInputBreaksPursuitWithoutDeferral(string change)
    {
        CardData heat = OldHeat(); Begin();
        if (change == "skip") owner.skipNextTurn = true;
        if (change == "pit") owner.pitStopScheduled = true;
        if (change == "oden") owner.kantoOdenSkipThisTurn = true;
        if (change == "finished") owner.hasFinished = true;
        if (change == "retired") owner.isBlown = true;
        // Oden is turn-scoped; invoke A1 before any new global cleanup clears it.
        Assert.That(race.BeginDriverSkillInput(owner, Nodes), Is.False);
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        owner.skipNextTurn = false; owner.pitStopScheduled = false; owner.kantoOdenSkipThisTurn = false;
        owner.hasFinished = false; owner.isBlown = false;
        target.position = 2; Begin();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        Assert.That(owner.deck.ContainsInHand(heat), Is.True);
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(3));
    }

    [Test]
    public void NewDrawAndBreakfastHeatCannotFillTheFrozenOldHandSource()
    {
        Qualify(); CardData newPermanent = OldHeat();
        CardData breakfast = CardData.CreateTempHeat(); owner.deck.AddCardsToHand(new[] { breakfast });
        var result = race.ResolveMansellPursuitAfterDraw(owner);
        Assert.That(result.Triggered, Is.True); Assert.That(result.Removed.Total, Is.Zero);
        Assert.That(owner.deck.ContainsInHand(newPermanent), Is.True);
        Assert.That(owner.deck.ContainsInHand(breakfast), Is.True);
        Assert.That(owner.driverSkill.Pursuit.HasTriggeredInLap(owner.lap), Is.True);
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
    }

    [Test]
    public void OldDiscardPaymentNeverBecomesDirectPassiveRefund()
    {
        owner.deck.DrawHeatFromPool(1); Qualify();
        var result = race.ResolveMansellPursuitAfterDraw(owner);
        Assert.That(result.Triggered, Is.True); Assert.That(result.Removed.Total, Is.Zero);
        Assert.That(owner.deck.CountHeatInDiscardPile(), Is.EqualTo(1));
    }

    [TestCase("removed")] [TestCase("duplicate")] [TestCase("draw-alias")]
    public void StaleOrDuplicateOriginalHeatCannotBeReplacedByEqualNewInstance(string change)
    {
        CardData old = OldHeat(); Qualify();
        if (change == "removed") owner.deck.RemoveFromHand(new List<CardData> { old });
        if (change == "duplicate")
            ((List<CardData>)typeof(CardDeck).GetField("hand", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(owner.deck)).Add(old); // Deliberately corrupt ownership; public APIs reject permanent heat insertion.
        if (change == "draw-alias")
            ((List<CardData>)typeof(CardDeck).GetField("drawPile", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(owner.deck)).Add(old);
        CardData replacement = OldHeat();
        var result = race.ResolveMansellPursuitAfterDraw(owner);
        Assert.That(result.Removed.Total, Is.Zero);
        Assert.That(owner.deck.ContainsInHand(replacement), Is.True);
    }

    [Test]
    public void ExistingOldHandInstanceWinsOverNewPermanentAndTemporaryHeat()
    {
        CardData old = OldHeat(); Qualify(); CardData fresh = OldHeat();
        CardData temporary = CardData.CreateTempHeat(); owner.deck.AddCardsToHand(new[] { temporary });
        var result = race.ResolveMansellPursuitAfterDraw(owner);
        Assert.That(result.Removed.FromHand, Is.EqualTo(1));
        Assert.That(result.Removed.FromDiscard + result.Removed.FromDraw, Is.Zero);
        Assert.That(result.Restored, Is.EqualTo(1)); Assert.That(result.Retired, Is.Zero);
        Assert.That(owner.deck.ContainsInHand(old), Is.False);
        Assert.That(owner.deck.ContainsInHand(fresh), Is.True);
        Assert.That(owner.deck.ContainsInHand(temporary), Is.True);
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
    }

    [Test]
    public void RegionalRetirementIsNotReportedAsNetEngineReturn()
    {
        owner.deck.SetRegionalCapacityBonus(1); owner.deck.DrawHeatFromPoolToHand(7);
        owner.deck.SetRegionalCapacityBonus(0); Qualify();
        var result = race.ResolveMansellPursuitAfterDraw(owner);
        Assert.That(result.Removed.Total, Is.EqualTo(1));
        Assert.That(result.Retired, Is.EqualTo(1)); Assert.That(result.Restored, Is.Zero);
        Assert.That(owner.deck.RegionalHeatPendingRetirement, Is.Zero);
        Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
    }

    [Test]
    public void EmptyFirstQualificationExpiresAndNextLapRearmsOnlyOnce()
    {
        Qualify(); Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Removed.Total, Is.Zero);
        OldHeat(); target.position = 1; Begin();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        owner.lap++; target.lap++; target.position = 8; Begin(); target.position = 2; Begin();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Restored, Is.EqualTo(1));
        OldHeat(); target.position = 1; Begin();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
    }

    [TestCase("position")] [TestCase("target-position")] [TestCase("reinitialize")]
    [TestCase("session")] [TestCase("deck")] [TestCase("pool")] [TestCase("close")]
    public void A2RejectsStaleOwnerTargetAndLifecycleSnapshots(string change)
    {
        CardData heat = OldHeat(); Qualify();
        if (change == "position") owner.position++;
        if (change == "target-position") target.position++;
        if (change == "reinitialize") owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        if (change == "session") race = new RaceSession(new SystemRandomSource(1)) { Players = race.Players };
        if (change == "deck") owner.deck = new CardDeck();
        if (change == "pool") owner.deck.heatPool = new HeatPool(6);
        if (change == "close") owner.driverSkill.Pursuit.CloseInput();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        if (change != "deck") Assert.That(owner.deck.ContainsInHand(heat), Is.True);
    }

    [Test]
    public void PresentationCopyCannotCommitOrMutateRealPendingSource()
    {
        CardData heat = OldHeat(); Qualify(); var copy = owner.driverSkill.CreatePresentationCopy();
        Assert.That(copy.Pursuit, Is.Not.SameAs(owner.driverSkill.Pursuit));
        Assert.That(copy.Pursuit.ConsumeAfterDraw(race, owner).Triggered, Is.False);
        copy.Pursuit.Interrupt();
        Assert.That(owner.driverSkill.Pursuit.IsPending, Is.True);
        Assert.That(owner.deck.ContainsInHand(heat), Is.True);
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Restored, Is.EqualTo(1));
    }

    [TestCase(true)] [TestCase(false)]
    public void TutorialDisabledAndOrdinaryAiNeverGainPursuit(bool ai)
    {
        owner.isAI = ai; owner.driverSkill.Initialize(owner.DriverProfile, 7, ai);
        CardData heat = OldHeat(); Qualify();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
        Assert.That(owner.deck.ContainsInHand(heat), Is.True);
    }

    [Test]
    public void NonZeroFinishUsesActualProgressAcrossPhysicalIndexWrap()
    {
        OldHeat(); owner.position = 58; target.lap = owner.lap + 1; target.position = 9;
        Begin(7); owner.lap++; owner.position = 8; target.position = 10; Begin(7);
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Restored, Is.EqualTo(1));
    }

    [Test]
    public void SameTeamAndSameDriverIdParticipantsOwnIndependentLapQuotas()
    {
        OldHeat();
        var second = new PlayerState("second Mansell", false, 0, 1)
            { teamId = TeamId.UK, driverId = owner.driverId };
        second.driverSkill.Initialize(second.DriverProfile, 7, true);
        second.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(6));
        second.deck.DrawHeatFromPoolToHand(1); race.Players.Add(second);
        foreach (int gap in new[] { 8, 2 })
        {
            target.position = gap;
            foreach (PlayerState p in new[] { owner, second })
            {
                race.BeginTurn(p);
                Assert.That(race.BeginDriverSkillInput(p, Nodes), Is.True);
            }
        }
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Restored, Is.EqualTo(1));
        Assert.That(second.driverSkill.Pursuit.IsPending, Is.True);
        Assert.That(race.ResolveMansellPursuitAfterDraw(second).Restored, Is.EqualTo(1));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6));
        Assert.That(second.deck.heatPool.remaining, Is.EqualTo(6));
    }

    [Test]
    public void NormalRaceReinitializationRebuildsQuotaAndRejectsOldRuntime()
    {
        OldHeat(); Qualify();
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Restored, Is.EqualTo(1));
        var old = owner.driverSkill.Pursuit;
        owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        Assert.That(owner.driverSkill.Pursuit, Is.Not.SameAs(old));
        Assert.That(owner.driverSkill.Pursuit.HasTriggeredInLap(owner.lap), Is.False);
        OldHeat(); Qualify();
        Assert.That(old.ConsumeAfterDraw(race, owner).Triggered, Is.False);
        Assert.That(race.ResolveMansellPursuitAfterDraw(owner).Restored, Is.EqualTo(1));
    }
}
