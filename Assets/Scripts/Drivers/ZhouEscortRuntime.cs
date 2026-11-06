/// <summary>
/// Zhou active escort window.  The committed input and its future owner-input
/// allowance are isolated per driver; a blocker is only overridden for one
/// positive slipstream cell.
/// </summary>
public sealed class ZhouEscortRuntime
{
    public bool CurrentInputEligible { get; private set; }
    public int RemainingFutureInputs { get; private set; }
    public int Version { get; private set; }

    internal void ArmForCurrentAndFuture(int tier)
    {
        CurrentInputEligible = true;
        RemainingFutureInputs = tier >= 2 ? 2 : 1;
        Version++;
    }

    internal void BeginOwnerInput()
    {
        if (CurrentInputEligible) return;
        if (RemainingFutureInputs <= 0) return;
        CurrentInputEligible = true;
        RemainingFutureInputs--;
        Version++;
    }

    public bool CanOverrideLeader(PlayerState leader)
    {
        if (!CurrentInputEligible || leader == null) return false;
        return TrickCardRules.IsIceJellyActive(leader.trickState) ||
            DriverSkillRules.BlocksTrailingSlipstream(leader.driverSkill);
    }

    public void CommitResolvedEscort()
    {
        if (!CurrentInputEligible) return;
        CurrentInputEligible = false;
        Version++;
    }

    internal void CloseCurrentInput() => CurrentInputEligible = false;

    public void Interrupt()
    {
        CurrentInputEligible = false;
        RemainingFutureInputs = 0;
        Version++;
    }

    public ZhouEscortRuntime CreatePresentationCopy()
    {
        return (ZhouEscortRuntime)MemberwiseClone();
    }
}
