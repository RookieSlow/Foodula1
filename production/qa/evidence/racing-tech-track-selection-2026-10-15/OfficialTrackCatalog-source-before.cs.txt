using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

/// <summary>
/// Immutable definition for one official track. Session selection belongs to
/// <see cref="TrackSelectionState"/>; this value is shared catalog content.
/// </summary>
public readonly struct TrackSelectionOption
{
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
/// The single ordered source for official selectable track definitions.
/// Runtime selections and career calendars consume this catalog without
/// owning or mutating its contents.
/// </summary>
public static class OfficialTrackCatalog
{
    private static readonly ReadOnlyCollection<TrackSelectionOption> TracksInternal =
        Array.AsReadOnly(new[]
        {
            new TrackSelectionOption("silverstone_afternoon_tea", "银石赛道", "英国 · 下午茶"),
            new TrackSelectionOption("nurburgring_bier", "纽博格林大奖赛道", "德国 · 啤酒"),
            new TrackSelectionOption("monza_pasta", "蒙扎赛道", "意大利 · 意面"),
            new TrackSelectionOption("indianapolis_burger", "印第安纳波利斯", "美国 · 汉堡"),
            new TrackSelectionOption("shanghai_dim_sum", "上海国际赛车场", "中国 · 点心"),
            new TrackSelectionOption("suzuka_sushi", "铃鹿赛道", "日本 · 寿司"),
            new TrackSelectionOption("nurburgring_24h_endurance", "纽博格林北环", "德国 · 绿色地狱"),
            new TrackSelectionOption("le_mans_old_mulsanne", "勒芒旧慕尚赛道", "法国 · 经典直道")
        });

    private static readonly HashSet<string> TrackIds = BuildTrackIds();

    /// <summary>Official tracks in the stable order used by menus and career.</summary>
    public static IReadOnlyList<TrackSelectionOption> Tracks => TracksInternal;

    /// <summary>Returns whether an exact, non-empty official ID is present.</summary>
    public static bool Contains(string trackId)
    {
        return !string.IsNullOrEmpty(trackId) && TrackIds.Contains(trackId);
    }

    private static HashSet<string> BuildTrackIds()
    {
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < TracksInternal.Count; i++)
        {
            string trackId = TracksInternal[i].TrackId;
            if (string.IsNullOrEmpty(trackId) || !ids.Add(trackId))
                throw new InvalidOperationException("Official track IDs must be non-empty and unique.");
        }

        return ids;
    }
}
