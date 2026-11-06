using System;

/// <summary>
/// Kyle Busch's one-target pressure ledger.  The marker belongs to the
/// pressured follower so refusal cannot redirect the automatic slipstream to
/// a farther car.  It is race-only state and never enables AI driver skills.
/// </summary>
[Serializable]
public sealed class BuschPressureRuntime
{
    private PlayerState lockedFrontTarget;
    private int lockedOwnerLap = -1;
    private int lockedOwnerInput;
    private int outgoingUsedLap = -1;

    private PlayerState incomingLeader;
    private string incomingLeaderId;
    private int incomingLap = -1;
    private bool incomingMarker;
    private bool incomingDecisionPending;
    private bool incomingDecisionResolved;
    private bool incomingAccepted;

    public PlayerState LockedFrontTarget => lockedFrontTarget;
    public PlayerState IncomingLeader => incomingLeader;
    public bool HasIncomingMarker => incomingMarker;
    public bool HasIncomingDecision => incomingDecisionPending;
    public bool IncomingDecisionResolved => incomingDecisionResolved;
    public bool IncomingAccepted => incomingAccepted;
    public int OutgoingUsedLap => outgoingUsedLap;

    public void BeginOwnerInput(RaceSession race, PlayerState owner, int totalNodes)
    {
        lockedFrontTarget = null;
        lockedOwnerLap = -1;
        lockedOwnerInput = 0;
        if (race?.Players == null || owner?.driverSkill == null || totalNodes <= 0 ||
            !owner.driverSkill.Enabled || owner.driverSkill.PassiveSkill != DriverPassiveSkillId.VillainAura ||
            !race.Players.Contains(owner) || RaceTurnRules.GetStartAction(owner) != RaceTurnStartAction.SelectGear)
            return;

        lockedFrontTarget = FindNearestAhead(race, owner, totalNodes,
            DriverSkillRules.GetVillainAuraRange(owner.driverSkill.PassiveTier));
        lockedOwnerLap = owner.lap;
        lockedOwnerInput = owner.driverSkill.DrivingInputsStarted;
    }

    public bool CanArmOutgoing(int lap) => outgoingUsedLap != lap;

    /// <summary>Active Taunting Bow arms a stable nearest follower after payment.</summary>
    public bool CommitActivePressure(PlayerState owner, PlayerState target, int lap)
    {
        if (owner == null || target == null || !CanArmOutgoing(lap)) return false;
        outgoingUsedLap = lap;
        target.driverSkill?.Pressure.ReceiveIncoming(owner, lap);
        return true;
    }

    /// <summary>Passive Villain Aura arms only when its locked front car was really overtaken.</summary>
    public bool ResolvePassiveActualOvertake(PlayerState owner, int lap, bool actualOvertake)
    {
        if (!actualOvertake || owner == null || lockedFrontTarget == null ||
            lockedOwnerLap != lap || !CanArmOutgoing(lap))
        {
            lockedFrontTarget = null;
            return false;
        }

        PlayerState target = lockedFrontTarget;
        lockedFrontTarget = null;
        outgoingUsedLap = lap;
        target.driverSkill?.Pressure.ReceiveIncoming(owner, lap);
        return true;
    }

    /// <summary>Finds the nearest legal car behind the owner for active Taunting Bow.</summary>
    public static PlayerState FindNearestBehind(RaceSession race, PlayerState owner, int totalNodes, int range,
        out int distance)
    {
        distance = int.MaxValue;
        if (race?.Players == null || owner == null || totalNodes <= 0) return null;
        PlayerState nearest = null;
        foreach (PlayerState candidate in race.Players)
        {
            if (candidate == null || ReferenceEquals(candidate, owner) || RaceTurnRules.IsTerminal(candidate) ||
                candidate.lap != owner.lap || candidate.pitStopScheduled) continue;
            int gap = RaceSession.ForwardDistance(candidate.position, owner.position, totalNodes);
            if (gap <= 0 || gap > range || gap >= distance) continue;
            distance = gap;
            nearest = candidate;
        }
        return nearest;
    }

    public bool BeginTargetInput(RaceSession race, PlayerState target)
    {
        if (!incomingMarker) return false;
        if (race?.Players == null || target == null || !race.Players.Contains(target) ||
            incomingLeader == null || !race.Players.Contains(incomingLeader) ||
            RaceTurnRules.IsTerminal(incomingLeader) || RaceTurnRules.IsTerminal(target) ||
            incomingLeader.lap != incomingLap || target.lap != incomingLap)
        {
            ClearIncoming();
            return false;
        }

        incomingDecisionPending = true;
        if (target.isAI)
            CommitIncomingDecision(false);
        return true;
    }

    public bool CommitIncomingDecision(bool accept)
    {
        if (!incomingMarker || !incomingDecisionPending) return false;
        incomingDecisionPending = false;
        incomingDecisionResolved = true;
        incomingAccepted = accept;
        return true;
    }

    public bool AllowsFollow(PlayerState leader)
    {
        return incomingMarker && incomingDecisionResolved && incomingAccepted &&
            ReferenceEquals(incomingLeader, leader) && incomingLeaderId == leader?.driverId;
    }

    /// <summary>Pressure is one target-input decision, never a persistent blocker.</summary>
    public void ExpireAfterSlipstreamResolution() => ClearIncoming();

    public void InterruptInput() => ClearIncoming();

    public BuschPressureRuntime CreatePresentationCopy() => (BuschPressureRuntime)MemberwiseClone();

    private void ReceiveIncoming(PlayerState leader, int lap)
    {
        if (leader == null || leader.driverSkill == null || RaceTurnRules.IsTerminal(leader)) return;
        incomingLeader = leader;
        incomingLeaderId = leader.driverId;
        incomingLap = lap;
        incomingMarker = true;
        incomingDecisionPending = false;
        incomingDecisionResolved = false;
        incomingAccepted = false;
    }

    private void ClearIncoming()
    {
        incomingLeader = null;
        incomingLeaderId = null;
        incomingLap = -1;
        incomingMarker = false;
        incomingDecisionPending = false;
        incomingDecisionResolved = false;
        incomingAccepted = false;
    }

    private static PlayerState FindNearestAhead(RaceSession race, PlayerState owner, int totalNodes, int range)
    {
        PlayerState nearest = null;
        int best = int.MaxValue;
        foreach (PlayerState candidate in race.Players)
        {
            if (candidate == null || ReferenceEquals(candidate, owner) || RaceTurnRules.IsTerminal(candidate) ||
                candidate.lap != owner.lap || candidate.pitStopScheduled) continue;
            int gap = RaceSession.ForwardDistance(owner.position, candidate.position, totalNodes);
            if (gap <= 0 || gap > range || gap >= best) continue;
            best = gap;
            nearest = candidate;
        }
        return nearest;
    }
}
