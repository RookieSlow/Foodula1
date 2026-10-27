using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

/// <summary>D03 receipt-only tuning, independent owner-input clock and isolated forecast state.</summary>
public class SchumacherTuningTests
{
    private RaceSession race;
    private PlayerState owner, other;
    private SchumacherTuningRuntime Tuning => owner.driverSkill.Tuning;

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(19));
        owner = NewOwner(6);
        other = new PlayerState("Other", true, 0, 1) { teamId = TeamId.DE };
        race.Players.Add(owner); race.Players.Add(other);
    }

    private static PlayerState NewOwner(int level)
    {
        var player = new PlayerState("Schumacher", false, 0, 1)
            { teamId = TeamId.DE, driverId = "de_michael_schumacher", lap = 1 };
        player.driverSkill.Initialize(player.DriverProfile, level, true);
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(20));
        player.deck.DrawToHand(1);
        return player;
    }

    private void Begin()
    {
        race.BeginTurn(owner);
        Assert.That(Tuning.BeginInput(race, owner), Is.True);
    }

    private void Pay(int count)
    {
        int drawn = owner.deck.DrawHeatFromPool(count, HeatPaymentDestination.Hand, owner.heatPaidCardsThisTurn);
        Assert.That(drawn, Is.EqualTo(count));
        Tuning.RecordOrdinaryCornerPayment(race, owner, drawn);
    }

    [TestCase(2, 1, false)] [TestCase(2, 2, true)] [TestCase(2, 3, true)]
    [TestCase(4, 1, true)] [TestCase(6, 1, true)]
    public void OnlyActualCornerPaymentThresholdPreparesNextInput(int level, int paid, bool expected)
    {
        owner.driverSkill.Initialize(owner.DriverProfile, level, true); Begin(); Pay(paid);
        Assert.That(Tuning.HasPending, Is.EqualTo(expected));
        Assert.That(Tuning.ConsumeReduction(race, owner, 4), Is.Zero, "Never consume a newly earned mark in its earning input");
        Assert.That(Tuning.PaidThisInput, Is.EqualTo(paid));
        Begin();
        Assert.That(Tuning.ConsumeReduction(race, owner, 4), Is.EqualTo(expected ? 1 : 0));
        Assert.That(Tuning.ConsumeReduction(race, owner, 4), Is.Zero);
    }

    [TestCase(2, 1, true)] [TestCase(2, 2, false)]
    [TestCase(4, 1, true)] [TestCase(4, 2, false)]
    [TestCase(6, 1, true)] [TestCase(6, 2, true)] [TestCase(6, 3, false)]
    public void ExactFutureOwnerInputLifetime(int level, int inputsAfter, bool available)
    {
        owner.driverSkill.Initialize(owner.DriverProfile, level, true); Begin(); Pay(2);
        for (int i = 0; i < inputsAfter; i++) Begin();
        Assert.That(Tuning.ConsumeReduction(race, owner, 2), Is.EqualTo(available ? 1 : 0));
    }

    [Test]
    public void TwoActualCornersAccumulateButDuplicateReceiptCallbackCannotReachThreshold()
    {
        owner.driverSkill.Initialize(owner.DriverProfile, 2, true); Begin(); Pay(1);
        Tuning.RecordOrdinaryCornerPayment(race, owner, 1);
        Assert.That(Tuning.PaidThisInput, Is.EqualTo(1)); Assert.That(Tuning.HasPending, Is.False);
        Pay(1); Assert.That(Tuning.PaidThisInput, Is.EqualTo(2)); Assert.That(Tuning.HasPending, Is.True);
    }

    [Test]
    public void QualifyingCornerAfterOldMarkNeverStacksOrRenewsIt()
    {
        Begin(); Pay(1); int expiry = Tuning.ExpiresInput;
        owner.lap = 2; Begin(); Pay(1);
        Assert.That(Tuning.ExpiresInput, Is.EqualTo(expiry));
        Assert.That(Tuning.HasGrantedInLap(2), Is.False, "A blocked grant must not fabricate a second token or lap receipt");
        Begin(); Assert.That(Tuning.HasPending, Is.True);
        Begin(); Assert.That(Tuning.HasPending, Is.False);
    }

    [Test]
    public void ZeroCornerDoesNotConsumeThenFirstPositiveCornerConsumesExactlyOne()
    {
        Begin(); Pay(1); Begin();
        Assert.That(Tuning.ConsumeReduction(race, owner, 0), Is.Zero);
        Assert.That(Tuning.ConsumeReduction(race, owner, -4), Is.Zero);
        Assert.That(Tuning.HasPending, Is.True);
        Assert.That(Tuning.ConsumeReduction(race, owner, 1), Is.EqualTo(1));
        Assert.That(Tuning.HasPending, Is.False);
        Assert.That(Tuning.ConsumeReduction(race, owner, 10), Is.Zero);
    }

    [Test]
    public void SameLapCannotGrantAgainEvenAfterUseAndInterruption()
    {
        Begin(); Pay(1); Begin(); Assert.That(Tuning.ConsumeReduction(race, owner, 2), Is.EqualTo(1));
        Pay(1); Assert.That(Tuning.HasPending, Is.False);
        Tuning.Interrupt(); Begin(); Pay(1); Assert.That(Tuning.HasPending, Is.False);
        owner.lap = 2; Begin(); Pay(1); Assert.That(Tuning.HasPending, Is.True);
    }

    [Test]
    public void PaidAmountsDoNotAccumulateAcrossInputs()
    {
        owner.driverSkill.Initialize(owner.DriverProfile, 2, true); Begin(); Pay(1); Begin(); Pay(1);
        Assert.That(Tuning.PaidThisInput, Is.EqualTo(1)); Assert.That(Tuning.HasPending, Is.False);
    }

    [Test]
    public void GlobalRoundsOtherCarAndDuplicateBeginDoNotSpendOwnerLifetime()
    {
        Begin(); Pay(1); int input = Tuning.InputsStarted; int expiry = Tuning.ExpiresInput;
        Assert.That(Tuning.BeginInput(race, owner), Is.False);
        for (int i = 0; i < 8; i++) { race.BeginTurn(other); owner.driverSkill.BeginGlobalTurn(); }
        Assert.That(Tuning.InputsStarted, Is.EqualTo(input)); Assert.That(Tuning.HasPending, Is.True);
        Assert.That(Tuning.ExpiresInput, Is.EqualTo(expiry));
        Assert.That(Tuning.BeginInput(race, owner), Is.True);
        Assert.That(Tuning.ConsumeReduction(race, owner, 2), Is.EqualTo(1));
    }

    [TestCase("finish")] [TestCase("blown")] [TestCase("skip")]
    [TestCase("kanto")] [TestCase("pit")] [TestCase("ai")]
    [TestCase("disabled")] [TestCase("foreign")]
    public void IneligibleInputNeverAwardsOrUsesTuning(string condition)
    {
        Begin(); Pay(1); owner.driverSkill.BeginGlobalTurn();
        switch (condition)
        {
            case "finish": owner.hasFinished = true; break;
            case "blown": owner.isBlown = true; break;
            case "skip": owner.skipNextTurn = true; break;
            case "kanto": owner.kantoOdenSkipThisTurn = true; break;
            case "pit": owner.pitStopScheduled = true; break;
            case "ai": owner.isAI = true; break;
            case "disabled": owner.driverSkill.Initialize(owner.DriverProfile, 6, false); break;
            case "foreign": race.Players.Remove(owner); break;
        }
        Assert.That(Tuning.BeginInput(race, owner), Is.False);
        Assert.That(Tuning.HasPending, Is.False);
        Assert.That(Tuning.ConsumeReduction(race, owner, 2), Is.Zero);
    }

    [TestCase("deck")] [TestCase("pool")] [TestCase("driver")] [TestCase("race")]
    public void BindingChangeInvalidatesMarkWithoutRestoringGrant(string changed)
    {
        Begin(); Pay(1); Begin();
        switch (changed)
        {
            case "deck": owner.deck = owner.deck.CreatePresentationCopy(); break;
            case "pool": owner.deck.heatPool = new HeatPool(20); break;
            case "driver": owner.driverId = "de_sebastian_vettel"; break;
            case "race": race = new RaceSession(new SystemRandomSource(20)); race.Players.Add(owner); break;
        }
        Assert.That(Tuning.ConsumeReduction(race, owner, 1), Is.Zero);
        Assert.That(Tuning.HasPending, Is.False); Assert.That(Tuning.HasGrantedInLap(1), Is.True);
    }

    [TestCase(0)] [TestCase(-1)] [TestCase(100)]
    public void NoOrInvalidActualDrawnNeverCreatesPhantomCredit(int actual)
    {
        Begin(); Tuning.RecordOrdinaryCornerPayment(race, owner, actual);
        Assert.That(Tuning.HasPending, Is.False); Assert.That(Tuning.PaidThisInput, Is.Zero);
    }

    [TestCase("temp")] [TestCase("speed")] [TestCase("absent")]
    public void OnlyActualUniquePermanentReceiptStillOwnedByDeckQualifies(string kind)
    {
        Begin();
        CardData card = kind == "speed" ? new CardData(CardType.Speed, 1) : new CardData(CardType.Heat, 0);
        if (kind == "temp") card.isTemp = true;
        if (kind != "absent") owner.deck.AddCardsToHand(new[] { card });
        owner.heatPaidCardsThisTurn.Add(card);
        Tuning.RecordOrdinaryCornerPayment(race, owner, 1);
        Assert.That(Tuning.PaidThisInput, Is.Zero); Assert.That(Tuning.HasPending, Is.False);
    }

    [Test]
    public void ReceiptPrefixBeforeInputIsFrozenAndDuplicateListInstanceCannotCountTwice()
    {
        owner.driverSkill.Initialize(owner.DriverProfile, 2, true);
        owner.driverSkill.BeginGlobalTurn();
        owner.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand, owner.heatPaidCardsThisTurn);
        Assert.That(Tuning.BeginInput(race, owner), Is.True);
        Tuning.RecordOrdinaryCornerPayment(race, owner, 1); Assert.That(Tuning.PaidThisInput, Is.Zero);
        owner.heatPaidCardsThisTurn.Add(owner.heatPaidCardsThisTurn[0]);
        Tuning.RecordOrdinaryCornerPayment(race, owner, 1); Assert.That(Tuning.PaidThisInput, Is.Zero);
        owner.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand, owner.heatPaidCardsThisTurn);
        CardData fresh = owner.heatPaidCardsThisTurn.Last(); owner.heatPaidCardsThisTurn.Add(fresh);
        Tuning.RecordOrdinaryCornerPayment(race, owner, 2);
        Assert.That(Tuning.PaidThisInput, Is.EqualTo(1)); Assert.That(Tuning.HasPending, Is.False);
    }

    [Test]
    public void NormalClosePreservesFutureMarkButCannotCollectOrUseAfterInputCloses()
    {
        Begin(); Pay(1); Tuning.CloseInput();
        Tuning.RecordOrdinaryCornerPayment(race, owner, 1);
        Assert.That(Tuning.ConsumeReduction(race, owner, 1), Is.Zero);
        Assert.That(Tuning.HasPending, Is.True); Begin();
        Assert.That(Tuning.ConsumeReduction(race, owner, 1), Is.EqualTo(1));
    }

    [Test]
    public void InitializeCreatesFreshRuntimeButOrdinaryGlobalClockNeverArmsGenericDiscount()
    {
        Begin(); Pay(1); var old = Tuning;
        owner.driverSkill.Initialize(owner.DriverProfile, 6, true);
        Assert.That(Tuning, Is.Not.SameAs(old)); Assert.That(Tuning.HasPending, Is.False);
        for (int i = 0; i < 12; i++)
        {
            owner.driverSkill.BeginGlobalTurn();
            Assert.That(owner.driverSkill.ConsumePassiveHeatDiscount(), Is.Zero);
        }
        Assert.That(DriverSkillRules.GetPassiveTriggerInterval(DriverPassiveSkillId.EngineersTune, 3), Is.Zero);
        Assert.That(DriverSkillRules.GetPassiveHeatDiscount(DriverPassiveSkillId.EngineersTune, 3), Is.Zero);
        Assert.That(owner.driverSkill.UsesDrivingInputClock, Is.False, "Legacy active clock migration is out of this slice");
        Assert.That(owner.driverSkill.UsesPassiveDrivingInputClock, Is.True);
    }

    [Test]
    public void ArmedMarkCannotReduceAnyGenericGearFuelOrFixedSkillCost()
    {
        Begin(); Pay(1); Begin();
        Assert.That(HeatPaymentCostRules.Resolve(owner, 1).Amount, Is.EqualTo(1));
        Assert.That(HeatPaymentCostRules.Resolve(owner, 4).Amount, Is.EqualTo(4));
        Assert.That(Tuning.HasPending, Is.True);
        Assert.That(Tuning.ConsumeReduction(race, owner, 2), Is.EqualTo(1));
    }

    [Test]
    public void PresentationCopyConsumesOnlyCopiedMarkAndGrantSet()
    {
        Begin(); Pay(1); Begin();
        var copy = new PlayerState("Copy", false, owner.position, owner.gear)
        {
            teamId = owner.teamId, driverId = owner.driverId, lap = owner.lap,
            driverSkill = owner.driverSkill.CreatePresentationCopy(), deck = owner.deck.CreatePresentationCopy()
        };
        Assert.That(copy.driverSkill.Tuning.RebindPresentationCopy(race, owner, copy), Is.True);
        Assert.That(copy.driverSkill.Tuning.ConsumeReduction(race, copy, 1), Is.EqualTo(1));
        Assert.That(Tuning.HasPending, Is.True);
        copy.lap = 2;
        copy.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand, copy.heatPaidCardsThisTurn);
        copy.driverSkill.Tuning.RecordOrdinaryCornerPayment(race, copy, 1);
        Assert.That(copy.driverSkill.Tuning.HasGrantedInLap(2), Is.True);
        Assert.That(Tuning.HasGrantedInLap(2), Is.False);
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
        Assert.That(Tuning.ConsumeReduction(race, owner, 1), Is.EqualTo(1));
    }

    [Test]
    public void PresentationCopyReceiptDedupCollectionIsIndependent()
    {
        owner.driverSkill.Initialize(owner.DriverProfile, 2, true); Begin(); Pay(1);
        var copy = new PlayerState("Copy", false, owner.position, owner.gear)
        {
            teamId = owner.teamId, driverId = owner.driverId, lap = owner.lap,
            driverSkill = owner.driverSkill.CreatePresentationCopy(), deck = owner.deck.CreatePresentationCopy()
        };
        copy.heatPaidCardsThisTurn.AddRange(owner.heatPaidCardsThisTurn);
        Assert.That(copy.driverSkill.Tuning.RebindPresentationCopy(race, owner, copy), Is.True);
        copy.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand, copy.heatPaidCardsThisTurn);
        copy.driverSkill.Tuning.RecordOrdinaryCornerPayment(race, copy, 1);
        Assert.That(copy.driverSkill.Tuning.HasPending, Is.True);
        Assert.That(Tuning.HasPending, Is.False);
        Assert.That(Tuning.PaidThisInput, Is.EqualTo(1));
        Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(19));
    }
}
