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
        Execute(player, session, applyYinYang, null, reportCombo, applyGrillCooldown);
    }

    /// <summary>
    /// Ordered technology settlement with CN 温杯 inserted after ordinary
    /// movement/fees/cooling and before the original YinYang/combo effects.
    /// The overload above keeps existing adapters and tests source-compatible.
    /// </summary>
    public static void Execute(PlayerState player, RaceSession session,
        Action<PlayerState, YinYangResult> applyYinYang,
        Action<PlayerState, ChinaWarmCupResult> applyWarmCup,
        Action<PlayerState> reportCombo,
        Action<PlayerState, int> applyGrillCooldown)
    {
        try
        {
            applyWarmCup?.Invoke(player, session.ResolveChinaWarmCup(player));
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
        finally
        {
            player.techState?.warmCup.CloseInput();
            player.techState?.germanBurst.CloseWindow();
        }
    }
}
