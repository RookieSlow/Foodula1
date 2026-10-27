using NUnit.Framework;
using UnityEngine;

/// <summary>Production fuel commits and isolated forecasts; manual L1/L3 windows remain a later I03 slice.</summary>
public class SchwarzbierFixedPaymentTests
{
    private RaceSession race;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(7)) { TeamVehicleBonusesEnabled = false };
        player = new PlayerState("fixed fuel", false, 0, 1) {
            teamId = TeamId.DE, driverId = "de_michael_schumacher", techState = new TechTreeState(TeamId.DE) };
        player.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel");
        player.deck.heatPool = new HeatPool(4);
        race.Players.Add(player);
    }

    [TestCase(0)] [TestCase(1)] [TestCase(4)]
    public void FixedCommitPaysOneExactDiscardReceiptOncePerLap(int available)
    {
        player.deck.heatPool = new HeatPool(available);
        for (int i = 0; i < 3; i++) race.CanTriggerSchwarzbierFuel(player, true);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(available));
        Assert.That(player.techState.schwarzbierFuelLastLap, Is.EqualTo(-1));
        bool paid = race.TryCommitSchwarzbierFuel(player, true, out var receipt);
        Assert.That(paid, Is.EqualTo(available > 0));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(System.Math.Max(0, available - 1)));
        Assert.That(player.deck.Hand, Is.Empty);
        if (paid)
        {
            Assert.That(receipt.IsHeat && !receipt.isTemp, Is.True);
            Assert.That(player.deck.DiscardPile, Is.EquivalentTo(new[] { receipt }));
            Assert.That(player.heatPaidCardsThisTurn, Is.EquivalentTo(new[] { receipt }));
            Assert.That(player.techState.grillSpezialHeatPaidThisTurn, Is.EqualTo(1));
            race.BeginTurn(player);
            Assert.That(race.TryCommitSchwarzbierFuel(player, true, out var duplicate), Is.False);
            Assert.That(duplicate, Is.Null);
        }
        else
        {
            Assert.That(receipt, Is.Null);
            Assert.That(player.deck.DiscardPile, Is.Empty);
            Assert.That(player.heatPaidCardsThisTurn, Is.Empty);
            Assert.That(player.techState.schwarzbierFuelLastLap, Is.EqualTo(-1));
        }
    }

    [TestCase("disabled")] [TestCase("no-l1")] [TestCase("finished")]
    [TestCase("blown")] [TestCase("no-pool")] [TestCase("no-deck")]
    public void InvalidRequestsAreAtomicAndReturnNoReceipt(string reason)
    {
        bool enabled = reason != "disabled";
        if (reason == "no-l1") { player.techState.activeNodeIds.Clear(); player.techState.activeNodeIds.Add("de-l3-grill-spezial"); }
        player.hasFinished = reason == "finished"; player.isBlown = reason == "blown";
        if (reason == "no-pool") player.deck.heatPool = null;
        CardDeck original = player.deck;
        if (reason == "no-deck") player.deck = null;
        Assert.That(race.TryCommitSchwarzbierFuel(player, enabled, out var receipt), Is.False);
        Assert.That(receipt, Is.Null);
        Assert.That(original.DiscardPile, Is.Empty);
        Assert.That(player.heatPaidCardsThisTurn, Is.Empty);
        Assert.That(player.techState.schwarzbierFuelLastLap, Is.EqualTo(-1));
        Assert.That(player.techState.grillSpezialHeatPaidThisTurn, Is.Zero);
        if (original.heatPool != null) Assert.That(original.heatPool.remaining, Is.EqualTo(4));
    }

    [Test]
    public void HandOrTemporaryHeatCannotSubstituteForEmptyEngine()
    {
        player.deck.DrawHeatFromPoolToHand(4);
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out var receipt), Is.False);
        Assert.That(receipt, Is.Null);
        Assert.That(player.deck.HandCount, Is.EqualTo(5));
        Assert.That(player.spinCounter, Is.Zero);
        Assert.That(player.techState.schwarzbierFuelLastLap, Is.EqualTo(-1));
    }

    [Test]
    public void LastEngineHeatCanPayAndNextLapGetsANewOrdinaryUse()
    {
        player.deck.heatPool = new HeatPool(1);
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out var first), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.Zero);
        player.deck.CoolRecordedHeatThroughDiscard(new[] { first }, 1);
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.False);
        player.lap++;
        race.BeginTurn(player);
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out var second), Is.True);
        Assert.That(second, Is.Not.SameAs(first));
    }

    [Test]
    public void ImmunityDoesNotGrantFreeFuelAndOrdinaryImmunityStillWorks()
    {
        player.driverSkill.Initialize(player.DriverProfile, 3, true);
        Assert.That(player.driverSkill.TryActivate(player.DriverProfile,
            new DriverSkillActivationContext(true, 1, 3, 4, 4, 0), out var reason), Is.True, reason);
        Assert.That(DriverSkillRules.ReduceCornerHeat(player.driverSkill, 1), Is.Zero, "Confirm ordinary corner immunity.");
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(3));
        Assert.That(DriverSkillRules.ReduceCornerHeat(player.driverSkill, 2), Is.Zero);
    }

    [Test]
    public void FixedFuelDoesNotConsumePreparedOrdinaryCornerTuningOrBread()
    {
        player.driverSkill.Initialize(player.DriverProfile, 2, true);
        race.BeginTurn(player); Assert.That(race.BeginDriverSkillInput(player), Is.True);
        Assert.That(RaceHeatPaymentExecution.Pay(player, race, 2, HeatPaymentDestination.Hand, HeatPaymentSource.OrdinaryCorner).Drawn, Is.EqualTo(2));
        player.deck.CoolHeat(2);
        race.BeginTurn(player); Assert.That(race.BeginDriverSkillInput(player), Is.True);
        player.trickState.schwarzbrotActive = true; player.trickState.schwarzbrotRemaining = 2;
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(3));
        Assert.That(player.trickState.schwarzbrotRemaining, Is.EqualTo(2));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
        Assert.That(HeatPaymentCostRules.Resolve(player, 3).Amount, Is.EqualTo(2));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.True);
        Assert.That(player.trickState.schwarzbrotRemaining, Is.EqualTo(1));
        Assert.That(race.ResolveOverspeedHeatCost(player, 2), Is.EqualTo(1));
        Assert.That(player.driverSkill.Tuning.HasPending, Is.False);
    }

    [Test]
    public void BorrowingGermanL3DoesNotGrantItsMissingL1()
    {
        player.teamId = TeamId.UK; player.techState = new TechTreeState(TeamId.UK);
        player.techState.activeNodeIds.Add("uk-l3-sun-never-sets"); player.techState.sunNeverSetsTarget = TeamId.DE;
        Assert.That(race.GetModifiers(player).hasGrillSpezial, Is.True);
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out _), Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
    }

    [Test]
    public void SameTeamParticipantsKeepIndependentPaymentsAndLapUses()
    {
        var other = new PlayerState("second DE", false, 1, 1) { teamId = TeamId.DE,
            techState = new TechTreeState(TeamId.DE) };
        other.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel"); other.deck.heatPool = new HeatPool(2);
        race.Players.Add(other);
        Assert.That(race.TryCommitSchwarzbierFuel(player, true, out var first), Is.True);
        Assert.That(race.TryCommitSchwarzbierFuel(other, true, out var second), Is.True);
        Assert.That(first, Is.Not.SameAs(second));
        CollectionAssert.DoesNotContain(other.deck.DiscardPile, first);
        CollectionAssert.DoesNotContain(player.deck.DiscardPile, second);
        Assert.That(other.deck.heatPool.remaining, Is.EqualTo(1));
    }

    [Test]
    public void NullParticipantDoesNotPay()
    {
        Assert.That(race.TryCommitSchwarzbierFuel(null, true, out var receipt), Is.False);
        Assert.That(receipt, Is.Null);
    }

    [TestCase(false)] [TestCase(true)]
    public void RepeatedHumanForecastCannotChooseOrSpendUnconfirmedFuel(bool immune)
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            config.enableTechTree = true; config.gearOneCooldown = 0;
            var card = new CardData(CardType.Speed, 2); player.deck.AddCardsToHand(new[] { card });
            if (immune)
            {
                player.driverSkill.Initialize(player.DriverProfile, 3, true);
                Assert.That(player.driverSkill.TryActivate(player.DriverProfile,
                    new DriverSkillActivationContext(true, 1, 3, 4, 4, 0), out _), Is.True);
            }
            player.trickState.schwarzbrotActive = true; player.trickState.schwarzbrotRemaining = 2;
            for (int i = 0; i < 3; i++)
            {
                var preview = RaceTurnPreviewRules.Evaluate(player, race, config, new[] { card }, false, 1);
                Assert.That(preview.HeatPaid, Is.Zero);
                Assert.That(preview.Movement.TotalCells, Is.EqualTo(2));
                Assert.That(preview.ProjectedHeat.EngineRemaining, Is.EqualTo(4));
            }
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
            Assert.That(player.techState.schwarzbierFuelLastLap, Is.EqualTo(-1));
            Assert.That(player.techState.grillSpezialHeatPaidThisTurn, Is.Zero);
            Assert.That(player.heatPaidCardsThisTurn, Is.Empty);
            Assert.That(player.trickState.schwarzbrotRemaining, Is.EqualTo(2));
            Assert.That(card.value, Is.EqualTo(2));
        }
        finally { Object.DestroyImmediate(config); }
    }

    [TestCase(false)] [TestCase(true)]
    public void TerminalForecastDoesNotInventAFuelPayment(bool blown)
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            config.enableTechTree = true; config.gearOneCooldown = 0;
            player.isBlown = blown; player.hasFinished = !blown;
            var card = new CardData(CardType.Speed, 2);
            player.deck.AddCardsToHand(new[] { card });
            var preview = RaceTurnPreviewRules.Evaluate(player, race, config,
                new[] { card }, false, 1);
            Assert.That(preview.HeatPaid, Is.Zero);
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
        }
        finally { Object.DestroyImmediate(config); }
    }
}
