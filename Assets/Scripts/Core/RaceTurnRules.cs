using System.Collections.Generic;

/// <summary>Pure participation rules shared by the turn coordinator and tests.</summary>
public static class RaceTurnRules
{
    /// <summary>Returns whether gameplay must no longer mutate this participant.</summary>
    public static bool IsTerminal(PlayerState player)
    {
        return player == null || player.isBlown || player.hasFinished;
    }

    /// <summary>Returns whether a participant has a turn-scoped skip flag.</summary>
    public static bool ShouldSkip(PlayerState player)
    {
        return player != null && (player.skipNextTurn || player.kantoOdenSkipThisTurn);
    }

    /// <summary>
    /// Returns whether a participant must be excluded from movement and resolution.
    /// The turn-skipped collection represents skip decisions already consumed in A1.
    /// </summary>
    public static bool IsInactive(PlayerState player, ICollection<PlayerState> turnSkipped)
    {
        if (player == null)
            return true;

        return (turnSkipped != null && turnSkipped.Contains(player)) ||
               ShouldSkip(player) ||
               IsTerminal(player);
    }
}
