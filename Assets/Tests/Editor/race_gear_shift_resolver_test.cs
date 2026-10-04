using System.Collections.Generic;
using NUnit.Framework;

public class RaceGearShiftResolverTests
{
    private static TeamGearRules.Resolution Shift(
        TeamId team, int gear, int consecutive, int requested) =>
        TeamGearRules.Resolve(team, gear, consecutive, requested, 1, 4, 1, 3, 1);

    [Test]
    public void StandardTwoGearCostIsPaidBeforeGearCommitAndSound()
    {
        var player = new PlayerState("Standard", false, 0, 1) { teamId = TeamId.DE };
        var events = new List<string>();

        RaceGearShiftResolver.Apply(player, Shift(TeamId.DE, 1, 0, 3),
            (amount, reason) =>
            {
                events.Add($"pay:{amount}:{reason}:gear={player.gear}");
                return true;
            },
            () => events.Add($"shift:gear={player.gear}"),
            () => events.Add("failure"));

        CollectionAssert.AreEqual(new[] {
            "pay:1:shift 2 gears:gear=1", "shift:gear=3"
        }, events);
        Assert.That(player.gear, Is.EqualTo(3));
        Assert.That(player.selectedGearThisTurn, Is.EqualTo(3));
        Assert.That(player.chinaConsecutiveGearCount, Is.Zero);
    }

    [Test]
    public void HoldingGearStillRunsZeroCostPaymentWithoutShiftSound()
    {
        var player = new PlayerState("Hold", false, 0, 2) { teamId = TeamId.DE };
        int payments = 0;

        RaceGearShiftResolver.Apply(player, Shift(TeamId.DE, 2, 0, 2),
            (amount, reason) =>
            {
                Assert.That(amount, Is.Zero);
                Assert.That(reason, Is.EqualTo("shift 2 gears"));
                payments++;
                return true;
            },
            () => Assert.Fail("Holding gear cannot play shift sound"),
            () => Assert.Fail("Holding gear cannot fail"));

        Assert.That(payments, Is.EqualTo(1));
        Assert.That(player.selectedGearThisTurn, Is.EqualTo(2));
    }

    [TestCase(false, 1)]
    [TestCase(true, 0)]
    public void FailedInitialPaymentKeepsRecoveryGearAndOnlyHumanFailureSound(
        bool isAI, int expectedFailureSounds)
    {
        var player = new PlayerState("Failure", isAI, 0, 2)
        { teamId = TeamId.DE, chinaConsecutiveGearCount = 4 };
        int failures = 0;

        RaceGearShiftResolver.Apply(player, Shift(TeamId.DE, 2, 4, 4),
            (amount, _) =>
            {
                Assert.That(amount, Is.EqualTo(1));
                player.gear = 1; // The coordinator's failed payment has spun the car.
                player.chinaConsecutiveGearCount = 0;
                return false;
            },
            () => Assert.Fail("Failed shift cannot play success sound"),
            () => failures++);

        Assert.That(failures, Is.EqualTo(expectedFailureSounds));
        Assert.That(player.gear, Is.EqualTo(1));
        Assert.That(player.selectedGearThisTurn, Is.EqualTo(1));
    }

    [Test]
    public void ChinaFirstGoCommitsChainWithoutOverclockPayment()
    {
        var player = new PlayerState("China", false, 0, ChinaGearShiftRules.RecoverGear)
        { teamId = TeamId.CN };
        int payments = 0;
        int shiftSounds = 0;

        RaceGearShiftResolver.Apply(player,
            Shift(TeamId.CN, ChinaGearShiftRules.RecoverGear, 0, ChinaGearShiftRules.GoGear),
            (amount, _) => { Assert.That(amount, Is.Zero); payments++; return true; },
            () => shiftSounds++, () => Assert.Fail("First Go cannot fail"));

        Assert.That(payments, Is.EqualTo(1));
        Assert.That(shiftSounds, Is.EqualTo(1));
        Assert.That(player.gear, Is.EqualTo(ChinaGearShiftRules.GoGear));
        Assert.That(player.chinaConsecutiveGearCount, Is.EqualTo(1));
        Assert.That(player.selectedGearThisTurn, Is.EqualTo(player.gear));
    }

    [Test]
    public void ChinaOverclockPaysAfterCommitAndRecordsRecoveryOnSpin()
    {
        var player = new PlayerState("China", false, 0, ChinaGearShiftRules.GoGear)
        { teamId = TeamId.CN, chinaConsecutiveGearCount = 1 };
        var events = new List<string>();

        RaceGearShiftResolver.Apply(player,
            Shift(TeamId.CN, ChinaGearShiftRules.GoGear, 1, ChinaGearShiftRules.GoGear),
            (amount, reason) =>
            {
                events.Add($"pay:{amount}:{reason}:chain={player.chinaConsecutiveGearCount}");
                if (amount == 0) return true;
                player.gear = ChinaGearShiftRules.RecoverGear;
                player.chinaConsecutiveGearCount = 0;
                return false;
            },
            () => Assert.Fail("Go to Go does not change gear"),
            () => Assert.Fail("Overclock failure does not use initial shift failure sound"));

        CollectionAssert.AreEqual(new[] {
            "pay:0:shift 2 gears:chain=1", "pay:1:Go overclock:chain=2"
        }, events);
        Assert.That(player.selectedGearThisTurn, Is.EqualTo(ChinaGearShiftRules.RecoverGear));
        Assert.That(player.chinaConsecutiveGearCount, Is.Zero);
    }
}
