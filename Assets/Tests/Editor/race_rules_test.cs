using System.Collections.Generic;
using NUnit.Framework;

public class RaceRulesTests
{
    private const int MinimumGear = 1;
    private const int MaximumGear = 4;
    private const int TwoGearShiftHeatCost = 1;

    [Test]
    public void test_one_gear_shift_is_free()
    {
        GearShiftResult result = RaceRules.ResolveGearShift(
            2,
            3,
            MinimumGear,
            MaximumGear,
            TwoGearShiftHeatCost);

        Assert.That(result.TargetGear, Is.EqualTo(3));
        Assert.That(result.HeatCost, Is.Zero);
    }

    [Test]
    public void test_large_gear_shift_is_limited_to_two_gears()
    {
        GearShiftResult result = RaceRules.ResolveGearShift(
            MinimumGear,
            MaximumGear,
            MinimumGear,
            MaximumGear,
            TwoGearShiftHeatCost);

        Assert.That(result.TargetGear, Is.EqualTo(3));
        Assert.That(result.HeatCost, Is.EqualTo(TwoGearShiftHeatCost));
    }

    [TestCase(1, 3)]
    [TestCase(2, 1)]
    [TestCase(3, 0)]
    [TestCase(4, 0)]
    public void test_cooldown_uses_configured_values(int gear, int expectedCooldown)
    {
        int cooldown = RaceRules.GetCooldown(gear, 3, 1);

        Assert.That(cooldown, Is.EqualTo(expectedCooldown));
    }

    [Test]
    public void test_card_values_are_summed()
    {
        var cards = new List<CardData>
        {
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 4)
        };

        Assert.That(RaceRules.SumCardValues(cards), Is.EqualTo(6));
    }

    [TestCase(4, 2, 2)]
    [TestCase(2, 3, 0)]
    public void test_missing_speed_card_count_never_goes_negative(
        int gear,
        int selectedCardCount,
        int expectedMissingCount)
    {
        int missingCount = RaceRules.GetMissingSpeedCardCount(gear, selectedCardCount);

        Assert.That(missingCount, Is.EqualTo(expectedMissingCount));
    }
}
