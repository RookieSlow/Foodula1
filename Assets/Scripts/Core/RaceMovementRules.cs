using System;
using System.Collections.Generic;

/// <summary>
/// Pure movement-order rules shared by visual overtake effects and gameplay bonuses.
/// Position values are interpreted on a closed track and are not mutated.
/// </summary>
public static class RaceMovementRules
{
    /// <summary>
    /// Counts opponents that were ahead of the player before movement and are
    /// behind afterward. The skip predicate is injected so recovery/pit rules
    /// stay owned by the race coordinator.
    /// </summary>
    public static int CountOvertakes(
        PlayerState player,
        IReadOnlyList<PlayerState> turnOrder,
        int totalNodes,
        bool useFinalMovement,
        Func<PlayerState, bool> shouldSkip)
    {
        if (player == null || turnOrder == null || totalNodes <= 0)
            return 0;

        int oldPosition = player.position;
        int movement = useFinalMovement
            ? player.totalMovementThisTurn
            : player.cornerTotalThisTurn;
        int newPosition = oldPosition + movement;
        int overtakes = 0;

        for (int i = 0; i < turnOrder.Count; i++)
        {
            PlayerState opponent = turnOrder[i];
            if (opponent == null || opponent == player || opponent.isBlown || opponent.hasFinished)
                continue;
            if (shouldSkip != null && shouldSkip(opponent))
                continue;

            int opponentMovement = useFinalMovement
                ? opponent.totalMovementThisTurn
                : opponent.cornerTotalThisTurn;
            int opponentNewPosition = opponent.position + opponentMovement;
            if (IsAhead(opponent.position, oldPosition, totalNodes) &&
                !IsAhead(opponentNewPosition, newPosition, totalNodes))
            {
                overtakes++;
            }
        }

        return overtakes;
    }

    /// <summary>Returns whether a position is in front of another on a closed track.</summary>
    public static bool IsAhead(int aheadPosition, int behindPosition, int totalNodes)
    {
        if (totalNodes <= 0)
            return false;

        int forward = (aheadPosition - behindPosition + totalNodes) % totalNodes;
        return forward <= totalNodes / 2;
    }
}
