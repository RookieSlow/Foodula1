using System;

/// <summary>
/// US Stewart passive state.  The overtake receipt arms exactly one following
/// owner input; the input is consumed even when its route is not a safe
/// straight, so the passive never defers a missed window.
/// </summary>
[Serializable]
public sealed class TrackFuryRuntime
{
    private bool pendingInput;
    private bool recoveryReadyThisInput;
    private int lastArmedLap = -1;
    private int pendingLap = -1;

    public int MovementBonusThisInput { get; private set; }
    public bool RecoveryReadyThisInput => recoveryReadyThisInput;
    public bool HasPendingInput => pendingInput;
    public int LastArmedLap => lastArmedLap;

    public void BeginInput(int tier)
    {
        MovementBonusThisInput = 0;
        recoveryReadyThisInput = false;
        if (!pendingInput) return;

        pendingInput = false;
        MovementBonusThisInput = tier >= 2 ? 2 : 1;
        recoveryReadyThisInput = tier >= 3;
    }

    /// <summary>Called only after the coordinator measured an actual overtake.</summary>
    public bool ArmAfterOvertake(int lap)
    {
        if (lap < 0 || lastArmedLap == lap) return false;
        lastArmedLap = lap;
        pendingLap = lap;
        pendingInput = true;
        return true;
    }

    /// <summary>Consumes the L3 recovery opportunity on this concrete input.</summary>
    public bool TryConsumeRecovery(bool pureStraight)
    {
        if (!recoveryReadyThisInput) return false;
        recoveryReadyThisInput = false;
        return pureStraight;
    }

    /// <summary>Invalid/terminal input closes the current window without refunding an overtake.</summary>
    public void InterruptInput()
    {
        MovementBonusThisInput = 0;
        recoveryReadyThisInput = false;
        pendingInput = false;
        pendingLap = -1;
    }

    public void Reset()
    {
        pendingInput = false;
        recoveryReadyThisInput = false;
        MovementBonusThisInput = 0;
        lastArmedLap = -1;
        pendingLap = -1;
    }

    public TrackFuryRuntime CreatePresentationCopy() => (TrackFuryRuntime)MemberwiseClone();
}
