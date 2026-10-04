using System;

/// <summary>Ordered driver-skill settlement; dynamic race facts and HUD remain injected adapters.</summary>
public static class RaceTurnSkillCleanup
{
    /// <summary>
    /// Commit the active cost before reporting it, then read live overtakes/runtime for
    /// the next-turn passive. Exceptions stop subsequent work without rolling back cost.
    /// Activation gates and cost formulas stay in DriverSkillRules.
    /// </summary>
    public static void Execute(PlayerState player,
        Func<PlayerState, int> getSpinMax,
        Func<PlayerState, int> getPassiveOvertakes,
        Action<PlayerState, int> reportFinalSprintCost)
    {
        if (DriverSkillRules.ShouldResolveFinalSprintCost(player?.driverSkill))
        {
            int spinMax = getSpinMax(player);
            FinalSprintTurnEndCost cost = DriverSkillRules.EvaluateFinalSprintCost(
                player.spinCounter, spinMax, player.driverSkill.Tier);
            player.spinCounter = cost.SpinCounter;
            if (cost.BlowsEngine)
                player.isBlown = true;
            else if (cost.RequiresRecovery)
                player.skipNextTurn = true;
            reportFinalSprintCost?.Invoke(player, spinMax);
        }

        int overtakes = getPassiveOvertakes(player);
        player.driverSkill?.ResolvePassiveTurnEnd(overtakes);
    }
}
