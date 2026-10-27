using System;
using System.Collections.Generic;

/// <summary>ADR-002: one fixed fuel payment after exact live-input snapshots validate. No UI or RNG.</summary>
public sealed class GermanFuelActivationPlan
{
    private readonly RaceSession session;
    private readonly PlayerState owner;
    private readonly CardDeck deck;
    private readonly HeatPool pool;
    private readonly TechTreeState tech;
    private readonly GermanBurstRuntime runtime;
    private readonly int turn, request, version, lap, position, gear, engine, retirement, fuelLap;
    private readonly TeamId team, techTeam;
    private readonly TeamId? borrowed;
    private readonly HashSet<string> nodes;
    private readonly ZoneSnapshot hand, draw, discard, paid;
    private bool closed;

    private sealed class ZoneSnapshot
    {
        private readonly CardData[] cards;
        private readonly CardType[] types;
        private readonly int[] values;
        private readonly bool[] temporary;
        private readonly string[] tricks;
        public ZoneSnapshot(IReadOnlyList<CardData> source)
        {
            int count = source.Count;
            cards = new CardData[count]; types = new CardType[count]; values = new int[count];
            temporary = new bool[count]; tricks = new string[count];
            for (int i = 0; i < count; i++)
            {
                CardData card = source[i]; cards[i] = card;
                if (card == null) continue;
                types[i] = card.type; values[i] = card.value; temporary[i] = card.isTemp; tricks[i] = card.trickId;
            }
        }
        public bool Matches(IReadOnlyList<CardData> source)
        {
            if (cards.Length != source.Count) return false;
            for (int i = 0; i < cards.Length; i++)
            {
                CardData card = source[i];
                if (!ReferenceEquals(card, cards[i]) || card == null || card.type != types[i] ||
                    card.value != values[i] || card.isTemp != temporary[i] || card.trickId != tricks[i]) return false;
            }
            return true;
        }
    }

    public bool IsClosed => closed;
    public CardData PaidHeat { get; private set; }
    public int MovementBonus => 2;

    private GermanFuelActivationPlan(RaceSession race, PlayerState player, int globalTurn, int requestVersion)
    {
        session = race; owner = player; deck = player.deck; pool = deck.heatPool;
        tech = player.techState; runtime = tech.germanBurst;
        turn = globalTurn; request = requestVersion; version = runtime.Version;
        lap = player.lap; position = player.position; gear = player.gear;
        engine = pool.remaining; retirement = deck.RegionalHeatPendingRetirement; fuelLap = tech.schwarzbierFuelLastLap;
        team = player.teamId; techTeam = tech.teamId; borrowed = tech.sunNeverSetsTarget;
        nodes = new HashSet<string>(tech.activeNodeIds);
        hand = new ZoneSnapshot(deck.Hand); draw = new ZoneSnapshot(deck.DrawPile);
        discard = new ZoneSnapshot(deck.DiscardPile); paid = new ZoneSnapshot(player.heatPaidCardsThisTurn);
    }

    /// <summary>Availability is read-only; callers supply the actual human card-input and technology gates.</summary>
    public static bool CanPrepare(RaceSession race, PlayerState player, int globalTurn,
        bool inputAllowed, bool technologyEnabled, out string reason)
    {
        reason = "仅可在本人的出牌输入阶段使用黑啤";
        if (!inputAllowed || !technologyEnabled || race == null || player == null || player.isAI ||
            !race.Players.Contains(player) || player.techState == null || player.techState.activeNodeIds == null || player.deck?.heatPool == null ||
            player.techState.germanBurst == null || player.techState.germanBurst.TurnId != globalTurn ||
            RaceTurnRules.GetStartAction(player) != RaceTurnStartAction.SelectGear) return false;
        reason = "需要1张永久引擎热，且本圈次数或本回合三次预算尚未用完";
        if (!race.CanTriggerSchwarzbierFuel(player, true)) return false;
        reason = string.Empty; return true;
    }

    /// <summary>Free request creation; a request never declares L3, reserves heat or locks its race use.</summary>
    public static bool TryCreate(RaceSession race, PlayerState player, int globalTurn, int requestVersion,
        bool inputAllowed, bool technologyEnabled, out GermanFuelActivationPlan plan, out string reason)
    {
        plan = null;
        if (!CanPrepare(race, player, globalTurn, inputAllowed, technologyEnabled, out reason)) return false;
        plan = new GermanFuelActivationPlan(race, player, globalTurn, requestVersion); return true;
    }

    /// <summary>Cancel only this request; an unpaid L3 declaration remains available.</summary>
    public void Cancel() => closed = true;

    /// <summary>Validate every captured boundary before the one atomic permanent-heat commit.</summary>
    public bool TryCommit(RaceSession race, PlayerState player, int globalTurn, int requestVersion,
        bool inputAllowed, bool technologyEnabled, out string reason)
    {
        reason = "黑啤确认已过期，请重新选择";
        bool current = !closed && ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
            ReferenceEquals(deck, player?.deck) && ReferenceEquals(pool, player?.deck?.heatPool) &&
            ReferenceEquals(tech, player?.techState) && ReferenceEquals(runtime, player?.techState?.germanBurst);
        if (!current || turn != globalTurn || request != requestVersion || version != runtime.Version ||
            lap != player.lap || position != player.position || gear != player.gear || team != player.teamId ||
            techTeam != tech.teamId || borrowed != tech.sunNeverSetsTarget || tech.activeNodeIds == null || !nodes.SetEquals(tech.activeNodeIds) ||
            engine != pool.remaining || retirement != deck.RegionalHeatPendingRetirement || fuelLap != tech.schwarzbierFuelLastLap ||
            !hand.Matches(deck.Hand) || !draw.Matches(deck.DrawPile) || !discard.Matches(deck.DiscardPile) ||
            !paid.Matches(player.heatPaidCardsThisTurn)) { Cancel(); return false; }
        if (!CanPrepare(race, player, globalTurn, inputAllowed, technologyEnabled, out reason))
        { Cancel(); return false; }
        if (!race.TryCommitSchwarzbierFuel(player, true, out CardData receipt))
        { reason = "永久热费用或黑啤次数已失效"; Cancel(); return false; }
        PaidHeat = receipt; closed = true; reason = string.Empty; return true;
    }
}
