using NUnit.Framework;

public class HeatPaymentCostTests
{
    private static PlayerState Player() => new PlayerState("driver", false, 0, 1);

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
