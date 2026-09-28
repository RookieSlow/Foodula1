using NUnit.Framework;
using UnityEngine;

public class SpeedCardRequirementBoundaryTests
{
    [TestCase(TeamId.UK, 1, 0, 0, 1, "G1 档")]
    [TestCase(TeamId.DE, 2, 0, 1, 2, "G2 档（基础 2 + 额外 1）")]
    [TestCase(TeamId.IT, 3, 0, 0, 3, "G3 档")]
    [TestCase(TeamId.US, 4, 0, 2, 4, "G4 档（基础 4 + 额外 2）")]
    [TestCase(TeamId.JP, 1, 0, 2, 1, "G1 档（基础 1 + 额外 2）")]
    [TestCase(TeamId.CN, 1, 1, 0, 1, "Recover 档")]
    [TestCase(TeamId.CN, 2, 1, 0, 3, "Go 档")]
    [TestCase(TeamId.CN, 2, 2, 1, 4, "Go 档（基础 4 + 额外 1）")]
    [TestCase(TeamId.CN, 2, 3, -2, 4, "Go 档")]
    public void RequirementAndCopyPreserveCurrentTurnContract(
        TeamId team, int gear, int consecutive, int extra, int required, string label)
    {
        var player = new PlayerState("Player", false, 0, gear)
        {
            teamId = team, chinaConsecutiveGearCount = consecutive,
            extraCardSlotsThisTurn = extra
        };
        TeamGearRules.SpeedCardRequirement result = CardPlayRules.GetSpeedCardRequirement(player);
        Assert.AreEqual(required, result.RequiredCardCount);
        Assert.AreEqual(required + System.Math.Max(0, extra), result.TotalCardCount);
        Assert.AreEqual(label, GearRequirementFeedbackRules.FormatRequirementLabel(team, gear, result));
        Assert.AreEqual(extra, player.extraCardSlotsThisTurn, "Reading must not consume carry-over slots.");
        Assert.AreEqual(consecutive, player.chinaConsecutiveGearCount);
    }

    [Test]
    public void SubsequentReadsReflectLiveGearAndExtraSlots()
    {
        var player = new PlayerState("Player", false, 0, 1) { teamId = TeamId.CN };
        Assert.AreEqual(1, CardPlayRules.GetSpeedCardRequirement(player).RequiredCardCount);
        player.gear = 2;
        player.chinaConsecutiveGearCount = 1;
        player.extraCardSlotsThisTurn = 2;
        var requirement = CardPlayRules.GetSpeedCardRequirement(player);
        Assert.AreEqual(3, requirement.RequiredCardCount);
        Assert.AreEqual(5, requirement.TotalCardCount);
    }

    [TestCase(TeamId.US, 2, 0, 1)]
    [TestCase(TeamId.CN, 2, 2, 1)]
    [TestCase(TeamId.JP, 1, 0, 2)]
    public void ManagerCompatibilityFacadeMatchesPureBoundary(TeamId team, int gear, int consecutive, int extra)
    {
        var host = new GameObject("RequirementBoundaryTest");
        host.SetActive(false);
        try
        {
            var manager = host.AddComponent<MVPGameManager>();
            var player = new PlayerState("Player", false, 0, gear)
            {
                teamId = team, chinaConsecutiveGearCount = consecutive,
                extraCardSlotsThisTurn = extra
            };
            var requirement = CardPlayRules.GetSpeedCardRequirement(player);
            Assert.AreEqual(requirement.RequiredCardCount, manager.GetRequiredSpeedCardsThisTurn(player));
            Assert.AreEqual(requirement.TotalCardCount, manager.GetMaxSpeedCardsThisTurn(player));
            Assert.AreEqual(GearRequirementFeedbackRules.FormatRequirementLabel(team, gear, requirement),
                manager.GetSpeedCardRequirementLabel(player));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }
}
