using NUnit.Framework;

public class RaceReactionCooldownTests
{
    private const int GearOneCooldown = 3;
    private const int GearTwoCooldown = 1;

    private static int Compute(RaceSession session, PlayerState player)
    {
        return session.ComputeReactionCooldown(player, GearOneCooldown, GearTwoCooldown);
    }

    [TestCase(1, 4)]
    [TestCase(2, 2)]
    [TestCase(3, 1)]
    public void StandardGearAndVehicleCoolingAreCombined(int gear, int expected)
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var player = new PlayerState("UK", false, 0, gear) { teamId = TeamId.UK };
        Assert.AreEqual(expected, Compute(session, player));
    }

    [Test]
    public void TutorialVehicleGateDoesNotRemoveGearCooling()
    {
        var session = new RaceSession(new SystemRandomSource(1))
        {
            TeamVehicleBonusesEnabled = false
        };
        var player = new PlayerState("UK", false, 0, 1) { teamId = TeamId.UK };
        Assert.AreEqual(3, Compute(session, player));
    }

    [Test]
    public void ChinaRecoverUsesConsecutiveCounterWithoutVehicleCooling()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var player = new PlayerState("CN", false, 0, ChinaGearShiftRules.RecoverGear)
        {
            teamId = TeamId.CN,
            chinaConsecutiveGearCount = 2
        };
        Assert.AreEqual(2, Compute(session, player));
    }

    [Test]
    public void RotorAndTemporaryShioAreSeparateSourcesWithoutDoubleCountingShio()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var player = new PlayerState("JP", false, 0, 3)
        {
            teamId = TeamId.JP,
            techState = new TechTreeState(TeamId.JP, 0)
        };
        player.techState.brothSelection = BrothType.Shio;
        player.techState.bankuruwaseActive = true;
        Assert.AreEqual(2, Compute(session, player));
        player.techState.bankuruwaseActive = false;
        Assert.AreEqual(1, Compute(session, player));
    }

    [Test]
    public void HotWeatherReducesCombinedCoolingAfterVehicleBonus()
    {
        var session = new RaceSession(new SystemRandomSource(1)) { Weather = WeatherType.Hot };
        var player = new PlayerState("UK", false, 0, 2) { teamId = TeamId.UK };
        Assert.AreEqual(1, Compute(session, player));
        player.gear = 3;
        session.TeamVehicleBonusesEnabled = false;
        Assert.AreEqual(0, Compute(session, player));
    }
}
