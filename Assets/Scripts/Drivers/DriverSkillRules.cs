using System;
using System.Collections.Generic;

/// <summary>Stable identifiers for the twelve driver active skills.</summary>
public enum DriverActiveSkillId
{
    None,
    FinalLap,
    AngryCharge,
    PerfectLap,
    PerfectRhythm,
    PrecisionCornering,
    FinalSprint,
    TrueSelf,
    TauntingBow,
    ChinaSpeed,
    ExtremeSwitch,
    SmokeScreen,
    GutterRun
}

/// <summary>Stable identifiers for the twelve driver passive skills.</summary>
public enum DriverPassiveSkillId
{
    None,
    HuntingInstinct,
    LionsHeart,
    EngineersTune,
    InformationSponge,
    BlueLuck,
    UnyieldingLegend,
    TrackFury,
    VillainAura,
    SteadyHeart,
    AllRounder,
    DriftInstinct,
    WaterCupTraining
}

/// <summary>Read-only race facts used to validate a manual activation.</summary>
public readonly struct DriverSkillActivationContext
{
    public DriverSkillActivationContext(bool canAcceptInput, int lap, int totalLaps,
        int engineRemaining, int engineCapacity, int nearbyOpponentsBehind)
    {
        CanAcceptInput = canAcceptInput;
        Lap = lap;
        TotalLaps = totalLaps;
        EngineRemaining = engineRemaining;
        EngineCapacity = engineCapacity;
        NearbyOpponentsBehind = nearbyOpponentsBehind;
    }

    public bool CanAcceptInput { get; }
    public int Lap { get; }
    public int TotalLaps { get; }
    public int EngineRemaining { get; }
    public int EngineCapacity { get; }
    public int NearbyOpponentsBehind { get; }
}

/// <summary>
/// Pure driver-active rules. Runtime orchestration stays in MVPGameManager,
/// while tests can validate every tier without entering Play Mode.
/// </summary>
public static class DriverSkillRules
{
    private static readonly Dictionary<string, DriverActiveSkillId> SkillByDriver =
        new Dictionary<string, DriverActiveSkillId>(StringComparer.Ordinal)
        {
            { "uk_hunter_hart", DriverActiveSkillId.FinalLap },
            { "uk_nigel_mansell", DriverActiveSkillId.AngryCharge },
            { "de_michael_schumacher", DriverActiveSkillId.PerfectLap },
            { "de_sebastian_vettel", DriverActiveSkillId.PerfectRhythm },
            { "it_alberto_ascari", DriverActiveSkillId.PrecisionCornering },
            { "it_tazio_nuvolari", DriverActiveSkillId.FinalSprint },
            { "us_tony_stewart", DriverActiveSkillId.TrueSelf },
            { "us_kyle_busch", DriverActiveSkillId.TauntingBow },
            { "cn_zhou_guanyu", DriverActiveSkillId.ChinaSpeed },
            { "cn_ma_qinghua", DriverActiveSkillId.ExtremeSwitch },
            { "jp_keiichi_tsuchiya", DriverActiveSkillId.SmokeScreen },
            { "jp_takumi_fujiwara", DriverActiveSkillId.GutterRun }
        };

    private static readonly Dictionary<string, DriverPassiveSkillId> PassiveByDriver =
        new Dictionary<string, DriverPassiveSkillId>(StringComparer.Ordinal)
        {
            { "uk_hunter_hart", DriverPassiveSkillId.HuntingInstinct },
            { "uk_nigel_mansell", DriverPassiveSkillId.LionsHeart },
            { "de_michael_schumacher", DriverPassiveSkillId.EngineersTune },
            { "de_sebastian_vettel", DriverPassiveSkillId.InformationSponge },
            { "it_alberto_ascari", DriverPassiveSkillId.BlueLuck },
            { "it_tazio_nuvolari", DriverPassiveSkillId.UnyieldingLegend },
            { "us_tony_stewart", DriverPassiveSkillId.TrackFury },
            { "us_kyle_busch", DriverPassiveSkillId.VillainAura },
            { "cn_zhou_guanyu", DriverPassiveSkillId.SteadyHeart },
            { "cn_ma_qinghua", DriverPassiveSkillId.AllRounder },
            { "jp_keiichi_tsuchiya", DriverPassiveSkillId.DriftInstinct },
            { "jp_takumi_fujiwara", DriverPassiveSkillId.WaterCupTraining }
        };

    public static DriverActiveSkillId GetSkill(string driverId)
    {
        return !string.IsNullOrEmpty(driverId) && SkillByDriver.TryGetValue(driverId, out DriverActiveSkillId skill)
            ? skill
            : DriverActiveSkillId.None;
    }

    public static DriverPassiveSkillId GetPassiveSkill(string driverId)
    {
        return !string.IsNullOrEmpty(driverId) && PassiveByDriver.TryGetValue(driverId, out DriverPassiveSkillId skill)
            ? skill
            : DriverPassiveSkillId.None;
    }

    public static bool CanActivate(DriverProfile profile, DriverSkillRuntimeState state,
        DriverSkillActivationContext context, out string reason)
    {
        if (profile == null || state == null || !state.Enabled)
        {
            reason = "当前模式禁用车手技能";
            return false;
        }
        if (state.Tier <= 0)
        {
            reason = "Lv3 解锁主动技能";
            return false;
        }
        if (state.UsesRemaining <= 0)
        {
            reason = "本场次数已用完";
            return false;
        }
        if (state.IsActive)
        {
            reason = "技能仍在持续";
            return false;
        }
        if (!context.CanAcceptInput)
        {
            reason = "仅可在档位确认前发动";
            return false;
        }

        switch (state.Skill)
        {
            case DriverActiveSkillId.FinalLap:
                if (context.TotalLaps <= 0 || context.Lap < context.TotalLaps - 1)
                {
                    reason = "仅限最后一圈";
                    return false;
                }
                break;
            case DriverActiveSkillId.FinalSprint:
                if (!IsEngineAtCriticalHeat(context.EngineRemaining, context.EngineCapacity))
                {
                    reason = "引擎热量达到 90% 后可用";
                    return false;
                }
                break;
            case DriverActiveSkillId.TauntingBow:
                if (context.NearbyOpponentsBehind < 1)
                {
                    reason = "身后 3 格内需要至少 1 名对手";
                    return false;
                }
                break;
        }

        reason = string.Empty;
        return true;
    }

    public static bool IsEngineAtCriticalHeat(int remaining, int capacity)
    {
        if (capacity <= 0) return false;
        int spent = Math.Max(0, capacity - Math.Max(0, remaining));
        return spent * 10 >= capacity * 9;
    }

    public static int GetDurationTurns(DriverActiveSkillId skill, int tier)
    {
        int t = Math.Max(1, Math.Min(3, tier));
        switch (skill)
        {
            case DriverActiveSkillId.PerfectRhythm: return t == 1 ? 3 : t == 2 ? 4 : 5;
            case DriverActiveSkillId.TrueSelf: return t == 1 ? 2 : 3;
            case DriverActiveSkillId.ChinaSpeed: return t == 1 ? 2 : 3;
            case DriverActiveSkillId.ExtremeSwitch: return t == 1 ? 3 : t == 2 ? 4 : int.MaxValue;
            default: return 1;
        }
    }

    /// <summary>
    /// Returns the turn cadence for a passive that prepares a concrete effect.
    /// Event-driven passives return zero and are armed by their event instead.
    /// </summary>
    public static int GetPassiveTriggerInterval(DriverPassiveSkillId skill, int tier)
    {
        int t = Math.Max(1, Math.Min(3, tier));
        switch (skill)
        {
            case DriverPassiveSkillId.EngineersTune: return t == 1 ? 3 : 2;
            case DriverPassiveSkillId.InformationSponge: return t == 1 ? 4 : t == 2 ? 3 : 2;
            default: return 0;
        }
    }

    public static int GetPassiveHeatDiscount(DriverPassiveSkillId skill, int tier)
    {
        return skill == DriverPassiveSkillId.EngineersTune && tier > 0 ? 1 : 0;
    }

    public static int GetPassiveDeckLookahead(DriverPassiveSkillId skill, int tier)
    {
        return skill == DriverPassiveSkillId.InformationSponge && tier > 0 ? 3 : 0;
    }

    public static int GetPassiveOvertakeMovementBonus(DriverPassiveSkillId skill, int tier)
    {
        if (skill != DriverPassiveSkillId.LionsHeart || tier <= 0) return 0;
        return tier >= 3 ? 2 : 1;
    }

    public static int GetPassiveOvertakeCoolingBonus(DriverPassiveSkillId skill, int tier)
    {
        return skill == DriverPassiveSkillId.LionsHeart && tier >= 2 ? 1 : 0;
    }

    public static int GetPassiveWeatherPenaltyReduction(DriverPassiveSkillId skill, int tier)
    {
        return skill == DriverPassiveSkillId.SteadyHeart && tier > 0 ? 1 : 0;
    }

    public static int GetPassiveCornerLimitBonus(DriverPassiveSkillId skill, int tier)
    {
        return skill == DriverPassiveSkillId.AllRounder && tier > 0 ? 1 : 0;
    }

    public static int GetPassiveCornerHeatReduction(DriverPassiveSkillId skill, int tier)
    {
        return skill == DriverPassiveSkillId.AllRounder && tier >= 2 ? 1 : 0;
    }

    public static int GetSpeedPerCardBonus(DriverSkillRuntimeState state)
    {
        return Active(state, DriverActiveSkillId.FinalLap) ? (state.Tier >= 2 ? 2 : 1) : 0;
    }

    public static int GetMovementBonus(DriverSkillRuntimeState state, bool crossedCorner)
    {
        if (Active(state, DriverActiveSkillId.AngryCharge)) return state.Tier >= 2 ? 2 : 1;
        if (Active(state, DriverActiveSkillId.FinalSprint)) return state.Tier >= 2 ? 3 : 2;
        if (Active(state, DriverActiveSkillId.TrueSelf) && !crossedCorner) return state.Tier >= 3 ? 3 : 2;
        if (Active(state, DriverActiveSkillId.TauntingBow) && !crossedCorner) return state.Tier >= 2 ? 3 : 2;
        return 0;
    }

    public static int GetOvertakeBonus(DriverSkillRuntimeState state, int overtakes)
    {
        return Active(state, DriverActiveSkillId.AngryCharge) && state.Tier >= 2 && overtakes > 0 ? 1 : 0;
    }

    public static int GetCornerLimitBonus(DriverSkillRuntimeState state)
    {
        return Active(state, DriverActiveSkillId.PrecisionCornering) && state.Tier >= 2 ? 1 : 0;
    }

    public static int ReduceCornerHeat(DriverSkillRuntimeState state, int heat)
    {
        if (Active(state, DriverActiveSkillId.PerfectLap)) return 0;
        int reduced = Active(state, DriverActiveSkillId.PrecisionCornering)
            ? Math.Max(0, heat - 1)
            : Math.Max(0, heat);
        if (state != null)
            reduced = Math.Max(0, reduced - state.ConsumePassiveCornerHeatReduction());
        return reduced;
    }

    public static int ApplyHeatMultiplier(DriverSkillRuntimeState state, int heat)
    {
        if (!Active(state, DriverActiveSkillId.FinalLap)) return Math.Max(0, heat);
        return state.Tier >= 3
            ? (int)Math.Ceiling(Math.Max(0, heat) * 1.5)
            : Math.Max(0, heat) * 2;
    }

    public static int ApplyGearHeatCost(DriverSkillRuntimeState state, int heatCost, bool isChinaAdditionalHeat)
    {
        int cost = Math.Max(0, heatCost);
        if (Active(state, DriverActiveSkillId.PerfectRhythm) && !isChinaAdditionalHeat)
            return 0;
        if (Active(state, DriverActiveSkillId.ExtremeSwitch))
            return state.Tier >= 3 ? 0 : cost / 2;
        return cost;
    }

    public static int GetSlipstreamBonus(DriverSkillRuntimeState state)
    {
        if (Active(state, DriverActiveSkillId.PerfectLap) && state.Tier >= 2) return 1;
        if (Active(state, DriverActiveSkillId.SmokeScreen) && state.Tier >= 3) return 2;
        return 0;
    }

    public static bool BlocksTrailingSlipstream(DriverSkillRuntimeState state)
    {
        return Active(state, DriverActiveSkillId.TauntingBow) ||
               Active(state, DriverActiveSkillId.SmokeScreen);
    }

    public static bool IsWeatherImmune(DriverSkillRuntimeState state)
    {
        return Active(state, DriverActiveSkillId.ChinaSpeed);
    }

    public static int GetIgnoredCornerCount(DriverSkillRuntimeState state)
    {
        if (Active(state, DriverActiveSkillId.FinalSprint)) return int.MaxValue;
        if (Active(state, DriverActiveSkillId.GutterRun)) return state.Tier >= 3 ? 2 : 1;
        return 0;
    }

    public static int GetGutterMovementPenalty(DriverSkillRuntimeState state)
    {
        return 0;
    }

    private static bool Active(DriverSkillRuntimeState state, DriverActiveSkillId skill)
    {
        return state != null && state.IsActive && state.Skill == skill;
    }
}
