using NUnit.Framework;

public class MaAdaptationRuntimeTests
{
    private static PlayerState CreateMa(int level)
    {
        DriverCatalog.TryGet("cn_ma_qinghua", out DriverProfile profile);
        var player = new PlayerState("Ma", false, 0, 1)
        {
            driverId = profile.Id,
            teamId = TeamId.CN,
            gear = ChinaGearShiftRules.RecoverGear,
            usesChinaGearSystem = true
        };
        player.driverSkill.Initialize(profile, level, true);
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(4));
        player.deck.DrawToHand(1);
        player.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand);
        return player;
    }

    [Test]
    public void ExtremeSwitchReservesOldHandHeatAndPaysNextGoMovement()
    {
        PlayerState player = CreateMa(7);
        var session = new RaceSession();
        session.Players.Add(player);
        session.BeginTurn(player, 1);
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        Assert.That(session.TryDeclareMaPreparation(player, ChinaGearShiftRules.RecoverGear, 3, 1), Is.True);

        int engineBefore = player.deck.heatPool.remaining;
        Assert.That(session.TryCommitMaRecoverPreparation(player, 3,
            out HeatCoolingResult cooled, out CardData reserved), Is.True);
        Assert.That(cooled.Total, Is.Zero, "The only heat is the reserved physical hand card.");
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engineBefore));
        Assert.That(player.deck.ContainsInHand(reserved), Is.True);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(1));

        session.BeginTurn(player, 2);
        player.gear = ChinaGearShiftRules.GoGear;
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        Assert.That(session.ResolveMaSubmittedInput(player, completed: true), Is.True);
        Assert.That(DriverSkillRules.GetMovementBonus(player.driverSkill, false), Is.EqualTo(3));
        Assert.That(DriverSkillRules.GetMovementBonus(player.driverSkill, false), Is.EqualTo(3),
            "Movement preview reads the committed input without consuming it twice.");
    }

    [Test]
    public void AllRounderPassiveRequiresRecoverThenGoAndTierThreeHasTwoLapUses()
    {
        PlayerState player = CreateMa(7);
        var session = new RaceSession();
        session.Players.Add(player);

        session.BeginTurn(player, 1);
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        player.gear = ChinaGearShiftRules.RecoverGear;
        session.ResolveMaSubmittedInput(player, completed: true);

        session.BeginTurn(player, 2);
        player.gear = ChinaGearShiftRules.GoGear;
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        session.ResolveMaSubmittedInput(player, completed: true);
        Assert.That(player.driverSkill.IsMaPassiveApexReady, Is.True);
        Assert.That(player.driverSkill.ConsumeMaPassiveCornerLimitBonus(), Is.EqualTo(1));
        Assert.That(player.driverSkill.ConsumeMaPassiveCornerHeatReduction(), Is.EqualTo(1));

        session.BeginTurn(player, 3);
        player.gear = ChinaGearShiftRules.RecoverGear;
        session.BeginDriverSkillInput(player);
        session.ResolveMaSubmittedInput(player, completed: true);
        session.BeginTurn(player, 4);
        player.gear = ChinaGearShiftRules.GoGear;
        session.BeginDriverSkillInput(player);
        session.ResolveMaSubmittedInput(player, completed: true);
        Assert.That(player.driverSkill.IsMaPassiveApexReady, Is.True);
    }

    [Test]
    public void InvalidNextInputExpiresActiveRewardWithoutRefund()
    {
        PlayerState player = CreateMa(4);
        var session = new RaceSession();
        session.Players.Add(player);
        session.BeginTurn(player, 1);
        session.BeginDriverSkillInput(player);
        Assert.That(session.TryDeclareMaPreparation(player, ChinaGearShiftRules.RecoverGear, 3, 1), Is.True);
        Assert.That(session.TryCommitMaRecoverPreparation(player, 3, out _, out _), Is.True);
        int uses = player.driverSkill.UsesRemaining;

        session.BeginTurn(player, 2);
        player.gear = ChinaGearShiftRules.RecoverGear;
        session.BeginDriverSkillInput(player);
        Assert.That(session.ResolveMaSubmittedInput(player, completed: true), Is.True);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(DriverSkillRules.GetMovementBonus(player.driverSkill, false), Is.Zero);
    }
}
