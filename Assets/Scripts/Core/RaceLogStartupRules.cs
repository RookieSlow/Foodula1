using System.Collections.Generic;

/// <summary>Read-only startup decisions for the race trace; the manager owns file output.</summary>
public static class RaceLogStartupRules
{
    public readonly struct TrackIdentity
    {
        public readonly string Id;
        public readonly string Name;

        public TrackIdentity(string id, string name)
        {
            Id = id;
            Name = name;
        }
    }

    public static TrackIdentity ResolveTrack(
        bool hasTrackManager, string activeTrackId,
        bool hasLoadedTrackConfig, string loadedTrackName, string fallbackTrackId)
    {
        string id = hasTrackManager ? activeTrackId : fallbackTrackId;
        return new TrackIdentity(id, hasLoadedTrackConfig ? loadedTrackName : id);
    }

    /// <summary>Preserves tutorial, then career, then free-race setup precedence.</summary>
    public static IEnumerable<string> BuildSetupLines(
        TutorialScenarioDefinition tutorial, TutorialRunPhase tutorialPhase,
        CareerRaceLaunchRequest career, FreeRaceRosterEntry[] freeRoster)
    {
        if (tutorial != null)
        {
            foreach (string line in RaceStateLogFormatter.BuildTutorialSetup(tutorial, tutorialPhase))
                yield return line;
        }
        else if (career != null)
        {
            yield return RaceStateLogFormatter.BuildCareerSetup(career);
        }
        else if (freeRoster != null)
        {
            yield return RaceStateLogFormatter.BuildFreeRaceSetup(freeRoster);
        }
    }
}
