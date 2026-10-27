using System;
using System.Collections.Generic;

/// <summary>Actual source movement and net engine return for Mansell's passive (locked D02 draft).</summary>
public readonly struct MansellPursuitCoolingResult
{
    public MansellPursuitCoolingResult(bool triggered, HeatCoolingResult removed, int retired, int restored)
    { Triggered = triggered; Removed = removed; Retired = retired; Restored = restored; }
    public bool Triggered { get; }
    public HeatCoolingResult Removed { get; }
    public int Retired { get; }
    public int Restored { get; }
}

/// <summary>
/// D02 passive: compare the same nearest car at two owner inputs, then cool only a frozen hand instance.
/// Race-only state; stable LionsHeart/driver/XP identities and active AngryCharge are unchanged.
/// </summary>
public sealed class MansellPursuitRuntime
{
    private RaceSession session;
    private PlayerState owner, target;
    private DriverSkillRuntimeState skill;
    private CardDeck deck;
    private HeatPool pool;
    private string ownerId, targetId;
    private int input, initialization, nodes, startFinish, ownerLap, ownerPosition, targetLap, targetPosition;
    private long gap;
    private bool pending;
    private List<CardData> oldHandHeat = new List<CardData>();
    private HashSet<int> triggeredLaps = new HashSet<int>();

    public PlayerState Target => target;
    public bool IsPending => pending;
    public bool HasTriggeredInLap(int lap) => triggeredLaps.Contains(lap);

    /// <summary>Observe once per real owner input, preserving the previous input across global round cleanup.</summary>
    public void BeginInput(RaceSession race, PlayerState player, int totalNodes, int startFinishIndex = 0)
    {
        CloseInput();
        if (!IsEligibleOwner(race, player, totalNodes)) { Interrupt(); return; }
        int finish = ((startFinishIndex % totalNodes) + totalNodes) % totalNodes;
        PlayerState nearest = FindNearest(race, player, totalNodes, finish, out long distance);
        var currentSkill = player.driverSkill;
        bool consecutive = ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
            ReferenceEquals(skill, currentSkill) && ReferenceEquals(deck, player.deck) && ReferenceEquals(pool, player.deck.heatPool) &&
            ownerId == player.driverId && initialization == currentSkill.InitializationVersion &&
            input + 1 == currentSkill.DrivingInputsStarted && nodes == totalNodes && startFinish == finish;
        bool sameTarget = nearest != null && ReferenceEquals(nearest, target) && targetId == nearest.driverId;
        bool qualifies = consecutive && sameTarget && distance < gap &&
            distance <= DriverSkillRules.GetMansellPursuitRange(currentSkill.PassiveTier) &&
            !triggeredLaps.Contains(player.lap);
        session = race; owner = player; target = nearest; skill = currentSkill; deck = player.deck;
        pool = deck.heatPool;
        ownerId = player.driverId; targetId = nearest?.driverId;
        input = skill.DrivingInputsStarted; initialization = skill.InitializationVersion;
        nodes = totalNodes; startFinish = finish; gap = distance;
        ownerLap = player.lap; ownerPosition = player.position;
        targetLap = nearest?.lap ?? 0; targetPosition = nearest?.position ?? 0;
        if (!qualifies) return;
        triggeredLaps.Add(ownerLap); // First qualifying observation consumes this lap even without old hand heat.
        pending = true;
        foreach (CardData card in deck.Hand)
            if (card != null && card.IsHeat && !card.isTemp &&
                deck.IsUniquePermanentHeatInHandOrDiscard(card) && !oldHandHeat.Contains(card)) oldHandHeat.Add(card);
    }

    /// <summary>One A2 attempt. New heat, temporary heat and displaced original instances cannot fill the frozen source.</summary>
    public MansellPursuitCoolingResult ConsumeAfterDraw(RaceSession race, PlayerState player)
    {
        if (!pending) return default;
        pending = false;
        bool valid = IsEligibleOwner(race, player, nodes) && ReferenceEquals(session, race) &&
            ReferenceEquals(owner, player) && ReferenceEquals(skill, player.driverSkill) &&
            ReferenceEquals(skill.Pursuit, this) && ReferenceEquals(deck, player.deck) &&
            ReferenceEquals(pool, player.deck.heatPool) &&
            ownerId == player.driverId && initialization == skill.InitializationVersion &&
            input == skill.DrivingInputsStarted && player.lap == ownerLap && player.position == ownerPosition &&
            target != null && race.Players.Contains(target) && targetId == target.driverId &&
            !RaceTurnRules.IsTerminal(target) && target.lap == targetLap && target.position == targetPosition;
        if (!valid) { oldHandHeat.Clear(); return default; }
        foreach (CardData card in oldHandHeat)
        {
            if (!deck.ContainsInHand(card) || !deck.IsUniquePermanentHeatInHandOrDiscard(card)) continue;
            int restored = deck.ReturnHeatCardsToPool(new[] { card });
            oldHandHeat.Clear();
            return new MansellPursuitCoolingResult(true, new HeatCoolingResult(1, 0, 0), 1 - restored, restored);
        }
        oldHandHeat.Clear();
        return new MansellPursuitCoolingResult(true, default, 0, 0);
    }

    /// <summary>Expire this input's A2 opportunity without breaking the preceding A1 observation.</summary>
    public void CloseInput() { pending = false; oldHandHeat.Clear(); }

    /// <summary>Interrupt consecutive pursuit, preserving already consumed per-lap opportunities.</summary>
    public void Interrupt()
    {
        CloseInput(); session = null; owner = null; target = null; skill = null; deck = null; pool = null;
        ownerId = null; targetId = null; input = 0;
    }

    /// <summary>Copies mutable collections; retained race references are read-only and commit identity-checked.</summary>
    public MansellPursuitRuntime CreatePresentationCopy()
    {
        var copy = (MansellPursuitRuntime)MemberwiseClone();
        copy.oldHandHeat = new List<CardData>(oldHandHeat);
        copy.triggeredLaps = new HashSet<int>(triggeredLaps);
        return copy;
    }

    private static bool IsEligibleOwner(RaceSession race, PlayerState player, int totalNodes) =>
        race?.Players != null && player?.driverSkill != null && player.deck?.heatPool != null &&
        totalNodes > 0 && race.Players.Contains(player) && !player.isAI && player.driverSkill.Enabled &&
        player.driverSkill.PassiveSkill == DriverPassiveSkillId.LionsHeart &&
        DriverSkillRules.GetPassiveSkill(player.driverId) == DriverPassiveSkillId.LionsHeart &&
        player.driverSkill.PassiveTier > 0 && player.driverSkill.IsDrivingInputOpen &&
        RaceTurnRules.GetStartAction(player) == RaceTurnStartAction.SelectGear;

    private static PlayerState FindNearest(RaceSession race, PlayerState player, int totalNodes, int finish, out long gap)
    {
        PlayerState nearest = null; gap = long.MaxValue;
        long progress = HunterSlipstreamRuntime.Progress(player, totalNodes, finish);
        foreach (PlayerState other in race.Players)
        {
            // A reservation is not an executed stop: this car is still the nearest
            // actual car on track until its position/terminal state changes.
            if (other == null || ReferenceEquals(other, player) || RaceTurnRules.IsTerminal(other)) continue;
            long distance = HunterSlipstreamRuntime.Progress(other, totalNodes, finish) - progress;
            if (distance <= 0 || distance >= gap) continue;
            nearest = other; gap = distance;
        }
        return nearest;
    }
}
