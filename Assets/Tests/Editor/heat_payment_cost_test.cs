using NUnit.Framework;

public class HeatPaymentCostTests
{
    [TestCase(HeatPaymentDestination.Hand, 3, true)]
    [TestCase(HeatPaymentDestination.Discard, 3, true)]
    [TestCase(HeatPaymentDestination.Hand, 1, false)]
    [TestCase(HeatPaymentDestination.Discard, 1, false)]
    [TestCase(HeatPaymentDestination.Hand, 0, false)]
    public void DataPaymentTracksActualReceiptsAndMarksComboOnlyWhenSufficient(
        HeatPaymentDestination destination, int available, bool sufficient)
    {
        var player = Player();
        player.techState = new TechTreeState(TeamId.CN)
        { dimSumPlayedTrick = true, dimSumPlayedSpeed = true };
        player.deck.heatPool = new HeatPool(available);
        var session = new RaceSession(new SystemRandomSource(1));

        var result = RaceHeatPaymentExecution.Pay(player, session, 3, destination);

        Assert.AreEqual(available, result.Drawn);
        Assert.AreEqual(sufficient, result.Sufficient);
        Assert.IsFalse(result.UsedFishAndChips);
        Assert.AreEqual(available, player.techState.grillSpezialHeatPaidThisTurn);
        Assert.AreEqual(sufficient, player.techState.dimSumPaidHeat);
        Assert.AreEqual(available, player.heatPaidCardsThisTurn.Count);
        var target = destination == HeatPaymentDestination.Hand ? player.deck.Hand : player.deck.DiscardPile;
        CollectionAssert.AreEqual(player.heatPaidCardsThisTurn, target);
        Assert.AreEqual(0, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.spinCounter, "Insufficiency is returned; only the coordinator spins.");
    }

    [TestCase(HeatPaymentDestination.Hand)]
    [TestCase(HeatPaymentDestination.Discard)]
    public void FishAndChipsRecoversPartialPaymentOnceWithoutMarkingCombo(HeatPaymentDestination destination)
    {
        var player = Player();
        player.techState = new TechTreeState(TeamId.UK)
        { dimSumPlayedTrick = true, dimSumPlayedSpeed = true };
        player.techState.activeNodeIds.Add("uk-l1-fish-and-chips");
        player.deck.heatPool = new HeatPool(1);
        var session = new RaceSession(new SystemRandomSource(1));

        var first = RaceHeatPaymentExecution.Pay(player, session, 3, destination);

        Assert.AreEqual(1, first.Drawn);
        Assert.IsFalse(first.Sufficient);
        Assert.IsTrue(first.UsedFishAndChips);
        Assert.IsTrue(player.techState.fishAndChipsUsed);
        Assert.AreEqual(1, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountHeatInHand());
        Assert.IsEmpty(player.deck.DiscardPile);
        Assert.AreEqual(1, player.heatPaidCardsThisTurn.Count, "Recovery does not erase payment receipts.");
        Assert.AreEqual(1, player.techState.grillSpezialHeatPaidThisTurn);
        Assert.IsFalse(player.techState.dimSumPaidHeat);

        var second = RaceHeatPaymentExecution.Pay(player, session, 3, destination);
        Assert.IsFalse(second.Sufficient);
        Assert.IsFalse(second.UsedFishAndChips);
        Assert.AreEqual(0, player.deck.heatPool.remaining);
        Assert.AreEqual(2, player.techState.grillSpezialHeatPaidThisTurn);
        Assert.IsFalse(player.techState.dimSumPaidHeat);
        Assert.AreEqual(0, player.spinCounter);
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void NonpositiveDataPaymentDoesNotRequirePlayerOrSession(int amount)
    {
        var result = RaceHeatPaymentExecution.Pay(null, null, amount, HeatPaymentDestination.Hand);
        Assert.IsTrue(result.Sufficient);
        Assert.AreEqual(0, result.Drawn);
        Assert.IsFalse(result.UsedFishAndChips);
    }

    private static PlayerState Player() => new PlayerState("driver", false, 0, 1);

    [TestCase(HeatPaymentDestination.Hand)]
    [TestCase(HeatPaymentDestination.Discard)]
    public void FishWithoutPermanentRecoverySourceStillIgnoresInsufficiencyOnlyOnce(
        HeatPaymentDestination destination)
    {
        var player = Player();
        player.techState = new TechTreeState(TeamId.UK);
        player.techState.activeNodeIds.Add("uk-l1-fish-and-chips");
        player.deck.heatPool = new HeatPool(0);
        var temporary = CardData.CreateTempHeat();
        player.deck.AddCardsToHand(new[] { temporary });
        var session = new RaceSession(new SystemRandomSource(1));

        var first = RaceHeatPaymentExecution.Pay(player, session, 2, destination);
        var second = RaceHeatPaymentExecution.Pay(player, session, 2, destination);

        Assert.IsTrue(first.UsedFishAndChips);
        Assert.IsFalse(first.Sufficient);
        Assert.IsFalse(second.UsedFishAndChips);
        Assert.IsFalse(second.Sufficient);
        Assert.AreEqual(0, first.Drawn + second.Drawn);
        Assert.AreEqual(0, player.deck.heatPool.remaining);
        CollectionAssert.AreEqual(new[] { temporary }, player.deck.Hand);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
    }

    [TestCase(0, true, 1, 0, 1)]
    [TestCase(-1, true, 1, 0, 1)]
    [TestCase(1, true, 1, 1, 0)]
    [TestCase(3, true, 2, 2, 1)]
    [TestCase(3, false, 2, 3, 2)]
    public void BreadMinimumAndConsumptionMatchExistingCostOrder(
        int requested, bool armed, int charges, int expected, int remaining)
    {
        var player = Player();
        player.trickState.schwarzbrotActive = armed;
        player.trickState.schwarzbrotRemaining = charges;
        var cost = HeatPaymentCostRules.Resolve(player, requested);
        Assert.AreEqual(expected, cost.Amount);
        Assert.AreEqual(requested > 0 ? requested : 0, cost.BeforeBread);
        Assert.AreEqual(requested > 0 && armed, cost.BreadWasArmed);
        Assert.AreEqual(remaining, player.trickState.schwarzbrotRemaining);
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
    }

    [TestCase(0, 0, 1)]
    [TestCase(1, 0, 0)]
    [TestCase(3, 1, 0)]
    public void PassiveDiscountPrecedesBreadAndCannotBeConsumedTwice(int requested, int expected, int discountLeft)
    {
        var player = Player();
        DriverCatalog.TryGet("de_michael_schumacher", out var driver);
        player.driverSkill.Initialize(driver, 2, true);
        for (int i = 0; i < 3; i++) player.driverSkill.BeginTurn();
        player.trickState.schwarzbrotActive = true;
        player.trickState.schwarzbrotRemaining = 2;
        var cost = HeatPaymentCostRules.Resolve(player, requested);
        Assert.AreEqual(expected, cost.Amount);
        Assert.AreEqual(System.Math.Max(0, requested - 1), cost.BeforeBread);
        Assert.AreEqual(requested > 1 ? 1 : 2, player.trickState.schwarzbrotRemaining);
        Assert.AreEqual(discountLeft, player.driverSkill.ConsumePassiveHeatDiscount());
        Assert.AreEqual(0, player.driverSkill.ConsumePassiveHeatDiscount());
    }

    [TestCase(3, 6, 5)]
    [TestCase(5, 6, 5)]
    [TestCase(7, 5, 4)]
    public void ActiveMultiplierPrecedesBreadAndPreservesRounding(int level, int beforeBread, int expected)
    {
        var player = Player();
        DriverCatalog.TryGet("uk_hunter_hart", out var driver);
        player.driverSkill.Initialize(driver, level, true);
        Assert.IsTrue(player.driverSkill.TryActivate(driver,
            new DriverSkillActivationContext(true, 2, 3, 6, 6, 0), out _));
        player.trickState.schwarzbrotActive = true;
        player.trickState.schwarzbrotRemaining = 1;
        var cost = HeatPaymentCostRules.Resolve(player, 3);
        Assert.AreEqual(beforeBread, cost.BeforeBread);
        Assert.AreEqual(expected, cost.Amount);
        Assert.IsFalse(player.trickState.schwarzbrotActive);
    }
}
