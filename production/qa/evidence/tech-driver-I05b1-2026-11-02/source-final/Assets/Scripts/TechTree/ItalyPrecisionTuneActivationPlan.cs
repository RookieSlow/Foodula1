using System;
using System.Collections.Generic;

/// <summary>Free selected-card request. Validation precedes the one atomic card commit; there is no RP payment.</summary>
public sealed class ItalyPrecisionTuneActivationPlan
{
    private readonly RaceSession session;
    private readonly PlayerState owner;
    private readonly CardDeck deck;
    private readonly TechTreeState state;
    private readonly ItalyPrecisionTuneRuntime runtime;
    private readonly DriverSkillRuntimeState skill;
    private readonly int turn, request, version, input, gear, position, lap, perCardBonus;
    private readonly TeamId team, techTeam;
    private readonly TeamId? borrowed;
    private readonly HashSet<string> active;
    private readonly Snapshot hand, played, selected;
    private readonly bool hotpotPending, hotpotApplied;
    private readonly int hotpotValue;
    private bool closed;

    private sealed class Snapshot
    {
        private readonly CardData[] cards;
        private readonly CardType[] types;
        private readonly int[] values;
        private readonly bool[] temporary;
        private readonly string[] tricks;
        public Snapshot(IReadOnlyList<CardData> source)
        {
            cards = new CardData[source.Count]; types = new CardType[source.Count]; values = new int[source.Count];
            temporary = new bool[source.Count]; tricks = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            {
                CardData c = source[i]; cards[i] = c;
                if (c == null) continue;
                types[i] = c.type; values[i] = c.value; temporary[i] = c.isTemp; tricks[i] = c.trickId;
            }
        }
        public bool Matches(IReadOnlyList<CardData> source)
        {
            if (source == null || cards.Length != source.Count) return false;
            for (int i = 0; i < cards.Length; i++)
            {
                CardData c = source[i];
                if (c == null || !ReferenceEquals(cards[i], c) || c.type != types[i] || c.value != values[i] ||
                    c.isTemp != temporary[i] || c.trickId != tricks[i]) return false;
            }
            return true;
        }
    }

    public CardData Target { get; }
    public bool IsClosed => closed;

    private ItalyPrecisionTuneActivationPlan(RaceSession race, PlayerState player, IReadOnlyList<CardData> cards,
        CardData target, int globalTurn, int requestVersion)
    {
        session = race; owner = player; deck = player.deck; state = player.techState; runtime = state.precisionTune;
        skill = player.driverSkill; Target = target; turn = globalTurn; request = requestVersion;
        version = runtime.Version; input = runtime.InputId; gear = player.gear; position = player.position; lap = player.lap;
        perCardBonus = DriverSkillRules.GetSpeedPerCardBonus(skill);
        team = player.teamId; techTeam = state.teamId; borrowed = state.sunNeverSetsTarget;
        active = new HashSet<string>(state.activeNodeIds);
        hand = new Snapshot(deck.Hand); played = new Snapshot(player.playedSpeedCardsThisTurn); selected = new Snapshot(cards);
        hotpotPending = TrickCardRules.HasHotpotAttack(player.trickState);
        hotpotApplied = player.hotpotAttackAppliedThisTurn; hotpotValue = player.hotpotAttackCardValueThisTurn;
    }

    public static bool CanPrepare(RaceSession race, PlayerState player, int globalTurn,
        bool inputAllowed, bool technologyEnabled, out string reason)
    {
        reason = "仅可在本人的出牌输入阶段精调";
        var r = player?.techState?.precisionTune;
        if (!inputAllowed || !technologyEnabled || race == null || player == null || player.isAI ||
            !race.Players.Contains(player) || player.deck == null || r == null || !r.InputOpen ||
            !r.Matches(race, player) || r.TurnId != globalTurn ||
            RaceTurnRules.GetStartAction(player) != RaceTurnStartAction.SelectGear) return false;
        reason = "需要有效意大利L3，且本场精调尚未使用";
        if (r.UsedThisRace || !race.GetModifiers(player).hasCavallinoRampante) return false;
        reason = string.Empty; return true;
    }

    public static bool TryCreate(RaceSession race, PlayerState player, IReadOnlyList<CardData> cards, CardData target,
        int globalTurn, int requestVersion, bool inputAllowed, bool technologyEnabled,
        out ItalyPrecisionTuneActivationPlan plan, out string reason, CardData driverSlowdownCard = null)
    {
        plan = null;
        if (!CanPrepare(race, player, globalTurn, inputAllowed, technologyEnabled, out reason)) return false;
        reason = "请选择已选中的普通速度牌：有效贡献至少2，且不能与车手减速选同一张";
        if (!ItalyPrecisionTuneRules.IsEligibleTarget(player, cards, target, driverSlowdownCard)) return false;
        plan = new ItalyPrecisionTuneActivationPlan(race, player, cards, target, globalTurn, requestVersion);
        reason = string.Empty; return true;
    }

    public void Cancel() => closed = true;

    /// <summary>No mutation. A stale request never moves a subset of the selection or consumes the use.</summary>
    public bool Validate(RaceSession race, PlayerState player, IReadOnlyList<CardData> cards, int globalTurn,
        int requestVersion, bool inputAllowed, bool technologyEnabled, out string reason, CardData driverSlowdownCard = null)
    {
        reason = "精调确认已过期，请重新选择";
        if (closed || !ReferenceEquals(session, race) || !ReferenceEquals(owner, player) ||
            !ReferenceEquals(deck, player?.deck) || !ReferenceEquals(state, player?.techState) ||
            !ReferenceEquals(runtime, player?.techState?.precisionTune) || !ReferenceEquals(skill, player?.driverSkill) ||
            turn != globalTurn || request != requestVersion || runtime.Version != version || runtime.InputId != input ||
            player.gear != gear || player.position != position || player.lap != lap || player.teamId != team ||
            state.teamId != techTeam || state.sunNeverSetsTarget != borrowed || state.activeNodeIds == null || !active.SetEquals(state.activeNodeIds) ||
            perCardBonus != DriverSkillRules.GetSpeedPerCardBonus(player.driverSkill) ||
            hotpotPending != TrickCardRules.HasHotpotAttack(player.trickState) ||
            hotpotApplied != player.hotpotAttackAppliedThisTurn || hotpotValue != player.hotpotAttackCardValueThisTurn ||
            !hand.Matches(deck.Hand) || !played.Matches(player.playedSpeedCardsThisTurn) || !selected.Matches(cards)) return false;
        return CanPrepare(race, player, globalTurn, inputAllowed, technologyEnabled, out reason) &&
            ItalyPrecisionTuneRules.IsEligibleTarget(player, cards, Target, driverSlowdownCard);
    }

    internal void CommitValidated()
    {
        runtime.CommitValidated(Target); closed = true;
    }

    /// <summary>Project only into an independently copied participant after its virtual card selection succeeds.</summary>
    internal void ApplyPresentation(RaceSession race, PlayerState original, PlayerState copy,
        IReadOnlyList<CardData> cards)
    {
        if (ReferenceEquals(original, copy) || ReferenceEquals(original?.techState, copy?.techState) ||
            !Validate(race, original, cards, turn, request, true, true, out _) ||
            !copy.playedSpeedCardsThisTurn.Contains(Target)) return;
        copy.techState.precisionTune.CommitValidated(Target);
    }
}
