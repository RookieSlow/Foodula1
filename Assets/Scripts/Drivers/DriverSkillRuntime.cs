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

    private bool passiveHeatDiscountReady;
    private bool passiveWeatherProtectionReady;
    private bool passiveCornerLimitBonusReady;
    private bool passiveCornerHeatReductionReady;
    private bool passiveDeckLookaheadReady;
    private int passiveTurnNumber;
    private bool pendingOvertakeReward;

    public bool IsActive => Enabled && ActiveTurnsRemaining > 0;

    public void Initialize(DriverProfile profile, int level, bool enabled)
    {
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
        pendingOvertakeReward = false;
    }

    public bool TryActivate(DriverProfile profile, DriverSkillActivationContext context, out string reason)
    {
        if (!DriverSkillRules.CanActivate(profile, this, context, out reason))
            return false;

        UsesRemaining--;
        ActiveTurnsRemaining = DriverSkillRules.GetDurationTurns(Skill, Tier);
        ActivatedThisTurn = true;
        IgnoredCornersThisTurn = 0;
        return true;
    }

    /// <summary>Advances durations before the new turn can activate another skill.</summary>
    public void BeginTurn()
    {
        if (IsActive && ActiveTurnsRemaining != int.MaxValue)
            ActiveTurnsRemaining = Math.Max(0, ActiveTurnsRemaining - 1);
        ActivatedThisTurn = false;
        IgnoredCornersThisTurn = 0;
        PassiveMovementBonusThisTurn = 0;
        PassiveCoolingBonusThisTurn = 0;
        passiveTurnNumber++;

        if (!Enabled || PassiveTier <= 0)
            return;

        passiveHeatDiscountReady = false;
        passiveDeckLookaheadReady = false;
        passiveWeatherProtectionReady = false;
        passiveCornerLimitBonusReady = false;
        passiveCornerHeatReductionReady = false;

        if (pendingOvertakeReward)
        {
            PassiveMovementBonusThisTurn = DriverSkillRules.GetPassiveOvertakeMovementBonus(PassiveSkill, PassiveTier);
            PassiveCoolingBonusThisTurn = DriverSkillRules.GetPassiveOvertakeCoolingBonus(PassiveSkill, PassiveTier);
            pendingOvertakeReward = false;
        }

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

    /// <summary>Arms the next-turn Lion's Heart reward after a real overtake.</summary>
    public void ResolvePassiveTurnEnd(int overtakes)
    {
        if (Enabled && PassiveSkill == DriverPassiveSkillId.LionsHeart &&
            PassiveTier > 0 && overtakes > 0)
            pendingOvertakeReward = true;
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
