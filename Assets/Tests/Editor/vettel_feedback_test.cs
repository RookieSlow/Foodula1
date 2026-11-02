using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>D04 typed actual-source contract, next-owner-input frozen old heat and isolated forecasts.</summary>
public class VettelFeedbackTests
{
    private RaceSession race;
    private PlayerState owner, other;
    private VettelFeedbackRuntime Feedback => owner.driverSkill.Feedback;

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(41));
        owner = NewOwner(6); other = NewOwner(6);
        race.Players.Add(owner); race.Players.Add(other);
    }

    private static PlayerState NewOwner(int level)
    {
        var p = new PlayerState("Vettel", false, 0, 1)
            { teamId = TeamId.DE, driverId = "de_sebastian_vettel", lap = 1 };
        p.driverSkill.Initialize(p.DriverProfile, level, true);
        p.deck.InitializeExactOrder(Enumerable.Range(0, 12).Select(_ => new CardData(CardType.Speed, 1)).ToArray(), new HeatPool(20));
        p.deck.DrawToHand(2); return p;
    }
    private void Begin()
    {
        Feedback.BeginGlobalTurn(); race.BeginTurn(owner);
        Assert.That(Feedback.BeginInput(race, owner), Is.True);
    }
    private CardData Heat()
    {
        Assert.That(owner.deck.DrawHeatFromPoolToHand(1), Is.EqualTo(1));
        return owner.deck.Hand.Last();
    }
    private void Earn(VettelNativeSource source = VettelNativeSource.BasicCruise)
        => Assert.That(Feedback.RecordSource(race, owner, source, 1), Is.True);
    private CardData Ready()
    {
        Begin(); Earn(); CardData old = Heat(); Begin(); return old;
    }

    [TestCase(2, VettelNativeSource.BasicCruise, true)]
    [TestCase(2, VettelNativeSource.PaidBeer, false)]
    [TestCase(2, VettelNativeSource.GermanCornerExit, false)]
    [TestCase(4, VettelNativeSource.BasicCruise, true)]
    [TestCase(4, VettelNativeSource.PaidBeer, true)]
    [TestCase(4, VettelNativeSource.GermanCornerExit, false)]
    [TestCase(6, VettelNativeSource.BasicCruise, true)]
    [TestCase(6, VettelNativeSource.PaidBeer, true)]
    [TestCase(6, VettelNativeSource.GermanCornerExit, true)]
    public void SourcesUnlockOnlyAtSpecifiedPassiveTier(int level, VettelNativeSource source, bool allowed)
    {
        owner.driverSkill.Initialize(owner.DriverProfile, level, true); Begin();
        int heat = owner.deck.heatPool.remaining, uses = owner.driverSkill.UsesRemaining;
        Assert.That(Feedback.RecordSource(race, owner, source, 1), Is.EqualTo(allowed));
        Assert.That(Feedback.HasPending, Is.EqualTo(allowed)); Assert.That(Feedback.HasTriggeredInLap(owner.lap), Is.EqualTo(allowed));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [TestCase(VettelNativeSource.BasicCruise, 0)] [TestCase(VettelNativeSource.BasicCruise, -1)]
    [TestCase(VettelNativeSource.PaidBeer, 0)] [TestCase(VettelNativeSource.PaidBeer, -1)]
    [TestCase(VettelNativeSource.GermanCornerExit, 0)] [TestCase(VettelNativeSource.GermanCornerExit, -1)]
    public void ZeroOrFailedSourcesCannotRecordOrConsumeLap(VettelNativeSource source, int amount)
    {
        Begin(); Assert.That(Feedback.RecordSource(race, owner, source, amount), Is.False);
        Assert.That(Feedback.HasPending, Is.False); Assert.That(Feedback.HasTriggeredInLap(owner.lap), Is.False);
        Earn(source); Assert.That(Feedback.PendingSource, Is.EqualTo(source));
    }

    [TestCase(0)] [TestCase(4)] [TestCase(999)]
    public void UnknownNativeSourceCannotCreateFeedback(int source)
    {
        Begin(); Assert.That(Feedback.RecordSource(race, owner, (VettelNativeSource)source, 20), Is.False);
        Assert.That(Feedback.HasPending, Is.False); Assert.That(Feedback.HasTriggeredInLap(1), Is.False);
    }

    [Test]
    public void SettledMovementUsesItsProducerLapRatherThanLapAfterCrossingTheLine()
    {
        Begin(); owner.lap = 2;
        Assert.That(Feedback.RecordSource(race, owner, VettelNativeSource.BasicCruise, 1, 1), Is.True);
        Assert.That(Feedback.HasTriggeredInLap(1), Is.True); Assert.That(Feedback.HasTriggeredInLap(2), Is.False);
        Heat(); Begin(); Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
        Earn(); Assert.That(Feedback.HasTriggeredInLap(2), Is.True);
    }

    [TestCase(-1)] [TestCase(2)]
    public void UnknownOrFutureProducerLapCannotPreconsumeARealLap(int sourceLap)
    {
        Begin(); Assert.That(Feedback.RecordSource(race, owner, VettelNativeSource.BasicCruise, 1, sourceLap), Is.False);
        Assert.That(Feedback.HasPending, Is.False); Assert.That(Feedback.HasTriggeredInLap(sourceLap), Is.False);
        Earn();
    }

    [Test]
    public void EarningInputCannotCoolAndNextInputFreezesOnlyThenExistingPermanentHandHeat()
    {
        Begin(); Earn(); int input = Feedback.InputsStarted;
        CardData before = Heat(); Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.ContainsInHand(before), Is.True);
        Begin(); CardData after = Heat();
        Assert.That(Feedback.InputsStarted, Is.EqualTo(input + 1));
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
        Assert.That(owner.deck.ContainsInHand(before), Is.False); Assert.That(owner.deck.ContainsInHand(after), Is.True);
        Assert.That(Feedback.LastRestored, Is.EqualTo(1)); Assert.That(Feedback.LastRetired, Is.Zero);
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(20));
    }

    [Test]
    public void MaximumOneOldHeatReturnsEvenWithManyOldInstancesAndLargeNativeAmount()
    {
        Begin(); Assert.That(Feedback.RecordSource(race, owner, VettelNativeSource.BasicCruise, 99), Is.True);
        CardData a = Heat(), b = Heat(); Begin();
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
        Assert.That(owner.deck.ContainsInHand(a), Is.False); Assert.That(owner.deck.ContainsInHand(b), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19)); Assert.That(Feedback.HasPending, Is.False);
    }

    [Test]
    public void NoOldHandHeatExpiresWithoutUsingNewPermanentOrTemporaryHeat()
    {
        Begin(); Earn(); Begin(); CardData added = Heat();
        owner.deck.AddCardsToHand(new[] { new CardData(CardType.Heat, 0) { isTemp = true } });
        int hand = owner.deck.HandCount, heat = owner.deck.heatPool.remaining;
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.HandCount, Is.EqualTo(hand)); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat));
        Assert.That(owner.deck.ContainsInHand(added), Is.True); Assert.That(Feedback.HasPending, Is.False);
        Begin(); Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
    }

    [Test]
    public void TemporaryHeatAtFreezeCannotReturnOrBeSubstitutedAfterDraw()
    {
        Begin(); Earn(); var temporary = new CardData(CardType.Heat, 0) { isTemp = true };
        owner.deck.AddCardsToHand(new[] { temporary }); Begin(); Heat();
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.ContainsInHand(temporary), Is.True); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
    }

    [Test]
    public void OrdinaryCooldownCannotReturnSameInstanceTwiceOrSubstituteNewHeat()
    {
        CardData old = Ready(); Assert.That(owner.deck.ReturnHeatCardsToPool(new[] { old }), Is.EqualTo(1));
        CardData fresh = Heat(); int heat = owner.deck.heatPool.remaining;
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat)); Assert.That(owner.deck.ContainsInHand(fresh), Is.True);
    }

    [Test]
    public void FrozenHeatMovedToDiscardIsNeverRecoveredFromDiscardOrReplacedByNewHeat()
    {
        CardData old = Ready(); var removed = owner.deck.RemoveFromHand(new List<CardData> { old });
        owner.deck.DiscardPlayedSpeedCards(removed); CardData fresh = Heat();
        Assert.That(owner.deck.DiscardPile.Contains(old), Is.True);
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.DiscardPile.Contains(old), Is.True); Assert.That(owner.deck.ContainsInHand(fresh), Is.True);
    }

    [Test]
    public void DuplicateOwnedInstanceInAnotherZoneIsNotAValidFrozenSource()
    {
        Begin(); Earn(); CardData old = Heat();
        owner.deck.DiscardPlayedSpeedCards(new List<CardData> { old }); Begin();
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.ContainsInHand(old), Is.True); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
    }

    [Test]
    public void RemovedFirstFrozenInstanceCanUseAnotherFrozenInstanceButNotANewOne()
    {
        Begin(); Earn(); CardData first = Heat(), second = Heat(); Begin();
        owner.deck.ReturnHeatCardsToPool(new[] { first }); CardData fresh = Heat();
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
        Assert.That(owner.deck.ContainsInHand(second), Is.False); Assert.That(owner.deck.ContainsInHand(fresh), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
    }

    [Test]
    public void NoStackOrRenewalAndOneFirstSourcePerLapEvenAfterCoolingOrInterruption()
    {
        Begin(); Earn(); int ready = Feedback.ReadyInput;
        Assert.That(Feedback.RecordSource(race, owner, VettelNativeSource.PaidBeer, 1), Is.False);
        owner.lap = 2;
        Assert.That(Feedback.RecordSource(race, owner, VettelNativeSource.PaidBeer, 1), Is.False);
        Assert.That(Feedback.HasTriggeredInLap(2), Is.True); Assert.That(Feedback.ReadyInput, Is.EqualTo(ready));
        Heat(); Begin(); Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
        Assert.That(Feedback.RecordSource(race, owner, VettelNativeSource.GermanCornerExit, 1), Is.False);
        Feedback.Interrupt(); Begin(); Assert.That(Feedback.RecordSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.False);
        owner.lap = 3; Earn(); Assert.That(Feedback.HasPending, Is.True);
    }

    [Test]
    public void GlobalRoundsAndOtherOwnerCannotAdvanceFeedbackAndDuplicateBeginCannotRefreeze()
    {
        Begin(); Earn(); CardData old = Heat(); int input = Feedback.InputsStarted;
        Assert.That(Feedback.BeginInput(race, owner), Is.False);
        for (int i = 0; i < 7; i++) { race.BeginTurn(other); Feedback.BeginGlobalTurn(); }
        Assert.That(Feedback.InputsStarted, Is.EqualTo(input)); Assert.That(Feedback.HasPending, Is.True);
        Assert.That(Feedback.BeginInput(race, owner), Is.True); CardData fresh = Heat();
        Assert.That(Feedback.BeginInput(race, owner), Is.False);
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
        Assert.That(owner.deck.ContainsInHand(old), Is.False); Assert.That(owner.deck.ContainsInHand(fresh), Is.True);
    }

    [Test]
    public void UnconsumedReadyInputExpiresRatherThanMovingToLaterInput()
    {
        CardData old = Ready(); Begin();
        Assert.That(Feedback.HasPending, Is.False); Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.ContainsInHand(old), Is.True);
    }

    [TestCase("finish")] [TestCase("blown")] [TestCase("skip")] [TestCase("kanto")] [TestCase("pit")]
    [TestCase("ai")] [TestCase("disabled")] [TestCase("foreign")] [TestCase("driver")] [TestCase("team")]
    public void IneligibleInputOrParticipantCannotRecordOrSpendFeedback(string condition)
    {
        Begin(); Earn(); CardData old = Heat(); int inputs = Feedback.InputsStarted;
        if (condition == "finish") owner.hasFinished = true;
        if (condition == "blown") owner.isBlown = true;
        if (condition == "skip") owner.skipNextTurn = true;
        if (condition == "kanto") owner.kantoOdenSkipThisTurn = true;
        if (condition == "pit") owner.pitStopScheduled = true;
        if (condition == "ai") owner.isAI = true;
        if (condition == "disabled") owner.driverSkill.Initialize(owner.DriverProfile, 6, false);
        if (condition == "foreign") race.Players.Remove(owner);
        if (condition == "driver") owner.driverId = "de_michael_schumacher";
        if (condition == "team") owner.teamId = TeamId.UK;
        Assert.That(Feedback.BeginInput(race, owner), Is.False);
        Assert.That(Feedback.InputsStarted, Is.EqualTo(condition == "disabled" ? 0 : inputs));
        Assert.That(Feedback.RecordSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.False);
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(owner.deck.ContainsInHand(old), Is.True);
    }

    [TestCase("pool")] [TestCase("deck")] [TestCase("driver")] [TestCase("session")]
    public void ChangedScopeCannotReturnFrozenHeat(string changed)
    {
        CardData old = Ready(); var original = owner.deck; var actualRace = race;
        if (changed == "pool") owner.deck.heatPool = new HeatPool(19);
        if (changed == "deck") owner.deck = owner.deck.CreatePresentationCopy();
        if (changed == "driver") owner.driverId = "de_michael_schumacher";
        if (changed == "session") race = new RaceSession(new SystemRandomSource(8)) { Players = actualRace.Players };
        int heat = original.heatPool.remaining;
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(original.ContainsInHand(old), Is.True); Assert.That(original.heatPool.remaining, Is.EqualTo(heat));
    }

    [Test]
    public void SameTeamDifferentOwnerAndWrongSessionCallsCannotCancelOrStealRealPending()
    {
        CardData old = Ready();
        Assert.That(Feedback.RecordSource(race, other, VettelNativeSource.BasicCruise, 1), Is.False);
        Assert.That(Feedback.ConsumeAfterDraw(race, other), Is.Zero); Assert.That(Feedback.HasPending, Is.True);
        var wrongRace = new RaceSession(new SystemRandomSource(9)) { Players = race.Players };
        Assert.That(Feedback.ConsumeAfterDraw(wrongRace, owner), Is.Zero); Assert.That(Feedback.HasPending, Is.True);
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
        Assert.That(owner.deck.ContainsInHand(old), Is.False); Assert.That(other.deck.heatPool.remaining, Is.EqualTo(20));
    }

    [Test]
    public void InitializationResetsLedgerAndWindowWithoutChangingStableDriverIdentityOrActiveUseCount()
    {
        CardData old = Ready(); var prior = Feedback; int uses = owner.driverSkill.UsesRemaining;
        owner.driverSkill.Initialize(owner.DriverProfile, 6, true);
        Assert.That(Feedback, Is.Not.SameAs(prior)); Assert.That(Feedback.HasPending, Is.False);
        Assert.That(Feedback.HasTriggeredInLap(1), Is.False); Assert.That(Feedback.InputsStarted, Is.Zero);
        Assert.That(prior.ConsumeAfterDraw(race, owner), Is.Zero); Assert.That(owner.deck.ContainsInHand(old), Is.True);
        Assert.That(owner.driverId, Is.EqualTo("de_sebastian_vettel")); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(owner.driverSkill.Skill, Is.EqualTo(DriverActiveSkillId.PerfectRhythm));
        Begin(); Earn();
    }

    [Test]
    public void ForecastDeepCopyCanCoolAndObserveAnotherLapWithoutMutatingRealCollectionsOrPool()
    {
        CardData old = Ready();
        var copy = new PlayerState("Forecast", false, owner.position, owner.gear) { teamId = owner.teamId,
            driverId = owner.driverId, driverSkill = owner.driverSkill.CreatePresentationCopy(),
            deck = owner.deck.CreatePresentationCopy(), lap = owner.lap };
        var feedback = copy.driverSkill.Feedback;
        Assert.That(feedback.ConsumeAfterDraw(race, owner), Is.Zero);
        Assert.That(feedback.HasPending, Is.True, "An unbound forecast call must preserve its cloned pending window");
        Assert.That(feedback.IsInputOpen, Is.True);
        Assert.That(feedback.RebindPresentationCopy(race, owner, copy), Is.True);
        Assert.That(feedback.ConsumeAfterDraw(race, copy), Is.EqualTo(1));
        copy.lap = 2; Assert.That(feedback.RecordSource(race, copy, VettelNativeSource.BasicCruise, 1), Is.True);
        Assert.That(Feedback.HasTriggeredInLap(2), Is.False); Assert.That(Feedback.HasPending, Is.True);
        Assert.That(owner.deck.ContainsInHand(old), Is.True); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
        Assert.That(copy.deck.ContainsInHand(old), Is.False); Assert.That(copy.deck.heatPool.remaining, Is.EqualTo(20));
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
    }

    [Test]
    public void ForecastCannotRebindSharedRealDeckOrEngine()
    {
        Ready();
        var copy = new PlayerState("invalid forecast", false, owner.position, owner.gear) { teamId = owner.teamId,
            driverId = owner.driverId, driverSkill = owner.driverSkill.CreatePresentationCopy(), deck = owner.deck };
        Assert.That(copy.driverSkill.Feedback.RebindPresentationCopy(race, owner, copy), Is.False);
        copy.deck = owner.deck.CreatePresentationCopy(); copy.deck.heatPool = owner.deck.heatPool;
        Assert.That(copy.driverSkill.Feedback.RebindPresentationCopy(race, owner, copy), Is.False);
        Assert.That(Feedback.HasPending, Is.True);
    }

    [Test]
    public void RegionalCapacityRetirementIsNotReportedAsNetEngineRecovery()
    {
        Begin(); Earn(); owner.deck.heatPool.remaining = 0; owner.deck.SetRegionalCapacityBonus(1);
        CardData old = Heat(); owner.deck.SetRegionalCapacityBonus(0); Begin();
        Assert.That(owner.deck.RegionalHeatPendingRetirement, Is.EqualTo(1));
        Assert.That(Feedback.ConsumeAfterDraw(race, owner), Is.EqualTo(1));
        Assert.That(Feedback.LastRetired, Is.EqualTo(1)); Assert.That(Feedback.LastRestored, Is.Zero);
        Assert.That(owner.deck.heatPool.remaining, Is.Zero); Assert.That(owner.deck.ContainsInHand(old), Is.False);
        Assert.That(owner.deck.RegionalHeatPendingRetirement, Is.Zero);
    }
}
