using System;
using System.Collections.Generic;

/// <summary>ADR-002/D05 free target intent, atomically consumed with the original selected-card group.</summary>
public sealed class AscariPrecisionActivationPlan
{
    private readonly RaceSession session;
    private readonly PlayerState owner;
    private readonly CardDeck deck;
    private readonly DriverSkillRuntimeState skill;
    private readonly AscariPrecisionRuntime runtime;
    private readonly IReadOnlyList<TrackNode> nodes;
    private readonly int turn, request, version, input, initialization, uses, tier, gear, position, lap, totalLaps;
    private readonly TeamId team;
    private readonly string driver;
    private readonly Snapshot hand, played, selected;
    private readonly bool hotpotPending, hotpotApplied;
    private readonly int hotpotValue, perCardBonus;
    private bool closed;
    public CardData Target { get; }
    public bool IsClosed => closed;

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
            { CardData c = source[i]; cards[i] = c; if (c == null) continue; types[i] = c.type; values[i] = c.value; temporary[i] = c.isTemp; tricks[i] = c.trickId; }
        }
        public bool Matches(IReadOnlyList<CardData> source)
        {
            if (source == null || cards.Length != source.Count) return false;
            for (int i = 0; i < cards.Length; i++)
            { CardData c = source[i]; if (c == null || !ReferenceEquals(c, cards[i]) || c.type != types[i] || c.value != values[i] || c.isTemp != temporary[i] || c.trickId != tricks[i]) return false; }
            return true;
        }
    }

    private AscariPrecisionActivationPlan(RaceSession race, PlayerState player, IReadOnlyList<CardData> cards,
        CardData target, int globalTurn, int requestVersion, IReadOnlyList<TrackNode> trackNodes, int raceLaps)
    {
        session = race; owner = player; deck = player.deck; skill = player.driverSkill; runtime = skill.Precision;
        Target = target; turn = globalTurn; request = requestVersion; version = runtime.Version; input = runtime.InputsStarted;
        initialization = skill.InitializationVersion; uses = skill.UsesRemaining; tier = skill.Tier;
        gear = player.gear; position = player.position; lap = player.lap; team = player.teamId; driver = player.driverId;
        nodes = trackNodes; totalLaps = raceLaps; perCardBonus = DriverSkillRules.GetSpeedPerCardBonus(skill);
        hand = new Snapshot(deck.Hand); played = new Snapshot(player.playedSpeedCardsThisTurn); selected = new Snapshot(cards);
        hotpotPending = TrickCardRules.HasHotpotAttack(player.trickState);
        hotpotApplied = player.hotpotAttackAppliedThisTurn; hotpotValue = player.hotpotAttackCardValueThisTurn;
    }

    public static bool CanPrepare(RaceSession race, PlayerState player, bool inputAllowed, out string reason)
    {
        reason = "仅可在本人出牌输入选择精密过弯目标";
        DriverSkillRuntimeState state = player?.driverSkill;
        if (!inputAllowed || race == null || player == null || player.isAI || !race.Players.Contains(player) ||
            player.deck == null || state == null || !state.Enabled || state.Skill != DriverActiveSkillId.PrecisionCornering ||
            !state.IsDrivingInputOpen || !state.Precision.HasInputScope(race, player) ||
            RaceTurnRules.GetStartAction(player) != RaceTurnStartAction.SelectGear) return false;
        if (state.Tier <= 0) { reason = "Lv3 解锁精密过弯"; return false; }
        if (state.UsesRemaining <= 0 || state.Precision.CommittedThisInput)
        { reason = "本场次数已用完或本次输入已经提交"; return false; }
        reason = string.Empty; return true;
    }

    public static bool TryCreate(RaceSession race, PlayerState player, IReadOnlyList<CardData> cards, CardData target,
        int globalTurn, int requestVersion, bool inputAllowed, IReadOnlyList<TrackNode> trackNodes, int raceLaps,
        out AscariPrecisionActivationPlan plan, out string reason, ItalyPrecisionTuneActivationPlan technologyPlan = null)
    {
        plan = null;
        if (!CanPrepare(race, player, inputAllowed, out reason)) return false;
        CardData techTarget = technologyPlan?.Target ?? player.techState?.precisionTune.CommittedTarget;
        reason = "请选择有效贡献至少2的已选普通速度牌，且不能与科技精调同牌";
        if (!AscariPrecisionRules.IsEligibleTarget(player, cards, target, techTarget)) return false;
        bool tune = techTarget != null;
        int speed = AscariPrecisionRules.SelectedBaseSpeed(player, cards, tune);
        reason = "减速后的普通速度基础路线必须实际可达弯心，不能预借奖励";
        if (!AscariPrecisionRules.TryGetFirstBaseApex(trackNodes, player.position, speed,
            raceLaps - player.lap, out _, out _)) return false;
        plan = new AscariPrecisionActivationPlan(race, player, cards, target, globalTurn, requestVersion, trackNodes, raceLaps);
        reason = string.Empty; return true;
    }

    public void Cancel() => closed = true;

    public bool Validate(RaceSession race, PlayerState player, IReadOnlyList<CardData> cards, int globalTurn,
        int requestVersion, bool inputAllowed, IReadOnlyList<TrackNode> trackNodes, int raceLaps,
        out string reason, ItalyPrecisionTuneActivationPlan technologyPlan = null)
    {
        reason = "精密过弯目标已过期，请重新选择";
        if (closed || !ReferenceEquals(session, race) || !ReferenceEquals(owner, player) ||
            !ReferenceEquals(deck, player?.deck) || !ReferenceEquals(skill, player?.driverSkill) ||
            !ReferenceEquals(runtime, player?.driverSkill?.Precision) || !ReferenceEquals(nodes, trackNodes) ||
            turn != globalTurn || request != requestVersion || runtime.Version != version || runtime.InputsStarted != input ||
            skill.InitializationVersion != initialization || skill.UsesRemaining != uses || skill.Tier != tier ||
            player.gear != gear || player.position != position || player.lap != lap || player.teamId != team ||
            player.driverId != driver || totalLaps != raceLaps || perCardBonus != DriverSkillRules.GetSpeedPerCardBonus(skill) ||
            hotpotPending != TrickCardRules.HasHotpotAttack(player.trickState) || hotpotApplied != player.hotpotAttackAppliedThisTurn ||
            hotpotValue != player.hotpotAttackCardValueThisTurn || !hand.Matches(deck.Hand) ||
            !played.Matches(player.playedSpeedCardsThisTurn) || !selected.Matches(cards)) return false;
        if (!CanPrepare(race, player, inputAllowed, out reason)) return false;
        CardData techTarget = technologyPlan?.Target ?? player.techState?.precisionTune.CommittedTarget;
        if (!AscariPrecisionRules.IsEligibleTarget(player, cards, Target, techTarget)) return false;
        return AscariPrecisionRules.TryGetFirstBaseApex(nodes, position,
            AscariPrecisionRules.SelectedBaseSpeed(player, cards, techTarget != null), totalLaps - lap, out _, out _);
    }

    internal void CommitValidated()
    { skill.CommitValidatedActivation(); runtime.CommitValidated(Target); closed = true; }

    internal void ApplyPresentation(RaceSession race, PlayerState original, PlayerState copy,
        IReadOnlyList<CardData> cards, ItalyPrecisionTuneActivationPlan technologyPlan)
    {
        if (ReferenceEquals(original, copy) || ReferenceEquals(original?.driverSkill, copy?.driverSkill) ||
            !Validate(race, original, cards, turn, request, true, nodes, totalLaps, out _, technologyPlan) ||
            !copy.playedSpeedCardsThisTurn.Contains(Target)) return;
        copy.driverSkill.CommitValidatedActivation(); copy.driverSkill.Precision.CommitValidated(Target);
    }
}
