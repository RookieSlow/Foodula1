using System.Collections.Generic;

/// <summary>
/// Completes the base-movement overtake pass before settled slipstream is
/// resolved. The coordinator owns the turn maps and movement presentation.
/// </summary>
public static class RaceOvertakeResolution
{
    public static void Execute(
        IReadOnlyList<PlayerState> turnOrder,
        ICollection<PlayerState> turnSkipped,
        int totalNodes,
        Dictionary<PlayerState, int> overtakesThisTurn)
    {
        // Keep the skill-bonus pass separate from the presentation count.
        foreach (PlayerState player in turnOrder)
        {
            if (RaceTurnRules.IsInactive(player, turnSkipped))
                continue;
            int projectedOvertakes = RaceMovementRules.CountOvertakes(
                player, turnOrder, totalNodes, true, RaceTurnRules.ShouldSkip);
            int bonus = DriverSkillRules.GetOvertakeBonus(player.driverSkill, projectedOvertakes);
            if (bonus > 0)
                player.totalMovementThisTurn += bonus;
        }

        foreach (PlayerState player in turnOrder)
        {
            if (RaceTurnRules.IsInactive(player, turnSkipped))
            {
                overtakesThisTurn[player] = 0;
                continue;
            }
            overtakesThisTurn[player] = RaceMovementRules.CountOvertakes(
                player, turnOrder, totalNodes, true, RaceTurnRules.ShouldSkip);
        }
    }
}
