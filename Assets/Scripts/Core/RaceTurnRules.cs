using System.Collections.Generic;

/// <summary>Action selected before a participant's gear decision; execution remains in the coordinator.</summary>
public enum RaceTurnStartAction
{
    ExcludeTerminal,
    ExecuteScheduledPitStop,
    ResolveSkip,
    SelectGear
}

/// <summary>Pure participation rules shared by the turn coordinator and tests.</summary>
public static class RaceTurnRules
{
    /// <summary>Preserves A1 priority: terminal, scheduled pit, skip, then gear input. Does not consume flags.</summary>
    public static RaceTurnStartAction GetStartAction(PlayerState player)
    {
        if (IsTerminal(player)) return RaceTurnStartAction.ExcludeTerminal;
        if (player.pitStopScheduled) return RaceTurnStartAction.ExecuteScheduledPitStop;
        if (ShouldSkip(player)) return RaceTurnStartAction.ResolveSkip;
        return RaceTurnStartAction.SelectGear;
    }

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
