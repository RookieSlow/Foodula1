using System;
using System.Collections.Generic;

/// <summary>
/// Resolves end-of-turn followers from one settled movement snapshot, then
/// applies their bonuses. Presentation and the later movement phase remain
/// with the race coordinator.
/// </summary>
public static class RaceSettledSlipstreamResolution
{
    public static void Execute(
        RaceSession session,
        IReadOnlyList<PlayerState> turnOrder,
        ICollection<PlayerState> turnSkipped,
        int totalNodes,
        PlayerState humanPlayer,
        TutorialOpponentCue tutorialCue,
        Dictionary<PlayerState, SlipstreamChainResult> results,
        Action<string> log)
    {
        var eligibleFollowers = new List<PlayerState>(turnOrder.Count);
        foreach (PlayerState follower in turnOrder)
        {
            if (RaceTurnRules.IsInactive(follower, turnSkipped))
            {
                results[follower] = default;
                continue;
            }

            if (!TutorialOpponentCueRules.ShouldResolveSlipstreamForFollower(
                    tutorialCue, follower == humanPlayer))
            {
                results[follower] = default;
                log?.Invoke(
                    $"[TUTORIAL_CUE] type=opponent step={tutorialCue.step} " +
                    $"follower={follower.name} tailwind=blocked reason=player_only");
                continue;
            }

            eligibleFollowers.Add(follower);
        }

        var resolvedChains = session.ComputeSettledSlipstreamChains(
            eligibleFollowers, turnOrder, totalNodes);
        foreach (PlayerState follower in eligibleFollowers)
            results[follower] = resolvedChains[follower];

        // Complete every calculation before any bonus changes the movement
        // values used by same-cell tie-breaking.
        foreach (PlayerState follower in turnOrder)
        {
            if (!resolvedChains.TryGetValue(follower, out SlipstreamChainResult chain))
                continue;
            int baseMovement = follower.totalMovementThisTurn;
            follower.totalMovementThisTurn += chain.TotalBonus;
            log?.Invoke(
                $"[MOVE_PLAN_FINAL] {follower.name} position={follower.position} " +
                $"base_total={baseMovement} tailwind_bonus={chain.TotalBonus} " +
                $"total={follower.totalMovementThisTurn}");
        }
    }
}
