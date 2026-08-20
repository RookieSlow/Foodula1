using System.Collections.Generic;

/// <summary>
/// Pure-function pit lane rules — entry/exit detection, pit stop resolution.
/// JSON tracks already define pit_entry / pit_exit node types.
/// </summary>
public static class PitLaneRules
{
    /// <summary>Default pit stop duration in turns (car skips this many turns).</summary>
    public const int DEFAULT_PIT_DURATION = 1;

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
    /// newPos is the unnormalized forward target (oldPos + actual movement).
    /// </summary>
    public static bool CrossedPitEntry(int oldPos, int newPos, IReadOnlyList<TrackNode> nodes)
    {
        if (nodes == null || nodes.Count == 0)
            return false;

        int entry = FindPitEntry(nodes);
        if (entry < 0 || newPos <= oldPos)
            return false;

        // The race coordinator keeps the current position normalized, but a
        // movement target may continue past the end of the closed track. Use
        // the shared path sampler so pit detection and vehicle animation see
        // the same ordered nodes across one or more lap boundaries.
        foreach (int nodeIndex in TrackRules.GetCrossedNodeIndices(nodes.Count, oldPos, newPos))
        {
            if (nodes[nodeIndex].isPitEntry)
                return true;
        }

        return false;
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
    /// Resolve a pit stop. Returns the result with effects to apply.
    /// Sets skipNextTurn for the pit duration.
    /// </summary>
    public static PitStopResult EnterPit(PlayerState player, IReadOnlyList<TrackNode> nodes)
    {
        if (!CanEnterPit(player, nodes))
            return PitStopResult.Fail("Cannot enter pit");

        int exitPos = GetPitExitPosition(nodes);
        // Move car to pit exit
        player.position = exitPos;
        // Skip turns for pit duration
        player.skipNextTurn = true;

        return new PitStopResult
        {
            success = true,
            exitPosition = exitPos,
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
    /// <summary>Position after exiting pit.</summary>
    public int exitPosition;
    /// <summary>Amount of heat cooled (returned to engine).</summary>
    public int heatCooled;
    /// <summary>Number of turns skipped.</summary>
    public int turnsSkipped;

    public static PitStopResult Fail(string msg) => new PitStopResult { success = false, message = msg };
}
