using NUnit.Framework;

public class NuvolariSkillTests
{
    [TestCase(1, 2)]
    [TestCase(2, 3)]
    [TestCase(3, 3)]
    public void PassiveEntryThresholdAndOldHandReturnMatchLockedMatrix(int tier, int entryMax)
    {
        Assert.That(DriverSkillRules.GetNuvolariPassiveEntryEngineMax(tier), Is.EqualTo(entryMax));
        Assert.That(DriverSkillRules.GetNuvolariPassiveOldHandReturn(tier), Is.EqualTo(tier >= 3 ? 2 : 1));
    }

    [Test]
    public void ActiveUsesLowEngineAndReservesFinalSpinTolerance()
    {
        Assert.That(DriverCatalog.TryGet("it_tazio_nuvolari", out DriverProfile driver), Is.True);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);

        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 2, 6, 0, 1, 3), out _), Is.True);
        Assert.That(state.Nuvolari.HasPrepaidSpin, Is.True);
        Assert.That(DriverSkillRules.GetIgnoredCornerCount(state), Is.EqualTo(1));
        Assert.That(state.TryConsumeCornerIgnore(), Is.True);
        Assert.That(state.TryConsumeCornerIgnore(), Is.False);

        state.Initialize(driver, 7, true);
        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 2, 6, 0, 2, 3), out _), Is.False);
        state.Initialize(driver, 7, true);
        Assert.That(state.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 3, 6, 0, 0, 3), out _), Is.False);
    }

    [Test]
    public void SuccessfulOrdinaryPaymentReturnsOnlyOldPermanentHandHeat()
    {
        PlayerState player = CreatePlayer(3);
        RaceSession race = BeginInput(player);
        int before = player.deck.heatPool.remaining;

        RaceHeatPaymentResult payment = RaceHeatPaymentExecution.Pay(
            player, race, 1, HeatPaymentDestination.Hand, HeatPaymentSource.OrdinaryCorner);

        Assert.That(payment.Sufficient, Is.True);
        Assert.That(player.driverSkill.Nuvolari.LastRestored, Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(before));
        Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(1));
        Assert.That(race.RecordOrdinaryCornerPaymentSuccess(player, 1), Is.Zero,
            "The one-per-lap passive must not refund a second ordinary corner.");
    }

    [Test]
    public void PartialOrdinaryPaymentDoesNotRefundAndLeavesNormalFailureEvidence()
    {
        PlayerState player = CreatePlayer(3);
        RaceSession race = BeginInput(player);

        RaceHeatPaymentResult payment = RaceHeatPaymentExecution.Pay(
            player, race, 3, HeatPaymentDestination.Hand, HeatPaymentSource.OrdinaryCorner);

        Assert.That(payment.Sufficient, Is.False);
        Assert.That(payment.Drawn, Is.EqualTo(2));
        Assert.That(player.driverSkill.Nuvolari.LastRestored, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.Zero);
    }

    private static PlayerState CreatePlayer(int engine)
    {
        Assert.That(DriverCatalog.TryGet("it_tazio_nuvolari", out DriverProfile driver), Is.True);
        var player = new PlayerState("Nuvolari", false, 0, 1)
        {
            driverId = driver.Id,
            teamId = TeamId.IT
        };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 2) }, new HeatPool(engine));
        Assert.That(player.deck.DrawHeatFromPoolToHand(1), Is.EqualTo(1));
        player.driverSkill.Initialize(driver, 7, true);
        return player;
    }

    private static RaceSession BeginInput(PlayerState player)
    {
        var race = new RaceSession();
        race.Players.Add(player);
        race.BeginTurn(player, 1);
        Assert.That(race.BeginDriverSkillInput(player), Is.True);
        return race;
    }
}
