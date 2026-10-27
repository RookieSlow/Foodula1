using System;
using System.Collections.Generic;

/// <summary>
/// A single pending Hart decision. It freezes participant/input/card identities;
/// validation is read-only, and a successful commit discards exactly one speed
/// instance and spends one race use together. It owns no Unity or save state.
/// </summary>
public sealed class HunterSkillActivationPlan
{
    private readonly RaceSession session;
    private readonly PlayerState owner;
    private readonly CardDeck deck;
    private readonly DriverSkillRuntimeState runtime;
    private readonly string driverId, targetDriverId;
    private readonly int turn, input, version, gear, nodes, initialization, uses, tier;
    private readonly int ownerLap, ownerPosition, targetLap, targetPosition;
    private readonly CardSnapshot[] hand;
    private bool closed;

    private readonly struct CardSnapshot
    {
        public readonly CardData Card;
        private readonly CardType type;
        private readonly int value;
        private readonly string trickId;
        private readonly bool temporary;

        public CardSnapshot(CardData card)
        {
            Card = card;
            type = card.type;
            value = card.value;
            trickId = card.trickId;
            temporary = card.isTemp;
        }

        public bool Matches(CardData card) => ReferenceEquals(Card, card) && card.type == type &&
            card.value == value && card.trickId == trickId && card.isTemp == temporary;
    }

    public PlayerState Target { get; }
    public int TargetDistance { get; }
    public IReadOnlyList<CardData> Choices { get; }
    public int MovementBonus => tier >= 2 ? 3 : 2;
    public bool IsClosed => closed;

    private HunterSkillActivationPlan(RaceSession session, PlayerState owner, PlayerState target,
        int distance, int totalNodes, int globalTurn, int pendingGear, int requestVersion,
        List<CardData> choices)
    {
        this.session = session;
        this.owner = owner;
        deck = owner.deck;
        runtime = owner.driverSkill;
        driverId = owner.driverId;
        Target = target;
        targetDriverId = target.driverId;
        TargetDistance = distance;
        nodes = totalNodes;
        turn = globalTurn;
        gear = pendingGear;
        version = requestVersion;
        input = runtime.DrivingInputsStarted;
        initialization = runtime.InitializationVersion;
        uses = runtime.UsesRemaining;
        tier = runtime.Tier;
        ownerLap = owner.lap;
        ownerPosition = owner.position;
        targetLap = target.lap;
        targetPosition = target.position;
        hand = new CardSnapshot[deck.Hand.Count];
        for (int i = 0; i < hand.Length; i++) hand[i] = new CardSnapshot(deck.Hand[i]);
        Choices = choices.AsReadOnly();
    }

    public static bool CanPrepare(RaceSession session, PlayerState player, int totalNodes, int pendingGear,
        DriverSkillActivationContext context, out string reason)
    {
        if (player?.driverSkill == null || player.deck == null || session?.Players == null ||
            !session.Players.Contains(player) || player.driverSkill.Skill != DriverActiveSkillId.FinalLap ||
            DriverSkillRules.GetSkill(player.DriverProfile.Id) != DriverActiveSkillId.FinalLap)
        {
            reason = "车手或赛事已变更";
            return false;
        }
        if (!DriverSkillRules.CanActivate(player.DriverProfile, player.driverSkill, context, out reason))
            return false;
        if (RaceTurnRules.GetStartAction(player) != RaceTurnStartAction.SelectGear ||
            !player.driverSkill.IsDrivingInputOpen)
        {
            reason = "当前没有可用的驾驶输入";
            return false;
        }
        var unique = new HashSet<CardData>();
        int count = 0;
        foreach (CardData card in player.deck.Hand)
        {
            if (card == null || !unique.Add(card))
            {
                reason = "手牌来源无效";
                return false;
            }
            if (IsOrdinarySpeed(card)) count++;
        }
        if (count < 2)
        {
            reason = "需要至少两张普通速度牌";
            return false;
        }
        int playable = player.deck.CountSpeedInHand() + (CardPlayRules.CanUseHeatAsSpeed(player, session, totalNodes)
            ? player.deck.CountHeatInHand() : 0);
        int required = TeamGearRules.GetSpeedCardRequirement(player.teamId, pendingGear,
            player.chinaConsecutiveGearCount, 0).RequiredCardCount;
        if (pendingGear < 1 || pendingGear > 4 || playable - 1 < required)
        {
            reason = "弃牌后不足拟选挡位张数，请先改选低挡";
            return false;
        }
        if (FindNearestAhead(session, player, totalNodes, out _) == null)
        {
            reason = "前方 1–5 格内需要对手";
            return false;
        }
        reason = string.Empty;
        return true;
    }

    public static bool TryCreate(RaceSession session, PlayerState player, int totalNodes,
        int globalTurn, int pendingGear, int requestVersion, DriverSkillActivationContext context,
        out HunterSkillActivationPlan plan, out string reason)
    {
        plan = null;
        if (!CanPrepare(session, player, totalNodes, pendingGear, context, out reason)) return false;
        PlayerState target = FindNearestAhead(session, player, totalNodes, out int distance);
        var choices = new List<CardData>();
        foreach (CardData card in player.deck.Hand)
            if (IsOrdinarySpeed(card)) choices.Add(card);
        plan = new HunterSkillActivationPlan(session, player, target, distance,
            totalNodes, globalTurn, pendingGear, requestVersion, choices);
        return true;
    }

    public void Cancel() => closed = true;

    public bool TryCommit(RaceSession currentSession, PlayerState currentOwner, int globalTurn,
        int pendingGear, int requestVersion, DriverSkillActivationContext context,
        CardData cost, out string reason)
    {
        reason = "选择已过期，请重新选择";
        if (closed || !ReferenceEquals(session, currentSession) || !ReferenceEquals(owner, currentOwner) ||
            owner.driverId != driverId || !ReferenceEquals(owner.deck, deck) ||
            !ReferenceEquals(owner.driverSkill, runtime) || globalTurn != turn || pendingGear != gear ||
            requestVersion != version || runtime.DrivingInputsStarted != input ||
            runtime.InitializationVersion != initialization || runtime.UsesRemaining != uses ||
            runtime.Tier != tier || owner.lap != ownerLap || owner.position != ownerPosition ||
            Target.driverId != targetDriverId || Target.lap != targetLap ||
            Target.position != targetPosition || deck.Hand.Count != hand.Length)
            return false;
        for (int i = 0; i < hand.Length; i++)
            if (!hand[i].Matches(deck.Hand[i])) return false;
        if (!CanPrepare(session, owner, nodes, pendingGear, context, out reason)) return false;
        if (!ReferenceEquals(FindNearestAhead(session, owner, nodes, out _), Target))
        {
            reason = "最近前车已变更，请重新选择";
            return false;
        }
        bool knownCost = false;
        foreach (CardData card in Choices)
            if (ReferenceEquals(card, cost)) knownCost = true;
        if (!knownCost)
        {
            reason = "请选择手中的普通速度牌";
            return false;
        }

        // All gates and exact sources passed. These two bounded mutations call
        // no UI, callbacks, random source, heat payment or draw/refill operation.
        deck.DiscardPlayableCardInstancesFromHand(new[] { cost });
        runtime.CommitValidatedActivation();
        closed = true;
        reason = string.Empty;
        return true;
    }

    private static bool IsOrdinarySpeed(CardData card) => card != null && card.IsSpeed &&
        !card.isTemp && string.IsNullOrEmpty(card.trickId);

    private static PlayerState FindNearestAhead(RaceSession session, PlayerState player,
        int totalNodes, out int distance)
    {
        distance = 0;
        if (session?.Players == null || totalNodes <= 0) return null;
        long progress = (long)player.lap * totalNodes + player.position;
        long nearest = 6;
        PlayerState target = null;
        foreach (PlayerState other in session.Players)
        {
            if (other == null || ReferenceEquals(player, other) || RaceTurnRules.IsTerminal(other)) continue;
            long gap = (long)other.lap * totalNodes + other.position - progress;
            if (gap <= 0 || gap >= nearest) continue;
            nearest = gap;
            target = other;
        }
        if (target != null) distance = (int)nearest;
        return target;
    }
}
