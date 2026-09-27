using System.Collections.Generic;

/// <summary>
/// Stores the track selected for the current application session.
/// It deliberately avoids mutating the shared GameConfigSO asset in Play Mode.
/// </summary>
public static class TrackSelectionState
{
    private static string selectedTrackId;

    /// <summary>Compatibility view of the shared official track catalog.</summary>
    public static IReadOnlyList<TrackSelectionOption> AvailableTracks => OfficialTrackCatalog.Tracks;

    /// <summary>The explicitly selected track ID, or an empty string when none is selected.</summary>
    public static string SelectedTrackId => selectedTrackId ?? string.Empty;

    /// <summary>Selects a known track.</summary>
    /// <returns><c>true</c> when the track exists in the catalog.</returns>
    public static bool TrySelect(string trackId)
    {
        if (!OfficialTrackCatalog.Contains(trackId))
        {
            return false;
        }

        selectedTrackId = trackId;
        return true;
    }

    /// <summary>
    /// Resolves the active track, preferring the session selection, then a known
    /// configured fallback, and finally the first catalog entry.
    /// </summary>
    public static string ResolveTrackId(string configuredFallback)
    {
        if (OfficialTrackCatalog.Contains(selectedTrackId))
        {
            return selectedTrackId;
        }

        if (OfficialTrackCatalog.Contains(configuredFallback))
        {
            return configuredFallback;
        }

        return OfficialTrackCatalog.Tracks[0].TrackId;
    }

    /// <summary>Clears the session selection. Primarily useful for deterministic tests.</summary>
    public static void Reset()
    {
        selectedTrackId = null;
    }
}
