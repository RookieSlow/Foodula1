using System;
using System.Collections.Generic;

/// <summary>D03 whole preparation discard or fixed execution payment, after exact snapshots validate (ADR-002).</summary>
public sealed class SchumacherStrategyActivationPlan
{
    private readonly RaceSession session;
    private readonly PlayerState owner;
    private readonly DriverSkillRuntimeState skill;
    private readonly SchumacherStrategyRuntime strategy;
    private readonly CardDeck deck;
    private readonly HeatPool pool;
    private readonly string driverId;
    private readonly TeamId team;
    private readonly int turn, gear, request, input, initialization, uses, tier, strategyVersion;
    private readonly int lap, position, engine, capacityBonus, retirement;
    private readonly CardSnapshot[] hand, draw, discard, receipts;
    private bool closed;

    private readonly struct CardSnapshot
    {
        private readonly CardData card;
        private readonly CardType type;
        private readonly int value;
        private readonly bool temporary;
        private readonly string trick;
        public CardSnapshot(CardData card)
        { this.card = card; type = card != null ? card.type : default; value = card?.value ?? 0; temporary = card?.isTemp ?? false; trick = card?.trickId; }
        public bool Matches(CardData other) => ReferenceEquals(card, other) && (other == null ||
            other.type == type && other.value == value && other.isTemp == temporary && other.trickId == trick);
    }

    public bool IsExecution { get; }
    public int ExecutionNumber { get; }
    public bool IsClosed => closed;
    public int MovementBonus => IsExecution ? DriverSkillRules.GetSchumacherStrategyMovementBonus(tier) : 0;
    public IReadOnlyList<CardData> Choices { get; }
    public CardData PaidHeat { get; private set; }

    private SchumacherStrategyActivationPlan(RaceSession race, PlayerState player, int globalTurn,
        int pendingGear, int requestVersion, List<CardData> choices)
    {
        session = race; owner = player; skill = player.driverSkill; strategy = skill.Strategy;
        deck = player.deck; pool = deck.heatPool; driverId = player.driverId; team = player.teamId;
        turn = globalTurn; gear = pendingGear; request = requestVersion; input = strategy.InputsStarted;
        initialization = skill.InitializationVersion; uses = skill.UsesRemaining; tier = skill.Tier;
        strategyVersion = strategy.Version; lap = player.lap; position = player.position; engine = pool.remaining;
        capacityBonus = deck.RegionalCapacityBonus; retirement = deck.RegionalHeatPendingRetirement;
        IsExecution = strategy.IsExecutionAvailable; ExecutionNumber = strategy.ExecutionNumber;
        hand = Snapshot(deck.Hand); draw = Snapshot(deck.DrawPile); discard = Snapshot(deck.DiscardPile);
        receipts = Snapshot(player.heatPaidCardsThisTurn); Choices = choices.AsReadOnly();
    }

    /// <summary>Read-only availability: preparation discards one ordinary speed; each later execution pays one permanent heat.</summary>
    public static bool CanPrepare(RaceSession race, PlayerState player, int pendingGear,
        DriverSkillActivationContext context, out string reason)
    {
        reason = "当前没有可用的三段战术驾驶输入";
        var skill = player?.driverSkill;
        var strategy = skill?.Strategy;
        if (strategy == null || !context.CanAcceptInput || !strategy.HasInputScope(race, player)) return false;
        bool execute = strategy.IsExecutionAvailable;
        if (strategy.CommittedThisInput || (!execute && !strategy.IsPreparationAvailable) ||
            (!strategy.ManualPath && (skill.IsActive || skill.ActivatedThisTurn))) return false;
        if (!execute && skill.UsesRemaining <= 0) { reason = "本场战术次数已用完"; return false; }
        if (execute ? pendingGear < 3 || pendingGear > 4 : pendingGear < 1 || pendingGear > 2)
        { reason = execute ? "执行段需要3或4挡" : "准备回合限1或2挡"; return false; }
        if (execute && player.deck.heatPool.remaining < 1)
        { reason = "执行段需要整笔支付1张永久引擎热"; return false; }
        var unique = new HashSet<CardData>(); int ordinary = 0;
        foreach (CardData card in player.deck.Hand)
        {
            if (card == null || !unique.Add(card)) { reason = "手牌原实例无效"; return false; }
            if (SchumacherStrategyRuntime.IsOrdinarySpeed(card)) ordinary++;
        }
        int required = TeamGearRules.GetSpeedCardRequirement(player.teamId, pendingGear,
            player.chinaConsecutiveGearCount, 0).RequiredCardCount;
        if (ordinary - (execute ? 0 : 1) < required)
        { reason = execute ? "当前普通速度牌不足执行挡位" : "弃1张速度后不足准备挡位张数"; return false; }
        reason = string.Empty; return true;
    }

    /// <summary>Creates no resource charge; failed eligible execution choices explicitly forfeit their remaining sequence.</summary>
    public static bool TryCreate(RaceSession race, PlayerState player, int globalTurn, int pendingGear,
        int requestVersion, DriverSkillActivationContext context, out SchumacherStrategyActivationPlan plan, out string reason)
    {
        plan = null;
        if (!CanPrepare(race, player, pendingGear, context, out reason))
        {
            var current = player?.driverSkill?.Strategy;
            if (context.CanAcceptInput && current != null && current.HasInputScope(race, player))
                current.DeclineExecution(current.Version);
            return false;
        }
        var choices = new List<CardData>();
        if (!player.driverSkill.Strategy.IsExecutionAvailable)
            foreach (CardData card in player.deck.Hand) if (SchumacherStrategyRuntime.IsOrdinarySpeed(card)) choices.Add(card);
        player.driverSkill.Strategy.AdvancePlanVersion();
        plan = new SchumacherStrategyActivationPlan(race, player, globalTurn, pendingGear, requestVersion, choices);
        return true;
    }

    /// <summary>Superseding a preview does not decline its input; an explicit execution cancel does.</summary>
    public void Cancel(bool declineExecution = true)
    {
        if (closed) return;
        closed = true;
        if (declineExecution && IsExecution) strategy.DeclineExecution(strategyVersion);
    }

    /// <summary>All identity, phase, version and card-zone snapshots are checked before a single atomic deck commit.</summary>
    public bool TryCommit(RaceSession race, PlayerState player, int globalTurn, int pendingGear,
        int requestVersion, DriverSkillActivationContext context, CardData speedCost, out string reason)
    {
        reason = "战术选择已过期，请重新选择";
        if (!SnapshotsMatch(race, player, globalTurn, pendingGear, requestVersion) ||
            !CanPrepare(race, player, pendingGear, context, out reason)) { Cancel(); return false; }
        bool knownCost = IsExecution && speedCost == null;
        if (!IsExecution) foreach (CardData card in Choices) if (ReferenceEquals(card, speedCost)) knownCost = true;
        if (!knownCost || !deck.TryCommitSchumacherStrategyCost(speedCost, IsExecution, out CardData paid))
        { reason = "普通速度牌原实例或固定永久热已失效"; Cancel(); return false; }
        if (IsExecution)
        {
            skill.CommitValidatedContinuation(); strategy.CommitExecution();
            race.RecordFixedPermanentPayment(player, paid);
        }
        else { skill.CommitValidatedActivation(); strategy.CommitPreparation(); }
        PaidHeat = paid; closed = true; reason = string.Empty; return true;
    }

    private bool SnapshotsMatch(RaceSession race, PlayerState player, int globalTurn, int pendingGear, int requestVersion) =>
        !closed && player != null && ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
        ReferenceEquals(skill, player.driverSkill) && ReferenceEquals(strategy, player.driverSkill?.Strategy) &&
        ReferenceEquals(deck, player.deck) && ReferenceEquals(pool, player.deck?.heatPool) &&
        driverId == player.driverId && team == player.teamId && turn == globalTurn && gear == pendingGear && request == requestVersion &&
        input == strategy.InputsStarted && initialization == skill.InitializationVersion && uses == skill.UsesRemaining &&
        tier == skill.Tier && strategyVersion == strategy.Version && lap == player.lap && position == player.position &&
        engine == pool.remaining && capacityBonus == deck.RegionalCapacityBonus && retirement == deck.RegionalHeatPendingRetirement &&
        IsExecution == strategy.IsExecutionAvailable && ExecutionNumber == strategy.ExecutionNumber &&
        Matches(hand, deck.Hand) && Matches(draw, deck.DrawPile) && Matches(discard, deck.DiscardPile) && Matches(receipts, player.heatPaidCardsThisTurn);

    private static CardSnapshot[] Snapshot(IReadOnlyList<CardData> cards)
    {
        var result = new CardSnapshot[cards.Count];
        for (int i = 0; i < result.Length; i++) result[i] = new CardSnapshot(cards[i]);
        return result;
    }
    private static bool Matches(CardSnapshot[] snapshots, IReadOnlyList<CardData> cards)
    {
        if (cards.Count != snapshots.Length) return false;
        for (int i = 0; i < cards.Count; i++) if (!snapshots[i].Matches(cards[i])) return false;
        return true;
    }
}
