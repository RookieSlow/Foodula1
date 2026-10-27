using System;
using System.Collections.Generic;

/// <summary>D02 exact-instance discard and fixed engine payment, committed only after all snapshots validate.</summary>
public sealed class MansellChargeActivationPlan
{
    private readonly RaceSession session;
    private readonly PlayerState owner, target;
    private readonly CardDeck deck;
    private readonly HeatPool pool;
    private readonly DriverSkillRuntimeState skill;
    private readonly MansellChargeRuntime charge;
    private readonly string driverId, targetId;
    private readonly int nodes, finish, turn, gear, request, input, initialization, uses, tier, chargeVersion;
    private readonly int ownerLap, ownerPosition, targetLap, targetPosition, engineRemaining;
    private readonly CardSnapshot[] hand;
    private bool closed;

    private readonly struct CardSnapshot
    {
        private readonly CardData card;
        private readonly CardType type;
        private readonly int value;
        private readonly bool temporary;
        private readonly string trick;
        public CardSnapshot(CardData card)
        { this.card = card; type = card.type; value = card.value; temporary = card.isTemp; trick = card.trickId; }
        public bool Matches(CardData other) => ReferenceEquals(card, other) && other.type == type &&
            other.value == value && other.isTemp == temporary && other.trickId == trick;
    }

    public bool IsSecondLeg { get; }
    public bool IsClosed => closed;
    public PlayerState Target => target;
    public long TargetDistance { get; }
    public int MovementBonus => IsSecondLeg ? (tier >= 2 ? 3 : 2) : (tier >= 2 ? 2 : 1);
    public IReadOnlyList<CardData> Choices { get; }
    public CardData PaidHeat { get; private set; }

    private MansellChargeActivationPlan(RaceSession race, PlayerState player, PlayerState leader, long distance,
        int totalNodes, int startFinish, int globalTurn, int pendingGear, int version, List<CardData> choices)
    {
        session = race; owner = player; target = leader; TargetDistance = distance;
        deck = player.deck; pool = deck.heatPool; skill = player.driverSkill; charge = skill.Charge;
        IsSecondLeg = charge.IsSecondLegAvailable; driverId = player.driverId; targetId = target.driverId;
        nodes = totalNodes; finish = startFinish; turn = globalTurn; gear = pendingGear; request = version;
        input = skill.DrivingInputsStarted; initialization = skill.InitializationVersion;
        uses = skill.UsesRemaining; tier = skill.Tier; chargeVersion = charge.Version;
        ownerLap = player.lap; ownerPosition = player.position;
        targetLap = target.lap; targetPosition = target.position; engineRemaining = pool.remaining;
        hand = new CardSnapshot[deck.Hand.Count];
        for (int i = 0; i < hand.Length; i++) hand[i] = new CardSnapshot(deck.Hand[i]);
        Choices = choices.AsReadOnly();
    }

    public static bool CanPrepare(RaceSession race, PlayerState player, int totalNodes, int startFinish,
        int pendingGear, DriverSkillActivationContext context, out string reason)
    {
        reason = "当前没有可用的雄狮追击驾驶输入";
        if (!MansellChargeRuntime.Eligible(race, player) || totalNodes <= 0 || !context.CanAcceptInput) return false;
        var runtime = player.driverSkill;
        bool second = runtime.Charge.IsSecondLegAvailable;
        if (second && !runtime.Charge.MatchesSecondObservation(race, player, totalNodes, startFinish)) return false;
        if (runtime.ActivatedThisTurn || runtime.IsActive || runtime.Charge.CommittedThisInput) return false;
        if (!second && (!runtime.Charge.CanStartFirst || runtime.UsesRemaining <= 0))
        { reason = "追击窗口已结束或本场次数已用完"; return false; }
        if (player.deck.heatPool.remaining < 1) { reason = "需要整笔支付1张引擎永久热"; return false; }
        PlayerState nearest = MansellChargeRuntime.FindNearest(race, player, totalNodes, startFinish, out long gap);
        if (nearest == null || (!second && gap > 6) || (second && !ReferenceEquals(nearest, runtime.Charge.Target)))
        { reason = "需要合格的同一最近前车，首段距离1–6格"; return false; }
        var unique = new HashSet<CardData>(); int speedChoices = 0;
        foreach (CardData card in player.deck.Hand)
        {
            if (card == null || !unique.Add(card)) { reason = "手牌来源无效"; return false; }
            if (IsOrdinarySpeed(card)) speedChoices++;
        }
        int required = TeamGearRules.GetSpeedCardRequirement(player.teamId, pendingGear,
            player.chinaConsecutiveGearCount, 0).RequiredCardCount;
        if (pendingGear < 1 || pendingGear > 4 || player.deck.CountSpeedInHand() - (second ? 0 : 1) < required ||
            (!second && speedChoices == 0))
        { reason = "弃牌后不足拟选挡位张数，请先改选低挡"; return false; }
        reason = string.Empty; return true;
    }

    public static bool TryCreate(RaceSession race, PlayerState player, int totalNodes, int startFinish,
        int globalTurn, int pendingGear, int requestVersion, DriverSkillActivationContext context,
        out MansellChargeActivationPlan plan, out string reason)
    {
        plan = null;
        if (!CanPrepare(race, player, totalNodes, startFinish, pendingGear, context, out reason))
        {
            var current = player?.driverSkill?.Charge;
            if (current != null && current.HasInputScope(race, player, totalNodes, startFinish))
                current.DeclineSecond(current.Version);
            return false;
        }
        PlayerState nearest = MansellChargeRuntime.FindNearest(race, player, totalNodes, startFinish, out long gap);
        var choices = new List<CardData>();
        if (!player.driverSkill.Charge.IsSecondLegAvailable)
            foreach (CardData card in player.deck.Hand) if (IsOrdinarySpeed(card)) choices.Add(card);
        player.driverSkill.Charge.AdvancePlanVersion();
        plan = new MansellChargeActivationPlan(race, player, nearest, gap, totalNodes, startFinish,
            globalTurn, pendingGear, requestVersion, choices);
        return true;
    }

    public void Cancel()
    {
        if (closed) return;
        closed = true;
        if (IsSecondLeg) charge.DeclineSecond(chargeVersion);
    }

    public bool TryCommit(RaceSession race, PlayerState player, int totalNodes, int startFinish,
        int globalTurn, int pendingGear, int requestVersion, DriverSkillActivationContext context,
        CardData speedCost, out string reason)
    {
        reason = "追击选择已过期，请重新选择";
        if (closed || !ReferenceEquals(session, race) || !ReferenceEquals(owner, player) ||
            !ReferenceEquals(deck, player.deck) || !ReferenceEquals(pool, player.deck?.heatPool) ||
            !ReferenceEquals(skill, player.driverSkill) || !ReferenceEquals(charge, skill.Charge) ||
            driverId != player.driverId || nodes != totalNodes || finish != startFinish ||
            turn != globalTurn || gear != pendingGear || request != requestVersion ||
            input != skill.DrivingInputsStarted || initialization != skill.InitializationVersion ||
            uses != skill.UsesRemaining || tier != skill.Tier || chargeVersion != charge.Version ||
            ownerLap != player.lap || ownerPosition != player.position || targetId != target.driverId ||
            targetLap != target.lap || targetPosition != target.position || engineRemaining != pool.remaining ||
            hand.Length != deck.Hand.Count) { Cancel(); return false; }
        for (int i = 0; i < hand.Length; i++)
            if (!hand[i].Matches(deck.Hand[i])) { Cancel(); return false; }
        if (!CanPrepare(race, owner, nodes, finish, pendingGear, context, out reason))
        { Cancel(); return false; }
        if (!ReferenceEquals(MansellChargeRuntime.FindNearest(race, owner, nodes, finish, out _), target))
        { Cancel(); return false; }
        bool knownCost = IsSecondLeg && speedCost == null;
        foreach (CardData card in Choices) if (ReferenceEquals(card, speedCost)) knownCost = true;
        if (!knownCost || !deck.TryCommitMansellChargeCost(speedCost, IsSecondLeg, out CardData paid))
        { reason = "固定费用或普通速度牌来源已失效"; Cancel(); return false; }
        if (IsSecondLeg) { skill.CommitValidatedContinuation(); charge.CommitSecond(); }
        else { skill.CommitValidatedActivation(); charge.CommitFirst(session, owner, target, nodes, finish, TargetDistance); }
        race.RecordFixedPermanentPayment(player, paid);
        PaidHeat = paid; closed = true; reason = string.Empty; return true;
    }

    private static bool IsOrdinarySpeed(CardData card) => card != null && card.IsSpeed &&
        !card.isTemp && string.IsNullOrEmpty(card.trickId);
}
