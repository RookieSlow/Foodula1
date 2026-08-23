using System.Collections.Generic;

/// <summary>
/// Pure-function pit lane rules — entry/exit detection, pit stop resolution.
/// JSON tracks already define pit_entry / pit_exit node types.
/// </summary>
public static class PitLaneRules
{
    /// <summary>Default pit stop duration in turns (car skips this many turns).</summary>
    public const int DEFAULT_PIT_DURATION = 1;

    /// <summary>Default number of track cells gained after reaching pit exit.</summary>
    public const int DEFAULT_EXIT_MOVE_BONUS = 1;

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
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].isPitEntry) return i;
        return -1;
    }

    /// <summary>Find the index of the pit exit node. Returns -1 if none.</summary>
    public static int FindPitExit(IReadOnlyList<TrackNode> nodes)
    {
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].isPitExit) return i;
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
    /// </summary>
    public static bool CrossedPitEntry(int oldPos, int newPos, IReadOnlyList<TrackNode> nodes)
    {
        int entry = FindPitEntry(nodes);
        if (entry < 0) return false;
        return oldPos <= entry && newPos >= entry;
    }

    /// <summary>
    /// Get the position where the car exits the pit lane.
    /// </summary>
    public static int GetPitExitPosition(IReadOnlyList<TrackNode> nodes)
    {
        int exit = FindPitExit(nodes);
        return exit >= 0 ? exit : 0;
    }

    /// <summary>
    /// Can the player enter the pit? Conditions:
    /// - Track has pit lane
    /// - Not already in pit
    /// - Not finished or blown
    /// </summary>
    public static bool CanEnterPit(PlayerState player, IReadOnlyList<TrackNode> nodes)
    {
        if (player.isBlown || player.hasFinished) return false;
        if (player.skipNextTurn) return false; // Already pitting or spinning
        return HasPitLane(nodes);
    }

    /// <summary>
    /// Resolve a pit stop. The car still skips one turn, but exits one or more
    /// cells beyond the authored pit-exit marker to model a shortened time loss.
    /// Sets skipNextTurn for the pit duration.
    /// </summary>
    public static PitStopResult EnterPit(PlayerState player, IReadOnlyList<TrackNode> nodes,
        int exitMoveBonus = DEFAULT_EXIT_MOVE_BONUS)
    {
        if (!CanEnterPit(player, nodes))
            return PitStopResult.Fail("Cannot enter pit");

        int pitExitPosition = GetPitExitPosition(nodes);
        int appliedMoveBonus = exitMoveBonus < 0 ? 0 : exitMoveBonus;
        int exitPosition = (pitExitPosition + appliedMoveBonus) % nodes.Count;

        // Move car through the pit and slightly beyond the pit exit.
        player.position = exitPosition;
        // Skip turns for pit duration
        player.skipNextTurn = true;

        return new PitStopResult
        {
            success = true,
            pitExitPosition = pitExitPosition,
            exitPosition = exitPosition,
            exitMoveBonus = appliedMoveBonus,
            heatCooled = PIT_HEAT_COOLDOWN,
            turnsSkipped = DEFAULT_PIT_DURATION
        };
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
    /// <summary>Authored pit-exit node position.</summary>
    public int pitExitPosition;
    /// <summary>Position after exiting pit and applying the forward bonus.</summary>
    public int exitPosition;
    /// <summary>Cells advanced beyond the authored pit exit.</summary>
    public int exitMoveBonus;
    /// <summary>Amount of heat cooled (returned to engine).</summary>
    public int heatCooled;
    /// <summary>Number of turns skipped.</summary>
    public int turnsSkipped;

    public static PitStopResult Fail(string msg) => new PitStopResult { success = false, message = msg };
}
