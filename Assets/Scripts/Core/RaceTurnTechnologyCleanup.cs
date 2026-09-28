using System;

/// <summary>Ordered technology settlement; Unity movement, cooling and logs remain injected adapters.</summary>
public static class RaceTurnTechnologyCleanup
{
    /// <summary>
    /// Called after RaceTurnCleanup's terminal/technology gate. Read each result only when
    /// needed: the first effect may change combo facts, drivetrain mode or finish status.
    /// Exceptions propagate and stop the remaining effects, as in the coordinator.
    /// </summary>
    public static void Execute(PlayerState player, RaceSession session,
        Action<PlayerState, YinYangResult> applyYinYang,
        Action<PlayerState> reportCombo,
        Action<PlayerState, int> applyGrillCooldown)
    {
        applyYinYang(player, session.ResolveEndOfTurn(player));
        if (TechTreeRules.CheckDimSumCombo(player.techState, session.TechDb))
        {
            reportCombo(player);
            applyYinYang(player, session.ResolveEndOfTurn(player));
        }

        int cooldown = session.GetGrillSpezialCooldown(player);
        if (cooldown > 0)
        {
            session.ActivateGrillSpezial(player);
            applyGrillCooldown(player, cooldown);
        }
    }
}
