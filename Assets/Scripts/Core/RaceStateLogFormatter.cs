using System.Text;

/// <summary>Read-only race trace formatting; the coordinator owns recording time and file output.</summary>
public static class RaceStateLogFormatter
{
    /// <summary>Requires a player with a deck and engine pool, as checked by the recording adapter.</summary>
    public static string BuildPlayerSnapshot(PlayerState player)
    {
        return $"[STATE] {player.name} role={(player.isAI ? "AI" : "PLAYER")} lap={player.lap} position={player.position} " +
            $"gear={player.gear} engine_heat={player.deck.heatPool.remaining} hand_speed={player.deck.CountSpeedInHand()} " +
            $"blown={player.isBlown} finished={player.hasFinished}";
    }

    public static string BuildPlayedCards(PlayerState player, string source)
    {
        var values = new StringBuilder();
        for (int i = 0; i < player.playedSpeedCardsThisTurn.Count; i++)
        {
            if (i > 0) values.Append(',');
            values.Append(player.playedSpeedCardsThisTurn[i].value);
        }

        TeamGearRules.SpeedCardRequirement requirement = CardPlayRules.GetSpeedCardRequirement(player);
        return $"[CARDS] {player.name} source={source} count={player.playedSpeedCardsThisTurn.Count} values=[{values}] " +
            $"gear_limit={requirement.TotalCardCount} base_limit={requirement.BaseCardCount} " +
            $"extra_slots={requirement.ExtraCardCount} hotpot_attack={player.hotpotAttackAppliedThisTurn} " +
            $"hotpot_card_value={player.hotpotAttackCardValueThisTurn}";
    }
}
