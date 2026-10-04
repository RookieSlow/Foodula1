using System.Collections.Generic;
using System.Text;

/// <summary>Read-only race trace formatting; the coordinator owns recording time and file output.</summary>
public static class RaceStateLogFormatter
{
    /// <summary>Enumerates the authored setup in recording order without applying checkpoints.</summary>
    public static IEnumerable<string> BuildTutorialSetup(TutorialScenarioDefinition scenario, TutorialRunPhase phase)
    {
        yield return $"[TUTORIAL_SETUP] event=runtime_ready scenario={scenario.id} phase={phase}";
        yield return $"[TUTORIAL_SETUP] event=exact_deck_loaded count={scenario.exactDrawOrder.Count} opening={scenario.openingHandSize}";
        yield return $"[TUTORIAL_SETUP] event=weather_script_ready start={scenario.guidedStartWeatherId} practice={scenario.practiceWeatherId}";
        yield return $"[TUTORIAL_SETUP] event=player_checkpoints_ready count={scenario.playerCheckpoints.Count} exact_zones=true";
        if (scenario.tutorialPitLane != null)
            yield return $"[TUTORIAL_SETUP] event=virtual_pit_ready entry={scenario.tutorialPitLane.entryCell} " +
                $"exit={scenario.tutorialPitLane.exitCell} official_track_mutated=false";
        TutorialOpponentCue cue = scenario.opponentScript.Count > 0 ? scenario.opponentScript[0] : null;
        if (cue != null)
            yield return $"[TUTORIAL_SETUP] event=opponent_script_ready step={cue.step} " +
                $"leader={cue.leaderCell} player={cue.playerCell} distance={cue.expectedSlipstreamDistance}";
    }

    /// <summary>Formats the immutable launch snapshot, without loading or recording a season.</summary>
    public static string BuildCareerSetup(CareerRaceLaunchRequest launch)
    {
        return $"[CAREER_SETUP] result_id={launch.ResultId} " +
            $"race={launch.RaceIndex + 1}/{CareerModeRules.RaceCount} " +
            $"track={launch.TrackId} team={launch.PlayerTeam} " +
            $"competitors={launch.Competitors.Count} tech_snapshot=true";
    }

    /// <summary>Requires a nonempty resolved roster; preserves unknown driver IDs for diagnostics.</summary>
    public static string BuildFreeRaceSetup(FreeRaceRosterEntry[] roster)
    {
        var text = new StringBuilder();
        for (int i = 0; i < roster.Length; i++)
        {
            if (i > 0) text.Append(',');
            FreeRaceRosterEntry entry = roster[i];
            DriverCatalog.TryGet(entry.DriverId, out DriverProfile driver);
            text.Append($"{entry.TeamId}:{driver?.ShortName ?? entry.DriverId}");
        }
        return $"[FREE_RACE_SETUP] field={roster.Length} " +
            $"player={roster[0].TeamId}:{roster[0].DriverId} roster=[{text}]";
    }

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
            values.Append(CardPlayRules.GetCommittedSpeedCardValue(player.playedSpeedCardsThisTurn[i]));
        }

        TeamGearRules.SpeedCardRequirement requirement = CardPlayRules.GetSpeedCardRequirement(player);
        int bbqHeat = 0;
        foreach (CardData card in player.playedSpeedCardsThisTurn)
            if (card.IsHeat) bbqHeat++;
        return $"[CARDS] {player.name} source={source} count={player.playedSpeedCardsThisTurn.Count} values=[{values}] " +
            $"gear_limit={requirement.TotalCardCount} base_limit={requirement.BaseCardCount} " +
            $"extra_slots={requirement.ExtraCardCount} hotpot_attack={player.hotpotAttackAppliedThisTurn} " +
            $"hotpot_card_value={player.hotpotAttackCardValueThisTurn}" +
            (bbqHeat > 0 ? $" bbq_heat={bbqHeat}" : string.Empty);
    }
}
