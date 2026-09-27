using System.Collections.Generic;
using NUnit.Framework;

public sealed class RaceParticipantPlanBuilderTests
{
    [SetUp]
    public void SetUp() => DriverSelectionState.Reset();

    [TearDown]
    public void TearDown() => DriverSelectionState.Reset();

    [Test]
    public void TutorialRosterUsesAuthoredTeamAndSkipsDriverProgressionLoad()
    {
        TutorialScenarioDefinition tutorial = TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.US);
        int xpLoads = 0;

        List<RaceParticipantPlan> plans = Build(tutorial, null, null, TeamId.DE,
            new[] { TeamId.IT }, 3, _ => { xpLoads++; return 800; });

        Assert.That(plans, Has.Count.EqualTo(tutorial.opponentCount + 1));
        Assert.That(plans[0].Team, Is.EqualTo(TeamId.US));
        Assert.That(plans[0].Driver, Is.SameAs(DriverCatalog.GetDefaultForTeam(TeamId.US)));
        Assert.That(plans[0].Name, Is.EqualTo("你"));
        Assert.That(plans[0].InitialXp, Is.Zero);
        Assert.That(plans[0].CareerTechSnapshot, Is.Null);
        Assert.That(plans[1].Team, Is.EqualTo(tutorial.opponentTeam));
        Assert.That(plans[1].Name, Is.EqualTo("AI1"));
        Assert.That(xpLoads, Is.Zero);
    }

    [Test]
    public void FreeRosterPreservesPlayerFirstOrderAndSelectedDrivers()
    {
        DriverProfile us = DriverCatalog.GetDefaultForTeam(TeamId.US);
        DriverProfile it = DriverCatalog.GetDefaultForTeam(TeamId.IT);
        var roster = new[]
        {
            new FreeRaceRosterEntry(us.Team, us.Id),
            new FreeRaceRosterEntry(it.Team, it.Id)
        };

        List<RaceParticipantPlan> plans = Build(null, null, roster, TeamId.DE,
            new[] { TeamId.JP }, 0, _ => 123);

        Assert.That(plans, Has.Count.EqualTo(2));
        Assert.That(plans[0].Driver.Id, Is.EqualTo(us.Id));
        Assert.That(plans[0].Name, Is.EqualTo($"你 · {us.ShortName}"));
        Assert.That(plans[0].InitialXp, Is.EqualTo(123));
        Assert.That(plans[1].Driver.Id, Is.EqualTo(it.Id));
        Assert.That(plans[1].Name, Is.EqualTo($"{TeamCarPresentationRules.GetBadgeCode(it.Team)} · {it.ShortName}"));
    }

    [Test]
    public void CareerRosterUsesLockedPlayerAndOpponentOrderEvenWhenPlayerIsNotFirst()
    {
        var season = new CareerSeasonState();
        TeamId[] field = { TeamId.DE, TeamId.UK, TeamId.IT, TeamId.US };
        Assert.That(CareerModeRules.TryStartSeason(season, TeamId.UK, field), Is.True);
        Assert.That(CareerRaceLaunchRequest.TryCreate(season, "plan-test", out CareerRaceLaunchRequest career), Is.True);

        List<RaceParticipantPlan> plans = Build(null, career,
            new[] { new FreeRaceRosterEntry(TeamId.US, DriverCatalog.GetDefaultForTeam(TeamId.US).Id) },
            TeamId.CN, new[] { TeamId.JP }, 2, _ => 55);

        Assert.That(plans, Has.Count.EqualTo(4));
        Assert.That(plans[0].Team, Is.EqualTo(TeamId.UK));
        Assert.That(plans[0].CareerTechSnapshot, Is.Not.Null);
        Assert.That(plans[1].Team, Is.EqualTo(TeamId.DE));
        Assert.That(plans[2].Team, Is.EqualTo(TeamId.IT));
        Assert.That(plans[3].Team, Is.EqualTo(TeamId.US));
        Assert.That(plans[1].CareerTechSnapshot, Is.Null);
    }

    [Test]
    public void QuickRaceClampsOpponentCountAndFallsBackToConfiguredTeams()
    {
        DriverProfile quickDriver = DriverCatalog.GetDefaultForTeam(TeamId.CN);

        List<RaceParticipantPlan> plans = Build(null, null, null, TeamId.DE,
            new[] { TeamId.US }, 8, _ => 24, quickDriver);

        Assert.That(plans, Has.Count.EqualTo(4));
        Assert.That(plans[0].Team, Is.EqualTo(TeamId.CN));
        Assert.That(plans[1].Team, Is.EqualTo(TeamId.US));
        Assert.That(plans[2].Team, Is.EqualTo(TeamId.JP));
        Assert.That(plans[3].Team, Is.EqualTo(TeamId.JP));
    }

    private static List<RaceParticipantPlan> Build(
        TutorialScenarioDefinition tutorial,
        CareerRaceLaunchRequest career,
        FreeRaceRosterEntry[] roster,
        TeamId fallbackTeam,
        TeamId[] aiTeams,
        int opponentCount,
        System.Func<string, int> loadXp,
        DriverProfile quickDriver = null)
    {
        return RaceParticipantPlanBuilder.Build(
            tutorial,
            career,
            roster,
            quickDriver ?? DriverCatalog.GetDefaultForTeam(fallbackTeam),
            aiTeams,
            opponentCount,
            loadXp);
    }
}
