using System.Collections.Generic;
using NUnit.Framework;

public class DriverRulesTests
{
    [SetUp]
    public void SetUp() => DriverSelectionState.Reset();

    [TearDown]
    public void TearDown() => DriverSelectionState.Reset();

    [Test]
    public void CatalogContainsTwoUniqueDriversPerTeam()
    {
        IReadOnlyList<DriverProfile> drivers = DriverCatalog.All;
        var ids = new HashSet<string>();
        var perTeam = new Dictionary<TeamId, int>();
        foreach (DriverProfile driver in drivers)
        {
            ids.Add(driver.Id);
            perTeam[driver.Team] = perTeam.TryGetValue(driver.Team, out int count) ? count + 1 : 1;
            Assert.That(driver.TalentMultiplier, Is.GreaterThan(0f));
        }

        Assert.That(drivers.Count, Is.EqualTo(12));
        Assert.That(ids.Count, Is.EqualTo(drivers.Count));
        Assert.That(perTeam.Count, Is.EqualTo(6));
        foreach (KeyValuePair<TeamId, int> entry in perTeam) Assert.That(entry.Value, Is.EqualTo(2));
    }

    [TestCase(-1, 1)]
    [TestCase(0, 1)]
    [TestCase(99, 1)]
    [TestCase(100, 2)]
    [TestCase(249, 2)]
    [TestCase(250, 3)]
    [TestCase(4000, 7)]
    public void LevelUsesDocumentedXpThresholds(int xp, int expectedLevel)
    {
        Assert.That(DriverProgression.GetLevel(xp), Is.EqualTo(expectedLevel));
    }

    [Test]
    public void ActiveUsesUnlockAtLevelThreeAndUkGetsBonusUse()
    {
        Assert.That(DriverProgression.GetActiveUsesPerRace(2, TeamId.UK), Is.EqualTo(0));
        Assert.That(DriverProgression.GetActiveUsesPerRace(3, TeamId.CN), Is.EqualTo(1));
        Assert.That(DriverProgression.GetActiveUsesPerRace(3, TeamId.UK), Is.EqualTo(2));
        Assert.That(DriverProgression.GetActiveUsesPerRace(7, TeamId.UK), Is.EqualTo(3));
    }

    [Test]
    public void RaceXpAppliesPlacementTalentAndUkBonus()
    {
        Assert.That(DriverProgression.CalculateRaceXp(1, 1.0f, TeamId.CN), Is.EqualTo(150));
        Assert.That(DriverProgression.CalculateRaceXp(2, 1.5f, TeamId.DE), Is.EqualTo(165));
        Assert.That(DriverProgression.CalculateRaceXp(1, 1.0f, TeamId.UK), Is.EqualTo(180));
        Assert.That(DriverProgression.CalculateRaceXp(0, 2.0f, TeamId.UK), Is.EqualTo(0));
    }

    [Test]
    public void SelectionRejectsUnknownAndResolvesSelectedDriver()
    {
        Assert.That(DriverSelectionState.TrySelect("missing"), Is.False);
        Assert.That(DriverSelectionState.TrySelect("jp_takumi_fujiwara"), Is.True);
        Assert.That(DriverSelectionState.ResolveDriver(TeamId.CN).Id, Is.EqualTo("jp_takumi_fujiwara"));
        Assert.That(DriverSelectionState.ResolveDriver(TeamId.CN).Team, Is.EqualTo(TeamId.JP));
    }
}
