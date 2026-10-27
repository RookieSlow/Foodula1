using System;
using System.Collections.Generic;

/// <summary>Pure UK L1 eligibility and capacity; keeps legacy node/effect IDs stable.</summary>
public static class UkPrepMealRules
{
    public static bool IsOrdinarySpeed(CardData card) => card != null && card.IsSpeed &&
        !card.isTemp && string.IsNullOrEmpty(card.trickId);

    public static int GetRecoveryLimit(TechTreeState state, TechTreeDatabase database, string country) =>
        state != null && state.teamId == TeamId.UK &&
        TechTreeRules.HasEffect(state, database, TechEffectType.SunNeverSets) &&
        TechTreeRules.IsSunNeverSetsHomeBonus(country) ? 2 : 1;

    /// <summary>Does not impose Hart's two-speed/gear-surplus gate: missing-card risk remains a choice.</summary>
    public static bool CanPrepare(RaceSession session, PlayerState player, int turn, string country,
        bool inputAllowed, bool technologyEnabled, out string reason)
    {
        reason = "当前科技不可用";
        if (!technologyEnabled || player?.techState == null || player.teamId != TeamId.UK ||
            player.techState.teamId != TeamId.UK || session?.Players == null ||
            !session.Players.Contains(player) || player.deck?.heatPool == null ||
            !TechTreeRules.HasEffect(player.techState, session.TechDb, TechEffectType.FishAndChips)) return false;
        reason = "当前没有可用的驾驶输入";
        if (!inputAllowed || RaceTurnRules.GetStartAction(player) != RaceTurnStartAction.SelectGear) return false;
        UkPrepMealRuntime runtime = player.techState.prepMeal;
        reason = "本圈已用（0/1）";
        if (runtime.LastUsedLap == player.lap) return false;
        reason = "仅首次正式出牌前可用";
        if (!runtime.IsOpen || runtime.InputTurn != turn || player.playedSpeedCardsThisTurn.Count > 0 ||
            (player.trickState != null && player.trickState.trickPlayedThisTurn)) return false;
        reason = "需要1张普通速度牌";
        bool hasSpeed = false;
        var seen = new HashSet<CardData>();
        foreach (CardData card in player.deck.Hand)
        {
            if (card == null || !seen.Add(card)) { reason = "手牌来源无效"; return false; }
            if (IsOrdinarySpeed(card)) hasSpeed = true;
        }
        if (!hasSpeed) return false;
        List<CardData> sources = runtime.GetRecoverableSources(player.deck,
            GetRecoveryLimit(player.techState, session.TechDb, country));
        reason = "没有可净回引擎的旧永久热";
        if (sources.Count <= player.deck.RegionalHeatPendingRetirement) return false;
        reason = string.Empty;
        return true;
    }
}

/// <summary>A single exact-instance UK technology transaction, separate from driver skill uses.</summary>
public sealed class UkPrepMealActivationPlan
{
    private readonly RaceSession session;
    private readonly PlayerState owner;
    private readonly CardDeck deck;
    private readonly HeatPool pool;
    private readonly TechTreeState tech;
    private readonly UkPrepMealRuntime runtime;
    private readonly int turn, version, inputVersion, lap, position, gear, engine, retirement, limit;
    private readonly CardData[] hand;
    private readonly int[] values;
    private readonly List<CardData> sources;
    private bool closed;

    public IReadOnlyList<CardData> Choices { get; }
    public HeatCoolingResult PreviewSources { get; }
    public int PreviewRetired { get; }
    public int PreviewRestored => sources.Count - PreviewRetired;
    public bool IsClosed => closed;

    private UkPrepMealActivationPlan(RaceSession session, PlayerState owner, int turn,
        int version, string country, List<CardData> choices)
    {
        this.session = session; this.owner = owner; this.turn = turn; this.version = version;
        deck = owner.deck; pool = deck.heatPool; tech = owner.techState; runtime = tech.prepMeal;
        inputVersion = runtime.Version; lap = owner.lap; position = owner.position; gear = owner.gear;
        engine = pool.remaining; retirement = deck.RegionalHeatPendingRetirement;
        limit = UkPrepMealRules.GetRecoveryLimit(tech, session.TechDb, country);
        sources = runtime.GetRecoverableSources(deck, limit);
        int fromHand = 0;
        foreach (CardData card in sources) if (deck.ContainsInHand(card)) fromHand++;
        PreviewSources = new HeatCoolingResult(fromHand, 0, sources.Count - fromHand);
        PreviewRetired = Math.Min(sources.Count, retirement);
        hand = new CardData[deck.Hand.Count]; values = new int[hand.Length];
        for (int i = 0; i < hand.Length; i++) { hand[i] = deck.Hand[i]; values[i] = hand[i].value; }
        Choices = choices.AsReadOnly();
    }

    public static bool TryCreate(RaceSession session, PlayerState player, int turn, int version,
        string country, bool inputAllowed, bool technologyEnabled,
        out UkPrepMealActivationPlan plan, out string reason)
    {
        plan = null;
        if (!UkPrepMealRules.CanPrepare(session, player, turn, country, inputAllowed, technologyEnabled, out reason))
            return false;
        var choices = new List<CardData>();
        foreach (CardData card in player.deck.Hand) if (UkPrepMealRules.IsOrdinarySpeed(card)) choices.Add(card);
        plan = new UkPrepMealActivationPlan(session, player, turn, version, country, choices);
        return true;
    }

    public void Cancel() => closed = true;

    public bool TryCommit(RaceSession currentSession, PlayerState currentOwner, int globalTurn, int requestVersion,
        string country, bool inputAllowed, bool technologyEnabled, CardData cost,
        out HeatCoolingResult removed, out int retired, out int restored, out string reason)
    {
        removed = default; retired = 0; restored = 0;
        reason = "选择已过期，请重新选择";
        if (closed || !ReferenceEquals(session, currentSession) || !ReferenceEquals(owner, currentOwner) ||
            globalTurn != turn || requestVersion != version || !ReferenceEquals(owner.deck, deck) ||
            !ReferenceEquals(deck.heatPool, pool) || !ReferenceEquals(owner.techState, tech) ||
            !ReferenceEquals(tech.prepMeal, runtime) || inputVersion != runtime.Version ||
            owner.lap != lap || owner.position != position || owner.gear != gear || pool.remaining != engine ||
            deck.RegionalHeatPendingRetirement != retirement || deck.Hand.Count != hand.Length ||
            limit != UkPrepMealRules.GetRecoveryLimit(tech, session.TechDb, country)) return false;
        for (int i = 0; i < hand.Length; i++)
            if (!ReferenceEquals(hand[i], deck.Hand[i]) || hand[i].value != values[i]) return false;
        if (!UkPrepMealRules.CanPrepare(session, owner, turn, country, inputAllowed, technologyEnabled, out reason))
            return false;
        List<CardData> current = runtime.GetRecoverableSources(deck, limit);
        if (current.Count != sources.Count) { reason = "旧热来源已变更"; return false; }
        for (int i = 0; i < sources.Count; i++)
            if (!ReferenceEquals(sources[i], current[i])) { reason = "旧热来源已变更"; return false; }
        bool known = false;
        foreach (CardData card in Choices) if (ReferenceEquals(card, cost)) known = true;
        if (!known || !deck.TryCommitPrepMeal(cost, sources, out removed, out retired, out restored))
        { reason = "费用或旧热来源已变更"; return false; }
        runtime.CommitValidatedUse(lap);
        closed = true;
        reason = string.Empty;
        return true;
    }
}
