using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>D04 active fixed-source transaction and owner-input lifetime, without UI or native-event claims.</summary>
public class VettelRhythmTests
{
    private RaceSession race;
    private PlayerState owner, other;
    private int turn;
    private VettelRhythmRuntime Rhythm => owner.driverSkill.Rhythm;
    private DriverSkillActivationContext Context => new DriverSkillActivationContext(true, owner.lap + 1, 99,
        owner.deck.heatPool.remaining, 20, 0);

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(83)); owner = NewOwner(7); other = NewOwner(7);
        race.Players.Add(owner); race.Players.Add(other); Begin();
    }
    private static PlayerState NewOwner(int level)
    {
        var p = new PlayerState("Vettel", false, 0, 1) { driverId = "de_sebastian_vettel", teamId = TeamId.DE, lap = 1 };
        p.driverSkill.Initialize(p.DriverProfile, level, true);
        p.deck.InitializeExactOrder(Enumerable.Range(0, 12).Select(_ => new CardData(CardType.Speed, 1)).ToArray(), new HeatPool(20));
        p.deck.DrawToHand(3); return p;
    }
    private void Begin()
    { turn++; owner.driverSkill.BeginGlobalTurn(); race.BeginTurn(owner); Assert.That(Rhythm.BeginInput(race, owner), Is.True); }
    private void OwnTech(bool beer = true, bool exit = true)
    {
        owner.techState = new TechTreeState(TeamId.DE);
        if (beer) { owner.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel"); owner.techState.unlockedNodeIds.Add("de-l1-schwarzbier-fuel"); }
        if (exit) { owner.techState.activeNodeIds.Add("de-l2-wurstplatte"); owner.techState.unlockedNodeIds.Add("de-l2-wurstplatte"); }
    }
    private VettelRhythmActivationPlan Plan(int gear = 1, int request = 3)
    {
        Assert.That(VettelRhythmActivationPlan.TryCreate(race, owner, turn, gear, request, Context, out var plan, out var reason), Is.True, reason);
        return plan;
    }
    private bool Commit(VettelRhythmActivationPlan plan, VettelNativeSource source = VettelNativeSource.BasicCruise, int gear = 1, int request = 3)
        => plan.TryCommit(race, owner, turn, gear, request, Context, source, out _);
    private VettelRhythmActivationPlan Activate(VettelNativeSource source = VettelNativeSource.BasicCruise)
    { var plan = Plan(); Assert.That(Commit(plan, source), Is.True); return plan; }

    [TestCase(3, 1, 1)] [TestCase(5, 2, 1)] [TestCase(7, 2, 2)]
    public void ExactOnePaidUseBuysOnlySpecifiedOwnerInputsAndMaximumTwoMovementEach(int level, int lifetime, int initialUses)
    {
        owner.driverSkill.Initialize(owner.DriverProfile, level, true); Begin();
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(initialUses)); var plan = Activate();
        Assert.That(plan.PaidHeat, Is.Not.Null); Assert.That(plan.PaidHeat.isTemp, Is.False);
        Assert.That(owner.deck.ContainsInHand(plan.PaidHeat), Is.True); Assert.That(owner.deck.DiscardPile.Contains(plan.PaidHeat), Is.False);
        Assert.That(owner.heatPaidCardsThisTurn.Single(), Is.SameAs(plan.PaidHeat));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(initialUses - 1));
        int total = 0;
        for (int i = 0; i < lifetime; i++)
        {
            if (i > 0) Begin();
            Assert.That(Rhythm.HasWindow, Is.True); total += Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 1);
            Assert.That(Rhythm.BonusThisInput, Is.EqualTo(2));
            Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 100), Is.Zero);
            Assert.That(VettelRhythmActivationPlan.CanPrepare(race, owner, 1, Context, out _), Is.False);
        }
        Assert.That(total, Is.EqualTo(2 * lifetime)); Begin(); Assert.That(Rhythm.HasWindow, Is.False);
        Assert.That(Rhythm.ManualPath, Is.True); Assert.That(Rhythm.BonusThisInput, Is.Zero);
        Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(20));
    }

    [TestCase(false, false, 1)] [TestCase(true, false, 2)] [TestCase(false, true, 2)] [TestCase(true, true, 3)]
    public void ChoicesDependOnOwnedNativeTechnologyWhileCruiseIsAlwaysSelectable(bool beer, bool exit, int count)
    {
        OwnTech(beer, exit); var p = Plan(); Assert.That(p.Choices.Count, Is.EqualTo(count));
        Assert.That(p.Choices.Contains(VettelNativeSource.BasicCruise), Is.True);
        Assert.That(p.Choices.Contains(VettelNativeSource.PaidBeer), Is.EqualTo(beer));
        Assert.That(p.Choices.Contains(VettelNativeSource.GermanCornerExit), Is.EqualTo(exit));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
    }

    [TestCase(VettelNativeSource.PaidBeer)] [TestCase(VettelNativeSource.GermanCornerExit)] [TestCase((VettelNativeSource)99)]
    public void UnknownOrUnownedSelectionCannotPartiallySpend(VettelNativeSource source)
    {
        var p = Plan(); Assert.That(Commit(p, source), Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20)); Assert.That(owner.deck.HandCount, Is.EqualTo(3));
        Assert.That(owner.heatPaidCardsThisTurn, Is.Empty); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
        Assert.That(Rhythm.ManualPath, Is.False); Assert.That(Rhythm.HasWindow, Is.False);
    }

    [TestCase(VettelNativeSource.BasicCruise)] [TestCase(VettelNativeSource.PaidBeer)] [TestCase(VettelNativeSource.GermanCornerExit)]
    public void SelectedSourceIsFixedAndOnlyFirstPositiveSettledEventCanGrantDriverBonus(VettelNativeSource source)
    {
        OwnTech(); Activate(source); if (source == VettelNativeSource.PaidBeer) Rhythm.MarkPaidBeer(race, owner);
        Assert.That(Rhythm.LockedSource, Is.EqualTo(source));
        foreach (VettelNativeSource wrong in new[] { VettelNativeSource.BasicCruise, VettelNativeSource.PaidBeer, VettelNativeSource.GermanCornerExit })
            if (wrong != source) Assert.That(Rhythm.ConsumeSettledSource(race, owner, wrong, 3), Is.Zero);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, source, 0), Is.Zero);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, source, -4), Is.Zero);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, source, 1), Is.EqualTo(2));
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, source, 2), Is.Zero);
        Assert.That(Rhythm.LockedSource, Is.EqualTo(source)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(1));
    }

    [Test]
    public void ThreePaidBeersKeepNativeSixPlusOnlyTwoRatherThanDoublingEachSource()
    {
        OwnTech(); Activate(VettelNativeSource.PaidBeer); int bonus = 0;
        for (int i = 0; i < 3; i++) { Assert.That(Rhythm.MarkPaidBeer(race, owner), Is.True); bonus += Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.PaidBeer, 2); }
        Assert.That(6 + bonus, Is.EqualTo(8)); Begin();
        Assert.That(Rhythm.PreviewBonus(race, owner, false, false), Is.Zero);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.PaidBeer, 2), Is.Zero);
        Assert.That(Rhythm.BonusThisInput, Is.Zero, "The next input needs another actual paid beer");
    }

    [Test]
    public void ActivationFeeCannotMasqueradeAsBeerAndAnotherOwnerCannotMarkIt()
    {
        OwnTech(); Activate(VettelNativeSource.PaidBeer);
        Assert.That(Rhythm.PreviewBonus(race, owner, false, false), Is.Zero);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.PaidBeer, 2), Is.Zero);
        Assert.That(Rhythm.MarkPaidBeer(race, other), Is.False);
        Assert.That(Rhythm.MarkPaidBeer(race, owner), Is.True);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.PaidBeer, 2), Is.EqualTo(2));
    }

    [Test]
    public void RepeatedNativePreviewsAreReadOnlyAndDoNotConsumeOrChangeCards()
    {
        Activate(); int heat = owner.deck.heatPool.remaining, version = Rhythm.Version; var cards = owner.deck.Hand.ToArray();
        for (int i = 0; i < 8; i++) Assert.That(Rhythm.PreviewBonus(race, owner, true, true), Is.EqualTo(2));
        Assert.That(Rhythm.PreviewBonus(race, owner, false, true), Is.Zero);
        Assert.That(Rhythm.Version, Is.EqualTo(version)); Assert.That(Rhythm.BonusThisInput, Is.Zero);
        Assert.That(owner.deck.Hand, Is.EqualTo(cards)); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(heat));
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.EqualTo(2));
        Assert.That(Rhythm.PreviewBonus(race, owner, true, true), Is.Zero);
    }

    [Test]
    public void MissingSourceExpiresWithoutRefundOrExtendingTheWindow()
    {
        var plan = Activate(); Begin(); Assert.That(Rhythm.HasWindow, Is.True); Begin();
        Assert.That(Rhythm.HasWindow, Is.False); Assert.That(owner.deck.ContainsInHand(plan.PaidHeat), Is.True);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(1));
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.Zero);
        Assert.That(VettelRhythmActivationPlan.CanPrepare(race, owner, 1, Context, out _), Is.True);
    }

    [Test]
    public void CancellationAndSupersededDelegatesNeverSpendOrCloseNewRequest()
    {
        var cancelled = Plan(); cancelled.Cancel(); Assert.That(Commit(cancelled), Is.False);
        var old = Plan(); var latest = Plan(request: 8); old.Cancel();
        Assert.That(Commit(latest, request: 8), Is.True); Assert.That(Commit(old), Is.False);
        Assert.That(Commit(latest, request: 8), Is.False); old.Cancel();
        Assert.That(Rhythm.HasWindow, Is.True); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(1));
    }

    [Test]
    public void FixedFeeIgnoresBreadAndTemporaryHeatAndCannotPayPartially()
    {
        owner.trickState.schwarzbrotActive = true; owner.trickState.schwarzbrotRemaining = 2;
        owner.trickState.tempHeatAvailable = true;
        owner.deck.AddCardsToHand(new[] { new CardData(CardType.Heat, 0) { isTemp = true } });
        owner.deck.heatPool.remaining = 0;
        Assert.That(VettelRhythmActivationPlan.CanPrepare(race, owner, 1, Context, out _), Is.False);
        owner.deck.heatPool.remaining = 1; var plan = Activate();
        Assert.That(owner.deck.heatPool.remaining, Is.Zero); Assert.That(plan.PaidHeat.isTemp, Is.False);
        Assert.That(owner.trickState.schwarzbrotRemaining, Is.EqualTo(2)); Assert.That(owner.trickState.tempHeatAvailable, Is.True);
        Assert.That(owner.deck.CountTemporaryHeatOutsideEngine(), Is.EqualTo(1));
    }

    [TestCase("turn")] [TestCase("gear")] [TestCase("request")] [TestCase("hand-value")] [TestCase("hand-temp")]
    [TestCase("hand-trick")] [TestCase("draw-value")] [TestCase("discard")] [TestCase("receipts")] [TestCase("engine")]
    [TestCase("pool")] [TestCase("deck")] [TestCase("driver")] [TestCase("initialization")] [TestCase("team")]
    [TestCase("position")] [TestCase("lap")] [TestCase("tech-reference")] [TestCase("active-nodes")]
    [TestCase("unlocked-nodes")] [TestCase("borrowed")] [TestCase("tech-team")] [TestCase("trick-reference")]
    [TestCase("temp-heat")] [TestCase("bread")] [TestCase("bread-count")] [TestCase("input")]
    [TestCase("session")] [TestCase("capacity")] [TestCase("burst-version")] [TestCase("fuel-lap")]
    public void AnyChangedSnapshotRejectsWithoutAdditionalMutation(string changed)
    {
        OwnTech(); var plan = Plan(); var original = owner.deck;
        if (changed == "turn") turn++;
        if (changed == "hand-value") owner.deck.Hand[0].value++;
        if (changed == "hand-temp") owner.deck.Hand[0].isTemp = true;
        if (changed == "hand-trick") owner.deck.Hand[0].trickId = "changed";
        if (changed == "draw-value") owner.deck.DrawPile[0].value++;
        if (changed == "discard") owner.deck.DrawHeatFromPool(1);
        if (changed == "receipts") owner.heatPaidCardsThisTurn.Add(new CardData(CardType.Heat, 0));
        if (changed == "engine") owner.deck.heatPool.remaining--;
        if (changed == "pool") owner.deck.heatPool = new HeatPool(20);
        if (changed == "deck") owner.deck = owner.deck.CreatePresentationCopy();
        if (changed == "driver") owner.driverId = "de_michael_schumacher";
        if (changed == "initialization") owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        if (changed == "team") owner.teamId = TeamId.UK;
        if (changed == "position") owner.position++;
        if (changed == "lap") owner.lap++;
        if (changed == "tech-reference") owner.techState = owner.techState.CreatePresentationCopy();
        if (changed == "active-nodes") owner.techState.activeNodeIds.Remove("de-l1-schwarzbier-fuel");
        if (changed == "unlocked-nodes") owner.techState.unlockedNodeIds.Clear();
        if (changed == "borrowed") owner.techState.sunNeverSetsTarget = TeamId.JP;
        if (changed == "tech-team") owner.techState.teamId = TeamId.JP;
        if (changed == "trick-reference") owner.trickState = owner.trickState.CreatePresentationCopy();
        if (changed == "temp-heat") owner.trickState.tempHeatAvailable = true;
        if (changed == "bread") owner.trickState.schwarzbrotActive = true;
        if (changed == "bread-count") owner.trickState.schwarzbrotRemaining++;
        if (changed == "input") Begin();
        if (changed == "session") race = new RaceSession(new SystemRandomSource(3)) { Players = race.Players };
        if (changed == "capacity") owner.deck.SetRegionalCapacityBonus(1);
        if (changed == "burst-version") owner.techState.germanBurst.SealDeclaration();
        if (changed == "fuel-lap") owner.techState.schwarzbierFuelLastLap = owner.lap;
        int heat = original.heatPool.remaining, hand = original.HandCount, discard = original.DiscardPileCount, uses = owner.driverSkill.UsesRemaining;
        Assert.That(Commit(plan, gear: changed == "gear" ? 2 : 1, request: changed == "request" ? 4 : 3), Is.False);
        Assert.That(original.heatPool.remaining, Is.EqualTo(heat)); Assert.That(original.HandCount, Is.EqualTo(hand));
        Assert.That(original.DiscardPileCount, Is.EqualTo(discard)); Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [Test]
    public void CanPrepareAndUnpaidPlanDoNotMutateResourcesOrActivate()
    {
        int version = Rhythm.Version; var cards = owner.deck.Hand.ToArray();
        for (int i = 0; i < 5; i++) Assert.That(VettelRhythmActivationPlan.CanPrepare(race, owner, 1, Context, out _), Is.True);
        Assert.That(Rhythm.Version, Is.EqualTo(version)); var p = Plan(); p.Cancel();
        Assert.That(owner.deck.Hand, Is.EqualTo(cards)); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(20));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2)); Assert.That(Rhythm.ManualPath, Is.False);
    }

    [TestCase("finish")] [TestCase("blown")] [TestCase("skip")] [TestCase("kanto")] [TestCase("pit")]
    [TestCase("ai")] [TestCase("disabled")] [TestCase("tier")] [TestCase("foreign")]
    public void IneligibleOwnerCannotPrepareOrKeepPaidWindow(string condition)
    {
        var plan = Activate();
        if (condition == "finish") owner.hasFinished = true;
        if (condition == "blown") owner.isBlown = true;
        if (condition == "skip") owner.skipNextTurn = true;
        if (condition == "kanto") owner.kantoOdenSkipThisTurn = true;
        if (condition == "pit") owner.pitStopScheduled = true;
        if (condition == "ai") owner.isAI = true;
        if (condition == "disabled") owner.driverSkill.Initialize(owner.DriverProfile, 7, false);
        if (condition == "tier") owner.driverSkill.Initialize(owner.DriverProfile, 2, true);
        if (condition == "foreign") race.Players.Remove(owner);
        int inputs = Rhythm.InputsStarted; Assert.That(Rhythm.BeginInput(race, owner), Is.False);
        Assert.That(Rhythm.InputsStarted, Is.EqualTo(inputs)); Assert.That(Rhythm.HasWindow, Is.False);
        Assert.That(VettelRhythmActivationPlan.CanPrepare(race, owner, 1, Context, out _), Is.False);
        Assert.That(owner.deck.ContainsInHand(plan.PaidHeat), Is.True); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
    }

    [Test]
    public void GlobalRoundsAndOtherOwnerDoNotSpendNextOwnerWindowAndDuplicateBeginCannotRearm()
    {
        Activate(); Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.EqualTo(2));
        Assert.That(Rhythm.BeginInput(race, owner), Is.False);
        int inputs = Rhythm.InputsStarted;
        for (int i = 0; i < 7; i++) { race.BeginTurn(other); Rhythm.BeginGlobalTurn(); }
        Assert.That(Rhythm.InputsStarted, Is.EqualTo(inputs)); Assert.That(Rhythm.HasWindow, Is.True);
        Assert.That(Rhythm.BeginInput(race, owner), Is.True);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.EqualTo(2));
        Assert.That(Rhythm.BeginInput(race, owner), Is.False);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.Zero);
    }

    [Test]
    public void InterruptionPreservesPaidMigrationAndDoesNotChangeGenericActiveDurationOrRefund()
    {
        Activate(); int generic = owner.driverSkill.ActiveTurnsRemaining, uses = owner.driverSkill.UsesRemaining;
        Rhythm.Interrupt(); Assert.That(Rhythm.ManualPath, Is.True); Assert.That(Rhythm.HasWindow, Is.False);
        Assert.That(owner.driverSkill.ActiveTurnsRemaining, Is.EqualTo(generic));
        Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(uses)); Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
        owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        Assert.That(Rhythm.ManualPath, Is.False); Assert.That(Rhythm.InputsStarted, Is.Zero);
    }

    [Test]
    public void IsolatedForecastConsumptionCannotUseRealWindowAndUnboundCopyCallsDoNotBreakRebinding()
    {
        Activate();
        var copy = new PlayerState("forecast", false, owner.position, owner.gear) { teamId = owner.teamId, driverId = owner.driverId,
            driverSkill = owner.driverSkill.CreatePresentationCopy(), deck = owner.deck.CreatePresentationCopy(), lap = owner.lap };
        var rhythm = copy.driverSkill.Rhythm;
        Assert.That(rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.Zero);
        Assert.That(rhythm.BeginInput(race, owner), Is.False); Assert.That(rhythm.HasWindow, Is.True);
        Assert.That(rhythm.RebindPresentationCopy(race, owner, copy), Is.True);
        Assert.That(rhythm.PreviewBonus(race, copy, true, false), Is.EqualTo(2));
        Assert.That(rhythm.ConsumeSettledSource(race, copy, VettelNativeSource.BasicCruise, 1), Is.EqualTo(2));
        rhythm.Interrupt(); Assert.That(Rhythm.HasWindow, Is.True); Assert.That(Rhythm.BonusThisInput, Is.Zero);
        Assert.That(Rhythm.ConsumeSettledSource(race, owner, VettelNativeSource.BasicCruise, 1), Is.EqualTo(2));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19)); Assert.That(copy.deck.heatPool, Is.Not.SameAs(owner.deck.heatPool));
    }

    [Test]
    public void ForecastRebindRejectsSharedDeckOrEngineAndOtherParticipantCannotTakeBonus()
    {
        Activate(); var copy = new PlayerState("bad forecast", false, owner.position, owner.gear) { teamId = owner.teamId,
            driverId = owner.driverId, driverSkill = owner.driverSkill.CreatePresentationCopy(), deck = owner.deck };
        Assert.That(copy.driverSkill.Rhythm.RebindPresentationCopy(race, owner, copy), Is.False);
        copy.deck = owner.deck.CreatePresentationCopy(); copy.deck.heatPool = owner.deck.heatPool;
        Assert.That(copy.driverSkill.Rhythm.RebindPresentationCopy(race, owner, copy), Is.False);
        Assert.That(Rhythm.PreviewBonus(race, other, true, true), Is.Zero);
        Assert.That(Rhythm.ConsumeSettledSource(race, other, VettelNativeSource.BasicCruise, 1), Is.Zero);
        Assert.That(Rhythm.HasWindow, Is.True); Assert.That(Rhythm.BonusThisInput, Is.Zero);
    }
}
