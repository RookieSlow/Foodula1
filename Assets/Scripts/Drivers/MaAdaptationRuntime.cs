using System;

/// <summary>
/// 马青骅（全能者）的独立输入运行时。
///
/// The runtime deliberately keeps the old card instance in hand while the
/// native Recover cooling traversal skips it. No temporary heat is fabricated,
/// and the next-Go reward is armed only after a real Recover has settled.
/// </summary>
[Serializable]
public sealed class MaAdaptationRuntime
{
    public bool InputOpen { get; private set; }
    public bool PreparationDeclared { get; private set; }
    public bool ReadyForNextGo { get; private set; }
    public int MovementBonusThisInput { get; private set; }
    public bool PassiveApexReady { get; private set; }
    public bool PassiveHeatReductionReady { get; private set; }
    public bool LastCompletedInputRecover { get; private set; }
    public int PassiveConversionsThisLap { get; private set; }
    public CardData ReservedHandHeat { get; private set; }

    public void BeginGlobalTurn()
    {
        InputOpen = false;
        PreparationDeclared = false;
        MovementBonusThisInput = 0;
        PassiveApexReady = false;
        PassiveHeatReductionReady = false;
    }

    public void BeginInput()
    {
        InputOpen = true;
        MovementBonusThisInput = 0;
        PassiveApexReady = false;
        PassiveHeatReductionReady = false;
    }

    public bool DeclarePreparation()
    {
        if (!InputOpen || ReadyForNextGo) return false;
        PreparationDeclared = true;
        return true;
    }

    public void CancelPreparationDeclaration() => PreparationDeclared = false;

    public bool CommitRecover(CardData reservedHeat)
    {
        if (!PreparationDeclared || reservedHeat == null || ReadyForNextGo)
            return false;
        PreparationDeclared = false;
        ReservedHandHeat = reservedHeat;
        ReadyForNextGo = true;
        return true;
    }

    /// <summary>
    /// Settles the next real owner input. Invalid/terminal inputs expire the
    /// armed reward with no refund. A valid Go can also arm the All-Rounder
    /// first-apex conversion after the previous valid Recover.
    /// </summary>
    public bool ResolveSubmittedInput(int tier, bool isGo, bool completed)
    {
        MovementBonusThisInput = 0;
        PassiveApexReady = false;
        PassiveHeatReductionReady = false;

        if (ReadyForNextGo)
        {
            if (completed && isGo)
                MovementBonusThisInput = DriverSkillRules.GetMaNextGoMovementBonus(tier);
            ReadyForNextGo = false;
            ReservedHandHeat = null;
        }

        if (!completed)
        {
            LastCompletedInputRecover = false;
            return false;
        }

        int quota = DriverSkillRules.GetMaPassiveConversionQuota(tier);
        if (isGo && LastCompletedInputRecover && PassiveConversionsThisLap < quota)
        {
            PassiveApexReady = true;
            PassiveHeatReductionReady = tier >= 2;
            PassiveConversionsThisLap++;
        }

        LastCompletedInputRecover = !isGo;
        return true;
    }

    public int ConsumePassiveCornerLimitBonus(int tier)
    {
        if (!PassiveApexReady) return 0;
        PassiveApexReady = false;
        return DriverSkillRules.GetMaPassiveCornerLimitBonus(tier);
    }

    public int ConsumePassiveCornerHeatReduction(int tier)
    {
        if (!PassiveHeatReductionReady) return 0;
        PassiveHeatReductionReady = false;
        return DriverSkillRules.GetMaPassiveCornerHeatReduction(tier);
    }

    public void OnNewLap()
    {
        PassiveConversionsThisLap = 0;
    }

    /// <summary>Lifecycle exit clears unfinished declarations and rewards.</summary>
    public void Interrupt()
    {
        InputOpen = false;
        PreparationDeclared = false;
        ReadyForNextGo = false;
        ReservedHandHeat = null;
        MovementBonusThisInput = 0;
        PassiveApexReady = false;
        PassiveHeatReductionReady = false;
        LastCompletedInputRecover = false;
    }

    public MaAdaptationRuntime CreatePresentationCopy()
    {
        return new MaAdaptationRuntime
        {
            InputOpen = InputOpen,
            PreparationDeclared = PreparationDeclared,
            ReadyForNextGo = ReadyForNextGo,
            MovementBonusThisInput = MovementBonusThisInput,
            PassiveApexReady = PassiveApexReady,
            PassiveHeatReductionReady = PassiveHeatReductionReady,
            LastCompletedInputRecover = LastCompletedInputRecover,
            PassiveConversionsThisLap = PassiveConversionsThisLap,
            ReservedHandHeat = ReservedHandHeat
        };
    }
}
