using NUnit.Framework;

/// <summary>D06 Blue Luck owner-input cadence, heat reset, cooling and preview isolation.</summary>
public class BlueLuckTests
{
    private static DriverProfile Ascari
    {
        get
        {
            DriverCatalog.TryGet("it_alberto_ascari", out DriverProfile profile);
            return profile;
        }
    }

    private static DriverSkillRuntimeState Create(int level)
    {
        var state = new DriverSkillRuntimeState();
        state.Initialize(Ascari, level, true);
        return state;
    }

    private static void BeginOwnerTurn(DriverSkillRuntimeState state)
    {
        state.BeginGlobalTurn();
        Assert.That(state.BeginDrivingInput(), Is.True);
    }

    [Test]
    public void RulesUseDocumentedBlueLuckThresholdsAndTierThreeCooling()
    {
        Assert.That(DriverSkillRules.GetBlueLuckNoHeatTurnThreshold(1), Is.EqualTo(3));
        Assert.That(DriverSkillRules.GetBlueLuckNoHeatTurnThreshold(2), Is.EqualTo(2));
        Assert.That(DriverSkillRules.GetBlueLuckNoHeatTurnThreshold(3), Is.EqualTo(2));
        Assert.That(DriverSkillRules.GetPassiveCornerLimitBonus(DriverPassiveSkillId.BlueLuck, 1), Is.EqualTo(1));
        Assert.That(DriverSkillRules.GetPassiveCoolingBonus(DriverPassiveSkillId.BlueLuck, 2), Is.Zero);
        Assert.That(DriverSkillRules.GetPassiveCoolingBonus(DriverPassiveSkillId.BlueLuck, 3), Is.EqualTo(1));
    }

    [Test]
    public void TierOneTriggersAfterThreeConsecutiveNoHeatOwnerInputs()
    {
        var state = Create(2);
        BeginOwnerTurn(state);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.False);
        BeginOwnerTurn(state);
        BeginOwnerTurn(state);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.False);
        BeginOwnerTurn(state);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.True);
        Assert.That(state.ConsumePassiveCornerLimitBonus(), Is.EqualTo(1));
        Assert.That(state.ConsumePassiveCornerLimitBonus(), Is.Zero);
        Assert.That(state.PassiveCoolingBonusThisTurn, Is.Zero);
    }

    [Test]
    public void TierThreeTriggersCoolingAndPositiveHeatResetsTheStreak()
    {
        var state = Create(6);
        BeginOwnerTurn(state);
        BeginOwnerTurn(state);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.False);
        state.RecordHeatPaid(1);
        BeginOwnerTurn(state);
        Assert.That(state.BlueLuckNoHeatTurns, Is.Zero);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.False);
        BeginOwnerTurn(state);
        BeginOwnerTurn(state);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.True);
        Assert.That(state.PassiveCoolingBonusThisTurn, Is.EqualTo(1));
    }

    [Test]
    public void BlueLuckPresentationCopyPreservesPendingTriggerWithoutConsumingRealState()
    {
        var state = Create(4);
        BeginOwnerTurn(state);
        BeginOwnerTurn(state);
        BeginOwnerTurn(state);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.True);

        var preview = state.CreatePresentationCopy();
        Assert.That(preview.ConsumePassiveCornerLimitBonus(), Is.EqualTo(1));
        Assert.That(preview.ConsumePassiveCornerLimitBonus(), Is.Zero);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.True);
        Assert.That(state.ConsumePassiveCornerLimitBonus(), Is.EqualTo(1));
    }

    [Test]
    public void RaceSessionHeatTrackingFeedsBlueLuckWithoutTechnologyState()
    {
        var session = new RaceSession();
        var player = new PlayerState("Ascari", false, 0, 1) { teamId = TeamId.IT, driverId = Ascari.Id };
        player.driverSkill.Initialize(Ascari, 4, true);
        session.Players.Add(player);
        session.BeginTurn(player, 1);
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        session.TrackHeatPaid(player, 2);
        session.BeginTurn(player, 2);
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        Assert.That(player.driverSkill.BlueLuckNoHeatTurns, Is.Zero);

        session.BeginTurn(player, 3);
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        session.BeginTurn(player, 4);
        Assert.That(session.BeginDriverSkillInput(player), Is.True);
        Assert.That(player.driverSkill.IsPassiveCornerLimitBonusReady, Is.True);
        Assert.That(player.driverSkill.PassiveCoolingBonusThisTurn, Is.Zero);
    }

    [Test]
    public void InterruptedOwnerInputClearsBlueLuckCadenceAndHeatSample()
    {
        var state = Create(4);
        BeginOwnerTurn(state);
        BeginOwnerTurn(state);
        Assert.That(state.BlueLuckNoHeatTurns, Is.EqualTo(1));

        state.RecordHeatPaid(2);
        state.InterruptDrivingInput();

        Assert.That(state.BlueLuckNoHeatTurns, Is.Zero);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.False);

        BeginOwnerTurn(state);
        BeginOwnerTurn(state);
        BeginOwnerTurn(state);
        Assert.That(state.IsPassiveCornerLimitBonusReady, Is.True);
    }

    [Test]
    public void RaceSessionConsumesBlueLuckCornerBonusExactlyOnce()
    {
        var session = new RaceSession { Weather = WeatherType.Sunny, TeamVehicleBonusesEnabled = false };
        var player = new PlayerState("Ascari", false, 0, 1) { teamId = TeamId.IT, driverId = Ascari.Id };
        player.driverSkill.Initialize(Ascari, 4, true);
        session.Players.Add(player);

        for (int turn = 1; turn <= 3; turn++)
        {
            session.BeginTurn(player, turn);
            Assert.That(session.BeginDriverSkillInput(player), Is.True);
        }

        var preview = session.GetCornerLimitBreakdown(player, 3, false);
        Assert.That(preview.DriverModifier, Is.EqualTo(1));
        Assert.That(preview.EffectiveLimit, Is.EqualTo(4));
        Assert.That(player.driverSkill.IsPassiveCornerLimitBonusReady, Is.True);

        var resolved = session.GetCornerLimitBreakdown(player, 3, true);
        Assert.That(resolved.DriverModifier, Is.EqualTo(1));
        Assert.That(resolved.EffectiveLimit, Is.EqualTo(4));
        Assert.That(player.driverSkill.IsPassiveCornerLimitBonusReady, Is.False);

        var afterConsume = session.GetCornerLimitBreakdown(player, 3, true);
        Assert.That(afterConsume.DriverModifier, Is.Zero);
        Assert.That(afterConsume.EffectiveLimit, Is.EqualTo(3));
    }
}
