using System;
using System.Collections.Generic;

/// <summary>
/// Describes one track that can be selected from the main menu.
/// </summary>
public readonly struct TrackSelectionOption
{
    /// <summary>Creates a selectable track definition.</summary>
    public TrackSelectionOption(string trackId, string displayName, string subtitle)
    {
        TrackId = trackId;
        DisplayName = displayName;
        Subtitle = subtitle;
    }

    /// <summary>The Resources JSON identifier for the track.</summary>
    public string TrackId { get; }

    /// <summary>The primary player-facing track name.</summary>
    public string DisplayName { get; }

    /// <summary>The secondary country and food-theme label.</summary>
    public string Subtitle { get; }
}

/// <summary>
/// Stores the track selected for the current application session.
/// It deliberately avoids mutating the shared GameConfigSO asset in Play Mode.
/// </summary>
public static class TrackSelectionState
{
    private static readonly TrackSelectionOption[] Options =
    {
        new TrackSelectionOption("silverstone_afternoon_tea", "银石赛道", "英国 · 下午茶"),
        new TrackSelectionOption("nurburgring_bier", "纽博格林大奖赛道", "德国 · 啤酒"),
        new TrackSelectionOption("monza_pasta", "蒙扎赛道", "意大利 · 意面"),
        new TrackSelectionOption("indianapolis_burger", "印第安纳波利斯", "美国 · 汉堡"),
        new TrackSelectionOption("shanghai_dim_sum", "上海国际赛车场", "中国 · 点心"),
        new TrackSelectionOption("suzuka_sushi", "铃鹿赛道", "日本 · 寿司"),
        new TrackSelectionOption("nurburgring_24h_endurance", "纽博格林北环", "德国 · 绿色地狱"),
        new TrackSelectionOption("le_mans_old_mulsanne", "勒芒旧慕尚赛道", "法国 · 经典直道")
    };

    private static readonly HashSet<string> KnownTrackIds = BuildKnownTrackIds();
    private static string selectedTrackId;

    /// <summary>All tracks shown by the selection menu.</summary>
    public static IReadOnlyList<TrackSelectionOption> AvailableTracks => Options;

    /// <summary>The explicitly selected track ID, or an empty string when none is selected.</summary>
    public static string SelectedTrackId => selectedTrackId ?? string.Empty;

    /// <summary>Selects a known track.</summary>
    /// <returns><c>true</c> when the track exists in the catalog.</returns>
    public static bool TrySelect(string trackId)
    {
        if (string.IsNullOrEmpty(trackId) || !KnownTrackIds.Contains(trackId))
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
        if (!string.IsNullOrEmpty(selectedTrackId) && KnownTrackIds.Contains(selectedTrackId))
        {
            return selectedTrackId;
        }

        if (!string.IsNullOrEmpty(configuredFallback) && KnownTrackIds.Contains(configuredFallback))
        {
            return configuredFallback;
        }

        return Options[0].TrackId;
    }

    /// <summary>Clears the session selection. Primarily useful for deterministic tests.</summary>
    public static void Reset()
    {
        selectedTrackId = null;
    }

    private static HashSet<string> BuildKnownTrackIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (TrackSelectionOption option in Options)
        {
            ids.Add(option.TrackId);
        }

        return ids;
    }
}
