using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RaceRecoverySkipAdapterTests
{
    [TestCase(TeamId.UK, 2)]
    [TestCase(TeamId.DE, 2)]
    [TestCase(TeamId.IT, 2)]
    [TestCase(TeamId.US, 2)]
    [TestCase(TeamId.CN, ChinaGearShiftRules.RecoverGear)]
    [TestCase(TeamId.JP, 2)]
    public void SessionConsumesOnlyRecoverySkipAndRestoresTeamGear(TeamId team, int expectedGear)
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var player = new PlayerState("driver", false, 8, 4)
        {
            teamId = team,
            skipNextTurn = true,
            chinaConsecutiveGearCount = 3,
            pitStopRequested = true,
            kantoOdenSkipThisTurn = true
        };

        Assert.IsTrue(session.ConsumeRecoverySkip(player, 2));
        Assert.IsFalse(player.skipNextTurn);
        Assert.AreEqual(expectedGear, player.gear);
        Assert.AreEqual(0, player.chinaConsecutiveGearCount);
        Assert.IsTrue(player.pitStopRequested);
        Assert.IsTrue(player.kantoOdenSkipThisTurn);
        Assert.AreEqual(8, player.position);
        Assert.IsFalse(session.ConsumeRecoverySkip(player, 2), "A consumed recovery cannot run twice.");
    }

    [Test]
    public void KantoOdenOnlyDoesNotConsumeRecoveryOrChangeGear()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var player = new PlayerState("driver", false, 8, 3)
        {
            teamId = TeamId.JP,
            kantoOdenSkipThisTurn = true,
            chinaConsecutiveGearCount = 2
        };

        Assert.IsFalse(session.ConsumeRecoverySkip(player, 1));
        Assert.AreEqual(3, player.gear);
        Assert.AreEqual(2, player.chinaConsecutiveGearCount);
        Assert.IsTrue(player.kantoOdenSkipThisTurn);
        Assert.IsFalse(session.ConsumeRecoverySkip(null, 1));
    }

    [TestCase(TeamId.UK, 2)]
    [TestCase(TeamId.CN, ChinaGearShiftRules.RecoverGear)]
    public void RealCoordinatorDelegatesRecoveryButLeavesOtherSkipCauseAlone(TeamId team, int expectedGear)
    {
        var host = new GameObject("Recovery skip adapter test");
        host.SetActive(false);
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            var manager = host.AddComponent<MVPGameManager>();
            manager.config = config;
            config.minGear = 2;
            var session = new RaceSession(new SystemRandomSource(1));
            var player = new PlayerState("driver", false, 8, 4)
            {
                teamId = team,
                skipNextTurn = true,
                chinaConsecutiveGearCount = 3,
                pitStopScheduled = true
            };
            session.Players.Add(player);
            const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
            typeof(MVPGameManager).GetField("session", flags).SetValue(manager, session);
            MethodInfo resolve = typeof(MVPGameManager).GetMethod("ResolveSkip", flags);
            Assert.IsNotNull(resolve, "Coordinator adapter changed; update this fixture.");

            resolve.Invoke(manager, new object[] { player });

            Assert.IsFalse(player.skipNextTurn);
            Assert.AreEqual(expectedGear, player.gear);
            Assert.AreEqual(0, player.chinaConsecutiveGearCount);
            Assert.IsTrue(player.pitStopScheduled, "The scheduled pit is a separate A1 decision.");
            Assert.AreEqual(8, player.position);
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(config);
        }
    }
}
