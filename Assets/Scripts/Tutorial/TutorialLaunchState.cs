using System;

/// <summary>
/// Session-only launch request for the isolated tutorial race. This keeps the
/// tutorial override out of the shared GameConfigSO asset and leaves normal
/// quick-race selections untouched.
/// </summary>
public static class TutorialLaunchState
{
    private static TutorialScenarioDefinition requestedScenario;
    private static TutorialScenarioDefinition activeScenario;

    public static bool IsRequested => requestedScenario != null;
    public static bool IsActive => activeScenario != null;
    public static bool IsTutorialMode => IsRequested || IsActive;
    public static TutorialScenarioDefinition Scenario => activeScenario ?? requestedScenario;

    public static void Request(TutorialScenarioDefinition scenario)
    {
        requestedScenario = scenario ?? throw new ArgumentNullException(nameof(scenario));
        activeScenario = null;
    }

    public static TutorialScenarioDefinition ActivateRequested()
    {
        if (requestedScenario != null)
        {
            activeScenario = requestedScenario;
            requestedScenario = null;
        }

        return activeScenario;
    }

    public static string ResolveTrackId(string configuredFallback)
    {
        TutorialScenarioDefinition scenario = Scenario;
        return scenario != null
            ? scenario.trackId
            : TrackSelectionState.ResolveTrackId(configuredFallback);
    }

    public static void Clear()
    {
        requestedScenario = null;
        activeScenario = null;
    }
}
