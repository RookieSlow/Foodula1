using System;

/// <summary>Ordered scheduled-stop settlement; scene effects and exit configuration are injected.</summary>
public static class RacePitStopExecution
{
    /// <summary>
    /// The A1 caller owns the skipped-turn collection. Required adapters execute in order,
    /// read live state and propagate exceptions without completing later mutations.
    /// </summary>
    public static PitStopResult Execute(
        PlayerState player,
        Func<PlayerState, PitStopResult> resolveExit,
        Action<PlayerState> recoverHeat,
        Func<PlayerState, int> getRecoveryGear)
    {
        if (player == null) return PitStopResult.Fail("Cannot enter pit");

        player.pitStopScheduled = false;
        player.skipNextTurn = false;
        PitStopResult result = resolveExit(player);
        if (!result.success) return result;

        recoverHeat(player);
        player.gear = getRecoveryGear(player);
        player.chinaConsecutiveGearCount = 0;
        player.pitChoiceResolvedThisLap = false;
        return result;
    }
}
