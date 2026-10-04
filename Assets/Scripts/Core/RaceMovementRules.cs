using System;
using System.Collections.Generic;

/// <summary>
/// Pure movement planning and order rules shared by technology and gameplay bonuses.
/// Position values are interpreted on a closed track and are not mutated.
/// </summary>
public static class RaceMovementRules
{
    /// <summary>Calculation only; the coordinator still owns every settlement effect.</summary>
    public readonly struct InstantMovementPlan
    {
        public int StartPosition { get; }
        public int RawEnd { get; }
        public int FinalPosition { get; }
        public int FinishCrossings { get; }

        internal InstantMovementPlan(int startPosition, int rawEnd, int finalPosition, int finishCrossings)
        {
            StartPosition = startPosition;
            RawEnd = rawEnd;
            FinalPosition = finalPosition;
            FinishCrossings = finishCrossings;
        }
    }

    /// <summary>
    /// Preserve the synchronous adapter's signed remainder and raw route endpoint.
    /// Count crossings before wrapping; invalid/empty tracks keep their original
    /// failure behavior instead of turning a failed movement into a successful no-op.
    /// </summary>
    public static InstantMovementPlan PlanInstantTechnologyMovement(
        IReadOnlyList<TrackNode> nodes, int position, int movement)
    {
        int rawEnd = position + movement;
        int finishCrossings = TrackRules.CountStartFinishCrossings(nodes, position, rawEnd);
        return new InstantMovementPlan(position, rawEnd, rawEnd % nodes.Count, finishCrossings);
    }

    /// <summary>
    /// Corner speed is based only on committed cards and per-card driver speed.
    /// Hotpot moves its selected ATTACK card out of corner checks; the authored
    /// JP torpedo leader is kept stationary before other players inspect it.
    /// </summary>
    public static int ComputeCornerSpeed(PlayerState player, TutorialOpponentCue activeCue,
        bool isTeachingLeader)
    {
        int speedPerCardBonus = DriverSkillRules.GetSpeedPerCardBonus(player.driverSkill);
        int rawSpeedTotal = CardPlayRules.SumCommittedSpeedCardValues(player.playedSpeedCardsThisTurn) +
            speedPerCardBonus * player.playedSpeedCardsThisTurn.Count;
        int cornerSpeed = Math.Max(0, rawSpeedTotal -
            CardPlayRules.GetHotpotCornerExclusion(player, speedPerCardBonus));
        return activeCue != null && activeCue.step == TutorialStepId.JpTorpedo && isTeachingLeader
            ? 0
            : cornerSpeed;
    }

    /// <summary>
    /// Keep the original order: clamp base movement, apply the authored leader
    /// movement, then remove that leader's corner speed for corner resolution.
    /// </summary>
    public static (int movement, int cornerSpeed) ResolveBaseMovement(
        int cornerSpeed, int bonus, TutorialOpponentCue activeCue, bool isTeachingLeader)
    {
        int movement = Math.Max(0, cornerSpeed + bonus);
        movement = TutorialOpponentCueRules.ResolveLeaderMovement(
            activeCue, isTeachingLeader, movement);
        if (activeCue != null && isTeachingLeader)
            cornerSpeed = 0;
        return (movement, cornerSpeed);
    }

    /// <summary>JP L1: each crossed corner matched exactly grants two cells.</summary>
    public static int ComputeNigiriBonus(
        int cornerSpeed, IEnumerable<int> crossedCornerIds, Func<int, int> effectiveLimit)
    {
        int bonus = 0;
        foreach (int cornerId in crossedCornerIds)
            if (cornerSpeed == effectiveLimit(cornerId))
                bonus += 2;
        return bonus;
    }

    /// <summary>JP attack trick: one extra cell per eligible base-movement overtake.</summary>
    public static int ComputeTorpedoBonus(
        TrickCardState trickState, PlayerState player,
        IReadOnlyList<PlayerState> turnOrder, int totalNodes,
        Func<PlayerState, bool> shouldSkip)
    {
        if (!TrickCardRules.IsTorpedoTempuraActive(trickState)) return 0;
        int overtakes = CountOvertakes(player, turnOrder, totalNodes, false, shouldSkip);
        return overtakes > 0 ? overtakes * TrickCardRules.GetTorpedoOvertakeBonus() : 0;
    }

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
