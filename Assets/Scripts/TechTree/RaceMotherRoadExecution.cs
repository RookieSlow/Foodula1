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
                    int permanentSelected = 0;
                    int permanentToConsume = converted + player.deck.RegionalHeatPendingRetirement;
                    foreach (CardData card in player.deck.Hand)
                    {
                        if (card == null || !card.IsHeat) continue;
                        if (card.isTemp)
                        {
                            // Temporary heat follows its existing lifecycle and is destroyed.
                            heatCards.Add(card);
                        }
                        else if (permanentSelected < permanentToConsume)
                        {
                            heatCards.Add(card);
                            permanentSelected++;
                        }
                    }
                    int returned = player.deck.ReturnHeatCardsToPool(heatCards);
                    advance(returned);
                    int retired = permanentSelected - returned;
                    appendLog?.Invoke($"<color=orange>{player.name} 母亲之路(复兴)！消耗 {heatCards.Count} 张热量牌（清偿借额 {retired}，净回 {returned}），转为 {returned} 格移动。</color>");
                }
                break;
            }
        }
    }
}
