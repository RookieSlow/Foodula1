using System.Collections.Generic;

/// <summary>
/// Pure lane-occupancy rules for rendering cars side by side.
/// Track-specific lane counts remain in <see cref="TrackPresentationRules"/>.
/// </summary>
public static class RaceLaneRules
{
    /// <summary>Returns the existing adjacent lane; negative means outside, positive inside.</summary>
    public static int GetAdjacentLane(int lane, int laneCount, int direction)
    {
        if (direction == 0) return lane;
        if (laneCount <= 1) return 0;
        return direction > 0 ? System.Math.Max(0, lane - 1) : System.Math.Min(laneCount - 1, lane + 1);
    }

    /// <summary>A blocked directional choice keeps the input gate open; keeping is accepted.</summary>
    public static bool TryChooseLane(int lane, int laneCount, int direction, out int selectedLane)
    {
        selectedLane = GetAdjacentLane(lane, laneCount, direction);
        return direction == 0 || selectedLane != lane;
    }

    /// <summary>
    /// Returns true when a car shares a lap/cell with an earlier active car and
    /// therefore renders on the outside lane on ordinary tracks.
    /// </summary>
    public static bool IsTrailingInParallel(
        PlayerState candidate,
        IReadOnlyList<PlayerState> players,
        int candidateIndex)
    {
        if (candidate == null || players == null || candidateIndex < 0 ||
            candidate.hasFinished || candidate.isBlown)
        {
            return false;
        }

        for (int i = 0; i < players.Count && i < candidateIndex; i++)
        {
            PlayerState other = players[i];
            if (other == null || other == candidate || other.hasFinished || other.isBlown)
                continue;

            if (other.lap == candidate.lap && other.position == candidate.position)
                return true;
        }

        return false;
    }
}
