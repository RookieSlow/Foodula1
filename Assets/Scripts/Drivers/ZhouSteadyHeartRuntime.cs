/// <summary>
/// Zhou's passive old-heat recovery window.  It is armed by an actual Go
/// payment (or, from tier 2, an ordinary corner payment) during this owner's
/// input and is consumed only after ordinary reaction cooling.
/// </summary>
public sealed class ZhouSteadyHeartRuntime
{
    public bool InputOpen { get; private set; }
    public bool IsGoInput { get; private set; }
    public bool RecoveryReady { get; private set; }
    public int RecoveryCap { get; private set; }
    public int RecoveredThisInput { get; private set; }

    internal bool BeginInput(int tier)
    {
        CloseInput();
        InputOpen = tier > 0;
        RecoveryCap = DriverSkillRules.GetZhouRecoveryCap(tier);
        return InputOpen;
    }

    internal void RecordPaidHeat(int amount, bool isGoMode, bool ordinaryCorner, int tier)
    {
        if (!InputOpen || amount <= 0) return;
        IsGoInput |= isGoMode;
        if (isGoMode || (tier >= 2 && ordinaryCorner))
            RecoveryReady = true;
    }

    internal int TryConsumeRecovery(CardDeck deck)
    {
        if (!InputOpen || !RecoveryReady || deck == null ||
            RecoveryCap <= 0 || deck.CountHandHeatRestorableToEngine() <= 0)
            return 0;

        int restored = 0;
        for (int i = 0; i < RecoveryCap; i++)
        {
            if (deck.RemoveOnePermanentHeatFromHand() <= 0) break;
            restored++;
        }
        if (restored > 0)
        {
            RecoveryReady = false;
            RecoveredThisInput = restored;
        }
        return restored;
    }

    public void CloseInput()
    {
        InputOpen = false;
        IsGoInput = false;
        RecoveryReady = false;
        RecoveryCap = 0;
        RecoveredThisInput = 0;
    }

    public ZhouSteadyHeartRuntime CreatePresentationCopy()
    {
        return (ZhouSteadyHeartRuntime)MemberwiseClone();
    }
}
