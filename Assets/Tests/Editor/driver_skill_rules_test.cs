using System.Collections.Generic;
using NUnit.Framework;

public class DriverSkillRulesTests
{
    [Test]
    public void EveryDriverMapsToOneUniqueActiveSkill()
    {
        var skills = new HashSet<DriverActiveSkillId>();
        foreach (DriverProfile driver in DriverCatalog.All)
        {
            DriverActiveSkillId skill = DriverSkillRules.GetSkill(driver.Id);
            Assert.That(skill, Is.Not.EqualTo(DriverActiveSkillId.None), driver.Id);
            Assert.That(skills.Add(skill), Is.True, driver.Id);
        }
        Assert.That(skills.Count, Is.EqualTo(12));
    }

    [Test]
    public void EveryDriverMapsToOneUniquePassiveSkill()
    {
        var skills = new HashSet<DriverPassiveSkillId>();
        foreach (DriverProfile driver in DriverCatalog.All)
        {
            DriverPassiveSkillId skill = DriverSkillRules.GetPassiveSkill(driver.Id);
            Assert.That(skill, Is.Not.EqualTo(DriverPassiveSkillId.None), driver.Id);
            Assert.That(skills.Add(skill), Is.True, driver.Id);
        }
        Assert.That(skills.Count, Is.EqualTo(12));
    }

    [Test]
    public void LockedAndTutorialDisabledSkillsExplainWhyTheyCannotActivate()
    {
        DriverProfile driver = DriverCatalog.GetDefaultForTeam(TeamId.DE);
        var state = new DriverSkillRuntimeState();
        var context = Context(canInput: true);

        state.Initialize(driver, 2, true);
        Assert.That(DriverSkillRules.CanActivate(driver, state, context, out string locked), Is.False);
        Assert.That(locked, Does.Contain("Lv3"));

        state.Initialize(driver, 7, false);
        Assert.That(DriverSkillRules.CanActivate(driver, state, context, out string disabled), Is.False);
        Assert.That(disabled, Does.Contain("禁用"));
    }

    [TestCase("uk_hunter_hart", 1, 3, 8, 10, 0, false)]
    [TestCase("uk_hunter_hart", 2, 3, 8, 10, 0, true)]
    [TestCase("it_tazio_nuvolari", 0, 3, 2, 20, 0, true)]
    [TestCase("it_tazio_nuvolari", 0, 3, 3, 20, 0, false)]
    [TestCase("us_kyle_busch", 0, 3, 8, 10, 0, false)]
    [TestCase("us_kyle_busch", 0, 3, 8, 10, 1, true)]
    public void ConditionalSkillsUseRaceFacts(string driverId, int lap, int laps,
        int remaining, int capacity, int opponentsBehind, bool expected)
    {
        DriverCatalog.TryGet(driverId, out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);
        var context = new DriverSkillActivationContext(
            true, lap, laps, remaining, capacity, opponentsBehind);
        Assert.That(DriverSkillRules.CanActivate(driver, state, context, out _), Is.EqualTo(expected));
    }

    [Test]
    public void RedesignedSkillsHaveImmediateRulesBackedEffects()
    {
        DriverSkillRuntimeState mansell = Activate("uk_nigel_mansell", 5);
        Assert.That(DriverSkillRules.GetMovementBonus(mansell, false), Is.EqualTo(2));
        Assert.That(DriverSkillRules.GetOvertakeBonus(mansell, 1), Is.EqualTo(1));

        DriverSkillRuntimeState vettel = Activate("de_sebastian_vettel", 5);
        Assert.That(DriverSkillRules.ApplyGearHeatCost(vettel, 2, false), Is.Zero);
        Assert.That(vettel.ActiveTurnsRemaining, Is.EqualTo(4));

        DriverSkillRuntimeState zhou = Activate("cn_zhou_guanyu", 5);
        Assert.That(DriverSkillRules.IsWeatherImmune(zhou), Is.True);
        Assert.That(zhou.ActiveTurnsRemaining, Is.EqualTo(3));
    }

    [Test]
    public void RedesignedPassiveRulesHavePracticalEffects()
    {
        Assert.That(DriverSkillRules.GetPassiveSkill("de_michael_schumacher"),
            Is.EqualTo(DriverPassiveSkillId.EngineersTune));
        Assert.That(DriverSkillRules.GetPassiveTriggerInterval(
            DriverPassiveSkillId.EngineersTune, 1), Is.EqualTo(3));
        Assert.That(DriverSkillRules.GetPassiveTriggerInterval(
            DriverPassiveSkillId.EngineersTune, 3), Is.EqualTo(2));
        Assert.That(DriverSkillRules.GetPassiveHeatDiscount(
            DriverPassiveSkillId.EngineersTune, 1), Is.EqualTo(1));

        Assert.That(DriverSkillRules.GetPassiveDeckLookahead(
            DriverPassiveSkillId.InformationSponge, 1), Is.EqualTo(3));
        Assert.That(DriverSkillRules.GetPassiveTriggerInterval(
            DriverPassiveSkillId.InformationSponge, 3), Is.EqualTo(2));

        Assert.That(DriverSkillRules.GetPassiveOvertakeMovementBonus(
            DriverPassiveSkillId.LionsHeart, 1), Is.EqualTo(1));
        Assert.That(DriverSkillRules.GetPassiveOvertakeMovementBonus(
            DriverPassiveSkillId.LionsHeart, 3), Is.EqualTo(2));
        Assert.That(DriverSkillRules.GetPassiveOvertakeCoolingBonus(
            DriverPassiveSkillId.LionsHeart, 2), Is.EqualTo(1));

        Assert.That(DriverSkillRules.GetPassiveWeatherPenaltyReduction(
            DriverPassiveSkillId.SteadyHeart, 1), Is.EqualTo(1));
        Assert.That(DriverSkillRules.GetPassiveCornerLimitBonus(
            DriverPassiveSkillId.AllRounder, 1), Is.EqualTo(1));
        Assert.That(DriverSkillRules.GetPassiveCornerHeatReduction(
            DriverPassiveSkillId.AllRounder, 2), Is.EqualTo(1));
    }

    [Test]
    public void CoreActiveModifiersMatchTheirDocumentedTiers()
    {
        DriverSkillRuntimeState hunter = Activate("uk_hunter_hart", 7,
            new DriverSkillActivationContext(true, 2, 3, 10, 10, 0));
        Assert.That(DriverSkillRules.GetSpeedPerCardBonus(hunter), Is.EqualTo(2));
        Assert.That(DriverSkillRules.ApplyHeatMultiplier(hunter, 2), Is.EqualTo(3));

        DriverSkillRuntimeState schumacher = Activate("de_michael_schumacher", 5);
        Assert.That(DriverSkillRules.ReduceCornerHeat(schumacher, 4), Is.Zero);
        Assert.That(DriverSkillRules.GetSlipstreamBonus(schumacher), Is.EqualTo(1));

        DriverSkillRuntimeState ascari = Activate("it_alberto_ascari", 5);
        Assert.That(DriverSkillRules.ReduceCornerHeat(ascari, 3), Is.EqualTo(2));
        Assert.That(DriverSkillRules.GetCornerLimitBonus(ascari), Is.EqualTo(1));

        DriverSkillRuntimeState takumi = Activate("jp_takumi_fujiwara", 7);
        Assert.That(DriverSkillRules.GetIgnoredCornerCount(takumi), Is.EqualTo(2));
        Assert.That(DriverSkillRules.GetGutterMovementPenalty(takumi), Is.Zero);
    }

    private static DriverSkillRuntimeState Activate(string driverId, int level,
        DriverSkillActivationContext? supplied = null)
    {
        DriverCatalog.TryGet(driverId, out DriverProfile driver);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, level, true);
        DriverSkillActivationContext context = supplied ?? Context(canInput: true);
        Assert.That(state.TryActivate(driver, context, out string reason), Is.True, reason);
        return state;
    }

    private static DriverSkillActivationContext Context(bool canInput)
    {
        return new DriverSkillActivationContext(canInput, 2, 3, 1, 10, 3);
    }
}
