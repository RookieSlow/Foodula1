using System;

/// <summary>Mutable per-race state for one driver's active skill.</summary>
[Serializable]
public sealed class DriverSkillRuntimeState
{
    public DriverActiveSkillId Skill { get; private set; }
    public DriverPassiveSkillId PassiveSkill { get; private set; }
    public int Tier { get; private set; }
    public int PassiveTier { get; private set; }
    public int UsesRemaining { get; private set; }
    public int ActiveTurnsRemaining { get; private set; }
    public bool Enabled { get; private set; }
    public bool ActivatedThisTurn { get; private set; }
    public int IgnoredCornersThisTurn { get; private set; }
    public int PassiveMovementBonusThisTurn { get; private set; }
    public int PassiveCoolingBonusThisTurn { get; private set; }
    public int DrivingInputsStarted { get; private set; }
    public int InitializationVersion { get; private set; }
    public bool IsDrivingInputOpen { get; private set; }
    public HunterSlipstreamRuntime Hunting { get; private set; } = new HunterSlipstreamRuntime();
    public MansellChargeRuntime Charge { get; private set; } = new MansellChargeRuntime();
    public MansellPursuitRuntime Pursuit { get; private set; } = new MansellPursuitRuntime();
    public SchumacherTuningRuntime Tuning { get; private set; } = new SchumacherTuningRuntime();

    /// <summary>D03 passive clock is independent of its not-yet-migrated active skill.</summary>
    public bool UsesPassiveDrivingInputClock => PassiveSkill == DriverPassiveSkillId.EngineersTune;

    /// <summary>Migration is explicit so other drivers retain their existing clock.</summary>
    public bool UsesDrivingInputClock => DriverSkillRules.UsesDrivingInputClock(Skill);

    /// <summary>
    /// Read-only presentation hooks. They let the corner-limit explanation
    /// preview a ready passive without consuming it.
    /// </summary>
    public bool IsPassiveWeatherProtectionReady => passiveWeatherProtectionReady;
    public bool IsPassiveCornerLimitBonusReady => passiveCornerLimitBonusReady;

    private bool passiveHeatDiscountReady;
    private bool passiveWeatherProtectionReady;
    private bool passiveCornerLimitBonusReady;
    private bool passiveCornerHeatReductionReady;
    private bool passiveDeckLookaheadReady;
    private int passiveTurnNumber;
    private bool drivingInputStartedThisTurn;

    /// <summary>Copies scalar runtime state, including private one-shot readiness flags.</summary>
    public DriverSkillRuntimeState CreatePresentationCopy()
    {
        var copy = (DriverSkillRuntimeState)MemberwiseClone();
        copy.Hunting = Hunting.CreatePresentationCopy();
        copy.Charge = Charge.CreatePresentationCopy();
        copy.Pursuit = Pursuit.CreatePresentationCopy();
        copy.Tuning = Tuning.CreatePresentationCopy();
        return copy;
    }

    public bool IsActive => Enabled && ActiveTurnsRemaining > 0;

    public void Initialize(DriverProfile profile, int level, bool enabled)
    {
        InitializationVersion++;
        Hunting = new HunterSlipstreamRuntime();
        Charge = new MansellChargeRuntime();
        Pursuit = new MansellPursuitRuntime();
        Tuning = new SchumacherTuningRuntime();
        Skill = profile != null ? DriverSkillRules.GetSkill(profile.Id) : DriverActiveSkillId.None;
        PassiveSkill = profile != null ? DriverSkillRules.GetPassiveSkill(profile.Id) : DriverPassiveSkillId.None;
        Tier = DriverProgression.GetActiveTier(level);
        PassiveTier = DriverProgression.GetPassiveTier(level);
        UsesRemaining = profile != null
            ? DriverProgression.GetActiveUsesPerRace(level, profile.Team)
            : 0;
        ActiveTurnsRemaining = 0;
        Enabled = enabled;
        ActivatedThisTurn = false;
        IgnoredCornersThisTurn = 0;
        PassiveMovementBonusThisTurn = 0;
        PassiveCoolingBonusThisTurn = 0;
        passiveHeatDiscountReady = false;
        passiveWeatherProtectionReady = false;
        passiveCornerLimitBonusReady = false;
        passiveCornerHeatReductionReady = false;
        passiveDeckLookaheadReady = false;
        passiveTurnNumber = 0;
        drivingInputStartedThisTurn = false;
        DrivingInputsStarted = 0;
        IsDrivingInputOpen = false;
    }

    public bool TryActivate(DriverProfile profile, DriverSkillActivationContext context, out string reason)
    {
        // Never interleave the unmigrated UI path with a paid sequence.
        if (Skill == DriverActiveSkillId.AngryCharge && Charge.HasSequence)
        { reason = "雄狮追击需要当前费用计划"; return false; }
        if (!DriverSkillRules.CanActivate(profile, this, context, out reason))
            return false;

        CommitValidatedActivation();
        return true;
    }

    /// <summary>Only a validated activation or fixed-cost commit may enter this mutation.</summary>
    internal void CommitValidatedActivation()
    {
        UsesRemaining--;
        ActiveTurnsRemaining = DriverSkillRules.GetDurationTurns(Skill, Tier);
        ActivatedThisTurn = true;
        IgnoredCornersThisTurn = 0;
    }

    /// <summary>Continuation spends fixed heat in its plan, never another race use.</summary>
    internal void CommitValidatedContinuation()
    {
        ActiveTurnsRemaining = 1;
        ActivatedThisTurn = true;
        IgnoredCornersThisTurn = 0;
    }

    /// <summary>Clears a global round without spending a migrated driver's input window.</summary>
    public void BeginGlobalTurn()
    {
        Hunting.CloseInput();
        Charge.CloseInput();
        Pursuit.CloseInput();
        Tuning.BeginGlobalTurn();
        drivingInputStartedThisTurn = false;
        IsDrivingInputOpen = false;
        if (!UsesDrivingInputClock)
        {
            BeginTurn();
            return;
        }

        ResetTurnEffects();
    }

    /// <summary>Advances a migrated skill once, before this participant's actual gear input.</summary>
    public bool BeginDrivingInput()
    {
        if (!Enabled || !UsesDrivingInputClock || drivingInputStartedThisTurn)
            return false;

        drivingInputStartedThisTurn = true;
        IsDrivingInputOpen = true;
        DrivingInputsStarted++;
        BeginTurn();
        return true;
    }

    /// <summary>Clears an interrupted UK window without restoring its committed race use.</summary>
    public void InterruptDrivingInput()
    {
        Hunting.CloseInput();
        Charge.Interrupt();
        Pursuit.Interrupt();
        Tuning.Interrupt();
        if (!UsesDrivingInputClock) return;
        IsDrivingInputOpen = false;
        drivingInputStartedThisTurn = true;
        ActiveTurnsRemaining = 0;
        ResetTurnEffects();
    }

    /// <summary>Close paid Mansell state on coordinator exit without turning it back into a legacy activation.</summary>
    public void InterruptMansellChargeWindow()
    {
        if (Skill == DriverActiveSkillId.AngryCharge) InterruptDrivingInput();
        else Charge.Interrupt();
    }

    private void ResetTurnEffects()
    {
        ActivatedThisTurn = false;
        IgnoredCornersThisTurn = 0;
        PassiveMovementBonusThisTurn = 0;
        PassiveCoolingBonusThisTurn = 0;
    }

    /// <summary>Advances one skill turn; UK production callers use BeginDrivingInput instead.</summary>
    public void BeginTurn()
    {
        if (IsActive && ActiveTurnsRemaining != int.MaxValue)
            ActiveTurnsRemaining = Math.Max(0, ActiveTurnsRemaining - 1);
        ResetTurnEffects();
        passiveTurnNumber++;

        if (!Enabled || PassiveTier <= 0)
            return;

        passiveHeatDiscountReady = false;
        passiveDeckLookaheadReady = false;
        passiveWeatherProtectionReady = false;
        passiveCornerLimitBonusReady = false;
        passiveCornerHeatReductionReady = false;

        int interval = DriverSkillRules.GetPassiveTriggerInterval(PassiveSkill, PassiveTier);
        if (interval > 0 && passiveTurnNumber % interval == 0)
        {
            passiveHeatDiscountReady = DriverSkillRules.GetPassiveHeatDiscount(PassiveSkill, PassiveTier) > 0;
            passiveDeckLookaheadReady = DriverSkillRules.GetPassiveDeckLookahead(PassiveSkill, PassiveTier) > 0;
        }

        if (PassiveSkill == DriverPassiveSkillId.SteadyHeart)
            passiveWeatherProtectionReady = DriverSkillRules.GetPassiveWeatherPenaltyReduction(PassiveSkill, PassiveTier) > 0;
        if (PassiveSkill == DriverPassiveSkillId.AllRounder)
        {
            passiveCornerLimitBonusReady = DriverSkillRules.GetPassiveCornerLimitBonus(PassiveSkill, PassiveTier) > 0;
            passiveCornerHeatReductionReady = DriverSkillRules.GetPassiveCornerHeatReduction(PassiveSkill, PassiveTier) > 0;
        }
    }

    public int ConsumePassiveHeatDiscount()
    {
        if (!passiveHeatDiscountReady) return 0;
        passiveHeatDiscountReady = false;
        return DriverSkillRules.GetPassiveHeatDiscount(PassiveSkill, PassiveTier);
    }

    public bool TryConsumePassiveWeatherProtection()
    {
        if (!passiveWeatherProtectionReady) return false;
        passiveWeatherProtectionReady = false;
        return true;
    }

    public int ConsumePassiveCornerLimitBonus()
    {
        if (!passiveCornerLimitBonusReady) return 0;
        passiveCornerLimitBonusReady = false;
        return DriverSkillRules.GetPassiveCornerLimitBonus(PassiveSkill, PassiveTier);
    }

    public int ConsumePassiveCornerHeatReduction()
    {
        if (!passiveCornerHeatReductionReady) return 0;
        passiveCornerHeatReductionReady = false;
        return DriverSkillRules.GetPassiveCornerHeatReduction(PassiveSkill, PassiveTier);
    }

    public bool TryConsumePassiveDeckLookahead(out int lookahead)
    {
        lookahead = 0;
        if (!passiveDeckLookaheadReady) return false;
        passiveDeckLookaheadReady = false;
        lookahead = DriverSkillRules.GetPassiveDeckLookahead(PassiveSkill, PassiveTier);
        return lookahead > 0;
    }

    /// <summary>Legacy overtake hook retained for callers; D02 pursuit now observes actual owner inputs.</summary>
    public void ResolvePassiveTurnEnd(int overtakes)
    {
    }

    public bool TryConsumeCornerIgnore()
    {
        int allowed = DriverSkillRules.GetIgnoredCornerCount(this);
        if (allowed <= IgnoredCornersThisTurn)
            return false;
        IgnoredCornersThisTurn++;
        return true;
    }
}
