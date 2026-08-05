using System;
using System.Collections.Generic;

/// <summary>Session-only driver choice made from the main menu.</summary>
public static class DriverSelectionState
{
    private static string selectedDriverId;

    public static IReadOnlyList<DriverProfile> AvailableDrivers => DriverCatalog.All;
    public static string SelectedDriverId => selectedDriverId ?? string.Empty;

    public static bool TrySelect(string driverId)
    {
        if (!DriverCatalog.TryGet(driverId, out _)) return false;
        selectedDriverId = driverId;
        return true;
    }

    public static DriverProfile ResolveDriver(TeamId configuredTeam)
    {
        if (DriverCatalog.TryGet(selectedDriverId, out DriverProfile selected)) return selected;
        return DriverCatalog.GetDefaultForTeam(configuredTeam);
    }

    public static DriverProfile ResolveDriver(string configuredDriverId, TeamId configuredTeam)
    {
        if (DriverCatalog.TryGet(selectedDriverId, out DriverProfile selected)) return selected;
        if (DriverCatalog.TryGet(configuredDriverId, out DriverProfile configured)) return configured;
        return DriverCatalog.GetDefaultForTeam(configuredTeam);
    }

    public static void Reset()
    {
        selectedDriverId = null;
    }
}
