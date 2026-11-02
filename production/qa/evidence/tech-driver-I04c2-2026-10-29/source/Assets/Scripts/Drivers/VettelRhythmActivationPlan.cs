using System;
using System.Collections.Generic;

/// <summary>ADR-002: fixed-source D04 activation after complete live-input snapshots; one permanent heat to hand.</summary>
public sealed class VettelRhythmActivationPlan
{
    private readonly RaceSession session;
    private readonly PlayerState owner;
    private readonly DriverSkillRuntimeState skill;
    private readonly VettelRhythmRuntime rhythm;
    private readonly CardDeck deck;
    private readonly HeatPool pool;
    private readonly TrickCardState trick;
    private readonly TechTreeState tech;
    private readonly GermanBurstRuntime burst;
    private readonly string driverId;
    private readonly TeamId team, techTeam;
    private readonly TeamId? borrowed;
    private readonly int turn, gear, request, input, initialization, uses, tier, version, lap, position;
    private readonly int engine, capacityBonus, retirement, burstVersion, fuelLap, breadRemaining;
    private readonly bool tempHeat, breadActive, beerOwned, exitOwned;
    private readonly HashSet<string> activeNodes, unlockedNodes;
    private readonly ZoneSnapshot hand, draw, discard, receipts;
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
            cards = new CardData[source.Count]; types = new CardType[source.Count]; values = new int[source.Count];
            temporary = new bool[source.Count]; tricks = new string[source.Count];
            for (int i = 0; i < source.Count; i++)
            { CardData c = source[i]; cards[i] = c; if (c == null) continue; types[i] = c.type; values[i] = c.value; temporary[i] = c.isTemp; tricks[i] = c.trickId; }
        }
        public bool Matches(IReadOnlyList<CardData> source)
        {
            if (cards.Length != source.Count) return false;
            for (int i = 0; i < cards.Length; i++)
            { CardData c = source[i]; if (!ReferenceEquals(c, cards[i]) || c == null || c.type != types[i] || c.value != values[i] || c.isTemp != temporary[i] || c.trickId != tricks[i]) return false; }
            return true;
        }
    }

    public IReadOnlyList<VettelNativeSource> Choices { get; }
    public bool IsClosed => closed;
    public CardData PaidHeat { get; private set; }

    private VettelRhythmActivationPlan(RaceSession race, PlayerState player, int globalTurn, int pendingGear,
        int requestVersion, List<VettelNativeSource> choices)
    {
        session = race; owner = player; skill = player.driverSkill; rhythm = skill.Rhythm; deck = player.deck; pool = deck.heatPool;
        driverId = player.driverId; team = player.teamId; turn = globalTurn; gear = pendingGear; request = requestVersion;
        input = rhythm.InputsStarted; initialization = skill.InitializationVersion; uses = skill.UsesRemaining;
        tier = skill.Tier; version = rhythm.Version; lap = player.lap; position = player.position;
        engine = pool.remaining; capacityBonus = deck.RegionalCapacityBonus; retirement = deck.RegionalHeatPendingRetirement;
        trick = player.trickState; tempHeat = trick?.tempHeatAvailable ?? false; breadActive = trick?.schwarzbrotActive ?? false;
        breadRemaining = trick?.schwarzbrotRemaining ?? 0; tech = player.techState; techTeam = tech?.teamId ?? default;
        borrowed = tech?.sunNeverSetsTarget; activeNodes = tech == null ? null : new HashSet<string>(tech.activeNodeIds);
        unlockedNodes = tech == null ? null : new HashSet<string>(tech.unlockedNodeIds);
        burst = tech?.germanBurst; burstVersion = burst?.Version ?? -1; fuelLap = tech?.schwarzbierFuelLastLap ?? -1;
        var modifiers = race.GetModifiers(player); beerOwned = modifiers.hasSchwarzbierFuel; exitOwned = modifiers.hasWurstplatteSuspension;
        hand = new ZoneSnapshot(deck.Hand); draw = new ZoneSnapshot(deck.DrawPile); discard = new ZoneSnapshot(deck.DiscardPile);
        receipts = new ZoneSnapshot(player.heatPaidCardsThisTurn); Choices = choices.AsReadOnly();
    }

    /// <summary>Availability never pays, reserves, changes source or consumes an input opportunity.</summary>
    public static bool CanPrepare(RaceSession race, PlayerState player, int pendingGear,
        DriverSkillActivationContext context, out string reason)
    {
        reason = "当前没有可用的维特尔驾驶输入";
        var skill = player?.driverSkill; var rhythm = skill?.Rhythm;
        if (!context.CanAcceptInput || rhythm == null || !rhythm.HasInputScope(race, player)) return false;
        if (rhythm.HasWindow || rhythm.ActivatedThisInput || !rhythm.ManualPath && (skill.IsActive || skill.ActivatedThisTurn))
        { reason = "固定来源窗口尚未结束，不能叠加或更换"; return false; }
        if (skill.UsesRemaining <= 0) { reason = "本场主动次数已用完"; return false; }
        if (pendingGear < 1 || pendingGear > 4) { reason = "请选择有效挡位"; return false; }
        if (player.deck.heatPool.remaining < 1) { reason = "需要整笔支付1张永久引擎热"; return false; }
        if (player.techState != null && (player.techState.activeNodeIds == null || player.techState.unlockedNodeIds == null)) return false;
        if (!ValidZones(player.deck)) { reason = "牌组原实例无效"; return false; }
        reason = string.Empty; return true;
    }

    /// <summary>Free source preview. A newer request versions out old delegates without spending resources.</summary>
    public static bool TryCreate(RaceSession race, PlayerState player, int globalTurn, int pendingGear,
        int requestVersion, DriverSkillActivationContext context, out VettelRhythmActivationPlan plan, out string reason)
    {
        plan = null;
        if (!CanPrepare(race, player, pendingGear, context, out reason)) return false;
        var modifiers = race.GetModifiers(player);
        var choices = new List<VettelNativeSource> { VettelNativeSource.BasicCruise };
        if (modifiers.hasSchwarzbierFuel) choices.Add(VettelNativeSource.PaidBeer);
        if (modifiers.hasWurstplatteSuspension) choices.Add(VettelNativeSource.GermanCornerExit);
        player.driverSkill.Rhythm.AdvancePlanVersion();
        plan = new VettelRhythmActivationPlan(race, player, globalTurn, pendingGear, requestVersion, choices); return true;
    }

    /// <summary>Only this unpaid request closes; a stale cancel cannot stop a newer plan or active window.</summary>
    public void Cancel() => closed = true;

    /// <summary>All live snapshots and chosen ownership validate before the single permanent payment and race-use commit.</summary>
    public bool TryCommit(RaceSession race, PlayerState player, int globalTurn, int pendingGear,
        int requestVersion, DriverSkillActivationContext context, VettelNativeSource source, out string reason)
    {
        reason = "固定来源选择已过期，请重新选择";
        if (!SnapshotsMatch(race, player, globalTurn, pendingGear, requestVersion) ||
            !CanPrepare(race, player, pendingGear, context, out reason)) { Cancel(); return false; }
        bool known = false; foreach (VettelNativeSource choice in Choices) if (choice == source) known = true;
        if (!known || !deck.TryPayFixedPermanentHeatToHand(out CardData paid))
        { reason = "来源尚未解锁或永久热费用已失效"; Cancel(); return false; }
        skill.CommitValidatedActivation(); rhythm.CommitActivation(source); race.RecordFixedPermanentPayment(player, paid);
        PaidHeat = paid; closed = true; reason = string.Empty; return true;
    }

    private bool SnapshotsMatch(RaceSession race, PlayerState player, int globalTurn, int pendingGear, int requestVersion)
    {
        if (closed || player == null || !ReferenceEquals(session, race) || !ReferenceEquals(owner, player) ||
            !ReferenceEquals(skill, player.driverSkill) || !ReferenceEquals(rhythm, player.driverSkill?.Rhythm) ||
            !ReferenceEquals(deck, player.deck) || !ReferenceEquals(pool, player.deck?.heatPool) ||
            driverId != player.driverId || team != player.teamId || turn != globalTurn || gear != pendingGear || request != requestVersion ||
            input != rhythm.InputsStarted || initialization != skill.InitializationVersion || uses != skill.UsesRemaining ||
            tier != skill.Tier || version != rhythm.Version || lap != player.lap || position != player.position ||
            engine != pool.remaining || capacityBonus != deck.RegionalCapacityBonus || retirement != deck.RegionalHeatPendingRetirement ||
            !ReferenceEquals(trick, player.trickState) || tempHeat != (trick?.tempHeatAvailable ?? false) ||
            breadActive != (trick?.schwarzbrotActive ?? false) || breadRemaining != (trick?.schwarzbrotRemaining ?? 0) ||
            !TechMatches(player) || !hand.Matches(deck.Hand) || !draw.Matches(deck.DrawPile) || !discard.Matches(deck.DiscardPile) ||
            !receipts.Matches(player.heatPaidCardsThisTurn)) return false;
        var modifiers = race.GetModifiers(player);
        return beerOwned == modifiers.hasSchwarzbierFuel && exitOwned == modifiers.hasWurstplatteSuspension;
    }
    private bool TechMatches(PlayerState player)
    {
        if (!ReferenceEquals(tech, player.techState)) return false;
        return tech == null || techTeam == tech.teamId && borrowed == tech.sunNeverSetsTarget &&
            tech.activeNodeIds != null && tech.unlockedNodeIds != null && activeNodes.SetEquals(tech.activeNodeIds) &&
            unlockedNodes.SetEquals(tech.unlockedNodeIds) && ReferenceEquals(burst, tech.germanBurst) &&
            burstVersion == (burst?.Version ?? -1) && fuelLap == tech.schwarzbierFuelLastLap;
    }
    private static bool ValidZones(CardDeck deck)
    {
        var seen = new HashSet<CardData>();
        return ValidZone(deck.Hand, seen) && ValidZone(deck.DrawPile, seen) && ValidZone(deck.DiscardPile, seen);
    }
    private static bool ValidZone(IReadOnlyList<CardData> cards, HashSet<CardData> seen)
    { foreach (CardData card in cards) if (card == null || !seen.Add(card)) return false; return true; }
}
