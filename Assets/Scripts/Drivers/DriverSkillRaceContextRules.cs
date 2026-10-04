using System.Collections.Generic;

/// <summary>Read-only race facts for skill activation, independent of scene references.</summary>
public static class DriverSkillRaceContextRules
{
    public static DriverSkillActivationContext Build(
        PlayerState player, bool canAcceptInput, int totalLaps,
        IReadOnlyList<PlayerState> participants, int totalNodes)
    {
        HeatGaugeState gauge = player?.deck != null
            ? HeatGaugeRules.Evaluate(player.deck, player.playedSpeedCardsThisTurn) : default;
        return new DriverSkillActivationContext(
            canAcceptInput, player != null ? player.lap : 0, totalLaps,
            gauge.EngineRemaining, gauge.Capacity,
            CountNearbyOpponentsBehind(player, participants, totalNodes, 3));
    }

    public static int CountNearbyOpponentsBehind(
        PlayerState player, IReadOnlyList<PlayerState> participants, int totalNodes, int range)
    {
        if (player == null || participants == null || totalNodes <= 0)
            return 0;

        int count = 0;
        foreach (PlayerState opponent in participants)
        {
            if (opponent == null || opponent == player || opponent.isBlown || opponent.hasFinished ||
                opponent.lap != player.lap)
                continue;

            int distance = RaceSession.ForwardDistance(opponent.position, player.position, totalNodes);
            if (distance > 0 && distance <= range) count++;
        }
        return count;
    }
}
