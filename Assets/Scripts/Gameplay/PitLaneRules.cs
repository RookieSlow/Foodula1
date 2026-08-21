using System.Collections.Generic;

/// <summary>
/// Pure-function pit lane rules — entry/exit detection, pit stop resolution.
/// JSON tracks already define pit_entry / pit_exit node types.
/// </summary>
public static class PitLaneRules
{
    /// <summary>Default pit stop duration in turns (car skips this many turns).</summary>
    public const int DEFAULT_PIT_DURATION = 1;

    /// <summary>
    /// Number of track cells used to represent the pit-lane transit.
    /// The car still skips one turn for servicing, then exits five cells after
    /// the authored pit entry instead of being teleported to an adjacent marker.
    /// </summary>
    public const int PIT_ADVANCE_CELLS = 5;

    /// <summary>Heat cooled during a standard pit stop.</summary>
    public const int PIT_HEAT_COOLDOWN = 999; // All heat returned to engine

    /// <summary>
    /// Check if a track has a functional pit lane (both entry and exit nodes).
    /// </summary>
    public static bool HasPitLane(IReadOnlyList<TrackNode> nodes)
    {
        return FindPitEntry(nodes) >= 0 && FindPitExit(nodes) >= 0;
    }

    /// <summary>Find the index of the pit entry node. Returns -1 if none.</summary>
    public static int FindPitEntry(IReadOnlyList<TrackNode> nodes)
    {
        if (nodes == null) return -1;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null && nodes[i].isPitEntry) return i;
        return -1;
    }

    /// <summary>Find the index of the pit exit node. Returns -1 if none.</summary>
    public static int FindPitExit(IReadOnlyList<TrackNode> nodes)
    {
        if (nodes == null) return -1;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null && nodes[i].isPitExit) return i;
        return -1;
    }

    /// <summary>
    /// Check if a car is at or has just passed the pit entry.
    /// </summary>
    public static bool IsAtPitEntry(int position, IReadOnlyList<TrackNode> nodes)
    {
        int entry = FindPitEntry(nodes);
        return entry >= 0 && position == entry;
    }

    /// <summary>
    /// Check if a car crossed the pit entry during its movement this turn.
    /// newPos is the unnormalized forward target (oldPos + actual movement).
    /// </summary>
    public static bool CrossedPitEntry(int oldPos, int newPos, IReadOnlyList<TrackNode> nodes)
    {
        // Keep the legacy pure-rule facade, but make the traversal event
        // snapshot the single source of truth for direct callers too.
        return TrackRules.GetTraversalEvents(nodes, oldPos, newPos).CrossedPitEntry;
    }

    /// <summary>
    /// Get the simulated position where the car exits the pit lane.
    /// The authored pit_exit node remains a validation/presentation marker;
    /// gameplay uses a fixed five-cell transit to model the pit-lane segment.
    /// </summary>
    public static int GetPitExitPosition(IReadOnlyList<TrackNode> nodes)
    {
        int entry = FindPitEntry(nodes);
        if (entry < 0 || nodes == null || nodes.Count == 0)
            return 0;

        return (entry + PIT_ADVANCE_CELLS) % nodes.Count;
    }

    /// <summary>
    /// Can the player enter the pit? Conditions:
    /// - Track has pit lane
    /// - Not already in pit
    /// - Not finished or blown
    /// </summary>
    public static bool CanEnterPit(PlayerState player, IReadOnlyList<TrackNode> nodes)
    {
        if (player == null) return false;
        if (player.isBlown || player.hasFinished) return false;
        if (player.skipNextTurn) return false; // Already pitting or spinning
        return HasPitLane(nodes);
    }

    /// <summary>
    /// Resolve a pit stop without mutating player state.
    /// The caller decides when to apply the returned transition.
    /// </summary>
    public static PitStopResult ResolvePitStop(PlayerState player, IReadOnlyList<TrackNode> nodes)
    {
        if (!CanEnterPit(player, nodes))
            return PitStopResult.Fail("Cannot enter pit");

        int exitPos = GetPitExitPosition(nodes);
        return new PitStopResult
        {
            success = true,
            exitPosition = exitPos,
            heatCooled = PIT_HEAT_COOLDOWN,
            turnsSkipped = DEFAULT_PIT_DURATION
        };
    }

    /// <summary>Apply a successful pit transition to the mutable race state.</summary>
    public static void ApplyPitStop(PlayerState player, PitStopResult result)
    {
        if (player == null || !result.success) return;

        player.position = result.exitPosition;
        player.skipNextTurn = result.turnsSkipped > 0;
    }

    /// <summary>
    /// Legacy one-step pit API. Kept for existing callers while new orchestration
    /// can explicitly separate pure resolution from state application.
    /// </summary>
    public static PitStopResult EnterPit(PlayerState player, IReadOnlyList<TrackNode> nodes)
    {
        PitStopResult result = ResolvePitStop(player, nodes);
        ApplyPitStop(player, result);
        return result;
    }

    /// <summary>
    /// China team unique: optional pit stop with reduced duration.
    /// Chinese team can choose to pit or not when passing pit entry.
    /// </summary>
    public static bool CanChinaSkipPit(TeamId teamId)
    {
        return teamId == TeamId.CN;
    }
}

/// <summary>Result of a pit stop.</summary>
public struct PitStopResult
{
    public bool success;
    public string message;
    /// <summary>Position after exiting pit.</summary>
    public int exitPosition;
    /// <summary>Amount of heat cooled (returned to engine).</summary>
    public int heatCooled;
    /// <summary>Number of turns skipped.</summary>
    public int turnsSkipped;

    public static PitStopResult Fail(string msg) => new PitStopResult { success = false, message = msg };
}
