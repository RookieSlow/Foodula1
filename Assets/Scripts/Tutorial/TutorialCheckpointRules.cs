using System;
using System.Collections.Generic;

public sealed class TutorialCheckpointApplyResult
{
    public bool success;
    public string failureReason;
    public int handCount;
    public int drawCount;
    public int discardCount;
    public int engineHeat;
}

/// <summary>
/// Pure checkpoint materialization and tutorial-only pit-lane overlay rules.
/// Normal races never call this class and official track nodes are never mutated.
/// </summary>
public static class TutorialCheckpointRules
{
    public static TutorialCheckpointApplyResult ApplyPlayerCheckpoint(
        TutorialScenarioDefinition scenario,
        TutorialPlayerCheckpoint checkpoint,
        PlayerState player)
    {
        if (scenario == null || checkpoint == null || player == null)
            return Fail("missing_input");
        if (checkpoint.exactDrawOrder == null || checkpoint.normalHandSize < 0 ||
            checkpoint.normalHandSize > checkpoint.exactDrawOrder.Count)
            return Fail("invalid_normal_hand_size");
        if (checkpoint.heatInHand < 0 || checkpoint.heatInDiscard < 0 ||
            checkpoint.heatInHand + checkpoint.heatInDiscard > scenario.engineHeatCapacity)
            return Fail("invalid_heat_distribution");

        var pool = new HeatPool(scenario.engineHeatCapacity);
        player.deck.InitializeExactOrder(checkpoint.CreateExactDeck(), pool);
        if (!player.deck.DrawToHand(checkpoint.normalHandSize))
            return Fail("normal_draw_failed");
        if (player.deck.DrawHeatFromPool(
                checkpoint.heatInHand, HeatPaymentDestination.Hand) != checkpoint.heatInHand)
            return Fail("heat_hand_draw_failed");
        if (player.deck.DrawHeatFromPool(
                checkpoint.heatInDiscard, HeatPaymentDestination.Discard) != checkpoint.heatInDiscard)
            return Fail("heat_discard_draw_failed");

        player.position = checkpoint.playerCell;
        player.gear = checkpoint.gear;
        player.chinaConsecutiveGearCount = 0;
        player.hasFinished = false;
        player.isBlown = false;
        player.finishOrder = 0;
        player.spinCounter = 0;
        player.skipNextTurn = false;
        player.pitStopRequested = false;
        player.pitStopScheduled = false;
        player.pitChoiceResolvedThisLap = false;
        player.trickState.ResetPerRace();
        player.ClearTurnState();

        return new TutorialCheckpointApplyResult
        {
            success = true,
            failureReason = string.Empty,
            handCount = player.deck.HandCount,
            drawCount = player.deck.DrawPileCount,
            discardCount = player.deck.DiscardPileCount,
            engineHeat = player.deck.heatPool.remaining
        };
    }

    public static IReadOnlyList<TrackNode> CreateVirtualPitRuleNodes(
        int totalNodes,
        TutorialPitLaneDefinition layout)
    {
        if (totalNodes <= 1) throw new ArgumentOutOfRangeException(nameof(totalNodes));
        if (layout == null) throw new ArgumentNullException(nameof(layout));
        if (layout.entryCell < 0 || layout.entryCell >= totalNodes ||
            layout.exitCell < 0 || layout.exitCell >= totalNodes ||
            layout.entryCell == layout.exitCell)
            throw new ArgumentException("Tutorial pit cells must be distinct valid track cells.", nameof(layout));

        var result = new List<TrackNode>(totalNodes);
        for (int i = 0; i < totalNodes; i++)
        {
            result.Add(new TrackNode(
                i,
                0,
                i == layout.entryCell ? "Tutorial Pit Entry" :
                i == layout.exitCell ? "Tutorial Pit Exit" : "Tutorial Rule Cell",
                isPitEntry: i == layout.entryCell,
                isPitExit: i == layout.exitCell));
        }
        return result;
    }

    private static TutorialCheckpointApplyResult Fail(string reason)
    {
        return new TutorialCheckpointApplyResult
        {
            success = false,
            failureReason = reason ?? string.Empty
        };
    }
}
