using System;

/// <summary>Hart's race-only target lock and once-per-origin-lap reward. No Unity or save state.</summary>
public sealed class HunterSlipstreamRuntime
{
    private RaceSession session;
    private PlayerState owner, target;
    private DriverSkillRuntimeState skill;
    private string ownerId, targetId;
    private int nodes, input, initialization, originLap, startFinish, version, requiredNativeCells;
    private bool prepared, eligible;
    private long nativeStart;

    public int LastUsedLap { get; private set; } = -1;
    public PlayerState Target => target;

    /// <summary>Freeze the nearest actually leading car once at the owner's input start.</summary>
    public void BeginInput(RaceSession race, PlayerState player, int totalNodes, int startFinishIndex = 0)
    {
        CloseInput();
        if (race?.Players == null || player?.driverSkill == null || totalNodes <= 0 ||
            !race.Players.Contains(player) || player.isAI || !player.driverSkill.Enabled ||
            player.driverSkill.PassiveSkill != DriverPassiveSkillId.HuntingInstinct ||
            player.driverSkill.PassiveTier <= 0 || !player.driverSkill.IsDrivingInputOpen ||
            RaceTurnRules.GetStartAction(player) != RaceTurnStartAction.SelectGear ||
            LastUsedLap == player.lap) return;
        session = race; owner = player; skill = player.driverSkill; nodes = totalNodes;
        startFinish = Math.Max(0, startFinishIndex) % nodes;
        ownerId = player.driverId; originLap = player.lap;
        input = skill.DrivingInputsStarted; initialization = skill.InitializationVersion;
        long nearest = Math.Min(3, skill.PassiveTier) + 3L;
        foreach (PlayerState other in race.Players)
        {
            if (other == player || RaceTurnRules.IsTerminal(other) || other.pitStopScheduled) continue;
            long gap = Progress(other, nodes, startFinish) - Progress(player, nodes, startFinish);
            if (gap <= 0 || gap >= nearest) continue;
            nearest = gap; target = other;
        }
        targetId = target?.driverId;
    }

    /// <summary>Observe the already-resolved native chain. This never spends the lap opportunity.</summary>
    public void PrepareNative(RaceSession race, PlayerState player, SlipstreamChainResult chain, int totalNodes)
    {
        if (prepared) return;
        prepared = true;
        if (!Matches(race, player, totalNodes) || !chain.Triggered) return;
        nativeStart = Progress(owner, nodes, startFinish);
        requiredNativeCells = chain.TotalBonus;
        long legStart = nativeStart;
        foreach (SlipstreamResult step in chain.Steps)
        {
            // A positive original leg must reduce a positive gap to this exact locked car.
            // Catching or passing it is valid; a same-cell tie alone is not pursuit.
            if (ReferenceEquals(step.Leader, target) && step.Bonus > 0 && Progress(target, nodes, startFinish) > legStart)
                eligible = true;
            legStart += step.Bonus;
        }
    }

    /// <summary>Called after the complete original chain actually moved, before adding one non-recursive cell.</summary>
    public Reward BeginReward(RaceSession race, PlayerState player, int totalNodes, int actualNativeCells)
    {
        if (!eligible || !Matches(race, player, totalNodes) || actualNativeCells != requiredNativeCells ||
            actualNativeCells <= 0) return null;
        eligible = false;
        return new Reward(this, skill, session, owner, input, initialization, originLap, version);
    }

    private bool Matches(RaceSession race, PlayerState player, int totalNodes) =>
        ReferenceEquals(session, race) && ReferenceEquals(owner, player) && nodes == totalNodes &&
        owner != null && target != null && race?.Players != null && race.Players.Contains(owner) &&
        race.Players.Contains(target) && ReferenceEquals(owner.driverSkill, skill) &&
        ReferenceEquals(skill.Hunting, this) && owner.driverId == ownerId && target.driverId == targetId &&
        skill.Enabled && skill.PassiveSkill == DriverPassiveSkillId.HuntingInstinct &&
        skill.InitializationVersion == initialization && skill.DrivingInputsStarted == input &&
        skill.IsDrivingInputOpen && LastUsedLap != originLap &&
        !RaceTurnRules.IsTerminal(owner) && !RaceTurnRules.ShouldSkip(owner) &&
        !RaceTurnRules.IsTerminal(target);

    /// <summary>Discard the input lock without refunding or resetting a successful lap use.</summary>
    public void CloseInput()
    {
        version++;
        session = null; owner = null; target = null; skill = null;
        ownerId = null; targetId = null; prepared = false; eligible = false;
    }

    /// <summary>Scalar state is independent; shared world references are read-only and identity-validated.</summary>
    public HunterSlipstreamRuntime CreatePresentationCopy() => (HunterSlipstreamRuntime)MemberwiseClone();

    public static long Progress(PlayerState player, int totalNodes, int startFinishIndex = 0) =>
        (long)player.lap * totalNodes + ((player.position - startFinishIndex + totalNodes) % totalNodes);

    /// <summary>One traversal receipt. Quota commits only after the coordinator reports the actual extra cell.</summary>
    public sealed class Reward
    {
        private readonly HunterSlipstreamRuntime runtime;
        private readonly DriverSkillRuntimeState skill;
        private readonly RaceSession session;
        private readonly PlayerState owner;
        private readonly int input, initialization, lap, version;
        private bool closed;

        internal Reward(HunterSlipstreamRuntime runtime, DriverSkillRuntimeState skill, RaceSession session,
            PlayerState owner, int input, int initialization, int lap, int version)
        {
            this.runtime = runtime; this.skill = skill; this.session = session; this.owner = owner;
            this.input = input; this.initialization = initialization; this.lap = lap;
            this.version = version;
        }

        public bool Complete(int actualCells)
        {
            if (closed) return false;
            closed = true;
            bool ownsInput = runtime.version == version && ReferenceEquals(owner.driverSkill, skill) &&
                ReferenceEquals(skill.Hunting, runtime) && ReferenceEquals(runtime.session, session) &&
                ReferenceEquals(runtime.owner, owner) && skill.InitializationVersion == initialization &&
                skill.DrivingInputsStarted == input;
            if (!ownsInput) return false; // An old finally must never close a newer input.
            bool valid = actualCells == 1 && skill.Enabled && skill.IsDrivingInputOpen &&
                owner.driverId == runtime.ownerId && runtime.target != null &&
                runtime.target.driverId == runtime.targetId && session.Players.Contains(owner) &&
                session.Players.Contains(runtime.target) && !RaceTurnRules.IsTerminal(runtime.target);
            if (valid) runtime.LastUsedLap = lap;
            runtime.CloseInput();
            return valid;
        }
    }
}
