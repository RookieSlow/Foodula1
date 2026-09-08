using System.Collections.Generic;
using NUnit.Framework;

public class FreeRaceRosterRulesTests
{
    [SetUp]
    public void SetUp()
    {
        FreeRaceRosterState.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        FreeRaceRosterState.Clear();
    }

    [Test]
    public void AvailableTeams_ContainsAllSixUniqueTeams()
    {
        IReadOnlyList<TeamId> teams = FreeRaceRosterRules.AvailableTeams;
        var unique = new HashSet<TeamId>(teams);

        Assert.That(teams.Count, Is.EqualTo(6));
        Assert.That(unique.Count, Is.EqualTo(teams.Count));
    }

    [Test]
    public void Validate_RequiresTwoToSixTeams()
    {
        var oneTeam = new List<TeamId> { TeamId.CN };
        var drivers = new Dictionary<TeamId, string>
        {
            { TeamId.CN, DriverCatalog.GetDefaultForTeam(TeamId.CN).Id }
        };

        Assert.That(FreeRaceRosterRules.TryValidate(oneTeam, drivers, out string error), Is.False);
        Assert.That(error, Does.Contain("2-6"));
    }

    [Test]
    public void Validate_RejectsWrongTeamDriverAndDuplicateTeam()
    {
        var duplicateTeams = new List<TeamId> { TeamId.CN, TeamId.CN };
        var duplicateDrivers = new Dictionary<TeamId, string>
        {
            { TeamId.CN, DriverCatalog.GetDefaultForTeam(TeamId.CN).Id }
        };
        Assert.That(FreeRaceRosterRules.TryValidate(duplicateTeams, duplicateDrivers, out _), Is.False);

        var teams = new List<TeamId> { TeamId.CN, TeamId.UK };
        var wrongAssignment = new Dictionary<TeamId, string>
        {
            { TeamId.CN, DriverCatalog.GetDefaultForTeam(TeamId.UK).Id },
            { TeamId.UK, DriverCatalog.GetDefaultForTeam(TeamId.UK).Id }
        };
        Assert.That(FreeRaceRosterRules.TryValidate(teams, wrongAssignment, out string error), Is.False);
        Assert.That(error, Does.Contain("CN"));
    }

    [Test]
    public void BuildEntries_PreservesSelectionOrderAndDriverAssignments()
    {
        var teams = new List<TeamId> { TeamId.JP, TeamId.US, TeamId.CN };
        var drivers = new Dictionary<TeamId, string>
        {
            { TeamId.JP, DriverCatalog.GetDefaultForTeam(TeamId.JP).Id },
            { TeamId.US, DriverCatalog.GetDefaultForTeam(TeamId.US).Id },
            { TeamId.CN, DriverCatalog.GetDefaultForTeam(TeamId.CN).Id }
        };

        FreeRaceRosterEntry[] entries = FreeRaceRosterRules.BuildEntries(teams, drivers);

        Assert.That(entries.Length, Is.EqualTo(3));
        Assert.That(entries[0].TeamId, Is.EqualTo(TeamId.JP));
        Assert.That(entries[1].TeamId, Is.EqualTo(TeamId.US));
        Assert.That(entries[2].DriverId, Is.EqualTo(drivers[TeamId.CN]));
    }

    [Test]
    public void State_DefaultFieldKeepsPrimaryDriverAndBuildsFourCars()
    {
        DriverProfile primary = DriverCatalog.TryGet("jp_takumi_fujiwara", out DriverProfile selected)
            ? selected
            : DriverCatalog.GetDefaultForTeam(TeamId.JP);
        FreeRaceRosterState.InitializeDefault(primary, new[] { TeamId.UK, TeamId.DE, TeamId.IT });

        Assert.That(FreeRaceRosterState.TryBuildRoster(out FreeRaceRosterEntry[] roster, out string error), Is.True, error);
        Assert.That(roster.Length, Is.EqualTo(4));
        Assert.That(roster[0].TeamId, Is.EqualTo(TeamId.JP));
        Assert.That(roster[0].DriverId, Is.EqualTo(primary.Id));
    }

    [Test]
    public void State_DefaultFieldFillsFourthTeamWhenPrimaryOverlapsFallback()
    {
        DriverProfile primary = DriverCatalog.GetDefaultForTeam(TeamId.UK);
        FreeRaceRosterState.InitializeDefault(primary, new[] { TeamId.UK, TeamId.DE, TeamId.IT });

        Assert.That(FreeRaceRosterState.TryBuildRoster(out FreeRaceRosterEntry[] roster, out string error), Is.True, error);
        Assert.That(roster.Length, Is.EqualTo(4));
        Assert.That(roster[0].TeamId, Is.EqualTo(TeamId.UK));
        Assert.That(roster[3].TeamId, Is.EqualTo(TeamId.US));
    }

    [Test]
    public void State_AllowsSixTeamsAndKeepsTheSelectionBounded()
    {
        FreeRaceRosterState.InitializeDefault(
            DriverCatalog.GetDefaultForTeam(TeamId.CN),
            new[] { TeamId.UK, TeamId.DE, TeamId.IT });
        Assert.That(FreeRaceRosterState.TrySetTeamSelected(TeamId.US, true), Is.True);
        Assert.That(FreeRaceRosterState.TrySetTeamSelected(TeamId.JP, true), Is.True);
        Assert.That(FreeRaceRosterState.SelectedCount, Is.EqualTo(6));

        Assert.That(FreeRaceRosterState.TrySetTeamSelected(TeamId.CN, false), Is.True);
        Assert.That(FreeRaceRosterState.TrySetTeamSelected(TeamId.CN, true), Is.True);
        Assert.That(FreeRaceRosterState.SelectedCount, Is.EqualTo(6));
    }

    [Test]
    public void Thunderstorm_BuildsAllTwelveDriversWithPlayerFirst()
    {
        DriverProfile primary = DriverCatalog.TryGet("jp_takumi_fujiwara", out DriverProfile selected)
            ? selected
            : DriverCatalog.GetDefaultForTeam(TeamId.JP);

        Assert.That(FreeRaceRosterRules.TryBuildThunderstormEntries(
            primary.Id, out FreeRaceRosterEntry[] entries, out string error), Is.True, error);
        Assert.That(entries.Length, Is.EqualTo(FreeRaceRosterRules.ThunderstormParticipants));
        Assert.That(entries[0].DriverId, Is.EqualTo(primary.Id));
        Assert.That(new HashSet<string>(System.Array.ConvertAll(entries, entry => entry.DriverId)).Count,
            Is.EqualTo(12));

        var driversPerTeam = new Dictionary<TeamId, int>();
        foreach (FreeRaceRosterEntry entry in entries)
        {
            driversPerTeam.TryGetValue(entry.TeamId, out int count);
            driversPerTeam[entry.TeamId] = count + 1;
        }

        Assert.That(driversPerTeam.Count, Is.EqualTo(6));
        foreach (KeyValuePair<TeamId, int> team in driversPerTeam)
            Assert.That(team.Value, Is.EqualTo(2));
    }

    [Test]
    public void State_ThunderstormModeExposesFullFieldAndCanReturnToCompactMode()
    {
        DriverProfile primary = DriverCatalog.GetDefaultForTeam(TeamId.CN);
        FreeRaceRosterState.InitializeThunderstorm(primary);

        Assert.That(FreeRaceRosterState.IsThunderstorm, Is.True);
        Assert.That(FreeRaceRosterState.RosterCount, Is.EqualTo(12));
        Assert.That(FreeRaceRosterState.TryBuildRoster(out FreeRaceRosterEntry[] roster, out string error), Is.True, error);
        Assert.That(roster.Length, Is.EqualTo(12));
        Assert.That(roster[0].DriverId, Is.EqualTo(primary.Id));
        Assert.That(FreeRaceRosterState.TryAssignDriver(TeamId.CN,
            DriverCatalog.GetDefaultForTeam(TeamId.CN).Id), Is.False);

        FreeRaceRosterState.InitializeDefault(primary, new[] { TeamId.UK, TeamId.DE, TeamId.IT });
        Assert.That(FreeRaceRosterState.IsThunderstorm, Is.False);
        Assert.That(FreeRaceRosterState.RosterCount, Is.EqualTo(4));
    }
}
