using System;

/// <summary>D02 two-leg owner-input clock. Payments live in the immutable activation plan.</summary>
public sealed class MansellChargeRuntime
{
    private RaceSession session;
    private PlayerState owner, target;
    private DriverSkillRuntimeState skill;
    private CardDeck deck;
    private HeatPool pool;
    private string driverId, targetId;
    private int firstInput, initialization, nodes, finish;
    private long firstGap;
    private int secondOwnerLap, secondOwnerPosition, secondTargetLap, secondTargetPosition;
    private bool waitingForNextInput, blocksFirstThisInput;

    public int Version { get; private set; }
    public bool IsSecondLegAvailable { get; private set; }
    public bool CommittedThisInput { get; private set; }
    public int MovementBonusThisInput { get; private set; }
    public PlayerState Target => target;
    public bool HasSequence => waitingForNextInput || IsSecondLegAvailable || CommittedThisInput || blocksFirstThisInput;
    public bool CanStartFirst => !HasSequence;

    /// <summary>Observe the next actual owner input once, after its ordinary skill clock advances.</summary>
    public void BeginInput(RaceSession race, PlayerState player, int totalNodes, int startFinish)
    {
        Version++;
        MovementBonusThisInput = 0; CommittedThisInput = false; IsSecondLegAvailable = false;
        bool pending = waitingForNextInput;
        waitingForNextInput = false;
        blocksFirstThisInput = pending;
        if (!pending) return;
        var current = player?.driverSkill;
        if (current == null || !ReferenceEquals(session, race) || !ReferenceEquals(owner, player) ||
            !ReferenceEquals(skill, current) || !ReferenceEquals(current.Charge, this) ||
            !ReferenceEquals(deck, player.deck) || !ReferenceEquals(pool, player.deck?.heatPool) ||
            player.driverId != driverId || current.InitializationVersion != initialization ||
            current.DrivingInputsStarted != firstInput + 1 || totalNodes != nodes || startFinish != finish ||
            !Eligible(race, player) || pool.remaining < 1 || target == null || target.driverId != targetId)
            return;
        PlayerState nearest = FindNearest(race, player, totalNodes, startFinish, out long gap);
        IsSecondLegAvailable = ReferenceEquals(nearest, target) && gap > 0 && gap < firstGap;
        secondOwnerLap = player.lap; secondOwnerPosition = player.position;
        secondTargetLap = target.lap; secondTargetPosition = target.position;
    }

    internal bool HasInputScope(RaceSession race, PlayerState player, int totalNodes, int startFinish) =>
        ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
        ReferenceEquals(skill, player?.driverSkill) && skill != null && ReferenceEquals(skill.Charge, this) &&
        skill.InitializationVersion == initialization && skill.DrivingInputsStarted == firstInput + 1 &&
        totalNodes == nodes && startFinish == finish;

    internal bool MatchesSecondObservation(RaceSession race, PlayerState player, int totalNodes, int startFinish) =>
        IsSecondLegAvailable && ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
        ReferenceEquals(skill, player?.driverSkill) && ReferenceEquals(skill.Charge, this) &&
        ReferenceEquals(deck, player.deck) && ReferenceEquals(pool, player.deck?.heatPool) &&
        player.driverId == driverId && skill.InitializationVersion == initialization &&
        skill.DrivingInputsStarted == firstInput + 1 && target != null && target.driverId == targetId &&
        totalNodes == nodes && startFinish == finish && player.lap == secondOwnerLap &&
        player.position == secondOwnerPosition && target != null && target.lap == secondTargetLap &&
        target.position == secondTargetPosition;

    /// <summary>Expire this input; only a committed first leg can survive until the next owner observation.</summary>
    public void CloseInput()
    {
        Version++; MovementBonusThisInput = 0; CommittedThisInput = false;
        IsSecondLegAvailable = false; blocksFirstThisInput = false;
    }

    /// <summary>Break pursuit on a skipped/pit/terminal input or race lifecycle boundary, without refunds.</summary>
    public void Interrupt()
    {
        CloseInput(); waitingForNextInput = false;
        session = null; owner = null; target = null; skill = null; deck = null; pool = null;
    }

    /// <summary>Decline only this exact continuation window; stale UI delegates cannot close a newer one.</summary>
    internal void DeclineSecond(int expectedVersion)
    {
        if (!IsSecondLegAvailable || Version != expectedVersion) return;
        IsSecondLegAvailable = false; blocksFirstThisInput = true; Version++;
    }

    internal void AdvancePlanVersion() => Version++;

    internal void CommitFirst(RaceSession race, PlayerState player, PlayerState leader,
        int totalNodes, int startFinish, long gap)
    {
        session = race; owner = player; target = leader; skill = player.driverSkill;
        deck = player.deck; pool = deck.heatPool; driverId = player.driverId; targetId = leader.driverId;
        firstInput = skill.DrivingInputsStarted; initialization = skill.InitializationVersion;
        nodes = totalNodes; finish = startFinish; firstGap = gap;
        waitingForNextInput = true; blocksFirstThisInput = true;
        CommittedThisInput = true; MovementBonusThisInput = skill.Tier >= 2 ? 2 : 1; Version++;
    }

    internal void CommitSecond()
    {
        IsSecondLegAvailable = false; waitingForNextInput = false; blocksFirstThisInput = true;
        CommittedThisInput = true; MovementBonusThisInput = skill.Tier >= 2 ? 3 : 2; Version++;
    }

    /// <summary>Only immutable identities/scalars are copied; a forecast cannot commit through the real runtime identity.</summary>
    public MansellChargeRuntime CreatePresentationCopy() => (MansellChargeRuntime)MemberwiseClone();

    internal static bool Eligible(RaceSession race, PlayerState player) =>
        race?.Players != null && player?.driverSkill != null && player.deck?.heatPool != null &&
        race.Players.Contains(player) && !player.isAI && player.driverSkill.Enabled &&
        player.driverSkill.Skill == DriverActiveSkillId.AngryCharge &&
        DriverSkillRules.GetSkill(player.driverId) == DriverActiveSkillId.AngryCharge &&
        player.driverSkill.Tier > 0 && player.driverSkill.IsDrivingInputOpen &&
        RaceTurnRules.GetStartAction(player) == RaceTurnStartAction.SelectGear;

    internal static PlayerState FindNearest(RaceSession race, PlayerState player, int totalNodes,
        int startFinish, out long distance)
    {
        distance = long.MaxValue; PlayerState nearest = null;
        if (race?.Players == null || player == null || totalNodes <= 0) return null;
        long origin = HunterSlipstreamRuntime.Progress(player, totalNodes, startFinish);
        foreach (PlayerState other in race.Players)
        {
            if (other == null || ReferenceEquals(other, player) || RaceTurnRules.IsTerminal(other)) continue;
            long gap = HunterSlipstreamRuntime.Progress(other, totalNodes, startFinish) - origin;
            if (gap <= 0 || gap >= distance) continue;
            nearest = other; distance = gap;
        }
        return nearest;
    }
}
