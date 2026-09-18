using NUnit.Framework;

public class ChinaGearRulesTests
{
    [TestCase(1, 0, 6, 1, false, TestName = "china_ai_accepts_one_affordable_corner_heat")]
    [TestCase(2, 0, 6, 1, true, TestName = "china_ai_rejects_corner_heat_above_tolerance")]
    [TestCase(1, 1, 1, 1, true, TestName = "china_ai_rejects_unpayable_combined_heat")]
    [TestCase(-1, -1, -1, -1, false, TestName = "china_ai_clamps_negative_heat_inputs")]
    public void china_ai_corner_heat_budget_is_safe(
        int cornerHeat,
        int committedHeat,
        int availableHeat,
        int tolerance,
        bool expected)
    {
        Assert.AreEqual(expected, ChinaGearShiftRules.ShouldForceRecoverForCorner(
            cornerHeat, committedHeat, availableHeat, tolerance));
    }

    [Test]
    public void first_go_uses_three_cards_without_heat()
    {
        ChinaGearShiftRules.Result result = ChinaGearShiftRules.Resolve(
            ChinaGearShiftRules.RecoverGear, 0, ChinaGearShiftRules.GoGear);

        Assert.AreEqual(ChinaGearShiftRules.GoGear, result.TargetGear);
        Assert.AreEqual(1, result.ConsecutiveCount);
        Assert.AreEqual(3, result.SpeedCardCount);
        Assert.AreEqual(0, result.AdditionalHeat);
        Assert.AreEqual(0, result.Cooldown);
    }

    [Test]
    public void consecutive_go_overclocks_to_four_cards_and_heat()
    {
        ChinaGearShiftRules.Result result = ChinaGearShiftRules.Resolve(
            ChinaGearShiftRules.GoGear, 1, ChinaGearShiftRules.GoGear);

        Assert.AreEqual(2, result.ConsecutiveCount);
        Assert.AreEqual(4, result.SpeedCardCount);
        Assert.AreEqual(1, result.AdditionalHeat);
    }

    [Test]
    public void recover_cooling_decays_and_mode_switch_resets_chain()
    {
        ChinaGearShiftRules.Result secondRecover = ChinaGearShiftRules.Resolve(
            ChinaGearShiftRules.RecoverGear, 1, ChinaGearShiftRules.RecoverGear);
        Assert.AreEqual(2, secondRecover.ConsecutiveCount);
        Assert.AreEqual(2, secondRecover.Cooldown);

        ChinaGearShiftRules.Result switched = ChinaGearShiftRules.Resolve(
            ChinaGearShiftRules.RecoverGear, 2, ChinaGearShiftRules.GoGear);
        Assert.AreEqual(1, switched.ConsecutiveCount);
        Assert.AreEqual(3, switched.SpeedCardCount);
        Assert.AreEqual(0, switched.AdditionalHeat);
    }

    [Test]
    public void team_facade_keeps_standard_four_gear_rules()
    {
        TeamGearRules.Resolution standard = TeamGearRules.Resolve(
            TeamId.UK, 1, 0, 3, 1, 4, 1, 3, 1);
        Assert.AreEqual(3, standard.TargetGear);
        Assert.AreEqual(1, standard.HeatCost);
        Assert.AreEqual(3, standard.CardCount);
        Assert.IsFalse(standard.IsChina);
    }

    [Test]
    public void china_facade_uses_dual_gear_card_limits()
    {
        Assert.AreEqual(3, TeamGearRules.GetSpeedCardCount(
            TeamId.CN, ChinaGearShiftRules.GoGear, 1, 0));
        Assert.AreEqual(1, TeamGearRules.GetSpeedCardCount(
            TeamId.CN, ChinaGearShiftRules.RecoverGear, 4, 0));
        Assert.AreEqual(0, TeamGearRules.GetCooldown(
            TeamId.CN, ChinaGearShiftRules.GoGear, 1, 3, 1));
        Assert.AreEqual(3, TeamGearRules.GetCooldown(
            TeamId.CN, ChinaGearShiftRules.RecoverGear, 1, 3, 1));
    }

    [Test]
    public void effective_limit_separates_go_base_cards_from_extra_slot()
    {
        TeamGearRules.SpeedCardRequirement firstGo = TeamGearRules.GetSpeedCardRequirement(
            TeamId.CN, ChinaGearShiftRules.GoGear, 1, 0);
        TeamGearRules.SpeedCardRequirement firstGoWithExtraSlot = TeamGearRules.GetSpeedCardRequirement(
            TeamId.CN, ChinaGearShiftRules.GoGear, 1, 1);
        TeamGearRules.SpeedCardRequirement secondGo = TeamGearRules.GetSpeedCardRequirement(
            TeamId.CN, ChinaGearShiftRules.GoGear, 2, 0);

        Assert.AreEqual(3, firstGo.BaseCardCount);
        Assert.AreEqual(3, firstGo.TotalCardCount);
        Assert.AreEqual(3, firstGoWithExtraSlot.BaseCardCount);
        Assert.AreEqual(1, firstGoWithExtraSlot.ExtraCardCount);
        Assert.AreEqual(3, firstGoWithExtraSlot.RequiredCardCount);
        Assert.AreEqual(4, firstGoWithExtraSlot.TotalCardCount);
        Assert.AreEqual(4, secondGo.BaseCardCount);
        Assert.AreEqual(4, secondGo.TotalCardCount);
    }

    [Test]
    public void china_ai_alternates_and_recovers_when_hot()
    {
        Assert.AreEqual(ChinaGearShiftRules.GoGear,
            ChinaGearShiftRules.ChooseAiGear(ChinaGearShiftRules.RecoverGear, 1, 4, 0.1f, 0.7f));
        Assert.AreEqual(ChinaGearShiftRules.RecoverGear,
            ChinaGearShiftRules.ChooseAiGear(ChinaGearShiftRules.GoGear, 3, 4, 0.1f, 0.7f));
        Assert.AreEqual(ChinaGearShiftRules.RecoverGear,
            ChinaGearShiftRules.ChooseAiGear(ChinaGearShiftRules.GoGear, 2, 4, 0.1f, 0.7f));
        Assert.AreEqual(ChinaGearShiftRules.RecoverGear,
            ChinaGearShiftRules.ChooseAiGear(ChinaGearShiftRules.GoGear, 1, 4, 0.8f, 0.7f));
    }
}
