using System;
using System.Collections.Generic;

/// <summary>
/// Settles one already-crossed Mother Road landmark. The race coordinator
/// supplies presentation-aware cooling, heat payment and instant movement.
/// </summary>
public static class RaceMotherRoadExecution
{
    public static void Execute(
        PlayerState player, int landmarkIndex, int totalLaps,
        Func<int, int> coolHeat, Func<int, bool> payHeat,
        Action<int> advance, Action<string> appendLog)
    {
        MotherRoadResult result = TechTreeRules.ResolveMotherRoadPass(
            player.techState, landmarkIndex, totalLaps);
        switch (result.phase)
        {
            case MotherRoadResult.MotherRoadPhase.Prosperity:
            {
                int cooled = coolHeat(result.freeCooldown);
                appendLog?.Invoke($"<color=green>{player.name} 母亲之路(繁荣)：自动冷却 {cooled} 张热量牌。</color>");
                break;
            }
            case MotherRoadResult.MotherRoadPhase.Decline:
            {
                // Repair remains automatic when affordable in the current runtime.
                if (player.deck == null || player.deck.heatPool == null ||
                    player.deck.heatPool.remaining < 1)
                {
                    appendLog?.Invoke($"{player.name} 母亲之路(衰退)：引擎无热，跳过修复。");
                    break;
                }
                if (payHeat(1))
                {
                    TechTreeRules.RepairLandmark(player.techState);
                    appendLog?.Invoke($"{player.name} 母亲之路(衰退)：修复地标，付 1 热。");
                }
                break;
            }
            case MotherRoadResult.MotherRoadPhase.Revival:
            {
                // Only permanent heat actually returned to the engine gives movement.
                int returnable = player.deck.CountHandHeatRestorableToEngine();
                int converted = TechTreeRules.UseMotherRoadUltimate(
                    player.techState, landmarkIndex, returnable);
                if (converted > 0)
                {
                    var heatCards = new List<CardData>();
                    foreach (CardData card in player.deck.Hand)
                        if (card.IsHeat) heatCards.Add(card);
                    int returned = player.deck.ReturnHeatCardsToPool(heatCards);
                    advance(returned);
                    appendLog?.Invoke($"<color=orange>{player.name} 母亲之路(复兴)！{returned} 张热量牌转为移动。</color>");
                }
                break;
            }
        }
    }
}
