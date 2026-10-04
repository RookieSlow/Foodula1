using System;
using System.Collections.Generic;

/// <summary>
/// Applies an already-resolved trick in the original effect order. Race-specific
/// heat payment, cooling and presentation remain supplied by the coordinator.
/// </summary>
public static class RaceTrickEffectExecutor
{
    public static void Execute(
        PlayerState player, TrickPlayResult result,
        Func<int, bool> payHeat, Func<int, int> coolHeat,
        Func<TrickEffectType?> resolveEffect, Action<string> appendLog)
    {
        if (result.heatToPay > 0 && !payHeat(result.heatToPay))
            return; // A spin interrupts every later effect, as before.

        if (result.heatToCool > 0)
        {
            int cooled = coolHeat(result.heatToCool);
            if (cooled > 0)
                appendLog?.Invoke($"{player.name} 特技冷却 {cooled} 张热量牌。");
        }

        player.trickMoveBonusThisTurn += result.extraMovement;

        if (result.cardsToDraw > 0)
            player.deck.DrawToHand(player.deck.HandCount + result.cardsToDraw);

        if (result.requiresSpeedDiscard)
        {
            List<CardData> speeds = player.deck.GetBottomNSpeedCards(1);
            if (speeds.Count > 0)
            {
                player.deck.RemoveFromHand(speeds);
                player.deck.DiscardSpeedCards(speeds);
                appendLog?.Invoke($"{player.name} 弃掉 {speeds[0].value} 速度牌。");
            }
        }

        if (TrickCardRules.HasTempHeat(player.trickState))
        {
            player.deck.AddCardsToHand(new List<CardData> { CardData.CreateTempHeat() });
            TrickCardRules.ConsumeTempHeat(player.trickState);
            appendLog?.Invoke($"{player.name} 获得 1 张限时热量牌（回合结束销毁）。");
        }

        if (resolveEffect() == TrickEffectType.KantoOden)
        {
            player.kantoOdenSkipThisTurn = true;
            TrickCardRules.AccumulateKantoOden(player.trickState, player.gear);
            appendLog?.Invoke($"{player.name} 关东慢煮生效：本回合跳过，下回合可多出 {player.gear} 张牌。");
        }
    }
}
