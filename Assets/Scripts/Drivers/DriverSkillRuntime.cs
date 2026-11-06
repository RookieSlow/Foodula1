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
    public SchumacherStrategyRuntime Strategy { get; private set; } = new SchumacherStrategyRuntime();
    public VettelFeedbackRuntime Feedback { get; private set; } = new VettelFeedbackRuntime();
    public VettelRhythmRuntime Rhythm { get; private set; } = new VettelRhythmRuntime();
    public AscariPrecisionRuntime Precision { get; private set; } = new AscariPrecisionRuntime();
    public NuvolariRuntime Nuvolari { get; private set; } = new NuvolariRuntime();
    public TrackFuryRuntime TrackFury { get; private set; } = new TrackFuryRuntime();
    public BuschPressureRuntime Pressure { get; private set; } = new BuschPressureRuntime();
    public ZhouEscortRuntime ZhouEscort { get; private set; } = new ZhouEscortRuntime();
    public ZhouSteadyHeartRuntime ZhouSteadyHeart { get; private set; } = new ZhouSteadyHeartRuntime();
    public MaAdaptationRuntime Ma { get; private set; } = new MaAdaptationRuntime();

    /// <summary>D03/D04 event passives use independent owner-input clocks.</summary>
    public bool UsesPassiveDrivingInputClock => PassiveSkill == DriverPassiveSkillId.EngineersTune || PassiveSkill == DriverPassiveSkillId.InformationSponge ||
        PassiveSkill == DriverPassiveSkillId.UnyieldingLegend || PassiveSkill == DriverPassiveSkillId.TrackFury ||
        PassiveSkill == DriverPassiveSkillId.VillainAura || PassiveSkill == DriverPassiveSkillId.SteadyHeart;

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
    private int blueLuckNoHeatTurns;
    private bool blueLuckHasTurnSample;
    private int heatPaidThisTurn;
    private bool drivingInputStartedThisTurn;
    private bool trueSelfHeatPaidThisInput;
    private bool trueSelfInputPaymentFailed;

    /// <summary>D06 current consecutive owner-input turns with no paid heat.</summary>
    public int BlueLuckNoHeatTurns => blueLuckNoHeatTurns;
    public bool TrueSelfInputPaymentResolved => trueSelfHeatPaidThisInput || trueSelfInputPaymentFailed;
    public bool TrueSelfInputPaymentFailed => trueSelfInputPaymentFailed;

    /// <summary>Copies scalar runtime state, including private one-shot readiness flags.</summary>
    public DriverSkillRuntimeState CreatePresentationCopy()
    {
        var copy = (DriverSkillRuntimeState)MemberwiseClone();
        copy.Hunting = Hunting.CreatePresentationCopy();
        copy.Charge = Charge.CreatePresentationCopy();
        copy.Pursuit = Pursuit.CreatePresentationCopy();
        copy.Tuning = Tuning.CreatePresentationCopy();
        copy.Strategy = Strategy.CreatePresentationCopy();
        copy.Feedback = Feedback.CreatePresentationCopy();
        copy.Rhythm = Rhythm.CreatePresentationCopy();
        copy.Precision = Precision.CreatePresentationCopy();
        copy.Nuvolari = Nuvolari.CreatePresentationCopy();
        copy.TrackFury = TrackFury.CreatePresentationCopy();
        copy.Pressure = Pressure.CreatePresentationCopy();
        copy.ZhouEscort = ZhouEscort.CreatePresentationCopy();
        copy.ZhouSteadyHeart = ZhouSteadyHeart.CreatePresentationCopy();
        copy.Ma = Ma.CreatePresentationCopy();
        return copy;
    }

    public bool IsActive => Enabled && (Skill == DriverActiveSkillId.PerfectRhythm && Rhythm.ManualPath
        ? Rhythm.HasWindow : ActiveTurnsRemaining > 0);

    public void Initialize(DriverProfile profile, int level, bool enabled)
    {
        InitializationVersion++;
        Hunting = new HunterSlipstreamRuntime();
        Charge = new MansellChargeRuntime();
        Pursuit = new MansellPursuitRuntime();
        Tuning = new SchumacherTuningRuntime();
        Strategy = new SchumacherStrategyRuntime();
        Feedback = new VettelFeedbackRuntime();
        Rhythm = new VettelRhythmRuntime();
        Precision = new AscariPrecisionRuntime();
        Nuvolari = new NuvolariRuntime();
        TrackFury = new TrackFuryRuntime();
        Pressure = new BuschPressureRuntime();
        ZhouEscort = new ZhouEscortRuntime();
        ZhouSteadyHeart = new ZhouSteadyHeartRuntime();
        Ma = new MaAdaptationRuntime();
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
        blueLuckNoHeatTurns = 0;
        blueLuckHasTurnSample = false;
        heatPaidThisTurn = 0;
        drivingInputStartedThisTurn = false;
        trueSelfHeatPaidThisInput = false;
        trueSelfInputPaymentFailed = false;
        DrivingInputsStarted = 0;
        IsDrivingInputOpen = false;
    }

    public bool TryActivate(DriverProfile profile, DriverSkillActivationContext context, out string reason)
    {
        if (Skill == DriverActiveSkillId.ExtremeSwitch)
        { reason = "极限切换必须在真实 Recover 冷却中声明"; return false; }
        if (Skill == DriverActiveSkillId.PrecisionCornering)
        { reason = "精密过弯需要已选普通速度牌目标与最终出牌计划"; return false; }
        if (Skill == DriverActiveSkillId.PerfectRhythm)
        { reason = "人车合拍需要当前固定来源费用计划"; return false; }
        if (Skill == DriverActiveSkillId.PerfectLap && Strategy.ManualPath)
        { reason = "三段战术需要当前费用计划"; return false; }
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
        if (Skill == DriverActiveSkillId.FinalSprint)
            Nuvolari.CommitPrepaidSpin();
    }

    /// <summary>Commits Zhou's fixed preparation path without legacy weather immunity or duration.</summary>
    internal void CommitZhouEscortActivation()
    {
        UsesRemaining = Math.Max(0, UsesRemaining - 1);
        ActivatedThisTurn = true;
        ActiveTurnsRemaining = 0;
        ZhouEscort.ArmForCurrentAndFuture(Tier);
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
        Strategy.BeginGlobalTurn();
        Feedback.BeginGlobalTurn();
        Rhythm.BeginGlobalTurn();
        Precision.CloseInput();
        Nuvolari.BeginGlobalTurn();
        ZhouEscort.CloseCurrentInput();
        ZhouSteadyHeart.CloseInput();
        Ma.BeginGlobalTurn();
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
        if (PassiveSkill == DriverPassiveSkillId.TrackFury)
        {
            TrackFury.BeginInput(PassiveTier);
            PassiveMovementBonusThisTurn = TrackFury.MovementBonusThisInput;
        }
        if (Skill == DriverActiveSkillId.ExtremeSwitch)
            Ma.BeginInput();
        return true;
    }

    /// <summary>Clears an interrupted UK window without restoring its committed race use.</summary>
    public void InterruptDrivingInput()
    {
        Hunting.CloseInput();
        Charge.Interrupt();
        Pursuit.Interrupt();
        Tuning.Interrupt();
        Strategy.Interrupt();
        Feedback.Interrupt();
        Rhythm.Interrupt();
        Precision.Interrupt();
        Nuvolari.Interrupt();
        TrackFury.InterruptInput();
        Pressure.InterruptInput();
        ZhouEscort.Interrupt();
        ZhouSteadyHeart.CloseInput();
        Ma.Interrupt();
        ResetBlueLuckCadence();
        if (!UsesDrivingInputClock) return;
        IsDrivingInputOpen = false;
        drivingInputStartedThisTurn = true;
        ActiveTurnsRemaining = 0;
        ResetTurnEffects();
    }

    /// <summary>
    /// Discards an incomplete Ascari owner-input sample when a pit, skip,
    /// terminal state, or race exit interrupts the driving window.
    /// </summary>
    private void ResetBlueLuckCadence()
    {
        if (PassiveSkill != DriverPassiveSkillId.BlueLuck) return;
        blueLuckNoHeatTurns = 0;
        blueLuckHasTurnSample = false;
        heatPaidThisTurn = 0;
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
        trueSelfHeatPaidThisInput = false;
        trueSelfInputPaymentFailed = false;
    }

    /// <summary>Feeds actual heat payment from the race coordinator into D06's owner-input sample.</summary>
    public void RecordHeatPaid(int amount)
    {
        if (amount > 0) heatPaidThisTurn += amount;
    }

    public void RecordHeatPaid(int amount, bool isGoMode, bool ordinaryCorner)
    {
        RecordHeatPaid(amount);
        if (amount > 0 && PassiveSkill == DriverPassiveSkillId.SteadyHeart)
            ZhouSteadyHeart.RecordPaidHeat(amount, isGoMode, ordinaryCorner, PassiveTier);
    }

    public void RecordOrdinaryCornerHeatPaid(int amount, bool isGoMode)
    {
        if (amount > 0 && PassiveSkill == DriverPassiveSkillId.SteadyHeart)
            ZhouSteadyHeart.RecordPaidHeat(amount, isGoMode, true, PassiveTier);
    }

    /// <summary>Records True Self's fixed per-input fee after card selection.</summary>
    public void ResolveTrueSelfInputPayment(bool paid)
    {
        if (Skill != DriverActiveSkillId.TrueSelf || TrueSelfInputPaymentResolved)
            return;
        if (paid)
            trueSelfHeatPaidThisInput = true;
        else
        {
            trueSelfInputPaymentFailed = true;
            ActiveTurnsRemaining = 0;
        }
    }

    /// <summary>Advances one skill turn; UK production callers use BeginDrivingInput instead.</summary>
    public void BeginTurn()
    {
        if (IsActive && ActiveTurnsRemaining != int.MaxValue)
            ActiveTurnsRemaining = Math.Max(0, ActiveTurnsRemaining - 1);
        ResetTurnEffects();
        passiveTurnNumber++;

        bool blueLuck = PassiveSkill == DriverPassiveSkillId.BlueLuck;
        if (blueLuck)
        {
            if (blueLuckHasTurnSample)
                blueLuckNoHeatTurns = heatPaidThisTurn > 0 ? 0 : blueLuckNoHeatTurns + 1;
            else
                blueLuckHasTurnSample = true;
            heatPaidThisTurn = 0;
        }

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
        if (blueLuck && blueLuckNoHeatTurns >= DriverSkillRules.GetBlueLuckNoHeatTurnThreshold(PassiveTier))
        {
            passiveCornerLimitBonusReady = true;
            PassiveCoolingBonusThisTurn = DriverSkillRules.GetPassiveCoolingBonus(PassiveSkill, PassiveTier);
            blueLuckNoHeatTurns = 0;
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

    public bool IsMaPassiveApexReady => Ma != null && Ma.PassiveApexReady;

    public int ConsumeMaPassiveCornerLimitBonus()
        => Ma?.ConsumePassiveCornerLimitBonus(PassiveTier) ?? 0;

    public int ConsumeMaPassiveCornerHeatReduction()
        => Ma?.ConsumePassiveCornerHeatReduction(PassiveTier) ?? 0;

    public bool ResolveMaSubmittedInput(bool isGo, bool completed)
        => Ma != null && Ma.ResolveSubmittedInput(PassiveTier, isGo, completed);

    public bool TryDeclareMaPreparation()
        => Skill == DriverActiveSkillId.ExtremeSwitch && Enabled && UsesRemaining > 0 && Ma != null && Ma.DeclarePreparation();

    public void CancelMaPreparationDeclaration() => Ma?.CancelPreparationDeclaration();

    internal bool CommitMaRecover(CardData reservedHeat)
    {
        if (Skill != DriverActiveSkillId.ExtremeSwitch || UsesRemaining <= 0 || Ma == null ||
            !Ma.CommitRecover(reservedHeat)) return false;
        UsesRemaining--;
        return true;
    }

    public void OnNewLap() => Ma?.OnNewLap();

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
        ResolvePassiveTurnEnd(overtakes, -1);
    }

    /// <summary>Track Fury arms only from the coordinator's actual overtake count.</summary>
    public void ResolvePassiveTurnEnd(int overtakes, int lap)
    {
        if (PassiveSkill == DriverPassiveSkillId.TrackFury && overtakes > 0)
            TrackFury.ArmAfterOvertake(lap);
    }

    public bool TryConsumeTrackFuryRecovery(bool pureStraight)
    {
        return PassiveSkill == DriverPassiveSkillId.TrackFury && TrackFury.TryConsumeRecovery(pureStraight);
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
