using System;
using System.Collections.Generic;

/// <summary>Resolved, scene-independent setup for one race participant.</summary>
public sealed class RaceParticipantPlan
{
    public bool IsHuman { get; }
    public TeamId Team { get; }
    public DriverProfile Driver { get; }
    public string Name { get; }
    public int InitialXp { get; }
    public CareerTechSnapshot CareerTechSnapshot { get; }

    internal RaceParticipantPlan(
        bool isHuman,
        TeamId team,
        DriverProfile driver,
        string name,
        int initialXp,
        CareerTechSnapshot careerTechSnapshot)
    {
        IsHuman = isHuman;
        Team = team;
        Driver = driver;
        Name = name;
        InitialXp = initialXp;
        CareerTechSnapshot = careerTechSnapshot;
    }
}

/// <summary>
/// Resolves the ordered race field from the active launch mode. Scene objects,
/// AI components, and player runtime state remain owned by MVPGameManager.
/// Precedence matches race-track resolution: tutorial, career, then quick race.
/// </summary>
public static class RaceParticipantPlanBuilder
{
    public static List<RaceParticipantPlan> Build(
        TutorialScenarioDefinition tutorial,
        CareerRaceLaunchRequest career,
        FreeRaceRosterEntry[] freeRaceRoster,
        DriverProfile quickRaceDriver,
        TeamId[] configuredAiTeams,
        int configuredAiCount,
        Func<string, int> loadDriverXp)
    {
        if (loadDriverXp == null) throw new ArgumentNullException(nameof(loadDriverXp));

        CareerRaceLaunchRequest activeCareer = tutorial == null ? career : null;
        FreeRaceRosterEntry[] activeRoster = tutorial == null && activeCareer == null
            ? freeRaceRoster
            : null;

        DriverProfile humanDriver = ResolveHumanDriver(tutorial, activeCareer, activeRoster, quickRaceDriver);
        string humanName = activeRoster != null
            ? $"你 · {humanDriver.ShortName}"
            : "你";
        int aiCount = ResolveOpponentCount(tutorial, activeCareer, activeRoster, configuredAiCount);

        var participants = new List<RaceParticipantPlan>(aiCount + 1)
        {
            new RaceParticipantPlan(
                true,
                humanDriver.Team,
                humanDriver,
                humanName,
                tutorial != null ? 0 : loadDriverXp(humanDriver.Id),
                activeCareer?.TechSnapshot)
        };

        for (int i = 0; i < aiCount; i++)
        {
            TeamId team = ResolveOpponentTeam(i, tutorial, activeCareer, activeRoster, configuredAiTeams);
            DriverProfile driver = ResolveOpponentDriver(i, team, activeRoster);
            string name = activeRoster != null
                ? $"{TeamCarPresentationRules.GetBadgeCode(team)} · {driver.ShortName}"
                : $"AI{i + 1}";
            participants.Add(new RaceParticipantPlan(false, team, driver, name, 0, null));
        }

        return participants;
    }

    private static DriverProfile ResolveHumanDriver(
        TutorialScenarioDefinition tutorial,
        CareerRaceLaunchRequest career,
        FreeRaceRosterEntry[] roster,
        DriverProfile quickRaceDriver)
    {
        if (tutorial != null)
            return DriverCatalog.GetDefaultForTeam(tutorial.playerTeam);
        if (career != null)
            return DriverCatalog.GetDefaultForTeam(career.PlayerTeam);
        if (roster != null && roster.Length > 0 &&
            DriverCatalog.TryGet(roster[0].DriverId, out DriverProfile rosterDriver))
            return rosterDriver;
        if (quickRaceDriver == null)
            throw new InvalidOperationException("Quick-race driver could not be resolved.");
        return quickRaceDriver;
    }

    private static int ResolveOpponentCount(
        TutorialScenarioDefinition tutorial,
        CareerRaceLaunchRequest career,
        FreeRaceRosterEntry[] roster,
        int configuredCount)
    {
        if (tutorial != null) return tutorial.opponentCount;
        if (career != null) return Math.Max(0, career.Competitors.Count - 1);
        if (roster != null) return Math.Max(0, roster.Length - 1);
        return Math.Max(0, Math.Min(3, configuredCount));
    }

    private static TeamId ResolveOpponentTeam(
        int index,
        TutorialScenarioDefinition tutorial,
        CareerRaceLaunchRequest career,
        FreeRaceRosterEntry[] roster,
        TeamId[] configuredTeams)
    {
        if (tutorial != null) return tutorial.opponentTeam;
        if (career != null)
        {
            int opponentIndex = 0;
            for (int i = 0; i < career.Competitors.Count; i++)
            {
                TeamId team = career.Competitors[i];
                if (team == career.PlayerTeam) continue;
                if (opponentIndex++ == index) return team;
            }
            return TeamId.JP;
        }
        if (roster != null) return roster[index + 1].TeamId;
        return configuredTeams != null && index < configuredTeams.Length
            ? configuredTeams[index]
            : TeamId.JP;
    }

    private static DriverProfile ResolveOpponentDriver(
        int index,
        TeamId team,
        FreeRaceRosterEntry[] roster)
    {
        if (roster != null && DriverCatalog.TryGet(roster[index + 1].DriverId, out DriverProfile driver))
            return driver;
        return DriverCatalog.GetDefaultForTeam(team);
    }
}
