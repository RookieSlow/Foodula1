using System.Collections.Generic;

/// <summary>
/// Pure lane-occupancy rules for rendering cars side by side.
/// Track-specific lane counts remain in <see cref="TrackPresentationRules"/>.
/// </summary>
public static class RaceLaneRules
{
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
