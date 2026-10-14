using UnityEngine;

/// <summary>Start a fresh application session even when the Editor keeps the managed domain.</summary>
public static class RuntimeSessionReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void Reset()
    {
        TrackSelectionState.Reset();
        DriverSelectionState.Reset();
        FreeRaceRosterState.Clear();
        TutorialLaunchState.Clear();
        CareerRaceLaunchState.Clear();
        TechTreeProfileStore.ResetRuntimeCache();
        GameSettingsRuntime.ResetRuntimeCache();
    }
}
